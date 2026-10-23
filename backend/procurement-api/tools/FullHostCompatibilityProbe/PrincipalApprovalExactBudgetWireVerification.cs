using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private sealed record ExactBudgetCase(string Name, string Quantity, string UnitPrice,
        string Estimate, string Minimum);
    private static readonly ExactBudgetCase[] ExactBudgetCases =
    [
        new("EightPlaceProduct", "1.0001", "1.0001", "1.00020001", "1.0003"),
        new("TinyEightPlaceProduct", "0.0001", "0.0001", "0.00000001", "0.0001"),
        new("MaximumFourPlaceAmount", "1", "99999999999999.9999", "99999999999999.9999", "99999999999999.9999")
    ];

    private static async Task VerifyExactBudgetWireAsync(PrincipalApprovalFixture fixture, string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles, NpgsqlConnection ownerConnection,
        Dictionary<string, object?> result, Action<string> stage, PrincipalSend send)
    {
        stage("ExactBudgetBaseGraphBaseline");
        var baseRequest = (Guid)result["positiveApprovalRequestId"]!;
        var baseCase = (Guid)result["caseId"]!;
        var baseHash = await BaseGraphHash(ownerConnection, baseRequest, baseCase);
        await using var host = new PrincipalApprovalHost(fixture, owner, roles.Record, roles.Execution, true, true);
        host.PrepareEntryPointEnvironment();
        using var client = host.CreateClient(new() { AllowAutoRedirect = false });
        var createdIds = new List<Guid>();
        foreach (var item in ExactBudgetCases)
        {
            stage("ExactBudget" + item.Name);
            var created = await send(client, HttpMethod.Post, "/api/procurement/v1/requests", fixture.Maker,
                new { purpose = "Private exact budget wire " + item.Name,
                    currencyCode = "ZMW", neededBy = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
                    lines = new[] { new { description = "Exact wire fixture", unitCode = "each",
                        requestedQuantity = item.Quantity, estimatedUnitPrice = item.UnitPrice,
                        categoryCode = "PRIVATE_P25" } } }, HttpStatusCode.Created);
            var id = created.GetProperty("id").GetGuid();
            Need(id != Guid.Empty && !createdIds.Contains(id) && id != baseRequest,
                "Exact-budget cohort must create distinct real requests.");
            createdIds.Add(id);
            var path = "/api/procurement/v1/requests/" + id;
            var submitted = await send(client, HttpMethod.Post, path + "/submit", fixture.Maker,
                new { version = created.GetProperty("version").GetInt64() }, HttpStatusCode.OK);
            var queue = await send(client, HttpMethod.Get,
                "/api/procurement/v1/requests/budget-queue?limit=100", fixture.Checker);
            var records = queue.GetProperty("records").EnumerateArray()
                .Where(x => x.GetProperty("id").GetGuid() == id).ToArray();
            Need(records.Length == 1, "New principal request missing from the real Finance check queue.");
            var row = records[0];
            Need(row.GetProperty("estimatedAmountExact").GetString() == item.Estimate
                && row.GetProperty("minimumCheckedAmountExact").GetString() == item.Minimum,
                "PostgreSQL budget queue did not retain the exact eight-place product and four-place minimum.");
            var checkedResult = await send(client, HttpMethod.Post, path + "/budget-checks", fixture.Checker,
                new { requestVersion = submitted.GetProperty("version").GetInt64(),
                    financeCheckRef = "PRIVATE-P25-EXACT-" + item.Name,
                    status = "Insufficient", checkedAmount = item.Minimum, currencyCode = "ZMW",
                    evidenceRef = "private-fixture:exact-wire-human-check",
                    decisionReason = "Synthetic insufficient-funding check; no Finance reservation or spending authority",
                    reservationRef = (string?)null, reservationStatus = (string?)null }, HttpStatusCode.Created);
            Need(checkedResult.GetProperty("checkedAmountExact").GetString() == item.Minimum
                && checkedResult.GetProperty("checkerIdentitySchemaVersion").GetInt32() == 2
                && checkedResult.GetProperty("checkedByWorkerId").ValueKind == JsonValueKind.Null,
                "Actual workerless checker2 response changed the exact amount or identity.");
            var history = await send(client, HttpMethod.Get, path + "/budget-checks", fixture.Checker);
            var retained = history.GetProperty("records").EnumerateArray().ToArray();
            Need(retained.Length == 1 && retained[0].GetProperty("checkedAmountExact").GetString() == item.Minimum,
                "Retained Finance-check history changed the exact four-place amount.");
        }
        stage("ExactBudgetOwnerReconciliation");
        Need(await BaseGraphHash(ownerConnection, baseRequest, baseCase) == baseHash,
            "Exact-budget cohort changed the previously accepted principal approval graph.");
        await using var db = fixture.Procurement();
        await using var core = fixture.Core();
        Need(!await core.Workers.AnyAsync(), "Exact-budget cohort unexpectedly required employee records.");
        foreach (var id in createdIds)
        {
            var request = await db.PurchaseRequests.AsNoTracking().SingleAsync(x => x.Id == id);
            var checks = await db.BudgetCheckSnapshots.AsNoTracking().Where(x => x.PurchaseRequestId == id).ToArrayAsync();
            Need(request.Status == "Submitted" && request.RequesterIdentitySchemaVersion == 1
                && checks.Length == 1 && checks[0].CheckerIdentitySchemaVersion == 2
                && checks[0].Status == "Insufficient"
                && !await db.ApprovalSubmissions.AsNoTracking().AnyAsync(x => x.PurchaseRequestId == id),
                "Exact-budget requests must remain submitted with one genuine insufficient checker2 result and no approval.");
        }
        Need(!await db.FinancePaymentStatusRefs.AnyAsync() && !await db.FinanceInvoiceHandoffs.AnyAsync(),
            "Exact-budget probe cannot create Finance payment or AP facts.");
        result["exactBudgetWire"] = new
        {
            status = "PASS_PRIVATE_POSTGRESQL_WIRE_ONLY", cases = ExactBudgetCases.Length,
            actualCreateSubmitQueueRecordHistory = true, workerlessMakerAndChecker = true,
            originalBaseGraphUnchanged = true, noFinancePaymentOrApFacts = true,
            noBudgetReservationOrApprovalClaim = true
        };
        result["status"] = "PASS_PRIVATE_P25_APPROVAL_WITH_EXACT_BUDGET_WIRE";
    }

    private static async Task<string> BaseGraphHash(NpgsqlConnection owner, Guid requestId, Guid caseId)
    {
        await using var command = new NpgsqlCommand("""
            SELECT encode(sha256(convert_to(jsonb_build_object(
              'request',(SELECT to_jsonb(r) FROM procurement.purchase_requests r WHERE r.id=@request),
              'lines',(SELECT coalesce(jsonb_agg(to_jsonb(l) ORDER BY l.id),'[]'::jsonb)
                       FROM procurement.purchase_request_lines l WHERE l.purchase_request_id=@request),
              'checks',(SELECT coalesce(jsonb_agg(to_jsonb(b) ORDER BY b.id),'[]'::jsonb)
                        FROM procurement.budget_check_snapshots b WHERE b.purchase_request_id=@request),
              'submissions',(SELECT coalesce(jsonb_agg(to_jsonb(s) ORDER BY s.id),'[]'::jsonb)
                             FROM procurement.purchase_request_approval_submissions s WHERE s.purchase_request_id=@request),
              'assignmentEvidence',(SELECT coalesce(jsonb_agg(to_jsonb(a) ORDER BY a.id),'[]'::jsonb)
                                    FROM procurement.assignment_authority_evidence a
                                    WHERE a.submission_id IN
                                      (SELECT s.id FROM procurement.purchase_request_approval_submissions s
                                       WHERE s.purchase_request_id=@request)),
              'outcomes',(SELECT coalesce(jsonb_agg(to_jsonb(o) ORDER BY o.id),'[]'::jsonb)
                          FROM procurement.purchase_request_approval_outcomes o WHERE o.purchase_request_id=@request),
              'commandReceipts',(SELECT coalesce(jsonb_agg(to_jsonb(a) ORDER BY a.id),'[]'::jsonb)
                                 FROM procurement.approval_command_receipts a WHERE a.purchase_request_id=@request),
              'audit',(SELECT coalesce(jsonb_agg(to_jsonb(a) ORDER BY a.id),'[]'::jsonb)
                       FROM procurement.audit_events a WHERE a.record_id=@request),
              'outbox',(SELECT coalesce(jsonb_agg(to_jsonb(o) ORDER BY o.id),'[]'::jsonb)
                        FROM procurement.outbox_events o WHERE o.aggregate_id=@request),
              'case',(SELECT to_jsonb(c) FROM core_approval.cases c WHERE c."Id"=@case),
              'stages',(SELECT coalesce(jsonb_agg(to_jsonb(s) ORDER BY s."Id"),'[]'::jsonb)
                        FROM core_approval.stages s WHERE s."CaseId"=@case),
              'decisions',(SELECT coalesce(jsonb_agg(to_jsonb(d) ORDER BY d."Id"),'[]'::jsonb)
                           FROM core_approval.decisions d WHERE d."CaseId"=@case)
            )::text,'UTF8')),'hex')
            """, owner);
        command.Parameters.AddWithValue("request", requestId);
        command.Parameters.AddWithValue("case", caseId);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
