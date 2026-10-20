# PROC-06 P02 — Governed quote-request discovery adapter

Status: source authored; not compiled, migrated, runtime-tested or deployed. Addendum to the governed RFQ draft adapter/runtime plans. Tracking: module-connect#5. No production readiness provider is introduced.

## 1. Feature Identification

Purpose-bound approved-demand picker and owned internal RFQ draft list. Existing `sourcing` route group; no new page, entity or ledger.

## 2. Purpose & Business Outcome

Let the existing sourcing UI discover eligible approved lines and locate its creator's retained drafts without broad request/PO access or manual IDs.

## 3. Scope & Exclusions

Read-only discovery; no reservation, publication, supplier invitation, award, Finance movement or award-to-PO conversion. Candidate count is not an eligible-source count.

## 4. Parent & Related Features

Governed RFQ draft Create/Cancel/detail/recovery, approved PR execution, catalog ownership, shared demand allocation balance.

## 5. Actors & Participants

Current canonical worker/account with `procurement-sourcing-convert` for picker; convert or cancel for own draft list. No automatic role grants.

## 6. Entry Points & Triggers

`GET /api/procurement/v1/sourcing/approved-sources?page=1&pageSize=25&q=...`; `GET .../source-drafts?page=1&pageSize=25&q=...&state=Draft|Cancelled` (state optional).

## 7. Preconditions

Same existing enabled/configured/schema-ready execution gate, canonical current actor and current Core branch permissions as commands. Production schema readiness remains Unavailable. Department selection remains unsupported, not an access grant.

## 8. Workflow

Acquire existing shared Core branch/catalog execution fences before snapshot. Filter source/immutable original company/branch/Core approval proof before count and paging; evaluate paged sources with shared `EligibleSource` (loader + catalog + Finance + pure competitive eligibility). Create uses that same helper under its existing request locks and revalidates all selections. Owned list uses immutable owner/header joins then the existing detailed retained proof; withdrawn current method/policy does not invalidate historical visibility.

## 9. Status / State Transitions

No transition. Picker row `Available`, `FullyAllocated`, or `Unavailable`; owned row `VerifiedRetainedDraft` or `Unavailable`. These are response states, not persisted business statuses.

## 10. Permissions & Data Scope

Current tenant/company, explicit selected branch even for organisation-wide actors, otherwise current allowed branches. Owned draft requires both retained canonical maker worker and subject. Original PR snapshot branch must equal current header before candidate count/search/page; actual scoped approved Core case/outcome/decision and matching source version are joined. No metadata for sources outside this boundary.

## 11. Business Rules

Only competitive, approved, funded demand with complete owned allocation ledger can expose monetary/quantity fields. `CompetitiveSourcingSource.Check` owns eligibility and exact remaining balance. No new reducer or financial threshold. Picker may include unavailable candidates in `candidateTotal`; it never labels that total eligible. A concurrent conversion can invalidate any preview.

## 12. Data Fields & Ownership

PR, source pins, evaluation, methods and allocations remain Procurement owned; Core owns principal/branch/case facts. Picker exposes PR/line IDs, named request/description/UOM/category, expected request version, source line version, exact decimal strings for available quantity and approved estimate, currency, quote requirement and source/method pin identifiers only after full eligibility. Failed proof exposes scoped PR/line IDs, request number and generic blocker only; no owner errors, private policy or money. Owned list exposes own retained draft summary/version/fingerprint, not other actors' drafts.

## 13. Screens & Data Views

Existing LiveSourcing source chooser and list can consume the two read contracts later. No duplicate navigation or replacement page.

## 14. User Experience / Human Behaviour

Show unavailable rows as needing review; do not treat an empty page with nonzero candidateTotal as a successful zero-eligible proof. Selection submits exact quantity strings and expected request CAS. Refresh after conflicts.

## 15. Notifications & Communications

None; GET emits no notifications or business events.

## 16. Documents & Attachments

None added; no document or sensitive original payload is returned.

## 17. Exceptions & Edge Cases

Literal `%`, `_` and backslash searches are escaped with PostgreSQL ILIKE ESCAPE. Default page1/size25; page1..100000, size1..25, trimmed search <=100 chars and no control chars. Optional owned state must exactly equal Draft/Cancelled. Current invalid actor/scope denies globally; individual eligible-candidate proof failure is a safe Unavailable row. Store failures fail the request, not a fabricated zero balance.

## 18. Integrations / APIs / Events / Sandbox

Picker response `{items,candidateTotal,page,pageSize,eligibilityMeaning:'CurrentPreviewOnly',canPublish:false}`; owned response `{items,total,page,pageSize,canPublish:false}`. All quantities/money strings. No async effects. No source row locks for discovery; shared coordinator's serializable read/fences remain. Private runtime fixture/finite role tests are required before enabling.

## 19. Audit / Security / Privacy

`private,no-store`. No raw evidence JSON or original Finance errors. Every new GET checks all three contexts have no tracked changes before returning; coordinator cannot silently persist a preview mutation. Retained header scope is filtered before count/paging; unsupported/tampered eligible evidence cannot reveal financial fields.

## 20. Reports / Dashboards / Metrics

No new KPI. `candidateTotal` counts scoped coarse approved competitive candidates, including rows failing later current proof or with no available quantity. Owned total counts current-scope owned Draft/Cancelled headers satisfying immutable owner joins, including rows needing retained integrity review.

## 21. Configuration & Localization

Existing execution option and governed method/Finance configuration only. No production fake ready provider. ISO/exact number text is transport data; UI may localize display without Number coercion.

## 22. Non-Functional Requirements

Bounded pages and deterministic ordering with ID tie-breaker. Full source allocation history is necessary to prove remaining quantity, not silently truncated. No per-source locks, no schema changes.

## 23. Postconditions

No changed rows, audit/outbox, reservation, Core state or Finance effect. Returned CAS/pins are advisory until Create revalidation.

## 24. Failure & Recovery

GET retry is safe; 503 remains unavailable/unconfirmed, not an empty success. Existing command receipt recovery is unchanged and historical commands do not require fresh catalog/current commercial eligibility.

## 25. Acceptance Criteria

Scoped approved competitive funded lines expose exact remaining quantities; other branches/companies produce neither rows nor counts. Explicit selected branch constrains organisation-wide actors. Historical cancelled drafts remain readable by current authorised owner despite withdrawn commercial policy. Every GET leaves persisted counts/digests unchanged. Create still rejects stale quantities/version and revalidates all owners.

## 26. Test Scenarios

Authored only: mapped TestServer gate covers both GETs with real production-Unavailable provider/no registered stores; bounds/literal wildcard and control/oversized search; selected-vs-membership branch filters; Npgsql query translation/model table proof without opening a connection. Still required actual restricted PG: positive funded competitive candidate, complete remaining ledger after partial RFQs; selected/foreign branch count0, tampered retained/current branch count0, revoked membership fence race; current catalog withdrawal/Finance expiry produces no money; query whitespace/wildcard/exact4dp quantities; owned Draft and Cancelled/revoked method behavior; unchanged DB counts/versions/audit/outbox. No execution evidence claimed.

## 27. Ownership / Dependencies

Existing RFQ adapter owns endpoints. Existing ApprovedDemandSourceLoader + CompetitiveSourcingSource + PurchaseDemandAllocationBalance remain authority helpers. Central root owns migration/readiness/runtime gates. P27 checker codec must later integrate coherently as a whole patch; this slice does not copy new fields or invent checker authority.

## 28. Open Decisions / Assumptions

Purpose read endpoints are gated with governed execution readiness until its exact migration manifest exists. Existing LiveSourcing integration, actual restricted-role tests/browser and schema readiness remain pending. No award-to-PO conversion or same-line direct+competitive transfer contract exists; unsupported paths stay denied. This source increment does not complete sourcing or Procurement.

### Distinct source review correction — wrapped store failures

The first source checkpoint e0818e1 caught InvalidOperationException per row without excluding database causes; EF execution-strategy wrappers could therefore have appeared as a successful Unavailable row. Follow-up reuses the existing ProcurementPostgresFailures helper with underlying DbException/DbUpdateException recognition. Both row catches now exclude storage failures. The route-level handler classifies wrapped storage failures as 503, confirmed 40001/40P01/23505 as 409 after the existing coordinator retry boundary. Known domain evidence failures remain row-level Unavailable. No silent zero/empty business result or owner error payload is produced.

Authored, unrun regressions cover nested PostgreSQL fault wrappers on both actual mapped GETs (no stores/coordinator registered, proving no store effects), transport/DbUpdate/aggregate wrappers denied by the shared row predicate, and ordinary business evidence failures still accepted as row blockers. This does not claim the full database-backed row-positive/zero-write fixture has run.
