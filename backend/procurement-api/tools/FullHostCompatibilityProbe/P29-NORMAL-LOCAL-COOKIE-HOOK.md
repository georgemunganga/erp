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
