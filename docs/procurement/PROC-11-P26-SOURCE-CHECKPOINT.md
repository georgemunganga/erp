# P26 source implementation checkpoint

**Status:** Incremental source authored on corrected P25 e2049ab; no SDK/build/tests/DB/browser/migration/deployment. Full adapter is still in progress. Plans: PROC-11-P26-INVOICE-MATCHING-ADAPTER and IMPLEMENTATION-PLAN. Issue30 remains open.

First source slice adds existing-record matching metadata plus three justified Procurement records (immutable mapping version, commercial allocation, exposure checkpoint), pure original-intake/mapping hashes and exact line projection, explicit unavailable Finance-history and original-unit-review interfaces, and pure qualified-claim guards. Missing production owner/reviewer returns Unavailable, not complete/verified zero. Clients cannot submit those authority flags. No code in this slice sets Approved, AP-ready, Posted or Paid.

Original invoice amounts/lines remain untouched by mapping. Operational row-version bumps do not change the original digest; original business changes do. Declared mapping unit is not verified UOM unless an actual owner review binds every line/document. Qualified claims require complete current owner evidence and a passed comparison; accepted capacity remains a hard claim bound even if diagnostics permit quantity variance. Existing aggregate variance columns become nullable rather than filling cross-UOM unavailable aggregates with fabricated zero.

Next source slices: exact P25/source/policy/identity resolver, partial EF mappings/guards and constraints, governed match-policy lifecycle, default-off API mapping/compare/read/recovery and authored actual HTTP/PostgreSQL denial probes. Positive operational proof still requires actual Finance history and unit-review adapters; no fake successful provider will be shipped.

## Independent pure-contract review follow-up — source only

Independent review `ab1e6f5` identified that the first helper slice trusted weak mapping-review,
owner completeness and previously compared input bindings. The follow-up changes the public
pure contracts before any production comparison adapter is wired:

- Verified mapping review now requires a typed tenant/entity/branch, current principal/worker,
  governed policy ID/hash and evaluation-time context. Its evidence binds the exact immutable
  mapping ID/hash, original invoice digest, issued PO revision and every original document
  version/hash/line-reference/unit. An unverified proposal still exposes no verified unit.
- Owner coverage now binds exact branch, policy hash, lease expiry, owner/watermark and a
  canonical digest of the actual returned allocation set. Missing, malformed, duplicate-key
  or unsupported evidence fails unavailable rather than throwing or establishing empty history.
- Complete history binds actual still-active local allocation/invoice pairs to the source
  exposure checkpoint. Owner/local overlap requires explicit reconciliation; this slice
  does not invent a deduplication or claim-release protocol.
- Claim creation takes the current original invoice/lines, current mapping, verified review,
  original command/scope and actual active local history. It compares the exact complete-history
  watermark and digest, recomputes the foundation result from the supplied current input, and
  pins the persisted result's scope, original/version/mapping, actor/worker, source versions and
  policy identities. It creates claims from the recomputed lines, never detached prior line objects.
- A separate receiving source-state binding is retained on each typed acceptance claim for the
  planned exact composite persistence foreign keys. It is not another receipt entity.

Synthetic positive and cross-binding denial tests are authored in
`InvoiceMatchExecutionEvidenceTests`; they prove no production owner configuration. The only
provided owner/reviewer adapters remain explicitly unavailable. No SDK, database, migration,
HTTP, browser, deployment or activation verification has run for this follow-up. The uncommitted
persistence slice and remaining runtime wiring are separate work and remain incomplete.

A second distinct review identified a residual same-ID policy substitution gap. The next source
correction derives `InvoiceMatchingPolicy` from the actual independently published owned
`PolicyVersion` using a strict `INVOICE_MATCHING` schema, checks that full projection against the
comparison input, and derives every review/owner/result policy hash from its actual rules JSON.
No arbitrary matching engine or parallel policy table is introduced. Each rule explicitly sets
method, acceptance kind and all four tolerance pairs; no tolerance/threshold is inferred.
The review context also supplies the expected immutable Core document-version hashes, which
must equal the reviewed version/hash pairs. Adapters still own authentication of these sources;
client mapping input cannot supply either authority. Same-ID changed rules, changed projection,
invalid publication independence and changed document hash have authored denial regressions.
These source changes remain unexecuted and the governed matching-policy HTTP extension is pending.

## Persistence mapping slice — source only, no P26 migration generated

The next partial maps the three planned owned evidence records onto the existing Procurement
context. Same-invoice composite foreign keys bind mapping/result/current pointers, exceptions
and original invoice lines. Claims bind exact issued PO/line, exposure state and (when used)
P25 receiving source-state plus typed goods/service record. Batch sequence uniqueness includes
the invoice line because one comparison can atomically claim multiple original lines.

Mapping and claim rows are append-only. Original intake records still create normally; material
invoice/header line overwrites are denied. Operational mapping/comparison pointers require their
new immutable fact; a live claim cannot be removed or replaced without a future authenticated
owner-resolution contract. Exposure sequence changes require a matching added immutable batch.
There is no inferred AP liability, payment or Finance balance. Nullable variance fields preserve
unknown/incomparable legacy variance rather than manufacture zero.

Model/denial test source is authored in `InvoiceMatchingPersistenceTests`. It is not evidence of
PostgreSQL constraint enforcement; migration generation, real FK/rollback/numeric rehearsal,
restricted runtime-role tests and actual mapped HTTP concurrency/replay gates remain required.

The matching policy schema now plugs into the existing configuration policy routes for
validation, draft/review/publication/return and effective-policy lookup. The same current
canonical worker independence helper used for receiving configuration applies; real company
currency and active category references are checked. Preview remains explicitly non-authoritative
and requires issued, document-review and owner-consumption evidence. No matching policy is seeded,
published or enabled by these source changes. Policy API integration tests and independent review
of the extension remain pending alongside the existing release gates.

Distinct persistence review identified and corrected three additional source gaps before runtime
wiring: every added claim batch must conversely advance its tracked exposure state (new state
sequence one or existing state CAS exactly plus one); even a Blocked result must bind the current
mapping/original invoice version/digest and exact PO/issued source through a composite foreign key
and context guard; and original invoice lines may be inserted only with a same-save new intake
parent. The original captured line set cannot grow alongside a mapping. Authored model/context
denials cover these cases. SQL constraints/concurrency and positive mapped execution remain
unverified; no migration has been generated or production settings changed.

## Actual route adapter checkpoint — authored source, not verified/activated

The new route partials compose into the existing host and shared serializable transaction:

| Route | Permission | Meaning |
|---|---|---|
| `POST /invoices/{id}/matching/mappings` | `procurement-invoice-capture` | Persist a complete immutable, unverified line/source/document proposal; preserve original intake. |
| `POST /invoices/{id}/matching/compare` | `procurement-invoice-match` (explicit grant) | Persist actual comparison evidence/exceptions; a qualified capacity claim additionally needs trusted current review, owner coverage and reconciled P25 acceptance. |
| `GET /invoices/{id}/matching` | `procurement-invoice-read` | Exact-string original amounts, mapping/history, held-claim pointer and assigned discrepancies; recorded state is not current approval. |
| `GET /invoices/{id}/matching/commands/{key}?operation=...` | Current recorded operation permission | Actor-owned durable historical recovery after current canonical identity and actual invoice/target-PO record scope checks. |

All are closed by default (`Procurement:InvoiceMatching:Enabled=false`) and require the explicit
least-privilege execution connection, no pending models/migrations, P25 schema and exact required
tables/privileges. New matching permission has no role-bootstrap grant. Existing invoice intake
and unrelated drafts keep their established routes. The legacy final validation/AP capability
remains false; `invoiceComparison` is a separate configured adapter capability.

`ProcurementReceivingInvoiceProjection` reuses the existing sealed issued-source resolver and
complete historical ledger reconciliation in a new partial. It does not call the effective-now
receiving resolver or create state. It verifies retained classification-policy identity/hash and
uses `ControlledReceiving.Balance` for accepted-minus-reversed and separately dispatched-return
quantities/net values. Unknown historical acceptance cannot become a qualified source. A truthful
mapping draft can still be saved without pretending that a declared acceptance is certified.

The only installed original-unit-review and historical Finance owner adapters remain unavailable.
Comparison therefore persists Blocked with owned exceptions and no capacity claims when those
owners are missing; no client completeness, reviewed-unit or Finance balance flag is accepted.
Immutable available Core document versions for the same authorized invoice supply expected hashes
when Core Documents is configured; absent/unavailable versions cannot verify declared units.
Comparison/outbox/exception/current pointer and durable command receipt commit together. Existing
active claims cannot be overwritten or released by remapping/recomparison.

Mapped gate/permission tests are authored. Disposable PostgreSQL probe `--matching` builds on the
actual receiving fixture, then exercises manual intake, immutable mapping, absent policy rollback,
independently published actual matching policy, unavailable-owner blocked comparison, exact retry,
changed-body/unauthorized denial, historical recovery, owned exceptions/outbox and zero fake AP or
claims. It deliberately installs no fake successful Finance owner/reviewer. Other CLI modes remain
mutually exclusive and unchanged. New finite runtime grants include no UPDATE/DELETE on immutable
mapping/result/allocation evidence; their denial probes are included only for `--matching`.

**Remaining gates:** independent route/bridge review; source compile and full relevant tests;
P26 migration/SQL/model review; raw FK/unique/precision/rollback and CAS PostgreSQL rehearsal;
restricted-role actual `--matching` HTTP execution; current scope/revocation and race scenarios;
compatible existing frontend wiring/browser checks; real owner/document-review contracts before
qualified claims can be enabled. This source is not deployed, migrated or production complete.

Distinct route review found three concrete issues before compilation. Ordinary invoice history
now uses a filtered read projection: only its own original-line comparison/discrepancy results
and opaque evidence/source watermarks are exposed. Full immutable evidence containing other
invoices' billing allocations and unrelated PO lines remains server-side. A regression embeds
unrelated private source facts and proves they are absent from the projected transport.

Core document hash reads now require explicit identical single server/port/database settings
between the configured Core Documents owner store and the execution connection. Different
finite credentials are allowed; separate/ambiguous stores return unavailable pins instead of
reading guessed local tables. Store-binding positive/denial tests are authored. Direct PostgreSQL
serialization/deadlock failures are classified409 before the generic database503 fallback.
All remain source-only; these corrections confer no release or production owner authority.

## Source consolidation — root947ff04 + reviewed P25 reads d44be49

P26 now retains both histories through ordinary merges, without rebasing/force-pushing:

- `6248385` merges root `947ff04`, including the verified exact-input/tax guards and actual
  `OrderExactInputVerification` call, exact codec/converters, mapped input tests, PO browser
  fixture and `f9fca21` PO-only policy-role correction. Root verification evidence remains
  historical evidence for that source; it does not verify this combined P25/P26 branch.
- `b9499fa` merges reviewed P25 read source `d44be49` (including `2d8b35f` service notes and
  disabled receiving metadata-fixture setup). Full source/read/purpose/legacy/preview behavior
  is retained, not reimplemented by the P26 bridge.

Conflict resolutions were confined to probe CLI validation, additive probe README sections and
metadata test registrations. The CLI keeps the explicitly supported `--serve --orders` pair,
all original single modes/no-argument PR mode, and the new single `--receiving`/`--matching`
modes. Duplicate flags, unsupported mixed modes and more than two flags remain rejected.
Browser preparation receives the actual `orders` flag and retains optional persisted outcome
verification on graceful stop. Both capability fixtures have exactly one disabled Receiving
and one disabled Matching registration.

Source comparison confirms root order drafts/contracts/codec/source-allocation/exact-input and
browser fixture files unchanged; `OrderVerification` differs only by the existing optional
`purchaseType` argument needed by receiving. Reviewed P25 resolver/read-scope/receiving probe
files match their branch exactly. Finite runtime grants preserve existing mode behavior and
add P25/P26 grants only behind their explicit private probe modes.

Distinct route review `99df2ab` records source closure of the `3b97559` privacy/store/abort
corrections. Authored tests from both histories are retained, including mapped raw input,
amount/tax denials, source allocation, receiving read scope/legacy/notes, P26 evidence/CAS,
filtered read privacy, store binding and disabled mapped endpoint gates. **No SDK/build/test,
P25/P26 migration generation, database or browser execution was performed for consolidation.**
Root's centrally generated P25 snapshot is intentionally not invented here; it must be merged
later before the separate P26 generation/rehearsal gate. Production remains unchanged.

## P25 central compile and independent service-note follow-up consolidation

Source-only follow-up merges preserve ancestry without force operations:

- `0437de2` merges root `f9c15b9`, retaining central P25 integration `ee4d229`, actual
  compile correction `bfc12e1` (`new HashSet<Guid>()` for the required `IReadOnlySet<Guid>`),
  typed audit recovery veto and historical settings-browser acceptance record.
- `9380113` merges independent additive service-note correction `6b574d7`. The existing
  `ServiceAcceptance` gains nullable max2000 Notes, while legacy Reason/cancellation text
  remains separate and unchanged. Draft/detail/cancel-preservation tests and probe scenarios
  are retained. Purpose-only tasks omit both notes and cancellation reason. No Reason backfill
  is inferred. Candidate Down rejects any new Notes as well as typed receiving audit evidence.

Conflicts involved additive probe CLI/docs from the separately integrated histories and the
candidate Down/plan's added Notes predicate. Supported `--serve --orders`, all original modes,
new single receiving/matching modes and duplicate/mixed-mode rejection remain intact. Exact
root input guard/codec/browser files and the HashSet fix match `f9c15b9`; service Notes model,
context mapping, draft/read and probe files match `6b574d7` exactly. Both disabled fixture
option registrations remain once each. No centrally generated migration/snapshot is created
or replaced by these merges.

### P26 evidence impact of ServiceAcceptance.Notes

Notes are nonfinancial draft annotations, not source identity or accepted entitlement. P26's
original-invoice digest hashes only original SupplierInvoice/Line fields. Its issued source
binds the sealed PO commercial snapshot and PR lineage; accepted authority binds retained
receiving classification and immutable acceptance effects, exact deliverable/period/unit/
quantity/net amounts and certified worker/policy/evidence. None derives authority from Notes
or cancellation Reason, before or after this correction. The ledger/effects digest and P26
projection therefore require no schema-version/hash rebinding for Notes. Existing published
or captured records are not silently recalculated or backfilled.

Changing Notes while a service is still a draft does not create an acceptance or invoice
claim; after certification the existing `ServiceCertificationImmutable` context guard already
denies a Notes edit. Cancellation preserves Notes and does not manufacture a negative effect.
Consequently no new Notes field enters a matching or Finance completeness contract, and the
purpose-read privacy exclusion remains appropriate. The P25 migration must still add this
nullable existing-table column, and runtime tests must verify draft max-length/cancel behavior.

No SDK/build/test, database, migration generation or production enable occurred in this source
consolidation. Root's reported central P25 test results belong to its tested checkpoint; the
combined P26 candidate still needs its own gates. The sourceVersion read follow-up and central
P25 schema snapshot will be merged separately when available.
