using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed class ProductProcurementAuthorityException(int status) : Exception("Workspace access changed.")
{
    internal int Status { get; } = status;
}

/// <summary>Checks the selected person's current authority before the owning EF
/// transaction commits. Product writes also hold the Core company shared fence.</summary>
internal sealed class ProductProcurementCommitGuard(IHttpContextAccessor accessor, IConfiguration configuration)
    : DbTransactionInterceptor
{
    private HttpContext? Request(TransactionEventData data) =>
        data.Context is ProcurementDbContext && ProductProcurementAdmission.Enabled(configuration)
            ? accessor.HttpContext : null;

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Request(eventData) is { } http)
        {
            if (!ProductProcurementAdmission.Allows(ProductRequestContext.Current(http)))
                throw new ProductProcurementAuthorityException(403);
            var checkedAccess = await ProductRequestContext.RecheckAsync(http, cancellationToken);
            if (checkedAccess != ModuleAccessStatus.Authorized)
                throw new ProductProcurementAuthorityException(checkedAccess == ModuleAccessStatus.Unavailable ? 503 : 403);
        }
        return result;
    }

    public override InterceptionResult TransactionCommitting(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result)
    {
        if (Request(eventData) is not null) throw new ProductProcurementAuthorityException(503);
        return result;
    }
}
