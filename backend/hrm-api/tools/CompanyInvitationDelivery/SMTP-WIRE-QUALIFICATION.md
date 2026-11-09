# Private SMTP/TLS wire gate

`SmtpCompanyInvitationWireTests` uses the production `SmtpCompanyInvitationMail`
adapter without its send override. A test-only loopback SMTP sink supports EHLO,
STARTTLS, TLS1.2/1.3, AUTH LOGIN, envelope/DATA/acknowledgement and rejection modes.
No external recipients, production credentials, host ports or host certificate
store modifications are needed. Captured bodies stay in memory; no protocol
trace, authentication data, raw email or invitation link is written to logs.

Use a new isolated Linux test container and private evidence directory. Run
`prepare-smtp-wire-fixture.sh` with a new absolute directory to generate a one-day
test CA, trusted server, wrong-host server and untrusted server certificates.
It refuses an existing directory; keys/PFX are0600 and the fixture is0700.
Map `smtp.fixture.test` to127.0.0.1 inside that test container only. The wrong-host
certificate deliberately retains the correct Common Name but has a different DNS
SAN. Independent OpenSSL checks verify chain/name geometry before the tests.
Keep all outputs private and retain failed attempts. Empty PFX passwords apply
only to these short-lived private test certificates.

For the wire test process only, set these absolute paths:

| Variable | Value |
| --- | --- |
| `KUUBU_INVITATION_SMTP_WIRE_FIXTURE` | Newly created fixture directory |
| `SSL_CERT_FILE` | Fixture `root.pem` |
| `SSL_CERT_DIR` | Fixture empty `trust` directory |
| `KUUBU_SESSION_TEST_POSTGRES` | Existing explicitly prepared private test control database |

Run VSTest with filter `FullyQualifiedName~SmtpCompanyInvitationWireTests` in a
fresh result directory. The six transport cases require Linux/process trust;
the three store-to-wire cases also require the private PostgreSQL fixture. With
no opt-in environment, all nine skip. The normal compatibility run must omit
the fixture/trust/database environment again.

Tests cover trusted TLS/authentication and actual MIME/link/Message-ID transfer,
missing STARTTLS, untrusted certificates, host mismatch, rejected recipient,
cancellation awaiting acknowledgement, actual PostgreSQL worker acceptance and
lost acknowledgement followed by stable-message retry, and cancellation rollback
with independently acquired row/company locks. Row outcomes/audits and
absence of membership/session/module effects require independent SQL inspection.
Capture/verdict errors must not echo credentials, message bodies or links.

The fixture does not install an application certificate-validation callback.
Kuubu's adapter accepts DNS hostnames only. The initial IP fixture exposed the
[runtime's IP Common Name fallback](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/native/libs/System.Security.Cryptography.Native/openssl.c);
its failed result remains retained. The conflicting-DNS-SAN case must still fail
authentication with no credentials or email transferred. Cancellation checks
require the send task to terminate; a test timeout cannot count as a rejection.
The pinned SMTP client performs a synchronous final acknowledgement read. The
adapter explicitly aborts its active connection on cancellation so that the
awaited send terminates and releases the worker's database/authority locks.
Linux process trust uses the documented [OpenSSL certificate-store environment](https://github.com/dotnet/docs/blob/main/docs/standard/security/cross-platform-cryptography.md).
The adapter uses [SmtpClient STARTTLS](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.enablessl?view=net-10.0).
These tests do not qualify a production SMTP account, DNS sender authentication,
inbox delivery, browser-to-email journey or live activation.
