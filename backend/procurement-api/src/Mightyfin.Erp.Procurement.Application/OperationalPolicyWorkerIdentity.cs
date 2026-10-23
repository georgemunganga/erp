using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Immutable observed Core references, not a policy engine or identity master.
/// Only the owning append-only store can authenticate provenance; a correct digest alone cannot.</summary>
public sealed record OperationalPolicyWorkerIdentityEvidence
{
    public int SchemaVersion { get; init; } = 1;
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string TenantId { get; init; } = "";
    public Guid LegalEntityId { get; init; }
    public string PolicyKey { get; init; } = "";
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string Stage { get; init; } = "";
    public string ActorPrincipalId { get; init; } = "";
    public Guid? ActorWorkerId { get; init; }
    public long PolicyRecordVersionAtAction { get; init; }
    public string RulesSha256 { get; init; } = "";
    public DateTimeOffset ActionAt { get; init; }
    public Guid? CreatedEvidenceId { get; init; }
    public string? CreatedEvidenceStage { get; init; }
    public Guid? SubmittedEvidenceId { get; init; }
    public string? SubmittedEvidenceStage { get; init; }
    public string EvidenceSha256 { get; init; } = "";
}

public enum OperationalPolicyPublicationMode
{
    CurrentPublished,
    RetainedHistorical,
}

/// <summary>Pure original-person/lineage verification for RECEIVING and INVOICE_MATCHING.
/// Current Core canonical identity, permissions, scope, effective period, schema and transaction
/// are separate owning-server guards. No historical worker is reconstructed from a current login.</summary>
public static class OperationalPolicyWorkerIdentity
{
    /// <summary>Structural capture only. recordVersionAtAction is the server's intended committed
    /// revision (existing policy routes increment on SaveChanges). Call the lifecycle verifier before
    /// using evidence; persistence must bind capture to the actual same-save owner/observed actor.</summary>
    public static OperationalPolicyWorkerIdentityEvidence Capture(PolicyVersion policy, string stage,
        string principal, Guid? worker, long recordVersionAtAction, DateTimeOffset at,
        OperationalPolicyWorkerIdentityEvidence? created = null,
        OperationalPolicyWorkerIdentityEvidence? submitted = null, int schemaVersion = 1)
    {
        var row = new OperationalPolicyWorkerIdentityEvidence
        {
            SchemaVersion = schemaVersion, TenantId = policy.TenantId, LegalEntityId = policy.LegalEntityId, PolicyKey = policy.PolicyKey,
            PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber, Stage = stage,
            ActorPrincipalId = principal, ActorWorkerId = worker, PolicyRecordVersionAtAction = recordVersionAtAction,
            RulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            ActionAt = ProcurementEvidenceEncoding.DatabaseTimestamp(at)
                ?? throw new InvalidOperationException("OperationalPolicyIdentityTimeRequired"),
            CreatedEvidenceId = created?.Id, CreatedEvidenceStage = created is null ? null : "Created",
            SubmittedEvidenceId = submitted?.Id, SubmittedEvidenceStage = submitted is null ? null : "Submitted",
        };
        row = row with { EvidenceSha256 = Digest(row) };
        Verify(row);
        if (stage == "Created")
        {
            Require(policy.State == "Draft" && policy.Version == 1, "OperationalPolicyCreationStateRequired");
            VerifyOrigin(policy, row);
        }
        return row;
    }

    public static string Digest(OperationalPolicyWorkerIdentityEvidence row)
        => ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new
        {
            row.SchemaVersion, row.Id, row.TenantId, row.LegalEntityId, row.PolicyKey,
            row.PolicyVersionId, row.PolicyVersionNumber, row.Stage, row.ActorPrincipalId, row.ActorWorkerId,
            row.PolicyRecordVersionAtAction, row.RulesSha256,
            actionAt = ProcurementEvidenceEncoding.DatabaseTimestamp(row.ActionAt),
            row.CreatedEvidenceId, row.CreatedEvidenceStage, row.SubmittedEvidenceId, row.SubmittedEvidenceStage,
        }));

    public static void Verify(OperationalPolicyWorkerIdentityEvidence row)
    {
        Require(row.SchemaVersion is 1 or 2 && row.Id != Guid.Empty && !string.IsNullOrWhiteSpace(row.TenantId)
            && row.LegalEntityId != Guid.Empty && Supported(row.PolicyKey) && row.PolicyVersionId != Guid.Empty
            && row.PolicyVersionNumber > 0 && row.PolicyRecordVersionAtAction > 0
            && Canonical(row.ActorPrincipalId) && ProcurementPrincipalEvidence.OptionalWorker(row.ActorWorkerId)
            && (row.SchemaVersion == 2 || row.ActorWorkerId is not null) && (row.PolicyKey != "MANUAL_PAYMENT_REVIEW" || row.SchemaVersion == 2) && row.ActionAt != default
            && row.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(row.ActionAt)
            && Hash(row.RulesSha256) && Hash(row.EvidenceSha256) && row.EvidenceSha256 == Digest(row),
            "OperationalPolicyIdentityInvalid");
        Require(row.Stage switch
        {
            "Created" => row.PolicyRecordVersionAtAction == 1 && row.CreatedEvidenceId is null
                && row.CreatedEvidenceStage is null && row.SubmittedEvidenceId is null && row.SubmittedEvidenceStage is null,
            "Submitted" => row.CreatedEvidenceId is { } c && c != Guid.Empty && c != row.Id
                && row.CreatedEvidenceStage == "Created" && row.SubmittedEvidenceId is null && row.SubmittedEvidenceStage is null,
            "Published" => row.CreatedEvidenceId is { } c && c != Guid.Empty && c != row.Id && row.CreatedEvidenceStage == "Created"
                && row.SubmittedEvidenceId is { } s && s != Guid.Empty && s != row.Id && s != c && row.SubmittedEvidenceStage == "Submitted",
            _ => false,
        }, "OperationalPolicyIdentityStageInvalid");
    }

    public static void VerifyOrigin(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence? created)
    {
        Require(created is not null, "OperationalPolicyHistoricalIdentityMissing");
        VerifyOwned(policy, created!);
        // Creation digest intentionally need not match edited draft rules. Persistence must
        // prove the initial same-save digest; the origin remains the original person's identity.
        Require(created!.Stage == "Created" && created.ActorPrincipalId == policy.CreatedBy
            && policy.CreatedAt != default && policy.CreatedAt == ProcurementEvidenceEncoding.DatabaseTimestamp(policy.CreatedAt)
            && created.ActionAt == policy.CreatedAt,
            "OperationalPolicyOriginalMakerRequired");
    }

    public static void RequireAuthor(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence? created,
        string principal, Guid? worker)
    {
        VerifyOrigin(policy, created);
        Require(Canonical(principal) && principal == created!.ActorPrincipalId
            && ProcurementPrincipalEvidence.WorkerBinding(created.SchemaVersion, created.ActorWorkerId, worker), "OperationalPolicyOriginalAuthorRequired");
    }

    /// <param name="expectedRecordVersion">Owning server's intended committed revision for a same-save
    /// guard; null means the currently persisted policy.Version. Never accept this from a public body.</param>
    public static void VerifySubmission(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence created,
        OperationalPolicyWorkerIdentityEvidence submitted, long? expectedRecordVersion = null)
    {
        Require(policy.State == "PendingReview", "OperationalPolicySubmissionStateRequired");
        VerifySubmitted(policy, created, submitted, expectedRecordVersion ?? policy.Version);
    }

    public static void RequireIndependent(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence created,
        OperationalPolicyWorkerIdentityEvidence submitted, string principal, Guid? worker)
    {
        VerifySubmission(policy, created, submitted);
        Independent(created, submitted, principal, worker);
    }

    /// <summary>CurrentPublished verifies Published state, not current effective eligibility.
    /// RetainedHistorical permits known Superseded state and later owner revisions for historical
    /// inspection only; it must NEVER substitute for current-policy selection on new operations.</summary>
    public static void VerifyPublication(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence created,
        OperationalPolicyWorkerIdentityEvidence submitted, OperationalPolicyWorkerIdentityEvidence published,
        OperationalPolicyPublicationMode mode = OperationalPolicyPublicationMode.CurrentPublished,
        long? ownerRecordVersion = null)
    {
        Require(mode == OperationalPolicyPublicationMode.CurrentPublished ? policy.State == "Published"
            : mode == OperationalPolicyPublicationMode.RetainedHistorical && (policy.State is "Published" or "Superseded"),
            "OperationalPolicyPublicationStateRequired");
        VerifySubmitted(policy, created, submitted, submitted.PolicyRecordVersionAtAction);
        VerifyOwned(policy, published);
        Independent(created, submitted, published.ActorPrincipalId, published.ActorWorkerId);
        var currentVersion = ownerRecordVersion ?? policy.Version;
        Require(published.SchemaVersion == created.SchemaVersion && published.Stage == "Published" && published.CreatedEvidenceId == created.Id
            && published.SubmittedEvidenceId == submitted.Id
            && published.PolicyRecordVersionAtAction > 1
            && published.PolicyRecordVersionAtAction - 1 == submitted.PolicyRecordVersionAtAction
            && currentVersion >= published.PolicyRecordVersionAtAction
            && published.ActionAt >= submitted.ActionAt
            && published.RulesSha256 == submitted.RulesSha256
            && published.RulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && published.ActorPrincipalId == policy.PublishedBy
            && published.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(policy.PublishedAt),
            "OperationalPolicyPublicationIdentityMismatch");
    }

    private static void VerifySubmitted(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence created,
        OperationalPolicyWorkerIdentityEvidence submitted, long expectedVersion)
    {
        VerifyOrigin(policy, created);
        VerifyOwned(policy, submitted);
        Require(expectedVersion > 0 && submitted.Stage == "Submitted" && submitted.CreatedEvidenceId == created.Id
            && submitted.SchemaVersion == created.SchemaVersion && submitted.ActorPrincipalId == created.ActorPrincipalId
            && ProcurementPrincipalEvidence.WorkerBinding(created.SchemaVersion, created.ActorWorkerId, submitted.ActorWorkerId)
            && submitted.PolicyRecordVersionAtAction == expectedVersion
            && submitted.PolicyRecordVersionAtAction > created.PolicyRecordVersionAtAction
            && submitted.ActionAt >= created.ActionAt
            && submitted.RulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && submitted.ActorPrincipalId == policy.ReviewRequestedBy
            && submitted.ActionAt == ProcurementEvidenceEncoding.DatabaseTimestamp(policy.ReviewRequestedAt),
            "OperationalPolicySubmittedIdentityMismatch");
    }

    private static void Independent(OperationalPolicyWorkerIdentityEvidence created,
        OperationalPolicyWorkerIdentityEvidence submitted, string principal, Guid? worker)
        => Require(ProcurementPrincipalEvidence.Independent(principal, worker, created.ActorPrincipalId, created.ActorWorkerId)
            && ProcurementPrincipalEvidence.Independent(principal, worker, submitted.ActorPrincipalId, submitted.ActorWorkerId)
            && (created.SchemaVersion == 2 || worker is not null), "OperationalPolicyIndependentReviewerRequired");

    private static void VerifyOwned(PolicyVersion policy, OperationalPolicyWorkerIdentityEvidence row)
    {
        Verify(row);
        Require(Supported(policy.PolicyKey) && row.PolicyKey == policy.PolicyKey && row.PolicyVersionId == policy.Id
            && row.PolicyVersionNumber == policy.PolicyVersionNumber && row.TenantId == policy.TenantId
            && row.LegalEntityId == policy.LegalEntityId, "OperationalPolicyIdentityScopeMismatch");
    }

    private static bool Supported(string key) => key is "RECEIVING" or "INVOICE_MATCHING" or "INVOICE_UNIT_REVIEW" or "MANUAL_PAYMENT_REVIEW";
    private static bool Canonical(string principal) => Guid.TryParseExact(principal, "D", out var id)
        && id != Guid.Empty && principal == id.ToString("D");
    private static bool Hash(string value) => value is { Length: 64 } && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require(bool valid, string code) { if (!valid) throw new InvalidOperationException(code); }
}
