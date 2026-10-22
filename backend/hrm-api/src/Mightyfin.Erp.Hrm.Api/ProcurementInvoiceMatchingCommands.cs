using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceMatchingRoutes
{
    private static Task<IResult> Compare(Guid id,CompareInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http); var fingerprint=Fingerprint(new { schemaVersion=1,id,input });
            if(input.Reason?.Length>2000) throw new MatchingProblem("InvoiceComparisonReasonTooLong",422);
            var invoice=await Invoice(db,scope,id,token);
            var worker=await Actor(core,scope,http,auth,"procurement-invoice-match",invoice.BranchRef,token);
            var mapping=await db.InvoiceMatchMappingVersions.SingleOrDefaultAsync(x=>x.Id==input.MappingVersionId && x.SupplierInvoiceId==id,token)
                ?? throw new MatchingProblem("InvoiceMappingNotFound",404);
            _=await Target(db,core,scope,http,auth,"procurement-invoice-match",invoice,mapping.PurchaseOrderId,token);
            if(await Replay(db,scope,"InvoiceCompare",key,fingerprint,id,mapping.PurchaseOrderId,token) is { } replay) return replay;
            var originalLines=await db.SupplierInvoiceLines.Where(x=>x.SupplierInvoiceId==id && x.IsActive).OrderBy(x=>x.LineNumber).ToArrayAsync(token);
            var snapshot=InvoiceMappingEvidence.Verify(mapping,invoice,originalLines);
            var source=await ProcurementReceivingRoutes.InvoiceEvidence(db,cases,scope,mapping.PurchaseOrderId,mapping.IssuedVersionId,snapshot.Lines,token);
            var exposure=await db.InvoiceMatchExposureStates.SingleOrDefaultAsync(x=>x.PurchaseOrderId==mapping.PurchaseOrderId && x.IssuedVersionId==mapping.IssuedVersionId,token);
            if(exposure is null)
            {
                exposure=new() { PurchaseOrderId=mapping.PurchaseOrderId,IssuedVersionId=mapping.IssuedVersionId,
                    SupplierId=invoice.SupplierId,CurrencyCode=invoice.CurrencyCode };
                db.AddScoped(exposure);
            }
            else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.invoice_match_exposure_states WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={exposure.Id} FOR UPDATE",token);
            invoice=await Invoice(db,scope,id,token,true);
            if(invoice.Version!=input.Version || invoice.CurrentMappingVersionId!=mapping.Id || invoice.MappingRevision!=mapping.MappingRevision)
                throw new MatchingProblem("InvoiceComparisonVersionChanged",409);
            if(invoice.ActiveMatchClaimResultId is not null) throw new MatchingProblem("InvoiceExistingClaimRequiresOwnerResolution",409);
            var now=Now();
            var policies=await db.PolicyVersions.Where(x=>x.PolicyKey==InvoiceMatchingPolicyRules.PolicyKey && x.State=="Published"
                && x.EffectiveFrom<=now && (x.EffectiveTo==null || now<x.EffectiveTo)).Take(2).ToArrayAsync(token);
            if(policies.Length!=1) throw new MatchingProblem("InvoicePublishedMatchingPolicyUnavailable",409);
            var policy=policies[0];
            try { await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db,scope,http.RequestServices,policy,token); }
            catch (OperationalPolicyPublicationProblem error) { throw new MatchingProblem(error.Code,error.Status); }
            var policyProjection=InvoiceMatchingPolicyRules.Project(policy);
            var policySha=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson);
            var hashes=await DocumentHashes(db,scope,http,id,snapshot,token);
            var reviewContext=new InvoiceMappingReviewContext(scope.TenantId,scope.LegalEntityId,invoice.BranchRef,
                scope.SubjectId,worker,policy.Id,policySha,now,hashes);
            var review=await ProcurementInvoiceUnitReviewRoutes.RetainedReview(db,cases,scope,http,invoice,originalLines,mapping,reviewContext,token)
                ?? await http.RequestServices.GetRequiredService<IInvoiceMappingEvidenceReviewer>().ReviewAsync(mapping,
                    InvoiceMatchDocument.From(invoice,originalLines),reviewContext,token);
            var projected=InvoiceMappingEvidence.Project(mapping,invoice,originalLines,review,reviewContext);
            var ownerContext=new InvoiceExposureReadContext(scope.TenantId,scope.LegalEntityId,invoice.BranchRef,
                source.Order.Id,source.Order.RevisionId,invoice.SupplierId,invoice.CurrencyCode,policySha,now);
            var owner=await http.RequestServices.GetRequiredService<IInvoiceHistoricalExposureOwner>().ReadAsync(ownerContext,token);
            var local=await (from row in db.InvoiceMatchAllocations
                join existing in db.SupplierInvoices on row.SupplierInvoiceId equals existing.Id
                where row.ExposureStateId==exposure.Id && existing.ActiveMatchClaimResultId==row.MatchResultId
                select new InvoiceActiveLocalClaim(row,existing)).ToArrayAsync(token);
            var ownerVerified=InvoiceMatchExecutionEvidence.OwnerCovers(owner,ownerContext);
            var history=ownerVerified ? InvoiceMatchExecutionEvidence.CompleteHistory(owner,ownerContext,exposure,local)
                : new InvoiceConsumptionEvidence("OwnerHistoryUnavailable",false,local.Select(x=>InvoiceMatchExecutionEvidence.Consumption(x.Allocation,x.Invoice.BranchRef)).ToArray());
            var duplicates=await db.SupplierInvoices.Where(x=>x.SupplierId==invoice.SupplierId && x.NormalizedInvoiceNumber==invoice.NormalizedInvoiceNumber && x.CurrencyCode==invoice.CurrencyCode)
                .Select(x=>new InvoiceDuplicateIdentity(x.Id,x.TenantId,x.LegalEntityId,x.SupplierId,x.NormalizedInvoiceNumber,x.CurrencyCode)).ToArrayAsync(token);
            var duplicateWatermark=Fingerprint(new { schemaVersion=1,identities=duplicates.OrderBy(x=>x.InvoiceId).ToArray() });
            var command=new InvoiceMatchCommand(key,input.Version,now,input.Reason?.Trim());
            var comparisonInput=new InvoiceMatchingInput(projected,source.Order,source.Acceptances,policyProjection,history,new(duplicateWatermark,true,duplicates));
            var comparison=InvoiceMatchingFoundation.Evaluate(command,scope,comparisonInput);
            // Even a provider bug may not turn an incomplete receiving projection into a qualified claim.
            if(source.Blockers.Count!=0 && comparison.ComparisonPassed) throw new MatchingProblem("InvoiceAcceptedHistoryUnavailable",409);
            var result=new InvoiceMatchResult { SupplierInvoiceId=id,MatchType=comparison.Lines.Count==0 ? "Unresolved" : string.Join("+",comparison.Lines.Select(x=>x.Method.ToString()).Distinct().Order()),
                Outcome=comparison.Outcome,PolicyVersionId=policy.Id,MatchedAt=now,ExecutionVersion=1,MappingVersionId=mapping.Id,
                PurchaseOrderId=mapping.PurchaseOrderId,IssuedVersionId=mapping.IssuedVersionId,InvoiceVersion=invoice.Version,
                InvoiceOriginalSha256=mapping.InvoiceOriginalSha256,ActorPrincipalId=scope.SubjectId,ActorWorkerId=worker,CommandKey=key,
                InputFingerprint=comparison.InputFingerprint,EvidenceJson=comparison.EvidenceJson,EvidenceSha256=comparison.EvidenceSha256,
                PolicyRulesSha256=policySha,SourceWatermarksJson=InvoiceMatchExecutionEvidence.SourceWatermarks(comparisonInput,mapping,review) };
            db.AddScoped(result);
            exposure.CoverageState=ownerVerified ? "Verified" : exposure.OwnerReference is null ? "Unknown" : "Stale";
            if(ownerVerified)
            {
                exposure.OwnerReference=owner.OwnerReference; exposure.OwnerWatermark=owner.Watermark;
                exposure.OwnerEvidenceSha256=owner.EvidenceSha256; exposure.OwnerValidUntil=owner.ValidUntil;
            }
            if(comparison.ComparisonPassed)
            {
                var claims=InvoiceMatchExecutionEvidence.Claims(comparison,comparisonInput,result,exposure,owner,source.AcceptanceSourceStates,
                    now,invoice,originalLines,mapping,review,reviewContext,command,scope,local,policy);
                foreach(var claim in claims) db.AddScoped(claim);
                exposure.Sequence=checked(exposure.Sequence+1); invoice.ActiveMatchClaimResultId=result.Id;
            }
            foreach(var issue in comparison.Discrepancies)
                db.AddScoped(new InvoiceException { SupplierInvoiceId=id,MatchResultId=result.Id,SupplierInvoiceLineId=issue.InvoiceLineId,
                    Type=issue.Code,Status="Open",OwnerSubjectId=scope.SubjectId,OwnerWorkerId=worker,
                    DiscrepancyKey=Fingerprint(new { invoiceId=id,issue.InvoiceLineId,issue.Code }),Reason="Review the original invoice and authoritative source evidence." });
            invoice.LastMatchResultId=result.Id; invoice.MatchRevision=checked(invoice.MatchRevision+1);
            invoice.MatchingState=comparison.ComparisonPassed ? "ComparisonPassed" : "ComparisonBlocked";
            db.AddScoped(new OutboxEvent { AggregateType="SupplierInvoice",AggregateId=id,EventType="supplier_invoice.compared.v1",
                IdempotencyKey=key.ToString("D"),PayloadJson=JsonSerializer.Serialize(new { invoiceId=id,resultId=result.Id,outcome=result.Outcome,canPostToFinance=false },Json) });
            return await Save(db,scope,id,mapping.PurchaseOrderId,"InvoiceCompare","procurement-invoice-match",key,fingerprint,
                ()=>new { result.Id,result.Outcome,invoice.Version,invoice.MatchingState,hasCapacityClaim=invoice.ActiveMatchClaimResultId==result.Id,
                    canPostToFinance=false,discrepancies=comparison.Discrepancies,sourceBlockers=source.Blockers },token);
        },ct);

    private static async Task<IReadOnlyDictionary<Guid,string>> DocumentHashes(ProcurementDbContext db,ProcurementRequestScope scope,
        HttpContext http,Guid invoiceId,InvoiceMappingSnapshot mapping,CancellationToken ct)
    {
        if(!http.RequestServices.GetRequiredService<CoreDocumentFeature>().Enabled
            || !(await http.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>()
                .AuthorizeAsync(http.User,null,"procurement-invoice-read")).Succeeded) return new Dictionary<Guid,string>();
        var configuration=http.RequestServices.GetRequiredService<IConfiguration>();
        if(!InvoiceDocumentStoreBinding.SamePhysicalDatabase(configuration.GetConnectionString("CoreDocuments"),db.Database.GetDbConnection().ConnectionString))
            return new Dictionary<Guid,string>(); // A separate owner store needs its own authenticated reader, never guessed local tables.
        var ids=mapping.Lines.Select(x=>x.DocumentVersionId).Where(x=>x!=Guid.Empty).Distinct().ToArray();
        if(ids.Length==0) return new Dictionary<Guid,string>();
        await using var documents=new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(db.Database.GetDbConnection()).Options,scope);
        await documents.Database.UseTransactionAsync(db.Database.CurrentTransaction!.GetDbTransaction(),ct);
        return await (from version in documents.Versions
            join document in documents.Documents on version.DocumentId equals document.Id
            where ids.Contains(version.Id) && !document.IsRetired && document.OwnerModule=="procurement"
                && document.OwnerType=="supplier_invoice" && document.OwnerId==invoiceId && version.ScanState==DocumentScanState.Available
            select new { version.Id,version.Sha256 }).ToDictionaryAsync(x=>x.Id,x=>x.Sha256,ct);
    }
}
