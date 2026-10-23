using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentRoutes
{
    private sealed record DetailPerson(string PrincipalId, string DisplayName);

    /// <summary>Purpose-limited Core labels for principals already present on this
    /// authorized request and its retained decisions. No account, employee or role
    /// directory is exposed. Inactive Core accounts keep their historical name.</summary>
    private static async Task<DetailPerson[]> DetailPeople(HrmDbContext core, ProcurementRequestScope scope,
        ManualPaymentRequest request, ManualPaymentDecision[] decisions,
        ManualPaymentWorkflowCaseLink[] links, ManualPaymentCoreDecisionLink[] linkedDecisions,
        CancellationToken ct)
    {
        var principals = new[] {
            request.CreatorPrincipalId, request.ApproverPrincipalId, request.RecorderPrincipalId,
            request.ConfirmerPrincipalId, request.ApprovedByPrincipalId,
            request.RecordedByPrincipalId, request.ConfirmedByPrincipalId
        }.Concat(decisions.SelectMany(x => new[] {
            x.ActorPrincipalId, x.PreviousApproverPrincipalId, x.NewApproverPrincipalId,
            x.PreviousRecorderPrincipalId, x.NewRecorderPrincipalId
        })).Concat(links.SelectMany(x => new[] { x.MakerPrincipalId, x.AssigneePrincipalId }))
            .Concat(linkedDecisions.Select(x => x.ActorPrincipalId));
        var ids = principals.Where(x => Guid.TryParseExact(x, "D", out var parsed) && parsed != Guid.Empty)
            .Select(x => Guid.ParseExact(x!, "D")).Distinct().ToArray();
        if (ids.Length == 0) return [];
        var found = await core.LocalUsers.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && ids.Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(ct);
        return found.Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .OrderBy(x => x.DisplayName, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .Select(x => new DetailPerson(x.Id.ToString("D"), x.DisplayName))
            .ToArray();
    }
}
