# ADR — shared session lease and document owner lifecycle fence

Status: source-only prerequisite; unbuilt/unrun, no provider or production activation.
Base source: `bdc5f95`. Existing document-use persistence and P03 migration remain unchanged.

## Context

Core branch authorization and Procurement catalog authority already use the same
`PostgresSessionAdvisoryLease` acquisition/cleanup implementation. Core document pin
acquisition and managed lifecycle changes need the same supported physical-session
mechanics. Documents must not depend on the Approvals domain/infrastructure merely
to obtain a PostgreSQL lease, and copying cleanup logic would risk divergent pool
recovery after cancellation. These are three concrete owning consumers, not a
speculative general workflow abstraction.

## Decision

Move the existing lease to a tiny `Mightyfin.Erp.Core.Persistence.Infrastructure`
project, with only its already-used Npgsql10.0.3 dependency. Apart from the namespace,
the implementation is byte-identical: pinned/nonmultiplex/reset validation,
acquisition cancellation cleanup, five-second unlock deadline, uncertain-session
pool invalidation and idempotent normal release are preserved. Approvals,
Documents and Procurement infrastructure reference this project explicitly.

The existing `CoreBranchAuthorizationFence` and `ProcurementCatalogAuthorityFence`
keep their key bytes, public owning APIs and lock-order semantics. No identity,
policy, business rule, ledger or generic document authorization is moved. The
shared lease's CLR namespace changes; source consumers are updated together, so a
full coherent build/publish is required rather than mixing old/new assemblies.

`DocumentOwnerLifecycleFence` is an owning Documents wrapper. It takes the actual
`DocumentDbContext`, matching `IDocumentScope`, a bounded nonempty duplicate-free
set of existing `DocumentOwner` tuples and explicit shared/exclusive mode. It:

1. Validates actor tenant/entity/subject match the owning context; this is scope
   binding, **not** permission to use/read/retire a business document.
2. Requires actual Npgsql context and already-open owning physical connection.
   Known EF, enlisted and ambient transactions are rejected before lock acquisition.
   The shared lease rejects multiplexing and `No Reset On Close` settings.
3. Derives SHA256-first64-bit signed-big-endian owner keys from the frozen namespace
   `greenwebb.core.document-owner-lifecycle.v1`, exact tenant, legal entity,
   owner module/type/ID separated by NUL. Canonical text is bounded and NUL-free;
   module/type follow the existing ASCII owner token shape without normalization.
   Actor subject, document version and Review/Publication kind are deliberately
   absent: all authorized uses and retirement writers for one owner must meet on
   the same key. This is not a selectable client lock version.
4. Sorts distinct signed keys before acquisition, regardless of selection order.
   A hypothetical hash collision safely coarsens serialization. No shared-to-
   exclusive upgrade, second acquisition round or concurrent connection use is
   supported. Collect the complete owner set before entering the fence.
5. Unwinds earlier leases in reverse on partial acquisition failure, preserving the
   original error. Normal disposal attempts every release, aggregates cleanup
   errors and is idempotent; the single shared primitive discards uncertain sessions.
   It never starts/commits a database transaction or closes a healthy caller session.

## Transaction and ordering contract

The owning coordinator must acquire **before BEGIN and authority reads**, then keep
all leases through the owning transaction's commit/rollback and dispose them before
returning the session to its pool. When composing existing capabilities the order is:

`Core branch -> Procurement catalog -> all ordered document owners -> transaction -> domain row locks`.

Only take applicable fences, and never acquire them in reverse. Document-only writers
may take document owner exclusive without creating a dependency on other modules.
Pin/use readers take shared; every managed retirement/purge writer must take the same
owner exclusive fence and check retained uses in its own enlisted transaction.
A reader-first command is ordered before the writer; a writer-first command must see
its committed lifecycle result after it obtains the fence and starts its snapshot.
No lock establishes document readability, scan cleanliness, supplier eligibility,
current business approval or Finance authority by itself.

Npgsql10.0.3 keeps connector and enlisted transaction state internal rather than
exposing a public current local transaction API ([official driver source](https://github.com/npgsql/npgsql/blob/v10.0.3/src/Npgsql/NpgsqlConnection.cs)).
EF exposes its own enlisted transaction through the existing supported
[transaction facade](https://github.com/dotnet/efcore/blob/v10.0.0/src/EFCore/Extensions/TransactionsDatabaseFacadeExtensions.cs).
Therefore this adapter boundary requires a coordinator-owned connection with all
local transactions tracked by EF and ambient transactions absent. **Untracked raw
`BEGIN`, externally begun Npgsql transactions not adopted by EF, prior unfenced
snapshot reads and concurrent connection commands are unsupported.** The fence
must not pretend it can introspect those through reflection, infer them from a
client boolean, probe by opening another transaction or reset an existing session.
Any future adapter that cannot establish this managed entry contract stays blocked.

## Deliberate integration limits

No document use-store provider/DI registration, publication/pinning handler,
retirement/purge route, new grant, schema/migration or readiness flag is added.
The wrapper has no production caller yet. Existing same-context persistence retirement
guards remain defensive only and are not converted into race protection by this
source. Direct SQL/foreign writers and blob deletion do not participate. Complete
protection requires independently reviewed reader **and** writer integration,
current-purpose authorization, exact owning same-store enlistment, immutable use
records and actual concurrency/recovery/runtime grants acceptance. No malware,
retention-period, backup or Finance guarantee is implied.

## Authored verification and gates

`DocumentOwnerLifecycleFenceTests` authors five pure/precondition Facts and two
unsafe-session-setting Theory cases: frozen version/key, tenant/entity/owner
separation, subject-independent pairing, deterministic order, duplicate/empty/
oversize/noncanonical inputs, context scope mismatch, nonrelational/closed sessions,
ambient transaction denial and unchanged shared unsafe-settings checks. They make
no physical locking claim.

Existing `CoreDocumentWorkflowProbe` gains optional `--lifecycle-fence` (alone or
with existing `--use-pins`; defaults unchanged). It is restricted to a private
`core_doc_rehearsal_*` database and refuses unsafe connection settings. It authors
actual shared coexistence, observed exclusive waiting, normal release, known active
EF transaction denial, two-owner ordering with cancellation while the first key is
held, uncertain-session discard and replacement acquiring both keys. It snapshots
all six owned Core document/use tables before/after and requires no mutation.
It uses existing migrated document workflow setup; no fake pin/provider/readiness
or document retirement operation is introduced. This does not prove reader-versus-
retirement business races until the owning operations are paired.

No SDK/Node/PostgreSQL/Docker/test job has run on this branch. Parent must serialize
Core tests, compilation of all three consumers, unchanged branch/catalog probe
regressions, model drift and the opt-in private fence proof. Retain true failure
logs and source provenance. Source review is not runtime or release acceptance.
