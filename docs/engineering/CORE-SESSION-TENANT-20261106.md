# CORE-SESSION-TENANT — Opaque-session tenant resolution

## 1. Feature Identification
Core identity security repair, Tier3, under GW-ENG-STD-001. Source implementation
is not deployment or customer acceptance. Main packaging PR199 is a prerequisite.

## 2. Purpose & Business Outcome
A valid non-default tenant's session resolves its own principal, permissions and
records without consulting another customer's process-default tenant.

## 3. Scope & Exclusions
Repair existing opaque session authentication only. No new login identity,
customer enrollment, subscription, workspace provisioning, global password
search, OIDC provider or public SaaS activation.

## 4. Parent & Related Features
Kuubu shared runtime, LocalIdentity and Core workspace context. Future Laravel
admission must independently establish an explicit identity/workspace mapping.

## 5. Actors & Participants
Existing active ERP user, opaque-session holder, Core authentication handler and
owning identity administrator. Billing membership is not an ERP actor grant.

## 6. Entry Points & Triggers
Existing hrm_session cookie on authenticated requests; no new HTTP route.

## 7. Preconditions
Globally unique existing token-hash index; unexpired, unrevoked, unarchived
session; active unarchived unlocked user in that session's exact tenant.

## 8. Workflow
Hash the opaque token; perform a narrow unfiltered unique session lookup; match
the user by both ID and session tenant; establish a temporary internal tenant
scope; refresh session activity and resolve current role grants; construct the
normal principal. Dispose the temporary scope on success and failure. Middleware
then uses the authenticated principal tenant for all operational queries.

## 9. Status / State Transitions
Active session remains active; activity timestamp may advance. Revoked, expired,
archived or invalid sessions never reactivate. Locked users remain denied.

## 10. Permissions & Data Scope
Only the opaque hash lookup and explicit session-tenant/user lookup bypass global
filters during authentication. Current role queries retain tenant filters. No
header, email or domain chooses a tenant or grants a role. No schema/ACL widening.

## 11. Business Rules
Existing token randomness/hash and global uniqueness remain. Reject oversized
cookies before hashing. Cross-tenant session/user mismatches fail closed. Matching
emails in unrelated tenants cannot merge identities or permissions.

## 12. Data Fields & Ownership
Core owns existing LocalSession and LocalUser records. No fields or migrations
added. The temporary context uses a private object key in HttpContext.Items and
cannot be populated by browser headers.

## 13. Screens & Data Views
Existing UI unchanged. No launch button is enabled by this repair.

## 14. User Experience / Human Behaviour
Existing signed-out/expired-session handling remains; a valid separately
authorized non-default session can retain its own organization context.

## 15. Notifications & Communications
None; authentication does not send mail or invite users.

## 16. Documents & Attachments
None; document authorization stays a later independently tested workflow.

## 17. Exceptions & Edge Cases
Missing/forged cookie, expiry, revocation, archived session/user, inactive/locked
user, mismatched tenant/user pair, role-name collision and context cleanup.

## 18. Integrations / APIs / Events / Sandbox
No new external endpoint or event. TestServer exercises the real authentication
handler and EF tenant filters with unrelated synthetic organizations. InMemory
tests are not PostgreSQL/native schema acceptance.

## 19. Audit / Security / Privacy
Never log cookie or token hashes. Existing session activity audit uses the
validated tenant, not the fallback tenant. Stored session ID/tenant pairing is
the authority; no organization access follows commercial/customer creation.

## 20. Reports / Dashboards / Metrics
No new report. Observe existing401/error rates without credential logging.

## 21. Configuration & Localization
No new public configuration. Existing default tenant is retained for legacy
startup/local login; it is not used to resolve an opaque session.

## 22. Non-Functional Requirements
Two bounded indexed lookups before permission resolution. Request cancellation
propagates. Internal scope is request-local and restores nested prior state.

## 23. Postconditions
Successful principal/session activity/role resolution share one verified tenant.
Failed authentication creates no identity, grant, session or audit activity row.

## 24. Failure & Recovery
Fail closed on missing/invalid pair or database failure. Preserve prior image and
logs. No migration rollback needed; rollback restores prior default-tenant
limitation and therefore must not re-open non-default shared access.

## 25. Acceptance Criteria
Non-default cookie reads only its tenant's company and roles despite matching
emails/role names; activity audit belongs to that tenant. Invalid states return
401 without activity writes. Forged headers do not authorize. Scope cleanup
survives exceptions and the next request starts without leaked tenant context.

## 26. Test Scenarios
LocalSessionTenantHttpTests covers these cases through the actual handler.
Existing shared-session/role/workspace tests and complete HRM suite remain
required. PostgreSQL cookie/admission tests and unrelated-tenant operational
read/write/export/job tests remain required before a shared customer launch.

## 27. Ownership / Dependencies
Root coordinates Core implementation and release qualification. Existing
application/module owners remain unchanged; no collaborator assignment inferred.

## 28. Open Decisions / Assumptions
This is a prerequisite, not the entire admission design. Laravel-to-Core
one-time admission, explicit organization/team mappings, entitlement freshness,
session lifetime/revocation policy and tenant-isolation acceptance remain work.
Do not deploy wholesale main or lift held Procurement/financial gates.
