using Microsoft.AspNetCore.Authorization;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static void MapCommercialRoundRoutes(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}/commercial-draft", CommercialSave)
            .RequireAuthorization("procurement-sourcing-commercial-configure");
        group.MapPost("/{id:guid}/commercial-prepare", CommercialPrepare)
            .RequireAuthorization("procurement-sourcing-commercial-configure");
        group.MapPost("/{id:guid}/commercial-rounds", CommercialOpen)
            .RequireAuthorization("procurement-sourcing-round-open");
        group.MapPost("/commercial-rounds/{id:guid}/assignments/{assignmentId:guid}/decisions",
            CommercialDecide).RequireAuthorization(
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapPost("/{eventId:guid}/commercial-rounds/{id:guid}/offers", CommercialSubmitOffer)
            .RequireAuthorization(PrincipalRfqSealedManualOffers.SubmitAction);
        group.MapPost("/commercial-rounds/{id:guid}/close", CommercialClose)
            .RequireAuthorization(PrincipalRfqSealedManualOffers.CloseAction);
        group.MapGet("/commercial-rounds", CommercialRounds).RequireAuthorization();
        group.MapGet("/commercial-rounds/{id:guid}", CommercialRound).RequireAuthorization();
        group.MapGet("/commercial-round-reviews/assigned", CommercialRoundAssigned)
            .RequireAuthorization(PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        group.MapGet("/commercial-round-commands/{operation}/{key:guid}", CommercialRoundRecovery)
            .RequireAuthorization();
    }
}
