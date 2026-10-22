# Private spending-authority identity HTTP rehearsal

**Status: compiled and executed against an isolated private PostgreSQL17 database on 22 October 2026.** This tool
is not a production application or authentication provider. It must never run
against real company, employee, procurement or financial data.

## Guarded inputs and ownership

- No command-line modes, database creation, reset, deletion, resume or role cleanup.
- Root supplies a **fresh empty existing** database through
  `ConnectionStrings__Procurement`. Only `authority_identity_http_*` names and
  `/authority-identity-private-socket` or loopback hosts are accepted.
- The owner connection is used only for fresh schema/bootstrap, synthetic identity
  changes, bounded private role provisioning, independent persisted verification
  and rollback-only ownership negatives. Neither credentials nor SQL are printed.
- Core `EnsureCreated` runs first, then the actual Procurement migration chain to
  P29. Two deliberately unproven legacy grants are synthetic pre-P30 fixtures
  built with existing domain transitions, not claimed as historical HTTP evidence.
  P30 is then applied without rewriting those grants or adding worker evidence.
- Three genuine Core LocalUsers/Workers with distinct IDs, company, branch and
  branch assignments are seeded. Their stored and synthetic authenticated roles
  are `erp_admin`; the probe does not impersonate or create a real employee.
- A unique unprivileged LOGIN role receives schema USAGE and only mapped reads of
  LocalUser, Worker, LegalEntity, WorkLocation, OrgUnit, HrUserBranchAssignment and
  TenantRoleAssignment. It has no Core DML or schema CREATE.
- Procurement reads are limited to grants, categories, identity evidence, audit,
  outbox, procurement categories and migration history. Grants allow INSERT/UPDATE;
  replaceable draft-category children allow INSERT/DELETE; evidence/audit/outbox
  allow INSERT only. No UPDATE/DELETE/TRUNCATE on append-only tables.
- Role credentials are generated privately; the owner's password/passfile is not
  reused. Private trust-socket runtime connections omit a password. The synthetic
  role and database remain for forensic inspection; the tool never drops either.

## Real execution boundary

The tool references the existing API and `Microsoft.AspNetCore.TestHost` 10.0.11.
TestServer uses the **finite runtime** connection, actual
`ProcurementHostIntegration.AddServices`, registered owning readiness provider,
Procurement permissions, canonical scope middleware and public authority/context
routes. Only the tool-local authenticated principal header is synthetic and accepts
exactly its three seeded IDs. No schema-ready, identity or permission provider is
replaced. No public listener, browser, production endpoint or new API is added.

## Authored assertions

1. Actual finite-role read/record readiness; direct Core UPDATE, identity
   UPDATE/DELETE and schema CREATE each denied with SQL42501.
2. Legacy detail remains readable and offers no Edit/Submit. A real POST submit
   returns the original-identity denial without changing the whole retained
   hrm/Procurement data+owner/ACL footprint. Authorized legacy revoke remains
   available and creates no retrospective worker evidence.
3. Real HTTP create → GET allowedActions Edit/Submit → submit → independent
   reviewer GET Return/Activate. Quantities are not involved; the explicitly
   synthetic limit is exact `12.3456` in company currency ZMW.
4. Swap the two **active distinct** maker/reviewer account-to-worker bindings
   using the private Core owner. The reviewer account now represents the retained
   original maker. Real Activate POST must return403 with
   `AuthorityIndependentWorkerRequired`; review hints disappear and all retained
   data/owner/ACL values remain unchanged. Restore the original Core mappings,
   then the same pending grant activates successfully and offers Revoke.
5. Reload each new grant's three owned action records and verify full retained
   lineage, original maker/recipient/reviewer worker IDs, fingerprint/digest and
   PostgreSQL timestamp round trip using existing pure verification.
6. A second separately referenced grant is submitted before its original maker is
   offboarded. An independently active reviewer can still review and activate it;
   no historical person is reconstructed or reactivated.
7. Two private owner transactions temporarily transfer the actual evidence or
   mapped LocalUser table to the finite role and revoke its DML. The tool proves
   those DML checks are false before requiring owning `IsReadyAsync` to reject
   the remaining ALTER/regrant ownership capability. Always roll back, then prove
   unchanged owner/ACL/data footprint. These require the reviewed A3 ownership
   guard and are not run against production.
8. Final counts are exactly four grants (two legacy, two new) and six new evidence
   rows, with no purchase requests/orders, RFQs or supplier invoices fabricated.

**Explicit limit:** recipient-rebinding spending *eligibility* is not exercised:
there is no bounded public eligibility route. The tool does not expose an internal
resolver, fabricate an approved PR/PO or call a pure helper as HTTP proof. Existing
helper tests and later full PR/PO execution gates remain separate requirements.
No supplier, document, stock, Finance budget or payable authority is claimed.

## Parent-controlled execution

Build and run only after the parent grants the serialized SDK slot. Use the
existing cached packages, one CPU with a 1200MiB SDK limit and network-none SDK/private-PG containers.
The parent creates a new `authority_identity_http_<timestamp>` database and mounts
its private socket, then runs the precompiled DLL with the single explicit owner
connection environment variable. Do not print or inline that connection in logs.

A terminal bounded JSON PASS is emitted only after every assertion. FAIL reports
only stage, exception type, SQLSTATE and tool-authored failure text. Preserve the
first failure, exact source/image hashes and protected logs. A runtime PASS is recorded below; no production release, deployment or activation is implied.


## Recorded verification

- Backend source checkpoint: `60b3cca` (held development branch).
- Initial tool build failed with two missing HrUserBranchAssignment namespace references; corrected with the existing Application import. Failed build log retained.
- Corrected tool build: zero warnings/errors, SDK10.0.400.
- Actual private PostgreSQL17 mapped-HTTP run: **PASS**.
- Protected local evidence: `artifacts/authority-identity/private/http_20261022_040241/`; source SHA256 manifest and bounded terminal JSON retained.
- Two new grants, six retained action rows, real active-worker swap denial403, restored review success, independent review after maker offboarding, legacy read/revoke with no backfill, finite SQL42501 denials, and two rollback-only table-ownership readiness denials.
- Actual public PR/PO recipient-rebind execution, concurrent identity-change fencing and browser acceptance remain separate open gates. No production image, data, identities, schema or feature flags changed.
