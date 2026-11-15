# Shared API gateway installation

The edge is deployment infrastructure in the maintained ERP repository. It does
not duplicate HR, issue tokens or infer company/employee access. The first release
publishes discovery/readiness and reserves the versioned attendance contract, but
holds employee operations until native OAuth and worker admission are qualified.
`mobile_ready:false` is intentional and must not be changed just to unlock a UI.

Generate the site beside the existing app site:

```sh
python3 deployment/kuubu/render_api_gateway.py \
  --host api.kuubuerp.com \
  --upstream https://127.0.0.1:28984 \
  --canonical-host app.kuubuerp.com \
  --trust-file /etc/caddy/kuubu-root.pem \
  --issuer https://commercial-api.greenwebb.tech
```

Validate the complete candidate configuration with the pinned Caddy image before
cutover. Preserve the current app-host block, read-only module mounts, certificate
storage and prior proxy for rollback. No direct database ports should be public.
No private configuration or identity keys belong in this template or Git.

Public contract:

| Surface | Behavior |
| --- | --- |
| `GET /.well-known/kuubu-api` | Installation discovery and explicit mobile-readiness flag |
| `GET /health/live`, `GET /health/ready` | Existing ERP probe over trusted upstream TLS |
| `/v1/hrm/me/attendance` and `/today`, `/clock-in`, `/clock-out` | Reserved staff operations;401 without bearer,503 pending native admission |
| Browser cookies | Rejected on staff operation surface; never shared with app origin |
| Unknown routes/modules |404; no unrestricted backend/admin proxy |

Before enabling staff operations, qualify a public native OAuth client with exact
redirect URIs, code+PKCE, issuer/audience/expiry/revocation checks, explicit company
selection, active module entitlement, employee identity link, own-record scope,
clock retry/idempotency behavior and denied/cross-company cases. Existing SaaS
browser/admin endpoints are not a substitute for this contract.

## Dedicated / on-premises

Use the same maintained source and contract, with an installation-specific API
hostname, upstream trust and identity profile. A mobile app must let the customer
select or discover its installation base URL; do not send dedicated customer HR
data through the SaaS service by default. For example New World Cargo can use its
own HTTPS origin once its release/profile is qualified. This file does not change
that customer server or claim it has been inspected.

Git updates deliver source only. Build/publish the compatible release, back up and
apply required migrations, configure TLS/proxy and identity/callback registration,
then test against the customer's records/permissions. Existing local credentials
must remain supported; do not enable SaaS product mode to imitate mobile support.

## Inspected mobile consumer

`Greenwebb/vcron-attendance-mobile` at
`0941bf29a651c105e5116047f4012f839f20ec8e` is Expo/React Native. Both preview package
IDs are `com.greenwebb.vcron.preview`, with URL scheme `vcron`. No native OAuth
callback handler, production sign-in or HTTP ERP adapter currently exists.
`src/services.ts` implements synchronous sample employee/schedule reads and local
attendance mutations; those interfaces need asynchronous server reads, secure
credential storage and explicit errors before a production adapter can replace it.
Do not silently fall back to sample attendance when the server returns401/403/503.
The existing preview identifiers are not evidence of production app registration.

The staff mobile release gate is broader than DNS: add public-client code+PKCE,
employee-only HR admission (the SaaS adapter currently requires HR administrator
coverage), company discovery/selection, exact employee linking, authoritative
timestamps and replay-safe attendance writes. Then test two companies, another
employee's record, revoked membership, expired license/token, repeated clock writes
and actual app restart/reconnection before changing readiness to true. Payslip
downloads are a later explicit capability, only after payroll release permission.

Additional attendance contract findings: the existing self-service GetToday method
initializes/updates attendance and audit rows, and ClockIn/ClockOut select the current
UTC day. A native adapter must not blindly turn background reads into writes or
assume those methods implement local-business-day/overnight behavior. Qualify a
read-only status contract and explicit server-owned clock/overnight/retry rules
inside the HR module before opening the held gateway routes. Do not reproduce HR
calculation logic in the gateway or trust the mobile device clock as authoritative.
