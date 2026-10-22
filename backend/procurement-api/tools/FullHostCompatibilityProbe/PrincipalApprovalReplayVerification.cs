using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task VerifyExactReplayAndConflictAsync(NpgsqlConnection owner,HttpClient client,PrincipalSend send,
        string path,Guid actor,Guid commandKey,object originalBody,object changedBody,JsonElement originalResponse,HttpStatusCode replayStatus)
    {
        Need(commandKey!=Guid.Empty,"Replay requires original explicit nonzero command UUID.");
        var baseline=await PrincipalFootprintAsync(owner);
        var replay=await send(client,HttpMethod.Post,path,actor,originalBody,replayStatus,commandKey);
        Need(replay.GetRawText()==originalResponse.GetRawText(),"Same-key body replay did not return exact original response bytes.");
        Need(await PrincipalFootprintAsync(owner)==baseline,"Same-key body replay changed persisted facts.");
        baseline=await PrincipalFootprintAsync(owner);
        var conflict=await send(client,HttpMethod.Post,path,actor,changedBody,HttpStatusCode.Conflict,commandKey);
        Need(conflict.GetProperty("code").GetString()=="ApprovalCommandKeyConflict","Changed-body original-key command did not report exact key conflict.");
        Need(await PrincipalFootprintAsync(owner)==baseline,"Changed-body original-key conflict changed persisted facts.");
    }

    private static async Task VerifyRevokedCurrentGrantReplayAsync(PrincipalApprovalFixture fixture,NpgsqlConnection owner,
        HttpClient client,PrincipalSend send,Guid actor,string roleKey,string permission,string path,Guid commandKey,object originalBody)
    {
        var before=await PrincipalFootprintAsync(owner);
        await using var core=fixture.Core();
        var user=await core.LocalUsers.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==actor);
        Need(user.RolesCsv.Split(',').Contains(roleKey,StringComparer.Ordinal),"Revocation fixture role is not actually assigned to original actor.");
        var role=await core.TenantRoleAssignments.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant
            &&x.RoleKey==roleKey&&x.Active&&!x.IsArchived);
        var original=role.PermissionsCsv;var parts=original.Split(',');
        Need(parts.Count(x=>x.Trim()==permission)==1,"Revocation needs exactly one original stored action grant.");
        var revoked=string.Join(',',parts.Where(x=>x.Trim()!=permission));
        // Private owning Core fixture edit, not an authorization bypass: remove one
        // real stored grant. ExecuteUpdate changes ONLY this field, so exact restore
        // need not rewrite audit timestamps or any account/identity/worker fields.
        try
        {
            Need(await core.TenantRoleAssignments.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id
                &&x.PermissionsCsv==original).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.PermissionsCsv,revoked))==1,
                "Exact stored Core grant revocation failed.");
            var deniedBaseline=await PrincipalFootprintAsync(owner);
            await send(client,HttpMethod.Post,path,actor,originalBody,HttpStatusCode.Forbidden,commandKey);
            Need(await PrincipalFootprintAsync(owner)==deniedBaseline,"Replay after actual current-grant revocation changed persisted facts.");
        }
        finally
        {
            // Read the real value even if the removal command failed ambiguously.
            // Only the exact fixture removal may be restored; never overwrite a third value.
            var actual=await core.TenantRoleAssignments.AsNoTracking().Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id)
                .Select(x=>x.PermissionsCsv).SingleAsync();
            if(actual==revoked)
                Need(await core.TenantRoleAssignments.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id
                    &&x.PermissionsCsv==revoked).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.PermissionsCsv,original))==1,
                    "Original Core action grant could not be restored exactly.");
            else Need(actual==original,"Unexpected concurrent Core grant value must not be overwritten.");
            Need(await PrincipalFootprintAsync(owner)==before,"Core grant restore or denied replay changed original full footprint.");
        }
    }
}
