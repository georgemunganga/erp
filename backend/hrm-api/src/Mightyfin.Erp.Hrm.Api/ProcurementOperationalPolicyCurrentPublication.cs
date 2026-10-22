using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Shared new-use provenance gate. The caller still selects the effective
/// policy and enforces operational permission, currency, category and record scope.</summary>
internal static class ProcurementOperationalPolicyCurrentPublication
{
    internal static async Task<ProcurementOperationalPolicyWorkerIdentity.PublicationProof> RequireAsync(
        ProcurementDbContext db, ProcurementRequestScope scope, IServiceProvider services,
        PolicyVersion policy, CancellationToken ct)
    {
        var readiness = services.GetService<IProcurementOperationalPolicyIdentitySchemaReadiness>();
        if (readiness is null || !await readiness.IsReadyAsync(db, ct))
            throw new OperationalPolicyPublicationProblem("OperationalPolicyIdentityUnavailable", 503);
        try
        {
            return await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy, ct);
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("OperationalPolicy", StringComparison.Ordinal))
        {
            throw new OperationalPolicyPublicationProblem("OperationalPolicyIdentityUnverified", 409);
        }
    }
}

internal sealed class OperationalPolicyPublicationProblem(string code, int status) : InvalidOperationException(code)
{
    internal string Code { get; } = code;
    internal int Status { get; } = status;
}
