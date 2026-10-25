using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Replays retained named Core votes through the shared engine. A
/// status field alone never proves an award or permits Draft correction.</summary>
public static class PrincipalRfqAwardCoreHistory
{
    public static PrincipalRfqAwardReviewOutcome VerifyTerminal(
        PrincipalRfqAwardReviewSubmission source,ApprovalCase actual,
        PolicyVersion policy,IReadOnlyCollection<ApprovalDecision> historicalDecisions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(historicalDecisions);
        Need(source.CoreCaseId==actual.Id && actual.Id!=Guid.Empty
            && actual.TenantId==source.TenantId
            && actual.LegalEntityId==source.LegalEntityId
            && actual.SourceModule=="procurement"
            && actual.SourceType==PrincipalRfqAwardReviewWorkflow.SourceType
            && actual.SourceId==source.Id
            && actual.SourceVersion==source.RecommendationRevision
            && actual.SourceVersion>0
            && actual.PolicyVersionId==source.PolicyVersionId
            && actual.PolicyVersionNumber==source.PolicyVersionNumber
            && actual.MakerSubjectId==source.MakerPrincipalId
            && actual.OpenedAt==source.SubmittedAt
            && policy.Id==source.PolicyVersionId
            && policy.TenantId==source.TenantId
            && policy.LegalEntityId==source.LegalEntityId
            && policy.PolicyKey==PrincipalRfqAwardFoundation.PolicyKey
            && policy.PolicyVersionNumber==source.PolicyVersionNumber
            && source.PolicyRulesSha256==ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && source.AssignmentPinsSha256==ProcurementEvidenceEncoding.Digest(source.AssignmentPinsJson)
            && (actual.State is "Returned" or "Rejected" or "Approved")
            && actual.CompletedAt is not null
            && historicalDecisions.Count is >0 and <=128,
            "RfqAwardCoreTerminalSourceChanged");
        var stages=actual.Stages.OrderBy(x=>x.Sequence).ToArray();
        Need(stages.Length is >0 and <=16
            && stages.Select(x=>x.Sequence).SequenceEqual(Enumerable.Range(1,stages.Length))
            && stages.SelectMany(x=>x.Assignments).Count()<=128,
            "RfqAwardCoreStageHistoryChanged");
        VerifyPins(source,actual,policy,stages);
        var copy=new ApprovalCase
        {
            Id=actual.Id,TenantId=actual.TenantId,
            LegalEntityId=actual.LegalEntityId,SourceModule=actual.SourceModule,
            SourceType=actual.SourceType,SourceId=actual.SourceId,
            SourceVersion=actual.SourceVersion,PolicyVersionId=actual.PolicyVersionId,
            PolicyVersionNumber=actual.PolicyVersionNumber,
            MakerSubjectId=actual.MakerSubjectId,OpenedAt=actual.OpenedAt,
            CreatedAt=actual.CreatedAt,UpdatedAt=actual.OpenedAt,
        };
        foreach(var original in stages)
        {
            var stage=new ApprovalStage
            {
                Id=original.Id,TenantId=original.TenantId,
                LegalEntityId=original.LegalEntityId,CaseId=original.CaseId,
                Sequence=original.Sequence,Code=original.Code,Mode=original.Mode,
                RequiredApprovals=original.RequiredApprovals,
                State=original.Sequence==1?"Active":"Waiting",
                CreatedAt=original.CreatedAt,UpdatedAt=original.CreatedAt,
            };
            foreach(var assignment in original.Assignments)
                stage.Assignments.Add(new ApprovalAssignment
                {
                    Id=assignment.Id,TenantId=assignment.TenantId,
                    LegalEntityId=assignment.LegalEntityId,
                    CaseId=assignment.CaseId,StageId=assignment.StageId,
                    AssigneeSubjectId=assignment.AssigneeSubjectId,
                    PermissionKey=assignment.PermissionKey,
                    State="Pending",CreatedAt=assignment.CreatedAt,
                    UpdatedAt=assignment.CreatedAt,
                });
            copy.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(copy);
        Need(copy.Stages.SelectMany(x=>x.Assignments).All(x=>
            x.PermissionKey==PrincipalRfqAwardFoundation.ReviewAction),
            "RfqAwardCoreAssignmentActionChanged");
        var seenIds=new HashSet<Guid>();
        var seenKeys=new HashSet<string>(StringComparer.Ordinal);
        var replayed=new List<ApprovalDecision>();
        var ordered=historicalDecisions.OrderBy(x=>x.DecidedAt).ThenBy(x=>x.Id).ToArray();
        var last=actual.OpenedAt;
        foreach(var historical in ordered)
        {
            Need(historical.Id!=Guid.Empty && seenIds.Add(historical.Id)
                && historical.TenantId==source.TenantId
                && historical.LegalEntityId==source.LegalEntityId
                && historical.CaseId==actual.Id
                && historical.DecidedAt>=last
                && ProcurementEvidenceEncoding.DatabaseTimestamp(historical.DecidedAt)==historical.DecidedAt
                && Guid.TryParseExact(historical.IdempotencyKey,"D",out var commandId)
                && commandId!=Guid.Empty
                && seenKeys.Add(historical.IdempotencyKey),
                "RfqAwardCoreVoteHistoryChanged");
            var stage=copy.Stages.SingleOrDefault(x=>x.Id==historical.StageId);
            var assignment=stage?.Assignments.SingleOrDefault(x=>x.Id==historical.AssignmentId);
            Need(assignment is not null
                && assignment.AssigneeSubjectId==historical.ActorSubjectId
                && historical.ActorSubjectId!=source.MakerPrincipalId,
                "RfqAwardCoreVoteActorChanged");
            var actor=new ApprovalActor(historical.ActorSubjectId,source.TenantId,
                source.LegalEntityId,new HashSet<string>{PrincipalRfqAwardFoundation.ReviewAction});
            var action=ApprovalEngine.Decide(copy,assignment,actor,historical.Action,
                historical.IdempotencyKey,historical.Reason,replayed,historical.DecidedAt);
            Need(action.Recorded && replayed.Count>0
                && replayed[^1].ActorSubjectId==historical.ActorSubjectId
                && replayed[^1].StageId==historical.StageId
                && replayed[^1].AssignmentId==historical.AssignmentId
                && replayed[^1].Action==historical.Action
                && replayed[^1].Reason==historical.Reason,
                "RfqAwardCoreVoteReplayChanged");
            last=historical.DecidedAt;
        }
        Need(copy.State==actual.State && copy.CompletedAt==actual.CompletedAt
            && copy.CompletionKey==actual.CompletionKey
            && stages.All(original=>copy.Stages.Single(x=>x.Id==original.Id).State==original.State
                && original.Assignments.All(assignment=>copy.Stages.Single(x=>x.Id==original.Id)
                    .Assignments.Single(x=>x.Id==assignment.Id).State==assignment.State)),
            "RfqAwardCoreTerminalReplayChanged");
        var terminal=ordered[^1];
        Need(actual.State switch
        {
            "Returned"=>terminal.Action=="Return",
            "Rejected"=>terminal.Action=="Reject",
            "Approved"=>terminal.Action=="Approve",
            _=>false,
        },"RfqAwardCoreTerminalDecisionChanged");
        return new PrincipalRfqAwardReviewOutcome
        {
            TenantId=source.TenantId,LegalEntityId=source.LegalEntityId,
            SubmissionId=source.Id,CoreCaseId=actual.Id,
            TerminalDecisionId=terminal.Id,CoreState=actual.State,
            SourceOutcome=actual.State=="Approved"?"CoreApprovedPendingAward":actual.State,
            RecordedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(actual.CompletedAt)!.Value,
            CreatedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(actual.CompletedAt)!.Value,
            CreatedBy=terminal.ActorSubjectId,
        };
    }

    private static void VerifyPins(PrincipalRfqAwardReviewSubmission source,
        ApprovalCase actual,PolicyVersion policy,ApprovalStage[] stages)
    {
        try
        {
            var rules=PrincipalRfqAwardFoundation.ParseRules(policy.RulesJson);
            Need(rules.ReviewBranchId==source.BranchId
                && rules.CurrencyCode==source.CurrencyCode
                && rules.Stages.Count==stages.Length,
                "RfqAwardAssignmentPinsChanged");
            using var document=JsonDocument.Parse(source.AssignmentPinsJson);
            var root=document.RootElement;
            Need(root.ValueKind==JsonValueKind.Object
                && root.EnumerateObject().Count()==4
                && root.GetProperty("schemaVersion").GetInt32()==1
                && root.GetProperty("caseId").GetGuid()==actual.Id
                && root.GetProperty("submissionId").GetGuid()==source.Id,
                "RfqAwardAssignmentPinsChanged");
            var pinnedStages=root.GetProperty("stages").EnumerateArray().ToArray();
            Need(pinnedStages.Length==stages.Length,"RfqAwardAssignmentPinsChanged");
            var aliases=new List<(string PrincipalId,Guid? WorkerAliasId)>();
            for(var i=0;i<stages.Length;i++)
            {
                var row=pinnedStages[i];var stage=stages[i];var rule=rules.Stages[i];
                Need(row.EnumerateObject().Count()==6
                    && row.GetProperty("Id").GetGuid()==stage.Id
                    && row.GetProperty("Sequence").GetInt32()==stage.Sequence
                    && row.GetProperty("Code").GetString()==stage.Code
                    && stage.Code==rule.Code
                    && row.GetProperty("Mode").GetString()==stage.Mode
                    && stage.Mode==(rule.Mode=="One"?"Any":"All")
                    && row.GetProperty("RequiredApprovals").GetInt32()==stage.RequiredApprovals
                    && stage.RequiredApprovals==(rule.Mode=="One"?1:rule.PrincipalIds.Count),
                    "RfqAwardAssignmentPinsChanged");
                var pinned=row.GetProperty("assignments").EnumerateArray().ToArray();
                Need(pinned.Length==stage.Assignments.Count
                    && pinned.Length==rule.PrincipalIds.Count,
                    "RfqAwardAssignmentPinsChanged");
                foreach(var assignmentRow in pinned)
                {
                    Need(assignmentRow.EnumerateObject().Count()==3,
                        "RfqAwardAssignmentPinsChanged");
                    var id=assignmentRow.GetProperty("Id").GetGuid();
                    var principal=assignmentRow.GetProperty("AssigneeSubjectId").GetString()??"";
                    var aliasValue=assignmentRow.GetProperty("workerAliasId");
                    Guid? alias=aliasValue.ValueKind==JsonValueKind.Null?null:aliasValue.GetGuid();
                    var assignment=stage.Assignments.SingleOrDefault(x=>x.Id==id);
                    Need(assignment is not null && assignment.CaseId==actual.Id
                        && assignment.StageId==stage.Id
                        && assignment.TenantId==source.TenantId
                        && assignment.LegalEntityId==source.LegalEntityId
                        && assignment.AssigneeSubjectId==principal
                        && assignment.PermissionKey==PrincipalRfqAwardFoundation.ReviewAction
                        && rule.PrincipalIds.Contains(principal,StringComparer.Ordinal)
                        && ProcurementPrincipalEvidence.OptionalWorker(alias)
                        && ProcurementPrincipalEvidence.Independent(source.MakerPrincipalId,
                            source.MakerWorkerAliasId,principal,alias),
                        "RfqAwardAssignmentPinsChanged");
                    aliases.Add((principal,alias));
                }
            }
            Need(aliases.Select(x=>x.PrincipalId).Distinct(StringComparer.Ordinal).Count()==aliases.Count,
                "RfqAwardAssignmentPinsChanged");
            for(var i=0;i<aliases.Count;i++)
                for(var j=i+1;j<aliases.Count;j++)
                    Need(ProcurementPrincipalEvidence.Independent(aliases[i].PrincipalId,
                        aliases[i].WorkerAliasId,aliases[j].PrincipalId,aliases[j].WorkerAliasId),
                        "RfqAwardAssignmentPinsChanged");
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException
            or FormatException or OverflowException)
        {throw new InvalidOperationException("RfqAwardAssignmentPinsChanged",error);}
    }

    private static void Need(bool valid,string code)
    {if(!valid)throw new InvalidOperationException(code);}
}
