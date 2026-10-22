using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Npgsql;

/// <summary>
/// Opt-in actual database adapter/writer proof. Authorization is a strict SYNTHETIC owning-module
/// fixture, not real Procurement/SSO/publication authority. Bootstrap uploads/scans use existing
/// synthetic workflow. All pin/retire operations below use the distinct finite runtime identity.
/// </summary>
internal static class DocumentOwnerAdapterVerification
{
    internal static void ValidateInputs(string bootstrap)
    {
        var owner=new NpgsqlConnectionStringBuilder(bootstrap);
        var runtime=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CORE_DOCUMENT_RUNTIME_CONNECTION")
            ?? throw new InvalidOperationException("Provide a distinct private finite runtime connection for --owner-adapter."));
        if(owner.Database is null || !owner.Database.StartsWith("core_doc_rehearsal_",StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(owner.Host) || owner.Host.Contains(',')
            || owner.Database!=runtime.Database || owner.Host!=runtime.Host || owner.Port!=runtime.Port
            || string.IsNullOrWhiteSpace(runtime.Username) || !runtime.Username.StartsWith("core_doc_u2_",StringComparison.Ordinal)
            || owner.Username==runtime.Username || !runtime.Pooling || runtime.Multiplexing || runtime.NoResetOnClose)
            throw new InvalidOperationException("Use the same guarded disposable store and a distinct core_doc_u2_* pooled runtime role.");
    }

    internal static async Task RunAsync(string bootstrap,Scope initial,Func<DocumentOwner,Task<DocumentView>> upload)
    {
        ValidateInputs(bootstrap);
        var runtime=Environment.GetEnvironmentVariable("CORE_DOCUMENT_RUNTIME_CONNECTION")!;
        var reader=initial with { SubjectId="synthetic-use-reader" };
        var officer=initial with { SubjectId="synthetic-retirement-officer" };
        DocumentDbContext Db(Scope actor)=>new(new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(runtime,
            p=>p.MigrationsHistoryTable("__EFMigrationsHistory","core_document")).Options,actor);
        await Provision(bootstrap,runtime);
        await Denials(runtime);
        await using(var readiness=Db(reader))
            Check(!readiness.Database.HasPendingModelChanges() && !(await readiness.Database.GetPendingMigrationsAsync()).Any(),
                "The actual committed owning schema is not current.");
        async Task<Fixture> New()
        {
            var owner=new DocumentOwner("procurement","sourcing_event",Guid.NewGuid());
            var file=await upload(owner);
            await using var db=Db(reader);
            var document=await db.Documents.SingleAsync(x=>x.Id==file.DocumentId);
            var version=await db.Versions.SingleAsync(x=>x.Id==file.VersionId);
            return new(owner,new(Guid.NewGuid(),"procurement","synthetic_review",owner.Id,1,DocumentUseKind.Review),
                new(owner.Module,owner.Type,owner.Id,document.Id,version.Id,document.Version,version.Version,version.Sha256));
        }
        async Task<DocumentProblemException> ExpectRetained(Fixture f)
        {
            await using var db=Db(officer);
            try { await Writer(db,f).RetireAsync(f.Owner,f.Selection.DocumentId,f.Selection.DocumentRevision,"Synthetic owner retirement"); }
            catch(DocumentProblemException ex) when(ex.Code=="document-retained") { return ex; }
            throw new InvalidOperationException("Retirement ignored a committed retained pin.");
        }
        EfDocumentRetirement Writer(DocumentDbContext db,Fixture f,Func<CancellationToken,Task>? pause=null)=>new(db,officer,
            async (actual,ct)=>
            {
                Check(ReferenceEquals(db,actual) && actual.Database.CurrentTransaction is not null,"Retirement authorizer detached from actual owner transaction.");
                if(pause is not null) await pause(ct);
                return new RetirementAccess(officer,f);
            });

        // Reader first: observe the real exclusive writer waiting while actual pin+save remains uncommitted.
        var first=await New();
        await using(var pinDb=Db(reader))
        {
            await pinDb.Database.OpenConnectionAsync();
            await using var fence=await DocumentOwnerLifecycleFence.AcquireAsync(pinDb,reader,[first.Owner],false);
            await using var tx=await pinDb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var store=await EfDocumentUsePinStore.CreateAsync(pinDb,reader,new PurposeAccess(reader,first),fence,[first.Owner]);
            var pins=await store.AcquireAsync(first.Owner,first.Source,[first.Selection],default);
            await pinDb.SaveChangesAsync();
            await using var retireDb=Db(officer);
            var writerTask=Writer(retireDb,first).RetireAsync(first.Owner,first.Selection.DocumentId,1,"Reader first fixture");
            await ObserveWaiting(bootstrap,retireDb);
            Check(!writerTask.IsCompleted,"Retirement did not wait for the reader session.");
            await tx.CommitAsync();
            await fence.DisposeAsync();
            try { await writerTask;throw new InvalidOperationException("Reader-first retained use did not block retirement."); }
            catch(DocumentProblemException ex) when(ex.Code=="document-retained") { }
            Check(pins.Single().VersionId==first.Selection.VersionId,"Wrong version retained.");
        }
        await ExpectRetained(first);

        // Original recovery/current access: exact original actor/set replay, different authorized reader, no new rows.
        var baseline=await Footprint(bootstrap);
        await InPin(first,reader,async (db,store,access)=>
        {
            var replay=await store.AcquireAsync(first.Owner,first.Source,[first.Selection],default);
            Check(replay.Single().CreatedBy==reader.SubjectId,"Exact replay rewrote original actor.");
            Check(!db.ChangeTracker.Entries().Any(x=>x.State==EntityState.Added),"Replay added evidence.");
            access.Enabled=false;
            await Unauthorized(()=>store.FindAsync(first.Owner,first.Source,default));
        });
        await InPin(first,reader with { SubjectId="synthetic-current-assignee" },async (_,store,_)=>
        {
            var history=await store.FindAsync(first.Owner,first.Source,default);
            Check(history.Single().CreatedBy==reader.SubjectId,"Current assignee changed historical signer.");
            await Invalid(()=>store.AcquireAsync(first.Owner,first.Source,[first.Selection],default));
        });
        Check(await Footprint(bootstrap)==baseline,"Read/replay/denied access changed stored evidence.");

        // Writer first: current reader cannot use the retired version once it acquires its post-writer snapshot.
        var second=await New();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using(var retireDb=Db(officer))
        {
            var writerTask=Writer(retireDb,second,async ct=>{entered.SetResult();await release.Task.WaitAsync(ct);})
                .RetireAsync(second.Owner,second.Selection.DocumentId,1,"Writer first fixture");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var pinDb=Db(reader);
            var pinTask=Task.Run(async ()=>
            {
                await pinDb.Database.OpenConnectionAsync();
                await using var fence=await DocumentOwnerLifecycleFence.AcquireAsync(pinDb,reader,[second.Owner],false);
                await using var tx=await pinDb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var store=await EfDocumentUsePinStore.CreateAsync(pinDb,reader,new PurposeAccess(reader,second),fence,[second.Owner]);
                await Invalid(()=>store.AcquireAsync(second.Owner,second.Source,[second.Selection],default));
                await tx.RollbackAsync();
            });
            try { await ObserveWaiting(bootstrap,pinDb); }
            finally { release.TrySetResult(); }
            await writerTask;await pinTask;
        }
        await using(var db=Db(reader))
            Check((await db.Documents.SingleAsync(x=>x.Id==second.Selection.DocumentId)).IsRetired
                && !await db.UseSets.AnyAsync(x=>x.UseKey==second.Source.Key),"Writer-first outcome lost or pin created.");

        // Real producer/tracking regression: A loads authority before the fence, B retires it,
        // A must not reuse the stale identity map even though its tracked document still looks active.
        var preloaded=await New();
        await using(var stale=Db(reader))
        {
            var staleDocument=await stale.Documents.SingleAsync(x=>x.Id==preloaded.Selection.DocumentId);
            _=await stale.Versions.SingleAsync(x=>x.Id==preloaded.Selection.VersionId);
            await using(var writerDb=Db(officer))
                await Writer(writerDb,preloaded).RetireAsync(preloaded.Owner,preloaded.Selection.DocumentId,1,"Preloaded-context regression");
            Check(!staleDocument.IsRetired,"Fixture did not retain the intended stale identity map.");
            await stale.Database.OpenConnectionAsync();
            try {await using var invalid=await DocumentOwnerLifecycleFence.AcquireAsync(stale,reader,[preloaded.Owner],false);
                throw new Exception("Preloaded authoritative context acquired a lifecycle lease.");}
            catch(InvalidOperationException ex) when(ex.Message.Contains("clean owning context",StringComparison.Ordinal)) { }
        }
        await InPin(preloaded,reader,async (_,store,_)=>
            await Invalid(()=>store.AcquireAsync(preloaded.Owner,preloaded.Source,[preloaded.Selection],default)));

        // Saved and unsaved pin rollback: full six-table footprint stays exact.
        foreach(var save in new[]{false,true})
        {
            var f=await New();var before=await Footprint(bootstrap);
            await InPin(f,reader,async (db,store,_)=>
            {
                await store.AcquireAsync(f.Owner,f.Source,[f.Selection],default);
                if(save) await db.SaveChangesAsync();
            });
            Check(await Footprint(bootstrap)==before,"Pin rollback left owner/use evidence.");
        }
        var canceledPin=await New();var canceledPinBefore=await Footprint(bootstrap);
        await InPin(canceledPin,reader,async (_,store,_)=>
        {
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            try {await store.AcquireAsync(canceledPin.Owner,canceledPin.Source,[canceledPin.Selection],cancel.Token);
                throw new Exception("Canceled pin succeeded.");}
            catch(OperationCanceledException) when(cancel.IsCancellationRequested) { }
        });
        Check(await Footprint(bootstrap)==canceledPinBefore,"Canceled pin changed persisted evidence.");
        var failedPin=await New();var failedPinBefore=await Footprint(bootstrap);
        try
        {
            await InPin(failedPin,reader,async (db,store,_)=>
            {
                await store.AcquireAsync(failedPin.Owner,failedPin.Source,[failedPin.Selection],default);
                await db.SaveChangesAsync();throw new SyntheticAbort();
            });
        }
        catch(SyntheticAbort) { }
        Check(await Footprint(bootstrap)==failedPinBefore,"Failure after pin save did not roll back.");
        // Failed retirement save rolls back metadata and event together (owner-only targeted synthetic trigger).
        var failed=await New();var failedBefore=await Footprint(bootstrap);
        await Sql(bootstrap,$$"""
            CREATE FUNCTION core_document.u2_retirement_fault() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN IF NEW."DocumentId"='{{failed.Selection.DocumentId:D}}'::uuid AND NEW."Action"='DocumentRetired'
            THEN RAISE EXCEPTION 'synthetic retirement rollback'; END IF; RETURN NEW; END $f$;
            CREATE TRIGGER u2_retirement_fault BEFORE INSERT ON core_document.record_events
            FOR EACH ROW EXECUTE FUNCTION core_document.u2_retirement_fault();
            """);
        try
        {
            await using var db=Db(officer);
            try { await Writer(db,failed).RetireAsync(failed.Owner,failed.Selection.DocumentId,1,"Rollback fixture");
                throw new InvalidOperationException("Synthetic retirement fault did not abort."); }
            catch(DbUpdateException ex) when(ex.GetBaseException() is PostgresException { SqlState:"P0001" }) { }
        }
        finally { await Sql(bootstrap,"DROP TRIGGER u2_retirement_fault ON core_document.record_events; DROP FUNCTION core_document.u2_retirement_fault();"); }
        Check(await Footprint(bootstrap)==failedBefore,"Retirement failure partially persisted.");

        // Cancellation under the actual writer fence rolls back, then a real pin can acquire the owner.
        var canceled=await New();var canceledBefore=await Footprint(bootstrap);
        using(var cancel=new CancellationTokenSource())
        await using(var db=Db(officer))
        {
            var task=Writer(db,canceled,ct=>{cancel.Cancel();ct.ThrowIfCancellationRequested();return Task.CompletedTask;})
                .RetireAsync(canceled.Owner,canceled.Selection.DocumentId,1,"Cancellation fixture",cancel.Token);
            try { await task;throw new InvalidOperationException("Canceled retirement succeeded."); }
            catch(OperationCanceledException) when(cancel.IsCancellationRequested) { }
        }
        Check(await Footprint(bootstrap)==canceledBefore,"Canceled retirement changed persisted facts.");
        await InPin(canceled,reader,async (_,store,_)=>{await store.AcquireAsync(canceled.Owner,canceled.Source,[canceled.Selection],default);});

        await SessionLoss(first);
        foreach(var foreign in new[]{reader with { TenantId="foreign-tenant" },reader with { LegalEntityId=Guid.NewGuid() }})
            await InPin(first,foreign,async (_,store,_)=>Check((await store.FindAsync(first.Owner,first.Source,default)).Count==0,
                "Foreign scope saw retained pins."));
        Console.WriteLine("PASS: U2 synthetic exact-purpose adapter, finite runtime, pin/retire orderings, rollback, current access, scope and session-loss guards.");

        async Task InPin(Fixture f,Scope actor,Func<DocumentDbContext,EfDocumentUsePinStore,PurposeAccess,Task> action)
        {
            await using var db=Db(actor);await db.Database.OpenConnectionAsync();
            await using var fence=await DocumentOwnerLifecycleFence.AcquireAsync(db,actor,[f.Owner],false);
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var access=new PurposeAccess(actor,f);
            var store=await EfDocumentUsePinStore.CreateAsync(db,actor,access,fence,[f.Owner]);
            await action(db,store,access);await tx.RollbackAsync();
        }
        async Task SessionLoss(Fixture f)
        {
            foreach(var mode in new[]{"transaction","disposed","lost-lock","closed-session"})
            {
                var before=await Footprint(bootstrap);
                await using var db=Db(reader);await db.Database.OpenConnectionAsync();
                var fence=await DocumentOwnerLifecycleFence.AcquireAsync(db,reader,[f.Owner],false);
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var store=await EfDocumentUsePinStore.CreateAsync(db,reader,new PurposeAccess(reader,f),fence,[f.Owner]);
                if(mode=="transaction") await tx.RollbackAsync();
                if(mode=="disposed") await fence.DisposeAsync();
                if(mode=="lost-lock") await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock_shared({DocumentOwnerLifecycleFence.Key(reader,f.Owner)})");
                if(mode=="closed-session")
                {
                    await tx.RollbackAsync();await db.Database.CloseConnectionAsync();await db.Database.OpenConnectionAsync();
                    await using var replacement=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    await Invalid(()=>EfDocumentUsePinStore.CreateAsync(db,reader,new PurposeAccess(reader,f),fence,[f.Owner]));
                    await replacement.RollbackAsync();
                }
                else await Invalid(()=>store.FindAsync(f.Owner,f.Source,default));
                if(mode is "disposed" or "lost-lock") await tx.RollbackAsync();
                try { await fence.DisposeAsync(); }
                catch(AggregateException) when(mode is "lost-lock" or "closed-session") { }
                Check(await Footprint(bootstrap)==before,"Lost-session proof changed stored evidence.");
            }
        }
    }

    private static async Task Provision(string bootstrap,string runtime)
    {
        await using var connection=new NpgsqlConnection(runtime);await connection.OpenAsync();
        await using(var identity=new NpgsqlCommand("""
            SELECT r.rolsuper OR r.rolbypassrls OR r.rolcreaterole OR r.rolcreatedb OR r.rolreplication
              OR EXISTS(SELECT 1 FROM pg_auth_members WHERE member=r.oid)
              OR EXISTS(SELECT 1 FROM pg_database WHERE datname=current_database() AND datdba=r.oid)
              OR EXISTS(SELECT 1 FROM pg_namespace WHERE nspname='core_document' AND nspowner=r.oid)
              OR EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                        WHERE n.nspname='core_document' AND c.relowner=r.oid)
            FROM pg_roles r WHERE r.rolname=current_user
            """,connection))
            Check(await identity.ExecuteScalarAsync() is false,"Runtime must be a distinct nonowner role without inherited/elevated authority.");
        var role=Quote(new NpgsqlConnectionStringBuilder(runtime).Username!);
        var database=Quote(new NpgsqlConnectionStringBuilder(runtime).Database!);
        await Sql(bootstrap,$$"""
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            REVOKE TEMP ON DATABASE {{database}} FROM PUBLIC;
            REVOKE ALL ON SCHEMA core_document FROM {{role}};
            REVOKE ALL ON ALL TABLES IN SCHEMA core_document FROM {{role}};
            DO $columns$
            DECLARE item record;
            BEGIN FOR item IN
                SELECT c.relname,string_agg(quote_ident(a.attname),',' ORDER BY a.attnum) AS columns
                FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid
                WHERE n.nspname='core_document' AND c.relkind='r' AND a.attnum>0 AND NOT a.attisdropped
                GROUP BY c.relname
            LOOP EXECUTE format('REVOKE ALL (%s) ON core_document.%I FROM %I',item.columns,item.relname,{{Literal(new NpgsqlConnectionStringBuilder(runtime).Username!)}});
            END LOOP; END $columns$;
            GRANT CONNECT ON DATABASE {{database}} TO {{role}};
            GRANT USAGE ON SCHEMA core_document TO {{role}};
            GRANT SELECT ON core_document.documents,core_document.versions,core_document.scan_evidence,
                core_document.record_events,core_document.use_sets,core_document.use_pins,
                core_document."__EFMigrationsHistory" TO {{role}};
            GRANT INSERT ON core_document.use_sets,core_document.use_pins,core_document.record_events TO {{role}};
            GRANT UPDATE ("IsRetired","RetiredBy","RetirementReason","RetiredAt","Version") ON core_document.documents TO {{role}};
            """);
    }
    private static async Task Denials(string runtime)
    {
        await using var connection=new NpgsqlConnection(runtime);await connection.OpenAsync();
        foreach(var sql in new[]{
            "CREATE TABLE core_document.u2_forbidden(id integer)",
            "CREATE SCHEMA u2_forbidden", "CREATE TEMP TABLE u2_forbidden(id integer)",
            "UPDATE core_document.versions SET \"Sha256\"=\"Sha256\" WHERE FALSE",
            "UPDATE core_document.use_pins SET \"Sha256\"=\"Sha256\" WHERE FALSE",
            "UPDATE core_document.documents SET \"OwnerId\"=\"OwnerId\" WHERE FALSE",
            "DELETE FROM core_document.use_sets WHERE FALSE","DELETE FROM core_document.use_pins WHERE FALSE",
            "TRUNCATE core_document.use_sets","TRUNCATE core_document.documents"})
        {
            await using var tx=await connection.BeginTransactionAsync();
            try {await using var command=new NpgsqlCommand(sql,connection,tx);await command.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Finite runtime accepted forbidden DDL/immutable metadata mutation.");}
            catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.InsufficientPrivilege) { }
            finally {await tx.RollbackAsync();}
        }
    }
    private static async Task ObserveWaiting(string bootstrap,DocumentDbContext contender)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var observer=new NpgsqlConnection(bootstrap);await observer.OpenAsync(timeout.Token);
        while(true)
        {
            var connection=(NpgsqlConnection)contender.Database.GetDbConnection();
            if(connection.State==ConnectionState.Open)
            {
                await using var command=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid=@pid AND locktype='advisory' AND NOT granted)",observer);
                command.Parameters.AddWithValue("pid",connection.ProcessID);
                if(await command.ExecuteScalarAsync(timeout.Token) is true) return;
            }
            await Task.Delay(20,timeout.Token);
        }
    }
    private static async Task<string> Footprint(string bootstrap)
    {
        await using var connection=new NpgsqlConnection(bootstrap);await connection.OpenAsync();
        var snapshots=new List<string>();
        foreach(var table in new[]{"documents","versions","scan_evidence","record_events","use_sets","use_pins","__EFMigrationsHistory"})
            snapshots.Add($"({Literal(table)},(SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY to_jsonb(r)::text),'[]'::jsonb) FROM core_document.{Quote(table)} r))");
        snapshots.Add("""
            ('constraints',(SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.relname,r.conname),'[]'::jsonb)
              FROM (SELECT c.relname,k.conname,k.contype,k.convalidated,pg_get_constraintdef(k.oid,false) AS definition
                    FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname='core_document') r))
            """);
        snapshots.Add("""
            ('indexes',(SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.tablename,r.indexname),'[]'::jsonb)
              FROM pg_indexes r WHERE r.schemaname='core_document'))
            """);
        snapshots.Add("""
            ('columns',(SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.relname,r.attnum),'[]'::jsonb)
              FROM (SELECT c.relname,a.attnum,a.attname,format_type(a.atttypid,a.atttypmod) AS type,a.attnotnull,
                           pg_get_expr(d.adbin,d.adrelid,false) AS default_expression
                    FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
                    WHERE n.nspname='core_document' AND c.relkind='r' AND a.attnum>0 AND NOT a.attisdropped) r))
            """);
        await using var command=new NpgsqlCommand("SELECT jsonb_object_agg(name,data)::text FROM (VALUES "+string.Join(',',snapshots)+") AS snapshot_rows(name,data)",connection);
        var json=(string)(await command.ExecuteScalarAsync())!;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
    private static async Task Sql(string connection,string text)
    {await using var db=new NpgsqlConnection(connection);await db.OpenAsync();await using var command=new NpgsqlCommand(text,db);await command.ExecuteNonQueryAsync();}
    private static string Quote(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
    private static string Literal(string value)=>"'"+value.Replace("'","''")+"'";
    private static async Task Invalid(Func<Task> action)
    {try {await action();}catch(InvalidOperationException){return;}throw new Exception("Expected a controlled invalid operation.");}
    private static async Task Unauthorized(Func<Task> action)
    {try {await action();}catch(UnauthorizedAccessException){return;}throw new Exception("Current revoked purpose access was accepted.");}
    private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    private sealed class SyntheticAbort:Exception { }
    private sealed record Fixture(DocumentOwner Owner,DocumentUseReference Source,DocumentUseSelection Selection);
    private sealed class PurposeAccess(Scope actor,Fixture fixture):IDocumentUseAccess
    {
        internal bool Enabled {get;set;}=true;
        public Task RequireAsync(DocumentOwner owner,DocumentUseReference source,DocumentUseOperation operation,
            IReadOnlyList<Guid> ids,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(!Enabled || actor.SubjectId is not ("synthetic-use-reader" or "synthetic-current-assignee")
                || owner!=fixture.Owner || source!=fixture.Source || operation==DocumentUseOperation.Download
                || ids.Any(id=>id!=fixture.Selection.VersionId)
                || operation==DocumentUseOperation.Acquire && ids.Count!=1)
                throw new UnauthorizedAccessException("Synthetic exact-purpose fixture denied.");
            return Task.CompletedTask;
        }
    }
    private sealed class RetirementAccess(Scope actor,Fixture fixture):IDocumentRetirementAccess
    {
        public Task RequireAsync(DocumentOwner owner,Guid documentId,long revision,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(actor.SubjectId!="synthetic-retirement-officer" || owner!=fixture.Owner
                || documentId!=fixture.Selection.DocumentId || revision!=fixture.Selection.DocumentRevision)
                throw new UnauthorizedAccessException("Synthetic exact retirement fixture denied.");
            return Task.CompletedTask;
        }
    }
}
