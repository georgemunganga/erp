# P33 private native HTTP proof hook

This source-only probe extends the existing P30 principal PO cohort when
`P33_HTTP_VERIFY_PRINCIPAL_PO_EDIT=1` and `P30_HTTP_VERIFY_PRINCIPAL_PO=1`.
The database must be a fresh `proc_p33_http_po_*` private PostgreSQL database.
The flags are exclusive of receiving, invoice, RFQ and normal-cookie phases.
The P30 profile, database prefix and owner result remain unchanged when the
P33 flag is absent.

The probe first runs the accepted genuine P25 approval and P30 direct PO
graph. It then enables the distinct P33 option and grants the execution role
only SELECT and INSERT on the three new immutable P33 tables. Normal Program
composition and the captured physical readiness provider must report the
P33 capability. A new workerless Core buyer uses the remaining approved PR
quantity to create an unfunded PO, revises its exact effective quantity
downward, replays the same Edit key, recovers the original result through GET,
and reloads the owning rows. It checks current role and branch denial, upward
amount and stale-version denial, and refusal to revise an issued PO. Finally,
it cancels the revised draft and checks exact reservation release with
immutable revision history retained. No Finance posting, supplier send or
employee fixture is created.

This is an authored tool contract. Compilation, private PostgreSQL migration,
finite privilege proof, actual HTTP execution and independent owner acceptance
remain unrun. A passing native probe would establish only the bounded internal
unfunded Draft edit flow; P33 UI and normal-cookie browser acceptance are
separate gates.

## Separate normal-cookie Draft edit cohort (source-only)

`P33_HTTP_SERVE_NORMAL_COOKIE=1` is a second explicit opt-in layered on the
P33 and principal-PO flags. It requires a **fresh**
`proc_p33_http_po_cookie_*` private database and leaves the original native
P33 profile and P30 cookie profile unchanged. After the native P30 graph and
P33 Edit/replay/recovery/cancel graph pass, it checks four native orders and
one remaining approved direct-source unit, activates only the ten new
synthetic Core accounts' placeholder passwords, and exports a protected
schema-4 `p33-po-local-cookie-fixture.private.json`. The normal local-cookie
host must advertise genuine captured P33 readiness and rejects private
subject-header authentication. Browser completion only ends the bounded
15-minute host lifecycle.

The paired imported-UI spec and SHA-bound HTTPS proxy are on the isolated UI
branch `codex/p33-po-normal-cookie-ui`. The buyer creates a fifth order from
that real approved source, edits the unfunded Draft from one to half a unit,
reloads its revision history, cancels the revised Draft and logs out. It
captures the three actual command bodies and keys in a protected result.
`verify_p33_po_cookie_owner.py` captures the four-order baseline before the
browser, then read-only verifies the exact fifth order, immutable original
and effective P33 graph, all three command receipts and fingerprints, source
conservation, shared audit/outbox, unchanged older records and revoked real
local sessions. No browser result or completion file certifies owner facts.
All code in this section is authored source and **unrun** until a separately
reviewed probe-only build, fresh private SQL cohort, browser and owner check.
