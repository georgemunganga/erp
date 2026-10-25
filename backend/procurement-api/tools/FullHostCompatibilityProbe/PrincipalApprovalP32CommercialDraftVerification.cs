using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

internal static partial class PrincipalApprovalHttpVerification
{
    // This is the first bounded P32 positive phase. P30 owns the real approved
    // source and later publication. The P32 Draft and zero-document prepared
    // candidate must be saved while that source remains a Draft. Neither result
    // authorizes a supplier bid, award or purchase order.
    private static async Task VerifyP32CommercialDraftPreparationAsync(
        PrincipalApprovalFixture fixture, PrincipalApprovalHost host,
        PrincipalApprovalRuntimePrivileges.Connections roles,
        Dictionary<string, object?> result, Action<string> stage, PrincipalSend send,
        HttpClient client, Guid eventId, JsonElement source)
    {
        stage("P32ActualFiniteDraftReadiness");
        using (var registration = host.Services.CreateScope())
        {
            var options=registration.ServiceProvider.GetRequiredService<ProcurementRfqCommercialOptions>();
            var ready=registration.ServiceProvider.GetRequiredService<IProcurementRfqCommercialReadiness>();
            Need(options.Enabled && ready is ProcurementRfqCommercialStoreReadiness,
                "P32 needs its actual registered owner-schema provider.");
            await using var execution=fixture.Procurement(roles.Execution);
            await using var tx=await execution.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            Need(await ready.IsReadyAsync(execution,true,default),
                "P32 exact physical schema and finite Execution role are not ready.");
        }
        const string sourcing="/api/procurement/v1/sourcing";
        var eventPath=sourcing+"/"+eventId;
        Need(source.GetProperty("status").GetString()=="Draft",
            "P32 controls require the original RFQ to remain Draft.");
        var definition=JsonSerializer.Serialize(new {
            schemaVersion=1,transactionType="SourcingEvaluationDefinition",
            normalizationTotal="100.0000",criteria=new[]{new {
                code="PRIVATE_TECHNICAL",phase="Technical",kind="Scored",mandatory=true,
                maximumScore="100.0000",weight="100.0000",
                evaluatorPrincipalIds=new[]{fixture.ConfigReviewer.ToString("D")}}}});
        Need(PrincipalRfqEvaluationPlanDefinition.Parse(definition).Criteria.Count==1,
            "P32 private named evaluator controls are invalid.");
        var opensAt=DateTimeOffset.UtcNow.AddMinutes(10);
        var closesAt=opensAt.AddMinutes(10);
        var draftBody=new {expectedRevision=(long?)null,draft=new {
            evaluationDefinitionJson=definition,opensAt,closesAt,
            comparisonCurrency="ZMW",currencyMode="SameCurrencyOnly",
            taxMode="OriginalSeparateComponents",requirements=Array.Empty<object>()}};
        stage("P32CommercialDraftCreateRealHttp");
        var draftKey=Guid.NewGuid();
        var saved=await send(client,HttpMethod.Put,eventPath+"/commercial-draft",
            fixture.Buyer,draftBody,HttpStatusCode.Created,draftKey);
        Need(saved.GetProperty("status").GetString()=="Draft"
            && saved.GetProperty("revision").GetInt64()==1
            && !saved.GetProperty("canPublish").GetBoolean(),
            "P32 original commercial Draft must remain a nonpublication configuration.");
        var replay=await send(client,HttpMethod.Put,eventPath+"/commercial-draft",
            fixture.Buyer,draftBody,HttpStatusCode.Created,draftKey);
        Need(replay.GetRawText()==saved.GetRawText(),
            "P32 Draft same-key replay changed its durable original response.");
        var prepareBody=new {
            expectedEventVersion=source.GetProperty("version").GetInt64(),
            expectedDraftRevision=saved.GetProperty("revision").GetInt64()};
        stage("P32ZeroDocumentPrepareRealHttp");
        var prepareKey=Guid.NewGuid();
        var prepared=await send(client,HttpMethod.Post,eventPath+"/commercial-prepare",
            fixture.Buyer,prepareBody,HttpStatusCode.OK,prepareKey);
        Need(prepared.GetProperty("status").GetString()=="PreparedCandidate"
            && prepared.GetProperty("draftRevision").GetInt64()==2
            && !prepared.GetProperty("canPublish").GetBoolean(),
            "P32 zero-document preparation must be a retained nonpublication candidate.");
        var prepareReplay=await send(client,HttpMethod.Post,eventPath+"/commercial-prepare",
            fixture.Buyer,prepareBody,HttpStatusCode.OK,prepareKey);
        Need(prepareReplay.GetRawText()==prepared.GetRawText(),
            "P32 preparation same-key replay changed its durable response.");
        await using (var verify=fixture.Procurement(roles.Execution))
        {
            var head=await verify.PrincipalRfqCommercialDrafts.AsNoTracking()
                .SingleAsync(x=>x.SourcingEventId==eventId);
            var plan=await verify.PrincipalRfqPreparedPlans.AsNoTracking()
                .SingleAsync(x=>x.Id==prepared.GetProperty("preparedPlanId").GetGuid());
            Need(head.Revision==2 && head.Id==saved.GetProperty("draftId").GetGuid()
                && plan.DocumentCount==0 && plan.SourcingEventId==eventId
                && plan.DraftRevision==2,
                "P32 independent owner reload did not retain the exact empty-document candidate.");
        }
        result["p32CommercialDraftPreparation"]=new {
            status="PREPARED_ZERO_DOCUMENT_CANDIDATE_ONLY",eventId,
            draftId=saved.GetProperty("draftId").GetGuid(),
            preparedPlanId=prepared.GetProperty("preparedPlanId").GetGuid(),
            draftKey,prepareKey,documentCount=0,
            internallyPublished=false,supplierBidSubmitted=false,awardCreated=false,
            purchaseOrderCreated=false,financePostingCreated=false};
    }
}
