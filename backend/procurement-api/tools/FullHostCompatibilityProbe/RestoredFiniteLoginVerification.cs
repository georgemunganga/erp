using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

/// <summary>Private infrastructure LOGIN/ACL proof only; never starts the API or publisher.</summary>
internal static class RestoredFiniteLoginVerification
{
    private sealed record Login(string Name,string Password,string ProofFile,string ProofSha256);
    private sealed record Input(string Database,string AuthMethod,Login[] Logins);
    internal static async Task RunAsync()
    {
        const string inputPath="/evidence/login-input.private.json";
        const string resultPath="/result/login-result.private.json";
        if(Environment.GetEnvironmentVariable("P30_RESTORED_FINITE_LOGIN")!="reviewed-private-only"
            || File.Exists(resultPath) || !File.Exists(inputPath)
            || (File.GetAttributes(inputPath)&FileAttributes.ReparsePoint)!=0
            || (File.GetUnixFileMode(inputPath)&(UnixFileMode.GroupRead|UnixFileMode.GroupWrite|UnixFileMode.GroupExecute
                |UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))!=0)
            throw new InvalidOperationException("Private reviewed login input required.");
        var input=JsonSerializer.Deserialize<Input>(await File.ReadAllTextAsync(inputPath))
            ?? throw new InvalidOperationException("Private login contract missing.");
        if(!Regex.IsMatch(input.Database,"^proc_p30_login_20261024_[0-9]{2}$")
            || input.AuthMethod!="scram-sha-256" || input.Logins.Length!=2
            || input.Logins.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=2)
            throw new InvalidOperationException("Private login contract invalid.");
        var outcomes=new List<object>();
        foreach(var login in input.Logins)
        {
            var suffix=input.Database[^2..];
            if(login.Name!="p30_record_login_"+suffix && login.Name!="p30_execute_login_"+suffix
                || !Regex.IsMatch(login.Password,"^[0-9a-f]{64}$")
                || login.ProofFile!=(login.Name=="p30_record_login_"+suffix?"record-proof.private.sql":"execution-proof.private.sql"))
                throw new InvalidOperationException("Private login identity invalid.");
            var proofPath=Path.Combine("/evidence",login.ProofFile);
            if((File.GetAttributes(proofPath)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidOperationException("Regular proof SQL required.");
            var proof=await File.ReadAllTextAsync(proofPath);
            if(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(proofPath)))!=login.ProofSha256
                || Regex.IsMatch(proof,@"\bSET\s+(?:(?:LOCAL|SESSION)\s+)?ROLE\b|\bSET\s+SESSION\s+AUTHORIZATION\b",RegexOptions.IgnoreCase))
                throw new InvalidOperationException("Exact no-role-switch SQL required.");
            var builder=new NpgsqlConnectionStringBuilder
            {
                Host="/private-pg-socket",Database=input.Database,Username=login.Name,Password=login.Password,
                Pooling=false,Multiplexing=false,NoResetOnClose=false,Timeout=10,CommandTimeout=120,
                IncludeErrorDetail=false,LogParameters=false,
            };
            var wrong=new NpgsqlConnectionStringBuilder(builder.ConnectionString)
            {Password=login.Password[..^1]+(login.Password[^1]=='0'?"1":"0")};
            var passwordDenied=false;
            try {await using var invalid=new NpgsqlConnection(wrong.ConnectionString);await invalid.OpenAsync();}
            catch(PostgresException error) when(error.SqlState=="28P01") {passwordDenied=true;}
            if(!passwordDenied) throw new InvalidOperationException("Wrong secret did not fail password authentication.");
            await using var connection=new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using(var identity=new NpgsqlCommand("""
                SELECT current_database()=@database AND current_user=@role AND session_user=@role
                  AND EXISTS(SELECT 1 FROM pg_roles r WHERE r.rolname=current_user AND r.rolcanlogin
                    AND NOT(r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls OR r.rolinherit)
                    AND r.rolconnlimit=-1 AND r.rolvaliduntil IS NULL AND r.rolconfig IS NULL)
                  AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r
                    ON r.oid=m.member OR r.oid=m.roleid WHERE r.rolname=current_user)
                  AND inet_server_addr() IS NULL
                """,connection))
            {
                identity.Parameters.AddWithValue("database",input.Database);
                identity.Parameters.AddWithValue("role",login.Name);
                if(await identity.ExecuteScalarAsync() is not true)
                    throw new InvalidOperationException("Actual finite login/session identity differs.");
            }
            await using(var command=new NpgsqlCommand(proof,connection)){await command.ExecuteNonQueryAsync();}
            // Proof ends in ROLLBACK; verify it did not change the connection identity.
            await using(var identity=new NpgsqlCommand("SELECT current_user=@role AND session_user=@role",connection))
            {
                identity.Parameters.AddWithValue("role",login.Name);
                if(await identity.ExecuteScalarAsync() is not true)
                    throw new InvalidOperationException("Finite proof changed its authenticated identity.");
            }
            outcomes.Add(new {role=login.Name,actualLogin=true,wrongSecretSqlState="28P01",
                exactMatrixPassed=true,proofSha256=login.ProofSha256});
        }
        await using(var output=new FileStream(resultPath,new FileStreamOptions {Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None,UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}))
            await JsonSerializer.SerializeAsync(output,new {status="PASS_PRIVATE_REAL_NPGSQL_LOGIN_ACL_ONLY",
                database=input.Database,authMethod=input.AuthMethod,logins=outcomes,
                apiStarted=false,publisherStarted=false,businessOperationsProven=false,productionTouched=false});
        File.SetUnixFileMode(resultPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        Console.WriteLine("PASS_PRIVATE_REAL_NPGSQL_LOGIN_ACL_ONLY");
    }
}
