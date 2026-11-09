# Core invitation delivery

Core owns the invitation and ID-only delivery intent. Its dedicated worker owns
delivery attempts/outcomes in the existing outbox; it creates no identity,
enrollment, billing membership, company access or ERP session. There is no new
queue or schema. HRM mail settings, generic publication and HRM notification retry
do not own invitation delivery.

Delivery defaults off. Only API mode starts the worker. Migration-owner and
generic publisher modes neither register SMTP nor start it. Required flags:

- `ERP:ProductIdentity:Enabled`
- `ERP:ProductLicensing:Enabled`
- `ERP:CompanyTeam:Enabled`
- `ERP:CompanyInvitations:Enabled`
- `ERP:CompanyInvitationDelivery:Enabled`

The first four still need their existing reviewed client/database/key-ring
configuration. Delivery uses its own `ERP:CompanyInvitationDelivery:Smtp:` settings:
`Host`, `Port` (default587), `From`, and paired `Username`/`Password` if required.
Use a verified Kuubu sender and securely supplied credentials. The adapter uses
STARTTLS and ordinary certificate validation; it does not support an environment
switch to plaintext or disabled certificate checks. Implicit-TLS-only SMTP is
not the same protocol as STARTTLS. No settings are exposed to the browser.

Before enabling, upgrade/pause every old generic publisher: the current publisher
excludes delivery events and refuses their completion, but old binaries lack that
protection. Qualify the actual SMTP/TLS endpoint, sender authentication,
deliverability, monitoring, recovery and owned operator retry. The sender-facing
delivery status UI remains separate work; creation must not be labelled "sent".

Each worker selects one due delivery row using PostgreSQL `FOR UPDATE SKIP LOCKED`.
The row stays locked through outcome commit. Fresh license, exact company/client
and original inviter authority are checked; a company advisory lock protects the
final reread and bounded send from local governance changes. Envelope/target/token
hashes and the original issue receipt/correlation must match. No address or link
is copied into outbox/audit. Fixed error codes replace SMTP exception strings.

`delivery-accepted` means SMTP returned acceptance, not inbox receipt or company
membership. Suppressed/invalid/exhausted are distinct terminal outcomes. Transient
failure retries the same row/link/Message-ID with exponential backoff, at most
eight attempts. A crash after SMTP acceptance but before database commit can cause
duplicate email; SMTP Message-ID is not an exactly-once guarantee. Invitation
acceptance remains a separately idempotent, freshly authorized company command.

Run `CoreCompanyInvitationMailTests`, `CoreOutboxPublisherTenantTests` and
`M26NotificationOutboxTests` for delivery/notification compatibility. Native facts
require the existing explicitly prepared private PostgreSQL fixture via
`KUUBU_SESSION_TEST_POSTGRES`, control database `kuubu_session_test_control`.
Tests create isolated `company_team_*` databases. Use new build/result/fixture
directories and retain failed attempts. The current capture transport tests do
not prove production SMTP/TLS, inbox delivery or live activation.
