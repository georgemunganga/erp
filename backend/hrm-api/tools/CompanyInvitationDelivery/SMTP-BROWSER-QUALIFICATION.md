# Private browser, PostgreSQL and SMTP qualification

This opt-in composition extends the existing real Laravel/Kuubu/Core recipient
browser journey. It is test infrastructure, never registered by deployed APIs.
The provider and frozen frontends stay on Windows loopback; PostgreSQL and the
production SMTP adapter run in isolated Linux containers. There are no published
container ports, external recipients or production credentials.

The browser creates the company and invitation through actual handlers. A
marker-protected test endpoint processes the existing intent through
`CompanyInvitationDeliveryStore` and `CoreCompanyInvitationMail`. PostgreSQL row
and company locks, original issue receipt/envelope checks and the actual Laravel
license reader remain in that path. A test-only `ICompanyInvitationMail` proxy
crosses an SSH loopback forward to Linux, where `SmtpCompanyInvitationMail` sends
the actual message through STARTTLS/authentication to a private SMTP sink.

The sink decodes received MIME and extracts the invitation URL. The browser
obtains that received URL, opens the invitation, signs in through Kuubu, enrolls
explicitly and confirms the company/role. This mode disables direct decryption
preview. It retains the earlier committed-response-loss/exact-retry and actual
subscription-suspension/replay-denial checks. Membership, billing access and
operational ERP sessions remain separate decisions.

Trust belongs only to the Linux test process via the private CA environment in
[the wire gate](SMTP-WIRE-QUALIFICATION.md). No host trust-store change or
application certificate-validation callback is used. HTTP bridge authorization
uses a unique private fixture marker; neither it nor cookies, message bodies,
invitation links, provider tokens or database credentials may appear in logs.

Run from fresh fixture/build/result directories. The Linux bridge fact requires
`KUUBU_INVITATION_SMTP_BRIDGE_MARKER`, an absolute new
`KUUBU_INVITATION_SMTP_BRIDGE_READY` file, and the wire fixture/trust variables.
It binds port8133 only inside its internal test network, accepts one synthetic
recipient/role, limits body size/concurrency/deadlines, and waits at most20 minutes
for a marker-protected stop. A passing bridge fact requires one actual accepted
SMTP message. Use only the owned loopback SSH forwards:

| Windows input | Destination |
| --- | --- |
| `http://127.0.0.1:8134/` | Owned Linux test bridge port8133 |
| PostgreSQL `127.0.0.1:8135` | Owned synthetic control database port5432 |

The browser fact additionally requires `KUUBU_REAL_BROWSER_INVITATION_SMTP` and
`KUUBU_REAL_BROWSER_INVITATION_POSTGRES` together. It validates those loopback
destinations and the synthetic control database name, creates a new
`company_team_*` database, applies the existing migrations and retains its data.
Select `ProductInvitationBrowserInteropTests` with the separate
`tools/ProductInvitationBrowser/smtp-browser.mjs` runner. Default test runs skip
the bridge and browser opt-ins.

Always stop the owned bridge, SSH forwards and provider/SSR children; restore
the synthetic PostgreSQL container to stopped. Independently inspect the exact
new database read-only, including command hashes, identity/company bindings,
immutable receipts, delivery history and absence of operational grants. Verify
the frozen frontend outputs, Foundation source and independently copied binary
dependencies after use. Retain failed/partial attempts without overwriting them.

This composition qualifies a private SMTP message through the actual delivery
store/resolver and browser acceptance. The Windows transport proxy is explicit;
it does not prove a single deployed production process, production HTTP ingress,
external inbox delivery, sender DNS authentication or secure production SMTP
configuration. Those remain separate release checks.
