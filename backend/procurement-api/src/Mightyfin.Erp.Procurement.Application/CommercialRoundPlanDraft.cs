using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record RoundPlanCriterionInput(Guid LogicalId, int Order, string Code, string Stage, string Kind,
    string? Description, string? Rubric, decimal? Weight, decimal? MaximumScore, decimal? MinimumScore);
public sealed record RoundPlanParticipantInput(Guid LogicalId, int Order, string PrincipalId, string Duty, string? ConflictNote);
public sealed record RoundPlanDraftInput(string? Requirements, string? ProposedPolicyReference,
    string? DisclosureProposal, string? DocumentRequirements, DateTimeOffset? OpensAt, DateTimeOffset? ClosesAt,
    DateTimeOffset? TechnicalReleaseAt, DateTimeOffset? CommercialReleaseAt,
    IReadOnlyList<RoundPlanCriterionInput> Criteria, IReadOnlyList<RoundPlanParticipantInput> Participants);
public sealed record RoundPlanSource(string TenantId, Guid CompanyId, Guid EventId, Guid BranchId,
    Guid EvidenceId, long EventVersion, string SourceSha256, string OriginalMakerPrincipalId);
public sealed record RoundPlanActor(string PrincipalId, Guid? WorkerAlias);
public sealed record RoundPlanChange(CommercialRoundEnrollment? Enrollment, CommercialRoundPlan Plan,
    CommercialRoundPlanRevision Revision, IReadOnlyList<CommercialRoundCriterion> Criteria,
    IReadOnlyList<CommercialRoundParticipant> Participants, CommercialRoundPlanReceipt Receipt);

/// <summary>Incomplete planning only. No policy approval, score, invitation or formal offer.</summary>
public static class CommercialRoundPlanDraft
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, PropertyNameCaseInsensitive = false };
    public static void Validate(RoundPlanDraftInput? input)
    {
        if (input is null || input.Criteria is null || input.Participants is null || input.Criteria.Count > 100
            || input.Participants.Count > 100 || input.Criteria.Any(x => x is null) || input.Participants.Any(x => x is null)) Fail();
        Text(input!.Requirements, 10000); Text(input.ProposedPolicyReference, 200);
        Text(input.DisclosureProposal, 10000); Text(input.DocumentRequirements, 10000);
        foreach (var instant in new[] { input.OpensAt, input.ClosesAt, input.TechnicalReleaseAt, input.CommercialReleaseAt })
            if (instant is { } at && (at == default || at.Offset != TimeSpan.Zero
                || ProcurementEvidenceEncoding.DatabaseTimestamp(at) != at)) Fail();
        if (input.OpensAt is { } opens && input.ClosesAt is { } closes && opens >= closes) Fail();
        if (input.ClosesAt is { } close && (input.TechnicalReleaseAt < close || input.CommercialReleaseAt < close)) Fail();
        if (input.Criteria.Select(x => x.LogicalId).Distinct().Count() != input.Criteria.Count
            || input.Criteria.Select(x => x.Order).Distinct().Count() != input.Criteria.Count
            || input.Criteria.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Criteria.Count
            || input.Participants.Select(x => x.LogicalId).Distinct().Count() != input.Participants.Count
            || input.Participants.Select(x => x.Order).Distinct().Count() != input.Participants.Count
            || input.Participants.Select(x => (x.PrincipalId, x.Duty)).Distinct().Count() != input.Participants.Count) Fail();
        foreach (var criterion in input.Criteria)
        {
            if (criterion.LogicalId == Guid.Empty || criterion.Order is < 1 or > 100
                || string.IsNullOrWhiteSpace(criterion.Code) || criterion.Code.Length > 80 || criterion.Code != criterion.Code.Trim()
                || criterion.Stage is not ("Mandatory" or "Technical" or "Commercial") || criterion.Kind is not ("PassFail" or "Weighted")) Fail();
            Text(criterion.Description, 2000); Text(criterion.Rubric, 10000);
            foreach (var amount in new[] { criterion.Weight, criterion.MaximumScore, criterion.MinimumScore })
                if (amount is { } value && (value < 0 || !ExactDecimalJsonCodec.IsRepresentable(value, 18, 4))) Fail();
            if (criterion.Kind == "PassFail" && (criterion.Weight is not null || criterion.MaximumScore is not null || criterion.MinimumScore is not null)
                || criterion.MaximumScore is { } max && criterion.MinimumScore is { } min && min > max) Fail();
        }
        foreach (var participant in input.Participants)
        {
            if (participant.LogicalId == Guid.Empty || participant.Order is < 1 or > 100
                || !ProcurementPrincipalEvidence.Canonical(participant.PrincipalId)
                || participant.Duty is not ("Planner" or "Reviewer" or "OpeningOfficer" or "TechnicalEvaluator" or "CommercialEvaluator" or "ConflictReviewer")) Fail();
            Text(participant.ConflictNote, 2000);
        }
    }
    public static string Content(RoundPlanDraftInput input, string companyTimeZone)
    {
        Validate(input);
        if (string.IsNullOrWhiteSpace(companyTimeZone) || companyTimeZone.Length > 100) Fail();
        _ = TimeZoneInfo.FindSystemTimeZoneById(companyTimeZone);
        var content = JsonSerializer.Serialize(new {
            schemaVersion = 1, companyTimeZoneId = companyTimeZone, input.Requirements, input.ProposedPolicyReference,
            input.DisclosureProposal, input.DocumentRequirements, input.OpensAt, input.ClosesAt,
            input.TechnicalReleaseAt, input.CommercialReleaseAt,
            criteria = input.Criteria.OrderBy(x => x.Order).Select(x => new { x.LogicalId, x.Order, x.Code, x.Stage, x.Kind,
                x.Description, x.Rubric, weight = Exact(x.Weight), maximumScore = Exact(x.MaximumScore), minimumScore = Exact(x.MinimumScore) }),
            participants = input.Participants.OrderBy(x => x.Order),
        }, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(content) > 2000000) Fail();
        return content;
    }
    public static RoundPlanDraftInput Restore(CommercialRoundPlanRevision row,
        IReadOnlyList<CommercialRoundCriterion> criteria, IReadOnlyList<CommercialRoundParticipant> participants)
    {
        if (row is null || criteria is null || participants is null || criteria.Any(x => x is null) || participants.Any(x => x is null)
            || row.CriterionCount != criteria.Count || row.ParticipantCount != participants.Count
            || row.SchemaVersion != 1 || row.ExpectedVersion < 0 || row.ExpectedVersion == long.MaxValue || row.ResultVersion != row.ExpectedVersion + 1 || row.ContentSha256 != ProcurementEvidenceEncoding.Digest(row.ContentJson)
            || criteria.Any(x => x.RevisionId != row.Id || x.PlanId != row.PlanId || !SameScope(x, row))
            || participants.Any(x => x.RevisionId != row.Id || x.PlanId != row.PlanId || !SameScope(x, row))) Fail();
        var input = new RoundPlanDraftInput(row.Requirements, row.ProposedPolicyReference, row.DisclosureProposal,
            row.DocumentRequirements, row.OpensAt, row.ClosesAt, row.TechnicalReleaseAt, row.CommercialReleaseAt,
            criteria.Select(x => new RoundPlanCriterionInput(x.LogicalId, x.Order, x.Code, x.Stage, x.Kind, x.Description,
                x.Rubric, x.Weight, x.MaximumScore, x.MinimumScore)).ToArray(),
            participants.Select(x => new RoundPlanParticipantInput(x.LogicalId, x.Order, x.PrincipalId, x.Duty, x.ConflictNote)).ToArray());
        if (Content(input, row.CompanyTimeZoneId) != row.ContentJson) Fail();
        return input;
    }
    public static RoundPlanChange Create(RoundPlanSource source, RoundPlanActor actor, RoundPlanDraftInput input,
        string zone, Guid key, string requestSha, DateTimeOffset at)
    {
        Source(source); Actor(actor); Command(key, requestSha, at);
        var enrollment = new CommercialRoundEnrollment { TenantId = source.TenantId, LegalEntityId = source.CompanyId,
            SourcingEventId = source.EventId, BranchId = source.BranchId, SourceEvidenceId = source.EvidenceId,
            SourceEventVersion = source.EventVersion, SourceSha256 = source.SourceSha256,
            OriginalMakerPrincipalId = source.OriginalMakerPrincipalId, EnrolledByPrincipalId = actor.PrincipalId,
            EnrolledByWorkerAlias = actor.WorkerAlias, RecordedAt = at };
        var plan = new CommercialRoundPlan { TenantId = source.TenantId, LegalEntityId = source.CompanyId,
            EnrollmentId = enrollment.Id, SourcingEventId = source.EventId, BranchId = source.BranchId, MakerPrincipalId = actor.PrincipalId };
        return Build(enrollment, plan, null, actor, input, zone, "Create", null, key, requestSha, at);
    }
    public static RoundPlanChange Revise(CommercialRoundPlan plan, CommercialRoundPlanRevision previous,
        long expectedVersion, RoundPlanActor actor, RoundPlanDraftInput input, string zone, string operation,
        string? reason, Guid key, string requestSha, DateTimeOffset at)
    {
        Actor(actor); Command(key, requestSha, at);
        if (plan.Status != "Draft" || expectedVersion != plan.Version || previous.PlanId != plan.Id
            || previous.ResultStatus != "Draft" || at < previous.RecordedAt
            || previous.ResultVersion != expectedVersion || previous.SourcingEventId != plan.SourcingEventId || previous.BranchId != plan.BranchId || !SameScope(plan, previous) || operation is not ("Revise" or "Cancel")) Fail();
        if (operation == "Cancel") { Text(reason, 2000); if (reason is null || Content(input, zone) != previous.ContentJson) Fail(); }
        else if (reason is not null) Fail();
        return Build(null, plan, previous, actor, input, zone, operation, reason, key, requestSha, at);
    }
    private static RoundPlanChange Build(CommercialRoundEnrollment? enrollment, CommercialRoundPlan plan,
        CommercialRoundPlanRevision? previous, RoundPlanActor actor, RoundPlanDraftInput input, string zone,
        string operation, string? reason, Guid key, string requestSha, DateTimeOffset at)
    {
        var content = Content(input, zone); var expected = previous?.ResultVersion ?? 0;
        var revision = new CommercialRoundPlanRevision { TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            PlanId = plan.Id, SourcingEventId = plan.SourcingEventId, BranchId = plan.BranchId, PreviousRevisionId = previous?.Id,
            ExpectedVersion = expected, ResultVersion = checked(expected + 1), Operation = operation,
            ResultStatus = operation == "Cancel" ? "Cancelled" : "Draft", ActorPrincipalId = actor.PrincipalId,
            ActorWorkerAlias = actor.WorkerAlias, CompanyTimeZoneId = zone, Requirements = input.Requirements,
            ProposedPolicyReference = input.ProposedPolicyReference, DisclosureProposal = input.DisclosureProposal,
            DocumentRequirements = input.DocumentRequirements, OpensAt = input.OpensAt, ClosesAt = input.ClosesAt,
            TechnicalReleaseAt = input.TechnicalReleaseAt, CommercialReleaseAt = input.CommercialReleaseAt,
            CriterionCount = input.Criteria.Count, ParticipantCount = input.Participants.Count,
            ContentJson = content, ContentSha256 = ProcurementEvidenceEncoding.Digest(content), Reason = reason, RecordedAt = at };
        var criteria = input.Criteria.Select(x => new CommercialRoundCriterion { TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            PlanId = plan.Id, RevisionId = revision.Id, LogicalId = x.LogicalId, Order = x.Order, Code = x.Code, Stage = x.Stage,
            Kind = x.Kind, Description = x.Description, Rubric = x.Rubric, Weight = x.Weight, MaximumScore = x.MaximumScore, MinimumScore = x.MinimumScore }).ToArray();
        var participants = input.Participants.Select(x => new CommercialRoundParticipant { TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            PlanId = plan.Id, RevisionId = revision.Id, LogicalId = x.LogicalId, Order = x.Order,
            PrincipalId = x.PrincipalId, Duty = x.Duty, ConflictNote = x.ConflictNote }).ToArray();
        var response = JsonSerializer.Serialize(new { schemaVersion = 1, eventId = plan.SourcingEventId, planId = plan.Id,
            revisionId = revision.Id, version = revision.ResultVersion, status = revision.ResultStatus, operation,
            commandKey = key, reviewAvailable = false, publicationAvailable = false }, Json);
        var receipt = new CommercialRoundPlanReceipt { TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            SourcingEventId = plan.SourcingEventId, PlanId = plan.Id, BranchId = plan.BranchId, RevisionId = revision.Id,
            ResultVersion = revision.ResultVersion, Operation = operation, ActorPrincipalId = actor.PrincipalId,
            CommandKey = key, RequestSha256 = requestSha, ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response), ResponseStatusCode = operation == "Create" ? 201 : 200 };
        return new(enrollment, plan, revision, criteria, participants, receipt);
    }
    public static void VerifyReceipt(CommercialRoundPlanReceipt receipt, CommercialRoundPlan plan, CommercialRoundPlanRevision revision)
    {
        if (!SameScope(receipt, plan) || !SameScope(receipt, revision) || !SameScope(plan, revision)
            || revision.PlanId != plan.Id || revision.SourcingEventId != plan.SourcingEventId || revision.BranchId != plan.BranchId
            || receipt.PlanId != plan.Id || receipt.RevisionId != revision.Id
            || receipt.SourcingEventId != plan.SourcingEventId || receipt.BranchId != plan.BranchId
            || receipt.ResultVersion != revision.ResultVersion || receipt.Operation != revision.Operation
            || receipt.ActorPrincipalId != revision.ActorPrincipalId || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            || receipt.ResponseStatusCode != (revision.Operation == "Create" ? 201 : 200)) Fail();
        using var parsed = JsonDocument.Parse(receipt.ResponseJson); var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 10
            || root.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != 10
            || root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("eventId").GetGuid() != plan.SourcingEventId
            || root.GetProperty("planId").GetGuid() != plan.Id || root.GetProperty("revisionId").GetGuid() != revision.Id
            || root.GetProperty("version").GetInt64() != revision.ResultVersion || root.GetProperty("status").GetString() != revision.ResultStatus
            || root.GetProperty("operation").GetString() != revision.Operation || root.GetProperty("commandKey").GetGuid() != receipt.CommandKey
            || root.GetProperty("reviewAvailable").GetBoolean() || root.GetProperty("publicationAvailable").GetBoolean()) Fail();
    }
    public static bool SameScope(ProcurementRecord a, ProcurementRecord b) => a.TenantId == b.TenantId && a.LegalEntityId == b.LegalEntityId;
    private static string? Exact(decimal? x) => x is null ? null : ProcurementEvidenceEncoding.Decimal(x.Value);
    private static void Source(RoundPlanSource x) { if (x is null || string.IsNullOrWhiteSpace(x.TenantId) || x.TenantId.Length > 128
        || new[] { x.CompanyId, x.EventId, x.BranchId, x.EvidenceId }.Any(id => id == Guid.Empty) || x.EventVersion < 1
        || !Hash(x.SourceSha256) || !ProcurementPrincipalEvidence.Canonical(x.OriginalMakerPrincipalId)) Fail(); }
    private static void Actor(RoundPlanActor x) { if (x is null || !ProcurementPrincipalEvidence.Canonical(x.PrincipalId) || x.WorkerAlias == Guid.Empty) Fail(); }
    private static void Command(Guid key, string hash, DateTimeOffset at) { if (key == Guid.Empty || !Hash(hash) || at == default
        || at.Offset != TimeSpan.Zero || ProcurementEvidenceEncoding.DatabaseTimestamp(at) != at) Fail(); }
    private static bool Hash(string? x) => x is { Length: 64 } && x.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Text(string? x, int max) { if (x is not null && (string.IsNullOrWhiteSpace(x) || x.Length > max || x != x.Trim() || x.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))) Fail(); }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail() => throw new InvalidOperationException("CommercialRoundPlanInvalid");
}
