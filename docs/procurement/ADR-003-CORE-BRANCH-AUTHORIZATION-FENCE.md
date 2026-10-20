# ADR-003 — Core branch authorization linearization

- Status: source implementation; verification pending; no production enablement.
- Date: 20 October 2026.
- Owners: Core authorization infrastructure and Procurement execution owners.
- Applies to: cooperative Core branch-assignment mutations and composed Procurement commands.
- Related: GW-ENG-STD-001; PROC-APPROVAL-P01-IMPLEMENTATION; initial PO execution contract.

## Context

PostgreSQL SERIALIZABLE gives a consistent snapshot, not a fresh authorization snapshot
on every SELECT. A command can acquire its snapshot, wait on a source row, then re-read
branch assignments that were revoked while waiting. Serializable ordering can legitimately
place the command before an independent membership write. An ordinary second query does
not prove that the revocation was observed.

Core owns branch assignments. Procurement must neither copy them nor gain HRM write rights.
Current Core semantics are explicit: an account without assignments is organisation-wide;
an account with assignments is confined to those branches. Removing the last assignment
is scope widening. Scope revocation tests use A+B -> B, never empty-as-deny.

## Decision and supported guarantee

Core provides a tenant-wide shared/exclusive PostgreSQL **session advisory fence**. The
same physical database must be used by both participants. A deterministic SHA-256-derived
64-bit namespace/key identifies the tenant. A theoretical key collision can cause additional
contention, never less isolation.

The composed Procurement transaction acquires the shared lease on its dedicated physical
connection **before BEGIN SERIALIZABLE or any authoritative transactional reads**. It then
creates fresh contexts, resolves current actor/assignees and branches, locks business records,
executes the command (including exact retries), commits or rolls back, disposes the transaction,
and releases the lease. Bounded transaction retries acquire a new lease and new snapshot.

Both existing Core branch writers (POST and DELETE `/branch-access` in ApiRoutesClean)
acquire the tenant's exclusive lease before membership reads and an explicit write transaction.
They retain it through commit. CoreBranchAccessMutation owns this composition; the module
does not implement its own branch administration or receive extra database permissions.

**Linearization:** a reader that owns the shared fence is ordered before a later writer,
even when that reader waits for a business row lock. The writer must wait for that command
to complete. A writer that obtains the exclusive fence first commits before any later
reader takes its SERIALIZABLE snapshot, so that reader sees the changed membership.
A successful administrative branch update means no earlier fenced command remains in
flight. This does not retroactively invalidate effects committed before that update.

Tenant-wide locking intentionally covers dynamic approver sets without discovering all
principals before the snapshot. Multiple commands can proceed concurrently; a branch
configuration write briefly waits for existing commands. Lock order is fence -> source
request(s) -> order/case. Branch writers must not acquire Procurement business locks first.

## Session lifecycle and failure

- Session leases require nonmultiplexed physical connections and ordinary pool reset;
  `Multiplexing=true` and `No Reset On Close=true` are rejected.
- Acquisition runs outside the transaction. Acquiring a transaction-scoped fence inside
  SERIALIZABLE would establish the very stale snapshot the fence must prevent.
- The transaction is disposed before explicit unlock. Unlock uses the same connection and
  a separate five-second cleanup token, even if the request token was canceled.
- Failed/uncertain acquisition or unlock clears the associated pool and closes the session,
  preventing a potentially locked physical connection from returning to circulation. This
  pool-wide eviction is deliberately conservative; failure is surfaced to the caller.
- Network/unknown commit outcomes still require the existing durable command-key recovery;
  the fence is not an exactly-once mechanism. Do not hold it across external network calls.
- Nested leases or writer upgrade from a held reader are unsupported; callers compose once
  around the physical connection. Existing coordinator creates a new connection per attempt;
  the Core writer helper rejects an existing EF transaction.

## Explicit limits

This is a **branch-membership fence**, not a claim of instantaneous revocation for every
identity attribute. Current role, account, worker and company checks still run, but their
writers do not all cooperate with this fence. Examples include LocalIdentity.UpdateUserAsync
(roles, active flag, worker link), account lockout, worker update/archive/account-link,
organisation location changes and Repositories.UpdateRoleAssignmentAsync. Those remain
snapshot-relative. Coupling them is a separate Core change with its own concurrency tests.
Direct operator SQL and fixture writes also require this protocol to claim the guarantee.
No database trigger silently grants Procurement permission to modify identity records.

The existing add/delete Core UI is not an atomic assignment-set replacement. For A -> B,
add B first and remove A second, or introduce a separately documented Core replacement
command. Deleting A first would intentionally expose the inherited organisation-wide state.
Middleware can deny early from a prior view; final command authorization must still resolve
current membership inside the fenced transaction. Selected branch context cannot broaden
current membership. Existing in-flight conservative confinement denial is retained.

## Alternatives

- Re-query inside SERIALIZABLE: rejected; snapshot does not refresh.
- Weaker isolation: rejected; sacrifices business transaction guarantees.
- Procurement-owned membership or HRM write/row locks: rejected; wrong ownership/grants.
- Per-person fences: deferred; dynamic approver discovery and acquisition ordering introduce
  additional snapshot and deadlock concerns without a demonstrated need.
- Database-global exclusive lock: rejected; unnecessarily serializes independent tenants.

## Verification source and release evidence

ApprovalHttpPostgresProbe `--orders` now authors actual PostgreSQL cases:

1. Runtime command holds shared fence and queues on source lock; cooperative exclusive
   writer must wait; reader commits, then writer completes.
2. Writer changes A+B to B while middleware can still observe old A, reader waits before
   snapshot; after writer commit the mapped HTTP mutation returns 403 without changing PO.
3. Transaction rollback releases shared lease; canceled acquisition discards its physical
   session; fresh pooled reader and exclusive writer remain usable without leaked locks.
4. Existing restricted-runtime assertions remain: no HRM writes are granted to Procurement.

These tests are authored, not executed in this source increment. Root verification must
compile, run the real disposable database probe and verify Core endpoint behavior before
any operational enablement. No schema migration or capability activation is introduced.
