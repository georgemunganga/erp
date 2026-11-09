# Private operator executable qualification

`InvitationOperationsExecutableTests` is an opt-in Linux composition. It launches
the unmodified API executable as a child with the actual operations arguments,
production registration, licensing client, protected invitation reader and SMTP
adapter. No application HTTP/SMTP client is replaced. It does not run in normal
tests or register a production endpoint.

The Linux runtime must include the existing ERP Dockerfile's
`libgssapi-krb5-2` dependency. The bare ASP.NET base can write a missing-library
diagnostic during Npgsql's ordinary GSS negotiation before password fallback.
The executable gate requires empty stderr; supply the runtime prerequisite
rather than suppressing diagnostics or changing connection negotiation.

Prepare a fresh internal Docker network, synthetic PostgreSQL control database
`kuubu_session_test_control`, private Laravel product fixture and named Linux
evidence volume. Do not publish host ports. The test assembly and previously
qualified API binaries must be independent/read-only inputs, with manifests
checked after use. Keep every failed/partial attempt.

The provider is the existing Laravel application with its actual Passport
machine client, subscription and licensing handlers. A fresh synthetic payer
and owner are explicitly seeded. Initial company setup uses a synthetic
short-lived owner observation and actual license through the Core provisioner;
this test does not replace or requalify the separate OAuth/browser setup flow.
Provider data, Passport keys, sessions and caches stay in its private fixture.
Payments use no credentials and mail is array-only on Laravel.

Stage application/vendor files on a fresh Linux volume from a retained archive.
Thousands of Windows bind-mounted PHP includes can exceed the unchanged
licensing deadline. Source/application mounts remain read-only, and the archive
must be fully closed before another fixture reads it. Record the provider PHP
version, runtime image/package versions and archive hash with the evidence.

An in-process test TLS gateway binds only container loopback443 for the fixed
`commercial-api.greenwebb.tech` origin. It forwards only token/licensing paths
to the private Laravel HTTP listener on the internal network. The executable's
ordinary HTTPS client validates the private DNS certificate using process-only
CA trust. This gateway is explicit test transport, not deployed ingress or a
production certificate configuration. The same private CA signs the isolated
STARTTLS sink certificate. Do not install it in the host trust store or add
certificate-validation callbacks to application code.

Inputs include `KUUBU_OPERATIONS_EXECUTABLE_FIXTURE` at the exact private
`/evidence/provider/core-interop.json` path, the existing private PostgreSQL/wire
fixture variables, the mounted API at `/api`, and distinct0700 key/intent
directories. The test generates the original invitation with the deployment's
Data Protection application/purpose. Operations must read that key ring without
changing its contents or generating a replacement.

Two actual SMTP recipient rejections create retryable failures through the
normal worker/store/resolver. The test waits the normal backoff before invoking
the executable. The first operator retry must send through real STARTTLS/auth,
commit acceptance and its operator receipt, then replay without another send.
Changed arguments for the same key must conflict. A test-only, marker-protected
provider control suspends the actual Laravel subscription before the second
retry; that command must record suppression without contacting SMTP. This
control exists only in the isolated fixture router.

Retain only sanitized aggregate observations: command statuses, attempt/receipt
counts, gateway request counts and unchanged key-ring evidence. Never output
client secrets, provider tokens, database passwords, raw MIME, invitation links
or key material. Independently inspect the retained PostgreSQL database and
Laravel SQLite copy, verify no credentials/ERP sessions/module grants, and stop
every owned service. This composition does not prove external inbox delivery,
production monitoring/support ownership, deployed TLS/ingress, background-worker
supervision or a customer installation's on-premise connectivity policy.
