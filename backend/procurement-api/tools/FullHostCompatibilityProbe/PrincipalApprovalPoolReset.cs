using Npgsql;

// Private finite-role transport check, before the HTTP journey. No business
// data, credentials, connection string, SQL result or backend PID is exported.
internal static class PrincipalApprovalPoolReset
{
    internal static async Task VerifyAsync(PrincipalApprovalRuntimePrivileges.Connections roles)
    {
        await VerifyRoleAsync(roles.Record,roles.RecordRole);
        await VerifyRoleAsync(roles.Execution,roles.ExecutionRole);
    }

    private static async Task VerifyRoleAsync(string connection,string expectedRole)
    {
        var builder=new NpgsqlConnectionStringBuilder(connection);
        PrincipalApprovalHttpVerification.Need(builder.Pooling && !builder.Multiplexing
            && !builder.NoResetOnClose,"Private pooling profile must reset a finite role connection.");
        int backend;
        string originalApplication;
        await using(var first=new NpgsqlConnection(connection))
        {
            await first.OpenAsync();
            (backend,originalApplication)=await IdentityAsync(first,expectedRole);
            await using var marker=new NpgsqlCommand("SET application_name='p25_private_pool_reset_marker'",first);
            await marker.ExecuteNonQueryAsync();
        }
        await using(var second=new NpgsqlConnection(connection))
        {
            await second.OpenAsync();
            var (reused,application)=await IdentityAsync(second,expectedRole);
            PrincipalApprovalHttpVerification.Need(reused==backend &&
                application==originalApplication && application!="p25_private_pool_reset_marker",
                "Private finite role pool did not reuse a reset connection.");
        }
    }

    private static async Task<(int Backend,string Application)> IdentityAsync(NpgsqlConnection connection,string expectedRole)
    {
        await using var command=new NpgsqlCommand(
            "SELECT pg_backend_pid(),current_user,session_user,current_setting('application_name')",connection);
        await using var row=await command.ExecuteReaderAsync();
        PrincipalApprovalHttpVerification.Need(await row.ReadAsync(),
            "Private pool identity row missing.");
        PrincipalApprovalHttpVerification.Need(row.GetString(1)==expectedRole &&
            row.GetString(2)==expectedRole,"Private pool connection changed the finite role.");
        var identity=(row.GetInt32(0),row.GetString(3));
        PrincipalApprovalHttpVerification.Need(!await row.ReadAsync(),
            "Private pool identity returned more than one row.");
        return identity;
    }
}
