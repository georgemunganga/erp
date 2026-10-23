# CORE-CTX-01 — Workspace company and branch choices

1. **Feature identification:** Core-owned, version 1, authenticated self-service read. Implementation checkpoint; production acceptance remains separate.
2. **Purpose and outcome:** Let a system user choose a permitted company and branch without organisation administration or an employee record.
3. **Scope and exclusions:** Basic selector metadata only. No organisation CRUD, account/grant changes or business-record access.
4. **Parent and related features:** ERP Core identity, organisations, branch assignments and the shared workspace header.
5. **Actors:** Active authenticated Core accounts, including system-only users. Management permissions are not implied.
6. **Entry and trigger:** Shared header bootstrap or an explicit metadata refresh.
7. **Preconditions:** Authenticated canonical account ID and tenant claim; current active, non-archived, unlocked account in that tenant.
8. **Workflow:** Resolve the current account; read declared branch assignments; intersect active branches and active companies; return a minimal projection.
9. **States and transitions:** N/A; this operation does not transition records.
10. **Permissions and data scope:** Named `core-workspace-context` authentication/tenant policy plus server-side current-account checks. Nonempty declared assignments restrict choices. No declared assignments retains the existing Core organisation-wide-within-tenant contract. Inactive assigned branches never turn a confined account into an organisation-wide one.
11. **Business rules:** Company/branch ownership must match; archived companies/branches are excluded. A selector result is not a grant; business endpoints independently reauthorize each action and record.
12. **Data and ownership:** Existing Core account, LegalEntity, WorkLocation and branch-assignment masters. Response: schemaVersion, tenantId, principalId, organisationWide, companies `{id,code,registeredName,currency,isDefault}`, branches `{id,code,name,legalEntityId}`. No employee, email, role, bank or personal fields.
13. **Screens:** Existing shared organisation/branch switcher. Reuse existing ERP controls and branding.
14. **UX:** Prefer an available Core default company, show only returned choices, and retain explicit selection. Empty choices explain unavailable access; do not invent a branch or HQ.
15. **Notifications:** N/A; no business decision or asynchronous work occurs.
16. **Documents:** N/A; no upload or file access.
17. **Exceptions:** Anonymous, missing tenant, missing/inactive/locked account, unavailable storage, stale session or revoked assignments. Never fall back around a 401/403.
18. **API and sandbox:** `GET /api/core/v1/workspace-context`, ordinary ERP cookie/identity authentication, JSON, `Cache-Control: no-store`. No caller-supplied account/tenant selector. Test with synthetic users in a disposable database; no production fixture writes.
19. **Audit, security and privacy:** Read only; no new master or grants. Restrict to the authenticated tenant and account. Minimal metadata only. No credentials in logs. Existing owner audit remains authoritative for account and assignment changes.
20. **Reports and metrics:** N/A for business analytics; ordinary API health/error monitoring applies.
21. **Configuration and localisation:** Names, currency and default flag come from Core; ordering prefers available defaults. No hard-coded company, currency or branch.
22. **Non-functional requirements:** Cancellation-aware asynchronous queries, accessible existing selector, authentication-bound client state and bounded/stale-safe loading. Expected tenant volumes remain an operating requirement.
23. **Postconditions:** The user can choose returned context; no database changes.
24. **Failure and recovery:** Report unavailable choices and permit a fresh authenticated read. Abort/discard old responses after account/context changes. A 404 from an older backend may use the existing compatibility reader; authorization denial must not use a broader fallback.
25. **Acceptance criteria:** Workerless custom module account can read its choices; assigned A+B and foreign-company C return exactly their active company/branch projections; other-tenant data never appears; inactive assignments yield no wider access; management endpoints remain protected.
26. **Test scenarios:** Mapped authorization/current-account matrix, restricted and organisation-wide tenant projections, inactive branches, real local-cookie/private-PostgreSQL directory and cold shared-header browser selection. Synthetic authentication tests do not substitute for real-cookie acceptance.
27. **Ownership and dependencies:** Core engineering owns this contract, existing Core identity and organisation stores, and shared header integration. Procurement consumes the projection and owns its own transaction branch checks.
28. **Open decisions and assumptions:** External IdP subjects require an established Core account mapping; not accepted by this local-account milestone. The inherited no-assignment organisation-wide policy is preserved, not silently redefined. Production rollout, independent review and UAT require recorded evidence.
