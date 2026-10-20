# P26 distinct source review — routes, evidence reads and owning stores

**Review scope:** source checkpoints `e2903db` and correction `3b97559`; persistence
`ac7e146` with corrections `990dffc` / stronger regression `ed59afa`.
**Reviewer:** distinct Procurement agent from the author; root controls centralized gates.
**Evidence:** source inspection only. No SDK, build, database, SQL, migration, HTTP,
browser, server or production jobs performed by this review.

## Findings and closure

| Finding | Concrete exposure / integrity gap | Source correction | Review conclusion |
|---|---|---|---|
| Ordinary read exposed raw comparison evidence | `ProcurementInvoiceMatchingReads` parsed the whole EvidenceJson into invoice-read response. Foundation evidence contains complete source PO/acceptance and owner/local consumption allocations, including other invoice identities, amounts and owner references. | `3b97559` introduces `InvoiceMatchReadProjection.Summary`, returning only current original-invoice line comparisons/discrepancy codes and opaque digests. Raw evidence stays in immutable server records. | Closed at source level. The privacy test embeds foreign invoice/line IDs and private owner/terms strings and asserts they are absent. Own mapped PO/acceptance IDs remain necessary context; no other source allocation list is returned. |
| Document hashes came from an implicitly guessed store | The route created Core DocumentDbContext on the execution connection while ignoring the configured CoreDocuments store. A separate configured owner could produce false unavailable errors or read a stale local lookalike schema. | `3b97559` requires explicit single-host/same-port/same-database configuration binding before using the shared transaction. Separate, multi-host, malformed or missing configuration returns unavailable hashes; different finite usernames are allowed. | Closed for this bounded co-located-store implementation. This is conservative declared store binding, not an assertion that a hash or connection string authenticates document content by itself. Current scoped Core metadata ownership/Available scan state, invoice read access and trusted reviewer provenance remain required. A separate store still requires a real owning reader. No credentials are returned. |
| Direct PostgreSQL abort mapped as unavailable | The generic Npgsql503 branch preceded the confirmed40001/40P01 classification. | `3b97559` checks the base PostgreSQL abort and EF concurrency before generic Npgsql503. | Closed at source level. Actual nested/direct provider behavior still needs the centralized HTTP/Postgres gate. |
| Added claims could reuse an unchanged exposure sequence | Equality with the current sequence did not require source-state CAS advancement. | `990dffc` requires one invoice/result batch and a new state at sequence1 or tracked existing state advanced exactly one. `ed59afa` supplies an otherwise valid positive authority graph for the denial. | Closed at source level; concurrency/constraint proof unrun. |
| Comparison result mapping and issued source could diverge | Mapping/invoice and issued/PO FKs were independently valid; a Blocked result lacked current original-source guard. | `990dffc` strengthens the composite mapping/result FK with invoice/PO/issued identity and validates current original digest/mapping/version for v1 results, including Blocked. | Closed at source level; real FK/migration proof unrun. |
| Original intake line set could grow after capture | Added lines to an already persisted invoice bypassed the Modified-only guard and could be excluded from the same-save mapping digest query. | `990dffc` permits new lines only with their same-save Added intake parent. | Closed at source level; original capture remains supported and later corrections require a separate reviewed contract. |

## Other reviewed controls

- Routes require existing explicit capture/match/read actions, resolve an active canonical
  account and unique worker, and enforce current Core branch access. Mapping/compare/recovery
  check both invoice scope and the actual target PO, including branch/supplier/currency/entity.
  A UI role label is not a financial grant or a server permission.
- Command and recovery UUIDs bind actor, operation, full fingerprint, invoice and actual
  target order. Historical responses explicitly deny current approval/AP authority. Current
  canonical access and record scope are checked before recovery; no new claim is allocated
  from the saved response. Re-evaluation/owner freshness is not inferred from historical success.
- The historical receiving bridge reuses the existing sealed Source, Facts and complete Ledger
  reconciliation. It never calls state-creating Resolve. Its calls remain compatible with
  receiving's additive optional parameters because command/default semantics are unchanged.
  It loads inactive related source history and preserves unknown-as-unavailable semantics.
- Lock composition inspected as upstream PR → PO → relevant receiving states → invoice exposure
  → invoice, inside the shared transaction/Core branch fence. Source, claim, invoice pointers,
  audit, outbox and durable command receipt commit atomically through the existing coordinator.
  No transport call is introduced in a locked transaction by the installed unavailable adapters.
- Mapping proposals cannot verify original document units. The only installed reviewer and
  historical Finance exposure owner return unavailable. Missing evidence produces an actual
  durable Blocked comparison; it does not fabricate complete zero history or a positive claim.
- Comparison pass/block is separate from invoice approval, AP posting, payment and notification
  delivery. Positive owner/reviewer adapters, claim release, credits, AP handoff and owner
  reconciliation remain declared unsupported dependencies, not hidden successful stubs.
- Three justified Procurement matching evidence/state records do not replace existing invoice
  intake or create an AP/Finance ledger. New claim money uses38,8 with exact CLR bounds and
  quantity uses18,4; storage precision is not a promise of arbitrary38-digit CLR values.

## Remaining gates and limits

This review does not establish compilation, EF model compatibility, migration safety,
restricted-runtime privileges, actual provider retries, HTTP concurrency or browser behaviour.
The author retains source-authored tests/probes; root must verify them on the consolidated
P25/P26 model, retain first failures and complete the required protected migration/recovery
matrix. Production matching remains default-off and no real positive Finance/document owner
integration is claimed. New findings in those gates must be addressed before activation.
