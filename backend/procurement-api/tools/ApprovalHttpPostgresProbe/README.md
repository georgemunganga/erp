# Procurement approval HTTP/PostgreSQL verification

This is a disposable verification program, not a runtime service. It requires a fresh database
named `proc_approval_http_<unique>`; other database names are rejected. Never use production.
Set `PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION` privately and run:

`dotnet run --project backend/procurement-api/tools/ApprovalHttpPostgresProbe`

It creates isolated Core/HRM, Procurement and shared approval model tables, seeds synthetic
canonical accounts and an explicit server-side **fixture** manual Finance policy, then exercises
real mapped HTTP routes and PostgreSQL transactions. Authentication is a fixture scheme;
authorization, current account grants, organizational scope, financial authority, policy,
source pinning, approval execution, audit/outbox, and persistence are the implementation.
No business API or database is mocked.

Coverage includes a sequential `One` then parallel `All` route; assigned queue/detail/history;
source/line effects; exact command-response replay; changed-command and revoked-authority
denial; changed Finance allowing return while preventing approval; manual full release and
resubmission; receipt-write failure after earlier context saves proving whole transaction
rollback; concurrent votes with explicit version conflicts and recovery; final approval racing
a Finance change. The synthetic receipt-failure trigger is installed only inside the guarded
disposable database and removed before retry.

This program rehearses the mapped model. Migration idempotency, runtime database grants,
production configuration, notification dispatch and browser verification remain separate gates.
No email, payment, reservation API call or production write is performed. A manual Finance
attestation is never represented as an automated external confirmation.

The probe explicitly registers its disposable connection as
`ConnectionStrings:ProcurementApprovalExecution`. Production activation must instead use a
verified dedicated execution database role; the probe's owner-level schema setup cannot
establish that production permission gate.
