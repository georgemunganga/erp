# Next Procurement CRUD integration: source preparation

**Ordering correction, 22 October 2026:** The original P22/P23 numbering below records the earlier
source preparation. P22 now belongs to genuinely generated Core principal ownership; original-unit
review follows as P23, recurring planning as P24. Current recurring principal contract and gates are
in [PROC-07A P24 plan](PROC-07A-P24-CORE-PRINCIPAL-PLAN.md). Do not generate either future delta from
a combined model before its prerequisite migration has been separately generated and reviewed.

Status: **source integration only; not ready for deployment**. No SDK, browser, database,
production or migration-generation job was run for this integration. This document does not
extend the verification claims of the separately frozen P21 release candidate.

## Integrated source and boundaries

| Input | Exact commit | Purpose |
| --- | --- | --- |
| Current release foundation | `68b91c0` | Canonical P21, strict HRM snapshot, receiving lookups and published-main reconciliation |
| Original invoice unit review | `f4af7d4` | P22 review source, shared Core approvals/documents, finite-role verification preparation |
| Recurring planning and actual verifier | `f19f158` | Owner-scoped recurring purchase CRUD, contract invariant and explicit private verifier opt-in |

Prepared branch: `codex/procurement-next-crud-integration`.

Two mechanical conflicts were resolved by keeping both additions:

- Policy lookup routes retain receiving lookups and invoice-unit reviewer lookups.
- The existing CRUD verifier retains receiving lookup checks, then invokes recurring checks
  only when the explicit recurring opt-in is set.

No UI source was merged into this backend repository. Recurring UI commit `cd3802b` remains a
separate UI integration candidate. Planning does not generate invoices, AP postings or payments.
No new employee, supplier master, approval engine or ledger has been introduced.

## Preservation checks performed

All **135 existing migration, designer and model snapshot files** have identical Git blob IDs
against `68b91c0`, including canonical P21 and the repaired strict HRM snapshot. No existing
migration was rewritten. Source contains no unresolved C# merge markers. The private launcher
parses as Python. `git diff --check` passes. These are source checks, not C# compile or runtime
acceptance evidence.

## Migration ordering: required before this candidate can run

1. Compile and test **foundation plus P22 only**, excluding the recurring model. Generate genuine
   P22 from the authoritative P21 snapshot in that isolated source tree. Follow
   [the P22 migration review plan](PROC-11-P22-MIGRATION-REVIEW-PLAN.md), including its first-operation
   retained-evidence Down veto. Review the generated migration, designer and snapshot before use.
2. Apply P22 only to private fresh and genuinely used P21 clone databases, twice. Verify full old
   data/catalog preservation, unused Down/Up and all retained-evidence Down denials. Capture
   actual PostgreSQL CHECK definitions and the real migration ID in P22's readiness owner;
   its current manifest constants deliberately remain empty and fail closed.
3. Merge the reviewed P22 migration/snapshot and exact readiness evidence into this combined
   source. **Do not generate P22 from this combined model:** it already contains recurring plans.
4. Generate genuine P23 from the resulting P22 snapshot. The intended recurring delta is the
   owned `procurement.recurring_purchase_commitments` table, its lifecycle/scoped fields,
   exact amount precision, schedule CHECK, unique number and ownership indexes, and same-scope
   supplier/optional-contract references. Inspect the actual generated delta; do not invent
   migration history or copy a candidate model into the snapshot by hand.
5. Give P23 a first-operation retained-data veto before destructive Down. Preserve closed as well
   as draft plans and consequential audit evidence. Prove unused Down/Up and used-data denial
   leave complete data, catalog and history unchanged. Keep all P01–P22 artifacts immutable.
6. Run full current-model drift, fresh and retained-chain tests with the genuine final migration
   sequence. The current combined model intentionally differs from the P21 snapshot; normal
   pending-model/migration guards must remain in place until these migrations exist and apply.

## Contracts and verification that still require work

- `ProcurementCrudHttpProbe` currently upgrades only to canonical P21 and requires exactly
  **39** validated CHECKs. Keep its original P19/P20 and canonical ownership assertions, then
  explicitly apply and verify genuine P22/P23 before current HTTP operations. Extend the final
  inventory with the captured P22/P23 catalog; do not remove or weaken the 39 baseline checks.
- The old operational policy readiness narrowly allows only the exact known pending P22 ID,
  never arbitrary pending migrations. Pending P23 must still deny readiness. After complete
  installation, the captured P22 three-key identity CHECK must match exactly; no global pending
  migration suppression is acceptable.
- Recurring adds a query to contract updates to prevent changing a supplier while any recurring
  plan, including a closed plan, references the contract. That query requires genuine P23;
  deploying the merged contract writer against P21/P22 alone is unsafe.
- Verify recurring supplier references are same tenant/company; own draft suppliers or genuinely
  approved/orderable suppliers only. Optional contracts must be owned, same supplier/company
  and not cancelled. Validate worker/subject/current branch scope, foreign owner denial, stale
  version, invalid exact amount/schedule, null draft, closed edit and default company currency.
- Verify real concurrent recurring create versus contract supplier reparent using the prepared
  independent database lock barrier and observed simultaneous PostgreSQL lock waits. The
  verifier must fail if genuine overlap is not observed; do not substitute task scheduling
  or application mocks as race evidence. Confirm protected downstream owners before and after.
- Compile focused host tests and both verification tools, then run provider-backed tests and
  actual finite-role P22 checks. A prior source branch's unit results do not prove this merge.
- Merge recurring UI into the current UI candidate with the exact typed API contract:
  `/recurring-purchases`, owner-scoped list/detail, create/update and reasoned close, exact decimal
  strings, current company currency, canonical permissions and `recurringPurchaseDraft` capability.
  Preserve existing UI capabilities when combining its older capability parser with newer UI.
- Test actual create, reload, update, reload, close, read-only closed detail, stale/foreign denial,
  unknown save recovery and module/company switching with Playwright. Use the shared floating
  dropdown interactions. Unknown writes must not be silently retried or represented as saved.

Release source, migrations, browser evidence, runtime-role provisioning and deployment remain
separate gates. No live workflow, account grant, production role or feature flag was changed here.

## Recurring unchanged-save CAS correction

Source follow-up makes every accepted recurring PUT consume exactly one version, including an
unchanged draft. The route sets the tracked version to its checked successor and explicitly marks
that concurrency property modified before constructing the `PlanUpdated` audit. The existing
`PrepareChanges` stamps `OriginalValue + 1`, so it does not increment that precomputed value twice.
The original concurrency token remains the update predicate; a stale replay still returns 409.

Added mapped HTTP regression checks unchanged PUT, independent GET, before/after audit versions,
and stale replay without additional audit. Extended the actual PostgreSQL verifier with unchanged
PUT/reload/replay footprint checks and exact audit pairs 1→2 and 2→3. These new tests are authored
but **not run**; the CPU slot and SDK/runtime gates remain with the release owner. No schema or
migration artifact changed for this correction.
