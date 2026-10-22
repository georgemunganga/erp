using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    // Private synthetic fixture only. Actual mapped current-Core checks and PG
    // records remain authoritative; no additional role or worker is invented.
    static async Task SupplierKnownAliasWithoutActiveWorkforce(HttpClient client)
    {
        Guid? priorOtherWorker; string priorWorkerStatus;
        await using (var core = Core())
        {
            var maker = await core.LocalUsers.SingleAsync(x => x.Id == Actor);
            var alias = await core.LocalUsers.SingleAsync(x => x.Id == Other);
            var worker = await core.Workers.SingleAsync(x => x.Id == Worker);
            Check(maker.WorkerId == Worker && alias.WorkerId == OtherWorker && maker.IsActive && alias.IsActive,
                "Stable alias proof requires the actual existing private Core links.");
            priorOtherWorker = alias.WorkerId; priorWorkerStatus = worker.Status;
            alias.WorkerId = Worker; worker.Status = "terminated";
            await core.SaveChangesAsync();
        }
        try
        {
            var context = await SupplierSend(client, Other, HttpMethod.Get, "/context");
            Check(context.GetProperty("principalId").GetGuid() == Other
                && context.GetProperty("workerId").ValueKind == JsonValueKind.Null,
                "Inactive optional HR projection must neither block Core access nor assert employment.");
            var saved = await SupplierSend(client, Actor, HttpMethod.Post, "/vendors", new
            {
                legalName = "Existing Core alias proof", displayName = "Existing Core alias proof",
                countryCode = "ZM", categoryCode = "SYS-SUP",
                primaryContact = new { name = "Synthetic alias contact", email = "alias-proof@example.test" },
            }, HttpStatusCode.Created);
            var id = Id(saved); var path = "/vendors/" + id;
            await using (var db = Db())
            {
                var supplier = await db.Suppliers.AsNoTracking().SingleAsync(x => x.Id == id);
                Check(supplier.ProposedByWorkerId == Worker && supplier.CreatedBy == Actor.ToString("D"),
                    "Creation with inactive HR lost the genuine existing Core alias reference.");
            }
            saved = await SupplierStep(client, Actor, path + "/submit", saved,
                v => new { version = v, businessReason = "Explicit private known-alias regression" });
            var detail = await SupplierSend(client, Other, HttpMethod.Get, path);
            Check(!detail.GetProperty("canApprove").GetBoolean() && !detail.GetProperty("canReject").GetBoolean()
                && !detail.GetProperty("canReturnForInformation").GetBoolean(), "Known alias review buttons became available after inactive HR.");
            var before = await SupplierLifecycleFootprint();
            await SupplierSend(client, Other, HttpMethod.Post, path + "/approve",
                new { version = Version(saved), reason = "Known alias cannot approve", duplicatesReviewed = true }, HttpStatusCode.Forbidden);
            await SupplierSend(client, Other, HttpMethod.Post, path + "/return",
                new { version = Version(saved), reason = "Known alias cannot review" }, HttpStatusCode.Forbidden);
            Check(before == await SupplierLifecycleFootprint(), "Known inactive-worker alias denial leaked supplier effects.");
        }
        finally
        {
            await using var core = Core();
            var alias = await core.LocalUsers.SingleAsync(x => x.Id == Other);
            var worker = await core.Workers.SingleAsync(x => x.Id == Worker);
            alias.WorkerId = priorOtherWorker; worker.Status = priorWorkerStatus;
            await core.SaveChangesAsync();
        }
    }
}
