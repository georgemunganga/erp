# Protected operational identity credentials

This credential layer supports Kuubu's separately flagged operational identity
handoff. `ERP:OperationalIdentity:Enabled` defaults false. Its separate OAuth and
cookie schemes never authenticate an ERP request. A verified person observation still needs separate Core company,
license, module/action and branch decisions before operational admission.

`ProductOperationalCredentialStore` requires separate existing private state and
Data Protection directories, neither nested inside the other. Use0700 directories
and0600 files on Linux, with deployment-owned access/ACLs on Windows. State must
be outside application/public assets. Reuse the reviewed deployment key ring
and application name; the credential purpose also binds the exact confidential
client ID. Never use a replacement key ring to recover an existing session.

Issue requires a current, separately scoped observation. Only an opaque random
handle and browser binding may leave this layer. The file name hashes the handle;
its encrypted packet binds handle/binding hashes, client, subject, token pair,
access expiry, revision and an immutable eight-hour absolute deadline. Raw
provider credentials never belong in cookies, URLs, audit events or logs.
Token-pair/packet string representations redact contents.

Every observation reads current protected state and calls the current-person
resource; there is no successful-proof cache or failure fallback. Access tokens
within30 seconds of expiry are refreshed through the fixed HTTPS token endpoint.
Refresh accepts only bounded ordinary Bearer token responses and rotating refresh
credentials. Scope mixing, ambiguous JSON, reused refresh tokens, redirects,
oversized responses and time anomalies fail closed. The eventual production
HTTP registration must disable redirects/cookies and request logging; no
automatic retries may resubmit rotating credentials.

A brief directory gate coordinates admission, per-handle lock acquisition and
cleanup. Rotation is serialized with an exclusive persistent per-handle lock.
Ordinary person-resource calls release that lock while awaiting the provider;
the store rechecks state, the rotation fence and revocation before returning a
proof. Concurrent refresh attempts return busy rather than sending twice.

Before refresh leaves the process, a16-byte journal records the current revision
and its bitwise complement using write-through plus disk flush. It is never
reset to permit retry. A successful encrypted publication advances the revision;
the journal must match exactly the previous revision. Lost acknowledgement,
cancellation/crash or invalid rotation leaves a fenced state requiring sign-in.
Restoring only an older encrypted packet cannot restore a consumed refresh token.
Do not clear a fence, edit a revision or retry an old token manually.

Sign-out/definitive person denial writes a persistent revocation marker. An
in-flight rotation cannot remove it or return a verified observation afterward.
An outage returns unavailable without a cached observation. Returned proofs keep
their original monotonic origin and are further bounded by access/absolute expiry.
Refreshing never extends the eight-hour absolute deadline. Once token validity
is already capped by that deadline, the store avoids unnecessary final rotations.

Admission capacity defaults to512 states (supported1–2048). Under the directory
gate, one verified person/client can hold at most four active credentials and
receive at most six credentials per ten minutes. Revocation frees an active
slot but does not reset recent issuance. The encrypted packets provide durable
admission history shared across workers; malformed history fails closed.
Directory scans take
at most16,385 entries and fail closed on overflow. Expired states become eligible
for cleanup after15 minutes; cleanup skips held locks. It removes expired state,
journal/tombstone/temporary publications and the lock inode under the directory
gate before reclaiming capacity. Invalid metadata/permissions need operator
investigation. Do not delete active state/lock files to free capacity.

The filesystem contract is for workers sharing a qualified local/persistent
filesystem and key ring. The two-process test does not qualify NFS/SMB, multiple
hosts or a distributed credential backend. Do not remove serialization to scale;
qualify the chosen shared storage or introduce an owned transactional adapter.
Durability relies on the underlying filesystem honoring write-through/flush.

The default-off web handoff adds private browser binding/challenge and CSRF,
independent operational client/callback/cookie, anonymous request budgets and
owned five-minute expiry cleanup. Configure a canonical, separate
`ERP:OperationalIdentity:ClientId`, private `ClientSecret` and existing private
`StatePath` outside application assets; retain the reviewed product key ring.
Do not enable it before connected branded browser/relay qualification,
company/module/branch authorization, capacity monitoring/alerts, recovery UX for ambiguous refresh and absolute/idle
session policy. The interim callback returns identity status, pending the real
company/module selection screen. Company/module grant persistence, revocable
ERP sessions, route/branch isolation and browser acceptance remain separate work.
Classic local authentication and dedicated/on-premise behavior are unchanged.

Run `ProductOperationalSubjectTests`, `ProductOperationalRefreshTests` and
`ProductOperationalCredentialTests`. Linux cross-process qualification uses only
an explicitly owned private directory with scope marker
`kuubu-operational-credentials-process-v1`; two separate test processes run
`OperationalCredentialProcessTests` as holder/observer. The observer must skip
the locked rotation and revoke it; the holder must deny without resurrecting
credentials. This is test infrastructure and never a production command mode.

Actual provider acceptance: `ProductOperationalProviderInteropTests` requires
Linux and an explicitly prepared private fixture at
`KUUBU_OPERATIONAL_PROVIDER_FIXTURE=/evidence/provider/operational-interop.json`.
It uses the production fixed HTTPS clients, a private CA and real Passport/SQLite,
with fixture-only20-second access tokens to force refresh. It checks explicit
personal enrollment, callback, rotating refresh, fresh-person denial after
suspension, tombstones and zero ERP admission. It is not branded-browser,
production-ingress or native ERP database qualification. Never point the harness
at production or bypass TLS validation. Latest evidence is
`inspection/qualifications/operational-wire-native-06`; earlier failures remain
retained. Preserve the existing key ring; do not edit state to recover tokens.
