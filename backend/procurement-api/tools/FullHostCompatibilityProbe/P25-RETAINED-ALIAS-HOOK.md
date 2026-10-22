# P25 retained alias/offboarding: source-only native hook

Base `5470deed2cafea204e2e27ab26a429914d6b7d48`. Two NEW tool files only.
No shared entry/fixture/application/model/migration/default registration changes.
Uncompiled and unexecuted; source review, actual build and fresh private native
acceptance are required. Finance/AP is unbuilt; explicit PRIVATE recorded manual
evidence is not a Finance reservation/ledger/authority proof. Test authentication
is not native IdP proof. These are NEW principal lineages, not legacy execution.

## Root-owned integration

- Introduce optional `P25_HTTP_VERIFY_RETAINED_ALIAS=1`, reject other present
  values before bootstrap; absent leaves existing defaults unchanged.
- Requires positive manual-evidence mode and the real finite Record/Execution
  connections. Use the SAME actual positive-host `client` and `PrincipalSend`.
- In `VerifyPrivateManualApprovalAsync`, call AFTER `VerifyRawStorageAsync` (if
  enabled) and all original first-graph owner/receipt/replay/GET checks, BEFORE
  `VerifyAuthorityRevocationDenialAsync`. Specifically insert a new opt-in block
  immediately before the final `if(verifyAccountScopeAuthority)` block:

  ```csharp
  await VerifyRetainedAliasOffboardingAsync(fixture, ownerConnection,
      client, send, grantId, result, stage);
  ```

  The existing authority MUST still be Active. Do not move the old
  `!core.Workers.AnyAsync()` proof after this hook: it deliberately establishes
  the ORIGINAL graph's workerless provenance before adding a separate synthetic
  Worker. The final irreversible authority test follows normally and may create
  another pending graph. Neither original graph nor its identity is rewritten.
- Additional actual HTTP calls are exactly **11**, all via existing counted
  `PrincipalSend` (no direct uncounted requests). Combined existing boundary
  mode's 59 becomes **70** when both flags are enabled. Alias-only successor
  may omit already accepted matrix/raw/account-authority flags: base43 plus11
  is **54**. The ordinary12GET/6replay/positive/negative cohorts remain. Main counts/defaults
  remain root-owned; this commit adds no counter/flag changes.
- Future launcher must require new exact source/SDK/publication/fresh DB pins,
  the new opt-in and exact result below, preserving all old checks. Never reuse
  an accepted DB. No automatic retry/reset/drop, new live grants or providers.

## Genuine fixture sequence and exact denial source

1. Require original three actual synthetic Core accounts to be active,
   workerless and unarchived, existing authority Active and no existing Workers.
   Capture all original persisted row hashes, not just current PRs.
2. Add ONE real Core `Worker` through `HrmDbContext.SaveChangesAsync`: explicit
   synthetic contingent identity, original fixture branch, no real SubjectId,
   no personal/payroll/bank fields. Set only private maker's actual Core
   `WorkerId` by exact field CAS BEFORE the additional PR is created.
3. Actual POST PR, submit and independent workerless checker2 capture, using
   the existing independently published category/policy and private manual
   evidence contract. Owner reload requires original principal identity1 and
   RequesterCoreWorkerId=W, retaining the exact original hash.
4. Clear maker's current Core WorkerId and set synthetic W to terminated and
   archived. These are private owning Core fixture transitions, not account
   grants. The immutable original request alias is never backfilled/rewritten.
5. Temporarily link existing checker to W. Actual Open must return **409
   FinanceEvidenceVerifierInvalid**, from
   `ProcurementPrincipalFinanceChecker.RequireAsync` independence check, BEFORE
   later `MatchesCurrentCanonical` could return a generic identity-change code.
   Whole footprint unchanged. Restore exact checker row/current grants intact.
6. Same Open body/key then succeeds after restoration. Owner checks later
   ensure exactly one Open receipt, actual PR3/assignment2, original requester W
   and MakerWorkerId=null (maker link was deliberately cleared before Open).
7. Temporarily link assigned approver to W. Actual detail200 has no actions and
   exact `ApprovalIdentityChanged` blocker. Actual Decide **409
   ApprovalIdentityChanged**, from
   `ProcurementPrincipalApprovalAssignees.ValidateAssignmentAsync` BEFORE
   authority re-evaluation. Whole footprint unchanged for BOTH requests.
   Restore complete original account; no evidence rewrite/authority regrant.
8. Make original maker ACCOUNT inactive, with link still null. Independent
   current approver detail must still allow Approve with exact unchanged CAS;
   same original Decide body/key succeeds. Historical maker availability is
   not the current approver's grant. Worker remains terminated/archived at
   this point; the decision must not rely on active HR resolution.
9. Owner reload verifies exact original source hash/W, independent checker2,
   grant/assignment2, actual Core decision/outcome/approved lines, two original
   successful receipt response bytes, three successor events and business
   evidence digest. No Finance release/AP/payment rows.
10. Finally CAS-restores all three complete synthetic Core account rows and the
    newly declared Worker baseline. Worker and additional graph are retained;
    nothing is deleted. Every preexisting persisted row hash/multiplicity must
    survive exactly (additions allowed, updates/deletions are not). This covers
    original negative/approved graphs, policies, audits, receipts and evidence.

## Failure containment and result

Only controlled phases and bounded exception KIND are recorded. The first
exception survives any secondary restoration/read-check failure. Allowed target
states are registered before CAS, so an ambiguous transport commit can be
restored only when actual state is exactly one declared fixture state; a third
state vetoes restoration and is retained for diagnosis. No errors count as PASS.
Denied commands/ALL GETs compare full existing logical footprint; temporary
alias blocks also compare it after exact restoration. Normal successful graph
writes are intentionally retained, so final checks compare original row hashes
rather than claiming the entire store stayed empty/unchanged.

Require result `principalRetainedAliasOffboardingVerification`, status
`PASS_PRIVATE_RETAINED_ALIAS_OFFBOARDING_ONLY`, actualHttpCalls=11, nonzero
workerId/requestId/caseId/assignmentId/checkerId/decisionId, original identity
hash, and all emitted proof booleans true. Exact global counts AT THIS HOOK
(before optional final authority graph) must be Requests/Checkers/Submissions/
AssignmentEvidence/Cases/Decisions/Outcomes/Receipts/SuccessorEvents:
`2/1/1/1/1/1/1/2/3` before and `3/2/2/2/2/2/2/4/6` after. Optional later authority
revocation legitimately adds its own pending graph and is checked separately.
Require externalFinanceAuthority,
nativeIdentityProviderVerified, legacyApprovalExecutionVerified,
productionTouched false. Preserve original approval/replay/GET/raw/account/
schema/privilege/authority cohort requirements separately. Result appears only
AFTER first-error-preserving finally restoration and original-row verification.

Published P24→P25 fresh/used schema rehearsal already proves original legacy
row preservation; it does not establish genuine old worker-v2 workflow
execution. Do not manufacture that proof by rehashing or adopting legacy rows.
