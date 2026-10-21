using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private Guid? browserBudgetRequestId;
    private Guid? browserBudgetLineId;
    private long browserBudgetCurrentVersion;
    private long browserBudgetBusinessVersion;
    private BudgetBrowserCounts? browserBudgetBefore;
    private string? browserBudgetOriginalSourceSha;
    private const string BudgetBrowserPurpose = "Browser exact eight-place Finance check";
    private const string BudgetBrowserFinanceReference = "BROWSER-EXACT-BUDGET-CHECK";
    private const string BudgetBrowserEvidenceReference = "BROWSER-EXACT-BUDGET-EVIDENCE";
    private const string BudgetBrowserReservationReference = "BROWSER-EXACT-BUDGET-RESERVATION";
    private sealed record BudgetBrowserCounts(int Checks, int Audits, int Outbox, int Cases, int Decisions,
        int Pins, int Outcomes, int Allocations, int Orders, int PolicyEvaluations);

    private async Task<BudgetBrowserCounts> BudgetBrowserFootprint()
    {
        using var services = Scope(OrderFinance);
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var core = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
        return new(await db.BudgetCheckSnapshots.CountAsync(), await db.AuditEvents.CountAsync(), await db.OutboxEvents.CountAsync(),
            await core.Cases.CountAsync(), await core.Decisions.CountAsync(), await db.ApprovalSubmissions.CountAsync(),
            await db.ApprovalOutcomes.CountAsync(), await db.PurchaseRequestAllocations.CountAsync(), await db.PurchaseOrders.CountAsync(),
            await db.PolicyEvaluations.CountAsync());
    }

    private async Task<object> PrepareBudgetBrowserFixture()
    {
        Require(OrderDistinctRuntime, "Browser Finance check requires separate bootstrap/runtime identities.");
        // Actual request HTTP only: no snapshot, approval, state, policy evaluation or
        // business-version evidence is seeded here. These exact originals satisfy18,4.
        using var created = await Send(HttpMethod.Post, Root + "/requests", OrderRequester, new
        {
            purpose = BudgetBrowserPurpose, currencyCode = "ZMW", neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            lines = new[] { new { description = "Exact eight-place request line", unitCode = "each", requestedQuantity = 1.0001m,
                estimatedUnitPrice = 0.0001m, categoryCode = "IT", purchaseType = "GOODS" } },
        });
        await Check(created, HttpStatusCode.Created);
        var draft = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        using var submitted = await Send(HttpMethod.Post, $"{Root}/requests/{id}/submit", OrderRequester,
            new { version = draft.GetProperty("version").GetInt64() });
        await Check(submitted, HttpStatusCode.OK);
        // Source totals and both version semantics come from the real purpose queue.
        var queue = await Get(Root + "/requests/budget-queue?limit=100&offset=0", OrderFinance);
        var row = queue.GetProperty("records").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Require(row.GetProperty("estimatedAmount").GetDecimal() == 0.00010001m, "Exact source total was rounded before browser review.");
        using (var services = Scope(OrderFinance))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = await db.PurchaseRequests.Include(x => x.Lines).AsNoTracking().SingleAsync(x => x.Id == id);
            var line = request.Lines.Single();
            Require(request.Status == "Submitted" && request.ApprovalState == "PendingPolicyAndBudget" && request.ApprovalInstanceRef is null
                && request.SubmittedVersion is > 0 && request.SubmittedVersion <= request.Version
                && line.RequestedQuantity == 1.0001m && line.EstimatedUnitPrice == 0.0001m
                && ExactDecimalArithmetic.Multiply(line.RequestedQuantity, line.EstimatedUnitPrice) == 0.00010001m
                && !await db.BudgetCheckSnapshots.AnyAsync(x => x.PurchaseRequestId == id)
                && !await db.ApprovalSubmissions.AnyAsync(x => x.PurchaseRequestId == id),
                "Budget fixture must remain genuinely submitted and unfunded/unapproved.");
            browserBudgetRequestId = id; browserBudgetLineId = line.Id;
            browserBudgetCurrentVersion = request.Version; browserBudgetBusinessVersion = request.SubmittedVersion.Value;
            browserBudgetOriginalSourceSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(request, Json));
            Require(row.GetProperty("version").GetInt64() == browserBudgetCurrentVersion
                && row.GetProperty("businessVersion").GetInt64() == browserBudgetBusinessVersion,
                "Actual queue did not distinguish currentCAS from authoritative business version.");
        }
        browserBudgetBefore = await BudgetBrowserFootprint();
        return new { requestId = id, lineId = browserBudgetLineId, number = row.GetProperty("number").GetString(), purpose = BudgetBrowserPurpose,
            currentVersion = browserBudgetCurrentVersion, businessVersion = browserBudgetBusinessVersion, quantity = "1.0001", unitPrice = "0.0001",
            exactSourceAmount = "0.00010001", reviewedCheckedAmount = "0.0002", currencyCode = "ZMW", financePrincipalId = OrderFinance,
            financeCheckReference = BudgetBrowserFinanceReference, evidenceReference = BudgetBrowserEvidenceReference,
            reservationReference = BudgetBrowserReservationReference, noFinanceCheckOrApprovalSeeded = true,
            authorityMeaning = "SyntheticManualFinanceEvidenceOnly" };
    }

    public async Task VerifyBudgetBrowserCompletion()
    {
        Require(browserBudgetRequestId.HasValue && browserBudgetLineId.HasValue && browserBudgetBefore is not null, "No Budget browser source was prepared.");
        using var services = Scope(OrderFinance);
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var hrm = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        var cases = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
        var request = await db.PurchaseRequests.Include(x => x.Lines).AsNoTracking().SingleAsync(x => x.Id == browserBudgetRequestId);
        var line = request.Lines.Single();
        var snapshot = await db.BudgetCheckSnapshots.AsNoTracking().SingleAsync(x => x.PurchaseRequestId == request.Id);
        var account = await hrm.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == OrderFinance);
        var worker = await hrm.Workers.AsNoTracking().SingleAsync(x => x.Id == account.WorkerId);
        Require(account.IsActive && !account.IsArchived && !worker.IsArchived && worker.Status == "active" && worker.Id != request.RequesterWorkerId
            && request.CreatedBy == OrderRequester.ToString("D") && request.TenantId == "probe" && request.LegalEntityId == OrderCompany
            && request.BranchRef == OrderBranch && request.Purpose == BudgetBrowserPurpose && request.CurrencyCode == "ZMW"
            && request.Version == browserBudgetCurrentVersion && request.SubmittedVersion == browserBudgetBusinessVersion
            && request.Status == "Submitted" && request.ApprovalState == "PendingPolicyAndBudget" && request.ApprovalInstanceRef is null
            && line.Id == browserBudgetLineId && line.RequestedQuantity == 1.0001m && line.EstimatedUnitPrice == 0.0001m
            && ExactDecimalArithmetic.Multiply(line.RequestedQuantity, line.EstimatedUnitPrice) == 0.00010001m
            && browserBudgetOriginalSourceSha == ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(request, Json))
            && line.ApprovedQuantity == 0m && line.AllocatedQuantity == 0m && line.OrderedQuantity == 0m && line.AcceptedQuantity == 0m,
            "Browser check changed the original request, source amounts, attribution, counters or approval state.");
        Require(snapshot.TenantId == request.TenantId && snapshot.LegalEntityId == request.LegalEntityId
            && snapshot.RequestVersion == browserBudgetBusinessVersion && snapshot.Status == "Available"
            && snapshot.CheckedAmount == 0.0002m && snapshot.CurrencyCode == "ZMW" && snapshot.FinanceCheckRef == BudgetBrowserFinanceReference
            && snapshot.EvidenceRef == BudgetBrowserEvidenceReference && snapshot.ReservationRef == BudgetBrowserReservationReference
            && snapshot.ReservationStatus == "Confirmed" && snapshot.DecisionReason is null
            && snapshot.CheckedBySubjectId == OrderFinance.ToString("D") && snapshot.CheckedByWorkerId == worker.Id
            && snapshot.CheckerIdentitySchemaVersion == 1 && FinanceCheckerIdentityEvidence.HasValidBinding(snapshot)
            && snapshot.CheckedAt.Ticks % 10 == 0,
            "Browser check did not persist exact reviewed amount, complete Finance tuple and canonical worker-bound P27 evidence.");
        Require(!await db.ApprovalSubmissions.AnyAsync(x => x.PurchaseRequestId == request.Id)
            && !await db.ApprovalOutcomes.AnyAsync(x => x.PurchaseRequestId == request.Id)
            && !await db.PurchaseRequestAllocations.AnyAsync(x => x.PurchaseRequestId == request.Id)
            && !await cases.Cases.AnyAsync(x => x.SourceModule == "procurement" && x.SourceType == "purchase_request" && x.SourceId == request.Id),
            "A funding check must not open/complete approval or reserve procurement demand.");
        Require(await db.AuditEvents.CountAsync(x => x.RecordType == "PurchaseRequest" && x.RecordId == request.Id && x.Action == "BudgetChecked") == 1,
            "Expected one actual Finance capture audit only.");
        var before = browserBudgetBefore ?? throw new InvalidOperationException("Budget browser baseline is unavailable.");
        var expected = before with { Checks = before.Checks + 1, Audits = before.Audits + 1 };
        Require(await BudgetBrowserFootprint() == expected, "Budget browser introduced unexpected Finance, audit, outbox, Core, allocation, order or policy effects.");
        Console.WriteLine("PASS actual Budget browser owner proof: exact source0.00010001, explicit reviewed0.0002, one bound canonical checker snapshot/audit, original Submitted/unapproved source unchanged; no extra workflow/ledger effects.");
    }
}
