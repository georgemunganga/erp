# PROC-FIN-01-P02 — Retained Finance checker identity

**Status:** Proposed bounded implementation contract; not implemented or verified.  
**Baseline:** `3613559` (current worker-independence correction, pending parent verification).  
**Hierarchy:** Procurement → Budget Integration → Finance Review Evidence → Record / verify retained checker identity.  
**Related:** [Finance reservation contract](PROC-FIN-01-BUDGET-RESERVATION-CONTRACT.md), PROC-03, GW-ENG-STD-001.  
**Readiness:** Technical implementation boundary defined below. Production activation still requires existing Finance, access, policy, reconciliation and release gates; this document approves no business policy.

## 1. Feature Identification

Close one attribution gap in existing immutable `BudgetCheckSnapshot` evidence. Retain the canonical Core worker who recorded the Finance check, alongside the existing login subject. Do not add a Finance ledger or another identity master.

## 2. Purpose & Business Outcome

Two logins can represent one worker, and a login can later be reassigned. Current canonical lookup prevents present-day requester/maker aliases from confirming funding; it cannot establish which worker originally recorded an old check. Example: subject S records while mapped to A, then becomes mapped to B. Looking up S today proves B, not A. Historical Finance checker attribution must survive that change without silently reinterpreting the record.

## 3. Scope & Exclusions

Scope: new manual Finance check capture, retained worker binding, new approval pin verification, exact replay/recovery and safe legacy compatibility. Excludes external Finance signer identity, reservation completeness, AP postings, automated Finance adapters, historical identity reconstruction and new identity administration. A worker binding proves an ERP recording operator, not the person who made an external Finance decision.

## 4. Parent & Related Features

Use existing Budget Integration, PR approval pinning, controlled manual Finance policy and Finance release contracts. Relevant source at this baseline:

- Domain: `backend/procurement-api/src/Mightyfin.Erp.Procurement.Domain/Records.cs` (`BudgetCheckSnapshot`).
- Mapping/append-only guard: `backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Data/ProcurementDbContext.cs`.
- Capture: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementBudgetRoutes.cs` (`Record`).
- Current eligibility: `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementApprovalExecutionOptions.cs` (`ProcurementApprovalFinanceEligibility.CheckAsync`).
- Core resolution/current independence: `ProcurementAuthorityIdentity.cs` and `ProcurementFinanceWorkerIndependence.cs` in that API directory.
- Frozen evidence: `backend/procurement-api/src/Mightyfin.Erp.Procurement.Application/PurchaseRequestApprovalPinning.cs` (`FinanceSnapshot`, `Pin`, `VerifyEvidence`).

The snapshot currently stores `CheckedBySubjectId`, without a checked-worker field. `CheckAsync` resolves that subject now. Existing schema-2 submission maker/requester worker evidence does not supply the missing Finance worker.

## 5. Actors & Participants

Current independent Finance recorder; requester; retained request maker; current approval actor; Core identity/access owner; Finance business owner; migration/recovery operator. Historical recorder and current consumer are separate concepts. Do not infer either from a display name or role alone.

## 6. Entry Points & Triggers

Extend existing record-budget-check command and authoritative approval eligibility checks. Existing queue/history/detail surfaces remain scoped reads. No client may set trusted worker IDs, schema versions or digests.

## 7. Preconditions

Require current canonical active recorder, existing server budget-check permission and record scope, supported independent requester/maker evidence, submitted business version/CAS, existing approved manual-control policy where consumption requires it, exact amount/currency/reservation checks and current transaction guards. Unknown identity or ambiguous mappings block; they are not an independent worker.

## 8. Workflow

1. Acquire existing locks/transaction and resolve canonical actor through enlisted Core context.
2. Validate current permission, source scope/version and worker independence before recording or replay authorization.
3. Allocate snapshot ID; stamp server `CheckedAt` once; construct financial tuple and retained identity binding before tracking the new immutable snapshot.
4. Save snapshot and existing audit atomically; return original persisted response. Never stamp a new checker onto a prior reference.
5. For new approval evidence, validate retained binding, current verifier mapping and all current eligibility requirements; freeze the new versioned Finance representation.
6. During later use, compare current verifier worker to retained checker worker. Reassignment blocks eligibility; it cannot change historical attribution. Current verifier inactivity/permission revocation continues to block operational eligibility under existing policy.

Historic cancellation/release resolution reads original immutable evidence under its existing authorization; it does not require reinterpreting an offboarded checker as a new person or certify a reservation released.

## 9. Status / State Transitions

No new financial status lifecycle. Identity evidence is `LegacyUnbound` (version 0), `RetainedBound` (version 1), or unsupported/corrupt (blocked). Bound evidence is append-only; no transition updates a legacy snapshot into a bound one. Replacement is a new actual check with a new reference and existing business-version/pin controls.

## 10. Permissions & Data Scope

Reuse current budget-check/approval permissions and tenant/company/branch source checks. Core owns canonical account-to-worker mapping and current grants. No automatic grants, default Finance authority or cross-Core foreign key. A retained ID is attribution, not permission. Current branch fence does not claim to fence every Core account/worker reassignment writer: capture linearizes identity at the authorized Core read within the existing transaction; stronger wall-clock reassignment semantics would require a separately reviewed Core-owned writer fence.

## 11. Business Rules

- Newly captured checker worker must equal the exact current canonical recorder mapping and differ from requester and retained maker.
- A valid old digest never permits a newly reassigned worker to act as the old checker.
- `CheckedAt` is already server-set in `Record`; use that normalized database timestamp for capture attribution. Do not claim it proves an external signer or older external decision time.
- Current manual-policy validity, freshness, verifier activity and permissions remain independent requirements. Retained identity does not exempt offboarded checkers from current operational eligibility.
- Schema 0/unknown/tampered provenance cannot authorize a new approval opening or positive financial decision after this control is enabled. Historical bytes and supported non-positive recovery remain available.
- No copying current worker mappings into historical rows, no backfilled success, and no assumed reservation completeness.

## 12. Data Fields & Ownership

**Minimum proposed additive schema** (final names subject to source review):

| Owned record | Field | Meaning / compatibility |
|---|---|---|
| BudgetCheckSnapshot | CheckerIdentitySchemaVersion, integer, default 0 | 0 retains old unbound evidence; new captures require 1 |
| BudgetCheckSnapshot | CheckedByWorkerId, nullable UUID | Core worker reference observed at server capture; nonempty for v1 |
| BudgetCheckSnapshot | CheckerIdentitySha256, nullable 64-character text | Exact v1 canonical binding digest; nonnull for v1 |
| PurchaseRequestApprovalSubmission | FinanceIdentitySchemaVersion, integer, default 0 | Selects legacy Finance serialization (0) or new bound representation (1), without redefining existing maker/requester SnapshotSchemaVersion 2 |

The v1 canonical binding must include schema discriminator, snapshot ID, tenant/entity, PR ID, submitted business version, checked subject, checked worker, normalized server CheckedAt and a digest of the **existing exact FinanceSnapshot financial/evidence tuple**. That tuple includes amount, currency, status, references and reservation fields; retain exact decimal and timestamp conventions. Construct it without recursion into the new digest. A hash detects inconsistent owned evidence; it is not a signature or external Finance attestation.

Keep stable scoped IDs as EF relationship keys. Do not put mutable binding fields in EF principal keys. Retain existing scoped FK to PR and unique Finance reference. Do not add employee/account master tables or cross-module identity FKs. New v1 checks explicitly require every nullable field `IS NOT NULL`, worker nonempty and valid lowercase SHA shape; PostgreSQL CHECK UNKNOWN must not admit partial v1 evidence. Application guards recompute the binding and reject unsupported versions and all modifications/deletes.

## 13. Screens & Data Views

Reuse existing Finance budget confirmation and request history UI. Add plain read-only provenance state, for example “Recorded by an independent Finance worker” or “Older evidence needs a new Finance review.” Do not expose credentials, other workers' private profile fields or an action that appears to backfill historical identity.

## 14. User Experience / Human Behaviour

When current mapping differs from the recorded checker, explain that funding evidence needs a fresh independent review. Preserve entered draft data and show the supported recovery step. Never label unbound evidence fraudulent or automatically change its operator. No UI redesign or duplicate Finance form is required.

## 15. Notifications & Communications

Use existing audit/outbox paths only where existing workflow specifies them. No new delivery claim; durable publication and actual delivery are distinct. Include a stable support error reference without sensitive evidence contents.

## 16. Documents & Attachments

Existing Finance evidence references remain references under existing owner/access contracts. Identity digest binds their retained exact values, not unverified uploaded contents. No fabricated document scan, external signature or Finance API confirmation.

## 17. Exceptions & Edge Cases

Multiple active accounts for one worker remain aliases; exclude that worker from self-review. Missing canonical mapping, stale business version, changed scope, zero worker, unsupported identity version or mismatched digest blocks new use. Offboarding preserves historical attribution but current policy may require a new review. Existing on-behalf/import records remain unsupported without authoritative maker evidence. No inference maker=requester where source provenance differs.

## 18. Integrations / APIs / Events / Sandbox

Extend existing capture/result contracts additively; trusted identity fields are response-only. Keep current budget status/amount/ref semantics. Review all Finance consumers, including PR open/approve/replay, PO funding/submit/issue, RFQ create/restore, source cancellation and receiving historic source readers. No new endpoint is necessary for identity backfill. Sandbox proof uses actual mapped APIs, Core fixture accounts and disposable PostgreSQL; no production writes.

## 19. Audit / Security / Privacy

Audit capture with recording principal, retained worker, schema and digest; avoid credentials/raw external evidence in logs. Pin scope and exact tuple prevent row transplantation. Re-evaluate current access on replay; original-reference retrieval must never substitute current actor as historic checker. Keep existing restricted runtime role boundaries, with only required new-column access and no Core writes. Identity reads do not establish external Finance authorization.

## 20. Reports / Dashboards / Metrics

Report counts of bound, legacy-unbound and invalid evidence within authorized scope. Report remediation backlog separately from funded totals. Do not sum legacy records as newly verified independent funding. No new financial ledger or analytics balance.

## 21. Configuration & Localization

No guessed business limits or new authority defaults. Existing approved manual-policy settings remain server-owned. Localize plain blocked/recovery wording. Deployment needs an explicit legacy-evidence impact assessment; default-disabled workflow capability is not enabled merely because columns exist.

## 22. Non-Functional Requirements

One current canonical lookup per capture/eligibility as supported by existing adapters; bounded queries. Preserve exact decimals and normalized timestamp round-trip through PostgreSQL. Atomically persist binding with the financial tuple. No arbitrary JSON or role membership supplied by the browser can create authority. Reads must remain side-effect-free.

## 23. Postconditions

A new snapshot immutably identifies the worker observed recording that exact scoped Finance tuple. Its new approval pin retains the selected representation. Current permissions and Finance truth remain separate. Failed verification creates no new snapshot, source mutation, audit, outbox or commitment.

## 24. Failure & Recovery

Migration is additive: default-zero discriminators and nullable binding columns preserve every old row without rewriting identities or Finance evidence. No amount/precision changes. Require schema readiness before enabling the new writer/positive consumer path. Down begins with locks and stops on any nondefault new identity metadata, new-format pinned evidence or corresponding audit/outbox; only an unused extension may reverse. Missing/null/partial metadata is not silently zeroed.

**Serialization compatibility:** leave the existing FinanceSnapshot encoder byte-for-byte unchanged for discriminator 0. Add an explicit encoder/verification branch for discriminator 1 containing legacy tuple plus binding fields; reject unsupported versions. Keep existing PR maker/requester schema-2 semantics unchanged. Golden tests must preserve existing request/submission/issued/receiving/RFQ retained hashes. Historical schema-0 verification may remain available only for original audit/cancel/release readers; it must not accidentally bypass new positive eligibility.

**Remediation:** never update old append-only snapshots. Obtain a genuinely new independent Finance check under a new reference after current validation. If an existing case pins the old snapshot, do not swap it in place: use the already-supported controlled return/release/revise/re-submit/re-approve route. Where that route cannot yet supply authoritative release or review, report a blocked operator case. A separate amendment/reconciliation facility is future work, not an assumed migration backfill or a client `verified=true` escape hatch.

## 25. Acceptance Criteria

1. Rebinding a checker login from A to B cannot reinterpret a v1 snapshot or satisfy new eligibility with B, even if B currently has Finance permissions.
2. A current alias of requester or retained maker cannot record, replay-authorize or provide eligible funding.
3. The retained checker remains attributable after offboarding; historical audit/release behavior preserves original evidence, while new positive eligibility still applies current policy.
4. New v1 partial/null/hash-mismatched/foreign snapshots fail before effects; legacy 0 remains readable but cannot authorize a new positive decision.
5. Old serialized Finance hashes and source restore/release paths remain compatible; no guessed historical worker IDs or external Finance success.

## 26. Test Scenarios

Author and centrally run pure/model, mapped HTTP, actual restricted-role PostgreSQL and relevant browser regressions:

- Capture independent worker A; reload actual PostgreSQL timestamps/decimals; verify exact identity digest and frozen new representation.
- Rebind same subject to B with valid current role; deny original evidence eligibility and unauthorized replay, preserve original snapshot/worker/hash/response. Also test B equals maker/requester and independent B.
- Two accounts on requester or separate retained maker worker; denial leaves all source/snapshot/audit/outbox counts unchanged.
- Offboard original maker and original checker separately: retained audit attribution survives; current-checker operational eligibility remains blocked where existing policy requires activity; existing authorized terminal release uses original proof without inventing funding status.
- Foreign tenant/entity/request/version/snapshot ID, altered worker/digest/time/financial tuple, zero/NULL fields, schema 0/unknown: fail closed. Raw SQL NULL negatives expect named CHECK violation and full rollback.
- Missing/ambiguous canonical account and current permission revoked; current API/URL scope checked independently.
- Reference replay under current access; different payload conflict; concurrent Finance capture/source CAS; concurrent identity reassignment meets documented snapshot linearization, not an unfenced wall-clock guarantee.
- Golden old FinanceSnapshot/submission/issued/receiving/RFQ restore bytes unchanged; mixed legacy/new pins select the correct serializer; schema readiness fails on missing extension.
- Additive Up twice on populated legacy fixtures, unused Down/Up preservation, used/partial metadata Down denial with data/history equality; no backfill.

All tests here are required future evidence, not executed results.

## 27. Ownership / Dependencies

Procurement owns snapshot/binding and consumer guards; Core owns canonical identity and any future reassignment fence; Finance owns verification policy/reservation/reconciliation. Parent integration owner schedules migration, independent review and actual runtime gates. Existing docs/tests are reused. The current `3613559` correction is a prerequisite, not completion of this retained-checker increment.

**Bounded coding sequence:** (A) domain fields + canonical binder/version-dispatched codecs + pure/golden tests; (B) map/append-only/CHECK guards + source migration plan and metadata tests; (C) real capture/current eligibility and replay wiring across consumers, legacy-positive denial and historic-reader separation; (D) parent-generated migration/readiness/Down with independent review and disposable preservation rehearsal; (E) actual mapped/restricted-role/approval-source regression and existing UI blocked-state checks. Only then reconsider enablement under the existing broader gates.

## 28. Open Decisions / Assumptions

This plan preserves the current requirement that a Finance verifier remains active/currently permitted for new positive use; relaxing it requires an explicitly approved future policy and authoritative historical permission evidence. Account reassignment is not assumed impossible. Exact stronger reassignment fencing, signed automated Finance responses and authoritative reconciliation remain separate Core/Finance contracts. Legacy remediation may remain operationally blocked until real owners can confirm required release and replacement funding; no completeness is invented to remove that blocker.

## Step A source checkpoint — canonical binding and compatibility only

Authored additive domain fields, `FinanceCheckerIdentityEvidence` and `FinanceSnapshotCodec`, with pure/golden regressions. New capture construction requires an unstamped new snapshot, exact subject and nonzero canonical worker supplied by the server caller; it does not perform Core lookups or authorize backfill. Current-canonical matching compares the retained original worker as well as subject/scope/version. The binding hashes the original explicit financial tuple, with normalized UTC PostgreSQL microsecond timestamp and exact decimal text; no recursive digest.

PR pinning dispatches Finance representation using the separate discriminator, leaving maker/requester schema 2 unchanged. Zero/null new domain fields are omitted from ordinary JSON serialization to preserve unchanged legacy object representations as well as the explicit original Finance bytes. Legacy verification remains available for original historic source use; Step C must explicitly deny it at new positive eligibility boundaries. Existing internal legacy encoder compatibility is retained for integration branches.

This checkpoint has **no EF mapping, migration, HTTP capture/eligibility wiring or capability activation**. New model fields require Step B before runtime use. Tests are authored, not executed: legacy literal JSON/SHA, full-object absent-field compatibility, timestamp/scale round-trip, binding tamper/partial/unknown schemas, canonical worker reassignment, old terminal source identity/Finance restoration, new Finance pin dispatch and downgrade denial. Current activity, permission, worker independence and external Finance truth remain caller/owner gates. No SDK, database or browser verification was run for this checkpoint.

## Step B source checkpoint — mapping, write guards and migration contract

**Status:** explicit model/write-guard source and authored tests only; no migration generated/applied, no readiness provider or route wiring implemented. This branch remains isolated from runtime integration until Step C and parent migration/gates are complete. The Step A fields are convention-discoverable by EF; do not merge only its domain files into a running host without this complete migration/consumer sequence.

`ProcurementFinanceCheckerIdentityMapping.cs` explicitly maps the three BudgetCheckSnapshot fields and the separate submission discriminator. The two integer discriminators are required/default 0; nullable worker uses ValueGeneratedNever; digest is nullable varchar(64). `ck_budget_checker_identity` accepts only v0 with BOTH identity fields NULL, or v1 with explicit nonnull/nonzero worker AND explicit nonnull lowercase 64-character hash. `ck_pr_finance_identity_version` accepts 0, or 1 with the existing maker/requester schema 2. No new key, unique index, table or FK is introduced; existing scoped source-version FK/key tuples and numeric precision are unchanged. Cross-row digest/scope correctness is enforced by the application guard, not falsely attributed to SQL CHECK.

Before any generic audit is added, SaveChanges rejects updates/deletes to Finance snapshots, incomplete/unknown/digest-mismatched added v1 records, and records whose checked subject differs from the authenticated recording subject. Added v1 pins must bind the exact owned Finance row, PR/submitted version and exact encoded JSON/hash. A new v0 pin cannot downgrade an existing bound Finance parent. Existing empty v0 graphs remain compatible; this is not permission for new positive eligibility, which Step C must enforce. Current Core account mapping and independence are still server application guards; storage hashes cannot establish those authorities.

### Parent-generated additive migration: exact reviewed delta

1. Verify the latest central model/snapshot first; choose the next migration ID centrally, not a guessed P-number.
2. Add `budget_check_snapshots.checker_identity_schema_version integer NOT NULL DEFAULT 0`, `checked_by_worker_id uuid NULL`, `checker_identity_sha256 varchar(64) NULL`; add `purchase_request_approval_submissions.finance_identity_schema_version integer NOT NULL DEFAULT 0`. Confirm that existing name remains unchanged in generated SQL.
3. Add exactly the two named CHECKs above; preserve all prior table names, scoped keys, FKs, indexes, precision, exact stored JSON/hash bytes and audit/history. No UPDATE/backfill, new table, default worker/digest, or identity lookup in migration.
4. Review generated Up/Down/Designer/Snapshot and compiled idempotent SQL. Up on populated legacy evidence must retain each row's original values and assign only discriminator 0 / NULL new fields.
5. Down begins with the locking/use veto below; then removes only these two CHECKs/four columns. No COALESCE, default identity, or old evidence JSON/hash rewrite. An unused reversal leaves all legacy Finance/submission history intact.

**Physical name verification:** `ConfigureBase<PurchaseRequestApprovalSubmission>` currently maps `purchase_request_approval_submissions`; the SQL below uses that existing name. Confirm generated SQL preserves it; do not create a second submissions table.

### Used-evidence Down veto (seal literal against actual generated names)

Lock the Finance, submission, audit and outbox tables **before** testing usage, in a deterministic order. Fail with `FinanceCheckerIdentityRollbackWouldLoseEvidence` if either snapshot has any nondefault identity metadata (including partial metadata), any submission uses a nonzero Finance discriminator, or retained audit/outbox identifies this increment after an owner-only selective evidence purge. Conservative audit matching may stop rollback after any post-upgrade capture that audited the new field names, even if that capture remained unbound; forward-fix is acceptable. Never delete audit/outbox to make rollback pass.

```sql
-- Source plan only: parent must review/seal this literal with the generated migration.
LOCK TABLE procurement.budget_check_snapshots, procurement.purchase_request_approval_submissions,
           procurement.audit_events, procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
DO $guard$
BEGIN
  IF EXISTS (SELECT 1 FROM procurement.budget_check_snapshots
      WHERE checker_identity_schema_version <> 0 OR checked_by_worker_id IS NOT NULL
         OR checker_identity_sha256 IS NOT NULL)
    OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_submissions
      WHERE finance_identity_schema_version <> 0)
    OR EXISTS (SELECT 1 FROM procurement.audit_events
      WHERE record_type IN ('BudgetCheckSnapshot','PurchaseRequestApprovalSubmission')
        AND (COALESCE(changed_fields_json::text,'') LIKE '%"CheckerIdentitySchemaVersion"%'
          OR COALESCE(changed_fields_json::text,'') LIKE '%"CheckedByWorkerId"%'
          OR COALESCE(changed_fields_json::text,'') LIKE '%"CheckerIdentitySha256"%'
          OR COALESCE(changed_fields_json::text,'') LIKE '%"FinanceIdentitySchemaVersion"%'))
    OR EXISTS (SELECT 1 FROM procurement.outbox_events
      WHERE event_type = 'procurement.finance_checker_identity.recorded.v1'
        OR (aggregate_type IN ('BudgetCheckSnapshot','PurchaseRequestApprovalSubmission')
          AND (payload_json::text LIKE '%CheckerIdentitySchemaVersion%'
            OR payload_json::text LIKE '%CheckedByWorkerId%'
            OR payload_json::text LIKE '%CheckerIdentitySha256%'
            OR payload_json::text LIKE '%FinanceIdentitySchemaVersion%')))
  THEN RAISE EXCEPTION 'FinanceCheckerIdentityRollbackWouldLoseEvidence'; END IF;
END $guard$;
```

No new event is implemented here. Step C must either use its reviewed exact event name/payload names or amend this conservative veto before migration sealing; this example does not claim a notification publisher exists. Literal Up/Down must be reviewed against actual snake-case generated columns and the complete Step C audit/outbox inventory.

### Readiness and required gates before integration

- Existing migration-history/pending-model checks must include the centrally generated increment. Positive Finance capture/eligibility readiness additionally requires the four exact column types/default/nullability, both named **validated** CHECK definitions (not merely lookalike names), existing scoped source-version FK, and runtime SELECT/INSERT privileges on the new fields. Use read-only catalog/schema probes; do not test readiness by writing.
- Missing columns, partial migration, invalid/removed/wrong-shape CHECK, inaccessible metadata/columns or pending model means unavailable positive operations. Readiness is not authority, external Finance completeness or retroactive independent-checker evidence.
- Authored context tests cover valid new save/reload/pin + field-name-only audit; invalid v1/partial0/unknown/valid-digest-wrong-recording-subject with zero effects; malformed/wrong-source-version/downgraded pins; immutable old/new snapshots. Npgsql design-model assertions cover NULL-safe predicates, defaults, nullable UUID non-generation, exact digest length, unchanged 18,4 amount and existing key/FK shape. None were executed for this source checkpoint.
- Parent runtime tests must prove raw PostgreSQL rejection of each v1 NULL/zero/uppercase-hash and v0 partial case, plus unsupported versions, while positive legacy0 and exact v1 save pass. Rehearse populated legacy Up twice; unused Down/Up; used metadata and audit-only veto with unchanged rows/history/schema on denial. Then run Step C mapped/current-identity/replay and real restricted-role API gates. No old row is promoted during these rehearsals.
