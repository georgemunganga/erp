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
