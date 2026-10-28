using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceMatchingRoutes
{
    // Selector DTOs describe historical source facts. They are never allocation,
    // payment, or posting authority and never manufacture a legacy receipt ID.
    internal static object PrincipalSourceItem(Guid orderId, string orderNumber, Guid issuedId,
        Guid lineId, string description, InvoiceIssuedOrderLine line, InvoiceCategoryMatchRule? rule,
        string tenantId, Guid companyId, Guid branchId, PrincipalReceivingSourceState? state)
        => new
        {
            orderId, orderNumber, issuedVersionId = issuedId, purchaseOrderLineId = lineId,
            Description = description, tenantId, companyId, branchId,
            issuedLineVersion = line.Version, line.UnitCode,
            authorizedQuantity = Exact(line.Quantity), quantity = Exact(line.Quantity), unitPrice = Exact(line.UnitPrice),
            taxAmount = Exact(line.TaxAmount), line.CategoryCode,
            method = rule?.Method.ToString(), acceptanceKind = rule?.AcceptanceKind?.ToString(),
            sourceSchemaVersion = 3, principalReceivingSourceStateId = state?.Id,
            principalSourceVersion = state?.Sequence, principalSourceSha256 = state?.SourceSha256,
            sourceState = "VerifiedIssued", blocker = rule is null ? "MatchingCategoryUnavailable" : null,
            canAllocate = false, canPostToFinance = false
        };

    internal static object PrincipalAcceptanceItem(PrincipalInvoiceAcceptedFact fact,
        PrincipalReceivingAcceptance accepted)
        => new
        {
            id = fact.FactId, reference = accepted.EvidenceReference,
            kind = fact.Kind.ToString(), sourceVersion = fact.SourceSequence,
            goodsReceiptLineId = (Guid?)null, serviceAcceptanceId = (Guid?)null,
            sourceSchemaVersion = 3, principalReceivingSourceStateId = fact.SourceStateId,
            principalReceivingFactId = fact.FactId, factPayloadSha256 = fact.PayloadSha256,
            acceptedQuantity = Exact(fact.AcceptedQuantity),
            acceptedNetAmount = Exact(fact.AcceptedNetAmount), fact.AcceptedAt,
            returnProposalHasNoCapacityEffect = true,
            canAllocate = false, canPostToFinance = false
        };

    private static async Task<IReadOnlySet<Guid>> PrincipalSelectorBranches(HrmDbContext core,
        ProcurementRequestScope scope, SupplierInvoice invoice, CancellationToken ct)
    {
        if (invoice.BranchRef is not { } branch || branch == Guid.Empty)
            throw new MatchingProblem("InvoiceMatchingInvoiceBranchUnavailable",409);
        var declared = await ProcurementExecutionRecordScope.Branches(core,scope,branch,true,ct);
        if (declared.Length > 0) return declared.ToHashSet();
        // A Core account with no branch assignments has company reach. Restrict
        // this selector to the invoice's actual current company branch.
        if (scope.IsConfined || !await core.WorkLocations.AsNoTracking().AnyAsync(x =>
            x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
            && x.Id == branch && !x.IsArchived,ct))
            throw new MatchingProblem("InvoiceMatchingInvoiceBranchUnavailable",404);
        return new HashSet<Guid> { branch };
    }

    private static IQueryable<PurchaseOrder> PrincipalSelectorOrders(ProcurementDbContext db,
        ProcurementRequestScope scope, SupplierInvoice invoice, IReadOnlySet<Guid> branches)
        => ProcurementPrincipalOrderRetainedIssued.QueryBoundIssued(db,scope,branches).AsNoTracking()
            .Where(x => x.SupplierId == invoice.SupplierId && x.CurrencyCode == invoice.CurrencyCode
                && x.BranchRef == invoice.BranchRef
                && (invoice.PurchaseOrderId == null || x.Id == invoice.PurchaseOrderId));

    private static Task<IResult> PrincipalSources(Guid id, int? page, int? pageSize, string? q,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunPrincipal(http,scope,write:false,async(db,cases,core,auth,token) =>
        {
            var paging = SelectorPage(page,pageSize,q);
            var invoice = await SelectorInvoice(db,core,scope,http,auth,id,token);
            if (invoice.TenantId!=scope.TenantId || invoice.LegalEntityId!=scope.LegalEntityId)
                throw new MatchingProblem("InvoiceMatchingInvoiceNotFound",404);
            var branches = await PrincipalSelectorBranches(core,scope,invoice,token);
            var policy = await SelectorPolicy(db,scope,http,invoice,token);
            var query = from order in PrincipalSelectorOrders(db,scope,invoice,branches)
                join issued in db.PrincipalOrderIssuedVersions.AsNoTracking()
                    on order.Id equals issued.PurchaseOrderId
                join line in db.PurchaseOrderLines.AsNoTracking()
                    on order.Id equals line.PurchaseOrderId
                where issued.CommercialRevision == 1 && line.IsActive
                    && (paging.Pattern == null
                        || EF.Functions.ILike(order.Number,paging.Pattern,"\\")
                        || EF.Functions.ILike(line.Description,paging.Pattern,"\\"))
                orderby order.Number,order.Id,line.LineNumber,line.Id
                select new { OrderId=order.Id, OrderNumber=order.Number, IssuedId=issued.Id,
                    LineId=line.Id, line.Description };
            var total = await query.CountAsync(token);
            var rows = await query.Skip((paging.Page-1)*paging.Size).Take(paging.Size).ToArrayAsync(token);
            var items = new List<object>();
            var verified = new Dictionary<Guid,(InvoiceIssuedOrder Issued,PrincipalInvoiceIssuedWitness Witness)>();
            foreach (var row in rows)
            {
                if (!verified.TryGetValue(row.OrderId,out var owner))
                {
                    var issued = await ProcurementPrincipalInvoiceIssuedWitness.LoadReviewIssuedOrderAsync(
                        db,cases,scope,branches,row.OrderId,row.IssuedId,token);
                    var witness = await ProcurementPrincipalInvoiceIssuedWitness.LoadAsync(
                        db,cases,scope,branches,row.OrderId,row.IssuedId,token);
                    owner = (issued,witness); verified.Add(row.OrderId,owner);
                }
                if (owner.Issued.RevisionId != row.IssuedId
                    || owner.Witness.IssuedVersionId != row.IssuedId
                    || owner.Witness.SupplierId != invoice.SupplierId
                    || owner.Witness.CurrencyCode != invoice.CurrencyCode
                    || owner.Witness.BranchId != invoice.BranchRef)
                    throw new MatchingProblem("InvoiceIssuedSourceUnavailable",409);
                var line = owner.Issued.Lines.SingleOrDefault(x=>x.Id==row.LineId)
                    ?? throw new MatchingProblem("InvoiceIssuedSourceUnavailable",409);
                var state = await db.PrincipalReceivingSourceStates.AsNoTracking()
                    .SingleOrDefaultAsync(x=>x.PurchaseOrderId==row.OrderId
                        && x.IssuedVersionId==row.IssuedId
                        && x.PurchaseOrderLineId==row.LineId,token);
                if (state is not null && !owner.Witness.Sources.ContainsKey(state.Id))
                    throw new MatchingProblem("InvoiceIssuedSourceUnavailable",409);
                var rule = policy.Rules.SingleOrDefault(x=>x.CategoryCode==line.CategoryCode
                    && x.CurrencyCode==invoice.CurrencyCode);
                items.Add(PrincipalSourceItem(row.OrderId,row.OrderNumber,row.IssuedId,row.LineId,
                    row.Description,line,rule,invoice.TenantId,invoice.LegalEntityId,
                    owner.Witness.BranchId,state));
            }
            return JsonSerializer.Serialize(new { invoiceId=id,invoiceVersion=invoice.Version,
                policyVersionId=policy.Id,items,total,page=paging.Page,pageSize=paging.Size,
                isAuthority=false,canAllocate=false,canPostToFinance=false },Json);
        },ct);

    private static Task<IResult> PrincipalAcceptances(Guid id, Guid orderId, Guid lineId,
        int? page, int? pageSize, string? q, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => RunPrincipal(http,scope,write:false,async(db,cases,core,auth,token) =>
        {
            var paging = SelectorPage(page,pageSize,q);
            var invoice = await SelectorInvoice(db,core,scope,http,auth,id,token);
            if (invoice.TenantId!=scope.TenantId || invoice.LegalEntityId!=scope.LegalEntityId)
                throw new MatchingProblem("InvoiceMatchingInvoiceNotFound",404);
            var branches = await PrincipalSelectorBranches(core,scope,invoice,token);
            var policy = await SelectorPolicy(db,scope,http,invoice,token);
            var order = await PrincipalSelectorOrders(db,scope,invoice,branches)
                .SingleOrDefaultAsync(x=>x.Id==orderId,token)
                ?? throw new MatchingProblem("InvoiceMatchingOrderNotFound",404);
            var versions = await db.PrincipalOrderIssuedVersions.AsNoTracking()
                .Where(x=>x.PurchaseOrderId==orderId && x.CommercialRevision==1)
                .Take(2).ToArrayAsync(token);
            if (versions.Length!=1) throw new MatchingProblem("InvoiceIssuedSourceUnavailable",409);
            var issuedId = versions[0].Id;
            var issued = await ProcurementPrincipalInvoiceIssuedWitness.LoadReviewIssuedOrderAsync(
                db,cases,scope,branches,orderId,issuedId,token);
            var witness = await ProcurementPrincipalInvoiceIssuedWitness.LoadAsync(
                db,cases,scope,branches,orderId,issuedId,token);
            if (witness.SupplierId != invoice.SupplierId || witness.CurrencyCode != invoice.CurrencyCode
                || witness.BranchId != invoice.BranchRef || issued.Lines.All(x=>x.Id!=lineId))
                throw new MatchingProblem("InvoiceMatchingLineNotFound",404);
            var line = issued.Lines.Single(x=>x.Id==lineId);
            var rule = policy.Rules.SingleOrDefault(x=>x.CategoryCode==line.CategoryCode
                && x.CurrencyCode==invoice.CurrencyCode);
            if (rule is null) throw new MatchingProblem("InvoiceMatchingCategoryUnavailable",409);
            var state = await db.PrincipalReceivingSourceStates.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.IssuedVersionId==issuedId
                    && x.PurchaseOrderLineId==lineId,token);
            var store = new PrincipalReceivingStore(db);
            if (await store.HasLegacyHistoryAsync(lineId,token))
                throw new MatchingProblem("InvoiceAcceptanceHistoryUnavailable",409);
            if (state is null)
            {
                if (await db.PurchaseOrderLines.AsNoTracking().AnyAsync(x=>x.Id==lineId
                    && x.AcceptedQuantity!=0,token))
                    throw new MatchingProblem("InvoiceAcceptanceHistoryUnavailable",409);
                return JsonSerializer.Serialize(new { invoiceId=id,invoiceVersion=invoice.Version,
                    orderId,issuedVersionId=issuedId,purchaseOrderLineId=lineId,policyVersionId=policy.Id,
                    method=rule.Method.ToString(),state="NoRecordedAcceptance",blocker=(string?)null,
                    items=Array.Empty<object>(),total=0,page=paging.Page,pageSize=paging.Size,
                    availabilityMeaning="HistoricalPhysicalFactsOnly",isAuthority=false,
                    canAllocate=false,canPostToFinance=false },Json);
            }
            if (!witness.Sources.TryGetValue(state.Id,out var expected)
                || state.PurchaseOrderId!=orderId || state.PurchaseOrderLineId!=lineId)
                throw new MatchingProblem("InvoiceAcceptanceHistoryUnavailable",409);
            var facts=await db.PrincipalReceivingFacts.AsNoTracking()
                .Where(x=>x.SourceStateId==state.Id).OrderBy(x=>x.Sequence).Take(2001).ToArrayAsync(token);
            if (facts.Length>2000) throw new MatchingProblem("InvoiceAcceptanceHistoryUnavailable",409);
            var keys=facts.Select(x=>x.CommandKey).Distinct().ToArray();
            var bindingIds=facts.Select(x=>x.DraftLineBindingId).Distinct().ToArray();
            var bindings=await db.PrincipalReceivingDraftLineBindings.AsNoTracking()
                .Where(x=>bindingIds.Contains(x.Id)).Take(2001).ToArrayAsync(token);
            var receipts=await db.PrincipalReceivingCommandReceipts.AsNoTracking()
                .Where(x=>keys.Contains(x.CommandKey)).Take(2001).ToArrayAsync(token);
            var policyIds=facts.Select(x=>x.PolicyVersionId).Distinct().ToArray();
            var retainedPolicies=await db.PolicyVersions.AsNoTracking()
                .Where(x=>policyIds.Contains(x.Id)).Take(2001).ToArrayAsync(token);
            var history=PrincipalInvoiceAcceptedSourceProjection.Verify(expected,state,facts,
                bindings,receipts,retainedPolicies,invoice.SupplierId);
            var currentLine=await db.PurchaseOrderLines.AsNoTracking()
                .SingleAsync(x=>x.Id==lineId && x.PurchaseOrderId==orderId,token);
            if (currentLine.AcceptedQuantity!=state.AcceptedQuantity)
                throw new MatchingProblem("InvoiceAcceptanceHistoryUnavailable",409);
            var ledger=PrincipalReceivingStorageCodec.Ledger(state,facts,retainedPolicies);
            var accepted=history.Facts.Select(fact=>new {
                Fact=fact,Value=ledger.Acceptances.Single(x=>x.Id==fact.FactId)
            }).Where(x=>rule.AcceptanceKind is null
                || x.Fact.Kind.ToString()==rule.AcceptanceKind.ToString())
                .Where(x=>paging.Pattern is null
                    || x.Value.EvidenceReference.Contains(q!.Trim(),StringComparison.OrdinalIgnoreCase))
                .OrderBy(x=>x.Value.EvidenceReference,StringComparer.Ordinal)
                .ThenBy(x=>x.Fact.FactId).ToArray();
            var items=accepted.Skip((paging.Page-1)*paging.Size).Take(paging.Size)
                .Select(x=>PrincipalAcceptanceItem(x.Fact,x.Value)).ToArray();
            return JsonSerializer.Serialize(new { invoiceId=id,invoiceVersion=invoice.Version,
                orderId,issuedVersionId=issuedId,purchaseOrderLineId=lineId,policyVersionId=policy.Id,
                method=rule.Method.ToString(),state=accepted.Length==0?"NoRecordedAcceptance":"VerifiedHistorical",
                blocker=(string?)null,items,total=accepted.Length,page=paging.Page,pageSize=paging.Size,
                availabilityMeaning="HistoricalPhysicalFactsOnly",isAuthority=false,
                canAllocate=false,canPostToFinance=false },Json);
        },ct);
}
