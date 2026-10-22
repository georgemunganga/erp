using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    static Guid[] recurringBeforeBrowserIds = [];
    static string recurringBeforeBrowserPlans = "", recurringBeforeBrowserContracts = "", recurringBeforeBrowserProtected = "", recurringBeforeBrowserAudit = "";
    static async Task CaptureRecurringBrowserBaseline()
    {
        await using var db = Db();
        var plans = await db.RecurringPurchaseCommitments.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync();
        recurringBeforeBrowserIds = plans.Select(x => x.Id).ToArray();
        recurringBeforeBrowserPlans = JsonSerializer.Serialize(plans);
        recurringBeforeBrowserContracts = JsonSerializer.Serialize(new {
            contracts=await db.SupplierContracts.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            versions=await db.SupplierContractVersions.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            suppliers=await db.Suppliers.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync() });
        recurringBeforeBrowserAudit = JsonSerializer.Serialize(await db.AuditEvents.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync());
        recurringBeforeBrowserProtected = await RecurringProtectedOwners();
    }
    // Called only after the actual listener stops. No HTTP and no fixture DB writes.
    static async Task VerifyRecurringBrowserCompletion()
    {
        stage = "fresh-owner-recurring-browser-completion-after-shutdown";
        await using var db = Db();
        var added = await db.RecurringPurchaseCommitments.AsNoTracking().Where(x=>!recurringBeforeBrowserIds.Contains(x.Id)).OrderBy(x=>x.Id).ToArrayAsync();
        Check(added.Length == 2 && added.All(x => x.OwnerPrincipalId == RecurringPlanner && x.OwnerWorkerId is null
            && x.OwnerSubjectId == RecurringPlanner.ToString("D") && x.CreatedBy == RecurringPlanner.ToString("D")
            && x.WorkLocationId == Branch && x.SupplierId == approvedSupplier && x.SupplierContractId is null
            && x.FirstCycleOn == new DateOnly(2026,11,1) && x.NextCycleOn == new DateOnly(2026,11,1)
            && x.LastCycleOn is null && x.Frequency == "Monthly"), "Browser did not persist exactly two own workerless recurring plans with original dates/references.");
        var closed = added.Single(x=>x.Title.StartsWith("RECURRING-BROWSER-",StringComparison.Ordinal));
        var lost = added.Single(x=>x.Title.StartsWith("RECURRING-LOST-",StringComparison.Ordinal));
        Check(closed.Status == "Closed" && closed.Version == 4 && closed.ExpectedAmount == 5.1234m
            && closed.ClosedAt is not null && closed.ClosedReason == "Actual browser repeat purchase ended",
            "Browser closed history lost exact amount/CAS/reason.");
        Check(lost.Status == "Draft" && lost.Version == 1 && lost.ExpectedAmount == 12345678901234.5678m
            && lost.ClosedAt is null && lost.ClosedReason is null, "Lost delivery duplicated or modified its genuine saved create.");
        await using var core = Core();
        var companyCurrency = await core.LegalEntities.Where(x=>x.Id==Company).Select(x=>x.Currency).SingleAsync();
        Check(added.All(x=>x.CurrencyCode == companyCurrency), "Browser did not preserve actual organisation default currency.");
        Check(await core.LocalUsers.AsNoTracking().AnyAsync(x=>x.Id==RecurringPlanner && x.WorkerId==null && x.IsActive && !x.IsArchived),
            "Browser planner is not the original real workerless Core user.");
        var audits = await db.AuditEvents.AsNoTracking().Where(x=>x.RecordId==closed.Id && (x.Action=="PlanCreated" || x.Action=="PlanUpdated" || x.Action=="PlanClosed"))
            .OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).ToArrayAsync();
        Check(audits.Select(x=>x.Action).SequenceEqual(new[]{"PlanCreated","PlanUpdated","PlanUpdated","PlanClosed"})
            && audits.All(x=>x.RecordType=="RecurringPurchaseCommitment" && x.ActorSubjectId==RecurringPlanner.ToString("D")), "Browser lifecycle audit is missing, duplicated or belongs to another actor.");
        var successors = new HashSet<(long,long)>();
        foreach(var audit in audits.Where(x=>x.Action!="PlanCreated"))
        {
            using var body=JsonDocument.Parse(audit.ChangedFieldsJson ?? throw new ProbeFailure("Browser audit JSON missing."));
            successors.Add((body.RootElement.GetProperty("before").GetProperty("Version").GetInt64(),body.RootElement.GetProperty("after").GetProperty("Version").GetInt64()));
        }
        Check(successors.SetEquals(new[]{(1L,2L),(2L,3L),(3L,4L)}), "Browser no-op and closure audits do not match exact persisted CAS successors.");
        Check(await db.AuditEvents.CountAsync(x=>x.RecordId==lost.Id && x.Action=="PlanCreated")==1
            && !await db.AuditEvents.AnyAsync(x=>x.RecordId==lost.Id && (x.Action=="PlanUpdated" || x.Action=="PlanClosed")), "Lost create delivery was repeated or treated as a confirmed update.");
        Check(recurringBeforeBrowserPlans == JsonSerializer.Serialize(await db.RecurringPurchaseCommitments.AsNoTracking().Where(x=>recurringBeforeBrowserIds.Contains(x.Id)).OrderBy(x=>x.Id).ToArrayAsync()),
            "Browser changed a pre-existing CLI plan.");
        var ids=added.Select(x=>x.Id).ToArray();
        Check(recurringBeforeBrowserAudit == JsonSerializer.Serialize(await db.AuditEvents.AsNoTracking().Where(x=>!ids.Contains(x.RecordId)).OrderBy(x=>x.Id).ToArrayAsync()),
            "Browser introduced unrelated audit effects.");
        Check(recurringBeforeBrowserContracts == JsonSerializer.Serialize(new {
            contracts=await db.SupplierContracts.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            versions=await db.SupplierContractVersions.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            suppliers=await db.Suppliers.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync() }), "Browser changed supplier/contract prerequisites.");
        Check(recurringBeforeBrowserProtected == await RecurringProtectedOwners(), "Browser recurring planning changed other business owners or emitted downstream effects.");
        Console.WriteLine("PASS recurring browser fresh owning reload: two workerless plans, exact values/dates/currency, closed CAS1/2/3/4 and audit, one retained lost-delivery create, unchanged original plans/contracts/downstream owners; no Finance or scheduler authority.");
    }
}
