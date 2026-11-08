# Actual product-provider qualification

These tools own fresh loopback fixtures for the real Laravel Passport provider
and the maintained Kuubu account client/signed relay. They create no production
keys, customers, memberships, subscriptions or Core records. Email uses the array
driver; payments are disabled. Preserve all consumed or failed fixture directories.

`run.py` requires absolute `--workspace` (must not exist), `--foundation` (the
Laravel backend directory), `--website` (exact published website source), and
`--php` executable paths. It verifies ports8101/8120 are free, prepares its own
SQLite database/keys/storage, starts only owned loopback children, and stops them
on interruption. Keep its logs and `stopped.json`; never print private manifests.

Ordinary staff qualification uses the default mode with **zero billing
memberships**. Set `KUUBU_REAL_PROVIDER_FIXTURE` to its private
`provider/core-interop.json`, leave `KUUBU_REAL_SETUP_PROVIDER_FIXTURE` unset, and
run only `ProductProviderInteropTests` against the compiled test assembly.

Protected setup qualification is a separate fresh run with `--with-setup-owner`.
That explicit flag seeds one synthetic owner membership; staff retain none.
Set `KUUBU_REAL_SETUP_PROVIDER_FIXTURE` to that run's private manifest, leave the
ordinary variable unset, and run only `ProductSetupProviderInteropTests`.
Do not run these consuming cases concurrently or reuse a consumed fixture.

The protected case performs ordinary staff sign-in first, then separately
consented owner setup, explicit personal enrollment and real confidential S256
exchange. It checks the actual payer list/authority and machine license, company
create/retry/capacity, revoked owner and suspended subscription denial, cancel
invalidation, role-free owners and zero operational sessions. Owner revoke/restore
and subscription suspension endpoints are fixture-only controls, gated by the
private marker and opt-in mode; they are not application routes.

The test transport adapter accepts only the pinned HTTPS commercial issuer and
reviewed protocol paths, then directs those requests to its actual loopback
Laravel instance. It fabricates no provider response and changes no production
TLS/issuer validation. Core allocation uses a real private SQLite database;
separate PostgreSQL gates qualify native schema/concurrency. These cases do not
prove deployed browser TLS/ingress, production PostgreSQL interoperation,
operational admission, payment processing or a customer SaaS launch.

Record exact ERP/foundation commits and website runtime Git blob hashes before
execution. Use an exclusive new qualification checkout, plain cache copies,
verified SDK and fresh result/log paths. The private manifest contains synthetic
client credentials; no tokens, cookies, OAuth state, passwords, request bodies or
manifest content belong in public logs or PR descriptions. Read-only aggregate
provider/Core database audits are suitable evidence after the processes stop.

## Browser setup qualification

Use a separate fresh `--with-setup-owner` fixture. Set these absolute paths:

- `KUUBU_REAL_BROWSER_SETUP_FIXTURE`: private `provider/core-interop.json`.
- `KUUBU_REAL_BROWSER_SETUP_RUNNER`: this directory's `browser.mjs`.
- `KUUBU_REAL_BROWSER_SETUP_WEBSITE`: exact source with its API-mode website build.
- `KUUBU_REAL_BROWSER_SETUP_OUTPUT`: exclusive, not-yet-existing output directory.
- `KUUBU_REAL_BROWSER_SETUP_PLAYWRIGHT`: installed `@playwright/test/index.mjs`.

Serve the built ERP setup UI on loopback3186 and run only
`ProductSetupBrowserInteropTests`. The test owns the marker-protected Core bridge
on loopback8121. It invokes the real website session relay directly, while Core's
real provider channel uses the fresh Laravel service. Do not expose the bridge.

The runner uses Chrome DevTools Fetch interception for **every redirect hop**;
Playwright's higher-level routing intercepts only the first URL of a redirect
chain. Original HTTP statuses, Location and Set-Cookie are processed by Chrome.
DNS resolution is disabled for this isolated browser, service workers are blocked,
and non-fixture origins are aborted. No certificate exceptions or existing user
profile are used. Diagnostics contain origins/paths and error types only. Missing
static files receive404 as on an ordinary static server.

The browser executes own branded login, explicit personal enrollment and setup
consent, verifies zero pre-confirmation companies, confirms one creation, reloads
and retries the same command, checks separate secure HttpOnly account/Core
cookies, and verifies operational access remains401. The test also verifies one
company/owner/link/audit/outbox and zero sessions/module-role grants in actual
Core SQLite. Screenshots and a bounded `result.json` are retained. This remains
isolated browser acceptance, not deployed DNS/TLS or live SaaS acceptance.
