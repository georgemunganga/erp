using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task VerifyUnavailableExistingPrincipalAsync(PrincipalApprovalFixture fixture,
        string owner,PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,
        Guid requestId,long version,Dictionary<string,object?> result,PrincipalSend send)
    {
        var before=await PrincipalFootprintAsync(ownerConnection);
        await using var disabled=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,false);
        disabled.PrepareEntryPointEnvironment();using var client=disabled.CreateClient(new(){AllowAutoRedirect=false});
        using var scope=disabled.Services.CreateScope();
        Need(scope.ServiceProvider.GetRequiredService<IProcurementPrincipalApprovalSchemaReadiness>()
            is UnavailableProcurementPrincipalApprovalSchemaReadiness,
            "Existing principal negative control requires unavailable provider.");
        await send(client,HttpMethod.Post,$"/api/procurement/v1/requests/{requestId}/approval-cases",
            fixture.Maker,new{expectedRequestVersion=version},HttpStatusCode.ServiceUnavailable);
        await ReadAcknowledgementWithoutEffectsAsync(ownerConnection,client,send,
            $"/api/procurement/v1/requests/{requestId}/approval-commands/Open/{Guid.NewGuid()}",
            fixture.Maker,HttpStatusCode.ServiceUnavailable);
        Need(await PrincipalFootprintAsync(ownerConnection)==before,
            "Unavailable existing principal control changed retained records.");
        result["unavailableExistingPrincipalOpenAndAcknowledgementVerified"]=true;
    }
}
