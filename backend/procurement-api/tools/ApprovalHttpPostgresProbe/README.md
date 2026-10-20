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

## Initial PO workflow probe — source candidate

Single `--orders` runs separately from single `--serve` and the original PR probe; the explicitly documented browser pair below is also supported. It requires the existing disposable `proc_approval_http_...` database guard, a distinct `proc_approval_runtime_...` login and bootstrap connection, and committed Procurement/Core migrations matching the source model. It applies those migrations in the disposable database and provisions only the synthetic runtime grants required by the first PO route. It does not provision or contact production.

After the centralized build/migration gates clear, use the same approved environment variables and run the tool with `--orders`. This source adds actual HTTP evidence for scoped positive PR conversion, exact retries and denied allocations, independent Finance/Core approval, unique commitment ownership, supplier eligibility, seal versus delivery, unfunded cancellation, revoked-authority replay and a deterministically queued branch-revocation scenario. It has not been run in this increment.

The queued-scope test is intentionally strict and may expose a shared authorization concurrency gap: a SERIALIZABLE transaction retains its first-statement snapshot. A post-lock query alone cannot prove observation of revocation committed during the wait. Retain any actual failure and resolve the shared Core authorization/fencing semantics; do not substitute an InMemory or pure helper test or weaken isolation to make the probe green. The first PO deployment remains disabled pending those gates and P24 review/rehearsal.

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

## Actual order browser fixture — source candidate

The explicitly supported pair `--serve --orders` runs the migrated loopback browser sandbox
with the real initial order feature enabled **only in this synthetic process**. Single
`--serve`, single `--orders`, single `--settings` and the original no-argument PR verification
retain their behavior. Duplicated flags, unknown flags, `--serve --settings`,
`--orders --settings` and three-flag combinations are rejected.

The pair requires both existing private connection variables, a fresh guarded disposable
`proc_approval_http_...` database, a distinct finite `proc_approval_runtime_...` role, and
committed Procurement/Core migrations matching the model. The bootstrap connection owns
fixture DDL/master/role setup; HTTP runtime still cannot write HRM or mutate Core decisions.
No production identity handler, config flag, database or listener is changed.

Before publishing the private0600 manifest, it activates finite PR/PO authorities through
the real configuration lifecycle, seeds the approved direct-buy method/PO policy, and
creates an additional PR using actual create → submit → controlled Finance evidence →
Core One/All decisions. It asserts the resulting approved quantity is1 and allocated/ordered
quantities are0. This source is not consumed by a fabricated PO or by the standalone order
verification suite. The manifest `orders` entry supplies its source/line/supplier/reservation
references, exact1×100 ZMW values and separate buyer/Finance/reviewer/dispatcher identities.

Only after that legitimate setup, the pair narrows synthetic roles: Finance loses general
`procurement-po-read`; manager2 becomes a PO-only reviewer with access+po-approve;
manager1 becomes a sending-only operator with access+po-dispatch-confirm. Neither reviewer
nor sending operator has PO read/PR read/PR decision permissions. The browser therefore
must exercise purpose-specific order reads and the PO-only assigned queue. Actual context
responses record these permissions. Original single-flag fixtures remain unchanged.

Run only after centralized compile/migration/assets gates have passed:

```sh
# Connections are privately supplied; never put their values in a command or artefact.
PROCUREMENT_ORDER_BROWSER_VERIFY_ON_STOP=1 \
  dotnet run --project backend/procurement-api/tools/ApprovalHttpPostgresProbe -- --serve --orders
```

With that optional verification flag, gracefully stopping the process **after** the browser
flow queries the actual disposable PostgreSQL records through the fixture-owner inspection
scope. It requires exactly one allocation, order line, Finance confirmation, submission,
Core approval decision, issued revision and delivery; exact1×100 ZMW; distinct expected
actors; and source ordered1/allocated0. A premature stop fails rather than claiming proof.
Do not terminate by SIGKILL if requesting this DB check. The flag is irrelevant to the
original modes. A failed browser/DB check is a retained gate failure, never a reason to
fabricate state or relax runtime grants.

The companion frontend `tests/backend-browser/orders.spec.ts` forwards every Procurement
response unchanged and performs convert → independent Finance → buyer submit → assigned
PO approval → buyer authorise → independent sending evidence. It verifies exact read
strings, original reservation references, PO-only navigation, immutable outcome counts and
exact delivery replay through real API reads. Shell authentication/branding alone are
synthetic presentation fixtures. This increment is **source-only and unverified**; it has
not run a compiler, browser, database, migration or server and does not enable live orders.
Use a fresh fixture for each destructive business-flow test and clean it up afterwards.

## Receiving positive adapter proof — unverified source

Single `--receiving` is an isolated, restricted-runtime actual HTTP/PostgreSQL probe. It
requires the existing private disposable DB/bootstrap/runtime guards and committed P25
migrations matching the model; it must not be launched while the source model is ahead.
It reuses actual PR approvals and issued/delivered PO commands, then publishes a RECEIVING
policy through real Draft/Review/independent Publish routes (future effective date and
bounded wait). No approved receiving rules, acceptance effects or Core votes are fabricated.

It authors successful draft-without-policy, blocked record-without-policy, truthful goods
overdelivery2 against accepted authority1, named independent inspection, positive acceptance,
service certification, exact immutable responses/retries, denied edits/over-capacity,
concurrent partial acceptors (one win/one409), and current permission revocation. Direct
fixture-owner SQL deliberately rehearses one inherited nonzero historic counter; physical
truth remains recordable but certification must block unknown history. Those fixture writes
never widen the HTTP runtime's grants. All proof output is synthetic and no credentials
are printed. Only receiving mode grants finite relevant receipt tables and policy lifecycle
DML to the private runtime. This source has not been built/run and is not completion evidence.

This branch's program additions are based on e752ed1; when combining with the separate
order-browser `--serve --orders` increment, preserve that explicitly allowed pair while
keeping `--receiving` single and rejecting other unsupported/duplicated combinations.


Receiving review follow-up (source only): `--receiving` now includes an inactive historical
PO-line acceptance cache that must block certification without erasing physical truth,
and a separately governed service policy restricted to another site that must deny an
otherwise named organisation-wide owner. Existing receiving policy creation/publication
is used; no synthetic published receiving policy or successful owner posting is fabricated.
The probe remains unrun for P25 until its migration and centralized runtime gate are ready.

### Invoice comparison unavailable-owner boundary (source authored; unexecuted)

`--matching` is mutually exclusive with all other modes and includes the reviewed receiving
fixture before the new invoice adapter scenarios. It requires the eventual P25/P26 migrations,
separate guarded disposable bootstrap/runtime users and an explicitly unpublished-production
fixture. It exercises actual invoice mapping, independently governed matching policy, durable
Blocked comparison under the real unavailable owner/reviewer adapters, exact retry/recovery,
permission/body-conflict denials, exceptions/outbox and no fabricated capacity/AP effects.
It does not prove a real Finance owner/reviewer integration or final payable approval.
No new mode or grant in this tool authorizes production mutation.

