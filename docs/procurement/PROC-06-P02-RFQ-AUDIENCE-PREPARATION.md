# PROC-06-P02 — Owned RFQ audience preparation contract

**Status:** Design prerequisite, source audit only; no executable eligibility evaluator, provider, model, API, migration or activation. Base `d57999a` (finite authority source remains separately unverified). Extends PROC-06 sections1–28/O4–O5. Existing UI is retained.

## 1. Feature identification

Prepare an exact proposed supplier/contact audience for an execution1 governed RFQ. Procurement owns the proposal, not another vendor master. `CanPublish` remains false.

## 2. Purpose and business outcome

A buyer can propose a policy-qualified named audience and an independent reviewer can inspect exactly which existing suppliers, contacts and qualification evidence were considered. Proposed eligibility never means supplier delivery, external authentication, PO eligibility or payment approval.

## 3. Scope and exclusions

Current source-backed RFQ, same company/tenant/branch, configured audience policy, complete relevant supplier facts, exact retained selection. Exclude dispatch/email/portal, bids, bank verification, AP, publication approval/execution and supplier master changes. Do not call `SupplierEligibility.CanReceiveNewOrders` for RFQ selection: that helper deliberately includes `IsOrderable` and payment verification rules for orders.

## 4. Parent and related features

PROC-06-P01 O4/O5, P28 owned draft/holds, P29 controlled SOURCING policy identity, PROC-03A finite limits, Core private documents/use retention. Existing `SourcingInvitation` remains the eventual invitation record; it does not currently retain reviewed contact/version/eligibility pins.

## 5. Actors and participants

Current canonical original RFQ owner with an explicitly supported preparation action; later independent named publication reviewers. Existing Core accounts and scope. Supplier contacts are commercial contacts, not Core users or portal principals. A contact email is not an authenticated supplier identity.

## 6. Entry points and triggers

Existing governed RFQ detail, proposed audience selection/reselection and preparation review. Future purpose-scoped lookup must not require broad vendor-master access merely to select an eligible audience, nor return private banking/tax/other supplier facts.

## 7. Preconditions and actual DoR gap

Current SOURCING schema1 cannot express audience eligibility. `SourcingPublicationPolicyRules` permits only minimum quote/invite counts, pack/criteria/deadline booleans and named review stages. It rejects unknown fields. It has no required qualification types, category-match rule, contact selection/required fields or validity horizon. Existing minInvited0 and false controls are not evidence of intentionally unrestricted suppliers. Consequently a positive evaluator cannot honestly be implemented from current published rules; missing audience policy must produce Unavailable, not eligible/defaults.

P29 establishes authentic policy maker/reviewer history, not these missing business rules. No schema1 policy is silently upgraded or rewritten.

## 8. Workflow and required policy addition

Proposed first technical contract: a separately validated SOURCING **schema2** retaining the existing root/action/currency/basis/stages and adding required `result.audience` to each matched rule. Continue schema1 read/configuration compatibility, but do not infer schema2 audience authority from it. Schema2 must pass the same P29 creation/submission/publication identity lineage; document schema and policy record version are separate.

The following are explicit proposed fields, **not adopted defaults**. Business/technical ownership must approve them before parser/evaluator coding:

| Field | Bounded initial meaning |
|---|---|
| `supplierLifecycle` | Explicit `RecordedActiveDecision` only in initial supported contract: current Supplier.Status Active plus exact immutable matching SupplierDecision and validity evidence. This is the existing owner's recorded lifecycle, not a claim of historical worker-level independent approval. Unsupported stronger approval-proof requirements block. |
| `categoryMode` | Explicit `Ignore` or `CoverAllSelectedCategories`. Cover uses dated SupplierCategory rows at the chosen horizon; never silently falls back to mutable Supplier.CategoryCode or catalogue preferred-supplier labels. |
| `qualificationTypes` | Explicit bounded distinct list (empty intentionally permitted), compared using the existing owner case-insensitive type convention, without aliases/synonyms. Latest active created revision per type is the proposed owner-compatible selection; newly pending latest evidence cannot fall back to an older Verified row. |
| `qualificationDocuments` | Explicit `NotRequired` or `CoreAvailableVersionRequired`. Required means verified actual Core owner/version/hash/clean-scan facts, not the existing bare EvidenceDocumentRefId. Unknown owner/version/scan blocks. |
| `validThrough` | Explicit `PreparationInstant` or `ClosingInstant`. Closing requires a valid actual closing timestamp; no invented deadline. DateOnly expiry uses documented UTC calendar-day inclusive end semantics; instant supplier windows use explicit boundary semantics in the approved parser contract. |
| `allowNoQualificationExpiry` / `allowOpenEndedSupplierValidity` | Explicit booleans, never assumed from null. IssuedOn after the horizon is ineligible. Missing required evidence is not a valid open-ended date. |
| `contactSelection` | Explicit `OneActiveSelected` or `OneActivePrimarySelected`, using actual SupplierContact ID/version. Multiple primaries do not silently pick the first. |
| `contactRequiredFields` | Explicit bounded subset of `Email`, `Phone` (empty intentionally permitted for internal review). Reuse existing owner validation; no assumption that syntax proves delivery/ownership/consent. |

Policy count controls remain explicit and separately reconciled with retained source quote requirements. A quotation count is not an invitation count or evidence that a bid exists. Reuse one shared policy match/coverage engine; no second policy master or approval engine.

Intended future path: resolve owned RFQ/source → resolve authentic effective schema2 policy → load complete relevant supplier facts/current document owner facts → evaluate before effects → retain a proposed audience revision → review/publish through the separate O4/O5 execution contract. Source/qualification/contact mutations invalidate current readiness, but not historical evidence.

## 9. Status and transition matrix

| From → To | Actor/guard | Effect |
|---|---|---|
| No proposal → Prepared proposal | Current owner, action/scope, schema2 policy, complete current facts | New immutable proposed snapshot; not an invitation/publication |
| Prepared → Superseded proposal | Authorized explicit reselection/CAS | New revision preserves old bytes; review pins invalidated |
| Prepared → Review-bound | Future supported Core submission contract | Pin exact proposal revision, never re-resolve silently |
| Review-bound → Published roster | Future independent Core outcome/current evidence contract | Sealed publication plus pending invitation references, not delivery |

Only the first two are the proposed bounded preparation milestone. Existing execution1 Draft/Cancelled SQL/state guard must not be bypassed by writing a status string.

## 10. Permissions and data scope

Preparation permission/action naming is a DoR decision; legacy sourcing-edit/vendor-read or selected department is not authorization. Reuse Core current canonical account, company membership, branch/department scope and branch fence. Filter owned RFQ immutable source scope before counts/pages. Supplier lookup is restricted to the proven RFQ purpose/company and minimal projection; commands recheck current scope even on replay. No frontend flags or client-provided `eligible=true` are trusted.

## 11. Business rules

Distinct supplier IDs; exactly one explicit owned contact per selected supplier; every chosen row belongs to the exact tenant/company/supplier. Capture and revalidate full relevant child sets, including inactive/history rows needed to prove latest/type/category selection; don't accept an injected filtered subset as complete. No email/name binding, dictionary first-match, multiple-primary guessing or empty-history-as-complete default.

Current lifecycle state must match its retained decision, not just an Approved label. The existing vendor approval route compares principal IDs and records actor/time; it does not retain canonical historical approver workers. Do not claim alias-proof historical supplier approval from these fields or current account lookups. If that stronger requirement is adopted, a separate supplier owner evidence upgrade is required.

Unknown policy, missing category/qualification/contact source, unverified document owner or changed RFQ/source blocks current eligibility. A proposed roster consumes no additional PR/Finance quantities. Only later actual command authority may publish.

## 12. Data fields, ownership and proposed interface

Reuse `Records.cs`: Supplier; SupplierContact; SupplierQualification; SupplierCategory; immutable SupplierDecision. Common ProcurementRecord owns stable ID/tenant/company/version. Contacts are mutable with parent and child CAS; qualifications are mutable only while Pending and reviewed records cannot be edited/removed through current routes. SupplierCategory has effective dates but no explicit inactive flag; Supplier.CategoryCode is a different primary classification, not equivalent dated coverage.

Proposed **documentation-only** application boundary, to finalize with schema2 adoption:

```csharp
// All inputs are server-resolved; not HTTP DTOs or a new persisted supplier master.
AudienceProposal PrepareAudience(
    VerifiedOwnedRfq draft,       // exact original fingerprint, CAS, selected line/category pins
    PublishedAudiencePolicy policy, // policy ID/record version/schema2/rule/digest + P29 lineage
    CurrentActor actor, DateTimeOffset now,
    CompleteSupplierAudienceFacts facts,
    IReadOnlyList<SelectedSupplierContact> selections);
```

`CompleteSupplierAudienceFacts` must carry the exact requested supplier IDs, source tenant/company, complete relevant child-history digest/watermark, and actual document reader result when required. Completeness is produced by the owning adapter, never a caller bool or an always-available fake. Do not create an injectable production provider until this source can be authenticated/reconciled.

Each immutable proposed supplier pin includes supplier ID/CAS/status/observed validity/decision ID+original actor/time, selected contact ID/CAS and exact required contact data, covered category row IDs/CAS/effective dates, selected qualification IDs/CAS/type/status/recorded reviewer/time/expiry and actual optional Core document version/scan/hash evidence, policy/rule/line-category digests, validation horizon, original preparer principal/worker/time, proposal schema/version and digest. Preserve original snapshot bytes; use existing evidence encoding/UTC microsecond rules. No CAS in mutable principal keys. No banking fields, API secrets or copied identity masters.

The result is an immutable proposed snapshot plus blockers, with `CanPublish=false`. It is not SupplierInvitation.Sent, approved publication, or an authenticated external delivery address. There is deliberately no implementation returning eligible while current policy is incapable of expressing these decisions.

## 13. Screens and data views

Extend existing GovernedSourcing detail/editor only after contracts. Purpose picker shows supplier display name, selected contact label, allowed minimal contact fields, readiness/blocker and exact CAS. Existing LiveVendors remains supplier owner UI. No new workspace/page or local-only eligibility persistence.

## 14. UX and human behaviour

Explain missing setup plainly. Let authorized users review why a supplier is unavailable; avoid disclosing private qualification documents broadly. Show proposal date/version and stale source warning. Internal audience is not “sent”; require explicit contact choice rather than silently selecting primary.

## 15. Notifications

No outgoing supplier communication in this milestone. Existing internal activity/audit may describe preparation. Actual delivery and retry/acknowledgment contracts are later work.

## 16. Documents and attachments

Existing qualification evidence references lack exact document version/hash. Core currently dispatches Procurement owner access only for supplier_invoice. Supplier qualification and RFQ pack purpose ownership must be specified, with current authorized owner read/scan/version evidence. Core P03 retention/fence primitives exist, but their managed writer/provider integration is not yet complete. Never assert document authority from a Guid alone or duplicate upload/storage/scanning.

## 17. Exceptions and edge cases

Foreign contact under selected supplier, removed contact, duplicate/aliased category/type, inactive latest qualification, expired/future-issued evidence, empty required source set, unknown eligibility horizon, changed approval/status decision, unavailable scanned document or owner rebound must fail closed. Zero configured invite count does not infer omitted audience policy. Historical preparation remains readable without pretending currently eligible.

## 18. Integrations/APIs/events/sandbox

Future same-store use-case adapter reuses current Core/Procurement coordination and owning supplier reads. New preparation/lookup routes are not yet designed as callable endpoints. No supplier-bank/Finance API or external portal is introduced. Proposed event means audience_prepared, never invitation_sent; later transactional outbox must not imply successful delivery.

## 19. Audit/security/privacy

Snapshot actor, source, policy, selected exact identities, before/after revision/digest, reason and current request correlation. Contacts are personal information; show only purpose-required fields and private documents only under actual owner authorization. Preserve an audit trail without exporting tax/bank/commercial notes. Current access must be enforced independently of immutable past eligibility.

## 20. Reports and metrics

Only proposed count/blocked reasons/preparation revision, subject to scope. No supplier performance, sourcing participation, savings or successful delivery metrics inferred.

## 21. Configuration and localization

Required schema2 audience decisions remain explicit. Use Core company currency/time context, configured UTC expiry convention and existing item/source category IDs. No hardcoded qualification types, mandatory email, bank verification, category fallback, monetary threshold or silent policy upgrade.

## 22. Non-functional requirements

Bound supplier/contact count and source-history payload before canonicalization. Exact deterministic ordering, stable row IDs, current counters and checksums; nonmutating evaluation. Future commands acquire Core branch → relevant owner lifecycle fences before transaction snapshot, then ordered domain source/supplier locks. Current vendor/contact/category/qualification writers need cooperative consistency coverage; SERIALIZABLE alone does not promise a reader sees an owner update committed while it queued. Reuse generic session lease; no second lock implementation.

## 23. Postconditions

After eventual successful preparation, one retained proposed roster revision exists and original source holds, Finance reservations and vendor master data remain unchanged. Under current code/policy, only this design exists; no positive eligibility or preparation capability is advertised.

## 24. Failure and recovery

Policy/fact mismatch returns explicit blockers before any effect. Durable preparation command key/CAS/fingerprint/replay and current-access recovery must be designed with the eventual persistence adapter, not promised by a pure helper. Retain historical snapshots and never mutate original selections after submission. No retry can duplicate invitation/publication effects.

## 25. Acceptance criteria

Schema2 parser retains explicit empty/false settings and rejects missing/unknown/duplicate/unsupported fields; schema1 never supplies audience readiness. Existing supplier IDs/version/qualified history/current contact, authentic policy lineage and source original fingerprint bind the proposal exactly. Scope and complete-history denials precede effects. CanPublish remains false. PO/bank eligibility behavior is unchanged.

## 26. Test scenarios

Required future tests: configured empty versus absent qualification rules; category Ignore versus full dated coverage and wrong primary-only shortcut; latest Pending denies older Verified fallback; date boundary/future issue/null expiry; removed/wrong-owner/duplicate contacts; optional versus mandatory actual scanned document; policy/draft/worker/scope tampering; deterministic order and eight-place source evidence; stale CAS and queued vendor/contact/category/qualification writer-first changes; no Finance/PR/vendor-master mutation; exact same-key retry/changed-payload conflict/current-scope recovery when adapter exists. No tests were authored as passing behavior because the required policy inputs are currently absent. No jobs executed.

## 27. Ownership and dependencies

Procurement: supplier/current commercial status, policy decisions, proposal. Core: current identities/org/permissions, actual documents/scans/retention and shared session mechanics. Finance: accounting/bank/AP, uninvolved here. Existing shared components and source ownership remain; no new supplier or identity table.

File-level sequence after policy adoption: (1) extend `SourcingPublicationPolicyRules` with explicit schema2 audience parser/projection and negatives; retain schema1 compatibility without eligibility; (2) implement one pure `SourcingAudiencePreparation`/immutable evidence contract and tests using server-owned projections; (3) exact owning supplier/document loaders and current writer fencing; (4) additive preparation evidence mapping/API/idempotency/actual PG proof; (5) extend existing GovernedSourcing UI and actual browser. SupplierInvitation contact/version pins and publication execution follow the reviewed O4/O5 schema, not duplicate stores.

## 28. Open decisions and assumptions

Adopt exact schema2 fields/enum/date semantics and required qualification categories/contact data; approve whether recorded Active decision is sufficient versus a new canonical independent supplier approval proof; define qualification document ownership and selection; explicit preparation permission; actual complete-facts/writer-fence and durable revision contracts. These are genuine DoR dependencies, not an authorization request to the user. Until resolved, keep current drafts, configuration and historical CRUD usable and preserve production default-off/release hold.

### Executable next schema/configuration increment — draft use remains available

No additional user permission is needed to author this bounded configuration work. Business activation requires explicit policy choices, not stopping draft CRUD. The next implementation should separate **draft syntax validity** from **review/publication completeness**:

1. Introduce a bounded typed schema2 parser/projection in `SourcingPublicationPolicyRules`, reusing the existing strict JSON duplicate/size/code/exact-number parser and shared match/coverage engine. Preserve schema1 behavior and retained hashes byte-for-byte. Schema2 requires an explicit `audience` property; `null` or a known-field object with null values means an incomplete draft. Unknown properties/enum values, duplicate fields/types, malformed non-null values and oversized lists remain errors. Do not deserialize with defaults that turn null/missing into false/empty/Ignore.
2. Provide explicit validation operations for draft versus submission/publication. Create/update and the draft validation endpoint accept syntactically valid incomplete schema2 audience selections and return distinct readiness/blocking issues. Submit/publish and actual audience preparation require complete explicit choices in every applicable rule plus existing supported coverage/person/company checks. Current schema1 configuration stays usable but cannot become audience-preparation authority. A generic `isValid`/P29 configurationVersion1 must never be interpreted as schema2 review-ready; include explicit draft/review status in the typed response and keep CanPublish false.
3. Update existing `ProcurementPolicyDraftRoutes` and `ProcurementSourcingPolicyConfiguration` call sites deliberately: create/update use draft validation; submit/publish use completeness validation. P29 captures the actual original incomplete draft digest and later complete submitted digest normally; never backfill the Created row or compare it to a later edited draft as if unchanged. Retain maker/worker independence and exact original policy identity.
4. Extend the existing Buying rules editor (no new page), with an initial “Not configured” state for each audience setting. Company currency still uses the existing organisation default. Users can save and reopen an incomplete audience draft; they must choose the relevant explicit values before submitting. No required qualification type, contact field, bank status, category behavior or validity horizon is auto-selected. Existing unrelated policy/PR/PO controls remain intact.
5. After parser/configuration tests pass, implement the pure proposal evaluator using the typed complete projection and server-owned facts, followed by the owning persistence/API/UI gate sequence in section27. Do not combine this first parser/configuration step with publication execution or enable an unavailable supplier/document owner.

Draft and complete interpretation of each proposed field:

| Setting | Draft representation | Explicit complete choices |
|---|---|---|
| supplier lifecycle | null | RecordedActiveDecision (initial supported observation), no implicit canonical-independent-approval claim |
| category mode | null | Ignore or CoverAllSelectedCategories |
| qualification types | null | Explicit [] for none, or bounded distinct required type labels; never missing means none |
| qualification documents | null | NotRequired or CoreAvailableVersionRequired |
| validity horizon | null | PreparationInstant or ClosingInstant |
| no qualification expiry | null | Explicit true or false |
| open-ended supplier validity | null | Explicit true or false |
| contact selection | null | OneActiveSelected or OneActivePrimarySelected |
| contact required fields | null | Explicit [] for no optional field requirement, or bounded Email/Phone selection |

Technical first-slice choices requiring no invented business value: category coverage uses the existing dated SupplierCategory rows only (no silent primary-field fallback); qualification type comparison uses the existing case-insensitive owner convention and latest active created revision; required Core document truth comes from the actual owning reader or blocks; all selections pin exact source IDs/CAS rather than names. The policy author still chooses Ignore versus category coverage, required types, contact fields, dates and null-expiry acceptance explicitly. Inclusive DateOnly expiry boundaries must be encoded once in the pure evaluator and tested against the selected trusted UTC date; supplier instant-window end behavior must follow the approved explicit contract, not a display locale.

Required first increment acceptance: incomplete schema2 draft saves/reloads with nulls unchanged; []/false survive round trip distinctly from null; incomplete submit/publish returns actionable issues with no state/identity/audit partial effects; completed explicit configuration follows normal independent P29 lifecycle; schema1 historical CRUD and PR/PO rule parsing are unchanged; unsupported stronger approval/document claims stay blocked. Tests must explicitly prove an otherwise qualifying RFQ audience is not blocked solely by supplier IsOrderable=false or pending bank/payment verification, because those PO/Finance fields are outside this RFQ contract. No test should claim a current production audience evaluator already exists.

### Pure audience configuration source checkpoint

An isolated source increment based `17a0a5e` adds `Application/SourcingAudienceConfiguration.cs` and meaningful pure tests only. Existing `SourcingPublicationPolicyRules`, schema1 parsing, owning routes, P29 identity evidence, DI and model/schema are unchanged. The new entry point is explicitly `ParseDraft(2, audienceJsonFragment)`: it parses only a proposed audience fragment, not an authentic full schema2 published policy. A C# null/missing fragment is invalid; JSON `null` is a legitimate unconfigured draft. Known fields omitted or set null remain unconfigured, while [] and false are preserved as deliberate complete choices.

Typed projections are immutable and list values read-only. The parser bounds UTF-8 bytes/depth/list lengths, uses the existing canonical evidence encoder to reject duplicate properties and preserve object/array semantics, rejects unknown/case-variant fields, numeric/combined enum strings and nonboolean flags, and uses the existing case-insensitive qualification-type convention without normalizing labels. Qualification label length matches the owning 80-character field. The fragment digest is over the canonical `{"audience": ...}` envelope and is explicitly not a full policy digest or supplier proof.

`ValidateDraft` reports syntax issues; `ReviewIssues` reports completeness of these nine choices only. Empty issues do not establish current policy approval, supplier lifecycle/category/qualification/contact truth, Core document authority or publication readiness. Every draft's `CanPublish` is false. No positive audience evaluator is added because authenticated complete owning-history/current document and lifecycle-writer adapters are not yet present. Pure syntax tests cannot supply those owners. The existing schema1 full-policy parser still rejects unsupported schema2; live adoption is a separate reviewed integration increment.

Authored tests cover unconfigured null/object fields, each individual completeness choice, explicit empty/false and fully selected values, immutable lists, unsupported versions, exact/nested-kind ambiguity/unknown fields and labels, duplicate case-insensitive qualification types, category/contact/horizon alternatives, bounds including actual multibyte UTF-8, deterministic canonical object order and retained array order. No tests/builds/SQL/browser jobs were executed in this source slice, and no business defaults, bank/PO coupling or capabilities were introduced.

### Owned schema2 draft lifecycle integration — source candidate

A dependent isolated source increment after33c5ab8 integrates only policy configuration. It adds no entity/mapping/migration, publication endpoint, supplier evaluator, owner provider, invitation/dispatch, feature flag or financial rule. Source changes and authored tests are unexecuted until parent gates them.

| Call site | Validation contract |
|---|---|
| SourcingPublicationPolicyRules.ValidateDraft | Root/action/number/code/fields/stages and schema1 compatibility; schema2 requires audience property and valid nullable fragment syntax |
| Existing Validate / AnalyzeCoverage / Evaluate / BindNamedReviewers | Strict audience completeness for schema2; incomplete choices block positive configuration preview/binding; CanPublish remains false |
| Policy draft create/edit CheckSourcingCompanyRules | Explicit draftSyntaxOnly, still current canonical actor, Core scope/company currency/category/named-person checks before mutation |
| Submit/publish CheckCompanyRules | Default strict validation; no default audience values; completeness denial before stage/identity/audit save |
| Return actor scope | Draft syntax check for rule scopes, not accidental audience completion or a publish grant |
| Effective SOURCING read | Existing retained identity verification plus strict AnalyzeCoverage; no incomplete2 Valid result |
| Existing validation endpoint | isValid/issues refer to draft syntax; separate reviewConfigurationComplete/reviewIssues refer to configuration completeness, not actual policy approval/actor eligibility; isAuthority=false/CanPublish=false |
| Scoped reviewer lookup/validation metadata | supportedSourcingRuleSchemas=[1,2] explicitly describes parser versions; independent existing configurationVersion remains actual store readiness1/0 |

Capture/Verify call graph was inspected: SourcingPolicyIdentity.Capture→Verify and VerifyOrigin/Submission/Publication/VerifyOwned use their own scope/action/actor/revision/time/whole-rule canonical digest guards; none invokes the typed publication parser. No helper or persisted identity semantics are changed. Created keeps its original incomplete draft digest and maker identity across an authorized draft edit. Submitted and Published still pin the exact completed/current canonical rules and full retained chain; mutating current rules after that proof fails. Business syntax/completeness remains a separate owned policy validation guard, not a weakened or repurposed identity hash.

Schema1 is not rewritten and still rejects an audience field. Schema2 completeness requires every rule's audience choices; absent field, unknown shape/version/enum, duplicate or malformed values are syntax errors rather than incomplete drafts. Empty/false choices remain explicit. Supported version metadata alone does not grant CRUD, review, supplier eligibility or publication. Existing frontend src/lib/buying-rules-editor.ts requires schemaVersion1 in parseSourcing; LivePolicies routes unrecognized rules to its preserved read-only panel. This source does not change that frontend guard or presume a schema2 editor exists.

Authored pure tests cover null draft versus strict review/coverage/preview, completed explicit configuration, schema1 compatibility, missing/unknown/quoted/unsupported schema2 and original incomplete Created→edited complete Submitted→Published digest continuity plus current-rule tamper denial. Mapped tests save/reload null unconfigured audience, expose syntax-versus-completeness/version metadata, reject incomplete submit/malformed create without state/identity/audit effects, then complete/edit/submit/independently publish and read the effective schema2 policy with original Created evidence unchanged. Existing marker-off tests assert supported versions remain separately exposed. No test here certifies complete supplier history, document truth, live audience preparation or operational publication.
