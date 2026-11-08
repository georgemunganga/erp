# Explicit Kuubu company invitations

## 1. Identification
Tier3 Core child feature, 8 November2026. Implementation in progress; not live.
## 2. Purpose
Let an authorized company actor invite a person who explicitly accepts access.
## 3. Scope
First increment: separate confidential-client recipient identity observation
and a strict server-side Core reader. Core reader is not yet registered or
exposed by a public endpoint; no accept flow can use it accidentally.
Invitation persistence/issue/revoke/accept, delivery, HTTP and UI remain to build.
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
Pending, accepted, revoked and expired; terminal outcomes cannot be repurposed.
## 10. Permissions
Owner may invite administrators/members; administrator may invite ordinary members only.
Owner promotion remains an explicit owner-authorized action. Server enforces all.
## 11. Rules
Email match alone grants nothing. Require an exact targeted invitation, token,
verified identity, enrollment, explicit acceptance and current Core guards.
Do not canonicalize provider aliases/dots or infer access from email domains.
## 12. Data
Planned invitation: immutable company/target/role/issuer, hashed secret, expiry,
revision, actor and accepted exact subject/member references. No plain token in
audit/logs; delivery payload encrypted at rest and bounded by invitation expiry.
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
