using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentRoutes
{
    internal static bool RetainedReadConfigured(ProcurementManualPaymentOptions? options)
        => options is { ExecutionDatabaseConfigured: true } && (options.Enabled || options.ReadEnabled);

    internal sealed record RetainedPaymentReceipt(ManualPaymentCommandReceipt receipt, ManualPaymentRequest request);

    // This query is used directly by GET recovery; it never treats possession of a
    // command key, an old grant, a different actor or company-wide header as authority.
    internal static IQueryable<RetainedPaymentReceipt> RetainedReceiptQuery(
        IQueryable<ManualPaymentRequest> requests, IQueryable<ManualPaymentCommandReceipt> receipts,
        ProcurementRequestScope scope, ProcurementPrincipalPeople.Person person, Guid[] branches,
        Guid? id, Guid key)
    {
        var visible = Visible(requests, scope, person, branches);
        return from receipt in receipts
               join request in visible on receipt.ManualPaymentRequestId equals request.Id
               where receipt.TenantId == scope.TenantId && receipt.LegalEntityId == scope.LegalEntityId
                   && receipt.CommandKey == key && receipt.ActorPrincipalId == scope.SubjectId
                   && receipt.BranchId == request.BranchId && (id == null || request.Id == id)
               select new RetainedPaymentReceipt(receipt, request);
    }

    // Optional metadata: older clients can continue using the existing workflow
    // capability. This does not enable any mutation or require write privileges.
    internal static async Task<bool> ReadCapability(ProcurementDbContext db, ProcurementRequestScope scope,
        HttpContext http, bool pending, CancellationToken ct)
    {
        var services = http.RequestServices;
        if (pending || !RetainedReadConfigured(services.GetService<ProcurementManualPaymentOptions>())
            || services.GetService<IProcurementManualPaymentReadReadiness>() is not { } readiness
            || !db.Database.IsNpgsql() || db.Database.CurrentTransaction is not null
            || scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
            return false;
        var authorization = services.GetService<IAuthorizationService>();
        if (authorization is null || !(await authorization.AuthorizeAsync(http.User, "procurement-payment-read")).Succeeded)
            return false;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL transaction_read_only=on", ct);
        if (!await readiness.CanReadAsync(db, ct)) return false;
        await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, ct);
        try
        {
            var person = await Actor(core, scope, http, authorization, "procurement-payment-read", ct);
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, null, false, ct);
            return !scope.IsConfined || !person.OrganisationWide;
        }
        catch (UnauthorizedAccessException) { return false; }
    }
}
