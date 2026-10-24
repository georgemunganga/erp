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
    PrincipalRfqPublishedRoundProof? AcceptingRound, string CoreOutcomeSha256);

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
            policyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
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
        IReadOnlyList<ApprovalDecision> decisions, ApprovalDecision terminalDecision, RfqPrincipalActor signer,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(decisions);
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
            && pin.GetProperty("policyKey").GetString()
                == PrincipalRfqCommercialRoundPolicyRules.PolicyKey
            && pin.GetProperty("policyVersionNumber").GetInt32() == policy.PolicyVersionNumber
            && pin.GetProperty("policyRulesSha256").GetString()
                == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && pin.GetProperty("opensAt").GetDateTimeOffset() == opening.OpensAt
            && pin.GetProperty("closesAt").GetDateTimeOffset() == opening.ClosesAt
            && pin.GetProperty("closerPrincipalId").GetString() == opening.ClosingOfficerPrincipalId
            && (pin.GetProperty("closerWorkerAliasId").ValueKind == JsonValueKind.Null
                ? opening.ClosingOfficerWorkerAliasId is null
                : pin.GetProperty("closerWorkerAliasId").GetGuid() == opening.ClosingOfficerWorkerAliasId)
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
            && current.OpenedAt == opening.Case.OpenedAt
            && (current.State == "Approved"
                ? !string.IsNullOrWhiteSpace(current.CompletionKey)
                    && current.CompletionKey == terminalDecision.IdempotencyKey
                : current.CompletionKey is null)
            && source.Id == prepared.SourcingEventId
            && source.Status == "Published"
            && source.BranchRef == prepared.BranchId
            && source.TenantId == prepared.TenantId
            && source.LegalEntityId == prepared.LegalEntityId
            && current.Stages.Count == opening.Case.Stages.Count
            && terminalDecision.CaseId == current.Id
            && terminalDecision.TenantId == current.TenantId
            && terminalDecision.LegalEntityId == current.LegalEntityId
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
        Need(decisions.Count > 0
            && decisions.Select(x => x.Id).Distinct().Count() == decisions.Count
            && decisions.Any(x => x.Id == terminalDecision.Id)
            && decisions.Single(x => x.Id == terminalDecision.Id) is { } retainedTerminal
            && retainedTerminal.IdempotencyKey == terminalDecision.IdempotencyKey
            && retainedTerminal.Action == terminalDecision.Action
            && retainedTerminal.AssignmentId == terminalDecision.AssignmentId
            && retainedTerminal.ActorSubjectId == terminalDecision.ActorSubjectId
            && retainedTerminal.DecidedAt == terminalDecision.DecidedAt
            && decisions.Select(x => x.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() == decisions.Count
            && decisions.Select(x => x.AssignmentId).Distinct().Count() == decisions.Count
            && CoreHistoryMatches(opening.Case.OpenedAt, current, currentStages,
                decisions, terminalDecision),
            "RfqCommercialRoundReviewGraphChanged");
        var assignment = current.Stages.SelectMany(x => x.Assignments)
            .SingleOrDefault(x => x.Id == terminalDecision.AssignmentId);
        Need(assignment is not null && assignment.CaseId == current.Id
            && assignment.StageId == terminalDecision.StageId
            && assignment.AssigneeSubjectId == signer.PrincipalId
            && assignment.PermissionKey == PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
            && assignment.State == (terminalDecision.Action switch
            {
                "Approve" => "Approved", "Return" => "Returned", "Reject" => "Rejected",
                _ => "Impossible",
            }),
            "RfqCommercialRoundDecisionChanged");
        var state = terminalDecision.Action switch
        {
            "Approve" when current.State == "Approved" => "Approved",
            "Return" when current.State == "Returned" => "Returned",
            "Reject" when current.State == "Rejected" => "Rejected",
            _ => throw new InvalidOperationException("RfqCommercialRoundDecisionChanged"),
        };
        var outcome = JsonSerializer.Serialize(new
        {
            caseId = current.Id, current.State, current.CompletionKey,
            completedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(current.CompletedAt),
            stages = currentStages.Select(s => new
            {
                s.Id, s.Sequence, s.State,
                assignments = s.Assignments.OrderBy(a => a.Id).Select(a => new
                { a.Id, a.State, a.AssigneeSubjectId }),
            }),
            decisions = decisions.OrderBy(d => d.DecidedAt).ThenBy(d => d.Id).Select(d => new
            { d.Id, d.StageId, d.AssignmentId, d.ActorSubjectId, d.Action,
                d.IdempotencyKey, d.Reason,
                decidedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(d.DecidedAt) }),
        });
        var outcomeSha = ProcurementEvidenceEncoding.Digest(outcome);
        if (state != "Approved")
            return new(state, terminalDecision.Id, signer.PrincipalId,
                terminalDecision.Reason, null, outcomeSha);
        Need(current.Stages.All(x => x.State == "Approved")
            && terminalDecision.StageId == currentStages[^1].Id
            && current.CompletedAt is { } completed
            && completed < opening.OpensAt
            && opening.OpensAt < opening.ClosesAt,
            "RfqCommercialRoundApprovalTooLateOrIncomplete");
        // The retained opening uses this same stable identifier. A retry or
        // reload must never mint a second round identity for one Core case.
        var proof = new PrincipalRfqPublishedRoundProof(current.Id, source.Id,
            prepared.Id, prepared.SnapshotSha256, current.Id,
            terminalDecision.Id, opening.SnapshotSha256,
            prepared.TenantId, prepared.LegalEntityId, prepared.BranchId,
            source.CurrencyCode, source.Version, opening.OpensAt,
            opening.ClosesAt, opening.ClosingOfficerPrincipalId,
            opening.ClosingOfficerWorkerAliasId, "Accepting", opening.SupplierIds);
        return new("Approved", terminalDecision.Id, signer.PrincipalId, null, proof, outcomeSha);
    }

    private static void Need(bool okay, string code)
    {
        if (!okay) throw new InvalidOperationException(code);
    }

    private static bool CoreHistoryMatches(DateTimeOffset openedAt, ApprovalCase current,
        IReadOnlyList<ApprovalStage> stages, IReadOnlyList<ApprovalDecision> decisions,
        ApprovalDecision terminal)
    {
        if (openedAt == default || decisions.Count == 0
            || decisions.Count(x => x.Action is "Return" or "Reject") > 1
            || decisions.Any(x => x.CaseId != current.Id || x.TenantId != current.TenantId
                || x.LegalEntityId != current.LegalEntityId
                || string.IsNullOrWhiteSpace(x.IdempotencyKey)
                || x.DecidedAt < openedAt || x.DecidedAt > terminal.DecidedAt
                || !stages.Any(s => s.Id == x.StageId
                    && s.Assignments.Any(a => a.Id == x.AssignmentId
                        && a.StageId == s.Id && a.CaseId == current.Id
                        && a.AssigneeSubjectId == x.ActorSubjectId)))) return false;
        var previousVoteAt = openedAt;
        var terminalStage = stages.SingleOrDefault(x => x.Id == terminal.StageId);
        if (terminalStage is null) return false;
        foreach (var stage in stages)
        {
            var votes = decisions.Where(x => x.StageId == stage.Id).ToArray();
            if (votes.Length > 0 && votes.Min(x => x.DecidedAt) < previousVoteAt) return false;
            if (votes.Length > 0) previousVoteAt = votes.Max(x => x.DecidedAt);
            if (votes.Any(v => !stage.Assignments.Any(a => a.Id == v.AssignmentId
                    && a.AssigneeSubjectId == v.ActorSubjectId))) return false;
            foreach (var assignment in stage.Assignments)
            {
                var vote = votes.SingleOrDefault(x => x.AssignmentId == assignment.Id);
                var valid = assignment.State switch
                {
                    "Approved" => vote?.Action == "Approve",
                    "Returned" => vote?.Action == "Return",
                    "Rejected" => vote?.Action == "Reject",
                    "Pending" or "Superseded" => vote is null,
                    _ => false,
                };
                if (!valid) return false;
            }
            var approvals = votes.Count(x => x.Action == "Approve");
            if (stage.State == "Approved")
            {
                if (approvals != stage.RequiredApprovals
                    || stage.Assignments.Any(x => x.State is "Pending" or "Returned" or "Rejected")
                    || stage.Mode == "All" && stage.Assignments.Any(x => x.State != "Approved")
                    || stage.Mode == "Any" && stage.Assignments.Count(x => x.State == "Approved") != 1)
                    return false;
            }
            else if (stage.Id == terminalStage.Id && current.State is ("Returned" or "Rejected"))
            {
                if (stage.State != current.State || votes.Count(x => x.Id == terminal.Id) != 1
                    || stage.Assignments.Count(x => x.State == current.State) != 1) return false;
            }
            else if (stage.State != "Waiting" || votes.Length != 0) return false;
            if (stage.Sequence < terminalStage.Sequence && stage.State != "Approved") return false;
            if (stage.Sequence > terminalStage.Sequence && stage.State != "Waiting") return false;
        }
        return current.State == "Approved"
            ? stages.All(x => x.State == "Approved") && terminalStage == stages[^1]
            : current.State is ("Returned" or "Rejected")
                && terminalStage.State == current.State;
    }
}
