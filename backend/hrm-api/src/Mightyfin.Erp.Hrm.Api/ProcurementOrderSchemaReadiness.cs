using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementOrderSchemaReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }

/// <summary>Read-only discovery through the explicitly configured execution principal.
/// No migration application, bootstrap role creation or permission escalation occurs here.</summary>
public sealed class ProcurementOrderSchemaReadiness(IConfiguration configuration,
    Mightyfin.Erp.Procurement.Application.IProcurementScope scope, IProcurementApprovalSchemaReadiness coreReadiness)
    : IProcurementOrderSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        var connection = configuration.GetConnectionString("ProcurementApprovalExecution");
        if (string.IsNullOrWhiteSpace(connection) || !await coreReadiness.IsReadyAsync(ct)) return false;
        try
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(connection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            // Select actual new columns as well as keys: a renamed/partially applied
            // migration must not make the API advertise a commercial workflow as usable.
            _ = await db.PurchaseOrders.AsNoTracking().Select(x => new { x.Id, x.SubmittedBusinessVersion }).Take(1).ToArrayAsync(ct);
            _ = await db.ProcurementMethods.AsNoTracking().Select(x => x.ExecutionRoute).Take(1).ToArrayAsync(ct);
            _ = await db.OrderSubmissions.AsNoTracking().Select(x => new { x.Id, x.BuyerWorkerId, x.ParticipantWorkersJson }).Take(1).ToArrayAsync(ct);
            _ = await db.OrderAssignmentAuthority.AsNoTracking().Select(x => new { x.Id, x.CoreStageId, x.AssigneeWorkerId }).Take(1).ToArrayAsync(ct);
            _ = await db.OrderFinanceEvidence.AsNoTracking().Select(x => new { x.Id, x.CommercialSnapshotSha256, x.VerifiedByWorkerId }).Take(1).ToArrayAsync(ct);
            _ = await db.OrderFinanceResolutions.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            _ = await db.OrderIssuedVersions.AsNoTracking().Select(x => new { x.Id, x.ParticipantWorkersJson }).Take(1).ToArrayAsync(ct);
            _ = await db.OrderDeliveryEvidence.AsNoTracking().Select(x => new { x.Id, x.VerifiedByWorkerId }).Take(1).ToArrayAsync(ct);
            _ = await db.CommercialCommandReceipts.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT bool_and(has_table_privilege(current_user, relation, privilege))
                    AND NOT (SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls FROM pg_roles WHERE rolname=current_user)
                FROM (VALUES
                  ('procurement.purchase_orders','SELECT,INSERT,UPDATE'),
                  ('procurement.purchase_order_lines','SELECT,INSERT,UPDATE'),
                  ('procurement.purchase_request_allocations','SELECT,INSERT,UPDATE'),
                  ('procurement.purchase_requests','SELECT,UPDATE'),
                  ('procurement.purchase_request_lines','SELECT,UPDATE'),
                  ('procurement.purchase_order_submissions','SELECT,INSERT'),
                  ('procurement.purchase_order_assignment_authority','SELECT,INSERT'),
                  ('procurement.purchase_order_finance_evidence','SELECT,INSERT'),
                  ('procurement.purchase_order_finance_resolutions','SELECT,INSERT'),
                  ('procurement.purchase_order_issued_versions','SELECT,INSERT'),
                  ('procurement.purchase_order_delivery_evidence','SELECT,INSERT'),
                  ('procurement.commercial_command_receipts','SELECT,INSERT'),
                  ('procurement.audit_events','SELECT,INSERT'),
                  ('procurement.outbox_events','SELECT,INSERT'),
                  ('core_approval.cases','SELECT,INSERT,UPDATE'),
                  ('core_approval.stages','SELECT,INSERT,UPDATE'),
                  ('core_approval.assignments','SELECT,INSERT,UPDATE'),
                  ('core_approval.decisions','SELECT,INSERT')) AS required(relation,privileges)
                CROSS JOIN LATERAL string_to_table(privileges,',') AS grants(privilege)
                """;
            return await command.ExecuteScalarAsync(ct) is true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }
}
