# Private browser, PostgreSQL and SMTP qualification

This opt-in composition extends the existing real Laravel/Kuubu/Core recipient
browser journey. It is test infrastructure, never registered by deployed APIs.
The provider and frozen frontends stay on Windows loopback; PostgreSQL and the
production SMTP adapter run in isolated Linux containers. Remote mode has no
published container ports. Local Docker mode publishes only owned127.0.0.1
ports8134/8135 on a separate test network. There are no external recipients or
production credentials.

The browser creates the company and invitation through actual handlers. A
marker-protected test endpoint processes the existing intent through
`CompanyInvitationDeliveryStore` and `CoreCompanyInvitationMail`. PostgreSQL row
and company locks, original issue receipt/envelope checks and the actual Laravel
license reader remain in that path. A test-only `ICompanyInvitationMail` proxy
uses the owned local Docker loopback ports (or SSH forwards where allowed) to
reach Linux, where `SmtpCompanyInvitationMail` sends
the actual message through STARTTLS/authentication to a private SMTP sink.

The sink decodes received MIME and extracts the invitation URL. The sender
refreshes its actual company list and sees email submitted while joining remains
pending, with inbox delivery explicitly unconfirmed. The browser
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
SMTP message. Use only the owned loopback destinations:

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

Keep certificate/key fixtures on a private Linux volume so Unix permission checks
are meaningful. A runtime-only container may execute the same portable managed
test assembly and managed VSTest runner mounted read-only from Windows; record
their hashes and the actual matching Linux framework image. This is execution on
Linux, not a new Linux compilation. The qualified runner requires.NET10.0.14.
Set `DOTNET_ReadyToRun=0` in this test container when using the Windows SDK's
managed runner; its Windows precompiled code cannot run on Linux. This does not
change application source or certificate validation. Verify the runner startup
before creating the browser fixture.

Record active `NetworkSettings.Ports`, not only requested `HostConfig` bindings.
The local Docker29.6.1 attempt needed an additional owned host-facing bridge
network to activate loopback publishing alongside the internal network. Verify
that every active binding remains127.0.0.1; do not change production networking.

Always stop the owned bridge, any SSH forwards and provider/SSR children; restore
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
