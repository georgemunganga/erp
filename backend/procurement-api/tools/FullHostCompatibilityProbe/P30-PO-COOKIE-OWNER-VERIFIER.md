# P30 private PO browser owner verifier

`verify_p30_po_cookie_owner.py` is a read-only, two-stage check for the P30
normal local cookie PO browser journey. It accepts only a fresh synthetic
`proc_p30_http_po_*` database on PostgreSQL 17 system
`7698678713374847009`, reached through the private Unix socket in a Docker
container with no network. It cannot target production or an arbitrary DB.

First capture the owning baseline after the fixture and proxy receipt exist,
and **before** Playwright creates the draft. Use a new file in the private 0700
evidence directory:

```text
python3 verify_p30_po_cookie_owner.py capture \
  --fixture "$P30_PO_COOKIE_FIXTURE" \
  --proxy-receipt "$P30_PO_COOKIE_PROXY_RECEIPT" \
  --baseline "$P30_PO_OWNER_BASELINE" \
  --container "$P30_PO_OWNER_CONTAINER" \
  --database "$P30_PO_COOKIE_EXPECTED_DATABASE"
```

After the real browser run, completion, and explicit revocation of its local
session, use another new 0600 output path:

```text
python3 verify_p30_po_cookie_owner.py verify \
  --fixture "$P30_PO_COOKIE_FIXTURE" \
  --proxy-receipt "$P30_PO_COOKIE_PROXY_RECEIPT" \
  --baseline "$P30_PO_OWNER_BASELINE" \
  --browser-result "$P30_PO_COOKIE_BROWSER_RESULT" \
  --output "$P30_PO_OWNER_RESULT" \
  --container "$P30_PO_OWNER_CONTAINER" \
  --database "$P30_PO_COOKIE_EXPECTED_DATABASE"
```

The verifier hashes every Procurement and Core Approval business row and the
Core identity account, branch assignment, role assignment, location and
legal-entity rows. Only the operational account login timestamp, failed-login
count, lock timestamp, update timestamp and version are excluded from the
account hash; role/scope and credential fields remain pinned. No PII or
credential values are written to the evidence file. It compares the
before/after sets and permits only the browser-created internal
PO, one PO line, one immutable source binding, its released reservation,
Create/Cancel receipts, linked audit and outbox rows, and the source request
and line version changes. It requires the three native orders and other
business rows to remain unchanged, the approved line allocation to return
to one, exact receipt response hashes and command keys, and the buyer's new
local sessions to be revoked. Authentication sessions are checked separately
and token hashes are never placed in the evidence file.

The verifier does not execute business commands, send supplier messages,
post to Finance, or claim that a challenged completion file proves the UI.
It refuses to overwrite any retained result. Failed evidence remains in the
private directory for investigation. The updated browser result retains only
the two PO command request bodies. The verifier hydrates their typed DTOs,
reconstructs the backend v2 canonical Create and Cancel fingerprints with
exact decimal values, and compares them to persisted command SHA values. It
fails closed on strings outside the reviewed simple ASCII subset rather than
guessing System.Text.Json escaping.
