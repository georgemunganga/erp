# M6D: new invoice calculation v2 with Core currency precision

Proposed child-feature specification using the mandated 28-section framework. Existing M6C product source remains frozen.

## 1. Feature Identification

Feature reference: M6D. Procurement owns invoice calculation evidence; Core owns precision. Discovery baseline: ERP 0f656d6316e39063eb09da45210db4b142717ecf and original UI 56fbd3b. Status: Core reference, persistence, invoice v2 API and original-layout UI integration are implemented on the M6D development branch. Automated checks and private real UI/native capture recovery passed; qualified matching, wider workflow UAT and release review remain open. See the [capture checkpoint](M6D-PRIVATE-CURRENCY-CAPTURE-CHECKPOINT-20261030.md). See the [bounded arithmetic checkpoint](M6D-PURE-CALCULATION-CHECKPOINT-20261029.md).

## 2. Purpose & Business Outcome

Current manual intake accepts quantity/rate/tax with up to four decimals and
persists exact raw sums. A 1 × 5.1234 invoice therefore has net 5.1234. A legitimate
matching policy with money scale 2 correctly blocks that header. New calculation
v2 must preserve rate 5.1234 while explicitly calculating its currency-rounded
line net 5.12; it must not weaken matching or rewrite that historical invoice.

## 3. Scope & Exclusions

Creation only. No Finance/Tax ledger, OCR, fake document availability, processed-invoice rewrite or matching-policy relaxation is included.

New captures opt into `intakeSchemaVersion:2` through a separately enabled
capability. Missing version retains the legacy v1 exact-raw calculation and bytes.
Unknown versions fail. This milestone covers creation only; it does not silently
change old-client behavior or retrofit v2 onto mapped/processed invoices.

Current `ManualInvoiceDrafts()` requires no mapping, match, attachments, allocations
or downstream evidence. Thus mapped/reviewed/Blocked invoices cannot use intake
PUT/Cancel even while Received and claim-free. This is an explicit separate gap:
a controlled correction/replacement workflow must preserve old histories, use
current CAS/authority, and explicitly reverse any active claims. Duplicate invoice
numbers remain reserved even after intake cancellation. M6D does not clear those
pointers, silently edit processed originals or reuse old unit attestations.

## 4. Parent & Related Features

Related contracts are Core currency precision, current intake CRUD, M6A owner/baseline inventory, M6B principal matching and M6C unit review. Their owners and retained history remain in force.

`ProcurementInvoiceIntakeEditing.Prepare` performs exact multiplication/sum and
requires totals representable at 18,4. `InvoiceIntakeInput` has no header override.
`InvoiceMatchingFoundation` requires currency-scale headers and sums per-line
`InvoiceMatchExact.ProductMoney`. `InvoiceMappingEvidence.OriginalDigest` already
pins original header amounts and exact active-line values. These contracts frame
this additive change.

Core owns authoritative currency precision under the [shared prerequisite](../../core/CORE-CURRENCY-PRECISION-20261029.md).
Procurement owns invoice capture, amount evidence and matching. Reuse current
invoice tables, Core identity/company defaults, exact arithmetic, shared Core
review engine and original UI. No Finance, Tax ledger, OCR, document availability
substitute or duplicate currency master is introduced.

## 5. Actors & Participants

Invoice maker enters source data and confirms the preview. Core administrator maintains precision. Existing independent Core unit reviewer verifies units when required; matcher uses current owner/capacity proof. No new AP approver, Finance posting actor or document scanner is introduced.

## 6. Entry Points & Triggers

`POST /api/procurement/v1/invoices/calculation-preview` is a nonpersisting,
selected-branch, capture-authorized operation. It resolves Core reference and
returns exact line/raw/rounded totals, adjustment, consumed reference revision and
calculation digest. It creates no invoice, approval, claim or Finance record.

Extend new-invoice capture with explicit schema 2 and an expected preview digest
or reference revision. Server recomputes using the actual owner context in its
transaction; stale configuration/input produces a refresh conflict. No caller can
set `MinorUnits` or override the authoritative header. The returned DTO adds a
versioned calculation object; existing IDs, totals, status and legacy fields keep
their meanings.

## 7. Preconditions

Require current Core access, selected permitted branch, invoice-capture permission, installed v2 physical/finite-role readiness and explicit capability. Unknown precision blocks v2 rather than silently reverting to v1. Only a genuinely new invoice may acquire creation-v2 evidence.

Use the selected active Core company's currency when omitted, through the existing
`ProcurementCurrencyDefaults` owner. Amizo/ZMW is a company setting, not a fixed
Procurement literal. Explicit currencies require an enabled Core precision
version. Unknown/unsupported precision blocks v2 with a configuration explanation.
No exchange-rate conversion.

## 8. Workflow

1. Select company/branch/vendor and enter exact quantity/rate/tax.
2. Server preview resolves Core precision and returns raw/rounded line totals and adjustment.
3. Maker confirms; capture recomputes with the actual owner/current reference.
4. Commit header, complete original lines and immutable calculation evidence atomically.
5. Map and independently review units using the original digest.
6. Existing Compare creates a typed claim only after unchanged policy, owner and capacity checks.

For each active line, compute exact raw net = quantity × rate, then round once to
Core minor units using its pinned mode. Header net is the sum of those rounded
line nets. Header tax is the sum of validated entered line tax. Gross is net + tax.
Use shared integer arithmetic extracted from the existing matching primitive.
Do not round the grand raw net instead: that differs for multiple tie-valued lines.

## 9. Status / State Transitions

Capture still creates Received. Calculation evidence moves only from absent to recorded immutable v2; absence on an older invoice means legacy v1. Preview is not a persisted invoice/approval state. Matching and review retain their existing Pending, Verified, Blocked and Compared records. An unknown capture result remains unconfirmed until reconciled.

## 10. Permissions & Data Scope

Reuse `procurement-invoice-capture`, current Core company access and the actual
selected permitted branch. Retain the raw-All/resolved-All rejection before store
access for invoice writes. Purpose-scoped currency reads grant no Core admin role.
Mapping/Compare retain invoice-match authority; a new manual review still needs
its independent current Core assignee. Rounding is not invoice/AP approval.

## 11. Business Rules

Monetary precision/mode must agree with the existing matching policy when consuming v2 evidence; disagreement is a visible blocker, never an automatic policy change.

Continue bounded exact decimal-string quantity and rate inputs up to four decimals.
Quantity is positive; rate and entered tax are nonnegative. Preserve entered rate
and quantity exactly. No JavaScript `Number` calculation or decimal multiplication
that can silently lose intermediate precision is authoritative.

This feature accepts supplier-entered tax amounts; it does not derive rates or
Tax treatment. V2 entered line tax must already fit currency minor units; reject
excess precision with a clear correction message rather than silently changing a
supplier tax assertion. Automatic tax calculation or jurisdictional tax-rounding
policies require their owning Tax contract separately.

Under ZMW minor units 2 and ToEven, quantity 1/rate 5.1234/tax 0 yields rounded net
and gross 5.12, with raw net 5.1234 and adjustment −0.0034. Rate remains 5.1234.
For separate 0.005 lines, round each line using the selected tie mode before
summing; the test must distinguish this from rounding only the header raw sum.
Zero-, three- and four-decimal references must not inherit a hard-coded two.

## 12. Data Fields & Ownership

Propose one append-only `invoice_amount_calculations` child relation for new v2
captures. Its versioned envelope pins invoice/scope, currency reference identity
and revision, minor units/mode/algorithm version, exact raw and rounded amounts
per original line ID, signed adjustment, totals and original invoice digest.
This avoids adding nullable properties to every legacy invoice row merely to mark
old rows v1.

Create header, lines and calculation evidence atomically. Enforce one calculation
record per scoped invoice and exact parent FK. Validate canonical bounded envelope,
digest, complete line set, exact arithmetic and header parity in the owning store;
a client-supplied digest or preview is not authority. Referencing the existing
original digest binds actual persisted totals/rates without altering its legacy
schema-1 encoder.

## 13. Screens & Data Views

Use `reference-ui/src/features/LiveInvoices.tsx` and its exact-string input helpers.
Show quantity/rate detail independently of currency-rounded line/header totals,
with an explicit rounding adjustment/explanation. Use server preview and returned
evidence for authoritative amounts. Do not present a visually formatted 5.12 as
proof that an old stored 5.1234 has changed.

Existing live inputs allow `step=0.0001` and serialize decimal strings. The current
`invoice-values.ts` preview calculates raw products using BigInt but does not apply
currency rounding. Add a versioned v2 preview path; leave legacy preview and
historical exact-amount display available. Preserve auth/scope invalidation and
ambiguous-write recovery behavior.

## 14. User Experience / Human Behaviour

Preserve exact rate detail independently from rounded monetary totals. Explain adjustment before confirmation and keep input on validation/CAS failure. A formatted historical 5.12 must not imply that stored 5.1234 changed. Failed live reads never fall back to demo data.

## 15. Notifications & Communications

Use existing capture success/conflict/recovery messages. No supplier email, payment instruction or Finance message is introduced. A versioned event can report recorded calculation identity; it grants no approval authority.

## 16. Documents & Attachments

Manual intake remains fileless. Existing Core Documents and document/manual unit review retain ownership of real files or attestations. Calculation evidence is a structured owning record, not document verification or a fake Available/hash substitute.

## 17. Exceptions & Edge Cases

Reject unknown currency/precision, unsupported scale, stale preview, malformed/overflow amounts, missing/duplicate original lines, conflicting invoice identity and foreign/All scope. Reject excessive entered-tax precision instead of silently changing it. Processed originals remain locked and old reviews cannot transfer to a replacement invoice.

## 18. Integrations / APIs / Events / Sandbox

Use the explicit preview/capture interfaces under Entry Points; reference resolution is in-process Core ownership, not a client-supplied minor-unit assertion. Existing recorded-result recovery and typed event contracts remain versioned.

Keep current moneyScale/tolerances unchanged. A v2 amount reference inconsistent
with the selected matching scale/mode fails explicitly instead of broadening
policy. With matching scale 2 and identical rate 5.1234, its existing rounded line
product is 5.12, price variance remains zero, and a new header 5.12 can pass. Every
normal source, units, current owner/baseline and hard capacity check remains.

The existing document/manual unit-review contracts continue to pin the new
invoice's actual original digest. V2 totals do not make units verified, reuse an
old invoice's review, or bypass Core approval. Compare still creates capacity only
from an accepted source and approved owner lease. Quantity 1/net 5.12 stays below
the raw accepted net ceiling 5.1234; no acceptance amount is rounded upward.

## 19. Audit / Security / Privacy

Validate canonical bounded evidence and current actor/scope before saving. Child records are append-only under finite roles; no UPDATE/DELETE/ownership fallback is granted. Browser arithmetic and preview hashes alone never establish original value or review authority.

Audit v2 capture with its calculation identity/digest/version. Any new event has
an explicit new schema/version; old capture events are unchanged. Follow current
ambiguous-capture reconciliation by invoice identity before retry, retaining
before/after owner proof. A failed response is not evidence that header/lines/child
rolled back. No blind retries, artificial policy updates or SQL business repair.

## 20. Reports / Dashboards / Metrics

Invoice detail/exports show exact rates, rounded amounts, consumed reference revision, algorithm and adjustment. Readiness may expose missing-reference or precision-mismatch blockers. No cross-currency aggregate, Finance report or historical recomputation is introduced.

## 21. Configuration & Localization

Core provides company/default currency and explicit precision revisions. V2 capability remains disabled until its dependencies pass; legacy reads remain available. Settings changes affect later v2 captures only. Cash rounding, rate conversion and automatic tax policy remain separate.

## 22. Non-Functional Requirements

Bound inputs to the existing line/range limits and use exact scaled integers. No binary floating-point or silent decimal intermediate rounding. Keep resolution/revalidation tenant/company scoped and consistent with the actual owning transaction. No external reference, Finance or document call is necessary to calculate a manual invoice.

## 23. Postconditions

A confirmed v2 capture has one Received header, complete original lines and one immutable scoped calculation record tied to the actual original digest. Capture itself creates no Core review, invoice approval, capacity claim or payment. Existing business/security/evidence remains unchanged.

## 24. Failure & Recovery

No UPDATE/backfill of legacy invoice amounts, original lines, mappings, reviews,
Core cases, receipts, baselines, allocations or audit/outbox evidence. Absence of
calculation evidence means legacy behavior, not permission to synthesize a new
calculation. Legacy original-digest encoders and v1/v2/v3/v4 retained codecs keep
their exact bytes. Currency settings changes affect only later explicit v2 captures.

V2 write readiness requires its genuine migration, exact captured physical schema,
finite privileges and Core reference readiness. Legacy reads remain independent
of v2 write flags. Extend exact successor manifests only where the additive graph
requires it. First Down vetoes any retained v2 calculation/audit/outbox evidence
under locks; never remove evidence to satisfy a rollback test.

## 25. Acceptance Criteria

New quantity1/rate5.1234 retains its exact rate and records scale-2 net5.12 through explicit v2 evidence. With unchanged policy it can Compare only after independent unit review and all normal source/owner/capacity checks. Prove genuine schema, finite roles, legacy byte preservation, recovery and actual original-UI/native graph; SQL business repairs and policy relaxation cannot satisfy acceptance.

## 26. Test Scenarios

Test 1×5.1234→5.12 with unchanged rate, per-line versus header rounding, both tie
modes, 0/3/4 minor units, line tax precision, signed adjustment, exact overflow and
unsupported currencies. Prove tampered line/header/reference/digest rejection,
missing/extra/duplicate line rejection, stale preview, wrong actor/scope, All write
denial and legacy DTO/default dispatch. No test may merely repeat implementation.

On a new private clone, prove genuine Up/empty Down/Up, finite roles and old-row/
owner/ACL/sequence/digest preservation. Through normal cookies and the original
UI, capture a NEW v2 invoice against the existing 4dp accepted source, verify units
independently through Core, then Compare with unchanged scale-2 policy and prove
actual typed claim/ceilings/receipts. Capture all graph/audit/outbox links, flag-off
historical reads and retained Down veto. Preserve the old Blocked invoice unchanged.

## 27. Ownership / Dependencies

Core owns the shared reference/arithmetic foundation; Procurement owns intake, immutable evidence and its owner-inventory bridge; the original UI consumes authoritative preview. No new Finance service or parallel approval engine.

The new child relation must join the current owner's complete scoped inventory:
prove every calculation row belongs to a known invoice, is immutable and matches
its original digest/line set. Keep old baseline inventory/recorded digests intact;
add a reviewed supplemental relation proof for post-migration calculation evidence.
Do not silently ignore new rows or relabel an incomplete inventory complete. Freeze
this owner bridge before activation; a mere arithmetic test is insufficient.

Generate EF migration/designer/snapshot for the append-only child using the current
Procurement owner. Existing numeric columns need no narrowing for two-decimal
amounts; preserve their physical types. Install exact scoped FK/unique/CHECK/index
proof and finite SELECT/INSERT only for the actual creating/reading roles, with
no child UPDATE/DELETE/TRUNCATE or ownership fallback. No global/default grants.

**Implementation milestones**

1. Complete Core reference/editor/reader and shared exact rounding prerequisite.
2. Freeze calculation-v2 envelope/API and supplemental owner-inventory proof.
3. Implement pure calculator plus atomic intake child/guard; generate genuine EF,
   exact readiness, migration rehearsal and finite-role tests.
4. Connect original UI preview/creation/history; run focused and full SDK checks.
5. Run private new-invoice 4dp→2dp UI/API/native acceptance, preservation and rollback
   evidence; only then consider an independently approved rollout.

## 28. Open Decisions / Assumptions

Freeze exact child envelope/relation, API discriminator, reference-version resolution and supplemental inventory contract before implementation. Confirm the supported entered-tax behavior and initial currencies; automatic tax calculation is not assumed. Genuine EF identifiers and final API/permission names are fixed through normal implementation review, not handwritten migration logic.

M6C's actual Blocked comparison is a retained negative control, not a qualified
claim. A separate genuine 2dp-issued/accepted-source happy path can validate basic
matching but does not close this 4dp calculation gap or post-history correction.
This spec makes no implementation, deployment, Finance posting or full-module UAT
claim. Design precedent: ERPNext [field precision](https://docs.frappe.io/erpnext/set-precision)
separates high-precision rates from ordinary totals; its [taxes/total controller](https://github.com/frappe/erpnext/blob/develop/erpnext/controllers/taxes_and_totals.py)
keeps rounding explicit. These are references, not a dependency or code transplant.

Implementation remains part of the already authorized programme. This design record creates no new permission gate and asserts no implementation, acceptance or rollout result.

### M6D v2 capture recovery appendix

`ProcurementCommercialCommandReceipt` has a purchase-order foreign key and cannot
represent a new invoice without a PO. New v2 capture therefore owns one scoped
`invoice_intake_command_receipts` row in the same transaction as the invoice,
original lines, amount calculation, audit and outbox. Its fixed operation is
`InvoiceCaptureV2`. The caller supplies a UUID command key; the server binds the
current principal, tenant/company and canonical request SHA-256 before mutation.
One actor/key identifies at most one stored 201 response. Reusing it with changed
request bytes returns 409. Recovery rechecks current actor and invoice scope and
returns the exact retained response bytes; a foreign or missing key returns 404.
Lost or unknown POST transport outcomes are reconciled through that GET before
any further write. There is no automatic replay with a new key.

The receipt stores the invoice/calculation IDs, exact response JSON and SHA-256,
status 201 and completion time. Two scoped foreign keys, an actor/key unique index,
and an immutable save guard require exactly one receipt for each new v2 calculation
and the converse. The guard verifies response IDs, header and arithmetic against
the server-owned invoice/calculation graph. Retained owner inventory also requires
complete receipt/calculation set parity, so a missing receipt cannot qualify an
otherwise intact calculation. Existing v1 intake and PO receipts keep their old
contracts. Generate a separate additive EF migration for this fourth table; do not
rewrite the already generated three-table M6D migrations or use raw Down on data.
