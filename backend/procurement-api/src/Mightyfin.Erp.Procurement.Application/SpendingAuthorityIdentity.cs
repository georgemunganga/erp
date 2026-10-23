using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Observed Core references, never an identity master or client-supplied authority.
/// The owning store must persist these envelopes append-only in the grant transaction.
/// A valid hash alone does not authenticate an envelope supplied outside that store.</summary>
public sealed record SpendingAuthorityIdentityEvidence
{
    public int SchemaVersion { get; init; } = 1;
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string TenantId { get; init; } = "";
    public Guid LegalEntityId { get; init; }
    public Guid GrantId { get; init; }
    public string AuthorityReference { get; init; } = "";
    public int AuthorityVersion { get; init; }
    public string Permission { get; init; } = "";
    public string Action { get; init; } = "";
    public string ConfigurationOwnerPrincipalId { get; init; } = "";
    public string RecipientPrincipalId { get; init; } = "";
    public Guid? RecipientWorkerId { get; init; }
    public string Stage { get; init; } = "";
    public string ActorPrincipalId { get; init; } = "";
    public Guid? ActorWorkerId { get; init; }
    public long GrantRecordVersionAtAction { get; init; }
    public string BusinessFingerprint { get; init; } = "";
    public DateTimeOffset ActionAt { get; init; }
    public Guid? CreatedEvidenceId { get; init; }
    public Guid? SubmittedEvidenceId { get; init; }
    public string EvidenceSha256 { get; init; } = "";
}

/// <summary>Retains original people across Core account rebinding. Callers still resolve
/// current canonical actors/recipients and enforce permissions, scope, state and locking.
/// No current lookup of a historical maker is used to reconstruct missing evidence.</summary>
public static class SpendingAuthorityIdentity
{
    public static SpendingAuthorityIdentityEvidence CaptureCreation(ProcurementSpendingAuthorityGrant grant,
        Guid? makerWorker, Guid? recipientWorker, int schemaVersion = 1)
    {
        Require(grant.State == "Draft" && grant.Version == 1
            && grant.ConfigurationOwnerPrincipalId == grant.CreatedBy, "AuthorityOriginalMakerRequired");
        var row = Capture(grant, "Created", grant.CreatedBy, makerWorker, recipientWorker, grant.CreatedAt, schemaVersion: schemaVersion);
        VerifyOrigin(grant, row);
        return row;
    }

    public static SpendingAuthorityIdentityEvidence CaptureSubmission(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence created, string principal, Guid? worker, Guid? currentRecipientWorker)
    {
        RequireAuthor(grant, created, principal, worker);
        RequireCurrentRecipient(grant, created, grant.PrincipalId, currentRecipientWorker);
        var submittedAt = grant.SubmittedAt ?? throw new InvalidOperationException("AuthoritySubmissionIdentityRequired");
        var row = Capture(grant, "Submitted", principal, worker, created.RecipientWorkerId,
            submittedAt, created);
        VerifySubmission(grant, created, row);
        return row;
    }

    public static SpendingAuthorityIdentityEvidence CaptureActivation(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence created, SpendingAuthorityIdentityEvidence submitted,
        string principal, Guid? worker, Guid? currentRecipientWorker)
    {
        RequireCurrentRecipient(grant, created, grant.PrincipalId, currentRecipientWorker);
        var activatedAt = grant.ActivatedAt ?? throw new InvalidOperationException("AuthorityActivationIdentityRequired");
        var row = Capture(grant, "Activated", principal, worker, created.RecipientWorkerId,
            activatedAt, created, submitted);
        VerifyActivation(grant, created, submitted, row);
        return row;
    }

    public static void VerifyOrigin(ProcurementSpendingAuthorityGrant grant, SpendingAuthorityIdentityEvidence? created)
    {
        Require(created is not null, "AuthorityHistoricalIdentityMissing");
        VerifyOwned(grant, created!);
        Require(created!.Stage == "Created" && created.ActorPrincipalId == grant.CreatedBy
            && created.ActorPrincipalId == grant.ConfigurationOwnerPrincipalId
            && created.ActionAt == Time(grant.CreatedAt), "AuthorityOriginalMakerRequired");
    }

    public static void RequireAuthor(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence? created, string principal, Guid? worker)
    {
        VerifyOrigin(grant, created);
        Require(Canonical(principal) && principal == created!.ActorPrincipalId
            && IdentityWorkerBinding(created.SchemaVersion,created.ActorWorkerId,worker),
            "AuthorityOriginalAuthorRequired");
    }

    public static void RequireCurrentRecipient(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence? created, string principal, Guid? worker)
    {
        VerifyOrigin(grant, created);
        Require(Canonical(principal) && principal == created!.RecipientPrincipalId
            && IdentityWorkerBinding(created.SchemaVersion,created.RecipientWorkerId,worker),
            "AuthorityRecipientIdentityChanged");
    }

    public static void VerifySubmission(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence created, SpendingAuthorityIdentityEvidence submitted)
    {
        Require(grant.State == "PendingReview", "AuthoritySubmissionStateRequired");
        VerifySubmittedAtVersion(grant, created, submitted, grant.Version);
    }

    public static void RequireIndependentReviewer(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence created, SpendingAuthorityIdentityEvidence submitted,
        string principal, Guid? worker)
    {
        VerifySubmission(grant, created, submitted);
        RequireIndependent(created, submitted, principal, worker);
    }

    public static void VerifyActivation(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence created, SpendingAuthorityIdentityEvidence submitted,
        SpendingAuthorityIdentityEvidence activated)
    {
        Require(grant.State == "Active" && grant.Version > 1, "AuthorityActivationStateRequired");
        VerifySubmittedAtVersion(grant, created, submitted, grant.Version - 1);
        VerifyOwned(grant, activated);
        RequireIndependent(created, submitted, activated.ActorPrincipalId, activated.ActorWorkerId);
        Require(activated.SchemaVersion==created.SchemaVersion && activated.Stage == "Activated" && activated.CreatedEvidenceId == created.Id
            && activated.SubmittedEvidenceId == submitted.Id
            && activated.RecipientWorkerId == created.RecipientWorkerId
            && activated.GrantRecordVersionAtAction == grant.Version
            && activated.ActorPrincipalId == grant.ActivatedByPrincipalId
            && activated.ActionAt == Time(grant.ActivatedAt)
            && activated.ActionAt >= submitted.ActionAt
            && activated.BusinessFingerprint == submitted.BusinessFingerprint
            && activated.BusinessFingerprint == grant.PublishedBusinessFingerprint,
            "AuthorityActivationIdentityMismatch");
    }

    private static void VerifySubmittedAtVersion(ProcurementSpendingAuthorityGrant grant,
        SpendingAuthorityIdentityEvidence created, SpendingAuthorityIdentityEvidence submitted, long version)
    {
        VerifyOrigin(grant, created);
        VerifyOwned(grant, submitted);
        Require(submitted.SchemaVersion==created.SchemaVersion && submitted.Stage == "Submitted" && submitted.CreatedEvidenceId == created.Id
            && submitted.ActorPrincipalId == created.ActorPrincipalId
            && IdentityWorkerBinding(created.SchemaVersion,created.ActorWorkerId,submitted.ActorWorkerId)
            && submitted.RecipientWorkerId == created.RecipientWorkerId
            && submitted.GrantRecordVersionAtAction == version
            && submitted.GrantRecordVersionAtAction > created.GrantRecordVersionAtAction
            && submitted.ActionAt >= created.ActionAt
            && submitted.ActorPrincipalId == grant.SubmittedByPrincipalId
            && submitted.ActionAt == Time(grant.SubmittedAt)
            && submitted.BusinessFingerprint == grant.SubmittedBusinessFingerprint
            && submitted.BusinessFingerprint == SpendingAuthorityConfiguration.Fingerprint(grant),
            "AuthoritySubmittedIdentityMismatch");
    }

    private static void RequireIndependent(SpendingAuthorityIdentityEvidence created,
        SpendingAuthorityIdentityEvidence submitted, string principal, Guid? worker)
        => Require(Canonical(principal) && principal != created.ActorPrincipalId && principal != submitted.ActorPrincipalId
            && principal != created.RecipientPrincipalId && principal != created.ConfigurationOwnerPrincipalId
            && (created.SchemaVersion==1 ? worker is not null && worker!=Guid.Empty && worker!=created.ActorWorkerId
                && worker!=submitted.ActorWorkerId && worker!=created.RecipientWorkerId
                : ProcurementPrincipalEvidence.Independent(created.ActorPrincipalId,created.ActorWorkerId,principal,worker)
                    && ProcurementPrincipalEvidence.Independent(submitted.ActorPrincipalId,submitted.ActorWorkerId,principal,worker)
                    && ProcurementPrincipalEvidence.Independent(created.RecipientPrincipalId,created.RecipientWorkerId,principal,worker)),
            "AuthorityIndependentWorkerRequired");

    private static bool IdentityWorkerBinding(int schema,Guid? retained,Guid? current) => schema==1
        ? retained is not null && retained!=Guid.Empty && retained==current
        : schema==2 && ProcurementPrincipalEvidence.OptionalWorker(retained) && ProcurementPrincipalEvidence.OptionalWorker(current);

    public static string Digest(SpendingAuthorityIdentityEvidence row)
        => ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(new
        {
            row.SchemaVersion, row.Id, row.TenantId, row.LegalEntityId, row.GrantId,
            row.AuthorityReference, row.AuthorityVersion, row.Permission, row.Action,
            row.ConfigurationOwnerPrincipalId, row.RecipientPrincipalId, row.RecipientWorkerId,
            row.Stage, row.ActorPrincipalId, row.ActorWorkerId, row.GrantRecordVersionAtAction,
            row.BusinessFingerprint, actionAt = Time(row.ActionAt), row.CreatedEvidenceId, row.SubmittedEvidenceId,
        }));

    public static void Verify(SpendingAuthorityIdentityEvidence row)
    {
        Require(row.SchemaVersion is 1 or 2 && row.Id != Guid.Empty && !string.IsNullOrWhiteSpace(row.TenantId)
            && row.LegalEntityId != Guid.Empty && row.GrantId != Guid.Empty && row.AuthorityVersion > 0
            && !string.IsNullOrWhiteSpace(row.AuthorityReference) && !string.IsNullOrWhiteSpace(row.Permission)
            && !string.IsNullOrWhiteSpace(row.Action) && Canonical(row.ActorPrincipalId)
            && Canonical(row.RecipientPrincipalId) && Canonical(row.ConfigurationOwnerPrincipalId)
            && ProcurementPrincipalEvidence.OptionalWorker(row.ActorWorkerId) && ProcurementPrincipalEvidence.OptionalWorker(row.RecipientWorkerId)
            && (row.SchemaVersion==2 || row.ActorWorkerId is not null && row.RecipientWorkerId is not null)
            && (row.SchemaVersion==1 || ProcurementSpendingActions.MatchesIdentity(row.Action, row.Permission))
            && row.GrantRecordVersionAtAction > 0 && row.ActionAt != default
            && row.ActionAt == Time(row.ActionAt) && Hex(row.BusinessFingerprint)
            && Hex(row.EvidenceSha256) && row.EvidenceSha256 == Digest(row), "AuthorityIdentityEvidenceInvalid");
        Require(row.Stage switch
        {
            "Created" => row.GrantRecordVersionAtAction == 1 && row.CreatedEvidenceId is null
                && row.SubmittedEvidenceId is null,
            "Submitted" => row.CreatedEvidenceId is { } c && c != Guid.Empty && c != row.Id
                && row.SubmittedEvidenceId is null,
            "Activated" => row.CreatedEvidenceId is { } c && c != Guid.Empty && c != row.Id
                && row.SubmittedEvidenceId is { } s && s != Guid.Empty && s != row.Id && s != c,
            _ => false,
        }, "AuthorityIdentityEvidenceStageInvalid");
    }

    private static void VerifyOwned(ProcurementSpendingAuthorityGrant grant, SpendingAuthorityIdentityEvidence row)
    {
        Verify(row);
        Require(row.TenantId == grant.TenantId && row.LegalEntityId == grant.LegalEntityId && row.GrantId == grant.Id
            && row.AuthorityReference == grant.AuthorityReference && row.AuthorityVersion == grant.AuthorityVersion
            && row.Permission == grant.Permission && row.Action == grant.Action
            && (row.SchemaVersion == 1 || ProcurementSpendingActions.Matches(grant.Action, grant.Permission, grant.AmountBasis))
            && row.ConfigurationOwnerPrincipalId == grant.ConfigurationOwnerPrincipalId
            && row.RecipientPrincipalId == grant.PrincipalId, "AuthorityIdentityEvidenceScopeMismatch");
    }

    private static SpendingAuthorityIdentityEvidence Capture(ProcurementSpendingAuthorityGrant grant,
        string stage, string principal, Guid? worker, Guid? recipientWorker, DateTimeOffset at,
        SpendingAuthorityIdentityEvidence? created = null, SpendingAuthorityIdentityEvidence? submitted = null, int schemaVersion = 1)
    {
        var row = new SpendingAuthorityIdentityEvidence
        {
            SchemaVersion = created?.SchemaVersion ?? schemaVersion,
            TenantId = grant.TenantId, LegalEntityId = grant.LegalEntityId, GrantId = grant.Id,
            AuthorityReference = grant.AuthorityReference, AuthorityVersion = grant.AuthorityVersion,
            Permission = grant.Permission, Action = grant.Action,
            ConfigurationOwnerPrincipalId = grant.ConfigurationOwnerPrincipalId,
            RecipientPrincipalId = grant.PrincipalId, RecipientWorkerId = recipientWorker,
            Stage = stage, ActorPrincipalId = principal, ActorWorkerId = worker,
            GrantRecordVersionAtAction = grant.Version, BusinessFingerprint = SpendingAuthorityConfiguration.Fingerprint(grant),
            ActionAt = Time(at)!.Value, CreatedEvidenceId = created?.Id, SubmittedEvidenceId = submitted?.Id,
        };
        row = row with { EvidenceSha256 = Digest(row) };
        Verify(row);
        return row;
    }

    private static DateTimeOffset? Time(DateTimeOffset? at) => ProcurementEvidenceEncoding.DatabaseTimestamp(at);
    private static bool Canonical(string? principal) => Guid.TryParseExact(principal, "D", out var id)
        && id != Guid.Empty && principal == id.ToString("D");
    private static bool Hex(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static void Require(bool valid, string code)
    { if (!valid) throw new InvalidOperationException(code); }
}
