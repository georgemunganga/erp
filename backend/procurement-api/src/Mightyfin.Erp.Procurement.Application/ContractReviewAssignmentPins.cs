using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public static class ContractReviewAssignmentPins
{
    public sealed record Pin(Guid AssignmentId, string PrincipalId, Guid? WorkerAliasId);

    /// <summary>Retained named assignment proof. Current Core roles and linked-worker
    /// aliases are checked separately at each effect; history itself is never rewritten.</summary>
    public static IReadOnlyList<Pin> Verify(ContractReviewSubmission source, ApprovalCase approval,
        PolicyVersion policy)
    {
        try
        {
            Need(source.CoreCaseId == approval.Id && approval.SourceId == source.Id
                && approval.TenantId == source.TenantId && approval.LegalEntityId == source.LegalEntityId
                && approval.SourceModule == "procurement"
                && approval.SourceType == ContractReviewCoreWorkflow.SourceType
                && approval.SourceVersion == source.ContractBusinessVersion + 1
                && approval.PolicyVersionId == source.PolicyVersionId
                && approval.PolicyVersionNumber == source.PolicyVersionNumber
                && approval.MakerSubjectId == source.MakerPrincipalId
                && policy.Id == source.PolicyVersionId && policy.TenantId == source.TenantId
                && policy.LegalEntityId == source.LegalEntityId
                && policy.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey
                && policy.PolicyVersionNumber == source.PolicyVersionNumber
                && source.PolicyRulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                && source.AssignmentPinsSha256 == ProcurementEvidenceEncoding.Digest(source.AssignmentPinsJson),
                "ContractReviewAssignmentSourceChanged");
            var rules = ProcurementContractReviewPolicyRules.Parse(policy.RulesJson);
            using var document = JsonDocument.Parse(source.AssignmentPinsJson);
            var root = document.RootElement;
            Need(root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Count() == 4
                && root.GetProperty("schemaVersion").GetInt32() == 1
                && root.GetProperty("caseId").GetGuid() == approval.Id
                && root.GetProperty("submissionId").GetGuid() == source.Id,
                "ContractReviewAssignmentPinsChanged");
            var stages = root.GetProperty("stages").EnumerateArray().ToArray();
            var caseStages = approval.Stages.OrderBy(x => x.Sequence).ToArray();
            Need(stages.Length == rules.Stages.Count && stages.Length == caseStages.Length,
                "ContractReviewAssignmentPinsChanged");
            var pins = new List<Pin>();
            for (var i = 0; i < stages.Length; i++)
            {
                var row = stages[i]; var actual = caseStages[i]; var rule = rules.Stages[i];
                Need(row.EnumerateObject().Count() == 6 && row.GetProperty("Id").GetGuid() == actual.Id
                    && row.GetProperty("Sequence").GetInt32() == actual.Sequence
                    && row.GetProperty("Code").GetString() == actual.Code && actual.Code == rule.Code
                    && row.GetProperty("Mode").GetString() == actual.Mode
                    && actual.Mode == (rule.Mode == "One" ? "Any" : "All")
                    && row.GetProperty("RequiredApprovals").GetInt32() == actual.RequiredApprovals
                    && actual.RequiredApprovals == (rule.Mode == "One" ? 1 : rule.PrincipalIds.Count)
                    && actual.CaseId == approval.Id && actual.TenantId == source.TenantId
                    && actual.LegalEntityId == source.LegalEntityId,
                    "ContractReviewAssignmentPinsChanged");
                var assignmentRows = row.GetProperty("assignments").EnumerateArray().ToArray();
                Need(assignmentRows.Length == actual.Assignments.Count
                    && assignmentRows.Length == rule.PrincipalIds.Count,
                    "ContractReviewAssignmentPinsChanged");
                foreach (var candidate in assignmentRows)
                {
                    Need(candidate.EnumerateObject().Count() == 3, "ContractReviewAssignmentPinsChanged");
                    var id = candidate.GetProperty("Id").GetGuid();
                    var principal = candidate.GetProperty("AssigneeSubjectId").GetString() ?? "";
                    var aliasElement = candidate.GetProperty("workerAliasId");
                    Guid? alias = aliasElement.ValueKind == JsonValueKind.Null ? null : aliasElement.GetGuid();
                    var assignment = actual.Assignments.SingleOrDefault(x => x.Id == id);
                    Need(assignment is not null && assignment.CaseId == approval.Id
                        && assignment.StageId == actual.Id && assignment.TenantId == source.TenantId
                        && assignment.LegalEntityId == source.LegalEntityId
                        && assignment.AssigneeSubjectId == principal
                        && assignment.PermissionKey == ContractReviewCoreWorkflow.ReviewAction
                        && assignment.DelegatedFromSubjectId is null
                        && rule.PrincipalIds.Contains(principal, StringComparer.Ordinal)
                        && ProcurementPrincipalEvidence.OptionalWorker(alias),
                        "ContractReviewAssignmentPinsChanged");
                    pins.Add(new(id, principal, alias));
                }
            }
            Need(pins.Select(x => x.AssignmentId).Distinct().Count() == pins.Count
                && pins.Select(x => x.PrincipalId).Distinct(StringComparer.Ordinal).Count() == pins.Count,
                "ContractReviewAssignmentPinsChanged");
            foreach (var pin in pins)
                Need(ProcurementPrincipalEvidence.Independent(source.MakerPrincipalId, source.MakerWorkerAliasId,
                    pin.PrincipalId, pin.WorkerAliasId), "ContractReviewAssignmentPinsChanged");
            for (var i = 0; i < pins.Count; i++)
                for (var j = i + 1; j < pins.Count; j++)
                    Need(ProcurementPrincipalEvidence.Independent(pins[i].PrincipalId, pins[i].WorkerAliasId,
                        pins[j].PrincipalId, pins[j].WorkerAliasId), "ContractReviewAssignmentPinsChanged");
            return pins;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException)
        { throw new InvalidOperationException("ContractReviewAssignmentPinsChanged", error); }
    }

    private static void Need(bool value, string code)
    { if (!value) throw new InvalidOperationException(code); }
}
