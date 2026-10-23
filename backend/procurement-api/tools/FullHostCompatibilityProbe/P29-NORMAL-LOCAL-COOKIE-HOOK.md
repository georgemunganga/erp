# P29 normal local-cookie browser handoff (source only)

`P29_HTTP_SERVE_NORMAL_COOKIE=1` is valid only with the exclusive
`P29_HTTP_VERIFY_PRINCIPAL_RFQ=1` private profile on a new
`proc_p25_http_p29_*` PostgreSQL database. It runs after the genuine Core
principal approval graph, finite-role P29 Draft Create/Read/Cancel/replay,
allocation release, and owner-only constraint checks pass. It does not enable
supplier publication, a purchase order, Finance posting, or payment.

The helper starts the ordinary Program host on one ephemeral HTTP loopback
port, with normal local authentication and the actual scoped P29 sourcing
readiness provider. It verifies that the private subject header cannot
authenticate. The root-owned same-origin HTTPS proxy and browser must use
the normal local login/session cookie. No private subject header, fake role,
or readiness replacement is accepted.

The protected directory `/p25-evidence` must already exist as a real 0700
directory. A new 0600 file
`/p25-evidence/p29-local-cookie-fixture.private.json` has schema version 2:

```json
{
  "schemaVersion": 2,
  "controlPlaneOnly": true,
  "apiUrl": "http://127.0.0.1:<ephemeral-port>",
  "database": "proc_p25_http_p29_<nonce>",
  "tenantId": "p25-private-http",
  "companyId": "<uuid>",
  "branchId": "<uuid>",
  "buyerPrincipalId": "<uuid>",
  "approvedRequestId": "<uuid>",
  "approvedRequestLineId": "<uuid>",
  "cancelledDraftEventId": "<uuid>",
  "syntheticCredentials": [{"principalId":"<uuid>","email":"<synthetic>","password":"<secret>"}],
  "challenge": "<random hex>",
  "completionPath": "/p25-evidence/p29-local-cookie-browser-completion.private.json",
  "expiresAt": "<UTC timestamp>"
}
```

The actual file also includes bounded setup/claim fields; consumers should
reject unexpected schema versions and verify the buyer entry among the seven
newly activated, workerless synthetic accounts. Buyer actions remain
`procurement-access`, `procurement-sourcing-convert`, and
`procurement-sourcing-cancel`. The fixture is secret: never log, commit,
attach, or copy its credentials to browser result artifacts.

The separate protected completion file must be new, 0600, <=4096 bytes,
and contain exactly:

```json
{"schemaVersion":2,"challenge":"<exact fixture challenge>","status":"BrowserFinished"}
```

This only closes the private API lifecycle. Its receipt is explicitly
`CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED`. Browser success requires a
separate real Playwright result and independent owner reload. The browser
proxy should publish a separate protected `P29_COOKIE_PROXY_RECEIPT`, with
`transport: normal-local-cookie-https-v1`, an HTTPS loopback origin, and a
SHA-256 binding to the exact P29 fixture bytes. An old P25 receipt is invalid.

## Independent owner verifier

`verify_p29_cookie_rfq_owner.py` has two strictly read-only modes. Root must
run capture **after** the API helper writes the fixture and **before** the
browser starts:

```text
P29_COOKIE_FIXTURE=<absolute 0600 path> \
  python3 verify_p29_cookie_rfq_owner.py --capture-baseline \
  --expected-database proc_p25_http_p29_<nonce> \
  --pg-container <full private container ID> \
  --output /p25-evidence/p29-owner-before-browser.private.json
```

After browser exit and the challenged completion, set
`P29_COOKIE_OWNER_BASELINE`, `P29_COOKIE_BROWSER_RESULT`, and
`P29_COOKIE_PROXY_RECEIPT` to distinct protected 0600 files in the same 0700
directory, and run the same command without `--capture-baseline` with a new
output path. The verifier requires exact private PostgreSQL 17 cluster/socket,
network-none container, and a repeatable-read read-only SQL transaction. It
compares the original approval graph and first API cancelled RFQ against the
prebrowser hashes. The original PR and line may advance exactly two CAS
versions for the second Create/Cancel; their other business fields must remain
stable. It checks the browser-created v2 RFQ, workerless Buyer, current
principal bindings, two exact observed command keys/receipts, released
allocation and restored quantity. It also requires no supplier invitation,
bid, award, purchase order, AP, or payment fact.

Only case codes, counts and hashes enter the new private result. Credentials,
cookies and complete source records never enter results, stdout or PRs. The
normal-cookie browser and proxy have their own source and execution gates;
this script has not been run by the source author.
