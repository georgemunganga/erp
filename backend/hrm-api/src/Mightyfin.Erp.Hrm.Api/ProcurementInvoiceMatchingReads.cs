using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceMatchingRoutes
{
    private static Task<IResult> Read(Guid id,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var invoice=await Invoice(db,scope,id,token);
            _=await Actor(core,scope,http,auth,"procurement-invoice-read",invoice.BranchRef,token);
            var mappings=await db.InvoiceMatchMappingVersions.Where(x=>x.SupplierInvoiceId==id).OrderBy(x=>x.MappingRevision).ToArrayAsync(token);
            var results=await db.InvoiceMatchResults.Where(x=>x.SupplierInvoiceId==id && x.ExecutionVersion==1).OrderBy(x=>x.MatchedAt).ThenBy(x=>x.Id).ToArrayAsync(token);
            var exceptions=await db.InvoiceExceptions.Where(x=>x.SupplierInvoiceId==id).OrderBy(x=>x.CreatedAt).ToArrayAsync(token);
            var originalLines=await db.SupplierInvoiceLines.Where(x=>x.SupplierInvoiceId==id).OrderBy(x=>x.LineNumber).ToArrayAsync(token);
            var currentOriginalSha=InvoiceMappingEvidence.OriginalDigest(invoice,originalLines);
            return JsonSerializer.Serialize(new { invoiceId=id,invoice.Version,invoice.Status,invoice.MatchingState,invoice.MappingRevision,invoice.MatchRevision,
                invoice.CurrentMappingVersionId,invoice.LastMatchResultId,invoice.ActiveMatchClaimResultId,
                canPostToFinance=false,isInvoiceApproval=false,matchingStateIsRecordedResult=true,
                original=new { netAmount=Exact(invoice.NetAmount),taxAmount=Exact(invoice.TaxAmount),grossAmount=Exact(invoice.GrossAmount),
                    invoice.CurrencyCode,lines=originalLines.Select(x=>new { x.Id,x.LineNumber,x.Description,quantity=Exact(x.Quantity),unitPrice=Exact(x.UnitPrice),taxAmount=Exact(x.TaxAmount) }) },
                mappings=mappings.Select(x=>new { x.Id,x.MappingRevision,x.PurchaseOrderId,x.IssuedVersionId,x.VerificationState,
                    originalDigestCurrent=x.InvoiceOriginalSha256==currentOriginalSha,x.ActorPrincipalId,x.ActorWorkerId,x.ReviewedAt,
                    snapshot=JsonDocument.Parse(x.MappingJson).RootElement }),
                comparisons=results.Select(x=>new { x.Id,x.Outcome,x.MatchedAt,x.InvoiceVersion,x.MappingVersionId,x.PurchaseOrderId,x.IssuedVersionId,
                    x.PolicyVersionId,x.PolicyRulesSha256,hasHeldCapacityClaim=invoice.ActiveMatchClaimResultId==x.Id,
                    comparison=InvoiceMatchReadProjection.Summary(x,originalLines.Select(line=>line.Id).ToHashSet()),
                    recordedHistoricalResult=true,isCurrentApproval=false,canPostToFinance=false }),
                exceptions=exceptions.Select(x=>new { x.Id,x.MatchResultId,x.SupplierInvoiceLineId,x.Type,x.Status,x.OwnerSubjectId,x.OwnerWorkerId,x.DueAt,x.Resolution }),
                capabilities=new { requiresVerifiedOriginalUnits=true,requiresCompleteAuthenticatedOwnerHistory=true,
                    negativeClaimEffectsAvailable=false,invoiceApprovalAvailable=false,apHandoffAvailable=false } },Json);
        },ct);

    private static Task<IResult> Recover(Guid id,Guid key,string? operation,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var permission=operation switch { "InvoiceMapping"=>"procurement-invoice-capture","InvoiceCompare"=>"procurement-invoice-match",_=>throw new MatchingProblem("InvoiceMatchingOperationRequired",400) };
            var invoice=await Invoice(db,scope,id,token);
            _=await Actor(core,scope,http,auth,permission,invoice.BranchRef,token);
            var receipt=await db.CommercialCommandReceipts.SingleOrDefaultAsync(x=>x.Operation==operation && x.CommandKey==key && x.ActorPrincipalId==scope.SubjectId,token)
                ?? throw new MatchingProblem("InvoiceMatchingRecordedCommandNotFound",404);
            _=await Target(db,core,scope,http,auth,permission,invoice,receipt.PurchaseOrderId,token);
            using var saved=JsonDocument.Parse(receipt.ResponseJson);
            if(saved.RootElement.GetProperty("invoiceId").GetGuid()!=id || saved.RootElement.GetProperty("commandPermission").GetString()!=permission)
                throw new MatchingProblem("InvoiceMatchingRecordedCommandNotFound",404);
            return JsonSerializer.Serialize(new { operation,commandKey=key,recordedHistoricalResult=true,isCurrentReplayAuthorization=false,
                currentInvoiceVersion=invoice.Version,canPostToFinance=false,recordedResult=saved.RootElement },Json);
        },ct);
    private static string Exact(decimal value)=>ProcurementEvidenceEncoding.Decimal(value);
}
