# Actual product provider interoperability qualification

This qualifies the existing CORE-PRODUCT-IDENTITY and CORE-COMMERCIAL-LICENSE
contracts. It adds test tooling, not a production URL override or admission API.

The opt-in ProductProviderInteropTests uses the actual ASP.NET OAuth/cookie
handlers and Core licensing client. A fresh Laravel fixture supplies real
Passport clients, RSA keys, code exchange, bearer resources and subscriptions.
The existing published Kuubu account client and signed relay obtain the code
from ASP.NET's original S256 challenge/state. No fake provider JSON or token is
returned by the .NET backchannel adapter.

Expected acceptance: personal enrollment required before authorization; explicit
join; confidential PKCE exchange and immutable verified subject; code replay
denial; token-free pending cookie; no ERP session or tenant grant; real machine
license without staff billing membership; foreign-product rejection; suspension
denies the next license read; bounded lease/proof expiry; independent before/after
provider counts and empty Core principal/link/session tables.

Loopback test transport is intentionally narrower than a deployment gate. It
maps only the pinned provider token/subject/licensing paths to the synthetic
loopback API. Production issuer and callback checks are unchanged; there is no
certificate-validation bypass. This does not qualify public TLS, DNS/proxy logs,
a real browser callback, operational sessions or company/module authorization.

Use a .NET10 SDK, locked foundation dependencies with PHP8.4/SQLite, Python and
Node, plus the exact published Kuubu website source. On the owning workstation:
```text
python backend/hrm-api/tools/ProductProviderInterop/run.py --workspace <new-absolute-exclusive-directory> --foundation <foundation/backend> --website <exact-kuubu-source> --php <php8.4-path>
```
The helper prepares fresh fixtures with mail=array/payments=false, owns PHP and
Node processes, and verifies both local services before recording readiness.
Local ports8101/8120 must be free. Stop the owning helper after the
gate: its finally block stops its direct children and retains logs/data/receipts.
Do not rerun a consumed fixture; generate a new exclusive workspace.

Set KUUBU_REAL_PROVIDER_FIXTURE to the private absolute path of the synthetic
provider/core-interop.json manifest on the same host. Never print or commit it;
it contains only fresh synthetic client
credentials, not production credentials. The bridge verifies the same fixture
marker. Run the single opt-in ProductProviderInteropTests case with both local
fixtures reachable. It is explicitly skipped when no fixture is set.
Normal existing tests and the production API remain independent of this fixture.

Retain exact source/website hashes, native SDK, logs, counts, job status and
cleanup evidence. Company/payer binding and setup authority, invitations,
protected operational grants/refresh/revocation, public SaaS local-auth policy,
runtime ACLs, UI and company/branch/module enforcement remain required after a
successful protocol gate. No public launch follows automatically.

## Actual protocol result

Exact implementation `3f5cd44bb280dd3770a9c4091f9b6289e9a9cc1b`, provider
source `8e96bef7a18896a3ba3dad6d7031f58a9059aec3`, Kuubu website source
`d3452e6c6ad652206169a7f298222197692e5edf`. The three imported website blobs
(relay, client and authorization validation) were independently checked against
their published Git blob hashes before execution; no candidate source was edited.

The actual opt-in case passed, zero failures/skips, in13 seconds, on Windows
.NET SDK10.0.403, PHP8.4.12 and Node24.18.0. It used the genuine Laravel Passport
code/S256/confidential exchange, RSA bearer subject resource and machine license
resource through the real Kuubu signed relay/client and ASP.NET handlers. Before
enrollment authorization was403; explicit join was201; subsequent authorization
was302. Code replay was rejected. The pending cookie had no provider tokens,
tenant, email or role claims and could not satisfy an ERP policy. No Core user,
identity link or local operational session was created.

The machine channel authorized hrm, denied payroll and a foreign product's
enrollment, then denied a fresh read immediately after the synthetic subscription
was suspended. Its single cached machine token was distinct from human identity
credentials. The lease and identity cookie both expired under the controlled
clock advance. A read-only provider audit confirmed the seeded two identities,
one customer, two payer enrollments and one subscription were unchanged by
authentication, with zero billing memberships and exactly one explicit personal
enrollment. Fixtures/processes were stopped and local ports8101/8120 closed.

Local retained evidence:
`inspection/qualifications/provider-interop-3f5cd44b-03/acceptance-receipt.json`,
with source archive SHA256
`120f26ce0ebbf2fcf481e72afccbaa03600ee8d36d3f5743180e757d7f4d360d`.
Windows focused122 checks also passed, zero failures/skips. Log SHA256:
- focused: `3642489d18600608f05402068d53400610604bbfc033e0621409358da07732d6`
- actual protocol: `ca5eead7478e47dafcbc65c83923d2437fdfbe11bbfa41da29c91096bd5911e9`

## Contract correction and retained earlier attempts

Real execution found an error hidden by the original synthetic license fixtures:
the locked Laravel HasUlids implementation emits lowercase keys, while Core
accepted uppercase only. Core now accepts the exact lowercase provider key and
continues rejecting uppercase/transformed references, trailing newlines and
path injection before network traffic. The original malformed-ID assertions
remain; valid synthetic IDs now match the real provider. No existing persisted
key was rewritten and no permission check was relaxed.

The first local attempt failed at relay authorization before enrollment; its
generic diagnostics did not establish a precise cause. Sanitized stage/status/
latency diagnostics were added, never logging state/code/tokens/request bodies.
The next attempt passed actual identity exchange/replay/cookie isolation and
then exposed the uppercase assumption before any license network request. Both
failed runs and their fixtures remain retained. A fresh fourth fixture and
exclusive third checkout passed after the correction. No historical source,
publication or evidence was patched; compiled caches were independent plain copies.

The initial SSH-forwarding fixture could not start its requested forwarding.
The existing key's restrict option was confirmed; no SSH policy was changed.
Qualification moved entirely to loopback on the workstation. A portable SDK was
downloaded from Microsoft's10.0 release metadata and its SHA512 verified before
execution. Its input receipt is under inspection/toolchains. NuGet timeouts
recovered through normal retries; three separately copied/cache-hash-verified
package archives were prepared as standby but not used by the successful restore.
Later invocations disable development-certificate generation; no certificate
trust or production TLS setting was changed.

## Linux runtime regression

The same exact source3f5cd44b passed122 focused checks and the complete existing
2,093-case HRM suite, zero failures, in7m21s. The newly added opt-in protocol case
is the only skip in that full job (2,094 total); it ran independently above and
passed. No original regression case was skipped or removed. Valid license
fixtures were corrected to match Laravel's actual key format; their malformed-ID
and permission/expiry assertions were retained.

Retained `/opt/kuubu-builds/provider-regression-3f5cd44b-01`, launcher
`kuubu-provider-regression01.service`, SDK container
`kuubu-provider-regression-gate01` exited0/OOMKilled=false. SDK10.0.403 pinned
digest `sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e`.
That job had networking disabled and no production database connection. The
previous28 PostgreSQL cases qualify the unchanged schema/tenant-session portion;
this ID-format correction adds no migration and claims no new PostgreSQL run.

Linux log SHA256:
- focused: `3b87557928718233ae626aabb1cd76e073e8fa19e49ba01ab215b2a1b0f0c88c`
- full: `a0743e9168fbbae27369c31ce3f0c544abb3a228ad12fe86442870e8a1d1e740`

No production signing/client credential, flag, image/schema or ingress was
changed. Private ERP readiness was200 on the previous image3ff07382. This is
actual protocol qualification, not deployed browser-to-ERP operational admission.
