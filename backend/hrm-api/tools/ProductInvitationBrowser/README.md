# Actual-provider invitation browser qualification

Opt-in test driver for `ProductInvitationBrowserInteropTests`. It is never
imported by the deployed application. It runs the existing Kuubu website and ERP
UI against actual Laravel Passport/resource handlers and ASP.NET Core handlers.

Prepare a new private fixture using the Foundation tools
`product-identity-acceptance.py --prepare-only` and
`seed-core-product-interop.py --with-setup-owner`. Both verified synthetic people
must initially have no personal product enrollment. Do not seed a preaccepted
Core membership. Enable `PRODUCT_INVITATION_RECIPIENT_ENABLED=true` only in that
fixture's server environment. Disable real mail/payments and redirect database,
keys, storage and all Laravel cache paths into the private fixture.

Start only the owned Laravel listener on `127.0.0.1:8101` and the qualified ERP
SSR listener on `127.0.0.1:3186`; refuse occupied ports. The test owns its bridge
on `127.0.0.1:8121`. Set absolute paths in these environment variables:

| Variable prefix `KUUBU_REAL_BROWSER_INVITATION_` | Value |
| --- | --- |
| `FIXTURE` | Private `core-interop.json` manifest |
| `RUNNER` | This directory's `browser.mjs` |
| `WEBSITE` | Independently qualified website source, `dist`, and session relay |
| `OUTPUT` | New, nonexistent browser evidence directory |
| `PLAYWRIGHT` | Qualified `@playwright/test/index.mjs` |
| `PROVIDER_ROOT` | Qualified Laravel backend with dependencies |
| `PHP` | PHP executable compatible with that backend |

Build tests into a new exclusive artifacts directory with references to the
qualified runtime. Run VSTest with
`/TestCaseFilter:FullyQualifiedName~ProductInvitationBrowserInteropTests`.
Without the explicit fixture variable, the test skips. Retain failed attempts;
use fresh fixture and result directories for every retry.

The driver checks owner login/enrollment/setup, one company and exact creation
retry, real sender invitation creation, account switching, recipient login and
explicit enrollment, separate recipient-verification consent, company/role
confirmation, actual atomic acceptance, response loss after commit, exact retry,
subscription suspension and denied replay. Account authentication alone must
create no Core membership. Membership acceptance must create no local password,
ERP operational session, billing membership or module grant.

Every browser request and redirect is intercepted through Chrome DevTools Fetch;
DNS resolution is disabled, origins are allowlisted and service workers blocked.
Original HTTPS origins and browser cookie behavior are retained, but this does
not qualify deployed TLS or production ingress. Logs contain paths/statuses only,
never OAuth parameters, invitation secrets, cookies or request bodies. No traces
are collected. Credentials, delivery URLs and fixture markers stay private.

The marker-protected, loopback-only `/delivery-preview` endpoint decrypts the
actual committed delivery intent for the fixture. It is never mapped in the
production application and does **not** prove mail delivery. The other fixture
endpoints inspect persisted counts or suspend the synthetic subscription.

Always stop owned processes, verify all three ports are closed, independently
audit the Core and Foundation SQLite databases read-only, and compare retained
source/build manifests after the run. Do not mutate retained build publications.
Actual delivery, anonymous prepare capacity controls, operational ERP admission
and public deployment remain separate release gates.
