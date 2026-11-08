using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProductCompanyInvitationsFlow
{
    internal static bool Enabled(IConfiguration config) => ProductCompanyTeamFlow.Enabled(config)
        && config.GetValue<bool>("ERP:CompanyInvitations:Enabled");
    internal static void MapProductCompanyInvitations(this WebApplication app)
    {
        var group = app.MapGroup(ProductCompanyTeamFlow.Prefix+"/invitations");
        group.AddEndpointFilter(async (context, next) => Enabled(app.Configuration) ? await next(context) : Results.StatusCode(503));
        group.MapGet("", async (HttpContext context) => {
            var proof = await ProductCompanyTeamFlow.ReadProof(context);
            if (proof is null) return Results.Unauthorized();
            var query = context.Request.Query;
            if (!ProductCompanyTeamFlow.ValidQuery(query, ["company_id", "link_id", "after"])
                || !ProductCompanyTeamFlow.Cursor(query, out var after)
                || !ProductCompanyTeamFlow.CanonicalGuid(query["company_id"].ToString(), out var company)
                || !ProductCompanyTeamFlow.CanonicalGuid(query["link_id"].ToString(), out var link)) return Results.BadRequest();
            var page = await context.RequestServices.GetRequiredService<CoreCompanyInvitationsApi>()
                .ListAsync(new(proof.Issuer, proof.Subject, link), company, after, context.RequestAborted);
            if (page.Status != CompanyTeamReadStatus.Ok) return ProductCompanyTeamFlow.ReadFailure(page.Status);
            return Results.Ok(new { invitations = page.Items!.Select(x => new { invitation_id = x.Id,
                recipient = x.Recipient, role = x.Role, state = x.State, revision = x.Revision, expires_at = x.ExpiresAt }),
                next_cursor = page.Next, actor_role = page.ActorRole, operational_access_granted = false });
        }).AllowAnonymous();
        group.MapPost("/issue", (Func<HttpContext, Task<IResult>>)(context => Execute(context, issue: true))).AllowAnonymous();
        group.MapPost("/revoke", (Func<HttpContext, Task<IResult>>)(context => Execute(context, issue: false))).AllowAnonymous();
    }
    private static async Task<IResult> Execute(HttpContext context, bool issue)
    {
        var proof = await ProductCompanyTeamFlow.ReadProof(context);
        if (proof is null) return Results.Unauthorized();
        if (!await ProductCompanyTeamFlow.ValidateCsrf(context, proof)) return Results.StatusCode(403);
        if (context.Request.Query.Count != 0) return Results.BadRequest();
        const int limit = 8192;
        if (context.Request.ContentLength > limit) return Results.StatusCode(413);
        var buffer = new byte[limit+1]; var used = 0;
        while (used < buffer.Length) {
            var read = await context.Request.Body.ReadAsync(buffer.AsMemory(used), context.RequestAborted);
            if (read == 0) break;
            used += read;
        }
        if (used > limit) return Results.StatusCode(413);
        Guid company, link, key, invitation = Guid.Empty;
        int revision = 0;
        string? email = null, role = null;
        try {
            using var json = JsonDocument.Parse(buffer.AsMemory(0,used), new JsonDocumentOptions { MaxDepth = 4 });
            var root = json.RootElement; ProductWorkspaceSetupAuthority.RejectDuplicates(root);
            string[] allowed = issue ? ["company_id","link_id","command_key","email","role","confirmed"]
                : ["company_id","link_id","command_key","invitation_id","expected_revision","confirmed"];
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != allowed.Length
                || root.EnumerateObject().Any(x => !allowed.Contains(x.Name)) || root.GetProperty("confirmed").ValueKind != JsonValueKind.True
                || !ProductCompanyTeamFlow.CanonicalGuid(root.GetProperty("company_id").GetString(), out company)
                || !ProductCompanyTeamFlow.CanonicalGuid(root.GetProperty("link_id").GetString(), out link)
                || !ProductCompanyTeamFlow.CanonicalGuid(root.GetProperty("command_key").GetString(), out key)) return Results.BadRequest();
            if (issue) {
                email = root.GetProperty("email").GetString(); role = root.GetProperty("role").GetString();
                if (email is null || email.Length > 254 || role is not ("administrator" or "member")) return Results.BadRequest();
            } else {
                if (!ProductCompanyTeamFlow.CanonicalGuid(root.GetProperty("invitation_id").GetString(), out invitation)
                    || !root.GetProperty("expected_revision").TryGetInt32(out revision) || revision < 1) return Results.BadRequest();
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(); }
        var api = context.RequestServices.GetRequiredService<CoreCompanyInvitationsApi>();
        var actor = new CompanyTeamActor(proof.Issuer,proof.Subject,link);
        var result = issue ? await api.IssueAsync(actor,new(company,key,email!,role!,true),context.RequestAborted)
            : await api.RevokeAsync(actor,new(company,invitation,key,revision,true),context.RequestAborted);
        var status = result.Status switch { CompanyInvitationStatus.Issued => 201, CompanyInvitationStatus.Existing or CompanyInvitationStatus.Revoked => 200,
            CompanyInvitationStatus.Invalid => 400, CompanyInvitationStatus.Denied => 403, CompanyInvitationStatus.Conflict => 409, _ => 503 };
        return Results.Json(new { status = result.Status.ToString(), invitation_id = result.InvitationId,
            state = result.State, revision = result.Revision, operational_access_granted = false },statusCode:status);
    }
}
