using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalRfqCommercialRoundOpening(ApprovalCase Case,
    string SnapshotJson, string SnapshotSha256,
    IReadOnlyList<Guid> SupplierIds, DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt, string ClosingOfficerPrincipalId,
    Guid? ClosingOfficerWorkerAliasId);

public sealed record PrincipalRfqCommercialRoundTerminal(string State,
    Guid CoreDecisionId, string ActorPrincipalId, string? Reason,
    PrincipalRfqPublishedRoundProof? AcceptingRound);

/// <summary>A distinct Core review case for one prepared commercial plan.
/// Pure transitions do not authenticate policy or Core database provenance;
/// the owning adapter must reread both under the shared transaction.</summary>
public static class PrincipalRfqCommercialRoundCoreWorkflow
{
    public const string SourceType = "sourcing_commercial_round";

    public static PrincipalRfqCommercialRoundOpening Open(
        PrincipalRfqPreparedPlan prepared, PrincipalRfqCommercialDraftHead head,
        PrincipalRfqCommercialDraftRevision revision, SourcingEvent ev,
        RfqPrincipalDraft original, PolicyVersion policy,
        IReadOnlyList<RfqPrincipalActor> currentReviewers,
        RfqPrincipalActor closer, IReadOnlyList<Guid> invitedSupplierIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(ev);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(currentReviewers);
        ArgumentNullException.ThrowIfNull(closer);
        ArgumentNullException.ThrowIfNull(invitedSupplierIds);
        PrincipalRfqPreparedPlanEvidence.Verify(prepared, head, revision);
        var rules = PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(
            policy, prepared.TenantId, prepared.LegalEntityId, now);
        Need(now != default && now.Offset == TimeSpan.Zero
            && ev.ExecutionVersion == 2 && ev.Status == "Published"
            && ev.Id == prepared.SourcingEventId && ev.TenantId == prepared.TenantId
            && ev.LegalEntityId == prepared.LegalEntityId && ev.BranchRef == prepared.BranchId
            && ev.CurrencyCode == original.CurrencyCode
            && ev.OriginalDraftFingerprint == original.OriginalFingerprint
            && original.Id == ev.Id && original.BranchId == prepared.BranchId
            && original.MakerPrincipalId == prepared.ActorPrincipalId
            && prepared.DocumentCount >= 0 && prepared.PolicyVersionId != Guid.Empty
            && prepared.PreparedAt <= now,
            "RfqCommercialRoundSourceChanged");
        Need(invitedSupplierIds.Count is >= 1 and <= 100
            && invitedSupplierIds.All(id => id != Guid.Empty)
            && invitedSupplierIds.Distinct().Count() == invitedSupplierIds.Count,
            "RfqCommercialRoundSupplierSetInvalid");
        using var candidate = JsonDocument.Parse(prepared.SnapshotJson);
        var source = candidate.RootElement.GetProperty("candidateSnapshot");
        var opens = source.GetProperty("opensAt").GetDateTimeOffset();
        var closes = source.GetProperty("closesAt").GetDateTimeOffset();
        Need(opens.Offset == TimeSpan.Zero && closes.Offset == TimeSpan.Zero
            && now < opens && opens < closes
            && source.GetProperty("eventId").GetGuid() == ev.Id
            && source.GetProperty("originalFingerprint").GetString() == original.OriginalFingerprint
            && source.GetProperty("comparisonCurrency").GetString() == ev.CurrencyCode,
            "RfqCommercialRoundWindowOrPlanChanged");
        var expected = rules.Stages.SelectMany(x => x.PrincipalIds)
            .Distinct(StringComparer.Ordinal).ToArray();
        Need(expected.Length is >= 1 and <= 100
            && expected.Length == rules.Stages.Sum(x => x.PrincipalIds.Count)
            && currentReviewers.Count == expected.Length
            && currentReviewers.Select(x => x.PrincipalId)
                .ToHashSet(StringComparer.Ordinal).SetEquals(expected)
            && currentReviewers.Select(x => x.PrincipalId)
                .Distinct(StringComparer.Ordinal).Count() == expected.Length,
            "RfqCommercialRoundReviewerSetChanged");
        var people = currentReviewers.ToDictionary(x => x.PrincipalId,
            StringComparer.Ordinal);
        foreach (var reviewer in currentReviewers)
        {
            Need(ProcurementPrincipalEvidence.Canonical(reviewer.PrincipalId)
                && ProcurementPrincipalEvidence.OptionalWorker(reviewer.LinkedWorkerId)
                && reviewer.TenantId == prepared.TenantId
                && reviewer.CompanyId == prepared.LegalEntityId
                && reviewer.Permissions.Contains(PrincipalRfqCommercialRoundPolicyRules.ApprovalAction)
                && (reviewer.SelectedBranchId is null || reviewer.SelectedBranchId == prepared.BranchId)
                && (reviewer.OrganisationWide || reviewer.Branches.Contains(prepared.BranchId))
                && ProcurementPrincipalEvidence.Independent(reviewer.PrincipalId,
                    reviewer.LinkedWorkerId, prepared.ActorPrincipalId,
                    prepared.ActorWorkerAliasId)
                && original.Lines.All(line =>
                    ProcurementPrincipalEvidence.Independent(reviewer.PrincipalId,
                        reviewer.LinkedWorkerId, line.MakerPrincipalId,
                        line.MakerWorkerAlias)
                    && ProcurementPrincipalEvidence.Independent(reviewer.PrincipalId,
                        reviewer.LinkedWorkerId, line.RequesterPrincipalId,
                        line.RequesterWorkerAlias)),
                "RfqCommercialRoundParticipantNotCurrentOrIndependent");
        }
        Need(closer.PrincipalId == rules.ClosingOfficerPrincipalId
            && ProcurementPrincipalEvidence.Canonical(closer.PrincipalId)
            && ProcurementPrincipalEvidence.OptionalWorker(closer.LinkedWorkerId)
            && closer.TenantId == prepared.TenantId
            && closer.CompanyId == prepared.LegalEntityId
            && closer.Permissions.Contains(PrincipalRfqSealedManualOffers.CloseAction)
            && (closer.SelectedBranchId is null || closer.SelectedBranchId == prepared.BranchId)
            && (closer.OrganisationWide || closer.Branches.Contains(prepared.BranchId))
            && ProcurementPrincipalEvidence.Independent(closer.PrincipalId,
                closer.LinkedWorkerId, prepared.ActorPrincipalId,
                prepared.ActorWorkerAliasId)
            && original.Lines.All(line =>
                ProcurementPrincipalEvidence.Independent(closer.PrincipalId,
                    closer.LinkedWorkerId, line.MakerPrincipalId,
                    line.MakerWorkerAlias)
                && ProcurementPrincipalEvidence.Independent(closer.PrincipalId,
                    closer.LinkedWorkerId, line.RequesterPrincipalId,
                    line.RequesterWorkerAlias))
            && currentReviewers.All(x => ProcurementPrincipalEvidence.Independent(
                closer.PrincipalId, closer.LinkedWorkerId,
                x.PrincipalId, x.LinkedWorkerId))
            && currentReviewers.Where(x => x.LinkedWorkerId is not null)
                .Select(x => x.LinkedWorkerId).Distinct().Count()
                == currentReviewers.Count(x => x.LinkedWorkerId is not null),
            "RfqCommercialRoundParticipantNotCurrentOrIndependent");
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
        var core = new ApprovalCase
        {
            TenantId = prepared.TenantId, LegalEntityId = prepared.LegalEntityId,
            SourceModule = "procurement", SourceType = SourceType,
            SourceId = prepared.Id, SourceVersion = prepared.DraftRevision,
            PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber,
            MakerSubjectId = prepared.ActorPrincipalId,
            OpenedAt = at, CreatedAt = at, UpdatedAt = at,
        };
        var sequence = 0;
        foreach (var configured in rules.Stages)
        {
            var stage = new ApprovalStage
            {
                TenantId = core.TenantId, LegalEntityId = core.LegalEntityId,
                CaseId = core.Id, Sequence = ++sequence, Code = configured.Code,
                Mode = configured.Mode == "One" ? "Any" : "All",
                RequiredApprovals = configured.Mode == "One" ? 1 : configured.PrincipalIds.Count,
                State = sequence == 1 ? "Active" : "Waiting",
                CreatedAt = at, UpdatedAt = at,
            };
            foreach (var principal in configured.PrincipalIds)
                stage.Assignments.Add(new ApprovalAssignment
                {
                    TenantId = core.TenantId, LegalEntityId = core.LegalEntityId,
                    CaseId = core.Id, StageId = stage.Id,
                    AssigneeSubjectId = principal,
                    PermissionKey = PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
                    CreatedAt = at, UpdatedAt = at,
                });
            core.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(core);
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, eventId = ev.Id, preparedPlanId = prepared.Id,
            preparedPlanSha256 = prepared.SnapshotSha256,
            sourceFingerprint = original.OriginalFingerprint,
            tenantId = prepared.TenantId, companyId = prepared.LegalEntityId,
            branchId = prepared.BranchId, currencyCode = ev.CurrencyCode,
            eventVersion = ev.Version, opensAt = opens, closesAt = closes,
            supplierIds = invitedSupplierIds.Order().ToArray(),
            requirementsUseSetId = prepared.DocumentUseSetId,
            requirementsDigest = prepared.DocumentSelectionDigest,
            policyVersionId = policy.Id,
            policyVersionNumber = policy.PolicyVersionNumber,
            policyRulesSha256 = rules.RulesSha256,
            closerPrincipalId = closer.PrincipalId,
            closerWorkerAliasId = closer.LinkedWorkerId,
            coreCaseId = core.Id,
            stages = core.Stages.OrderBy(x => x.Sequence).Select(x => new
            {
                x.Id, x.Sequence, x.Code, x.Mode, x.RequiredApprovals,
                assignments = x.Assignments.OrderBy(a => a.AssigneeSubjectId)
                    .Select(a => new { a.Id, a.AssigneeSubjectId, a.PermissionKey }),
            }),
        });
        return new(core, snapshot, ProcurementEvidenceEncoding.Digest(snapshot),
            Array.AsReadOnly(invitedSupplierIds.Order().ToArray()),
            opens, closes, closer.PrincipalId, closer.LinkedWorkerId);
    }

    public static PrincipalRfqCommercialRoundTerminal Finish(
        PrincipalRfqCommercialRoundOpening opening, PrincipalRfqPreparedPlan prepared,
        SourcingEvent source, PolicyVersion policy, ApprovalCase current,
        ApprovalDecision terminalDecision, RfqPrincipalActor signer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(terminalDecision);
        ArgumentNullException.ThrowIfNull(signer);
        using var retained = JsonDocument.Parse(opening.SnapshotJson);
        var pin = retained.RootElement;
        var retainedStages = pin.GetProperty("stages").EnumerateArray().ToArray();
        var currentStages = current.Stages.OrderBy(x => x.Sequence).ToArray();
        Need(retainedStages.Length == currentStages.Length
            && retainedStages.Zip(currentStages).All(pair =>
            {
                var row = pair.First;
                var stage = pair.Second;
                var assignments = row.GetProperty("assignments").EnumerateArray().ToArray();
                return row.GetProperty("Id").GetGuid() == stage.Id
                    && row.GetProperty("Sequence").GetInt32() == stage.Sequence
                    && row.GetProperty("Code").GetString() == stage.Code
                    && row.GetProperty("Mode").GetString() == stage.Mode
                    && row.GetProperty("RequiredApprovals").GetInt32() == stage.RequiredApprovals
                    && assignments.Length == stage.Assignments.Count
                    && assignments.Select(x => x.GetProperty("Id").GetGuid()).ToHashSet()
                        .SetEquals(stage.Assignments.Select(x => x.Id))
                    && assignments.All(x => stage.Assignments.Any(a =>
                        a.Id == x.GetProperty("Id").GetGuid()
                        && a.AssigneeSubjectId == x.GetProperty("AssigneeSubjectId").GetString()
                        && a.PermissionKey == x.GetProperty("PermissionKey").GetString()));
            }), "RfqCommercialRoundReviewGraphChanged");
        Need(opening.SnapshotSha256 == ProcurementEvidenceEncoding.Digest(opening.SnapshotJson)
            && pin.GetProperty("schemaVersion").GetInt32() == 1
            && pin.GetProperty("preparedPlanId").GetGuid() == prepared.Id
            && pin.GetProperty("preparedPlanSha256").GetString() == prepared.SnapshotSha256
            && pin.GetProperty("eventId").GetGuid() == source.Id
            && pin.GetProperty("branchId").GetGuid() == prepared.BranchId
            && pin.GetProperty("tenantId").GetString() == prepared.TenantId
            && pin.GetProperty("companyId").GetGuid() == prepared.LegalEntityId
            && pin.GetProperty("eventVersion").GetInt64() == source.Version
            && pin.GetProperty("coreCaseId").GetGuid() == current.Id
            && pin.GetProperty("policyVersionId").GetGuid() == policy.Id
            && pin.GetProperty("policyVersionNumber").GetInt32() == policy.PolicyVersionNumber
            && pin.GetProperty("policyRulesSha256").GetString()
                == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && pin.GetProperty("opensAt").GetDateTimeOffset() == opening.OpensAt
            && pin.GetProperty("closesAt").GetDateTimeOffset() == opening.ClosesAt
            && pin.GetProperty("closerPrincipalId").GetString() == opening.ClosingOfficerPrincipalId
            && pin.GetProperty("supplierIds").EnumerateArray().Select(x => x.GetGuid())
                .SequenceEqual(opening.SupplierIds)
            && current.Id == opening.Case.Id
            && current.TenantId == prepared.TenantId
            && current.LegalEntityId == prepared.LegalEntityId
            && current.SourceModule == "procurement"
            && current.SourceType == SourceType
            && current.SourceId == prepared.Id
            && current.SourceVersion == prepared.DraftRevision
            && current.PolicyVersionId == policy.Id
            && current.PolicyVersionNumber == policy.PolicyVersionNumber
            && current.MakerSubjectId == prepared.ActorPrincipalId
            && source.Id == prepared.SourcingEventId
            && source.Status == "Published"
            && source.BranchRef == prepared.BranchId
            && source.TenantId == prepared.TenantId
            && source.LegalEntityId == prepared.LegalEntityId
            && current.Stages.Count == opening.Case.Stages.Count
            && terminalDecision.CaseId == current.Id
            && terminalDecision.ActorSubjectId == signer.PrincipalId
            && signer.Permissions.Contains(PrincipalRfqCommercialRoundPolicyRules.ApprovalAction)
            && signer.TenantId == prepared.TenantId
            && signer.CompanyId == prepared.LegalEntityId
            && (signer.SelectedBranchId is null || signer.SelectedBranchId == prepared.BranchId)
            && (signer.OrganisationWide || signer.Branches.Contains(prepared.BranchId))
            && ProcurementPrincipalEvidence.Independent(signer.PrincipalId,
                signer.LinkedWorkerId, prepared.ActorPrincipalId,
                prepared.ActorWorkerAliasId)
            && ProcurementEvidenceEncoding.DatabaseTimestamp(current.CompletedAt)
                == ProcurementEvidenceEncoding.DatabaseTimestamp(terminalDecision.DecidedAt)
            && current.CompletedAt <= now,
            "RfqCommercialRoundDecisionChanged");
        var assignment = current.Stages.SelectMany(x => x.Assignments)
            .SingleOrDefault(x => x.Id == terminalDecision.AssignmentId);
        Need(assignment is not null && assignment.CaseId == current.Id
            && assignment.StageId == terminalDecision.StageId
            && assignment.AssigneeSubjectId == signer.PrincipalId
            && assignment.PermissionKey == PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
            "RfqCommercialRoundDecisionChanged");
        var state = terminalDecision.Action switch
        {
            "Approve" when current.State == "Approved" => "Approved",
            "Return" when current.State == "Returned" => "Returned",
            "Reject" when current.State == "Rejected" => "Rejected",
            _ => throw new InvalidOperationException("RfqCommercialRoundDecisionChanged"),
        };
        if (state != "Approved")
            return new(state, terminalDecision.Id, signer.PrincipalId,
                terminalDecision.Reason, null);
        Need(current.Stages.All(x => x.State == "Approved")
            && current.CompletedAt is { } completed
            && completed < opening.OpensAt
            && opening.OpensAt < opening.ClosesAt,
            "RfqCommercialRoundApprovalTooLateOrIncomplete");
        var proof = new PrincipalRfqPublishedRoundProof(Guid.CreateVersion7(), source.Id,
            prepared.Id, prepared.SnapshotSha256, current.Id,
            terminalDecision.Id, opening.SnapshotSha256,
            prepared.TenantId, prepared.LegalEntityId, prepared.BranchId,
            source.CurrencyCode, source.Version, opening.OpensAt,
            opening.ClosesAt, opening.ClosingOfficerPrincipalId,
            opening.ClosingOfficerWorkerAliasId, "Accepting");
        return new("Approved", terminalDecision.Id, signer.PrincipalId, null, proof);
    }

    private static void Need(bool okay, string code)
    {
        if (!okay) throw new InvalidOperationException(code);
    }
}
