using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Retained identity proof only. Current canonical account/permission and schema/transaction
/// readiness must still be supplied by the owning server. No historical account mapping is inferred.</summary>
public static class SourcingPolicyIdentity
{
    public static SourcingPolicyIdentityEvidence Capture(PolicyVersion policy, string tenant, Guid company,
        string stage, string principal, Guid? worker, long recordVersion, DateTimeOffset at,
        SourcingPolicyIdentityEvidence? created = null, SourcingPolicyIdentityEvidence? submitted = null,
        int schemaVersion = 1)
    {
        var row = new SourcingPolicyIdentityEvidence { SchemaVersion = schemaVersion, TenantId = tenant, LegalEntityId = company,
            PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber, Stage = stage,
            ActorPrincipalId = principal, ActorWorkerId = worker, PolicyRecordVersionAtAction = recordVersion,
            ActionAt = (ProcurementEvidenceEncoding.DatabaseTimestamp(at) ?? throw new InvalidOperationException("SourcingPolicyIdentityTimeRequired")),
            RulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            CreatedEvidenceId = created?.Id, CreatedEvidenceStage = created is null ? null : "Created",
            SubmittedEvidenceId = submitted?.Id, SubmittedEvidenceStage = submitted is null ? null : "Submitted" };
        row.EvidenceSha256 = Digest(row); Verify(row); return row;
    }
    public static string Digest(SourcingPolicyIdentityEvidence row) => ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new {
        schemaVersion = row.SchemaVersion, row.Id, row.TenantId, row.LegalEntityId, row.PolicyVersionId, row.PolicyVersionNumber,
        row.Stage, row.ActorPrincipalId, row.ActorWorkerId, row.PolicyRecordVersionAtAction, row.RulesSha256,
        actionAt = ProcurementEvidenceEncoding.DatabaseTimestamp(row.ActionAt), row.CreatedEvidenceId, row.CreatedEvidenceStage,
        row.SubmittedEvidenceId, row.SubmittedEvidenceStage }));
    public static void Verify(SourcingPolicyIdentityEvidence row)
    {
        Require(row.SchemaVersion is 1 or 2 && row.Id != Guid.Empty && !string.IsNullOrWhiteSpace(row.TenantId) && row.LegalEntityId != Guid.Empty
            && row.PolicyVersionId != Guid.Empty && row.PolicyVersionNumber > 0 && row.PolicyRecordVersionAtAction > 0
            && Guid.TryParse(row.ActorPrincipalId, out var principal) && principal != Guid.Empty && row.ActorPrincipalId == principal.ToString("D")
            && ProcurementPrincipalEvidence.OptionalWorker(row.ActorWorkerId)
            && (row.SchemaVersion == 2 || row.ActorWorkerId is not null) && row.ActionAt != default
            && row.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(row.ActionAt)
            && Hash(row.RulesSha256) && Hash(row.EvidenceSha256) && row.EvidenceSha256 == Digest(row), "SourcingPolicyIdentityInvalid");
        Require(row.Stage switch {
            "Created" => row.PolicyRecordVersionAtAction == 1 && row.CreatedEvidenceId is null && row.CreatedEvidenceStage is null
                && row.SubmittedEvidenceId is null && row.SubmittedEvidenceStage is null,
            "Submitted" => row.CreatedEvidenceId is { } c && c != Guid.Empty && row.CreatedEvidenceStage == "Created"
                && row.SubmittedEvidenceId is null && row.SubmittedEvidenceStage is null,
            "Published" => row.CreatedEvidenceId is { } c && c != Guid.Empty && row.CreatedEvidenceStage == "Created"
                && row.SubmittedEvidenceId is { } s && s != Guid.Empty && row.SubmittedEvidenceStage == "Submitted",
            _ => false }, "SourcingPolicyIdentityStageInvalid");
    }
    public static void VerifyOrigin(PolicyVersion policy, SourcingPolicyIdentityEvidence created)
    {
        VerifyOwned(policy, created);
        Require(created.Stage == "Created" && created.ActorPrincipalId == policy.CreatedBy, "SourcingPolicyOriginalMakerRequired");
    }
    public static void RequireAuthor(PolicyVersion policy, SourcingPolicyIdentityEvidence created, string principal, Guid? worker)
    {
        VerifyOrigin(policy, created);
        Require(created.ActorPrincipalId == principal
            && ProcurementPrincipalEvidence.WorkerBinding(created.SchemaVersion, created.ActorWorkerId, worker),
            "SourcingPolicyOriginalAuthorRequired");
    }
    public static void VerifySubmission(PolicyVersion policy, SourcingPolicyIdentityEvidence created,
        SourcingPolicyIdentityEvidence submitted, bool pendingState = true)
    {
        VerifyOrigin(policy, created); VerifyOwned(policy, submitted);
        Require((!pendingState || policy.State == "PendingReview") && submitted.Stage == "Submitted"
            && submitted.CreatedEvidenceId == created.Id && submitted.ActorPrincipalId == created.ActorPrincipalId
            && submitted.SchemaVersion == created.SchemaVersion
            && ProcurementPrincipalEvidence.WorkerBinding(created.SchemaVersion, created.ActorWorkerId, submitted.ActorWorkerId)
            && submitted.PolicyRecordVersionAtAction == policy.Version
            && submitted.PolicyRecordVersionAtAction > created.PolicyRecordVersionAtAction && submitted.ActionAt >= created.ActionAt
            && submitted.RulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && submitted.ActorPrincipalId == policy.ReviewRequestedBy && submitted.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(policy.ReviewRequestedAt),
            "SourcingPolicySubmittedIdentityMismatch");
    }
    public static void RequireIndependent(PolicyVersion policy, SourcingPolicyIdentityEvidence created,
        SourcingPolicyIdentityEvidence submitted, string principal, Guid? worker)
    {
        VerifySubmission(policy, created, submitted);
        Require((created.SchemaVersion == 2 || worker is not null)
            && ProcurementPrincipalEvidence.Independent(principal, worker, created.ActorPrincipalId, created.ActorWorkerId)
            && ProcurementPrincipalEvidence.Independent(principal, worker, submitted.ActorPrincipalId, submitted.ActorWorkerId),
            "SourcingPolicyIndependentReviewerRequired");
    }
    public static void VerifyPublication(PolicyVersion policy, SourcingPolicyIdentityEvidence created,
        SourcingPolicyIdentityEvidence submitted, SourcingPolicyIdentityEvidence published)
    {
        VerifyOrigin(policy, created); VerifyOwned(policy, submitted); VerifyOwned(policy, published);
        Require(policy.State == "Published" && submitted.Stage == "Submitted" && published.Stage == "Published"
            && submitted.CreatedEvidenceId == created.Id && published.CreatedEvidenceId == created.Id && published.SubmittedEvidenceId == submitted.Id
            && submitted.SchemaVersion == created.SchemaVersion && published.SchemaVersion == created.SchemaVersion
            && submitted.ActorPrincipalId == created.ActorPrincipalId
            && ProcurementPrincipalEvidence.WorkerBinding(created.SchemaVersion, created.ActorWorkerId, submitted.ActorWorkerId)
            && submitted.PolicyRecordVersionAtAction > created.PolicyRecordVersionAtAction
            && submitted.ActionAt >= created.ActionAt && published.ActionAt >= submitted.ActionAt
            && published.ActorPrincipalId != created.ActorPrincipalId && published.ActorPrincipalId != submitted.ActorPrincipalId
            && ProcurementPrincipalEvidence.Independent(published.ActorPrincipalId, published.ActorWorkerId,
                created.ActorPrincipalId, created.ActorWorkerId)
            && ProcurementPrincipalEvidence.Independent(published.ActorPrincipalId, published.ActorWorkerId,
                submitted.ActorPrincipalId, submitted.ActorWorkerId)
            && published.PolicyRecordVersionAtAction == checked(submitted.PolicyRecordVersionAtAction + 1)
            && policy.Version >= published.PolicyRecordVersionAtAction
            && published.RulesSha256 == submitted.RulesSha256 && published.RulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && submitted.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(policy.ReviewRequestedAt)
            && submitted.ActorPrincipalId == policy.ReviewRequestedBy && published.ActorPrincipalId == policy.PublishedBy
            && published.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(policy.PublishedAt), "SourcingPolicyPublicationIdentityMismatch");
    }
    private static void VerifyOwned(PolicyVersion policy, SourcingPolicyIdentityEvidence row)
    {
        Verify(row);
        Require(policy.PolicyKey == SourcingPublicationPolicyRules.PolicyKey && row.PolicyVersionId == policy.Id
            && row.PolicyVersionNumber == policy.PolicyVersionNumber && row.TenantId == policy.TenantId
            && row.LegalEntityId == policy.LegalEntityId, "SourcingPolicyIdentityScopeMismatch");
    }
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require(bool valid, string code) { if (!valid) throw new InvalidOperationException(code); }
}
