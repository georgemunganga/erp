# Durable product ERP session registry

## 1. Identification
Tier3 Core child feature,9 November2026. Source implementation; not activated.
## 2. Purpose
Persist explicitly selected ERP sessions with recoverable exact retries, durable
revocation, bounded expiry and current permission checks, preserving local login.
## 3. Scope
Immutable issuance/location records, append-only revocations, encrypted token
recovery, actor serialization, current scope resolution and atomic audit/outbox.
No HTTP launch endpoint, cookie writer, authentication adapter or route activation.
## 4. Parent
Licensed product admission and session-scope authority. Core owns sessions/grants;
Laravel owns commercial identity/enrollment/subscriptions. One ERP codebase serves
shared SaaS and dedicated/on-premise modes; existing Perfex remains operational.
## 5. Actors
Verified explicitly enrolled person, authorized company/module member, Core
session issuer/authenticator and current identity/licensing providers.
## 6. Entry
Future same-origin JSON/CSRF launch must construct trusted credential handle,
browser binding and parent absolute deadline from server-protected operational
state. They are never browser selectors. Client selects company/link/module,
reviewed grant version/revision, explicit branches and a unique command key.
## 7. Preconditions
Fresh operational-person observation and matching current license; exact active
Core link/member/principal/company; latest explicit grant and owned branches.
Setup or ordinary login alone cannot issue a session. No email/domain inference.
## 8. Workflow
Select current authority, resolve tenant, acquire subject/client and company
locks, revalidate selection, replay exact active command or issue a new opaque
random token. Replace the previous session for the same credential/browser,
including across company tenants, in the same database transaction. Save session,
locations, issue/revoke audit and ID-only events; recheck current scope before
commit. Every resolution restores the stored grant/scope exactly, checks token/
browser/credential/person/expiry/revocation, then current Core/license authority.
## 9. States
Issued/current or denied by changed authority; terminal revoked/replaced; absolute
expired. Unavailable never means authorized. Issuance evidence is immutable;
revocation is a separate immutable record. No historical replay reactivates it.
## 10. Permissions
Saving permissions does not issue a session. Issuance requires a separate explicit
selection. A token alone cannot resolve without the matching server-held
operational credential/browser binding and current person/license. Company roles,
legacy roles and broad headers do not supply module or branch authorization.
## 11. Rules
Same command recovers the exact token/ID/deadline only while current and active.
Changed intent/binding conflicts. Revoked/replaced/expired commands cannot revive
access or extend expiry. Lifetime is at most8hours and capped by parent credential
expiry. Default30 recent issues per subject/client per10minutes is durable across
instances; retries do not consume another slot, revocations do not erase history.
The reviewed activation configuration must select the bounded operational budget.
## 12. Data
CoreProductSession binds issuer/subject/client, tenant/company/principal/link/
membership/module/grant version/revision, scope digest, token/credential/browser
hashes, protected recovery token, command/request hashes and exact UTC deadlines.
Locations and revocations have scoped foreign keys. No provider credential,
password, email, browser role payload or commercial customer record is stored.
The raw ERP token is CSPRNG32bytes, returned only internally for a future HttpOnly
cookie. Default JSON excludes token and credential/browser handles; diagnostic
string representations are redacted.
## 13. UI
Keep Kuubu and GreenWebb sign-in surfaces separate. Add launch only after cookie/
request adapters and protected route/data checks qualify. Uncertain launch must
retain its reviewed command; retry is explicit. Company/module switching is
explicit and invalidates the replaced selection; stale tabs must reselect.
## 14. UX
Differentiate changed access, expired login and provider/key/DB unavailability.
Do not silently follow a newer grant, adopt another company or widen branches.
Local logout revokes its own session without requiring an available provider.
## 15. Notifications
No new emails. Issue/revoke commit consequential audit and ID-only outbox events
with session state. Delivery/consumption is separate and cannot grant access.
## 16. Documents
Sessions do not grant documents independently of current module/action/data scope.
## 17. Errors
Invalid material, denied authority, conflicting intent, limited issue budget and
unavailable DB/key ring have distinct internal outcomes. Cancellation propagates.
Transaction failures and expired authority roll back new and replaced evidence.
Never return an old token/scope as an unavailable-success fallback.
## 18. Integration
Current person/license are supplied by the real operational flow in the future
adapter, not deserialized client claims. A request's module/action/resource owner
must come from server route policy. Unknown routes remain denied. Do not reuse
legacy empty-branches-means-all or overwrite bound authority with legacy role
refresh. Company-owned resources need explicit module policy alongside branches.
## 19. Audit/privacy
Audit/outbox contains session/company/grant references, module and outcome, never
raw/protected token, provider credentials, browser/credential hashes or proof
bodies. Cross-company replacement writes preserve each record's owning tenant.
## 20. Configuration
No DI/route/default-auth change yet. Use the existing private persisted Data
Protection key ring before activation, shared by intended replicas and included
in encrypted recovery procedures. No global key-store/DNS/trust changes.
## 21. Security
Token hashes permit lookup; recovery tokens are protected with purpose-bound row,
identity/browser/credential, exact scope digest and deadlines. Resolution verifies
the protected token too. Branch lists are sealed at issuance: EF blocks later
appends; PostgreSQL enforces same issue transaction, ownership and deferred
cardinality; the authenticated digest rejects unauthorized list changes even in
providers without those triggers. Immutable DB guards reject edits/deletes.
Issuance owns its whole transaction and disables EF savepoints, so the PostgreSQL
insertion seal uses the same top-level transaction ID. Any failure rolls back all
changes. Read-only/provider failure never silently revokes a recoverable session.
## 22. Reliability
Exact retry recovers through independently created readers of the same key ring.
Missing keys are unavailable, not permission success. Expiry/revocation survives
new registry instances. Populated migration rollback refuses evidence destruction;
empty rollback/reapply must pass. Capacity, key rotation/restore, monitoring and
production load/availability remain release gates.
## 23. Performance
Unique token and actor-command indexes; bounded branch lists/readback and recent
issue windows. Actor locks serialize issuance/logout; company locks align with
team/grant changes. Deadlines use lossless UTC tick conversion for portable
indexed comparisons and exact authenticated metadata round-trip. Existing ERP
columns are unchanged. See Microsoft's [SQLite limitations](https://learn.microsoft.com/ef/core/providers/sqlite/limitations)
and [value conversions](https://learn.microsoft.com/ef/core/modeling/value-conversions).
PostgreSQL documents top-level IDs for [transaction information functions](https://www.postgresql.org/docs/16/functions-info.html).
## 24. Tests
CoreProductSessionRegistryTests covers explicit issue, no plaintext/JSON secrets,
exact retry/key-ring recovery, conflicting intent, logout, expiry, wrong binding,
regrant, atomic cross-company replacement/rollback, budget, failed commit/stale
proof/cancellation, scope sealing, sub-microsecond deadlines, missing keys,
immutable/FK guards, empty/populated rollback and native concurrent issuance.
Run SQLite and separate isolated native PostgreSQL gates with prior Core/session
scope/grant/operational regressions. Tests do not prove real browser admission.
## 25. Deployment
Owning migration only, after review/backup and native qualification. Production
runtime continues to require schema readiness; the ordinary process does not
implicitly apply migrations. Do not deploy or enable public ERP sessions yet.
## 26. Rollback
Empty new schema may roll back; populated evidence requires an explicit retention
plan. Preserve all failed/partial build and qualification artifacts in new roots.
## 27. Acceptance
Qualified component:648 Windows passes/44 explicit environment skips and141
native PostgreSQL passes/zero skips. All32 ordinary registry cases pass in each;
native adds four cases for concurrent exact retry, direct mutation guards,
composite foreign keys and empty/populated rollback. These use synthetic current
person/license observations and actual Core commands, registry persistence,
key-ring readers and relational checks. They do not qualify a new real-provider
browser ERP launch or registered/default authentication.

Evidence: inspection/qualifications/product-session-registry-core-03,
product-session-registry-native-02 and
product-session-registry-verification-01/verification.json. Nine compiled C# inputs,
32 independently copied dependency DLLs and586 read-only native runtime files
retain verified hashes. The disconnected model check and native owning startup
both show no pending model changes. Four owned test containers are stopped with
no published host ports. Domain/Infrastructure/API/test builds have0/3/27/21
warnings respectively; the inherited test EF1003 is the previously documented
schema-failure fixture over hardcoded identifiers.

Prior design builds/drafts and failed core02/native01 are retained. The first
regression had647 passes/one legacy error-contract failure: a new membership
alternate key let EF preempt the existing domain rejection. The corrected schema
keeps that legacy key unchanged, retains scoped grant/link foreign keys and
checks the exact principal/membership/link pair in the native insert guard.
No test assertion or access guard was relaxed. Native01 was stopped after the
verified regression; its exit137, unchanged binaries and stopped DB are retained.
Earlier unapplied migration drafts were independently archived before UTC-tick
mapping and key-contract correction. No production migration was applied.

Registry correctness alone does not qualify cookie handling, real provider/
current-proof integration, registered authentication, actual protected reads/
writes, browser launch, ingress or full SaaS/dedicated/on-premise readiness.
## 28. Ownership
Core/identity owns registry, session lifecycle and request scope; modules own
business decisions and record scope. Publish exact code/handoff/milestone
baselines. Preserve Perfex, one ASP.NET ERP codebase and separate branded auth.
Lenco, hosting/domain/email, controlled rollout and offline licensing/upgrades/
backup restore remain part of the full GreenWebb platform programme.
