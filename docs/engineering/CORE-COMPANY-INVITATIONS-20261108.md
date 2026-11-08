# Explicit Kuubu company invitations

## 1. Identification
Tier3 Core child feature, 8 November2026. Implementation in progress; not live.
## 2. Purpose
Let an authorized company actor invite a person who explicitly accepts access.
## 3. Scope
Separate confidential-client recipient identity observation and strict Core
reader, followed by internal invitation issue/revoke/accept commands, persistence,
immutable receipts/audit and durable delivery intent. All remain unregistered and
unexposed by public endpoints. Actual delivery worker, HTTP/CSRF integration,
account authorization/consent relay and acceptance/team UI remain to build.
## 4. Parent
CORE-COMPANY-TEAM-20261108.md; one ERP release for SaaS and dedicated installations.
## 5. Actors
Current company owner/admin, invited verified and explicitly enrolled identity.
## 6. Entry
Invitation creation must explicitly name recipient, company and bounded role.
Acceptance must review the company/role and require an unchecked consent control.
## 7. Preconditions
Inviter's current Core authority and company license; recipient's verified current
email and explicit personal Kuubu enrollment. No payer membership is required.
## 8. Workflow
Issue commits invitation/audit/durable delivery intent before attempting delivery.
Acceptance checks a protected invitation token, current recipient observation,
current company/license/inviter authority, explicit confirmation and then commits
exact principal/link/membership/audit/outbox together. No automatic acceptance.
## 9. States
Pending at revision1; accepted/revoked at revision2. Expiry is computed from
ExpiresAt without rewriting history. Terminal outcomes cannot be repurposed.
## 10. Permissions
Owner may invite administrators/members; administrator may invite ordinary members only.
Owner promotion remains an explicit owner-authorized action. Server enforces all.
## 11. Rules
Email match alone grants nothing. Require an exact targeted invitation, token,
verified identity, enrollment, explicit acceptance and current Core guards.
Do not canonicalize provider aliases/dots or infer access from email domains.
## 12. Data
Invitation: immutable company/target hash/role/issuer/inviter binding, SHA256 secret
hash,48-hour expiry, revision, actor and accepted exact subject/member references.
Token is32 random bytes. Recipient hash is SHA256 of the provider-compatible
trimmed/lowercase address; hashing does not anonymize it. No alias/domain inference.
No plain token/email in receipts/audit/outbox. ProtectedDelivery encrypts recipient
and fixed /accept-invitation#token= link through Data Protection. Delivery intent
contains IDs only; worker must recheck expiry/state/license/inviter before sending.
Key-ring backup/rotation, retention/erasure and actual mail transport remain release
gates. Scoped company/principal/link/member FKs and guarded terminal transitions
apply in ORM and PostgreSQL; receipts are append-only. Populated rollback refuses
to discard invitation history or delivery obligations.
## 13. UI
Preserve Kuubu login. Separate company invitations from GreenWebb billing-team UI.
## 14. UX
Review recipient/company/role before issue; generic invalid/expired acceptance
states without company disclosure; no fake sent/accepted acknowledgements.
## 15. Notifications
Delivery cannot create membership. Queued retry/deduplication and real mail
provider qualification remain required; local acceptance uses mail=array only.
## 16. Documents
No employee, payroll or financial records exposed by recipient proof.
## 17. Errors
Default deny on missing scope, wrong client/product, unavailable provider,
unverified/un-enrolled identity, stale proof, expiry, revoked invitation or license.
## 18. Integration
Laravel adds a separately scoped confidential-Kuubu-only resource:
GET /api/v1/product-identity/invitation-recipient,
scope product:invitation-recipient together with product:identity. It returns
schema1, reviewed issuer, current subject/client/product, verified_email,
email_verified=true, purpose=company-invitation-v1, observed_at/proof_until,
and operational_access_granted=false. Proof is short-lived and server-consumed;
not a browser-supplied email, signed admission token or membership grant.
Normal subject responses continue to exclude email. Kuubu account authorization
UI/relay must separately support this scope and explain recipient verification
before any production activation; currently unsupported scopes fail closed.
## 19. Audit/privacy
Return only the current person's email to their scoped confidential product
client. Reject selectors; no user lookup, email directory, payer or company list.
Never log raw proof bodies, provider tokens, invitation tokens or OAuth state.
## 20. Configuration
PRODUCT_INVITATION_RECIPIENT_ENABLED=false plus existing identity flag. No schema
or production configuration change for the recipient contract. Later Core
invitations need their own default-off activation gate; classic local auth stays.
## 21. Security
Test real PKCE/tokens, scope escalation, public/foreign/licensing clients, revoked
tokens/client/binding/enrollment/verification, cookie-only calls, selectors and
default-off behavior. Core validates exact issuer/subject/client/product/purpose,
bounded observation time, email syntax and operational_access_granted=false.
## 22. Operations
No real mail, customer access, production deployment or DNS changes in this stage.
## 23. Postconditions
Recipient observation creates no customer, enrollment, subscription or company
membership. Later accepted invitation creates only consented Core governance.
## 24. Recovery
Lost responses retry the same operation key. No replay may resurrect revoked
membership. Migrations must preserve populated invitations and immutable receipts.
Issue retries return the historical receipt after current role/license checks;
new keys conflict while the same recipient has an unexpired pending invitation.
Acceptance retries require a still-usable explicit member/link/principal and
current inviter/recipient/license. Active members cannot use acceptance as a role
change. Inactive members need a new invitation and consent, which restores only
the requested bounded role and advances membership revision. All membership,
identity-link, receipt, audit and outbox writes roll back together on post-save
expiry, cancellation or failure. Receipt acknowledgements never establish access.
## 25. Acceptance
Explicit targeted acceptance and fresh provider/Core guards; zero operational
sessions, credentials or module-role grants. Complete flow still unqualified.
## 26. Tests
Recipient protocol and strict Core reader first; native transactional/concurrency,
delivery retry, browser acceptance, cross-tenant and recovery gates afterward.
## 27. Release gates
Do not enable this contract before account consent/relay, Core invitations,
delivery, migrations/runtime grants, audit and complete browser flow qualify.
## 28. Evidence
### Qualified internal lifecycle; public/delivery integration pending

Published source a419439942a77db22a968ef5d94bb2c10f53921e passed97 genuine
PostgreSQL invitation/team checks and90 shared identity/session/schema checks,
zero skips. Focused Linux passed261/14 native skips; full Linux passed2,355/18
opt-in skips, zero failures. All14 native-only opt-ins ran in the separate
cohorts. The four new native cases exercised real concurrent issue/accept,
revoke/accept races, history triggers and empty/populated rollback.

Read-only SQL audit verified97 new company/team databases:96 have the current
invitation schema, six validated FKs and both enabled history guards; one legacy
team-command rollback fixture intentionally retains the previous schema, two
members and its guarded role-change receipt. EF can commit an earlier empty
Down migration before a later populated-history guard refuses another Down;
reapply the qualified latest schema before current-runtime admission. The audit
verifies that exact legacy signature rather than altering the fixture. There
are24 invitations,29 immutable invitation receipts,24 delivery intents,33 matching
audits and57 events, including four accepted memberships. Request hashes,
actor/company/tenant/subject links, before/after revisions and correlations match.
No credentials, operational sessions or module roles were created.

Native attempt01 passed97, then the original64MB /dev/shm test database exhausted
shared memory and entered recovery;23 shared cases failed and later phases did
not run. No application assertion changed. Attempt02 uses a fresh isolated
PostgreSQL container with512MB shared memory, the same pinned engine image and
586 independently copied byte-identical binary input files. All hashes/sizes/
mtime remained unchanged after consumers. Both test clusters are stopped. Source
archive909df1c6ba7a869b3258375527e410503ea9c6e88a1e517cf2c5608a86ccaf7c;
full log6c2c1683b04885c3b7f1ebecc6d22624d8f1ed6362e5be21efb7306d44e09b20;
auditb4a0a7f68da91196456a89974753048edf4ea4398cf0483114404827e700051f.
Audit02's initial missing-table assumption failed on the intentional older
rollback fixture; audit03 retains that result and classifies the exact older
schema read-only. No test database was upgraded to make the audit pass.

Actual existing website/Laravel/Core/SSR browser compatibility passed47.167s
against this API and the unchanged UI661c30a0/websiteb01985ee. It covers own Kuubu
login/Google unavailable, explicit enrollment/setup consent, company create/retry,
fresh team sign-in, licensed reads, role/revoke, response loss/retry and subscription
suspension/replay denial. It uses a preaccepted-member fixture, not the new
invitation flow. Read-only SQLite audit verifies exact links/events, zero new
invitation rows/receipts and zero credentials/sessions/module roles. All487 SSR
output files unchanged; owned local services stopped. Three other standalone
provider/browser opt-ins remain unrefreshed for this runtime. New recipient wire,
mail delivery, public invitation HTTP/CSRF, consent relay and acceptance UI are
still unqualified. No deployed TLS/DNS or production activation.

Evidence: inspection/qualifications/company-invitations-native-a4194399-02,
company-invitations-audit-a4194399-03 and invitation-browser-compat-01, each with
verification.json. Read-only existing private ERP readiness and Perfex sign-in
returned200 after qualification. Production/Perfex/DNS/payment unchanged.

### Earlier local lifecycle gate

Fresh Infrastructure/API/test builds passed. Final local gate in
inspection/qualifications/company-invitations-04 passed243 tests, zero failures,
14 native opt-ins skipped. The complete identity/link/setup/team cohort includes
the existing identity constraints, not only new invitation cases. Design-only EF
10.0.11 generation and read-only model comparison passed without a database
connection. Four new native cases cover issue/accept concurrency, revoke/accept
race, physical binding/receipt immutability and safe empty/populated rollback.
They are not proven by this local-only result; the native qualification above
exercises them separately.

Attempt01 exposed missing link tenant persistence after introducing the scoped
inviter/link composite FK. Explicit trusted tenant construction restored setup/
team cases in02 (98 passed/four skips), but the broader identity cohort still
found five failing link insertions. Assigning the tenant through EF's property
entry fixes generated composite-key persistence;03 and04 pass the unchanged
identity constraints. Added constructors carry their trusted scope before
tracking.03 includes membership before/after audit and reactivation/expiry tests;
04 also verifies token/email command string representations are redacted.
All attempts and failures remain; no assertion or scope guard was removed.

No DI registration, public endpoint, delivery worker, UI, schema application,
production flag, payment, Perfex or DNS change. The following recipient-only
checkpoint is historical and does not qualify the newer lifecycle schema.

### Earlier recipient proof contract

Recipient contract Foundation source15707bd9eb04ee813aedc3797e425e5548548223
passed123 tests/980 assertions on isolated PHP8.4.12/SQLite and Pint. The same
source passed123 tests/981 assertions on genuine MySQL8.0.46 in a new network-none container
without host ports, host SQL/privilege changes or production mutations. Native
evidence: inspection/qualifications/invitation-recipient-mysql-15707bd9-01.
The container is stopped. Archive SHA256
dda64626e2fe71ccf0f38e396fe8bc6bacab35d8d2bb37a5a89d9478c11632b9;
native log24a72cb6b4522e57efbceab57510fa4ff8d64aaccc2c6faed544bcb61fdb77df.

Core reader build passed zero errors (27 existing API/20 test warnings).191
focused tests passed, zero skips;41 recipient cases cover exact identity/client/
purpose/email, bounded wall and monotonic expiry including transit, no stale
reuse, malformed/duplicate/oversized/non-JSON data, denial, transport failure and
caller cancellation. Other checks cover existing protected setup/team/identity.
Evidence: inspection/qualifications/invitation-recipient-core-01/verification.json
and invitation-recipient-foundation-03/verification.json. Independent dependency
copies retained hashes; no old restore or clean metadata copied. The proof class
has no email-bearing automatic ToString and never retains provider tokens.

No schema, feature activation, deployed ingress, UI, production, Perfex, DNS or
payment change. Recipient resource tests use actual Passport PKCE/tokens; the
Core reader tests use a synthetic HTTP provider. Actual provider-to-Core wire
interoperability and account consent/relay remain unqualified. Full Core
invitation lifecycle/migrations/authority/replay/concurrency, delivery and browser
acceptance still need implementation. Do not describe this proof as a membership.
