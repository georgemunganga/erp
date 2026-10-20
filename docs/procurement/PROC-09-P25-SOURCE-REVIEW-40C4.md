# P25 distinct-author source review — 40c4c5a

**Scope:** Receiving persistence/API candidate `40c4c5a`, source only. Reviewer is distinct from the adapter author; reviewer authored the earlier pure receiving foundation/specification, so this is not independent certification of every inherited component.
**Status:** Changes requested before enabling. No SDK/tests/DB/browser run. Root owns runtime/migration gates; author is preparing separate corrections. Tracks [#31](https://github.com/georgemunganga/module-connect/issues/31).

## R1 — Service certification bypasses configured receiving sites

**Impact:** An assigned organisation-wide service owner with current certify permission can certify a PO in branch/site B under a rule permitting only site A.

`ProcurementReceivingCommands.CertifyService` resolves Actor with actual source branch/site but calls VerifyCurrentActor without siteId. That helper's CertifyPermission path and pure CertifyService call Guard without a site. ReceivingPolicyRules.Project sets policy.BranchId=null for multisite rules, so neither later guard checks ReceivingSiteIds for services. Core location permission is not receiving policy site authority.

**Correction:** Enforce trusted source branch/site in shared pure certification and its replay authorization, including actor.SiteIds and policy.ReceivingSiteIds; no frontend-only fix.

**Regression:** Actual mapped service source B/current actor permitted B/named owner/policy A only denies with no effect/counter/receipt/outbox; same controls/source A pass. Old-key execution replay rechecks current site authority; historical recovery retains its explicitly limited meaning.

## R2 — Current PO/Core approval pointers incompletely bind to sealed issued source

**Impact:** Resolver claims CurrentIssuedAuthority=true after Issued/current revision and Approved Core case checks, without comparing current PO scope/header/pointers to the sealed header. Existing ReceivingSourceState/receipt A can retain old sealed A scope after current PO branch drifts to B, when no branch is explicitly selected. No public issued-PO edit bypass was identified; this is an inherited/corrupt/current-source fail-closed requirement.

`ProcurementReceivingSourceResolver.Source` checks current line quantity/rate/tax/UOM but not current BranchRef/CurrencyCode/SupplierId/BuyerSubjectId, ApprovalInstanceRef or SubmittedBusinessVersion against sealed root/submission. Core query omits exact SourceVersion/policy identity/version. Pure VerifySource proves issued/submission/pin integrity, not the mutable current header it never receives.

**Correction:** Explicitly bind current stable header IDs/scope, submission/Core source version/policy/pointers before claiming authority. Do not reconstruct full Snapshot using current supplier legal/display data or recheck new-order supplier activation; valid existing obligations remain receivable after supplier profile/status changes.

**Regression:** Owner-only fixture corrupts each current header/pointer/Core version after legitimate issuance/recording. No-selected-branch command with otherwise permitted old scope blocks without effects. Valid source and suspended/renamed same supplier ID remain supported.

## R3 — AcceptanceHistoryVerified omits inactive/malformed controlled history

**Impact:** Unknown accepted history can be interpreted as zero and authorize another acceptance.

Ledger selects only active PO lines for the source PR before collecting legacy receipt/service facts and comparing counters. Inactive historical line acceptance is omitted. Service legacy check flags only ExecutionVersion!=1; a malformed v1 certified row without valid state binding escapes the symmetric unknown check and is absent from Facts(state). Candidate service check permits certified_at with null receiving_source_state_id if other label fields exist.

**Correction:** Audit all historical PO lines/facts for the PR identity, including inactive rows. Distinguish draft intended quantity from certification. Certified/accepted v1 rows require exact state/source/issued/certification-effect binding; missing binding is Unknown, blocking Balance/certification. Strengthen database/Added guards where feasible. Do not fabricate reconciliation deltas. Physical held evidence may remain recordable.

**Regression:** Owner-only fixtures add legacy accepted receipt/service on inactive same-PR PO line, and v1 certified service missing/invalid state. Physical recording/inspection may retain truth, but acceptance/Balance block without effects. Valid complete histories across legitimate PO allocations remain usable.

## R4 — Inspection rejection does not complete the operational disposition view

**Impact:** Received 2 / qualified 1 / accepted 1 remains Partially Accepted indefinitely; inspection-rejected 1 is not reflected in derived rejected view.

GoodsAction derives Accepted by comparing accepted with received minus initial rejection, ignoring final inspection disposition. RejectedQuantity retains initial rejection only. Immutable facts are sound, but work queues/reporting can imply another unit remains available for acceptance.

**Correction:** Derive qualified/inspection-rejected/still-held/disposed quantities from immutable observation/inspection and gross positive certifications. Preserve received/initial rejection; no return/reversal reuse of physical authority. Expose separate current totals.

**Regression:** Received 2 / qualified 1 / accept 1 shows accepted 1 / rejected 1 / held 0 with complete disposition. Received 2 / qualified 2 / accept 1 against authority 1 shows accepted 1 / held 1 exception, not completion/stock/extra acceptance permission. Rejected-only observation stays explicit.

## Controls confirmed in source (runtime proof pending)

- Two receiving tables only; existing receipt/inspection/service/return masters and matcher IDs reused.
- Shared coordinator acquires Core tenant session fence before BEGIN, one SERIALIZABLE connection, bounded full-command retries. Current canonical identity/permission/branch checks precede exact replay; accessible scope precedes worklist count/page.
- Truthful over-delivery does not certify stock. Positive certification enforces physical qualification/gross-positive usage and sealed source capacity; worker independence uses canonical identities.
- AcceptanceHistoryVerified is separate from structural completeness; R3 closes omitted-history paths. Context guards and composite relationships bind final evidence, counters and sequence.
- Intermediate SaveChanges remains inside shared transaction; coordinator subsequently persists command receipt/audit/outbox and commits atomically.
- No negative runtime commands/fake consumption provider. Receiving policy is explicitly independently published with approved controlled-manual references, no implicit policy/inspector/success acknowledgement.
- Matching authority remains explicitly false; no AP/Inventory/Assets execution is claimed.

## Remaining gates

Review correction commit, compile/full tests, additive P25 migration/model check, restricted real PostgreSQL lifecycle/FK/immutability/multiline rollback/concurrency/fence/replay probes, purpose-specific inspection reads and existing UI adapter, actual API Playwright, policy/UAT/operations/release approval. No completion/production activation is implied.

## Correction follow-up — e2049ab

Distinct adapter review of `40c4c5a..e2049ab` finds all four identified source issues addressed:

- R1: shared Guard requires service source branch/site in actor.SiteIds and policy.ReceivingSiteIds, so both factory and current exact-replay authorization enforce it.
- R2: ReceivingIssuedAuthority verifies current stable PO header/full active commercial snapshot against retained issued JSON using retained supplier labels, plus exact Core source/case/policy/submission pointers and historical effective period. It does not reauthorize current supplier activation/Finance availability.
- R3: Ledger loads inactive historical PO lines and all associated receipts/services, then uses explicit ReceivingAcceptanceHistory. Malformed certified service binding blocks; final service Added/context/SQL guards require its positive effect/state. Existing fact/counter/sequence validation remains.
- R4: inspection updates derived rejection; certification compares against qualified quantity. Detail exposes inspection qualification/rejection and held quantity, and multi-line response projection preserves other lines' retained facts. Original physical observation stays frozen.

The additional source-state/effect guard requires a changed source sequence for every added effect, and acceptance also checks approved PR capacity. No new blocker was found in this correction diff. This closes the four **source review findings only**; authored tests/probes, EF model/migration and actual runtime/UI behavior remain unexecuted/unproved by this reviewer. Source-only positive receiving remains off by default. Root retains independent deployment/UAT gates and exact-input probe changes when consolidating.
