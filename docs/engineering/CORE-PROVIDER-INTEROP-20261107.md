# Actual product provider interoperability qualification

This qualifies the existing CORE-PRODUCT-IDENTITY and CORE-COMMERCIAL-LICENSE
contracts. It adds test tooling, not a production URL override or admission API.

The opt-in ProductProviderInteropTests uses the actual ASP.NET OAuth/cookie
handlers and Core licensing client. A fresh Laravel fixture supplies real
Passport clients, RSA keys, code exchange, bearer resources and subscriptions.
The existing published Kuubu account client and signed relay obtain the code
from ASP.NET's original S256 challenge/state. No fake provider JSON or token is
returned by the .NET backchannel adapter.

Expected acceptance: personal enrollment required before authorization; explicit
join; confidential PKCE exchange and immutable verified subject; code replay
denial; token-free pending cookie; no ERP session or tenant grant; real machine
license without staff billing membership; foreign-product rejection; suspension
denies the next license read; bounded lease/proof expiry; independent before/after
provider counts and empty Core principal/link/session tables.

Loopback test transport is intentionally narrower than a deployment gate. It
maps only the pinned provider token/subject/licensing paths to the synthetic
loopback API. Production issuer and callback checks are unchanged; there is no
certificate-validation bypass. This does not qualify public TLS, DNS/proxy logs,
a real browser callback, operational sessions or company/module authorization.

Use a .NET10 SDK, locked foundation dependencies with PHP8.4/SQLite, Python and
Node, plus the exact published Kuubu website source. On the owning workstation:
```text
python backend/hrm-api/tools/ProductProviderInterop/run.py --workspace <new-absolute-exclusive-directory> --foundation <foundation/backend> --website <exact-kuubu-source> --php <php8.4-path> --ssh-host <authorized-qualification-host>
```
The helper prepares fresh fixtures with mail=array/payments=false, owns PHP and
Node processes, and creates temporary SSH forwards bound only to remote loopback
28101/28120. Local ports8101/8120 must be free. Stop the owning helper after the
gate: its finally block stops its direct children and retains logs/data/receipts.
Do not rerun a consumed fixture; generate a new exclusive workspace.

Copy only the synthetic provider/core-interop.json manifest privately to the
qualification container and set KUUBU_REAL_PROVIDER_FIXTURE to that private
absolute file. Never print or commit it; it contains only fresh synthetic client
credentials, not production credentials. The bridge verifies the same fixture
marker. Run the single opt-in ProductProviderInteropTests case with the two
loopback forwards reachable. It is explicitly skipped when no fixture is set.
Normal existing tests and the production API remain independent of this fixture.

Retain exact source/website hashes, native SDK, logs, counts, job status and
cleanup evidence. Company/payer binding and setup authority, invitations,
protected operational grants/refresh/revocation, public SaaS local-auth policy,
runtime ACLs, UI and company/branch/module enforcement remain required after a
successful protocol gate. No public launch follows automatically.

Qualification results will be recorded after the real gate completes.
