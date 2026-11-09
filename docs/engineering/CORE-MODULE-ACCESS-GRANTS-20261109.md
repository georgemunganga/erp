# Explicit company module grants

## 1. Identification
Core Tier3 feature,9 November2026; ledger, owner API and isolated editor qualified; ERP admission not live.
## 2. Purpose
Let an explicit company owner deliberately grant/revoke a current member's
module roles/actions and company or work-location scope without creating a login,
employee, payer membership or ERP session.
## 3. Scope
Versioned grant ledger, current grant resolution and transactional owner commands.
Owner HTTP review/commands and UI are implemented and tested separately. Composed
browser/provider/Core qualification, ERP sessions and route/data enforcement follow.
No existing local credential/session/role/branch behavior is changed here.
## 4. Parent
Licensed product admission, explicit company membership and Core workspace binding.
Keep the existing ASP.NET/Docker codebase for SaaS and dedicated/on-premise.
## 5. Actors
Fresh verified/enrolled Kuubu person; active explicit company owner; target Core
member; separate commercial license service; future operational session adapter.
## 6. Entry
Internal application commands take a current operational-person proof plus exact
actor link/company, target membership, current membership/grant revisions,
explicit module/roles/actions/scope, confirmation and actor-scoped idempotency key.
## 7. Preconditions
Current person, owner link/principal/membership and bound purchased company slot;
selected module currently licensed. Assignment targets an active federated member
with an active explicit identity link. Revocation may retire an inactive member's
old grant. Billing authority and email/domain confer none of these powers.
## 8. Workflow
Review current member/revision and module catalog; choose roles/actions/scope;
confirm; serialize key/company with team commands; recheck current authority;
append changed grant version, immutable receipt, audit and ID-only outbox event
atomically. Exact replay reports the original command outcome, not current access.
## 9. States
Latest version is granted or revoked. Earlier versions are never merged or used
as fallback. Membership revision changes invalidate old grants, including revoke/
rejoin. Reassignment after a change needs a new explicit grant version.
## 10. Permissions
Initial grant management is company-owner-only, including explicit self-grants.
It grants no payment/spending/approval limits or exemption from module business
rules. Governance administrator/member roles do not imply operational authority.
## 11. Rules
Modules initially hrm/procurement. Reuse known module roles and registered
Procurement action keys, without legacy role-to-action bootstrap or erp_admin
fallback. HRM initially uses its existing hrm-access/hrm-admin policy boundaries
and workforce role profiles; finer HRM route qualification remains required.
## 12. Data
Core owns append-only grant versions, location rows and command receipts. Version
binds tenant/company/target membership, its revision, module, explicit role/action
snapshot, scope, predecessor and issuing membership. Composite keys prevent
cross-company target/predecessor/receipt binding and cross-tenant branch binding.
Work-location company ownership is rechecked on every decision; moving/archiving
a branch invalidates its old scoped grant without breaking legacy branch moves.
## 13. UI
Owner editor reuses company/team controls, exact member ID/revisions and
shared dialogs. All-company scope has its own unchecked confirmation. No module
launch until the separate operational session and route gates are implemented.
## 14. UX
Show current member/module permissions, stale-change conflict and uncertain write
recovery. Retry the identical reviewed key; never silently retry changed authority.
## 15. Notifications
Only ID-only grant-change outbox events; no mail/payment/provisioning activation.
## 16. Documents
No new business-document access follows from creating a grant record alone.
## 17. Errors
Unknown module/role/action/scope/duplicate/empty branch set is invalid; missing
authority/current license denies; revision/key mismatch conflicts; provider/DB
failure is unavailable. Cancellation rolls back; no partial grant/audit/receipt.
## 18. Integration
Future adapter must resolve latest grant and recheck current person, exact
membership/link, license/module and work locations for each operational request.
No issuer tokens, roles CSV or browser assertions can substitute for this read.
## 19. Audit/privacy
Append-only audit records versioned before/after role/action/scope configuration,
actor and correlation. Events carry IDs/revision/state only. No credentials,
email directory or provider proof bodies are persisted in this ledger.
## 20. Configuration
The initial ledger had no HTTP registration. Owner management now has a separate
default-false `ERP:ModuleAccess:Enabled` gate requiring operational identity,
company discovery and licensing. No production activation occurs here.
Migration is additive; owning deployment applies it explicitly. New records are
not created from historical owners, roles, employees or email matches.
## 21. Security
Test foreign company/member/link/license/branch, non-owner, expired proofs,
cross-tenant constraints, unknown permissions, no implicit owner grants, stale
membership/version, revoked latest version, branch moves, replay and concurrency.
## 22. Operations
Qualify real PostgreSQL apply/rollback and locking independently of SQLite unit
checks. Populated rollback must refuse to destroy grant/evidence history. No
production migration runs during development or test preparation.
## 23. Postconditions
A command records an explicit decision only. No authentication principal, ERP
session, employee, password, billing access or operational side effect is created.
## 24. Recovery
Reload latest membership/grant after conflict. Exact-key replay reports historical
outcome; current resolver must still deny revoked/replaced/stale grants. Never
delete a version or clear revocation to recover access.
## 25. Acceptance
Complete ledger increment when schema constraints, atomic commands/current
resolution and real DB concurrency/rollback tests pass. Full admission remains
unproven until browser/editor/session/module/branch isolation is verified.
## 26. Tests
Focused relational/model tests plus native PostgreSQL cases; retain failed builds
and fixtures, use exclusive new publication roots and read-only consumers.
## 27. Release gates
Composed browser/editor admission, revocable sessions, default authentication integration, module route
and data-scope enforcement, real browser composition and production ops remain.
Lenco/hosting products and dedicated/offline/upgrades/restore remain programme work.
## 28. Evidence
Record executed source/runtime/DB qualifications here after verification.

The internal ledger/current-resolution increment passes556 Windows checks with39
environment-specific opt-ins skipped, and82 native PostgreSQL checks with no skips
or failures. The latter includes all25 module-grant cases, team commands and
startup-policy checks. Real migrations apply; empty rollback/reapply has no model
drift; populated rollback refuses to remove versions/receipts. Four simultaneous
same-key commands produce one version, receipt, audit and event. Relational tests
reject foreign-tenant locations and retargeted predecessors/receipts. Current
resolution rejects revoked or membership-stale grants and archived locations.
Legacy branch company moves remain supported by the new relational keys.

Evidence: `inspection/qualifications/module-grants-core-02` (full fresh build and
disconnected model comparison), `module-grants-core-03` (independent corrected
test assembly and full regression), `module-grants-native-02` and
`module-grants-verification-02/verification.json`. Ten C# inputs and49 independent
dependency DLLs match their manifests. All585 native runtime inputs remain
unchanged; the owned test/database containers exited, with no host ports and
read-only binary mounts/root filesystem for the test runner. No production
migration, flag, deployment, payment, external mail, Perfex or DNS change occurred.

Native01 is retained:80 passed/one fixture failure because the fixture attempted
to insert a second company into an allocated SaaS tenant. The existing allocation
guard correctly refused it. Core03/native02 seed the foreign branch in its owning
tenant, test archival invalidation and separately preserve legacy company moves;
no production guard was bypassed or weakened. API/test builds retain27/20 existing
warnings. HTTP/editor/session/route qualification remains required; these tests
use internal current-person/license fixtures and do not establish the combined
branded browser/Passport/ERP journey or production readiness.

Qualified owner API increment: `/api/core/product-identity/operational/
module-access/{members,locations,review,commands}` uses the distinct operational
cookie/browser binding and fresh provider observation. GET selectors are exact
canonical company/link IDs, optional canonical cursor, and target membership/module
for review. Unknown/duplicate selectors fail before observation. Owner membership
and immutable enrollment binding select commercial licensing; callers supply no
tenant/enrollment/payer. Member and active-location directories page20 records.
Review includes the current member revision, registered role/action catalogs,
latest configuration/revision, selected branch availability and an explicit
reconfirmation warning for stale membership/location evidence. Unavailable branch
names are not disclosed. Reviews create no sessions or operational claims.

Commands require same-origin JSON, bound operational CSRF, a maximum32768-byte
body/depth4, all14 exact fields with duplicate rejection and bounded arrays,
explicit operation/confirmation, target membership revision and expected grant
revision. Only this registered command can carry a nonempty body under the
operational group; start/logout retain empty-body validation. Unknown roles/actions
and implicit company scope fail before provider/DB writes. API orchestration
checks current person, owner and purchased module again before invoking the
transactional ledger. HTTP200 reports Changed/Unchanged/Existing with historical
version;400 invalid,401 identity,403 authority,409 stale revision/key,413 size,
429 admission budget,503 unavailable/default off. No automatic POST retry or
authentication fallback is allowed. The existing session DTO is unchanged until
the paired editor integration is qualified.

Owner API evidence:579 Windows checks pass/39 environment-specific skips, and68
PostgreSQL checks pass without skips or failures. These include ten API query/
license-boundary cases, thirteen new HTTP/security/configuration cases, existing
operational callback/session defenses and the grant ledger. The HTTP journey uses
real operational handlers, cookie binding, protected store, strict parsing and
Core relational commands with a synthetic identity backchannel. It proves no
default ERP admission/session/legacy roles arise from saving permissions; it does
not prove the actual branded website/relay/provider/network/ingress journey.

Evidence roots: `inspection/qualifications/module-access-api-01` (fresh API/test
build and retained578-pass/one-fixture-failure regression), `module-access-api-02`
(independent corrected test build;579 pass), `module-access-native-01` (retained
67-pass/one identical fixture expectation failure), `module-access-native-02`
(68 pass) and `module-access-verification-01/verification.json`. The paging
fixture already contained one branch. Its correction compares the complete
authoritative company branch-ID set across both pages, preserving that branch
and checking no duplicates rather than assuming a fixed second-page count.
No API/production rule was weakened. Fresh builds retain27/20 existing warnings.
Source and independent dependency/runtime hash checks pass; owned containers
exited, no host ports or production changes. The existing session/picker DTOs and
frontend, Foundation runtime and branded website source are unchanged. No new
migration is needed beyond the qualified ledger migration; the owning deployment
must apply that migration before enabling this API. Grant editor, bound revocable
sessions, route/data isolation and production qualification remain release gates.

Owner editor acceptance: the `/workspace-access` owner review now mounts the
module permission editor outside the classic ERP authentication/tenant shell.
It consumes the qualified owner API with exact company/link, member and module
selectors, strict DTO/identity matching, bounded no-store/same-origin transport,
timeouts, cancellation and no automatic POST retries. Members/administrators do
not receive this owner workflow. A disabled/unavailable API gives an explicit
recovery state rather than a working form. No session DTO or backend change was
needed; companion API sourcee52e8049 remains byte-for-byte unchanged.

Member/module choices reset drafts and cancel superseded reads. New/stale grants
start without inferred roles/actions; HRM entry/admin rules must be selected
explicitly, and Procurement switching cannot carry HR permissions. Selected
branches use independent paging; an empty list cannot become company-wide.
Company-wide scope has an unchecked current/future-branch confirmation after
every fresh review. The final shared dialog names the person/company/module,
roles/actions and selected branches. Revocation has its own confirmation and
empty role/action/branch snapshot. Inactive identities cannot receive new grants.

An uncertain write locks the draft and retains the exact reviewed payload/key
for explicit retry, or lets the owner discard it and reload current permissions.
Conflicts require fresh review; historical replay does not imply current access.
Company changes, sign-out and unmount cancel reads/writes and clear in-memory
permission state. No credentials or grants are persisted in browser storage.
Saving records a permission decision only; no ERP entry/session or employee is
created. Server point-of-use checks remain authoritative for branch availability,
membership, module licensing and all command revisions.

Evidence: `inspection/qualifications/module-access-ui-02` passes122 client contract
checks, targeted lint, production build and typecheck, plus23 browser scenarios
(13 editor and10 company-picker regressions). Scenarios cover named-branch grant,
all-company confirmation, Procurement switching, uncertain completed-save replay,
revoke, conflict, stale/inactive membership, paging, late response, expiry,
disabled API and390px sign-out/layout. Final screenshots were visually inspected.
Browser API responses are synthetic: no actual Core permission record/provider
wire is qualified by these browser checks. Companion real API/relational evidence
retains its579 Windows/68 PostgreSQL scope. `module-access-ui-verification-01`
verifies368 source files,493 unchanged build outputs, seven unchanged C# inputs
and stopped owned SSR/listener. UI01 retains121 contracts and22 passing browser
cases before added catalog guards/explicit Procurement coverage. No retained
publication was overwritten. Real browser/Core/Passport composition, revocable
ERP sessions and protected route/data isolation remain next; production schema,
flags, Perfex, DNS, payment/mail and runtime activation were not changed here.

Integrated-main acceptance: ERP main advanced independently to
`52bdb5a0c36778a2f082e8e4085d65cbff25df7e` (PR208: custom Procurement role entry).
Its current-principal/default-company server-authority fix and earlier material
request shortcut were merged into this SaaS topic branch. The sole conflict was
the handoff prepend; both histories are preserved. Imported deployment override/
notification documentation is retained as source; no private environment,
running worker/container/service, database or DNS action was performed.

Fresh `module-access-ui-04` is built with `VITE_USE_REAL_API=true`:125 contracts,
targeted lint (zero errors/five existing fast-refresh warnings), production build,
typecheck and28 browser cases pass. The five imported Procurement entry fixtures
pass alongside the13 editor/10 picker cases, including custom-role denial and
current actor/company matching. Browser responses remain synthetic. Final390px
editor/dialog screenshots were inspected. `module-access-ui-verification-02`
checks373 source files,491 immutable output files, seven unchanged companion C#
inputs and stopped owned SSR/listener. Core/API runtime remains e52e8049 with its
separately scoped579 Windows/68 PostgreSQL evidence; no new schema is introduced.

UI03 retains125 passing contracts and the imported formatting-only lint failure.
UI04 formats seven imported files and verifies unchanged normalized emitted
runtime ASTs in `module-editor-main-format-02`. The first format-verifier attempt
wrongly compared whole SourceFile.text (including formatting); its failure receipt
is retained separately. No application permission rule or test assertion was
weakened. This is source integration, not production activation or composed
browser/Core/Passport acceptance. In particular the existing classic context
request uses an all-branches header: future SaaS session enforcement must bind
the selected company/module and explicit allowed branches server-side and must
not let a classic header, nominal role or UI entry widen those grants.
