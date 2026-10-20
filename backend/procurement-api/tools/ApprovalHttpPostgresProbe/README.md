# Procurement approval HTTP/PostgreSQL verification

This is a disposable verification program, not a runtime service. It requires a fresh database
named `proc_approval_http_<unique>`; other database names are rejected. Never use production.
Set `PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION` privately and run:

`dotnet run --project backend/procurement-api/tools/ApprovalHttpPostgresProbe`

It creates isolated Core/HRM, Procurement and shared approval model tables, seeds synthetic
canonical accounts and an explicit server-side **fixture** manual Finance policy, then exercises
real mapped HTTP routes and PostgreSQL transactions. Authentication is a fixture scheme;
authorization, current account grants, organizational scope, financial authority, policy,
source pinning, approval execution, audit/outbox, and persistence are the implementation.
No business API or database is mocked.

Coverage includes a sequential `One` then parallel `All` route; assigned queue/detail/history;
source/line effects; exact command-response replay; changed-command and revoked-authority
denial; changed Finance allowing return while preventing approval; manual full release and
resubmission; receipt-write failure after earlier context saves proving whole transaction
rollback; concurrent votes with explicit version conflicts and recovery; final approval racing
a Finance change. The synthetic receipt-failure trigger is installed only inside the guarded
disposable database and removed before retry.

This program rehearses the mapped model. Migration idempotency, runtime database grants,
production configuration, notification dispatch and browser verification remain separate gates.
No email, payment, reservation API call or production write is performed. A manual Finance
attestation is never represented as an automated external confirmation.

The probe explicitly registers its disposable connection as
`ConnectionStrings:ProcurementApprovalExecution`. Production activation must instead use a
verified dedicated execution database role; the probe's owner-level schema setup cannot
establish that production permission gate.

## Actual browser sandbox mode

With a **fresh**, separately provisioned `proc_approval_http_<unique>` database and the same
private connection environment variable, run the optional mode:

```sh
dotnet run --project backend/procurement-api/tools/ApprovalHttpPostgresProbe -- --serve
```

This listens only on IPv4 loopback (`127.0.0.1:5197`). Override the port with
`PROCUREMENT_APPROVAL_HTTP_PROBE_PORT` (1024–65535). Do not expose or proxy this sandbox
on a public interface: its `X-Probe-User` header authentication is deliberately test-only.
It maps the real complete Procurement routes, including `/context` and `/meta`. Core/HRM
identity tables are synthetic fixture tables; Procurement and shared Core approval tables
are created using their **committed migrations**. Serve refuses a model newer than its
migration snapshot. It is therefore only runnable after all model changes have migrations.

Serve seeds two **unopened**, submitted requests with controlled manual Finance evidence,
and finite independently activated spending authority for the two manager identities. It
does not consume the normal HTTP verification scenarios. The configured route is:

1. NEED: current requester line manager (`manager1`), `One`.
2. PROC: explicitly authorized procurement managers (`manager1`, `manager2`), `All`.

Actor `buyer` can open cases; `requester` owns the requests; `finance` can independently
record the controlled manual Finance release after a return. Exact current roles, worker
IDs and permissions are obtained from the actual `/context` endpoint and written into the
manifest, together with raw seeded request responses and company/branch IDs. Pass these
IDs in `X-Probe-User`, `X-Shell-Entity` and `X-Shell-Location` when forwarding browser API
requests. Forward business requests/responses unchanged; do not mock approval results.

A generated private directory contains `manifest.json` and its path is printed. Optionally
set `PROCUREMENT_APPROVAL_HTTP_PROBE_MANIFEST` to an absolute **new file** path; an existing
parent directory must be private to its owner. Directories/files are created with owner-only
permissions on Unix. The manifest contains no database credentials or login passwords.
Keep the process running during browser verification; stop it afterwards and remove the
disposable database and manifest using the sandbox operator's normal cleanup procedure.
This mode is verification tooling and does not enable production approvals or prove
production least-privilege database grants.


## Dedicated least-privilege runtime rehearsal

The optional `PROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION` separates fixture setup
from the app. Both private connection strings must point at the same fresh guarded database
and endpoint. `PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION` then identifies the **runtime**
login; it must have a distinct username beginning `proc_approval_runtime_`, no memberships,
no object/database/schema ownership, and no superuser, create-role, create-database,
replication or bypass-RLS privileges. The bootstrap login provisions the disposable schemas
and grants; none of its credentials enter the app's service provider or execution options.

An operator may create an empty rehearsal login using their private administrative session:

```sql
CREATE ROLE proc_approval_runtime_example LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE
  NOINHERIT NOREPLICATION NOBYPASSRLS;
```

Set authentication credentials through the operator's protected PostgreSQL credential
procedure (for example interactive `\password`); never put passwords in this repository,
command examples, source, manifest or test output. Provide both connections through private
environment configuration, then run the normal verification command or `--serve`. No role
is automatically created, and an existing elevated role is rejected rather than modified.

Bootstrap setup revokes PUBLIC table grants and schema/database CREATE/TEMP rights only in
this disposable database, then grants explicit rights to the existing runtime login:

- Core cases, stages and assignments: SELECT/INSERT/UPDATE; decisions: SELECT/INSERT;
  delegations and migration history: SELECT only.
- Procurement request headers/lines and authority grants: SELECT/INSERT/UPDATE; draft
  authority category replacement: SELECT/INSERT/DELETE, guarded by the domain's draft rule.
- Submission, assignment evidence, command receipts, outcomes, Finance evidence/releases,
  policy decisions, audit and outbox: SELECT/INSERT, with no UPDATE/DELETE.
- Referenced Procurement categories, policies, catalog and supplier qualification records:
  SELECT only. Schema history receives SELECT when migrated serve mode needs it.
- HRM canonical account, worker, organization, role and branch references: only enumerated
  SELECT columns needed by current adapters. Password hashes and unrelated HR fields receive
  no SELECT. No identity/organization table receives write grants.

The app's ordinary Procurement/Core contexts, scope middleware, fixture authentication
projection and shared transaction coordinator all use the runtime connection. DDL, fixture
seed/role/offboarding changes, deliberate evidence corruption and failure-trigger setup use
an entirely separate bootstrap-only service provider/connection. Do not broaden runtime
grants to make fixture manipulation succeed.

The tool explicitly asserts permission denial for schema/table/temp DDL, HRM account and
workforce/organization/role writes, password-hash reads, Core decision mutation/deletion,
and all immutable Procurement evidence/audit/outbox mutation/deletion. Even unexpected DDL
success is rolled back before failing the gate. These checks precede the actual mapped
HTTP approval/return/release/concurrency/rollback scenarios under runtime credentials.
Unexpected API permission failures are defects to investigate, not permission to grant
ownership or blanket write access. This is source for a disposable privilege rehearsal;
only a completed runtime test establishes its result, and real production rollout still
requires protected role provisioning and independent release review.

## Actual authority-settings browser fixture

`--serve` additionally seeds a synthetic read-only settings account, gives the existing
independent Finance reviewer `procurement-authority-read`, and confines both to the fixture
branch using Core-owned branch assignments. These are disposable bootstrap rows, not a
production role change. The protected manifest includes `actors.reader` and a `settings`
seed descriptor; it contains no password, connection string, bank detail or token. Existing
finite approval grants remain unchanged. The browser creates a separate named branch-only
limit with exact `99999999999999.9999`, an explicit expiry, and independent Finance activation.

After source compilation and migration compatibility have been verified and an operator has
started a fresh restricted-runtime `--serve` sandbox, run the frontend test against its
separately verified staged settings assets:

```sh
PROCUREMENT_APPROVAL_BROWSER_MANIFEST=/absolute/private/manifest.json \
  ./node_modules/.bin/playwright test tests/backend-browser/authority-settings.spec.ts \
  --config playwright.backend.config.ts --workers=1
```

The test refuses non-loopback/non-synthetic manifests and group/world-readable files. All
Procurement traffic, capabilities, lookups, actions and state come from the real mapped API;
only HRM shell login/branding presentation is synthetic. It checks persisted exact amount,
branch scope, creator self-review denial, independent activation, read-only current actions,
server denial of reader mutations and absence of private identity/payment fields. Use a
fresh disposable fixture for a rerun after a committed create; do not erase evidence/reset a
consumed record to make a test pass. Source is authored only until a recorded execution gate.
