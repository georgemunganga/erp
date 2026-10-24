using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record RfqCommercialConflictParticipant(string PrincipalId, Guid? RetainedWorkerAliasId);
public sealed record PrincipalRfqOpenedOffersProof(Guid RoundId, Guid SealId,
    Guid EventId, Guid BranchId, string OfficerPrincipalId, Guid? OfficerWorkerAliasId,
    Guid PolicyVersionId, string PolicyRulesSha256, DateTimeOffset OpenedAt,
    int OfferCount, string OfferSetSha256, string Reason, string Reference,
    string SnapshotJson, string SnapshotSha256);

/// <summary>Pure source transition, not a substitute for current Core resolution,
/// published lineage, retained Core/doc/source proof, serializable locking or
/// physical readiness. Existing round approval alone never unseals offers.</summary>
public static class PrincipalRfqOfferOpening
{
    public static PrincipalRfqOpenedOffersProof Open(PrincipalRfqPublishedRoundProof round,
        string approvedRoundSnapshot, PrincipalRfqCommercialRoundSealRecord seal,
        PolicyVersion policy, IReadOnlyList<PrincipalRfqManualOfferSubmission> offers,
        RfqPrincipalActor actor, IReadOnlyList<RfqCommercialConflictParticipant> conflicts,
        IReadOnlyDictionary<string, Guid?> currentAliases, DateTimeOffset now,
        string? reason, string? reference)
    {
        ArgumentNullException.ThrowIfNull(round); ArgumentNullException.ThrowIfNull(seal);
        ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(conflicts); ArgumentNullException.ThrowIfNull(currentAliases);
        var rules = PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(policy,
            round.TenantId, round.CompanyId, now);
        var controls = rules.OfferOpening
            ?? throw new InvalidOperationException("RfqOfferOpeningPolicyUnavailable");
        Need(rules.SchemaVersion == 2 && round.State == "Accepting"
            && ApprovedSnapshotMatches(round, approvedRoundSnapshot, policy, rules)
            && PrincipalRfqOfferOpeningControls.MatchesRoundSnapshot(approvedRoundSnapshot, rules)
            && seal.Id != Guid.Empty && seal.OpeningId == round.RoundId
            && seal.ApprovedDecisionId == round.ApprovedDecisionId
            && seal.SourcingEventId == round.EventId && seal.BranchId == round.BranchId
            && seal.ClosedAt >= round.ClosesAt
            && seal.TenantId == round.TenantId && seal.LegalEntityId == round.CompanyId
            && seal.OfficerPrincipalId == round.ClosingOfficerPrincipalId
            && seal.OfficerWorkerAliasId == round.ClosingOfficerWorkerAliasId
            && seal.SubmissionCount == offers.Count
            && seal.SubmissionSetSha256 == PrincipalRfqSealedManualOffers.RetainedSetDigest(round, offers),
            "RfqOfferOpeningSealChanged");
        Need(Instant(now) && Instant(seal.ClosedAt) && now >= seal.ClosedAt
            && now - seal.ClosedAt >= TimeSpan.FromSeconds(controls.DelayAfterCloseSeconds),
            "RfqOfferOpeningTooEarly");
        RequireActor(round, controls, actor, currentAliases,
            PrincipalRfqOfferOpeningControls.OpenAction, true);
        RequireIndependent(actor, conflicts, currentAliases);
        if (!controls.AllowClosingOfficer)
            RequireIndependent(actor, [new(round.ClosingOfficerPrincipalId,
                round.ClosingOfficerWorkerAliasId)], currentAliases);
        reason = reason?.Trim() ?? ""; reference = reference?.Trim() ?? "";
        Need(reason.Length <= 1000 && reference.Length <= 200
            && (!controls.ReasonRequired || reason.Length > 0)
            && (!controls.ReferenceRequired || reference.Length > 0),
            "RfqOfferOpeningReasonOrReferenceRequired");
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, roundId = round.RoundId, sealId = seal.Id,
            eventId = round.EventId, tenantId = round.TenantId, companyId = round.CompanyId,
            branchId = round.BranchId, preparedPlanId = round.PreparedPlanId,
            preparedPlanSha256 = round.PreparedPlanSha256,
            approvalEvidenceSha256 = round.ApprovalEvidenceSha256,
            policyVersionId = policy.Id, policyRulesSha256 = rules.RulesSha256,
            openingControls = controls, officerPrincipalId = actor.PrincipalId,
            officerWorkerAliasId = actor.LinkedWorkerId, openedAt = now,
            offerCount = offers.Count, offerSetSha256 = seal.SubmissionSetSha256,
            reason, reference,
        });
        return new(round.RoundId, seal.Id, round.EventId, round.BranchId,
            actor.PrincipalId, actor.LinkedWorkerId, policy.Id, rules.RulesSha256,
            now, offers.Count, seal.SubmissionSetSha256, reason, reference,
            snapshot, ProcurementEvidenceEncoding.Digest(snapshot));
    }

    public static void RequireActor(PrincipalRfqPublishedRoundProof round,
        PrincipalRfqOfferOpeningControls controls, RfqPrincipalActor actor,
        IReadOnlyDictionary<string, Guid?> currentAliases, string action, bool opening)
    {
        Need(ProcurementPrincipalEvidence.Canonical(actor.PrincipalId)
            && ProcurementPrincipalEvidence.OptionalWorker(actor.LinkedWorkerId)
            && actor.Permissions is not null && actor.Permissions.Contains(action)
            && actor.TenantId == round.TenantId && actor.CompanyId == round.CompanyId
            && actor.Branches is not null
            && (actor.SelectedBranchId is null || actor.SelectedBranchId == round.BranchId)
            && (actor.OrganisationWide || actor.Branches.Contains(round.BranchId))
            && currentAliases.TryGetValue(actor.PrincipalId, out var alias)
            && alias == actor.LinkedWorkerId
            && (opening ? controls.OfficerPrincipalId == actor.PrincipalId
                : controls.ReaderPrincipalIds.Contains(actor.PrincipalId, StringComparer.Ordinal)),
            "RfqOfferOpeningActorDenied");
    }

    public static void RequireIndependent(RfqPrincipalActor actor,
        IReadOnlyList<RfqCommercialConflictParticipant> others,
        IReadOnlyDictionary<string, Guid?> currentAliases)
    {
        Need(currentAliases.TryGetValue(actor.PrincipalId, out var alias)
            && alias == actor.LinkedWorkerId, "RfqOfferOpeningAliasesUnavailable");
        foreach (var other in others)
        {
            Need(ProcurementPrincipalEvidence.Canonical(other.PrincipalId)
                && ProcurementPrincipalEvidence.OptionalWorker(other.RetainedWorkerAliasId)
                && currentAliases.ContainsKey(other.PrincipalId), "RfqOfferOpeningAliasesUnavailable");
            var known = new[] { other.RetainedWorkerAliasId, currentAliases[other.PrincipalId] };
            Need(other.PrincipalId != actor.PrincipalId
                && !known.Any(x => x is not null && x == actor.LinkedWorkerId),
                "RfqOfferOpeningActorConflict");
        }
    }

    private static bool ApprovedSnapshotMatches(PrincipalRfqPublishedRoundProof round,
        string snapshot, PolicyVersion policy, PrincipalRfqCommercialRoundPolicy rules)
    {
        if (round.ApprovalEvidenceSha256 != ProcurementEvidenceEncoding.Digest(snapshot)) return false;
        using var document = JsonDocument.Parse(snapshot);
        var pin = document.RootElement;
        return pin.GetProperty("coreCaseId").GetGuid() == round.RoundId
            && pin.GetProperty("eventId").GetGuid() == round.EventId
            && pin.GetProperty("preparedPlanId").GetGuid() == round.PreparedPlanId
            && pin.GetProperty("preparedPlanSha256").GetString() == round.PreparedPlanSha256
            && pin.GetProperty("policyVersionId").GetGuid() == policy.Id
            && pin.GetProperty("policyRulesSha256").GetString() == rules.RulesSha256
            && pin.GetProperty("tenantId").GetString() == round.TenantId
            && pin.GetProperty("companyId").GetGuid() == round.CompanyId
            && pin.GetProperty("branchId").GetGuid() == round.BranchId;
    }

    private static bool Instant(DateTimeOffset value) => value != default
        && value.Offset == TimeSpan.Zero && value.UtcTicks % 10 == 0;
    private static void Need(bool okay, string code)
    { if (!okay) throw new InvalidOperationException(code); }
}
