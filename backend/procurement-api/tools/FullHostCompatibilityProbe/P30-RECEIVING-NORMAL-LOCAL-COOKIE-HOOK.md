# P30 receiving normal local-cookie proof (private only)

The opt-in `P30_HTTP_SERVE_RECEIVING_NORMAL_COOKIE=1` requires both
`P30_HTTP_VERIFY_PRINCIPAL_PO=1` and `P30_HTTP_VERIFY_PRINCIPAL_RECEIVING=1`
on a fresh `proc_p30_http_po_*` private PostgreSQL 17 database. It cannot run
with the PO cookie or retained-cancellation profiles. The ordinary native
receiving verification completes first. The hook then creates one **additional**
approved goods request, funded and approved PO, issued version, and manual
supplier acknowledgment through the actual API. It leaves that line unreceived
for the browser. No supplier send, stock transaction, Finance posting, or
employee identity is created by the cookie hook.

The protected 0600 fixture is
`/p25-evidence/p30-receiving-local-cookie-fixture.private.json` and the separate
challenged completion is
`/p25-evidence/p30-receiving-local-cookie-browser-completion.private.json`.
The fixture activates only its sixteen synthetic workerless Core accounts and
starts the real Program host with ordinary local password/session authentication.
The private subject header is explicitly rejected. Completion only ends the
listening control plane; it is **not** browser or owning-store acceptance.

Use `reference-ui/scripts/p30-receiving-cookie-browser-proxy.mjs` with its own
`P30_RECEIVING_COOKIE_*` inputs. The proxy binds the exact fixture SHA-256 in a
new protected receipt and passes cookies and request bodies through unchanged.
The isolated Playwright config/spec use the imported receiving editor and
detail, with separate receiver, inspector and acceptor logins. No routes or
business responses are mocked. The spec writes command keys and exact bounded
JSON bodies to a private result; credentials and cookies are never written there.

Before browser login, run `verify_p30_receiving_cookie_owner.py capture` and
then `check-baseline` against the private container. After the browser exits,
run its `verify` mode with the protected fixture, proxy receipt, baseline and
browser result. Its read-only owner snapshot rejects changes to native history,
Core identity and unrelated rows; it checks one new goods source, exact
receiving command/fact/counter graph, no stock or Finance posting declaration,
and revoked normal local sessions. The result remains private. This control
does not claim external supplier delivery or production deployment.
