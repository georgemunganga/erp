using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private readonly List<RoundPlanChange> stagedRoundPlans = [];
    public void StageCommercialRoundPlan(RoundPlanChange change)
    {
        if (change is null || stagedRoundPlans.Any(x => x.Plan.Id == change.Plan.Id)
            || change.Revision.ActorPrincipalId != scope.SubjectId || !CommercialRoundPlanDraft.SameScope(change.Plan, change.Revision))
            throw new InvalidOperationException("RoundPlanStagingInvalid");
        if (change.Enrollment is { } enrollment) { AddScoped(enrollment); AddScoped(change.Plan); }
        else
        {
            if (Entry(change.Plan).State == EntityState.Detached
                || change.Revision.Operation is not ("Revise" or "Cancel" or "Submit" or "Return" or "Reject" or "Approve")
                || change.Revision.Operation is ("Revise" or "Submit") && change.Plan.Status != "Draft"
                || change.Revision.Operation == "Cancel" && change.Plan.Status is not ("Draft" or "Rejected")
                || change.Revision.Operation is ("Return" or "Reject" or "Approve") && change.Plan.Status != "InReview")
                throw new InvalidOperationException("RoundPlanTrackedDraftRequired");
            change.Plan.Status = change.Revision.ResultStatus;
            change.Plan.UpdatedAt = change.Revision.RecordedAt;
            Entry(change.Plan).Property(x => x.UpdatedAt).IsModified = true;
        }
        AddScoped(change.Revision);
        foreach (var row in change.Criteria) AddScoped(row);
        foreach (var row in change.Participants) AddScoped(row);
        AddScoped(change.Receipt);
        stagedRoundPlans.Add(change);
    }
    private static bool RoundRecord(ProcurementRecord row) => row is CommercialRoundEnrollment or CommercialRoundPlan
        or CommercialRoundPlanRevision or CommercialRoundCriterion or CommercialRoundParticipant or CommercialRoundPlanReceipt;
    private void GuardCommercialRoundPlans()
    {
        var changes = ChangeTracker.Entries<ProcurementRecord>().Where(x => RoundRecord(x.Entity)
            && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (changes.Length == 0) return;
        var expected = stagedRoundPlans.SelectMany(x => new ProcurementRecord[] { x.Plan, x.Revision, x.Receipt }
            .Concat(x.Enrollment is null ? [] : new ProcurementRecord[] { x.Enrollment })
            .Concat(x.Criteria).Concat(x.Participants)).ToArray();
        if (changes.Length != expected.Length || changes.Any(x => !expected.Any(y => ReferenceEquals(y, x.Entity))
            || x.State == EntityState.Deleted || x.State == EntityState.Modified && x.Entity is not CommercialRoundPlan))
            throw new InvalidOperationException("RoundPlanOwnedMutationRequired");
        foreach (var value in stagedRoundPlans)
        {
            var plan = value.Plan; var revision = value.Revision; var receipt = value.Receipt;
            _ = CommercialRoundPlanDraft.Restore(revision, value.Criteria, value.Participants);
            CommercialRoundPlanDraft.VerifyReceipt(receipt, plan, revision);
            if (plan.TenantId != scope.TenantId || plan.LegalEntityId != scope.LegalEntityId
                || plan.Version != revision.ResultVersion || plan.Status != revision.ResultStatus
                || receipt.ActorPrincipalId != scope.SubjectId || receipt.PlanId != plan.Id || receipt.RevisionId != revision.Id
                || receipt.SourcingEventId != plan.SourcingEventId || receipt.BranchId != plan.BranchId
                || revision.SourcingEventId != plan.SourcingEventId || revision.BranchId != plan.BranchId
                || receipt.ResultVersion != revision.ResultVersion || receipt.Operation != revision.Operation
                || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
                || receipt.ResponseStatusCode != (revision.Operation is "Create" or "Submit" ? 201 : 200)
                || expected.Any(x => x.TenantId != scope.TenantId || x.LegalEntityId != scope.LegalEntityId))
                throw new InvalidOperationException("RoundPlanOwnedGraphInvalid");
            if (value.Enrollment is { } enrollment && (revision.Operation != "Create" || plan.EnrollmentId != enrollment.Id
                || enrollment.SourcingEventId != plan.SourcingEventId || enrollment.BranchId != plan.BranchId
                || enrollment.EnrolledByPrincipalId != scope.SubjectId || plan.MakerPrincipalId != scope.SubjectId))
                throw new InvalidOperationException("RoundPlanEnrollmentGraphInvalid");
            if (Entry(plan).State == EntityState.Modified)
                foreach (var property in Entry(plan).Properties.Where(x => x.IsModified))
                    if (property.Metadata.Name is not (nameof(CommercialRoundPlan.Status) or nameof(CommercialRoundPlan.Version)
                        or nameof(CommercialRoundPlan.UpdatedAt) or nameof(CommercialRoundPlan.UpdatedBy)))
                        throw new InvalidOperationException("RoundPlanIdentityImmutable");
        }
    }
}
