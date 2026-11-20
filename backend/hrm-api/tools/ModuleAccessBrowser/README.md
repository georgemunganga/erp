# Private module-access browser qualification

This opt-in tool belongs to `ProductModuleAccessBrowserInteropTests`. It is never
imported by the production API or frontend. It exercises the branded Kuubu
website relay, real Laravel/Passport, the actual Core endpoints and a fresh
PostgreSQL database. Its CDP adapter carries real responses at the reviewed
origins; it does not replace authorization responses with fixtures.

The owning test requires absolute `KUUBU_MODULE_BROWSER_MANIFEST`, `_TLS`,
`_RUNNER`, `_WEBSITE`, `_OUTPUT` and `_PLAYWRIGHT` paths, plus a private `_POSTGRES`
connection string. `_RUNNER` points to `real-module-editor-browser.mjs`.
Without the manifest, test discovery explicitly skips this test. Do not populate
these settings from production credentials or print their contents.

Prepare an independent Laravel fixture using the Foundation repository's
`tools/product-identity-acceptance.py --prepare-only` and
`tools/seed-core-product-interop.py --with-setup-owner`. The qualification owner
must register distinct setup, operational and machine-licensing clients, enable
operational identity only in that fixture, provide two purchased workspaces,
license HRM and Procurement, and retain the private manifest. The website source
must include its actual product-authorization relay and explicit setup/operational
consent screens. Google credentials, payments and outgoing mail stay disabled.

Use fresh independent working directories and database volumes for every attempt.
The retained Windows qualification controller is recorded in the feature spec;
it prepares the fixture, private test CA/certificate, and owned processes. It
enables OPcache only for the disposable PHP development-server process (the real
relay deadline is unchanged), and publishes PostgreSQL only on `127.0.0.1:8135`; PHP uses loopback port8101, the Core
bridge8121, TLS8143, and real-API frontend SSR3186. The C# test creates a fresh
random database, applies owning migrations and asserts no model drift. It accepts
only the explicit test control database/loopback endpoint. Do not reuse a running
production service on these ports.

The C# transport retains `https://commercial-api.greenwebb.tech`, trusts only the
fixture CA in that client, pins TCP to loopback, and checks wrong-hostname rejection.
Chrome uses a fresh private context, no service workers or personal profile, and
failed-closed DNS. Every navigation hop is intercepted. The application sees
actual statuses, redirects, cookies and CSRF decisions. No global trust-store or
DNS changes are needed. This is **not** deployed browser TLS or ingress evidence.

The journey creates two companies through explicit consent, reviews one company's
owner, records a branch-scoped module grant, loses the first committed response,
retries its exact key, revokes, replays the historical command, grants again,
rejects a foreign-company link and rejects current access after actual subscription
suspension. It checks PostgreSQL receipts/audits, sign-out cleanup and zero ERP
sessions/legacy-role assignments. Local storage must be empty. Session storage may
contain only the router's documented scroll-restoration cache, checked as opaque
history keys and empty/window numeric coordinate records; no credentials, grants
or setup attempts may remain. Permission decisions do not themselves grant ERP
entry. Bound operational ERP sessions and protected route/data isolation require
separate qualification.

Retain failures, private diagnostics, TRX, hashes and screenshots under a fresh
exclusive evidence root. Diagnostics can contain OAuth callback data and must not
be published or printed. Publish only sanitized results. Stop all owned children
and the fixture database in cleanup; preserve the retained evidence and volume.
