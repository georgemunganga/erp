# Actual product-provider qualification

These tools own fresh loopback fixtures for the real Laravel Passport provider
and the maintained Kuubu account client/signed relay. They create no production
keys, customers, memberships, subscriptions or Core records. Email uses the array
driver; payments are disabled. Preserve all consumed or failed fixture directories.

`run.py` requires absolute `--workspace` (must not exist), `--foundation` (the
Laravel backend directory), `--website` (exact published website source), and
`--php` executable paths. It verifies ports8101/8120 are free, prepares its own
SQLite database/keys/storage, starts only owned loopback children, and stops them
on interruption. Keep its logs and `stopped.json`; never print private manifests.

Ordinary staff qualification uses the default mode with **zero billing
memberships**. Set `KUUBU_REAL_PROVIDER_FIXTURE` to its private
`provider/core-interop.json`, leave `KUUBU_REAL_SETUP_PROVIDER_FIXTURE` unset, and
run only `ProductProviderInteropTests` against the compiled test assembly.

Protected setup qualification is a separate fresh run with `--with-setup-owner`.
That explicit flag seeds one synthetic owner membership; staff retain none.
Set `KUUBU_REAL_SETUP_PROVIDER_FIXTURE` to that run's private manifest, leave the
ordinary variable unset, and run only `ProductSetupProviderInteropTests`.
Do not run these consuming cases concurrently or reuse a consumed fixture.

The protected case performs ordinary staff sign-in first, then separately
consented owner setup, explicit personal enrollment and real confidential S256
exchange. It checks the actual payer list/authority and machine license, company
create/retry/capacity, revoked owner and suspended subscription denial, cancel
invalidation, role-free owners and zero operational sessions. Owner revoke/restore
and subscription suspension endpoints are fixture-only controls, gated by the
private marker and opt-in mode; they are not application routes.

The test transport adapter accepts only the pinned HTTPS commercial issuer and
reviewed protocol paths, then directs those requests to its actual loopback
Laravel instance. It fabricates no provider response and changes no production
TLS/issuer validation. Core allocation uses a real private SQLite database;
separate PostgreSQL gates qualify native schema/concurrency. These cases do not
prove deployed browser TLS/ingress, production PostgreSQL interoperation,
operational admission, payment processing or a customer SaaS launch.

Record exact ERP/foundation commits and website runtime Git blob hashes before
execution. Use an exclusive new qualification checkout, plain cache copies,
verified SDK and fresh result/log paths. The private manifest contains synthetic
client credentials; no tokens, cookies, OAuth state, passwords, request bodies or
manifest content belong in public logs or PR descriptions. Read-only aggregate
provider/Core database audits are suitable evidence after the processes stop.
