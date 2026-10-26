using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>The governance admission composes the existing source/draft checks
/// with the M5 physical proof on the same finite execution connection. The
/// physical inspector has no parent callbacks, so M3b can safely use it when
/// admitting the newer PO/receipt constraint shape.</summary>
public sealed class ProcurementPrincipalOrderGovernanceSchemaReadiness(
    IConfiguration configuration, IProcurementScope scope,
    IProcurementPrincipalOrderDraftReadiness drafts) : IProcurementPrincipalOrderGovernanceReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!ProcurementPrincipalOrderDraftSchemaReadiness.TryExecutionConnection(configuration, out _))
            return false;
        try
        {
            await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(configuration.GetConnectionString("ProcurementApprovalExecution"),
                    x => x.MigrationsHistoryTable("__procurement_migrations", "procurement"))
                .Options, scope);
            return await IsReadyAsync(owner, false, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!ProcurementPrincipalOrderDraftSchemaReadiness.TryExecutionConnection(configuration, out var role)
            || !owner.Database.IsNpgsql()) return false;
        return await ProcurementPrincipalOrderGovernancePhysicalSchema.InspectAsync(owner, write, role, ct)
            && await drafts.IsReadyAsync(owner, write, ct);
    }
}

internal static class ProcurementPrincipalOrderGovernanceCapability
{
    internal static async Task<bool> ReadReadyAsync(IServiceProvider services,
        ProcurementRequestScope scope, bool pending, CancellationToken ct)
    {
        if (pending || scope.OrgUnitId is not null || scope.PrincipalId?.ToString("D") != scope.SubjectId
            || !ProcurementPrincipalOrderDraftRoutes.GovernedContextAllowed(true,
                scope.WorkLocationId, scope.IsExplicitAllBranches)
            || services.GetService<ProcurementPrincipalOrderDraftOptions>() is not { } drafts
            || services.GetService<ProcurementApprovalExecutionOptions>() is not { } approvals
            || !ProcurementPrincipalOrderDraftCapability.Configured(drafts, approvals)
            || services.GetService<IProcurementPrincipalOrderGovernanceReadiness>() is not { } readiness
            || !await readiness.IsReadyAsync(ct)) return false;
        var person = await ProcurementPrincipalPeople.Resolve(
            services.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>(),
            scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        return person is not null
            && (scope.WorkLocationId is { } branch
                ? person.OrganisationWide || person.AllowedBranches.Contains(branch)
                : person.OrganisationWide || person.AllowedBranches.Count > 0)
            && person.Permissions.Any(x => x is "procurement-po-read" or "procurement-po-approve"
                or "procurement-po-finance-confirm" or "procurement-po-issue" or "procurement-po-dispatch-confirm");
    }
}
