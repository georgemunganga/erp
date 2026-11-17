# September local-account compatibility profile

This compatibility build pins ERP baseline6079337e7a17107ad0b33fca59a267f6efc6927a,
the inspected New World Cargo checkout. It reuses the current OnPremMobileApi,
mobile-only host, local session entity, server-clock attendance command and tests.
prepare_classic_mobile.py writes an exclusive independent build snapshot and a
hash manifest. It does not modify a customer checkout or copy customer secrets/data.
No per-customer mobile application or central SaaS account is required.

Model adaptations are explicit: this baseline has only local credentials, no
federated credential-mode column; company timezone comes from explicit deployment
configuration rather than a new legal-entity column; new session foreign keys use
the existing primary keys. The API still requires a current local account's
explicit WorkerId, employee role, active employment and company-owned branch on
every request. It does not rewrite legacy IdP subjects or grant roles.

Set ERP:OnPremMobile:Only=true, its exact HTTPS public origin and a company/tenant
allowlist. Each company entry additionally requires TimeZoneId (Africa/Lusaka for
the inspected Zambian employer). This host has no browser routes, initial admin,
business migration execution, scheduled leave accrual or outbox publisher. Use a
separate runtime database role, read-only published code and bounded resources.

The inspected baseline contains inherited differences between its EF snapshot and
its application model. A generated full migration proposed unrelated worker,
payroll, branding and contract changes. That proposal is **not** the mobile release
and must not be applied. Use only classic-mobile-schema-v1.sql: an independently
versioned extension receipt and the new mobile session table/indexes. It leaves the
existing ERP migration history and business schema intact. Back up the database,
restore privately on the owning server, rehearse the extension twice and verify
existing schema/row fingerprints and web-session continuity before production.

The new full ERP model has a separate owning mobile-session migration. A future
full ERP upgrade from this classic extension requires an explicit, reviewed schema
adoption step; do not run a blind git pull/migration over a customer installation.
This does not create a second source of attendance/payroll decisions: the extension
reuses the installed version's domain service. Rollback removes the mobile ingress
and stops only its sidecar, retaining its session table and attendance/audit history.

New World Cargo has three existing local accounts and no explicit employee links
at inspection. Deployment must not silently associate them by email/name or enroll
all employees. The local administrator chooses account/employee associations using
the ERP's existing account management. Qualification uses isolated synthetic
employees; it never punches attendance for an existing customer employee.
