# P25 normal local-cookie/Kestrel prerequisite — source only

Base `bb932c5`. No build, browser, DB or runtime acceptance is claimed. Three
source files: optional factory mode, new partial and this root-owned hook note.
No application auth/schema/provider/default registration/Node proxy/UI changes.
The ordinary factory still uses its explicitly private subject authentication.

## Future root-owned opt-in / source binding

Introduce optional `P25_HTTP_NORMAL_LOCAL_COOKIE_SERVE=1`; reject any other
present value before bootstrap; absent leaves existing modes/counts unchanged.
Requires the positive/manual base43 proof (including original negative and
approved graph,12GET/6replay) and ONE active real independently reviewed grant.
Do not combine with retained-alias or final authority-revoke modes: the intended
normal browser journey starts from exactly TWO existing PRs and no new graph.
Repeated matrix/raw phases are unnecessary once independently accepted.

After `VerifyPrivateManualApprovalAsync` returns successfully, before `RunAsync`
leaves its existing try/owner-connection lifetime, root owns this optional call:

```csharp
await ServeNormalLocalCookieAsync(fixture, owner, roles,
    ownerConnection, result, Advance);
```

Here `owner` is the already validated private owner CONNECTION STRING, never a
manifest value or stdout. `ownerConnection` is its open Npgsql session. Do not
call on any accepted/production DB; launch only in a NEW exact private fixture
with actual successful source/publication pins and normal before/finally guards.
This commit does not edit shared flag parsing, calls, counters or final PASS.

## Factory and authentication boundary

`PrincipalApprovalHost(..., normalLocalAuthentication:true)` omits BOTH the
private subject service AND scheme registration. Default false preserves all
existing private test-auth callers. OfflineHost settings already select local
AuthMode and blank local-admin bootstrap. Normal Program registers
LocalAuthenticationHandler, routes, current Core refresh/action scope and finite
Procurement Record/Execution contexts; this is not a fake handler/provider.
Background application workers stay removed/unsupported workers fail closed,
external CoreDocuments/operational services remain disabled as existing settings.
No outbox publisher process/host is started.

The new helper verifies actual default authenticate/challenge scheme=local,
actual handler type name and absence of the private scheme/service. It then
calls installed .NET10 WebApplicationFactory.UseKestrel(callback) BEFORE
StartServer, with only `Listen(IPAddress.Loopback,0)`. Exact returned address must
be one HTTP127.0.0.1 endpoint with an allocated port. No host/public listener,
Unix socket changes or Docker port publishing. Keep original runtime networknone.
An actual network HttpClient request supplies the old private subject header
without cookies and must get401. This is ONE separately counted control-plane
HTTP call, not a completed local-cookie login/browser journey.

## Credentials and private manifest

Only the original SIX synthetic workerless accounts can be seeded; each must
have original placeholder PasswordHash, active/unarchived/unlocked state, no
failure count/required password change. Each gets independent random>=12char
password,16byte salt and32byte PBKDF2SHA256 key/210000iterations, exactly the
owned LocalPasswordHash stored format. That class is internal; no production
hash API is exposed or replaced. Later REAL /api/hrm/auth/login must validate it.
Only PasswordHash changes by scoped original-value CAS. No real account/role,
auth claims, employee link or external credential is involved. Partial seed
failure retains fixture; no blind retry/credential overwrite.

Fixed new evidence mount must be0700 `/p25-evidence`. New CreateNew0600 file:
`/p25-evidence/local-cookie-fixture.private.json`. It contains API loopback URL,
validated private DB name, synthetic tenant/company/branch/principal IDs,
`syntheticCredentials` array (`principalId`, `email`, `password`), random
challenge and expiry/completion path. It contains NO connection strings/role
passwords. This file IS SENSITIVE; never copy to public results, browser logs,
storageState/HAR/traces/shared screenshots, user messages or PR attachments.
Public tool result has counts/controlled flags only, no credentials or cookies.
Password seeding and later login/session writes are DECLARED fixture/auth setup,
not a claim that the original workerless acceptance footprint stayed unchanged.

## Browser transport and bounded lifecycle — not implemented here

Normal login issues Secure/HttpOnly/SameSiteLax/path=/ cookie. Root must provide
reviewed same-origin HTTPS frontend/API proxy in the SAME networknone namespace
(e.g. exact owned runtimePID `nsenter -n`, host filesystem retained). The proxy
must preserve real Set-Cookie/Cookie/API bytes and inject NO subject/action/role
headers. Private self-signed TLS trust remains a fixture limitation, not public
TLS/IdP proof. Node TLS/proxy/Playwright are separate future source work.

The helper polls for at most15minutes, observes cancellation and disposes the
factory/Kestrel on return/timeout/exception. Root's completion writer must use
atomic replacement from its own new0600 temporary file at fixed
`/p25-evidence/local-cookie-browser-completion.private.json`, exact bounded JSON:

```json
{"schemaVersion":1,"challenge":"EXACT_PRIVATE_MANIFEST_CHALLENGE","status":"BrowserFinished"}
```

Only three unique fields,<=4096bytes, same challenge,0600/no symlink and unexpired
are accepted. This is ONLY a lifecycle acknowledgement so the API may shut down.
It does NOT prove Playwright exited0, expected actions occurred, correct UI
bundle was served or database receipts/source facts passed independent reload.
The helper result remains `CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED`;
`localCookieLoginVerified`, `browserAcceptanceVerified`,
`ownerCompletionVerified`, `nativeIdentityProviderVerified`,
`externalFinanceAuthority` all remain FALSE. Never promote base43 test-auth PASS
into normal-auth browser acceptance based on file presence. Root must bind
separate exact PW exit/results/bundle and independent owner proof before any
new acceptance status. No file on timeout means fail/retain, not automatic pass.

Normal-auth follow-up: real wrong-password401 on dedicated synthetic account;
real six-user login/session bindings; context workerId=null/current customgrants;
independent cookie contexts for actual configured authority/PR/checker/Open/
Decide/GET; ungranted and revoked session/action/scope denials; exact receipts/
source/outcomes via owner. Auth login/logout/sessionLastSeen create legitimate
writes. Baseline commands AFTER login, keep per-actor session age<5minutes or
explicitly establish a new auth baseline; never normalize those writes away.
External OIDC/MFA, real IdP, ERP Finance/AP/payment owners remain unavailable.
