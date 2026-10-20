namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    /// <summary>Composition is deliberately separate from existing draft CRUD. The host must
    /// enable only after a reviewed PO migration, least-privilege schema readiness and complete
    /// workflow verification; defining these endpoints does not activate commercial execution.</summary>
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/orders");
        group.MapGet("/approved-sources", ApprovedSources).RequireAuthorization("procurement-po-convert");
        group.MapGet("/assigned-approvals", AssignedOrders).RequireAuthorization("procurement-po-approve");
        group.MapPost("/from-approved-sources", ConvertSources).RequireAuthorization("procurement-po-convert");
        group.MapPut("/{id:guid}/source-draft", UpdateSourceDraft).RequireAuthorization("procurement-po-edit");
        group.MapPost("/{id:guid}/source-cancel", CancelSourceDraft).RequireAuthorization("procurement-po-edit");
        group.MapPost("/{id:guid}/draft-finance-release-confirmations", ResolveUnsubmittedFunding).RequireAuthorization("procurement-po-finance-confirm");
        group.MapPost("/{id:guid}/finance-confirmations", ConfirmFunding).RequireAuthorization("procurement-po-finance-confirm");
        group.MapPost("/{id:guid}/finance-release-confirmations", ResolveOrderFunding).RequireAuthorization("procurement-po-finance-confirm");
        group.MapPost("/{id:guid}/submit", SubmitOrder).RequireAuthorization("procurement-po-submit");
        group.MapPost("/{id:guid}/assignments/{assignmentId:guid}/decisions", DecideOrder).RequireAuthorization("procurement-po-approve");
        group.MapGet("/{id:guid}/commercial-history", OrderCommercialHistory).RequireAuthorization("procurement-po-read");
        group.MapPost("/{id:guid}/issue", IssueOrder).RequireAuthorization("procurement-po-issue");
        group.MapPost("/{id:guid}/delivery-confirmations", ConfirmOrderDelivery).RequireAuthorization("procurement-po-dispatch-confirm");
    }
}
