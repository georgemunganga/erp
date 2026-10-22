# ADR-003 — Core user authorization and optional employee links

**Date:** 22 October 2026  
**Decision:** Accepted for implementation following the product owner's explicit clarification.  
**Scope:** Core identity, Procurement access, ownership and optional HRMS references.

## Context

A system user may be an employee, an external professional or a user without an HR record. An employee may also exist without a login. Job titles and designations describe HR organisation and governance; they do not independently grant application permissions.

The earlier Procurement requirement for an active employee on every request, including the worker-less administrator read-only fallback, conflated these responsibilities. It is superseded by this decision. Existing evidence from that implementation remains historical baseline evidence, not acceptance of the corrected access contract.

## Decision

1. Core owns login identities, account lifecycle, assigned roles, action permissions and organisation/branch membership. Procurement reuses these records and services; it must not create a second user or role master.
2. General Procurement access and CRUD require a current active, unarchived, unlocked Core identity, the relevant module/action permission and the applicable tenant/company/branch/record scope. Being an employee is not an additional blanket requirement. An administrator's permission is also a Core grant, not an employee designation.
3. Context exposes the verified `principalId`. `workerId` is a nullable, optional HRMS projection. Neither UI nor backend may synthesize a principal from an employee ID, use a user ID in an employee field, or fill a missing employee with an empty GUID.
4. New purchase requests retain an immutable server-stamped `RequesterPrincipalId`; `RequesterWorkerId` becomes genuinely optional. The requester cannot select or change either ownership identity through ordinary draft input.
5. Existing requests are preserved without guessed backfill. Where the principal field is absent, exact original canonical `CreatedBy` attribution may support a read-time ownership check against the current verified Core user, within the original tenant/company scope. Unattributed or ambiguous records fail closed for owner-only changes. Names, email similarity, job title and present-day employee reassignment are not ownership evidence.
6. Action permission and data scope are separate checks. A user allowed to create or edit a request does not gain access to another user's records or a different company. Revocation, account lock and deactivation take effect through the current Core authorization contract.
7. Employee data is used only when a specified business operation needs it, such as an explicitly configured reporting-line assignment or employee delivery/custodian reference. Such use must be documented by that operation and must not silently block unrelated Procurement functions.
8. Existing approval, Finance and receiving evidence pinned to employees remains immutable. It must not be relabelled as user-based evidence or accepted with fabricated employee identifiers. Those operational contracts require their own reviewed principal-based migration before being offered to system-only users. Amount authority, independent review and segregation of duties remain enforced.
9. The UI consumes server permissions and capabilities. It must not infer action grants from a job title, an employee link, the presence of an administrator role label, or a hidden button. Direct API requests receive the same authorization checks.

## Acceptance and rollout

- Verify authorized system-only users can perform supported general CRUD and refresh persisted records.
- Verify linked employees receive only their actual Core grants; changing an HR designation does not grant Procurement access.
- Verify missing/revoked action grants, inactive/locked accounts and foreign tenant/company/branch/record access are denied at API level.
- Verify stable principal ownership, optional employee data, stale-version conflicts and retained audit/history.
- Generate and review a new forward migration; do not rewrite applied migrations or invent ownership for historical rows.
- Re-run relevant actual PostgreSQL and Playwright flows on the corrected API/UI before deployment. Retain prior failures and distinguish mocked UI evidence from actual API/database evidence.

## Consequences

The live administrator account does not need an invented employee record or an arbitrary employee link to use authorized Procurement CRUD. HRMS remains responsible for employee records and designations. Core role administration remains the shared source of access grants. Broader principal-based operational evidence and optional direct-user permission overrides must use explicit Core contracts; neither is implied complete by the general CRUD correction.
