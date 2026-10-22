using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ProcurementCrudHttpProbe;

internal static partial class Program
{
    // Run only after the genuine additive recurring migration has been rehearsed.
    // This source increment deliberately does not bypass the canonical schema gate.
    static async Task RecurringPurchases(HttpClient client)
    {
        stage = "recurring-real-http-crud";
        object Contract(Guid supplier, string title) => new {
            supplierId = supplier, title, contractType = "Services", currencyCode = "ZMW",
            ceilingAmount = "12345678901234.5678", effectiveFrom = "2026-10-22",
            expiresOn = "2027-10-22", renewalNoticeOn = "2027-09-22"
        };
        var contract = await Send(client, HttpMethod.Post, "/contracts",
            Contract(approvedSupplier, "Recurring source contract"), HttpStatusCode.Created);
        var contractId = Id(contract);
        var secondVendor = await Send(client, HttpMethod.Post, "/vendors", new {
            legalName = "Synthetic alternate recurring supplier", displayName = "Alternate recurring supplier", countryCode = "ZM"
        }, HttpStatusCode.Created);
        var alternateSupplier = Id(secondVendor);
        object Plan(Guid supplier, Guid? sourceContract, string title, string amount, string? currency = null) => new {
            supplierId = supplier, supplierContractId = sourceContract, title, frequency = "Monthly",
            firstCycleOn = "2026-10-22", lastCycleOn = "2027-10-22", nextCycleOn = "2026-11-22",
            currencyCode = currency, expectedAmount = amount
        };
        var protectedBefore = await CreditProtectedState();
        var saved = await Send(client, HttpMethod.Post, "/recurring-purchases",
            Plan(approvedSupplier, contractId, "Monthly service planning", "12345678901234.5678"), HttpStatusCode.Created);
        var id = Id(saved); var path = "/recurring-purchases/" + id;
        Check(saved.GetProperty("currencyCode").GetString() == "ZMW"
            && saved.GetProperty("exactExpectedAmount").GetString() == "12345678901234.5678"
            && saved.GetProperty("ownerWorkerId").GetGuid() == Worker,
            "Recurring create lost Core currency, canonical owner or exact amount.");
        Check(Id(await Get(client, path)) == id, "Recurring plan did not survive HTTP reload.");
        var list = await Get(client, "/recurring-purchases?page=1&pageSize=25");
        Check(list.GetProperty("items").EnumerateArray().Any(row => Id(row) == id), "Recurring list omitted own saved plan.");
        saved = await Send(client, HttpMethod.Put, path, new {
            version = Version(saved), draft = Plan(approvedSupplier, contractId, "Updated service plan", "0.0001")
        });
        Check(saved.GetProperty("exactExpectedAmount").GetString() == "0.0001"
            && saved.GetProperty("title").GetString() == "Updated service plan"
            && Version(saved) == 2, "Recurring update lost exact values or did not advance exactly one version.");
        var beforeNoOpVersion = Version(saved);
        saved = await Send(client, HttpMethod.Put, path, new {
            version = beforeNoOpVersion, draft = Plan(approvedSupplier, contractId, "Updated service plan", "0.0001")
        });
        Check(Version(saved) == beforeNoOpVersion + 1, "Unchanged recurring PUT did not consume exactly one CAS version.");
        var reloaded = await Get(client, path);
        Check(Version(reloaded) == Version(saved)
            && reloaded.GetProperty("exactExpectedAmount").GetString() == "0.0001"
            && reloaded.GetProperty("title").GetString() == "Updated service plan", "Unchanged recurring PUT did not survive owning HTTP reload.");
        var before = await RecurringFootprint();
        await Send(client, HttpMethod.Put, path, new {
            version = beforeNoOpVersion, draft = Plan(approvedSupplier, contractId, "Updated service plan", "0.0001")
        }, HttpStatusCode.Conflict);
        Check(before == await RecurringFootprint(), "Replayed unchanged recurring PUT changed retained records or audit.");
        await Send(client, HttpMethod.Put, path, new {
            version = 1, draft = Plan(approvedSupplier, contractId, "Stale plan", "1")
        }, HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Put, path, new { version = Version(saved), draft = (object?)null }, HttpStatusCode.UnprocessableEntity);
        await Send(client, HttpMethod.Post, "/recurring-purchases",
            Plan(alternateSupplier, contractId, "Wrong contract supplier", "1"), HttpStatusCode.UnprocessableEntity);
        await Send(client, HttpMethod.Post, "/recurring-purchases",
            Plan(approvedSupplier, contractId, "Unrepresentable amount", "0.00001"), HttpStatusCode.UnprocessableEntity);
        await Send(client, HttpMethod.Get, path, null, HttpStatusCode.Forbidden, foreign: true);
        await RecurringOtherActorDenied(client, path);
        Check(before == await RecurringFootprint(), "Denied recurring actions changed records or audit.");
        var denied = await Send(client, HttpMethod.Put, "/contracts/" + contractId, new {
            version = Version(contract), draft = Contract(alternateSupplier, "Must retain original supplier")
        }, HttpStatusCode.Conflict);
        Check(denied.GetProperty("code").GetString() == "contract-referenced", "Referenced contract reparent denial used an unrelated guard.");
        saved = await Send(client, HttpMethod.Post, path + "/close", new {
            version = Version(saved), reason = "Service planning no longer required"
        });
        var closed = await Get(client, path);
        Check(closed.GetProperty("status").GetString() == "Closed"
            && closed.GetProperty("closedReason").GetString() == "Service planning no longer required"
            && closed.GetProperty("exactExpectedAmount").GetString() == "0.0001", "Recurring closure destroyed original planning evidence.");
        before = await RecurringFootprint();
        await Send(client, HttpMethod.Put, path, new {
            version = Version(saved), draft = Plan(approvedSupplier, contractId, "Closed edit", "2")
        }, HttpStatusCode.Conflict);
        denied = await Send(client, HttpMethod.Put, "/contracts/" + contractId, new {
            version = Version(contract), draft = Contract(alternateSupplier, "Closed reference must retain supplier")
        }, HttpStatusCode.Conflict);
        Check(denied.GetProperty("code").GetString() == "contract-referenced", "Closed reference lost its supplier invariant.");
        Check(before == await RecurringFootprint(), "Denied closed recurring action changed records or audit.");
        Check(protectedBefore == await CreditProtectedState(), "Recurring planning changed invoices, Finance, receiving or outbox records.");
        await using (var proof = Db())
        {
            var row = await proof.RecurringPurchaseCommitments.AsNoTracking().SingleAsync(x => x.Id == id);
            Check(row.Status == "Closed" && row.ExpectedAmount == 0.0001m && row.SupplierContractId == contractId
                && row.SupplierId == approvedSupplier && row.OwnerWorkerId == Worker, "Independent owning-store recurring reload mismatch.");
            foreach (var action in new[] { "PlanCreated", "PlanUpdated", "PlanClosed" })
                Check(await proof.AuditEvents.CountAsync(x => x.RecordId == id && x.Action == action) == (action == "PlanUpdated" ? 2 : 1),
                    "Recurring lifecycle audit missing or duplicated.");
            var updateAudits = await proof.AuditEvents.AsNoTracking()
                .Where(x => x.RecordId == id && x.Action == "PlanUpdated").Select(x => x.ChangedFieldsJson).ToArrayAsync();
            var versionPairs = new HashSet<(long Before, long After)>();
            foreach (var updateAudit in updateAudits)
            {
                using var auditDocument = JsonDocument.Parse(updateAudit);
                var audit = auditDocument.RootElement;
                versionPairs.Add((audit.GetProperty("before").GetProperty("Version").GetInt64(),
                    audit.GetProperty("after").GetProperty("Version").GetInt64()));
            }
            Check(versionPairs.SetEquals(new[] { (1L, 2L), (2L, 3L) }),
                "Recurring change and unchanged PUT audit versions do not match persisted CAS transitions.");
        }
        await RecurringContractRace(client, approvedSupplier, alternateSupplier, Contract, Plan);
        Check(protectedBefore == await CreditProtectedState(), "Concurrent recurring planning changed invoices, Finance, receiving or outbox records.");
    }

    static async Task<string> RecurringFootprint()
    {
        await using var proof = Db();
        return JsonSerializer.Serialize(new {
            baseline = await Footprint(),
            plans = await proof.RecurringPurchaseCommitments.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            contracts = await proof.SupplierContracts.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            versions = await proof.SupplierContractVersions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()
        });
    }

    static async Task RecurringOtherActorDenied(HttpClient client, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Root + path);
        request.Headers.Add("X-Crud-Actor", Other.ToString());
        request.Headers.Add("X-Shell-Entity", Company.ToString());
        request.Headers.Add("X-Shell-Location", Branch.ToString());
        using var response = await client.SendAsync(request); Interlocked.Increment(ref requests);
        Check(response.StatusCode == HttpStatusCode.NotFound, "Another employee read a maker-owned recurring plan.");
    }

    static async Task RecurringContractRace(HttpClient client, Guid supplier, Guid alternate,
        Func<Guid, string, object> contractDraft, Func<Guid, Guid?, string, string, string?, object> planDraft)
    {
        stage = "recurring-contract-two-http-request-serializable-race";
        var contract = await Send(client, HttpMethod.Post, "/contracts",
            contractDraft(supplier, "Concurrent recurring source"), HttpStatusCode.Created);
        var contractId = Id(contract);
        // A separate owner transaction holds only the target contract row. Both
        // actual HTTP transactions must reach an observable database lock wait
        // before it is released; simultaneous task scheduling alone is not proof.
        await using var barrier = new NpgsqlConnection(connection);
        await barrier.OpenAsync();
        await using var barrierTransaction = await barrier.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT id FROM procurement.supplier_contracts WHERE id=@id FOR UPDATE", barrier, barrierTransaction))
        {
            command.Parameters.AddWithValue("id", contractId);
            Check(await command.ExecuteScalarAsync() is Guid locked && locked == contractId, "Recurring race barrier did not lock the exact source contract.");
        }
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpStatusCode> Write(HttpMethod method, string path, object body)
        {
            using var request = new HttpRequestMessage(method, Root + path);
            request.Headers.Add("X-Crud-Actor", Actor.ToString());
            request.Headers.Add("X-Shell-Entity", Company.ToString());
            request.Headers.Add("X-Shell-Location", Branch.ToString());
            request.Content = JsonContent.Create(body);
            await start.Task;
            using var response = await client.SendAsync(request); Interlocked.Increment(ref requests);
            Check(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity,
                "Recurring contract race produced an unrelated failure.");
            return response.StatusCode;
        }
        var create = Write(HttpMethod.Post, "/recurring-purchases",
            planDraft(supplier, contractId, "Concurrent service plan", "1.0001", "ZMW"));
        var reparent = Write(HttpMethod.Put, "/contracts/" + contractId, new {
            version = Version(contract), draft = contractDraft(alternate, "Concurrent supplier correction")
        });
        start.SetResult();
        var observedOverlap = false;
        try
        {
            await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline && !create.IsCompleted && !reparent.IsCompleted)
            {
                await using var waiting = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_stat_activity
                    WHERE datname=current_database() AND state='active' AND wait_event_type='Lock'
                      AND @barrier = ANY(pg_blocking_pids(pid))
                      AND (query ILIKE '%supplier_contracts%' OR query ILIKE '%recurring_purchase_commitments%')
                    """, observer);
                waiting.Parameters.AddWithValue("barrier", barrier.ProcessID);
                if (await waiting.ExecuteScalarAsync() is long count && count == 2) { observedOverlap = true; break; }
                await Task.Delay(100);
            }
        }
        finally { await barrierTransaction.RollbackAsync(); }
        var results = await Task.WhenAll(create, reparent);
        Check(observedOverlap, "Both real HTTP transactions did not reach the observed database barrier; this run cannot claim concurrent write proof.");
        Check(results.Count(status => status is HttpStatusCode.Created or HttpStatusCode.OK) == 1,
            "Concurrent recurring creation and supplier change must not both commit.");
        await using var proof = Db();
        var actual = await proof.SupplierContracts.AsNoTracking().SingleAsync(x => x.Id == contractId);
        var plans = await proof.RecurringPurchaseCommitments.AsNoTracking().Where(x => x.SupplierContractId == contractId).ToArrayAsync();
        Check(plans.All(plan => plan.SupplierId == actual.SupplierId), "Concurrent writes broke retained supplier/contract ownership.");
        Check(results[0] == HttpStatusCode.Created ? plans.Length == 1 && actual.SupplierId == supplier
            : plans.Length == 0 && actual.SupplierId == alternate, "Race outcome disagrees with independent owning-store reload.");
    }
}
