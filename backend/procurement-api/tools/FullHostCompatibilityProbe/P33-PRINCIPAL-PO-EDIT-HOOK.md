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
