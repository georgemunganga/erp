using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Server-resolved current identity/scope facts, never an HTTP command DTO.
/// The eventual adapter must refresh these under the shared owner fences.</summary>
public sealed record RfqCurrentActor(string SubjectId, Guid WorkerId, string TenantId,
    Guid CompanyId, Guid? SelectedBranchId, IReadOnlySet<Guid> Branches,
    bool OrganisationWide, IReadOnlySet<string> Permissions, bool AllDepartments, IReadOnlySet<Guid> Departments);
public sealed record RfqDemandSelection(ApprovedPurchaseSource Source, long ExpectedRequestVersion, decimal Quantity);
public sealed record RfqDemandPin(Guid RequestId, Guid LineId, long RequestVersion, Guid SubmissionId,
    long SubmittedVersion, Guid CaseId, Guid OutcomeId, Guid DecisionId, string RequestSha,
    string PolicyDecisionSha, string PolicyRulesSha, Guid FinanceId, string FinanceSha,
    Guid MethodId, long MethodRevision, string MethodCode, int MinimumQuoteCount, Guid MakerWorkerId, Guid RequesterWorkerId,
    Guid AllocationId, Guid EventLineId, decimal Quantity, decimal UnitEstimate, decimal EstimatedValue,
    string UnitCode, string? CategoryCode, Guid? DepartmentId);
public sealed record RfqAllocationInstruction(string Kind, Guid AllocationId, long? ExpectedVersion,
    Guid RequestId, Guid RequestLineId, long ExpectedRequestVersion, Guid EventId, Guid EventLineId, decimal Quantity, decimal EstimatedValue,
    string CurrencyCode, string EffectKey, decimal ResultingAllocatedQuantity);
public sealed record RfqPublicationIntent(Guid EventId, string DraftFingerprint, string ReviewerSubjectId,
    Guid ReviewerWorkerId, decimal FullEventAmount, IReadOnlyList<string> Blockers);

/// <summary>Pure planning only: no persisted source authority, HTTP activation or separate ledger.
/// Immutable plans are not durable receipts until the eventual adapter persists them atomically.</summary>
public static class GovernedRfqDraft
{
    public sealed class Draft
    {
        public Guid Id { get; }
        public string TenantId { get; }
        public Guid CompanyId { get; }
        public Guid? BranchId { get; }
        public string CurrencyCode { get; }
        public string Title { get; }
        public string MakerSubjectId { get; }
        public Guid MakerWorkerId { get; }
        public string State { get; }
        public IReadOnlyList<RfqDemandPin> Lines { get; }
        public string Fingerprint { get; }
        private Draft(Guid id, RfqCurrentActor actor, Guid? branch, string currency, string title,
            string state, IEnumerable<RfqDemandPin> lines)
        {
            Id = id; TenantId = actor.TenantId; CompanyId = actor.CompanyId; BranchId = branch;
            CurrencyCode = currency; Title = title; MakerSubjectId = actor.SubjectId; MakerWorkerId = actor.WorkerId;
            State = state; Lines = Array.AsReadOnly(lines.ToArray());
            Fingerprint = Hash(new { Id, TenantId, CompanyId, BranchId, CurrencyCode, Title,
                MakerSubjectId, MakerWorkerId, State, Lines });
        }
        internal static Draft Create(Guid id, RfqCurrentActor actor, Guid? branch, string currency,
            string title, string state, IEnumerable<RfqDemandPin> lines) => new(id, actor, branch, currency, title, state, lines);
    }
    public sealed class Receipt
    {
        public Guid CommandKey { get; }
        public string Operation { get; }
        public string ActorSubjectId { get; }
        public Guid ActorWorkerId { get; }
        public string Fingerprint { get; }
        public Draft Result { get; }
        internal Receipt(Guid key, string operation, RfqCurrentActor actor, string fingerprint, Draft result)
        { CommandKey = key; Operation = operation; ActorSubjectId = actor.SubjectId; ActorWorkerId = actor.WorkerId;
            Fingerprint = fingerprint; Result = result; }
    }
    public sealed record Plan(Draft Result, IReadOnlyList<RfqAllocationInstruction> Instructions, Receipt Receipt, bool IsReplay);

    public static Plan Create(RfqCurrentActor actor, Guid eventId, Guid commandKey, string title,
        IReadOnlyList<RfqDemandSelection> selections, DateTimeOffset at, Receipt? prior = null)
    {
        Actor(actor, "procurement-sourcing-convert");
        title = Normalize(title);
        if (eventId == Guid.Empty || commandKey == Guid.Empty || title.Length is < 1 or > 200
            || selections is null || selections.Count is < 1 or > 200
            || selections.Any(x => x is null || x.Source is null)
            || selections.Select(x => x.Source.Line.Id).Distinct().Count() != selections.Count)
            Fail("RfqDraftInvalid");
        var fingerprint = Hash(new { eventId, title, sources = selections.OrderBy(x => x.Source.Line.Id)
            .Select(x => new { requestId = x.Source.Request.Id, lineId = x.Source.Line.Id,
                x.ExpectedRequestVersion, quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity) }) });
        var replay = Replay(prior, actor, "Create", commandKey, fingerprint, eventId);
        if (replay is not null) return replay;
        var first = selections[0].Source.Request;
        Scope(actor, first.BranchRef);
        var pins = new List<RfqDemandPin>(); var effects = new List<RfqAllocationInstruction>();
        foreach (var selection in selections.OrderBy(x => x.Source.Line.Id))
        {
            var s = selection.Source; var r = s.Request;
            Scope(actor, r.BranchRef); DepartmentScope(actor, r.DepartmentRef);
            if (r.BranchRef != first.BranchRef || r.CurrencyCode != first.CurrencyCode
                || r.Version != selection.ExpectedRequestVersion || selection.Quantity <= 0
                || !ExactDecimalJsonCodec.IsRepresentable(selection.Quantity, 18, 4)) Fail("RfqSourceChangedOrMixed");
            var available = CompetitiveSourcingSource.Check(s, actor.TenantId, actor.CompanyId, at);
            if (selection.Quantity > available.AvailableQuantity || s.Allocations.Any(x => x.TargetId == eventId)
                || available.MethodId != selections[0].Source.Method.Id
                || available.MethodRevision != selections[0].Source.Method.Version) Fail("RfqSourceUnavailable");
            var p = s.Submission;
            if (p.MakerWorkerId is not { } maker || maker == Guid.Empty
                || p.RequesterWorkerId is not { } requester || requester == Guid.Empty) Fail("RfqSourceWorkerUnknown");
            var value = ExactDecimalArithmetic.Multiply(selection.Quantity, available.ApprovedUnitPrice);
            var allocationId = EffectId(commandKey, s.Line.Id, "allocation");
            var eventLineId = EffectId(commandKey, s.Line.Id, "line");
            pins.Add(new(r.Id, s.Line.Id, r.Version, p.Id, p.SubmittedBusinessVersion, p.CoreApprovalCaseId,
                s.Outcome.Id, s.FinalDecision.Id, p.RequestSnapshotSha256, p.PolicyDecisionSha256, p.PolicyRulesSha256,
                p.FinanceSnapshotId, p.FinanceSnapshotSha256, available.MethodId, available.MethodRevision,
                s.Method.Code, available.MinimumQuoteCount, p.MakerWorkerId!.Value, p.RequesterWorkerId!.Value,
                allocationId, eventLineId, selection.Quantity, available.ApprovedUnitPrice, value, s.Line.UnitCode,
                s.Line.CategoryCode, r.DepartmentRef));
            effects.Add(new("Reserve", allocationId, null, r.Id, s.Line.Id, r.Version, eventId, eventLineId, selection.Quantity, value,
                r.CurrencyCode, $"rfq.reserve.{commandKey:N}.{s.Line.Id:N}",
                ExactDecimalArithmetic.Add(s.Line.AllocatedQuantity, selection.Quantity)));
        }
        _ = ExactDecimalArithmetic.Sum(pins.Select(x => x.EstimatedValue));
        var draft = Draft.Create(eventId, actor, first.BranchRef, first.CurrencyCode, title, "Draft", pins);
        return NewPlan(actor, "Create", commandKey, fingerprint, draft, effects);
    }

    public static Plan Cancel(RfqCurrentActor actor, Draft draft, Guid commandKey, string expectedDraftFingerprint, string reason,
        IReadOnlyList<ApprovedPurchaseSource> sources, Receipt? prior = null)
    {
        Actor(actor, "procurement-sourcing-cancel"); OwnScope(actor, draft);
        reason = Normalize(reason);
        if (commandKey == Guid.Empty || reason.Length is < 1 or > 2000) Fail("RfqCancellationInvalid");
        var fingerprint = Hash(new { eventId = draft.Id, expectedDraftFingerprint, reason });
        var replay = Replay(prior, actor, "Cancel", commandKey, fingerprint, draft.Id);
        if (replay is not null) return replay;
        if (draft.Fingerprint != expectedDraftFingerprint || draft.State != "Draft" || sources is null || sources.Count != draft.Lines.Count
            || sources.Select(x => x.Line.Id).Distinct().Count() != sources.Count) Fail("RfqDraftNotCancellable");
        var effects = new List<RfqAllocationInstruction>();
        foreach (var pin in draft.Lines)
        {
            var source = sources.SingleOrDefault(x => x.Line.Id == pin.LineId);
            if (source is null || source.Request.Id != pin.RequestId || source.Request.BranchRef != draft.BranchId
                || source.Request.CurrencyCode != draft.CurrencyCode) Fail("RfqReleaseSourceMismatch");
            // Historical release deliberately does not rerun current catalog, method or approval eligibility.
            _ = PurchaseDemandAllocationBalance.Evaluate(source!.Request, source.Line, source.Allocations,
                source.AllocationOwners ?? [], actor.TenantId, actor.CompanyId);
            var rows = source.Allocations.Where(x => x.TargetType == "SourcingEvent" && x.TargetId == draft.Id).ToArray();
            if (rows.Length != 1 || rows[0].Id != pin.AllocationId || rows[0].Status != "Reserved"
                || rows[0].Quantity != pin.Quantity || rows[0].EstimatedValue != pin.EstimatedValue)
                Fail("RfqReleaseAllocationMismatch");
            var row = rows[0];
            effects.Add(new("Release", row.Id, row.Version, pin.RequestId, pin.LineId, source.Request.Version, draft.Id, pin.EventLineId,
                row.Quantity, row.EstimatedValue, draft.CurrencyCode, $"rfq.release.{commandKey:N}.{row.Id:N}",
                ExactDecimalArithmetic.Subtract(source.Line.AllocatedQuantity, row.Quantity)));
        }
        // Retain original maker even if a future authorized cancellation adapter supports reassignment.
        var maker = actor with { SubjectId = draft.MakerSubjectId, WorkerId = draft.MakerWorkerId };
        var cancelled = Draft.Create(draft.Id, maker, draft.BranchId, draft.CurrencyCode, draft.Title, "Cancelled", draft.Lines);
        return NewPlan(actor, "Cancel", commandKey, fingerprint, cancelled, effects);
    }

    public static RfqPublicationIntent PreparePublicationIntent(RfqCurrentActor actor, Draft draft, RfqCurrentActor reviewer)
    {
        Actor(actor, "procurement-sourcing-submit"); OwnScope(actor, draft);
        Actor(reviewer, "procurement-sourcing-publish");
        if (reviewer.TenantId != draft.TenantId || reviewer.CompanyId != draft.CompanyId) Fail("RfqReviewerScopeMismatch");
        Scope(reviewer, draft.BranchId);
        foreach (var pin in draft.Lines) DepartmentScope(reviewer, pin.DepartmentId);
        if (draft.State != "Draft" || reviewer.SubjectId == draft.MakerSubjectId || reviewer.WorkerId == draft.MakerWorkerId
            || draft.Lines.Any(x => x.MakerWorkerId == reviewer.WorkerId || x.RequesterWorkerId == reviewer.WorkerId))
            Fail("RfqReviewerNotIndependent");
        return new(draft.Id, draft.Fingerprint, reviewer.SubjectId, reviewer.WorkerId,
            ExactDecimalArithmetic.Sum(draft.Lines.Select(x => x.EstimatedValue)),
            Array.AsReadOnly(new[] { "SourcingPublicationPolicyUnavailable", "CorePublicationReviewNotOpened",
                "CurrentSourceAndSupplierDocumentEvidenceRequired", "FinitePublicationAuthorityNotVerified" }));
    }

    private static Plan NewPlan(RfqCurrentActor actor, string operation, Guid key, string fingerprint,
        Draft draft, List<RfqAllocationInstruction> effects) => new(draft, effects.AsReadOnly(),
            new Receipt(key, operation, actor, fingerprint, draft), false);
    private static Plan? Replay(Receipt? prior, RfqCurrentActor actor, string operation, Guid key, string fingerprint, Guid id)
    {
        if (prior is null) return null;
        OwnScope(actor, prior.Result);
        if (prior.CommandKey != key || prior.Operation != operation || prior.Fingerprint != fingerprint
            || prior.ActorSubjectId != actor.SubjectId || prior.ActorWorkerId != actor.WorkerId || prior.Result.Id != id)
            Fail("RfqCommandKeyConflict");
        return new(prior.Result, Array.Empty<RfqAllocationInstruction>(), prior, true);
    }
    private static void OwnScope(RfqCurrentActor actor, Draft draft)
    {
        if (actor.TenantId != draft.TenantId || actor.CompanyId != draft.CompanyId
            || actor.SubjectId != draft.MakerSubjectId || actor.WorkerId != draft.MakerWorkerId) Fail("RfqDraftNotOwned");
        Scope(actor, draft.BranchId);
        foreach (var pin in draft.Lines) DepartmentScope(actor, pin.DepartmentId);
    }
    private static void Actor(RfqCurrentActor actor, string permission)
    {
        if (string.IsNullOrWhiteSpace(actor.SubjectId) || actor.WorkerId == Guid.Empty
            || string.IsNullOrWhiteSpace(actor.TenantId) || actor.CompanyId == Guid.Empty
            || !actor.Permissions.Contains(permission)) Fail("RfqActorDenied");
    }
    private static void Scope(RfqCurrentActor actor, Guid? branch)
    {
        if (branch == Guid.Empty || actor.SelectedBranchId == Guid.Empty
            || actor.SelectedBranchId is { } selected && selected != branch
            || !actor.OrganisationWide && (branch is null || !actor.Branches.Contains(branch.Value))) Fail("RfqBranchDenied");
    }
    private static void DepartmentScope(RfqCurrentActor actor, Guid? department)
    {
        if (department == Guid.Empty || !actor.AllDepartments && (department is null || !actor.Departments.Contains(department.Value)))
            Fail("RfqDepartmentDenied");
    }
    private static Guid EffectId(Guid key, Guid line, string purpose) => new(Convert.FromHexString(Hash(new { key, line, purpose })[..32]));
    private static string Hash<T>(T value) => ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(value));
    private static string Normalize(string? value) => string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
