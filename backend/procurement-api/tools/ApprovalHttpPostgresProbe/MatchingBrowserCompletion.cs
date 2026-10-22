using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    public async Task VerifyMatchingBrowserCompletion()
    {
        var source=matchingBrowserSource??throw new InvalidOperationException("No matching browser fixture source.");
        Require(matchingDocumentDirectory is not null && matchingBrowserSourceBefore is not null && matchingBrowserActors.Count==4,"Matching browser owner verifier lacks retained bootstrap identity/evidence.");
        var mapper=matchingBrowserActors["mapper"]; var matcher=matchingBrowserActors["matcher"];
        using(var services=Scope(OrderBuyer))
        {
            // Fresh owner contexts inspect persisted records, never browser-provided completion flags.
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var docs=services.ServiceProvider.GetRequiredService<DocumentDbContext>();
            var invoice=await db.SupplierInvoices.AsNoTracking().SingleAsync(x=>x.InvoiceNumber==MatchingBrowserInvoiceNumber && x.SupplierId==source.Supplier);
            var original=await db.SupplierInvoiceLines.AsNoTracking().Where(x=>x.SupplierInvoiceId==invoice.Id).ToArrayAsync();
            Require(await db.SupplierInvoices.CountAsync()==1 && original.Length==1 && invoice.Status=="Received" && invoice.IntakeChannel=="Manual"
                && invoice.Version==3 && invoice.BranchRef==OrderBranch && invoice.CurrencyCode=="ZMW" && invoice.NetAmount==100 && invoice.TaxAmount==0 && invoice.GrossAmount==100
                && invoice.FinancePostingRef is null && invoice.ActiveMatchClaimResultId is null && invoice.MatchingState=="ComparisonBlocked"
                && original[0].Description=="P26 original goods invoice" && original[0].Quantity==1 && original[0].UnitPrice==100 && original[0].TaxAmount==0,
                "Actual browser did not preserve one original Received invoice without Finance authority.");
            var document=await docs.Documents.AsNoTracking().SingleAsync(x=>x.OwnerModule=="procurement" && x.OwnerType=="supplier_invoice" && x.OwnerId==invoice.Id);
            var version=await docs.Versions.AsNoTracking().SingleAsync(x=>x.DocumentId==document.Id);
            var scan=await docs.ScanEvidence.AsNoTracking().SingleAsync(x=>x.VersionId==version.Id);
            Require(await docs.Documents.CountAsync()==1 && await docs.Versions.CountAsync()==1 && !document.IsRetired
                && document.CreatedBy==mapper.Principal.ToString("D") && version.CreatedBy==mapper.Principal.ToString("D")
                && version.ScanState==DocumentScanState.Available && version.Number==1 && version.Version==3 && version.FileName==MatchingDocumentName
                && version.MediaType=="application/pdf" && version.SizeBytes==MatchingDocumentBytes.Length && version.Sha256==MatchingDocumentSha
                && scan.Verdict==ScannerVerdict.Clean && scan.Sha256==MatchingDocumentSha && scan.Engine=="TEST-ONLY KnownFixtureOnlyDocumentScanner"
                && scan.Definitions=="synthetic-exact-benign-bytes-only" && scan.Attempt==1,
                "Core document original/scan owner lifecycle was fabricated or mismatched.");
            var lifecycle=await docs.RecordEvents.AsNoTracking().Where(x=>x.DocumentId==document.Id).ToArrayAsync();
            Require(lifecycle.Length==4 && new[] { "UploadRegistered","ScanStarted","ScanFinished","DownloadAuthorized" }.All(action=>lifecycle.Any(x=>x.Action==action))
                && lifecycle.All(x=>x.ActorSubjectId==mapper.Principal.ToString("D") && x.VersionId==version.Id),"Core document lifecycle lacks its actual uploader or scoped download evidence.");
            await using(var bytes=await new PrivateDocumentStore(matchingDocumentDirectory!).OpenPrivateReadAsync(version.StorageKey))
                Require(Convert.ToHexStringLower(await SHA256.HashDataAsync(bytes))==MatchingDocumentSha,"Retained private PDF bytes changed.");
            var attachment=await db.AttachmentRefs.AsNoTracking().SingleAsync(x=>x.OwnerType=="SupplierInvoice" && x.OwnerId==invoice.Id);
            Require(attachment.DocumentServiceId==document.Id.ToString("D") && attachment.DocumentVersion==1 && attachment.Sha256==MatchingDocumentSha,"Procurement invented a duplicate/unowned original document.");
            var mapping=await db.InvoiceMatchMappingVersions.AsNoTracking().SingleAsync(x=>x.SupplierInvoiceId==invoice.Id);
            var snapshot=InvoiceMappingEvidence.Verify(mapping,invoice,original); var link=snapshot.Lines.Single();
            Require(await db.InvoiceMatchMappingVersions.CountAsync()==1 && mapping.Id==invoice.CurrentMappingVersionId && mapping.MappingRevision==invoice.MappingRevision
                && mapping.VerificationState=="Unverified" && mapping.ActorPrincipalId==mapper.Principal.ToString("D") && mapping.ActorWorkerId==mapper.Worker
                && mapping.PurchaseOrderId==source.Order && mapping.IssuedVersionId==source.Issued && link.InvoiceLineId==original[0].Id
                && link.PurchaseOrderLineId==source.Line && link.GoodsReceiptLineId==source.ReceiptLine && link.ServiceAcceptanceId is null
                && link.DocumentVersionId==version.Id && link.DocumentLineReference=="Page 1 line 1"
                && link.InvoiceUnitCode==(await db.PurchaseOrderLines.AsNoTracking().Where(x=>x.Id==source.Line).Select(x=>x.UnitCode).SingleAsync()),
                "Persisted mapping lost original/source/document identity or falsely verified units.");
            var result=await db.InvoiceMatchResults.AsNoTracking().SingleAsync(x=>x.SupplierInvoiceId==invoice.Id && x.ExecutionVersion==1);
            var policy=await db.PolicyVersions.AsNoTracking().SingleAsync(x=>x.Id==result.PolicyVersionId);
            var summary=InvoiceMatchReadProjection.Summary(result,original.Select(x=>x.Id).ToHashSet());
            Require(await db.InvoiceMatchResults.CountAsync()==1 && result.Outcome=="Blocked" && result.Id==invoice.LastMatchResultId && invoice.MatchRevision==1
                && result.InvoiceVersion==2 && result.MappingVersionId==mapping.Id && result.PurchaseOrderId==source.Order && result.IssuedVersionId==source.Issued
                && result.InvoiceOriginalSha256==InvoiceMappingEvidence.OriginalDigest(invoice,original)
                && result.PolicyRulesSha256==ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                && result.ActorPrincipalId==matcher.Principal.ToString("D") && result.ActorWorkerId==matcher.Worker
                && summary.Discrepancies.Any(x=>x.Code=="CompleteConsumptionEvidenceRequired"),"Actual comparison did not retain a safe missing-consumption Blocked result.");
            var errors=await db.InvoiceExceptions.AsNoTracking().Where(x=>x.SupplierInvoiceId==invoice.Id).ToArrayAsync();
            Require(errors.Length==summary.Discrepancies.Count && errors.All(x=>x.MatchResultId==result.Id && x.Status=="Open"
                && x.OwnerSubjectId==matcher.Principal.ToString("D") && x.OwnerWorkerId==matcher.Worker)
                && errors.Select(x=>(x.SupplierInvoiceLineId,x.Type)).OrderBy(x=>x.Type).ThenBy(x=>x.SupplierInvoiceLineId).SequenceEqual(summary.Discrepancies.Select(x=>(x.InvoiceLineId,x.Code)).OrderBy(x=>x.Code).ThenBy(x=>x.InvoiceLineId)),
                "Blocked comparison exceptions lack exact owned discrepancies and responsible identity.");
            var receipts=await db.CommercialCommandReceipts.AsNoTracking().Where(x=>x.PurchaseOrderId==source.Order && (x.Operation=="InvoiceMapping" || x.Operation=="InvoiceCompare")).ToArrayAsync();
            Require(receipts.Length==2 && receipts.Select(x=>x.CommandKey).Distinct().Count()==2,"Actual UI/recovery duplicated a command or lost its original key.");
            foreach(var receipt in receipts)
            {
                using var response=JsonDocument.Parse(receipt.ResponseJson); var body=response.RootElement;
                var mappingCommand=receipt.Operation=="InvoiceMapping";
                object intended=mappingCommand
                    ? new { version=1L,purchaseOrderId=source.Order,issuedVersionId=source.Issued,lines=snapshot.Lines }
                    : new { version=2L,mappingVersionId=mapping.Id,reason=(string?)null };
                var fingerprint=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new { schemaVersion=1,id=invoice.Id,input=intended },Json));
                Require(receipt.Fingerprint==fingerprint,"Actual receipt lost the exact original version/body fingerprint.");
                Require(receipt.ActorPrincipalId==(mappingCommand?mapper.Principal:matcher.Principal).ToString("D") && receipt.CommandKey!=Guid.Empty
                    && body.GetProperty("invoiceId").GetGuid()==invoice.Id && body.GetProperty("commandKey").GetGuid()==receipt.CommandKey
                    && body.GetProperty("recordedHistoricalResult").GetBoolean() && !body.GetProperty("isCurrentApproval").GetBoolean() && !body.GetProperty("canPostToFinance").GetBoolean()
                    && body.GetProperty("result").GetProperty("id").GetGuid()==(mappingCommand?mapping.Id:result.Id)
                    && body.GetProperty("result").GetProperty("version").GetInt64()==(mappingCommand?2L:3L)
                    && (mappingCommand || receipt.CommandKey==result.CommandKey),"Actual command receipt/actor changed during GET recovery.");
                Require(await db.AuditEvents.AnyAsync(x=>x.RecordId==receipt.Id && x.RecordType==nameof(ProcurementCommercialCommandReceipt)
                    && x.ActorSubjectId==receipt.ActorPrincipalId),"Actual saved command receipt lacks durable audit.");
            }
            foreach(var (type,id,actor) in new[] { (nameof(SupplierInvoice),invoice.Id,mapper.Principal),(nameof(SupplierInvoiceLine),original[0].Id,mapper.Principal),
                (nameof(InvoiceMatchMappingVersion),mapping.Id,mapper.Principal),(nameof(InvoiceMatchResult),result.Id,matcher.Principal) })
                Require(await db.AuditEvents.AnyAsync(x=>x.RecordType==type && x.RecordId==id && x.Action=="create" && x.ActorSubjectId==actor.ToString("D")),"Actual invoice evidence lacks independent durable creation audit.");
            foreach(var error in errors) Require(await db.AuditEvents.AnyAsync(x=>x.RecordType==nameof(InvoiceException) && x.RecordId==error.Id && x.ActorSubjectId==matcher.Principal.ToString("D")),"Invoice exception audit missing.");
            var events=await db.OutboxEvents.AsNoTracking().Where(x=>x.AggregateId==invoice.Id && x.EventType=="supplier_invoice.compared.v1").ToArrayAsync();
            Require(events.Length==1 && result.CommandKey is { } comparisonKey && events[0].IdempotencyKey==comparisonKey.ToString("D"),"Comparison outbox duplicated or lost command correlation.");
            var exposure=await db.InvoiceMatchExposureStates.AsNoTracking().SingleAsync(x=>x.PurchaseOrderId==source.Order && x.IssuedVersionId==source.Issued);
            var expectedAudits=new Dictionary<Guid,(string Type,int Count)>
            {
                [invoice.Id]=(nameof(SupplierInvoice),3),[original[0].Id]=(nameof(SupplierInvoiceLine),1),
                [attachment.Id]=(nameof(ProcurementAttachmentRef),1),[mapping.Id]=(nameof(InvoiceMatchMappingVersion),1),
                [result.Id]=(nameof(InvoiceMatchResult),1),[exposure.Id]=(nameof(InvoiceMatchExposureState),1),
            };
            foreach(var receipt in receipts) expectedAudits.Add(receipt.Id,(nameof(ProcurementCommercialCommandReceipt),1));
            foreach(var error in errors) expectedAudits.Add(error.Id,(nameof(InvoiceException),1));
            foreach(var (target,expected) in expectedAudits)
                Require(await db.AuditEvents.CountAsync(x=>x.RecordId==target && x.RecordType==expected.Type)==expected.Count,
                    "Unexpected extra write/audit on owned invoice evidence, including read/recovery paths.");
            Require(await db.InvoiceMatchExposureStates.CountAsync()==1 && !await db.InvoiceMatchAllocations.AnyAsync() && !await db.FinanceInvoiceHandoffs.AnyAsync() && !await db.FinancePaymentStatusRefs.AnyAsync()
                && exposure.Sequence==0 && exposure.CoverageState=="Unknown" && exposure.OwnerReference is null && exposure.OwnerWatermark is null
                && exposure.OwnerEvidenceSha256 is null && exposure.OwnerValidUntil is null,"Unavailable owners fabricated allocation/AP/payment authority.");
        }
        Require(matchingBrowserSourceBefore==await MatchingSourceFootprint(),"Invoice comparison/recovery changed source acceptance/effects/counters.");
        Console.WriteLine("PASS: actual Bills capture -> real owned document upload/scan -> Unverified mapping -> explicit Blocked comparison/recovery; no claims, AP, payment or source mutation. Test-only known-byte scanner is not malware validation.");
    }
}
