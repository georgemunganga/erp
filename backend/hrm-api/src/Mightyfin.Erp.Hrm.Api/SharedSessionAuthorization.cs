using Microsoft.AspNetCore.Authorization;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Manage the caller's authenticated session/credential without requiring
/// HRM workforce admission. This policy grants no business or administration access.</summary>
internal static class SharedSessionAuthorization
{
    internal const string PolicyName = "shared-authenticated-session";
    internal static void AddPolicy(AuthorizationOptions options) =>
        options.AddPolicy(PolicyName, policy => policy.RequireAuthenticatedUser());
}
