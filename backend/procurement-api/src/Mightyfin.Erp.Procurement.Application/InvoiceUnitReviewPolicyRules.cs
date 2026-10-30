using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Pure proposed policy contract. Inputs are current owner projections, never HTTP authority.
/// No policy, permissions, database adapter or runtime provider is installed by this helper.</summary>
public static class InvoiceUnitReviewPolicyRules
{
    public sealed record Stage(string Code, string Mode, IReadOnlyList<string> Principals);
    public sealed record Document(IReadOnlyList<Stage> Stages)
    {
        public int SchemaVersion { get; init; } = 1;
        public string EvidenceMode { get; init; } = "DocumentReview";
    }
    // For source 3, WorkerId is optional genuine Core-link alias metadata, not employment.
    public sealed record Person(string TenantId, Guid LegalEntityId, string PrincipalId, Guid? WorkerId,
        bool Active, IReadOnlySet<string> Permissions, bool OrganisationWide, IReadOnlySet<Guid> AllowedBranches);
    public sealed record Assignee(string PrincipalId, Guid? WorkerId);
    public sealed record ProjectedStage(int Sequence, string Code, string CoreMode, int RequiredApprovals,
        IReadOnlyList<Assignee> Assignees);
    public sealed record Projection(Guid PolicyId, int PolicyVersion, string RulesSha256, string TenantId,
        Guid LegalEntityId, Guid? BranchId, string MakerPrincipalId, Guid? MakerWorkerId,
        IReadOnlyList<ProjectedStage> Stages)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SchemaVersion { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EvidenceMode { get; init; }
    }

    public static Document Parse(string json) => ParseMode(json, false);
    public static Document ParseDraft(string json) => ParseMode(json, true);
    public static IReadOnlyList<string> Validate(string json, bool draft = false)
    {
        try { _ = ParseMode(json, draft); return Array.Empty<string>(); }
        catch (InvalidOperationException) { return new[] { draft ? "InvoiceUnitReviewDraftInvalid" : "InvoiceUnitReviewNamedStagesRequired" }; }
    }
    public static bool IsDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) return false;
        try { using var value = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            return value.RootElement.ValueKind == JsonValueKind.Object && value.RootElement.TryGetProperty("transactionType", out var type)
                && type.ValueKind == JsonValueKind.String && type.GetString() == "InvoiceUnitReview"; }
        catch (JsonException) { return false; }
    }
    private static Document ParseMode(string json, bool draft)
    {
        try { return ParseCore(json, draft); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException
            or FormatException or OverflowException or KeyNotFoundException)
        { throw new InvalidOperationException("InvoiceUnitReviewPolicyUnavailable", error); }
    }

    private static Document ParseCore(string json, bool draft)
    {
        Need(!string.IsNullOrWhiteSpace(json) && json.Length <= 65536 && Encoding.UTF8.GetByteCount(json) <= 65536);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        // Shared canonical validation rejects duplicate properties; do not create another JSON codec.
        _ = ProcurementEvidenceEncoding.PolicyJsonDigest(json);
        var root = document.RootElement;
        var schema = root.GetProperty("schemaVersion").GetInt32();
        if (schema == 1) Only(root, "schemaVersion", "transactionType", "stages");
        else if (schema == 2) Only(root, "schemaVersion", "transactionType", "evidenceMode", "stages");
        else Need(false);
        Need(root.GetProperty("transactionType").GetString() == "InvoiceUnitReview"
            && (schema == 1 || root.GetProperty("evidenceMode").GetString() == "ManualAttestation"));
        var rows = root.GetProperty("stages"); Need(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() <= 16 && (draft || rows.GetArrayLength() > 0));
        var stages = new List<Stage>(); var total = 0;
        foreach (var row in rows.EnumerateArray())
        {
            Only(row, "code", "mode", "principals");
            var code = row.GetProperty("code").GetString(); var mode = row.GetProperty("mode").GetString();
            Need(code is { Length: > 0 and <= 40 } && code[0] is >= 'A' and <= 'Z'
                && code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') && mode is "One" or "All");
            var members = row.GetProperty("principals");
            Need(members.ValueKind == JsonValueKind.Array && members.GetArrayLength() <= 32 && (draft || members.GetArrayLength() > 0));
            var principals = members.EnumerateArray().Select(x => x.GetString()!).ToArray();
            Need(principals.All(Canonical) && principals.Distinct(StringComparer.Ordinal).Count() == principals.Length);
            total = checked(total + principals.Length); Need(total <= 128);
            stages.Add(new(code!, mode!, Array.AsReadOnly(principals.Order(StringComparer.Ordinal).ToArray())));
        }
        Need(stages.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() == stages.Count);
        return new(stages.AsReadOnly()) { SchemaVersion = schema,
            EvidenceMode = schema == 1 ? "DocumentReview" : "ManualAttestation" };
    }

    public static Projection Resolve(PolicyVersion policy, string tenant, Guid company, Guid? branch,
        string maker, Guid? makerWorker, IReadOnlyList<Person> people, DateTimeOffset now)
    {
        ValidatePolicy(policy, tenant, company, branch, maker, makerWorker, now);
        var rules = Parse(policy.RulesJson);
        var ids = rules.Stages.SelectMany(x => x.Principals).Distinct(StringComparer.Ordinal).ToArray();
        Need(people is not null && people.Count == ids.Length && people.All(x => x is not null)
            && people.Select(x => x.PrincipalId).Distinct(StringComparer.Ordinal).Count() == people.Count
            && people.All(x => ids.Contains(x.PrincipalId, StringComparer.Ordinal)));
        var stages = new List<ProjectedStage>();
        foreach (var stage in rules.Stages)
        {
            var members = new List<Assignee>();
            foreach (var principal in stage.Principals)
            {
                var person = people.Single(x => x.PrincipalId == principal);
                Need(person.TenantId == tenant && person.LegalEntityId == company && person.Active
                    && ProcurementPrincipalEvidence.Independent(person.PrincipalId, person.WorkerId, maker, makerWorker) && person.Permissions is not null
                    && person.Permissions.Contains("procurement-access") && person.Permissions.Contains(InvoiceRetainedUnitReview.ReviewPermission)
                    && person.AllowedBranches is not null && !person.AllowedBranches.Contains(Guid.Empty)
                    && (person.OrganisationWide || branch is { } site && person.AllowedBranches.Contains(site)));
                members.Add(new(person.PrincipalId, person.WorkerId));
            }
            Need(members.Where(x => x.WorkerId.HasValue).Select(x => x.WorkerId).Distinct().Count() == members.Count(x => x.WorkerId.HasValue));
            stages.Add(new(stages.Count + 1, stage.Code, stage.Mode == "One" ? "Any" : "All",
                stage.Mode == "One" ? 1 : members.Count, members.AsReadOnly()));
        }
        return new Projection(policy.Id, policy.PolicyVersionNumber, ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            tenant, company, branch, maker, makerWorker, stages.AsReadOnly())
            { SchemaVersion = rules.SchemaVersion == 2 ? 2 : null,
                EvidenceMode = rules.SchemaVersion == 2 ? rules.EvidenceMode : null };
    }

    /// <summary>Retained graph is historical. Reparse the actual owned policy without
    /// manufacturing current account eligibility for reviewers who already completed work.</summary>
    public static void VerifyRetainedProjection(Projection pinned, PolicyVersion policy, DateTimeOffset now,
        bool allowRetiredAtHistoricalTime = false)
    {
        ValidatePolicy(policy, pinned.TenantId, pinned.LegalEntityId, pinned.BranchId,
            pinned.MakerPrincipalId, pinned.MakerWorkerId, now, allowRetiredAtHistoricalTime);
        var rules = Parse(policy.RulesJson);
        Need(policy.Id == pinned.PolicyId && policy.PolicyVersionNumber == pinned.PolicyVersion
            && ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) == pinned.RulesSha256
            && pinned.Stages.Count == rules.Stages.Count
            && (pinned.SchemaVersion ?? 1) == rules.SchemaVersion
            && (pinned.EvidenceMode ?? "DocumentReview") == rules.EvidenceMode);
        for (var i = 0; i < rules.Stages.Count; i++)
        {
            var expected = rules.Stages[i]; var retained = pinned.Stages[i];
            Need(retained.Sequence == i + 1 && retained.Code == expected.Code
                && retained.CoreMode == (expected.Mode == "One" ? "Any" : "All")
                && retained.RequiredApprovals == (expected.Mode == "One" ? 1 : expected.Principals.Count)
                && retained.Assignees.Select(x => x.PrincipalId).SequenceEqual(expected.Principals)
                && retained.Assignees.All(x => ProcurementPrincipalEvidence.Independent(x.PrincipalId, x.WorkerId, pinned.MakerPrincipalId, pinned.MakerWorkerId))
                && retained.Assignees.Where(x => x.WorkerId.HasValue).Select(x => x.WorkerId).Distinct().Count() == retained.Assignees.Count(x => x.WorkerId.HasValue));
        }
    }

    private static void ValidatePolicy(PolicyVersion policy, string tenant, Guid company, Guid? branch,
        string maker, Guid? makerWorker, DateTimeOffset now, bool allowRetiredAtHistoricalTime = false)
    {
        Need(policy is not null && policy.TenantId == tenant && policy.LegalEntityId == company && company != Guid.Empty
            && !string.IsNullOrWhiteSpace(tenant) && branch != Guid.Empty && Canonical(maker) && ProcurementPrincipalEvidence.OptionalWorker(makerWorker)
            && policy.Id != Guid.Empty && policy.PolicyKey == InvoiceRetainedUnitReview.ReviewPolicyKey
            && policy.PolicyVersionNumber > 0
            && (policy.State == "Published" || allowRetiredAtHistoricalTime && policy.State == "Retired")
            && policy.PublishedAt is { } published
            && published <= now && policy.EffectiveFrom != default && policy.EffectiveFrom <= now
            && (policy.EffectiveTo is null || now < policy.EffectiveTo)
            && Canonical(policy.CreatedBy) && Canonical(policy.PublishedBy) && policy.CreatedBy != policy.PublishedBy
            && !string.IsNullOrWhiteSpace(policy.PublicationReason) && now != default);
    }

    /// <summary>Compare the freshly resolved owned policy projection with actual Core execution.
    /// This validates its shape; Core engine and retained verifier own decision/final-state semantics.</summary>
    public static void VerifyCaseGraph(Projection projection, ApprovalCase workflow,
        IReadOnlyList<InvoiceUnitReviewAssignmentWorker> workers)
    {
        Need(projection is not null && workflow is not null && workers is not null
            && workflow.TenantId == projection.TenantId && workflow.LegalEntityId == projection.LegalEntityId
            && workflow.PolicyVersionId == projection.PolicyId && workflow.PolicyVersionNumber == projection.PolicyVersion
            && workflow.MakerSubjectId == projection.MakerPrincipalId && workflow.SourceModule == "procurement"
            && workflow.SourceType == InvoiceRetainedUnitReview.ReviewSourceType && workflow.Stages is not null
            && workflow.Stages.Count == projection.Stages.Count);
        var assignments = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
        Need(assignments.Select(x => x.Id).Distinct().Count() == assignments.Length
            && workers.Count == assignments.Length && workers.Select(x => x.AssignmentId).Distinct().Count() == workers.Count);
        foreach (var stage in projection.Stages)
        {
            var actual = workflow.Stages.SingleOrDefault(x => x.Sequence == stage.Sequence);
            Need(actual is not null && actual.TenantId == projection.TenantId && actual.LegalEntityId == projection.LegalEntityId
                && actual.CaseId == workflow.Id && actual.Code == stage.Code && actual.Mode == stage.CoreMode
                && actual.RequiredApprovals == stage.RequiredApprovals && actual.Assignments.Count == stage.Assignees.Count);
            foreach (var expected in stage.Assignees)
            {
                var assignment = actual!.Assignments.SingleOrDefault(x => x.AssigneeSubjectId == expected.PrincipalId);
                Need(assignment is not null && assignment.TenantId == projection.TenantId && assignment.LegalEntityId == projection.LegalEntityId
                    && assignment.StageId == actual.Id && assignment.CaseId == workflow.Id && assignment.DelegatedFromSubjectId is null
                    && assignment.PermissionKey == InvoiceRetainedUnitReview.ReviewPermission
                    && workers.Any(x => x.AssignmentId == assignment.Id && x.PrincipalId == expected.PrincipalId && x.WorkerId == expected.WorkerId));
            }
        }
    }

    private static bool Canonical(string? value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty && value == id.ToString("D");
    private static void Only(JsonElement row, params string[] keys)
    {
        Need(row.ValueKind == JsonValueKind.Object && row.EnumerateObject().Count() == keys.Length
            && row.EnumerateObject().All(x => keys.Contains(x.Name, StringComparer.Ordinal)));
    }
    private static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition) { if (!condition) throw new InvalidOperationException("InvoiceUnitReviewPolicyUnavailable"); }
}
