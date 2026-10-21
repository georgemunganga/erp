# Shared document use and retirement — private verification

## Scope and source

21 October 2026, source `17a0a5e`. This increment implements an enlisted exact-purpose document-use adapter, historical restoration, current-purpose authorization port, managed retirement writer and paired lifecycle/save guards. These remain unregistered: an owning module must supply its genuine authorization and transaction composition before operational use.

Independent review found that an EF identity map loaded before fencing could reuse stale availability after another writer retired the document. `f7782c3` rejects a nonempty change tracker before lock acquisition. A second review accepted the correction and the actual interleaving test source. The first compile failed on a missing transaction-extension namespace; `17a0a5e` fixes it. The failure log is retained, rather than counted as a pass.

## Executed gates

| Gate | Result |
| --- | --- |
| Shared document tests | 112 passed, zero failed/skipped |
| Procurement domain/model tests | 781 passed, zero failed/skipped |
| Mapped Procurement/Finance schema/Core organization regression | 311 passed, zero failed/skipped |
| Core workflow and Procurement HTTP tools | Release builds passed |
| Actual PostgreSQL document workflow, lifecycle fence and owner adapter | Passed, process exit0 |

Source gate logs/TRX: `artifacts/u2/verify-enlisted-guard-followup.log`, `artifacts/u2/core-tests/`, `artifacts/u2/procurement-tests/`, and the latest corresponding `artifacts/p29/api/` TRX. These checkpoint counts are separate suites, not cumulative historical totals. Existing nullable/analyzer warnings remain; the Core workflow tool build had zero warnings/errors.

## Actual database proof

Protected evidence: `/home/amizo/erp-backups/core-document-owner-postgres-20261021_094029/`. Source, binary hashes, SDK image ID, database/runtime-role manifest, exact command modes, terminal logs and result are retained. The process used an isolated PostgreSQL17 database, a separate bootstrap owner and a finite `core_doc_u2_*` runtime role, with networking disabled and the private Unix socket only.

- Existing workflow: migrations twice, exact/conflicting upload replay, twelve concurrent uploads respecting the ten-file cap, retained scan/operation evidence, audited download and company isolation.
- Shared lifecycle lease: real shared/exclusive contention, pre-BEGIN guard, ordered multi-owner cancellation/unwind, pooled replacement with a different canceled-session PID, and unchanged original records.
- Reader first: the exclusive writer was observed waiting in actual `pg_locks`; after the reader committed its pin, retirement was denied.
- Writer first: a waiting reader acquired its snapshot after retirement committed and could not pin the retired version.
- Stale tracking regression: actor A loaded the original document/version, B retired it, and A's stale context was rejected before fencing. A fresh reader also saw retirement and was denied.
- Exact original-key recovery retained original actor/selection without new evidence; a different current authorized reader could read history. Revoked purpose access was denied.
- Saved/unsaved pin rollback, cancellation and an injected retirement-event save failure preserved the full data/schema/index/history footprint.
- Transaction loss, disposed lease, explicit lock loss, close/reopen and tenant/entity isolation were denied without retained effects.
- Finite runtime SQL permissions denied immutable mutations and schema operations as authored by the proof. Runtime writes and owner fixture setup were separate.

## Evidence boundary and next integration

Authorization and scanning in this private tool are explicitly synthetic fixtures. The test proves real transaction/session/locking/persistence behavior, not production SSO, business approval, malware-scanner assurance or operational RFQ publication. Existing scan/download checks are not a purpose-scoped publication download integration.

No model/schema fields changed in this increment; the actual current-schema/model check passed. No production migration, configuration, flags or deployment changed. No document use-store or retirement authorizer was added to runtime DI.

Next, the RFQ owner must supply exact revision/purpose authorization, complete owner keys before BEGIN, shared-session enlistment with Procurement/Core approvals, current eligibility checks and a purpose-scoped download path. The caller must hold the lease through save and commit/rollback. Those module integration and actual UI/API gates remain required before enabling publication.
