# PROC-SAAS-01 — Kuubu account to Procurement workspace

## 1. Feature Identification
Procurement / shared ERP admission / Kuubu company and licensed module session.
Design baseline: ERP `be000319`, UI `1c1f984c`, Kuubu website `2f1b57d4`.
Status: admission implemented and published as ERP `991ba01c`; original UI adapter
published as `georgemunganga/module-connect` `c0dde2b9` (draft PR118). Private
schema, protocol and vendor browser persistence checks passed on 12 November.
Public opening remains held pending the paired production release and remaining
transaction/concurrency acceptance. Advanced purchasing/approval/Finance
capabilities remain independently held; this is not whole-module business UAT.
The subsequent server-native EF commit gate passed four cases (commit while
authorized; physical rollback after expiry, revocation or verifier outage).
Concurrent company/grant changes and the paired public release remain outstanding.

## 2. Purpose & Business Outcome
A customer completes Kuubu payment or an explicit operator trial, finishes company
setup, invites people, assigns module permissions, and opens Procurement for the
chosen company without a second ERP password or a disconnected local identity.

## 3. Scope & Exclusions
Reuse the existing Procurement workspace and enabled server-owned capabilities.
Do not rebuild its screens, bootstrap business permissions from role names, or
activate held purchasing/Finance workflows merely by opening the module.

## 4. Parent & Related Features
Core product identity, company setup/team, module grants, selected ERP session;
existing Procurement context, metadata, record APIs and UI adapters. Laravel
remains the owner of SaaS billing, distinct from Procurement supplier payments.

## 5. Actors & Participants
Verified enrolled identity, billing payer, company owner, invited team member and
Greenwebb operator. These actors and their permissions remain separate.

## 6. Entry Points & Triggers
Kuubu Companies → Open workspace → Procurement → reviewed company/module scope.
The destination is the existing `/procurement/workspace/`, using the reviewed
selected-session cookie. Switching company remains a Kuubu account operation.

## 7. Preconditions
Current commercial entitlement, live company and person link, active membership,
explicit current Procurement grant and selected-session proof. Production currently
has only HRM schema tables, no Procurement connection, and no Procurement assets
in the tracked HRM frontend; these are deployment prerequisites, not UI flags.

## 8. Workflow
Verify payment server-side → activate module entitlement → confirm company setup
→ invite/accept people → explicitly assign module/action/location grants → review
selected session → open the existing Procurement UI → load verified company,
branch and capability context → use enabled scoped APIs → persist and reload.

## 9. Status / State Transitions
Pending payment never becomes access because a popup closed. Trial authorization
has a finite expiry and does not fabricate a payment. Admission is rechecked on
every request; cancellation, expiry, revocation and session replacement deny access.

## 10. Permissions & Data Scope
Only the private Core product request proof supplies tenant, company, person,
module and permitted locations/actions. Branch headers are narrowed selections,
never grants. Company All remains the selected company and permitted branch union.
No empty legacy branch assignment may widen a restricted product grant.

## 11. Business Rules
Require exact Procurement module/session matching and current explicit actions.
Preserve workflow-specific state guards, segregation and finite execution roles.
Admission must not write legacy RolesCsv or mirror product grants into another ACL.

## 12. Data Fields & Ownership
Core owns person/company/membership/session/branding; Laravel owns commercial
entitlements; Procurement owns suppliers/items/requests and its existing records.
Use the existing PostgreSQL schemas with tenant/company fences; no database per user.

## 13. Screens & Data Views
Preserve the imported workspace, forms, tabs, toolbars and capability-aware
navigation. Connect its organisation/session adapters to selected product context.
Show an honest unavailable state for held features, with no local-data fallback.

## 14. User Experience / Human Behaviour
No fresh ERP sign-up or password after Kuubu admission. Explain missing personal
permissions and expired access with a direct return to Kuubu. Keep Poppins/Kuubu
branding and accessible controls. Never silently switch company or branch on retry.

## 15. Notifications & Communications
Reuse existing invitation delivery and billing confirmations. No new marketing,
invitation or operational notification is sent as a side effect of opening a module.

## 16. Documents & Attachments
Existing document ownership, scan and record-access gates remain unchanged.
This admission bridge does not activate unqualified upload/publication paths.

## 17. Exceptions & Edge Cases
Missing/expired cookie, changed identity, wrong module/company, restricted branch,
stale browser tab, revoked grant, cancelled subscription, provider outage, unknown
write outcome and unavailable schema must fail closed without switching authority.

## 18. Integrations / APIs / Events / Sandbox
Reuse Core workspace context and selected ERP-session APIs. Extend current-principal
and Procurement scope adapters from a verified product proof; retain legacy local
ERP operation. Mutations require same-origin CSRF, an unchanged browser session
marker and server-side authority through their owning transaction.

## 19. Audit / Security / Privacy
No browser selector or role label establishes authority. Recheck current grant,
license and membership before committing mutations; preserve owning record audit.
Keep keys, tokens and connection strings outside source/logs and API responses.

## 20. Reports / Dashboards / Metrics
Use actual scoped API counts only. Health must distinguish missing schema from
missing permission. Admission, denied scope and unknown-write recovery are test evidence.

## 21. Configuration & Localization
Product Procurement remains default-off until paired runtime/schema/UI acceptance.
Retain existing capability flags, company currency/time zone and shared branding.
No guessed business threshold or purchasing policy is introduced.

## 22. Non-Functional Requirements
Tenant/company isolation, bounded request bodies, no-store context, transaction
integrity, limited identity/provider timeouts and safe stale-tab recovery. Reuse
the existing ASP.NET host and separate Kuubu workload server.

## 23. Postconditions
An authorized member sees the chosen company's enabled Procurement screens and
persisted records. Payment, login and company ownership alone grant no business action.

## 24. Failure & Recovery
Keep current production and rollback assets. Rehearse schema provisioning on a
private database first. Unknown writes use existing command/version recovery;
never blindly retry. Restore archived test data into new independent locations.

## 25. Acceptance Criteria
Actual private Laravel/ASP.NET/PostgreSQL browser flow: two licensed companies,
Procurement module grants, invitation acceptance, module opening and company
switching; permitted create/reload and forbidden cross-company/branch requests.
No launch hold is removed before these pass with the exact paired assets.

## 26. Test Scenarios
Paid activation; finite trial/expiry; no module grant; entry-only versus write
actions; restricted/all branch scope; stale session marker after switch; revoked
member/grant/license; provider outage; current schema; real record persistence;
direct API/URL denial; retained HRM entry and legacy local compatibility.

## 27. Ownership / Dependencies
Root owns paired deployment/acceptance. Reuse ERP Core and Procurement owners and
the separate module-connect UI repository. Capacity cleanup preserves production,
retained publications, source and recovery evidence. Perfex is outside this change.

## 28. Open Decisions / Assumptions
Payment credentials are configured; no live collection is performed by an agent.
Existing commercial plans publish HRM and Procurement; this does not claim other
modules are implemented. Advanced Procurement workflows retain their independent
readiness and policy gates. Full on-premise qualification remains separate.
