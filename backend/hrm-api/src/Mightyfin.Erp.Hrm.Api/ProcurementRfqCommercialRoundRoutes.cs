using Microsoft.AspNetCore.Authorization;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static void MapCommercialRoundRoutes(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}/commercial-draft", CommercialSave)
            .RequireAuthorization("procurement-sourcing-commercial-configure");
        group.MapPost("/{id:guid}/commercial-prepare", CommercialPrepare)
            .RequireAuthorization("procurement-sourcing-commercial-configure");
        group.MapGet("/commercial-rounds", CommercialRounds).RequireAuthorization();
        group.MapGet("/commercial-rounds/{id:guid}", CommercialRound).RequireAuthorization();
        group.MapGet("/commercial-round-commands/{operation}/{key:guid}", CommercialRoundRecovery)
            .RequireAuthorization();
    }
}
