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
