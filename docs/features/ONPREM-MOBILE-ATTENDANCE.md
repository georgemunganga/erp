# On-prem employee mobile attendance

Owner: GreenWebb ERP engineering. Tier3: employee records and company boundaries.

Outcome: the same Vcron binary can discover an explicitly enabled standalone ERP,
search its opted-in companies, authenticate an existing local employee account and
read/punch its own attendance. Core owns local identities/session authority; HRM
owns employee assignment, branch confinement and attendance decisions. Hosted
product authentication, browser cookies and existing business endpoints are unchanged.

The feature defaults off and rejects product/OIDC-only hosts. Configuration pins an
HTTPS public origin and an explicit company/tenant allowlist. Search returns only
company ID/name/code for opted-in companies, or an exact private company code. It
does not register an installation in a central directory. Directory registration is
separate explicit operator work; the app also accepts a company ERP address.

Credentials are verified against an existing local account and its explicit WorkerId.
No account, worker, email/domain link, membership or role is created. Active employee,
company, location, current employee role and branch confinement are checked at login
and on every use. Password-required-change accounts must finish recovery in ERP.
Dedicated hashed mobile sessions are company/origin/worker/password-version bound;
short access tokens and rotating refresh credentials never authorize ERP cookies.
Mobile logout revokes its own session only. Existing browser sessions remain intact.

Attendance reuses IMobileAttendanceService, server timestamps, company timezone and
its exact expected date/record preconditions. Transaction/worker locks serialize
punches; retries converge without changing timestamps/auditing twice. Payload worker,
tenant, location, role or device timestamps cannot choose server authority. Offline
replay, location collection, reports, corrections and payroll are out of scope.

Acceptance: disabled routes404; company search minimal/private; real local login,
wrong/federated/inactive/unlinked/expired worker denied; cross-company/token/cookie
isolation; refresh rotation/revocation/password-change checks; in/out/retry/server
time and audit; negative direct URL/payload authority tests; existing local login
compatibility. Deploy first into isolated test data/host, with no customer migration
or automatic enabling. Additive session-table migration and rollback are documented
with executable tests before customer rollout. Disable flag and remove ingress to
roll back; do not delete or rewrite employee/attendance history.

## Operator rollout

1. Pin and build the reviewed ERP source, back up the target database and confirm
   its existing local login works. Do not substitute a customer database into a QA
   command. The new session table is additive; existing sessions/users are not
   converted. Apply the owning migration with the schema-owner connection and
   `dotnet Mightyfin.Erp.Hrm.Api.dll --apply-migrations-only`, then start runtime
   with its usual limited account. Normal production API startup must not have DDL.
2. Feature defaults off. Set `ERP__AuthMode=local`,
   `ERP__OnPremMobile__Enabled=true`,
   `ERP__OnPremMobile__PublicOrigin=https://erp.company.example`,
   `ERP__OnPremMobile__Companies__0__Id=<existing legal entity UUID>`,
   `ERP__OnPremMobile__Companies__0__Tenant=<exact existing tenant>`, and
   `ERP__OnPremMobile__Companies__0__Searchable=false` (private exact-code discovery).
   More explicitly enabled companies may use subsequent indexes. Enable name
   search only with that company's consent. No setting publishes to GreenWebb.
3. Route only `/mobile/v1/*` through the existing HTTPS proxy to the new API, with
   the exact configured Host, a4KB body limit, no credential/query logging and no
   redirects to another authority. Keep legacy ERP locations unchanged. Limit
   login/refresh by caller at the trusted ingress; the API also enforces bounded
   process limits. For a loopback proxy that overwrites X-Real-IP, explicitly set
   `ERP__OnPremMobile__TrustedProxyAddress=127.0.0.1`; other connected addresses
   cannot select their limiter partition through a forwarded header. Mobile
   sign-in additionally has its own5-attempt/15-minute company/identifier budget,
   including unknown identifiers. It never changes ERP account lockout counters.
   Never trust arbitrary forwarded IP/scope headers. Multiple replicas require
   coordinated ingress limits; process-local budgets are not a distributed limiter.
4. Existing eligible local account needs employee role, active employment, a current
   company-owned location and explicit LocalUser.WorkerId. This local account link
   is checked independently of any legacy IdP subject on Worker. The private
   server-created mobile attendance proof covers only that exact worker and local
   principal; it never grants another role or changes the legacy subject. An
   administrator must establish a missing link through a separately reviewed
   account operation. No mobile endpoint creates or repairs links or resets passwords.
5. Verify manifest/search, existing-account login, in/out/history, retries,
   company isolation and existing web-session continuity on isolated records. Keep
   the existing host and database untouched until this passes. Runtime upgrades
   require the owning operator's release/backup process; a git pull alone neither
   enables mobile nor moves local accounts into SaaS.
6. Roll back by disabling this feature/removing its ingress and returning to the
   previous application image. Retain the additive session table and attendance
   history; do not apply Down against live sessions casually. Expired/revoked mobile
   credentials can be removed by a separately scheduled maintenance job after the
   operator's retention period; retain consequential audit per HR policy. No
   destructive scheduled cleanup is introduced by this release.

## Pre-deployment checks

The focused HTTP/domain suite covers standalone eligibility/search/sessions/punches,
cookie/company isolation, web-session row preservation, existing native admission,
local role fallback and retry/timezone/overnight attendance rules. A Windows locale
test exposed culture-dependent mobile result formatting; only the mobile DTO's
clock strings now use invariant HH:mm. Server mobile payload timestamps remain
explicit ISO instants. Separate PostgreSQL/actual APK acceptance is recorded with
the installed source and artifact pins; mock/SQLite checks are not described as
customer rollout.

For a sidecar alongside an older ERP host, set `ERP__OnPremMobile__Only=true` only
after applying its additive owning migration. That mode checks the session table
and configured company, and serves only mobile/health routes. It does not run
full ERP migrations, initial-admin bootstrap, browser authentication endpoints or
scheduled business jobs. Do not blindly run the current full ERP model against an
older database: use the version-qualified compatibility build and its narrow
migration, preserving existing customizations and the running ERP host.

## Qualified deployment: New World Cargo, 17 November 2026

The live instance https://erp.newworldcargo.com now serves /mobile/v1 through a
separate mobile-only container, erp-hrm-mobile-api-20261117-01. Its source profile
pins the inspected classic baseline6079337e7a17107ad0b33fca59a267f6efc6927a and
shared mobile implementationb633b831f4840182faa6d957c2679bcda771938c. Prepare it
with tools/verification/prepare_classic_mobile.py from that shared implementation.
Archive SHA2569fb633da6367b90be8295927520c7b7702ab35327a9a20f8b0bcec41120a4be8.
The immutable manifest/publication is /opt/kuubu-builds/newworld-classic-mobile-b633b831-01.
The underlying container image supplies the existing runtime; the read-only mounted
publication and its manifest identify the executing mobile code, not that base image's label.

Apply only tools/verification/classic-mobile-schema-v1.sql. Before production it
was rehearsed twice on a private database restored from the real instance's backup:
all existing row fingerprints across96 tables remained identical. The full ERP
migration history was untouched. A second qualification copied schema only to an
isolated database, seeded two synthetic companies, preserved different legacy IdP
subjects, and passed the classic HTTP/domain suite23 cases plus real PostgreSQL
admission/isolation/refresh/revocation and the installed Android app's complete
two-company check-in/out/switch/restart flow. Modern-schema qualification also
completed both companies. Synthetic attendance is not real customer staff testing.

The production adapter uses a separate SQL role: SELECT, and INSERT/UPDATE only
for mobile sessions, attendance and audit. It has no account/payroll mutation
privileges. Its new loopback listener28915 and Docker alias kuubu-mobile-api are
used by the sole added Nginx /mobile/v1/ location. Existing API/web/proxy/database/
outbox container IDs and images, three local source customizations and account
fingerprints were preserved. The existing proxy was validated and gracefully
reloaded; its bind-mounted config inode was preserved. No ERP process was restarted.

Public HTTPS checks confirmed manifest/company search, denied anonymous/cookie/
unknown-account mobile access, and existing HTML, health and browser-auth endpoints.
The installed APK searched Newworld and reached that server's existing ERP login.
The adapter deployment created no customer employee punch, login-to-employee
association, new staff account or compensation. Existing local administrators have no branch restrictions;
hr_admin's configured permissions expand to the existing local administration roles.

Operations, original proxy bytes, release receipts and private backup/restore are
on the owning server at /opt/kuubu-mobile-qualification/20261117-01. Runtime-only
configuration is /opt/erp-hrm/mobile/20261117-01/runtime.private.env. Rollback restores
the recorded original Nginx bytes in place, validates/reloads Nginx, then stops only
the mobile container. Retain session/attendance/audit evidence and the original ERP.
The three inspected local accounts had no employee links at adapter rollout.
The user subsequently selected an existing staff login and employee, reviewed the
different account/employee name spellings, and explicitly requested a new Head
office for that employee. A separate backed-up operator transaction created that
location, linked the selected existing account/employee and assigned the location,
with three audit entries. No new staff, pay, role, legacy subject or password change
was made. Private evidence is /opt/kuubu-mobile-qualification/rose-access-20261117-01
and local inspection/qualifications/newworld-rose-access-20261117-01. The employee
had no recent/open attendance records. First-time password setup is still required;
the user was handed the existing ERP/Android login forms. The required password
completion is not bypassed. Actual customer sign-in/punch qualification remains
pending that handoff; no real staff attendance has been fabricated.
