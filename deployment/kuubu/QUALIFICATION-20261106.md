# Kuubu image qualification — 6 November 2026

This is a separate release qualification on the authorized Kuubu host
`2.24.140.104`. It does not update the older ERP production installation, enable
held Procurement workflows or establish multi-customer SaaS readiness.

## Exact source and evidence

- Documented application baseline: `e340a6f46ada8b2402b04eea8a7734c67004f348`.
- Qualified image source: `408da398210c6e5e220077f4000ecdfc6cedb0aa` on the
  published `release/kuubu-baseline-20261106` branch. Changes from that baseline
  are Docker packaging and regression fixture repairs; application logic remains
  unchanged. Operations scripts are subsequent commits on the same release line.
- Full original Docker test-and-publish gate: **1,972 passed, 0 failed, 0 skipped**.
- Local image tag: `kuubu/erp-api:408da398-build01`; inspected image ID
  `sha256:bb8e62aa95213e88ceaca3bd3bcd8986e054b6cba5b7c37c46e0d4c470b1568b`.
- Exact source archive SHA-256:
  `e69e579603faa2ac75679c0230f04d55d148e998d79165eacb62eeb8b2e8c647`.
- Retained full build log SHA-256:
  `3085c5090edeb3d6b0de83d8efaabec0d76bfa87fefa4cd25d80c5ec321e33c3`.
- Actual runtime smoke: UID1654, WeasyPrint70.0, valid 6,491-byte PDF; container
  networking disabled. This verifies the rendering dependency, not payroll
  calculations or document-layout/business acceptance.

Host evidence is retained under
`/opt/kuubu-builds/runtime-408da398-build01/`, including the successful smoke JSON.
Failed earlier build attempts remain retained; their paths/tags are not reused.

The newer main-line repair in PR199 separately passed **1,990 tests**. Its GitHub
hosted job did not start because the account is locked for a billing issue;
local runtime evidence must not be described as a passing hosted CI run.

## Deployment boundaries

The isolated stack has now applied57 genuine Core/HRM migrations. The API is
healthy on `127.0.0.1:28911`; administrator-user requests without authentication
return401 and the session probe returns200 with `authenticated:false`. There are
zero login users and sessions. Native runtime-role DDL and audit UPDATE/DELETE
denials passed. Qualification ACLs permit only startup role definitions/audit
append, session reads and exact existing shell-scope lookup columns. This is not
the operational module privilege contract.

Actual logical backup was restored into a new retained database
`kuubu_restore_20261106t141547z_9d6d196d`. All98 Core/HRM tables' record hashes,
owners, table ACLs and column ACLs matched. Dump SHA-256:
`fc86ecb73f30702ed86becb9d07fc58131e2ed421ce94709613cce37f485407b`.
The root-private receipt is
`/opt/kuubu-qualification/receipts/recovery_20261106T141547Z_9d6d196d/receipt.json`.
This is same-host recovery; off-host recovery and recurring backup/alert operation
remain pending. No existing database was restored over or deleted.

Qualification uses pinned images, separate generated bootstrap/schema/runtime
credentials, genuine Core/HRM migrations and an API loopback binding only.
No default customer, administrator identity, subscription, enrollment or company
is seeded. There is no public ERP ingress in this configuration.

Operational module ACLs, tenant/session resolution, global identity admission and unrelated-tenant denial
acceptance must be recorded separately before customer launch. Image success is
not completion of those gates. Perfex and central Laravel remain separate owners.
