namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Reviewed first-party account UI; never a browser-supplied redirect/origin.</summary>
internal static class ProductAccountHub
{
    internal const string Origin = "https://www.kuubuerp.com";
    internal static bool Enabled(IConfiguration config) => config.GetValue<bool>("ERP:AccountHub:Enabled");
    internal static bool Allows(IConfiguration config, string origin) =>
        origin == (config["ERP:ProductIdentity:PublicOrigin"] ?? "https://app.kuubuerp.com")
        || Enabled(config) && origin == Origin;
    internal static bool MatchesSubject(HttpContext http, string subject) =>
        http.Request.Headers.Origin.ToString() != Origin
        || Enabled(http.RequestServices.GetRequiredService<IConfiguration>())
            && http.Request.Headers["X-Kuubu-Account-Subject"].ToString() == subject;
    internal static string Landing(IConfiguration config, string page, string legacy) =>
        Enabled(config) ? Origin + "/account/#/" + (page switch {
            "company-invitation" => "company-invitation", "new-company" => "new-company", "team" => "team", _ => "companies"
        }) : legacy;
}
