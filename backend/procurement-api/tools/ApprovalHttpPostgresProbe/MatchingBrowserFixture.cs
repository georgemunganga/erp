using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private readonly Dictionary<string,(Guid Principal,Guid Worker,Guid Branch)> matchingBrowserActors=[];
    private (Guid Order,Guid Line,Guid Issued,Guid ReceiptLine,Guid Supplier)? matchingBrowserSource;
    private string? matchingBrowserSourceBefore;
    private const string MatchingBrowserInvoiceNumber="P26-BROWSER-ORIGINAL-1";

    private async Task<string> PrepareMatchingBrowserManifest(int port)
    {
        Require(OrderDistinctRuntime && matchingDocumentDirectory is not null,"Matching browser fixture requires separate owner/runtime and owned document storage.");
        await Authority(OrderManager1,"MATCHING-BROWSER-PR-M1");
        await Authority(OrderManager2,"MATCHING-BROWSER-PR-M2");
        await Authority(OrderManager2,"MATCHING-BROWSER-PO-M2","PurchaseOrder.Approve");
        Guid supplierId;
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var supplier=new Supplier { Number="MATCHING-BROWSER",LegalName="P26 browser supplier",DisplayName="P26 browser supplier",CountryCode="ZM",Status="Active",IsOrderable=true };
            db.AddScoped(supplier); await db.SaveChangesAsync(); supplierId=supplier.Id;
        }
        // Actual upstream commands establish approved/issued/accepted authority; no source status patching.
        var source=await DeliveredReceivingOrder(supplierId,"Goods");
        await PublishReceivingPolicy();
        var draftResponse=await Send(HttpMethod.Post,Root+"/receipts/drafts",OrderRequester,new
        {
            purchaseOrderId=source.OrderId,issuedVersionId=source.IssuedId,siteId=OrderBranch,receivedAt=DateTimeOffset.UtcNow,
            deliveryNoteRef="MATCHING-BROWSER-DELIVERY",notes="Synthetic source evidence for blocked invoice browser",
            lines=new[] { new { purchaseOrderLineId=source.LineId,receivedQuantity="1",rejectedQuantity="0" } },
        },Guid.NewGuid());
        await Check(draftResponse,HttpStatusCode.OK); var draft=await draftResponse.Content.ReadFromJsonAsync<JsonElement>(); var receiptId=draft.GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.OK,HttpMethod.Post,$"{Root}/receipts/{receiptId}/record",OrderRequester,
            new { version=draft.GetProperty("version").GetInt64(),evidenceReference="RCP:MATCHING-BROWSER-ARRIVAL" },Guid.NewGuid());
        var recorded=await Get($"{Root}/receipts/{receiptId}",OrderManager1); var physical=recorded.GetProperty("lines")[0]; var receiptLineId=physical.GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.OK,HttpMethod.Post,$"{Root}/receipts/{receiptId}/lines/{receiptLineId}/inspect",OrderManager1,
            new { version=physical.GetProperty("version").GetInt64(),sourceVersion=physical.GetProperty("sourceVersion").GetInt64(),quantity="1",evidenceReference="RCP:MATCHING-BROWSER-INSPECTION" },Guid.NewGuid());
        var inspected=await Get($"{Root}/receipts/{receiptId}",OrderManager2); physical=inspected.GetProperty("lines")[0];
        await Expect(HttpStatusCode.OK,HttpMethod.Post,$"{Root}/receipts/{receiptId}/lines/{receiptLineId}/accept",OrderManager2,
            new { version=physical.GetProperty("version").GetInt64(),sourceVersion=physical.GetProperty("sourceVersion").GetInt64(),quantity="1",evidenceReference="RCP:MATCHING-BROWSER-ACCEPTANCE" },Guid.NewGuid());
        matchingBrowserSource=(source.OrderId,source.LineId,source.IssuedId,receiptLineId,supplierId);
        await PublishMatchingPolicy();
        var actors=new Dictionary<string,object>();
        using(var services=Scope(OrderBuyer))
        {
            var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var otherBranch=new WorkLocation { Code="MATCHING-BROWSER-OTHER",Name="Other invoice branch",LegalEntityId=OrderCompany };
            core.WorkLocations.Add(otherBranch);
            foreach(var (name,permissions,other) in new[]
            {
                ("mapper","procurement-invoice-read,procurement-invoice-capture,procurement-invoice-match,procurement-vendor-read",false),
                ("matcher","procurement-invoice-read,procurement-invoice-match",false),
                ("reader","procurement-invoice-read",false),
                ("otherBranchMatcher","procurement-invoice-read,procurement-invoice-match",true),
            })
            {
                var location=other?otherBranch.Id:OrderBranch; var role="browser_matching_"+name.ToLowerInvariant();
                core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey=role,RoleName="Synthetic matching "+name,Active=true,PermissionsCsv="procurement-access,"+permissions });
                var worker=new Worker { EmployeeNo="HTTP-matching-"+name,FirstName=name,LastName="Matching browser fixture",Status="active",LocationId=location };
                var user=new LocalUser { Email="matching-"+name+"@example.test",NormalizedEmail=("MATCHING-"+name+"@EXAMPLE.TEST").ToUpperInvariant(),
                    DisplayName=name,PasswordHash="synthetic-no-login-secret",WorkerId=worker.Id,RolesCsv=role };
                core.Workers.Add(worker); core.LocalUsers.Add(user); core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId=user.Id,LocationId=location });
                matchingBrowserActors[name]=(user.Id,worker.Id,location);
            }
            await core.SaveChangesAsync();
        }
        foreach(var (name,person) in matchingBrowserActors)
        {
            var response=await Send(HttpMethod.Get,Root+"/context",person.Principal,selectedLocation:person.Branch); await Check(response,HttpStatusCode.OK);
            var context=await response.Content.ReadFromJsonAsync<JsonElement>();
            var grants=context.GetProperty("permissions").EnumerateArray().Select(x=>x.GetString()!).ToHashSet(StringComparer.Ordinal);
            var expected=(name switch
            {
                "mapper"=>"procurement-access,procurement-invoice-read,procurement-invoice-capture,procurement-invoice-match,procurement-vendor-read",
                "reader"=>"procurement-access,procurement-invoice-read",
                _=>"procurement-access,procurement-invoice-read,procurement-invoice-match",
            }).Split(',');
            Require(grants.SetEquals(expected),"Matching browser actor has unexpected authority: "+name);
            actors[name]=new { principalId=person.Principal,branchId=person.Branch,context };
        }
        var mapper=matchingBrowserActors["mapper"].Principal;
        var meta=await Get(Root+"/meta",mapper); var capabilities=meta.GetProperty("capabilities");
        Require(meta.GetProperty("schemaReady").GetBoolean() && new[] { "invoiceIntake","invoiceDocuments","invoiceComparison" }.All(key=>capabilities.GetProperty(key).GetBoolean())
            && new[] { "invoiceQualifiedClaims","invoiceValidation","financeHandoff" }.All(key=>!capabilities.GetProperty(key).GetBoolean()),"Matching browser metadata misrepresented source/owner availability.");
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var docs=services.ServiceProvider.GetRequiredService<DocumentDbContext>();
            Require(!await db.SupplierInvoices.AnyAsync() && !await db.SupplierInvoiceLines.AnyAsync() && !await db.InvoiceMatchMappingVersions.AnyAsync()
                && !await db.InvoiceMatchResults.AnyAsync() && !await db.InvoiceExceptions.AnyAsync() && !await db.InvoiceMatchAllocations.AnyAsync()
                && !await db.InvoiceMatchExposureStates.AnyAsync() && !await db.FinanceInvoiceHandoffs.AnyAsync() && !await db.FinancePaymentStatusRefs.AnyAsync()
                && !await docs.Documents.AnyAsync() && !await docs.Versions.AnyAsync() && !await docs.ScanEvidence.AnyAsync() && !await docs.RecordEvents.AnyAsync(),"Matching browser must start before original invoice/document/matching evidence exists.");
            var order=await db.PurchaseOrders.SingleAsync(x=>x.Id==source.OrderId); var line=await db.PurchaseOrderLines.SingleAsync(x=>x.Id==source.LineId);
            var receipt=await db.GoodsReceipts.SingleAsync(x=>x.Id==receiptId); var supplier=await db.Suppliers.SingleAsync(x=>x.Id==supplierId);
            var policy=await db.PolicyVersions.SingleAsync(x=>x.PolicyKey==InvoiceMatchingPolicyRules.PolicyKey && x.State=="Published");
            var state=await db.ReceivingSourceStates.SingleAsync(x=>x.PurchaseOrderLineId==source.LineId);
            var requestLine=await db.PurchaseRequestLines.SingleAsync(x=>x.Id==line.PurchaseRequestLineId);
            Require(order.Status=="Issued" && line.Quantity==1 && line.UnitPrice==100 && line.AcceptedQuantity==1
                && state.Sequence==3 && state.CurrentAcceptedQuantity==1 && state.CurrentAcceptedNetAmount==100 && requestLine.CategoryCode=="IT" && requestLine.PurchaseType=="GOODS","Matching browser source lacks exact actual accepted authority.");
            matchingBrowserSourceBefore=await MatchingSourceFootprint();
            return await WriteBrowserManifest(new
            {
                baseUrl=$"http://127.0.0.1:{port}",tenantId="probe",companyId=OrderCompany,branchId=OrderBranch,currencyCode="ZMW",authenticationHeader="X-Probe-User",actors,
                matching=new
                {
                    source=new { orderId=source.OrderId,orderNumber=order.Number,issuedVersionId=source.IssuedId,purchaseOrderLineId=source.LineId,line.Description,
                        supplierId,supplierName=supplier.DisplayName,line.UnitCode,categoryCode="IT",currencyCode="ZMW",receiptLineId,receiptReference=receipt.Number,acceptedQuantity="1",acceptedNetAmount="100" },
                    policyVersionId=policy.Id,invoice=new { number=MatchingBrowserInvoiceNumber,date="2026-10-20",description="P26 original goods invoice",quantity="1",unitPrice="100",taxAmount="0" },
                    document=new { fileName=MatchingDocumentName,mediaType="application/pdf",contentUtf8=MatchingDocumentText,sha256=MatchingDocumentSha,
                        invoiceUnitCode=line.UnitCode,lineReference="Page 1 line 1",scannerBoundary="KnownFixtureOnlyDocumentScanner" },
                    noInvoiceOrMatchingRowsSeeded=true,originalUnitReviewer="Unavailable",historicalExposureOwner="Unavailable",noFinanceAuthority=true,
                },
                warning="Synthetic disposable sandbox. Known-fixture-byte scanner is not malware protection. No verified units, complete Finance history, invoice approval or AP authority exists.",
            });
        }
    }
    private async Task<string> MatchingSourceFootprint()
    {
        var source=matchingBrowserSource??throw new InvalidOperationException("Matching browser source missing.");
        using var services=Scope(OrderBuyer); var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var line=await db.PurchaseOrderLines.AsNoTracking().SingleAsync(x=>x.Id==source.Line);
        var sourceState=await db.ReceivingSourceStates.AsNoTracking().SingleAsync(x=>x.PurchaseOrderLineId==source.Line);
        return JsonSerializer.Serialize(new
        {
            line,requestLine=await db.PurchaseRequestLines.AsNoTracking().SingleAsync(x=>x.Id==line.PurchaseRequestLineId),
            state=sourceState,
            effects=await db.ReceivingAcceptanceEffects.AsNoTracking().Where(x=>x.ReceivingSourceStateId==sourceState.Id).OrderBy(x=>x.Id).ToArrayAsync(),
            receiptLine=await db.GoodsReceiptLines.AsNoTracking().SingleAsync(x=>x.Id==source.ReceiptLine),
        },Json);
    }
}
