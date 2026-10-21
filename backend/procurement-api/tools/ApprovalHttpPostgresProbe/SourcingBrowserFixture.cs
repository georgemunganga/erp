using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private Guid? browserRfqRequestId;
    private Guid? browserRfqLineId;
    private long browserRfqRequestVersion;
    private long browserRfqLineVersion;
    private string? browserRfqInvariantBefore;
    private const string RfqBrowserPurpose = "Browser governed competitive RFQ source";
    private const string RfqBrowserTitle = "Browser exact governed RFQ draft";
    private const string RfqBrowserCancellationReason = "Browser source no longer needed";

    private async Task<string> PrepareRfqBrowserManifest(int port)
    {
        Require(OrderDistinctRuntime, "RFQ browser fixture needs distinct owner/runtime identities.");
        await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices, rfq: true);
        await Authority(manager1, "BROWSER-RFQ-PR-M1");
        await Authority(manager2, "BROWSER-RFQ-PR-M2");
        await PublishRfqRequestPolicy(true);
        // Genuine HTTP request, independently recorded manual funding and actual Core One/All decisions.
        // No RFQ, hold, receipt, source evidence or original-command outcome is pre-created.
        var source = await NewApprovedOrderSource(RfqBrowserPurpose, 0.0002m, 0.0001m, 0.0001m);
        browserRfqRequestId = source.GetProperty("id").GetGuid();
        browserRfqLineId = SourceLine(source);
        using (var services = Scope(buyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "browser_rfq_buyer", RoleName = "Synthetic finite RFQ browser buyer",
                Active = true, PermissionsCsv = "procurement-access,procurement-sourcing-convert,procurement-sourcing-cancel" });
            (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).RolesCsv = "browser_rfq_buyer";
            if (!await core.UserBranchAssignments.AnyAsync(x => x.UserId == buyer && x.LocationId == branch && !x.IsArchived))
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = buyer, LocationId = branch });
            await core.SaveChangesAsync();
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = await db.PurchaseRequests.AsNoTracking().SingleAsync(x => x.Id == browserRfqRequestId);
            var line = await db.PurchaseRequestLines.AsNoTracking().SingleAsync(x => x.Id == browserRfqLineId);
            Require(request.Status == "Approved" && request.BranchRef == branch && request.ApprovalInstanceRef is not null
                && line.RequestedQuantity == 0.0002m && line.ApprovedQuantity == 0.0002m && line.EstimatedUnitPrice == 0.0001m
                && line.AllocatedQuantity == 0 && line.OrderedQuantity == 0 && line.AcceptedQuantity == 0
                && !await db.SourcingEvents.AnyAsync() && !await db.PurchaseRequestAllocations.AnyAsync()
                && !await db.SourcingDraftEvidenceRecords.AnyAsync() && !await db.SourcingDemandBindings.AnyAsync()
                && !await db.SourcingCommandReceipts.AnyAsync(), "RFQ browser source must be genuinely approved and untouched.");
            browserRfqRequestVersion = request.Version; browserRfqLineVersion = line.Version;
        }
        var picker = await Get(Root + "/sourcing/approved-sources?page=1&pageSize=25", buyer);
        var selected = picker.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("purchaseRequestLineId").GetGuid() == browserRfqLineId);
        Require(selected.GetProperty("canSelect").GetBoolean() && selected.GetProperty("minimumQuoteCount").GetInt32() == 0
            && selected.GetProperty("availableQuantity").GetString() == "0.0002"
            && selected.GetProperty("approvedUnitEstimate").GetString() == "0.0001"
            && selected.GetProperty("expectedRequestVersion").GetInt64() == browserRfqRequestVersion,
            "Actual RFQ picker lacks the exact untouched competitive source.");
        string methodCode;
        using (var services = Scope(buyer))
            methodCode = await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().ProcurementMethods
                .Where(x => x.Id == selected.GetProperty("methodId").GetGuid()).Select(x => x.Code).SingleAsync();
        Require(methodCode == "COMPETITIVE", "RFQ source has not retained the explicitly published competitive policy.");
        var context = await Get(Root + "/context", buyer);
        var meta = await Get(Root + "/meta", buyer);
        Require(meta.GetProperty("schemaReady").GetBoolean() && meta.GetProperty("capabilities").GetProperty("sourcingSourceDrafts").GetBoolean(),
            "Actual P28 readiness/capability unavailable; no browser-ready fixture is claimed.");
        browserRfqInvariantBefore = await RfqBrowserInvariant();
        return await WriteBrowserManifest(new {
            baseUrl = $"http://127.0.0.1:{port}", tenantId = "probe", companyId = company, branchId = branch,
            currencyCode = "ZMW", authenticationHeader = "X-Probe-User",
            actors = new { buyer = new { principalId = buyer, context } },
            rfq = new { requestId = browserRfqRequestId, lineId = browserRfqLineId, requestVersion = browserRfqRequestVersion,
                requestNumber = selected.GetProperty("requestNumber").GetString(), purpose = RfqBrowserPurpose,
                title = RfqBrowserTitle, cancellationReason = RfqBrowserCancellationReason, currencyCode = "ZMW",
                approvedQuantity = "0.0002", unitEstimate = "0.0001", selectedQuantity = "0.0001", expectedEstimatedValue = "0.00000001",
                minimumQuoteCount = 0, methodCode, buyerPrincipalId = buyer, noSourcingEffectsSeeded = true },
            warning = "Synthetic disposable sandbox only. Header authentication and fixture owner are not production authority.",
        });
    }

    private async Task<string> RfqBrowserInvariant()
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        string[] unchanged = ["budget_check_snapshots", "purchase_request_approval_submissions", "purchase_request_approval_outcomes",
            "purchase_request_finance_release_requests", "purchase_request_finance_release_confirmations", "policy_evaluations", "policy_versions",
            "procurement_methods", "sourcing_invitations", "supplier_bids", "supplier_bid_lines", "bid_evaluations", "sourcing_awards", "sourcing_award_lines", "purchase_orders", "purchase_order_lines", "purchase_order_finance_evidence", "purchase_order_issued_versions",
            "purchase_order_delivery_evidence", "goods_receipts", "service_acceptances", "supplier_invoices", "supplier_invoice_lines", "finance_invoice_handoffs"];
        var parts = unchanged.Select(table => $"SELECT '{table}' AS name,coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.id),'[]'::jsonb) AS rows FROM procurement.{table} r").ToList();
        parts.Add("SELECT 'purchase_requests',coalesce(jsonb_agg(to_jsonb(r)-'version'-'updated_at'-'updated_by' ORDER BY r.id),'[]'::jsonb) FROM procurement.purchase_requests r");
        parts.Add("SELECT 'purchase_request_lines',coalesce(jsonb_agg(to_jsonb(r)-'version'-'updated_at'-'updated_by'-'allocated_quantity'-'route' ORDER BY r.id),'[]'::jsonb) FROM procurement.purchase_request_lines r");
        foreach (var table in new[] { "cases", "stages", "assignments", "decisions" })
            parts.Add($"SELECT 'core_{table}',coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.\"Id\"),'[]'::jsonb) FROM core_approval.{table} r");
        await using var query = new NpgsqlCommand("SELECT jsonb_object_agg(name,rows)::text FROM (" + string.Join(" UNION ALL ", parts) + ") retained", owner);
        return (string)(await query.ExecuteScalarAsync())!;
    }

    public async Task VerifyRfqBrowserCompletion()
    {
        Require(browserRfqRequestId.HasValue && browserRfqLineId.HasValue && browserRfqInvariantBefore is not null, "No untouched RFQ browser fixture prepared.");
        using var services = Scope(buyer); // Fresh owner contexts after Kestrel stop; no browser completion flag is trusted.
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        var request = await db.PurchaseRequests.AsNoTracking().SingleAsync(x => x.Id == browserRfqRequestId);
        var source = await db.PurchaseRequestLines.AsNoTracking().SingleAsync(x => x.Id == browserRfqLineId);
        var ev = await db.SourcingEvents.AsNoTracking().SingleAsync();
        var line = await db.SourcingLines.AsNoTracking().SingleAsync();
        var original = await db.SourcingDraftEvidenceRecords.AsNoTracking().SingleAsync();
        var binding = await db.SourcingDemandBindings.AsNoTracking().SingleAsync();
        var allocation = await db.PurchaseRequestAllocations.AsNoTracking().SingleAsync();
        var receipts = await db.SourcingCommandReceipts.AsNoTracking().OrderBy(x => x.Operation).ToArrayAsync();
        var buyerAccount = await core.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == buyer);
        var buyerWorker = await core.Workers.AsNoTracking().SingleAsync(x => x.Id == buyerAccount.WorkerId);
        Require(buyerAccount.RolesCsv == "browser_rfq_buyer" && buyerAccount.IsActive && buyerWorker.Status == "active"
            && ev.ExecutionVersion == 1 && ev.Status == "Cancelled" && ev.Version == 2 && ev.Title == RfqBrowserTitle
            && ev.BranchRef == branch && ev.CurrencyCode == "ZMW" && ev.CancellationReason == RfqBrowserCancellationReason
            && ev.OwnerSubjectId == buyer.ToString("D") && ev.MakerWorkerId == buyerWorker.Id
            && line.SourcingEventId == ev.Id && line.PurchaseRequestLineId == source.Id && line.Status == "Cancelled" && line.Quantity == 0.0001m
            && ev.SourceEvidenceId == original.Id && original.SourcingEventId == ev.Id && original.MakerWorkerId == buyerWorker.Id
            && original.OriginalDraftFingerprint == ev.OriginalDraftFingerprint && original.PayloadSha256 == ProcurementEvidenceEncoding.Digest(original.PayloadJson)
            && binding.SourcingEventId == ev.Id && binding.SourceEvidenceId == original.Id && binding.SourcingLineId == line.Id
            && binding.PurchaseRequestId == request.Id && binding.PurchaseRequestLineId == source.Id && binding.AllocationId == allocation.Id
            && binding.TargetType == "SourcingEvent" && binding.Quantity == 0.0001m && binding.UnitEstimate == 0.0001m && binding.EstimatedValue == 0.00000001m
            && allocation.TargetType == "SourcingEvent" && allocation.TargetId == ev.Id && allocation.PurchaseRequestLineId == source.Id
            && allocation.Status == "Released" && allocation.Quantity == 0.0001m && allocation.EstimatedValue == 0.00000001m && allocation.Version == 2
            && allocation.Reason == RfqBrowserCancellationReason && request.Status == "Approved" && request.Version == browserRfqRequestVersion + 2
            && source.Version == browserRfqLineVersion + 2 && source.AllocatedQuantity == 0 && source.OrderedQuantity == 0 && source.AcceptedQuantity == 0
            && source.CancelledQuantity == 0 && source.ApprovedQuantity == 0.0002m && source.Route == "PendingSourcing",
            "RFQ browser final owned identity/evidence/exact value/cancellation/conservation failed.");
        Require(receipts.Length == 2 && receipts.Count(x => x.Operation == "Create") == 1 && receipts.Count(x => x.Operation == "Cancel") == 1
            && receipts.Select(x => x.CommandKey).Distinct().Count() == 2 && receipts.All(x => x.SourcingEventId == ev.Id
                && x.ActorPrincipalId == buyer.ToString("D") && x.ActorWorkerId == buyerWorker.Id
                && x.ResponseSha256 == ProcurementEvidenceEncoding.Digest(x.ResponseJson))
            && original.CreateCommandKey == receipts.Single(x => x.Operation == "Create").CommandKey
            && receipts.Single(x => x.Operation == "Create").ResultVersion == 1 && receipts.Single(x => x.Operation == "Create").ResponseStatusCode == 201
            && receipts.Single(x => x.Operation == "Cancel").ResultVersion == 2 && receipts.Single(x => x.Operation == "Cancel").ResponseStatusCode == 200,
            "RFQ browser did not retain exactly one original create and cancel command outcome.");
        Require(await db.AuditEvents.CountAsync(x => x.RecordId == ev.Id && x.Action == "DraftReserved") == 1
            && await db.AuditEvents.CountAsync(x => x.RecordId == ev.Id && x.Action == "DraftCancelled") == 1
            && await db.OutboxEvents.CountAsync(x => x.AggregateId == ev.Id && x.EventType == "sourcing.draft_reserved.v1") == 1
            && await db.OutboxEvents.CountAsync(x => x.AggregateId == ev.Id && x.EventType == "sourcing.draft_cancelled.v1") == 1,
            "RFQ browser duplicated reserve/release business effects.");
        Require(await RfqBrowserInvariant() == browserRfqInvariantBefore, "RFQ browser changed original source/business approval/Finance/PO/receiving/invoice state.");
        Console.WriteLine("PASS RFQ browser owner: exactly one retained cancelled source draft/evidence/binding/released allocation, two original receipts and two named audit/outbox effects, exact0.00000001 value; no current hold or Finance/PO/receiving/invoice/approval mutation.");
    }
}
