using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialPrepareInput(long ExpectedEventVersion,long ExpectedDraftRevision);
    // Unmapped/default-off. Prepare creates immutable candidate/retention evidence,
    // not a Core approval case, Published event, submitted offer or usable award.
    private static async Task<IResult> CommercialPrepare(Guid id,CommercialPrepareInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        http.Response.Headers.CacheControl="private, no-store";
        try
        {
            if(http.RequestServices.GetService<ProcurementRfqCommercialOptions>() is not{Enabled:true})throw new Problem("RfqCommercialUnavailable",503);
            await RequireCommercialDocumentCommandFoundation(http,null,ct);
            if(input is null||id==Guid.Empty||input.ExpectedEventVersion<1||input.ExpectedDraftRevision<1)throw new Problem("RfqCommercialPreparationInputRequired",422);
            var key=Key(http);var fingerprint=Fingerprint(new{operation="PrepareCommercialCandidate",eventId=id,input.ExpectedEventVersion,input.ExpectedDraftRevision});
            var result=await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteWithSourcingDocumentsAsync(id,
                async(db,cases,documents,fence,token)=>
                {
                    await RequireCommercialDocumentCommandFoundation(http,db,token);
                    await RequireCommercial(http,db,true,token);
                    await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                    var auth=http.RequestServices.GetRequiredService<IAuthorizationService>();
                    var actor=await PrincipalActor(core,scope,http,auth,PrincipalRfqCommercialDraft.Permission,null,false,token);
                    _=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,PrincipalRfqCommercialDraft.Permission,id,token);
                    await LockPrincipalSourcingCommandKey(db,key,token);
                    var store=new PrincipalRfqCommercialDraftStore(db,scope);
                    if(await store.RecoverPreparationAsync(id,key,fingerprint,token) is {} replay)return new Reply(replay.ResponseJson,200);
                    var source=await LoadCommercialSource(db,cases,core,scope,http,auth,id,true,token);
                    if(source.Event.Version!=input.ExpectedEventVersion)throw new Problem("RfqCommercialEventVersionChanged",409);
                    var current=await store.LoadAsync(id,token);
                    if(current.Head.Revision!=input.ExpectedDraftRevision)throw new Problem("RfqCommercialDraftVersionChanged",409);
                    var draft=PrincipalRfqCommercialDraft.Restore(current.Head,current.Revision);
                    if(draft.EvaluationDefinitionJson is null||draft.OpensAt is null||draft.ClosesAt is null
                        ||draft.ComparisonCurrency is null||draft.CurrencyMode is null||draft.TaxMode is null)
                        throw new Problem("RfqCommercialControlsIncomplete",409);
                    var definition=PrincipalRfqEvaluationPlanDefinition.Parse(draft.EvaluationDefinitionJson);
                    var evaluators=await ResolveCommercialEvaluators(source,definition,core,scope,token);
                    // Same logical command retains a successor Draft revision and its
                    // receipt. No detached document use or adopted historical revision.
                    var draftCommand=store.Revise(source.Actor,key,fingerprint,current.Head,current.Revision,input.ExpectedDraftRevision,
                        source.Event,source.Evidence,source.Bindings,draft);
                    var revision=db.PrincipalRfqCommercialDraftRevisions.Local.Single(x=>x.Id==draftCommand.DraftRevisionId);
                    var pins=await StageCommercialRequirementsForReview(db,core,documents,fence,scope,http,auth,current.Head,revision,token);
                    var at=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
                    var policy=await db.PolicyVersions.SingleOrDefaultAsync(x=>x.PolicyKey==SourcingPublicationPolicyRules.PolicyKey
                        &&x.State=="Published"&&x.EffectiveFrom<=at&&(x.EffectiveTo==null||at<x.EffectiveTo),token)
                        ??throw new Problem("RfqCommercialPublishedPolicyRequired",409);
                    var identity=await db.SourcingPolicyIdentityEvidenceRecords.Where(x=>x.PolicyVersionId==policy.Id).ToArrayAsync(token);
                    var published=identity.SingleOrDefault(x=>x.Stage=="Published")??throw new Problem("RfqCommercialPolicyIdentityRequired",409);
                    var classifications=new List<PolicyScenario>();
                    foreach(var pin in source.Original.Lines.OrderBy(x=>x.EventLineId))
                    {
                        var original=source.Sources.Single(x=>x.Line.Id==pin.LineId);
                        using var json=JsonDocument.Parse(original.Submission.RequestSnapshotJson);
                        var line=json.RootElement.GetProperty("legacyBusiness").GetProperty("lines").EnumerateArray().Single(x=>x.GetProperty("Id").GetGuid()==pin.LineId);
                        classifications.Add(new(source.Original.CurrencyCode,pin.EstimatedValue,line.GetProperty("CategoryCode").GetString(),
                            line.GetProperty("PurchaseType").GetString(),source.Event.BranchRef));
                    }
                    var controls=PrincipalRfqPublishedPlanPolicy.Select(policy,identity.Single(x=>x.Id==published.CreatedEvidenceId),
                        identity.Single(x=>x.Id==published.SubmittedEvidenceId),published,source.Original,classifications,at);
                    var reference=new DocumentUseReference(revision.Id,"procurement",PrincipalRfqPrepublicationPlan.DocumentSourceType,id,revision.Revision,DocumentUseKind.Review);
                    var candidate=PrincipalRfqPrepublicationPlan.Prepare(source.Event,source.Evidence,source.Bindings,definition,controls,evaluators,pins,
                        input.ExpectedEventVersion,revision.Revision,reference,draft.OpensAt.Value,draft.ClosesAt.Value,at,
                        draft.ComparisonCurrency,draft.CurrencyMode,draft.TaxMode);
                    var set=pins.Count==0?null:documents.UseSets.Local.Single(x=>x.UseKey==revision.Id);
                    var receipt=store.StagePreparation(candidate,controls,current.Head,revision,source.Actor,at,pins.Count,set?.Id,set?.SelectionDigest,draftCommand);
                    // Coordinator saves/commits all contexts. Missing controls or a
                    // later source/save failure rolls back the prior Core flush too.
                    return new Reply(receipt.ResponseJson,200);
                },ct);
            return Results.Content(result.Body,"application/json",statusCode:result.Status);
        }
        catch(Exception error) when(error is Problem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or DbUpdateException or Npgsql.NpgsqlException or ApprovedDemandLoadException
            ||ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status=error is Problem problem?problem.Status:ProcurementPostgresFailures.IsConcurrency(error)?409:
                ProcurementPostgresFailures.IsStoreFailure(error)?503:error is UnauthorizedAccessException?403:error is KeyNotFoundException?404:409;
            return Error(error is Problem?error.Message:status==503?"RfqCommercialPreparationUnconfirmed":"RfqCommercialPreparationNeedsReview",status);
        }
    }
}
