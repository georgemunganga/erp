# Kuubu deployment modes

User requirement confirmed8 November2026: support shared hosted SaaS and separate
customer installations, including on-premise. Both use the maintained ASP.NET ERP
and its existing modules; do not fork source or pin a customer to an unmaintained
custom ERP. This document defines required behavior, not a completed installer.

| Concern | Shared hosted SaaS | Dedicated hosted / on-premise |
| --- | --- | --- |
| Runtime | Shared maintained application deployment | Separate customer deployment |
| Database | Company tenant isolation within the shared database | Separate customer database; existing company/branch structure |
| Identity | Explicit Kuubu enrollment and verified central product identity | Preserve existing local accounts; any central SSO integration is explicit |
| Company access | Core-owned membership plus licensed allocated workspace | Core/local company and branch permissions appropriate to the dedicated installation |
| Commercial operations | GreenWebb owns subscription/entitlement decisions; Perfex keeps its current billing responsibilities during migration | GreenWebb commercial records remain separate from customer ERP data; activation and renewal policy still need implementation |
| Updates | Qualified shared release and controlled migration | Same maintained release line, applied through a qualified customer upgrade procedure |

A dedicated instance may support several companies/branches for that customer.
“Single instance” does not mean “single company.” SaaS provisioning/identity flags
remain opt-in; classic installations must not require the central product login
to retain their existing supported local workflows. Conversely, local login must
not become a bypass into shared SaaS tenants.

Deployment profiles are installation/operator configuration, never a browser
parameter or request header. Customer data, configuration, keys, uploads and
backups stay separate from application release artifacts. Reuse the same module
code and shared contracts; differ only where hosting/identity/license policy
requires it. Test both deployment modes before claiming a release supports both.

The current SaaS licensing reader uses the pinned central HTTPS provider. It is
not an offline on-premise license engine. If disconnected operation is required,
signed entitlements, expiry/grace, renewal, revocation and clock/recovery behavior
need a separately reviewed implementation. A provider outage must not silently
turn into unlimited access or cross-company authorization.

Dedicated release acceptance still needs a reproducible install bundle, supported
host/runtime requirements, secrets/activation setup, TLS routing, finite database
permissions, upgrade and populated migration behavior, backup/restore, rollback,
monitoring and a real dedicated customer journey. Existing classic multi-company
tests are evidence for preserved semantics, not proof of an on-premise installer.
Shared SaaS tests similarly do not prove dedicated operation or vice versa.
