# Request draft exact original values — bounded release contract

## 1. Reference and status

GW-ENG-STD-001 Tier 3. Bounded request draft transport/editor correction. Prepared source only; no compile, unit, browser or database execution has been performed for this candidate.

## 2. Outcome

Capture and edit exact numeric18,4 original quantities, unit estimates and discounts without JavaScript Number rounding or erasing hidden mutable fields.

## 3. Existing UI

Reuse LiveRequestEditor, existing Field/Input/Panel controls, published catalog selection and request routes. No page, navigation, branding or state-machine replacement.

## 4. Owned records

Procurement owns PurchaseRequest/PurchaseRequestLine. Core identity, company, branch, permissions and document ownership remain existing contracts.

## 5. Schema applicability

Schema-free: no domain/model/persistence migration change. Existing numeric18,4 quantity/rate and numeric7,4 discount storage retained.

## 6. Actors and permissions

Existing own-draft creator/requester and permission rules retained. No reviewer, admin-view-only or service-principal bypass added.

## 7. Record scope

Existing tenant/company/selected branch and own-record filters remain authoritative. Company/route response IDs and safe integer CAS are checked client-side before edit/confirmation.

## 8. Entry conditions

New drafts use organisation default currency. Existing Draft/ReturnedForChanges editing requires complete requestEditVersion=1 plus explicit owned header/line fields and exact companions.

## 9. Mutable fields

Whitelist purpose/currency/needed-by/cost/project/delivery/funding/note and all existing RequestLineInput fields. Preserve unseen discount/tax/cost/project/specification-document and catalog bindings. State, owner, approval references, counters and immutable scope are not sent as edit authority.

## 10. Raw transport

RequestLineDecimalJsonConverter and nullable equivalent delegate to existing ExactDecimalJsonCodec.Read(18,4). Numeric and quoted decimal inputs accepted without first passing through CLR approximate/rounded JSON decimal parsing. Unsupported scale/range/underflow representations reject before handler mutation.

## 11. Additive read contract

Detail adds requestEditVersion:1 and each line exactAmounts:{requestedQuantity:string,estimatedUnitPrice:string,discountPercent:null|string}. Companions use invariant 0.####. Existing numeric fields remain unchanged for legacy consumers.

## 12. Complete edit proof

Client requires owned-field presence and matching null semantics, safe positive version and IDs. It reads financial originals only from exact string companions; approximate numeric originals are retained solely in unchanged readonly detail interfaces.

## 13. Catalog source

Select only validated exactEstimatedUnitPrice from the separately prepared catalog companion contract. Missing exact price leaves browsing usable and Use item disabled. Preserve catalog item/entry/publishedVersion/vendor; actual server catalog eligibility/version resolution remains unchanged.

## 14. Calculations

Reuse existing invoicePreview BigInt scale8 product/sum implementation. PR estimate is before discounts/tax/delivery and may retain eight decimals. Do not round individual products, force 4dp totals, infer currency rounding or change server total cap. Preview is an estimate, never a Finance/approval decision.

## 15. Validation

Quantity positive; price nonnegative; input up to14 whole/4 fractional digits via existing pure invoiceDecimal. Existing server discount0..100 and all domain/total checks remain final. No new thresholds/default financial policy.

## 16. Transitions

Existing create/update only; Submit/Withdraw/Return/approval workflows untouched. Exact edit response must still describe Draft/ReturnedForChanges. No automatic submission or approval.

## 17. Concurrency

Retain current parent CAS input. Update response must match requested ID and version+1. Context generation/unmount guard remains; edit GET cannot fall back to POST create.

## 18. Atomicity

Existing server unit of work remains. Raw invalid POST and PUT tests compare all request/line data plus audit/outbox counts after denial, not counts alone.

## 19. Audit and effects

Existing create/update audit only. No new outbox, approval, Finance, document or reservation operations. Numeric companion projection is read-only.

## 20. Failures and recovery

Invalid local decimal has no HTTP mutation. Missing/incomplete edit contract hides editable form. Unknown/5xx/invalid acknowledgement locks repeat submission and links to saved records. No automatic POST replay.

## 21. Compatibility

Legacy numeric backend clients still send/receive existing numeric fields; inputs that depended on decimal rounding are intentionally rejected. Updated UI paired with old backend can browse/read but cannot safely edit. Missing exact acknowledgement after create/update means unconfirmed save, not proof of rollback.

## 22. Mapped HTTP tests

Nine authored cases: wide create/edit/reload with hidden fields and independent owning-context exact reload; six raw precision/range/underflow POST+PUT denials with zero data/audit/outbox effects; legacy numeric+8dp products without authority; catalog binding retention. Existing synthetic worker/scope/InMemory fixture is not actual PG/runtime identity proof.

## 23. Client tests

Eight authored tests: wide companion/hidden fields; missing/old fields; bad exact/CAS; mutable whitelist; exact8dp sums; actual string PUT+CAS; invalid0POST/wrong receipt; legacy readonly compatibility.

## 24. Browser tests

Four new mocked-transport cases in existing live-request.spec: wide capture/reload/header edit; two tiny8dp products; old readable contract edit denied; unavailable catalog exact plus invalid precision0POST. Existing tests adapted to truthful exact companions and approximate legacy numeric wire. No mocks represent actual API/DB proof.

## 25. Required actual verification

After source review, run focused mapped HTTP and unit tests/TSC/build, then full relevant request browser tests on frozen bundle. Extend existing private CrudHttpProbe actual API/owner reload for wide exact request/create/edit/raw denials before claiming PostgreSQL/full integration.

## 26. Release and rollback

Apply backend additive contract before enabling updated edit UI. Schema-free rollback can restore source but must not make old rounded values appear authoritative. Preserve unchanged existing rows/source evidence; no backfill or historical rewrite.

## 27. Files and ownership

Backend ProcurementRequestRoutes.cs + two thin transport converters + partial mapped tests. Frontend procurement-requests-api.ts, LiveRequests.tsx, request-draft-values.ts, focused tests and existing browser spec. Depends on separately reviewed shared/procurement-decimal.ts extraction and catalog exact companion.

## 28. Limits and acceptance

Source-only candidate. No migration, provider, engine, role or financial authority added. Readonly request detail/queues still expose legacy numeric presentation outside this edit correction; do not claim universal read precision. Release only after serialized review/test/browser/API evidence and source hashes are recorded.


## Actual probe source addendum (not executed)

The isolated backend candidate extends the existing private `ProcurementCrudHttpProbe` through one new partial source file and a call in its normal sequence. Source category/item/catalog publication prerequisites are genuinely created/reviewed through mapped HTTP by distinct existing synthetic worker-linked identities; supplier approval remains the tool's labelled owner fixture. Max18,4 input, full mutable-field correction, removed-line retention, exact numeric legacy/tiny8dp input, stale/foreign/nonowner denials and raw invalid token0effect cases are authored. Full independent owner table snapshots reuse existing PolicyStoreRows; no new provider/readiness/HTTP endpoint exists. New assertions and exact request counts await actual execution and must not inherit earlier tool PASS counts.
