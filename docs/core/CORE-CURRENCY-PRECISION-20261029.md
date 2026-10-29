# Core currency precision: M6D shared prerequisite

Proposed child-feature specification using the mandated 28-section framework. Existing M6C product source remains frozen.

## 1. Feature Identification

Feature reference: CORE-CURRENCY-PRECISION, shared prerequisite for M6D. Owner: ERP Core. Discovery baseline: ERP 0f656d6316e39063eb09da45210db4b142717ecf and Procurement UI 56fbd3b. Status: proposed design; implementation is not claimed.

## 2. Purpose & Business Outcome

Provide an authoritative, versioned currency calculation reference shared by ERP
modules. Preserve operational quantity/rate precision separately from currency
amount precision. This is a prerequisite for [new invoice calculation v2](../procurement/gap-closure/M6D-NEW-INVOICE-CURRENCY-CALCULATION-V2-20261029.md).

## 3. Scope & Exclusions

Core owns currency codes, enabled reference versions, minor units and the approved
rounding convention. Finance retains exchange rates, tax rules, ledgers, posting
and cash settlement. Procurement consumes Core's reference; no Finance module is
required to calculate a manually entered invoice.

Minor units mean monetary decimal places. Quantity/rate input precision, display
format, exchange rate and the smallest circulating coin are separate concepts.
Do not infer precision from `Intl.NumberFormat`, a symbol, number-format text,
payment limits, or a coin denomination. Cash rounding is outside this feature.

## 4. Parent & Related Features

Related contracts are Core Organization/Settings, shared authorization/audit and M6D new-invoice calculation. Existing M6C and Finance ownership remain unchanged.

`LegalEntity.Currency` in `backend/hrm-api/src/Mightyfin.Erp.Hrm.Domain/Entities/Organization.cs:15`
is persisted and defaults to `ZMW`. `OrgConfigServiceImpl` creates/updates that code.
`ProcurementCurrencyDefaults.ForSelectedCompanyAsync` reads the selected active
Core company. Reuse these records and defaults; never hard-code the organization
name, tenant, company or a Procurement-specific default currency.

## 5. Actors & Participants

Core settings administrators publish/retire references. Authorized module actors consume purpose-scoped snapshots, using the selected company default. Finance/business owners confirm monetary conventions without a new ledger dependency. A Procurement maker does not gain Core administrator authority.

## 6. Entry Points & Triggers

`GET /api/core/v1/currency-precisions` lists permitted tenant references for Core
administration. A purpose-scoped business read resolves a requested code/version
and the current company's default; its final route is part of the implementation
contract. It exposes calculation metadata, not organization PII or Finance data.
The in-process typed reader is authoritative for Procurement writes.

Use a versioned Core configuration command under `/api/core/v1/currency-precisions`
to publish a new revision and a separate retirement command with expected version
and reason. Require idempotency keys and immutable recorded responses. A stale
revision returns 409; invalid scale/mode/code returns 422. Do not reuse a
Procurement policy key or its maker/checker engine for Core reference ownership.

## 7. Preconditions

Before new-use resolution, require reviewed Core schema/readiness, current tenant/company access, explicit permissions and exactly one configured effective reference.

Provision the reviewed ZMW/2/ToEven reference through the normal Core API after
schema readiness and before enabling v2 intake. Retain its audit/recovery proof.
The schema migration does not seed tenant policies or silently rewrite company
currency. Other currencies require explicit configured precision before new v2
use. Bundled reference metadata may assist entry but is not an invisible fallback.

## 8. Workflow

1. Resolve the existing Core company currency or explicit transaction currency.
2. Review provenance, minor units, mode and effectivity, then publish through the normal keyed configuration command.
3. Preview resolves an immutable typed snapshot.
4. Capture revalidates and pins that version in the owning transaction; stale preview asks for refresh.
5. History uses consumed snapshots; later settings revisions never rewrite them.

When Core and Procurement share the physical database, resolve/pin the immutable
version in the actual owning transaction/coordinator. Record the observed version
and time. A preview version that changed before capture yields a refresh response.
A separated deployment needs an explicit owner-consistency contract; do not claim
cross-database atomicity merely because both contexts exist.

## 9. Status / State Transitions

Unconfigured becomes Published revision 1; later changes create Published revision N+1. Retirement removes a version from new-use selection while retaining historical reads. A published calculation revision is immutable; administration CAS metadata is distinct from its revision identity. Unknown command outcomes remain unconfirmed until original-key recovery.

## 10. Permissions & Data Scope

Reuse current Core identity, tenant membership, settings administration and audit
infrastructure. Add explicit Core currency read/configure policies if the current
catalog lacks them; freeze their mapping before implementation. Reading an
invoice's purpose-scoped calculation reference does not require giving its maker
Core administrator rights. No implicit global role grants or Procurement role
flattening.

Core reference administration is a deliberately tenant/company-authorized master
operation, unlike branch-bound invoice creation. Document this boundary explicitly;
adding a currency reference does not authorize invoice writes in Company All.
New invoice commands retain their selected permitted branch requirement.

## 11. Business Rules

Historical reads of consumed versions remain valid after retirement. Retirement is neither deletion nor current-use reauthorization.

SIX's ISO 4217 Maintenance Agency [amendment 154](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/amendments/dl_currency_iso_amendment_154.pdf)
records ZMW, numeric code 967, minor unit 2. The [Bank of Zambia](https://www.boz.zm/currency)
confirms 100 ngwee per kwacha. References checked 2026-10-29. This supports the
initial ZMW precision proposal; it does not prescribe the ERP's tie-rounding method.

First implementation supports explicit integer minor units 0–4, matching existing
Procurement amount and matching-policy limits. Unknown codes or unsupported
precision fail with a clear configuration error. No fallback of “all currencies
have two decimals.” Supporting a larger scale requires a separately reviewed
storage/consumer change.

Support `ToEven` and `AwayFromZero` explicitly. Propose `ToEven` for the initial
ZMW reference to agree with the existing matching configuration; configuration
publication must make this choice visible. Minor units from ISO do not imply a
rounding convention. A transaction pins both values and the exact revision.

Resolve exactly one effective enabled version for the requested tenant/currency
and observation time. Overlap or ambiguity fails closed. The shared service
returns a typed immutable snapshot including code, revision, minor units, mode,
provenance and digest. Do not select an arbitrary row or silently use browser data.

## 12. Data Fields & Ownership

Extend the existing Core-owned Hrm domain/context/API host and Core settings UI;
no new service, database, project or Finance ledger. Since no reusable persisted
currency configuration was found, introduce one narrowly typed versioned currency
reference relation in that existing context. Exact naming and genuine EF migration
are frozen during implementation review, not invented migration SQL here.

Proposed `CurrencyPrecisionVersion`: tenant, canonical currency code, revision,
minor units, rounding mode, effective time, reference provenance, enabled/retired
state, creator/time and optimistic version. Published calculation fields are
immutable. Changing them creates a new revision; retirement prevents new use but
never deletes a version referenced by a transaction.

If a new invoice omits currency, resolve the selected active company through the
existing Core company reader first. Amizo's currency comes from that company
record; this contract proposes ZMW metadata, not a company-row rewrite. An
explicit transaction currency requires its own enabled Core precision reference.
No currency conversion is introduced.

## 13. Screens & Data Views

Connect the existing Core Currencies directory item to the typed editor. Show code,
minor units, rounding method, revision/effectivity and source provenance. Show the
company's existing default from Organization. A display-format preference must
never appear to alter calculation precision or historical invoices.

## 14. User Experience / Human Behaviour

Show the consequence before saving: minor units affect calculation, formatting affects presentation, and neither changes old transactions. Preserve drafts after validation/conflicts. Use actual Core company labels, not a fixed fictional organization.

## 15. Notifications & Communications

Use normal in-app save/retire/conflict confirmations. No supplier email, payment instruction or external message is introduced. Any later cache-invalidation event must be versioned and tenant-scoped; it grants no business authority.

## 16. Documents & Attachments

No uploaded file, OCR, antivirus or document-availability assertion is required. Store controlled reference provenance; supporting attachments, if introduced later, remain Core Documents-owned.

## 17. Exceptions & Edge Cases

Reject unknown/disabled/ambiguous references, stale versions, unsupported scales/modes and foreign scope. Preserve historical consumption independently from current eligibility. A separate physical Core deployment requires an explicit consistency adapter, not a claim of cross-database atomicity.

## 18. Integrations / APIs / Events / Sandbox

Use the purpose-scoped resolution and versioned configuration APIs described under Entry Points. No Finance rate lookup is required for this feature.

Extract the core-neutral integer quotient/remainder rounding algorithm currently
in `InvoiceMatchExact.Round` into an existing shared foundation location. Retain
identical tie behavior and exact arithmetic. Matching-specific policy types stay
in Procurement; the shared primitive must not depend on its application project.
Do not replace exact arithmetic with binary floating point or unchecked decimal
intermediates.

## 19. Audit / Security / Privacy

Business adapters get finite reference SELECT, never Core configuration writes, schema ownership or an owner-role fallback. Current identity/scope is revalidated for every configuration action.

Record who published/retired each version, expected/current revision, reason and
reference source. Preserve original command responses. Business transactions
retain their consumed snapshot/digest; a later settings change cannot reinterpret
old amounts. This feature creates no Finance journal or tax determination.

## 20. Reports / Dashboards / Metrics

Core settings/audit views show code, revision, effectivity, scale, mode and provenance. Readiness may count missing/ambiguous references per permitted company. Do not add ledger balances or cross-currency aggregate reports.

## 21. Configuration & Localization

Core company currency remains the default; reference configuration is versioned. Cash-denomination rounding, exchange conversion and presentation formats remain separate.

Full tracked-source inspection found no currency precision entity, `MinorUnits`
contract, generic persisted key/value settings service, or connected currency
editor at this baseline. `modules/hrm/frontend/module-connect/src/modules/core/settings.ts`
lists Currencies and General as planned. `routes/core.workspace.settings.$slug.tsx`
returns “Editor coming soon” for both and calls no settings save API. The connected
organization editor uses `IConfigAdminService`; it is not a generic JSON store.

The separate UI's `reference-ui/src/features/SettingsLists.tsx` CurrencySettings
contains React-local rows and “Save preview snapshot.” Its Settings route currently
explains Finance ownership instead of connecting a currency editor. Neither is
runtime authority. Reuse the Core settings directory/organization integration;
replace the ownership explanation with a link to Core's connected reference editor
only when that editor exists. No second Procurement currency master.

## 22. Non-Functional Requirements

Keep lookups bounded and tenant/company scoped. Cache immutable versions by identity; any current-reference cache must invalidate and cannot override write-time revalidation. No outbound reference download occurs in a business write. Exact integer arithmetic avoids floating-point intermediates.

## 23. Postconditions

Confirmed publication leaves a durable audited revision and recoverable command result. Consumption pins its exact snapshot/digest. Existing references, company settings and historical transactions remain unchanged; no Finance posting results.

## 24. Failure & Recovery

Unconfigured/disabled/ambiguous precision blocks new v2 calculation with a clear
link to Core settings. Failed configuration commands preserve the key and use
recorded-result GET recovery; an unknown outcome is not permission to repeat a
new write. Existing invoice/history reads remain available when new publication
is disabled.

Legacy invoices, matching policies, M6A baselines and recorded receipts remain
byte-identical. Old paths do not acquire a new mandatory currency-reference lookup.
Retained versions consumed by calculation evidence must veto destructive Down
under locks; never delete references or reset counters to make Down succeed.
Earlier deployments must not require a future relation before its migration.

## 25. Acceptance Criteria

The connected Core editor and API enforce current administration scope and truthful key recovery. ZMW2 has primary-source support; every other enabled currency has explicit precision. Both tie modes are deterministic, ambiguity fails closed, and later settings changes do not alter historical amounts.

## 26. Test Scenarios

Verify ZMW2, explicit 0/3/4 scales, unknown/disabled/ambiguous references, stale
preview, wrong tenant/company and unauthorized configuration. Test both tie modes,
exact overflow rejection and version pinning across setting changes. Genuine
private EF Up/empty Down/Up must preserve all existing rows, ACLs and owners;
rollback-only retained-reference probes prove the downgrade veto.

## 27. Ownership / Dependencies

Core owns configuration; Procurement consumes it. Reuse current Hrm domain/context/API host and Core editor infrastructure, not a new project, service or Finance database.

Generate EF migration/designer/snapshot from the reviewed model in the current
Core owner context. No existing company or business-row updates, account grants,
or organization defaults are part of schema installation. Business adapters need
only finite SELECT on currency references; Core configuration writes use the
existing Core owner service. Capture exact effective column/table privileges.

**Implementation milestones**

1. Freeze source discovery, typed owner contract and permission mapping.
2. Implement the narrow Core relation/reader/configuration API/editor and shared
   rounding primitive; generate genuine EF and verify legacy arithmetic parity.
3. Prove private normal-cookie Core configuration/recovery and finite access.
4. Integrate new-invoice v2 through its separate feature contract, then run paired
   original-UI/native acceptance. No live rollout is implied by a unit test.

## 28. Open Decisions / Assumptions

Before implementation freeze exact relation/route names, permission mapping, first enabled currencies, initial effective time and the explicit ToEven choice. Confirm whether any reference-change event is needed for existing caching.

This proposed specification records no implementation, configuration publication or production activation result. Existing M6C control
cases, blocked invoice history, UI/backend source and runtime obligations remain
separate. Currency-aware totals and controlled post-history correction remain
open until independently implemented and accepted.

Implementation remains part of the already authorized programme. This design record creates no new permission gate and asserts no implementation, acceptance or rollout result.
