using Microsoft.AspNetCore.Mvc;
using Mightyfin.Erp.Core.Workflows.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Read-only discovery. Each module remains responsible for policy and execution authorization.</summary>
public static class CoreWorkflowConfigurationRoutes
{
    public static void MapRoutes(WebApplication app) => app.MapGet("/api/core/v1/workflow-configuration", Read)
        .RequireAuthorization("hrm-admin", CoreWorkspaceContextRoutes.Policy);

    private static IResult Read(HttpContext http, [FromServices] IEnumerable<IWorkflowConfigurationProvider> providers)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            return Results.Ok(new { schemaVersion = 1, items = WorkflowConfigurationRegistry.Read(providers) });
        }
        catch (InvalidOperationException)
        {
            return Results.Problem(title: "Workflow settings are temporarily unavailable.", statusCode: 503);
        }
    }
}
