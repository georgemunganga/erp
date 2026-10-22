# PROC-AUTH-WORKER-01 — retained people behind spending authority

Status: implementation in progress, not released. Owner: coordinator/backend. Manus remains assigned only to the merged-main vendor milestone. Development starts from held integration `f571d6e`; this is not a reason to merge or activate P20–P29.

## 1. Feature identification

Procurement → Governance → Spending authority configuration → retain original maker/recipient/reviewer identity across Core account changes.

## 2. Purpose and outcome

A changed login-to-employee link must not transfer financial authority or let the original maker become their own reviewer through another account.

## 3. Scope and exclusions

Creation, submission, activation, author checks and PR/PO authority eligibility. Preserve historical read/revoke paths. Core owns accounts and employees. Receiving/invoice-matching policy-maker evidence is a separate identified gap; this change cannot close that hold by itself.

## 4. Related features

Existing authority configuration, PR approval assignment, PO execution, future sourcing-publication preparation, immutable audit and Core canonical workforce resolution. Reuse the corrected SOURCING policy identity approach without changing its historical payloads.

## 5. Actors

Canonical active configuration maker, recipient and independent reviewer; historical people may later be offboarded. Roles do not imply financial limits.

## 6. Entry points

Existing server-authorized grant commands and eligibility resolvers. Evidence is never accepted from a public request body.

## 7. Preconditions

Correct tenant/company scope; current actor and recipient resolved from Core under the command transaction; existing business/permission/version checks; owning evidence store ready. Missing legacy identity is unknown, not reconstructed.

## 8. Workflow

Create draft with observed maker/recipient → retain Created evidence → edit only as original person → submit with exact business fingerprint/version → retain Submitted evidence → independent original-worker check → activate and retain reviewer evidence → eligibility verifies retained recipient against current person.

## 9. States and transitions

Existing Draft/PendingReview/Active/Revoked/Superseded states remain unchanged. Evidence stages Created/Submitted/Activated follow the business transition atomically. Returned and resubmitted drafts require a new submission round; old evidence stays intact.

## 10. Permissions and scope

Existing named configuration/review/revoke and execution permissions remain required. Evidence adds guards; it grants no action or data scope. No role, administrator flag or read projection bypasses original-worker independence.

## 11. Business rules

- A new login representing the original maker, submitter or recipient cannot review.
- Rebinding the maker account does not transfer draft authorship.
- Rebinding the recipient account does not transfer an active grant.
- Historical maker evidence does not require reactivating an offboarded account.
- Missing/corrupt/stale/wrong-scope evidence denies protected work.
- Legacy grants remain readable and authorized-revocable, but cannot be edited/submitted/reviewed/superseded or selected for new execution without original evidence. No automatic backfill.

## 12. Data and ownership

Procurement-owned immutable envelope: schema version, evidence ID, tenant/company/grant stable ID, authority reference/version, permission/action, configuration owner, observed recipient principal/worker, action actor principal/worker, record version at action, exact existing business fingerprint, normalized action time, Created/Submitted lineage IDs and canonical digest. Worker/principal masters stay Core-owned. A digest is integrity evidence, not authentication of an arbitrary supplied object.

## 13. Screens

Reuse existing authority screens and server-computed action availability. Legacy records show an actionable historical-identity explanation. No replacement vendor UI.

## 14. User experience

Use plain language: create a new reviewed authority version when original identity cannot be established. Preserve the old record and entered values on denied/stale actions.

## 15. Notifications

Existing audit/notification contracts; rejection creates no successful-transition event.

## 16. Documents

No new upload or document workflow.

## 17. Exceptions

Account swaps, aliases, recipient rebind, offboarding, return/resubmit, version changes, malformed lineage, missing store, unexpected evidence, concurrent identity updates and legacy rows.

## 18. Integrations

Consume Core canonical identity inside existing coordinated transaction/fence. PR and PO resolvers validate recipient proof before returning a grant. Future sourcing preparation must consume the same trusted evidence before activation. Generic amount/scope calculations alone remain non-authoritative.

## 19. Audit/security/privacy

Append-only evidence in the owning scoped store, stable composite grant FK, no FK to mutable grant Version. Capture Created proof against the initial same-save Draft fingerprint and server-observed workers. Preserve existing financial fingerprint and sealed PR/PO evidence bytes. Historical revoke/supersede readers must not incorrectly require current Active state.

## 20. Reports

Operational report of grants with unknown original identity and blocked changes, scoped to authorized governance staff. No fabricated recipient history.

## 21. Configuration

No thresholds, limits, currencies or approved policy defaults are introduced.

## 22. Non-functional

Atomic grant/evidence/audit writes; bounded indexed lookups; current Core identity checks; isolated tenants; migration/readiness compatibility and observable failures.

## 23. Postconditions

A successful protected transition has exactly matching persisted business and identity evidence. A denied attempt cannot mutate grant, evidence, audit or outbox footprint.

## 24. Failure and recovery

Rollback the whole command on identity mismatch, stale version or evidence failure. Reconciliation is read-only after uncertain network outcome; do not repeat activation automatically. No evidence adoption from current mappings.

## 25. Acceptance

Original maker/reviewer worker swap is denied; recipient rebind is denied for new PR/PO eligibility; legitimate independent current reviewer succeeds with original maker offboarded and valid proof. Missing/corrupt/foreign/stale evidence fails. Existing historical read/revoke recovery remains available.

## 26. Test scenarios

Pure envelope/version/fingerprint/worker/lineage tests; append-only persistence and stable FK tests; actual Core account swap and grant consumer HTTP/PostgreSQL tests; denied-command whole-footprint comparison; schema readiness and legacy upgrade; actual browser authority workflow after backend gates. Mocked UI or a pure hash test is not persistence/authentication proof.

## 27. Ownership and implementation checkpoints

| Stage | Work | Exit evidence |
| --- | --- | --- |
| A1 — Pure evidence | Immutable typed envelopes and origin/submission/activation guards | Focused unit tests; unchanged financial fingerprints; independent source review |
| A2 — Persistence | Append-only scoped owning table, lineage/uniqueness constraints, additive migration and readiness | Persistence tests, generated migration/snapshot, private upgrade/rollback checks; no legacy backfill |
| A3 — Commands and consumers | Server capture, original-author/reviewer guards, truthful read hints, PR/PO recipient checks and future sourcing boundary | Mapped HTTP tests plus current-identity and legacy denial cases |
| A4 — Actual integration | Genuine Core worker swaps, offboarding, concurrency and denied footprint proof | Isolated real API/PostgreSQL evidence and browser workflow; no production writes |
| A5 — Review/release | Independent review, shared-host compatibility, reviewed baseline merges and controlled release | Existing P20–P29/receiving-policy holds resolved explicitly; release evidence, backup/rollback and migration readiness |

No stage is complete because a helper compiles. Publish reviewed prerequisites before assigning dependent work; no Manus changes to these backend files.

## 28. Open decisions and inherited holds

A1 is the first bounded implementation. A2–A5 are pending; current production and flags remain unchanged. Receiving/INVOICE_MATCHING historical-maker evidence needs its own correction. Existing approvals/Finance releases and retained HRM compatibility gates remain required. No automatic activation or retrospective policy approval is implied.

### A1 verification — 21 October 2026

Pure evidence foundation implemented. Fourteen new identity cases and 69 existing configuration/PR/general-spend cases passed: **83 passed, zero failed/skipped**. Executed in the existing .NET10 SDK image with one CPU/768MiB, no database connection or production mutation. Initial compile introduced two nullable warnings; explicit timestamp guards removed them and the same 83 cases passed again. Existing GovernedRfqDraft nullable warnings remain outside this slice. Logs/TRX: `artifacts/authority-identity/domain.log` and `domain-corrected.log` with result files beside them.

Independent read-only source review found no concrete blocker in A1. This proves pure envelope/worker/version/fingerprint behavior, not actual Core swaps or persistence authenticity. In particular A2 must bind Created proof to the initial same-save draft and trusted observed workers, and historical revoke/supersede handling must not incorrectly call the Active-only verification path. A2–A5 and the release hold remain open.

### A2 persistence and migration checkpoint — 21 October 2026

Typed `SpendingAuthorityIdentityRecord` storage, lossless converter, strict same-save guards and generated migration **`20261021114959_P30SpendingAuthorityWorkerIdentity`** are implemented. The snapshot adds only the new entity/relationships (+198 lines); old business fingerprints, keys and financial columns are unchanged. `IProcurementScope.WorkerId` defaults to null for old/read-only scopes; new evidence requires the actual nonempty worker. Creation metadata is preserved only for a validated same-save initial grant/evidence pair, avoiding fingerprint drift from the generic creation stamper.

The combined targeted suite passed **107/107**, zero failed/skipped, including 14 pure identity cases, 16 new owning-store/model cases and existing authority regressions. InMemory proves application guards, not physical PostgreSQL constraints.

The separate disposable PostgreSQL probe passed against the private Unix socket with networking disabled. Evidence: `artifacts/authority-identity/private/20261021_120345/`. Verified:

- P29→P30 upgrade, empty Down/re-up and a legacy grant unchanged without identity backfill.
- Actual create/submit/activate, three retained action rows, database reload and fingerprint verification.
- Named owner/Created/Submitted foreign-key denials, uniqueness and empty-worker CHECK denial.
- Append-only application guard; a used Down rejected; actual generated Down also rejected when action rows were removed inside a rollback-only owner transaction but their audit history remained.
- Whole retained grant/evidence/audit/outbox/migration-history fingerprint unchanged after all denials; final snapshot has no pending model change.

Down locks grant/evidence/audit tables in ACCESS EXCLUSIVE mode before testing retained evidence or `SpendingAuthorityIdentityRecord` audit rows. It never silently drops used history. No identity outbox event names were invented.

Initial runner/build failures remain retained: initial EF relational version conflict (explicit 10.0.11 pin fixes it), password/TCP assumptions incompatible with the existing isolated trust/socket rehearsal server, an incorrect lowercase EF history-column name, and an expected retention SQL55000 wrapped by EF before the probe handled that exact state/message. The final probe builds with zero warnings/errors. Private authentication/network settings were not changed; each actual run used a fresh synthetic database and failed databases remain available for inspection.

This proves owner-role persistence and migration behavior only. The schema/readiness provider, canonical Core capture, guarded API action hints and PR/PO eligibility integration remain A3; actual Core swaps, finite runtime-role/HTTP/browser evidence remain A4. Historical grant/policy holds and production P19-compatible image/flags are unchanged. No production data, identities or schema were touched.


## A3 command and consumer integration — 22 October 2026

Implemented on the held development branch, not released:

- The owning-store bridge reads persisted typed evidence with explicit tenant/company/grant scope. Attached or client-provided envelopes cannot establish historical identity.
- Create records observed canonical maker/recipient workers. Submit and Activate append their exact identity stages in the same grant/audit transaction. Edit/Submit retain original-person authorship; Return/Activate/Supersede enforce original-worker independence and exact pending submission lineage. Supersede requires retained activation proof for its predecessor; legacy unknown authorities require authorized revocation instead of adoption.
- Existing PR and PO resolvers evaluate complete authority families before validating the selected grant's retained activation and current recipient worker. Missing identity never filters an overlapping grant out or silently selects a different authority.
- Server action hints use the same identity rules and actual recording readiness. List readiness is inspected once per request. Historical views and authorized Revoke remain available when identity capture is unavailable; unknown legacy records advertise no protected new-work actions.
- Current Core actor resolution rejects a worker mapping that differs from the one resolved for the request. No scope replacement, fake employee, Core UPDATE privilege or administrative financial allowance is introduced.
- P30 readiness checks its actual owning connection/current transaction, applied history, model, mapped fields, exact PostgreSQL17 CHECK/index predicates and scoped typed FKs. Read and recording grants differ. Evidence/audit/outbox stay append-only. Core identity writes, protected table/schema/database ownership and assumable elevated roles deny readiness, even after ordinary DML ACLs are revoked.
- AddScoped rejects foreign identity evidence before stamping or tracking; it cannot relabel another company's original evidence.

Verification so far: **95 host tests and 108 domain/persistence cases passed, zero failed/skipped**, including the new identity/schema cases and affected Procurement scope/approval/order/receiving/invoice/sourcing access gates. The first 38-case run is retained separately; review then identified and fixed the owner-ACL bypass. Source review confirmed the ownership fix. One new nullable warning was corrected; unrelated pre-existing host/analyzer warnings remain visible in logs. Evidence: `artifacts/authority-identity/a3/` (source hashes, logs and TRX).

The standalone private finite-role mapped-HTTP rehearsal is being implemented separately. It does not replace actual operational PR/PO recipient-rebind, concurrency or browser evidence. The future SOURCING publication preflight remains non-operational; original sealed evidence is unchanged. RECEIVING/INVOICE_MATCHING historical-maker holds remain separate.

Concurrency contract remains the existing coordinated SERIALIZABLE snapshot, not instant wall-clock revocation: swaps committed before canonical resolution are rejected, and retained workers are rechecked on subsequent actions. A stronger immediate/queued identity-change fence would require Core-owned cooperative identity mutations; no SELECT row lock requiring Core UPDATE permission is used or claimed here. A4/A5 release gates remain open.


## A4 private API/database rehearsal checkpoint — 22 October 2026

The standalone `backend/hrm-api/tools/ProcurementAuthorityHttpProbe` now builds with zero warnings/errors and passed once against actual isolated PostgreSQL17. The runtime uses a restricted database role, real Core records, normal host service registration, actual permissions/scope middleware/public authority routes and the actual owning schema provider. Only authentication of its three synthetic users is tool-local; no readiness, identity or authorization provider is replaced.

Verified: two actual HTTP create/submit/activate workflows retained six exact typed action rows; a genuine swap of two active distinct Core account/worker links returned403 for original-maker self-review with unchanged whole hrm/Procurement data and owner/ACL footprint; restoring the links allowed the same pending review. A separately submitted grant activated after its historical maker was offboarded. Legacy rows stayed readable and authorized-revocable without historical worker backfill. Core/evidence mutation and schema creation were denied under the finite role. Two rollback-only ownership negatives rejected actual evidence/Core table ownership even with DML revoked and preserved the footprint after rollback.

Evidence: `artifacts/authority-identity/private/http_20261022_040241/`, source hashes and bounded JSON. Initial tool compilation failed because the Application namespace for HrUserBranchAssignment was missing; that genuine failure is retained, followed by the corrected clean build. This is stronger than the earlier owner-role persistence rehearsal, but does not certify operational PR/PO recipient-rebind execution, concurrent identity fencing, browser acceptance, the current production role or a combined release. Those A4/A5 gates remain open; all earlier financial and receiving/matching release holds remain. Production was untouched.
