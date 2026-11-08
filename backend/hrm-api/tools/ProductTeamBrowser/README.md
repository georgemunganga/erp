# Actual Kuubu team browser qualification

This runner belongs to `ProductTeamBrowserInteropTests`. It uses the published
Kuubu website session relay, actual local Laravel/Passport, unchanged Core APIs
and the built ERP `/setup` and `/team` pages. It is never a deployed API or seed.

Prepare a **fresh private** Laravel fixture using the Foundation
`tools/product-identity-acceptance.py --prepare-only` and
`tools/seed-core-product-interop.py --with-setup-owner` helpers. Its database,
keys/storage/cache must be fixture-owned; mail=array, payments disabled and Lenco/
Perfex credentials empty. Give the verified synthetic staff identity an explicit
personal Kuubu enrollment as a precondition, with zero payer memberships. The
test-only, marker-protected Core bridge seeds its preaccepted company membership.
This does not implement or qualify invitations/acceptance/delivery.

Run PHP only on loopback8101 and a frozen Node SSR build on loopback3186. The
opt-in test owns loopback8121; refuse occupied ports. Do not alter retained
publications or reuse a mutable fixture. Set these absolute-path variables:

- `KUUBU_REAL_BROWSER_TEAM_FIXTURE`: private `core-interop.json`.
- `KUUBU_REAL_BROWSER_TEAM_RUNNER`: this `browser.mjs`.
- `KUUBU_REAL_BROWSER_TEAM_WEBSITE`: published website source with API-mode `dist`.
- `KUUBU_REAL_BROWSER_TEAM_OUTPUT`: a nonexistent output directory.
- `KUUBU_REAL_BROWSER_TEAM_PLAYWRIGHT`: `@playwright/test/index.mjs` from pinned UI dependencies.
- `KUUBU_REAL_BROWSER_TEAM_PROVIDER_ROOT`: qualified Laravel backend root.
- `KUUBU_REAL_BROWSER_TEAM_PHP`: PHP executable.

Filter .NET tests to `ProductTeamBrowserInteropTests`. Without the fixture variable
the case skips. Do not print manifests, cookies, keys, environment values or OAuth
parameters. The runner logs only origins/paths, fixed actions, status and timing.
Write/read the script as UTF-8; Windows-default CP1252 broke a prior assertion.

Chrome uses a fresh context and DNS disabled. Every navigation/redirect/request
is intercepted with CDP Fetch; only the pinned account/app origins go through
loopback services. Off-fixture traffic aborts. No TLS exception or user browser
profile is used. This validates protocol/cookie behavior, not deployed TLS/DNS.

The case covers login/Google unavailable, explicit enrollment/setup consent,
create/retry, setup-cookie rejection for team access, fresh team sign-in, licensed
members, confirmed role change, response loss after commit/exact-key retry,
revocation, last-owner denial, subscription suspension and replay denial. Core
default ERP access must remain401. Read-only audit must independently verify
identity/tenant links, receipts, audit/outbox, revisions and zero credentials,
operational sessions or module roles. Stop every owned process and verify ports
closed; retain failed attempts and private diagnostics without publishing secrets.

The current runner requires setup response `team_management_available=true` and
clicks the visible fixed `/team` link. Use matching API/UI builds; the prior
API2607cd69/UIaaafdf25 pair does not implement that new entry contract. The updated
combined gate passed41.395s; evidence is
inspection/qualifications/team-entry-browser-01/verification.json. No operational
admission or invitation acceptance is implied.

Known successful reference: source API2607cd69/UIaaafdf25/websiteb01985ee,
36.589s; evidence `inspection/qualifications/real-team-browser-acceptance-03`.
