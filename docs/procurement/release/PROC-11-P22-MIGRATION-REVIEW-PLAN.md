# PROC-11 P22 additive migration review plan

Companion to the existing 28-section original-unit review plan. Source preparation only; no P22
migration has been generated/applied. Canonical P21 source, designer and snapshot must remain intact.

## Exact intended delta

Four new tables under `procurement`: invoice_unit_review_sources, invoice_unit_review_assignments,
invoice_unit_review_outcomes, invoice_unit_review_receipts. Source/issued/outcome/request/response JSON
are **text**, preserving literal SHA proof; do not silently remap to jsonb. Existing policy-version and
operational evidence tables keep all columns/data. Only ck_operational_policy_identity widens its exact
key allowlist from RECEIVING/INVOICE_MATCHING to include INVOICE_UNIT_REVIEW; remaining predicate
must be unchanged. No money precision, default, legacy history, approval, inventory or Finance table
changes are expected. Core case stays an explicit reference; no cross-schema FK/new Core engine.

Expected scoped ownership FKs (six explicit names):

- fk_inv_unit_source_mapping: tenant/company/mapping/invoice/order/issued, existing six-field owner key.
- fk_inv_unit_source_matching_policy and fk_inv_unit_source_review_policy: tenant/company/policy ID.
- fk_inv_unit_assignment_source and fk_inv_unit_outcome_source: tenant/company/source/CoreCase.
- fk_inv_unit_receipt_source: tenant/company/source.

Expected ordinary unique indexes: ux_inv_unit_source_case, ux_inv_unit_source_mapping,
ux_inv_unit_assignment, ux_inv_unit_outcome_source, ux_inv_unit_command_key. Expected explicit scoped
source+case AK: ak_inv_unit_source_case. Inspect base scoped PK/AK and generated supporting FK indexes;
no mutable business/operational CAS field belongs to a principal key. No deferred manual FK overlay
is needed because all new principal bindings are immutable.

Four explicit null-safe CHECKs: ck_inv_unit_source, ck_inv_unit_assignment, ck_inv_unit_outcome,
ck_inv_unit_receipt. Review actual NOT NULL/type/length semantics as well as CHECKs; SQL UNKNOWN
must not admit a missing required evidence hash or lineage. Freeze actual pg_get_constraintdef and
index definitions after genuine PG17 apply; readiness constants must not be guessed from strings.

## Down source candidate and preservation

[Candidate guard](sql/P22-original-unit-review-down-guard.candidate.sql) is proposed as the FIRST Down
operation within the same ordinary EF transaction. It locks all new tables and existing policy/evidence/
audit/outbox owners before denying any retained new rows, third-key policies/identity, exact new domain
audit/outbox producer types and explicit PolicyVersion audit JSON `PolicyKey`. An existing owned Core
case with sourceType invoice_unit_review also vetoes; absence of the separate Core schema is handled
without creating it. The guard must precede all CHECK/drop operations. Return SQLSTATE55000 with
exact message `Retained original-invoice unit review prevents destructive downgrade.`.

This is not a guarantee of concurrent live downgrade availability. Multi-owner DDL locks may conflict
with live writers; a PostgreSQL abort must roll back the whole Down. Operational rollback requires a
controlled window; never disable constraints/disconnect/delete retained evidence to force success.

After an unused guard passes, remove only new child tables before source, restore the exact prior
two-key CHECK, and preserve all P21/legacy data. Generic operational evidence audit rows from existing
RECEIVING/INVOICE_MATCHING must not cause false veto. Generic PolicyVersion property-name-only audit
arrays alone do not identify a policy key; actual new policy routes also emit explicit audit objects with
PascalCase PolicyKey. No claim is made that owner-deleted arbitrary history can be reconstructed.

## Required central verification sequence

1. Source compile/focused host/tool pass, then review model and generate ONE P22 from authoritative
   canonical P21 snapshot. Inspect actual migration/designer/snapshot; reject unrelated renames/drops.
2. Add reviewed guarded Down; build Infrastructure; model pending check false; generate full/up/down/
   idempotent SQL with preserved byte digests. No production SQL.
3. Fresh private PG17 baseline→P22 Up twice; cloned genuinely used P21→P22 Up twice preserves all old
   column values/schema/history; unused Down→Up exact; used-source, third-key-policy-only, new-type-
   audit-only and Core-case-only Down denials preserve complete data/schema/history.
4. Run genuine --unit-reviews finite role positive/rollback/orphan UPDATE checks, scoped HTTP/source/
   document/current-worker replay negatives, named raw FK/CHECK/unique/ACL/concurrency denial tests.
5. Freeze actual migration ID + four CHECK and three-key operational CHECK catalog outputs in owning
   readiness and adapt source tests from uncaptured=false to exact real manifest. Preserve exact old
   two-key readiness before additive migration, never accept arbitrary broader CHECK.
6. Rebuild/drift and actual provider positive/negative role checks, then later actual UI browser/full-host
   compatibility. Default execution stays off through these gates; no Finance owner completeness added.
