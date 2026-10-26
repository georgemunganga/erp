# P24 M0 request scope and Procurement UI release

Status: deployed on 25 October 2026. The [sanitized deployment receipt](P24-M0-COMPANY-REQUEST-SCOPE-RELEASE-20261025.json) preserves the operator result bytes (SHA256 `4f8bd97cc2639c4e6920e960f0e8c951f1286f4f33761f12f215b0e018a6c0b7`), reviewed plan `e1076df1473f2f16c5ced9a2083eb194f6c6f79f752dc83a29a28ce52a37740d` and COMPLETE cutover.

- Backend [PR94](https://github.com/georgemunganga/erp/pull/94): exact compiled-compatible source `8fe231438aebb1ade8ac47c4584dd2e7ee3866f5`; API source/output equals the actual `d4807f80` compilation after a test-only correction.
- UI [PR80](https://github.com/georgemunganga/module-connect/pull/80): `bc871e75f37404d951f4211e6185dae3b1273e90`, preserving the existing workspace.
- Current API/publisher image: `sha256:ceb4cbeb80fc57fb65370bb55026c3c95c5804bf04746f473365b7257497ccf7`. Both run with zero restarts and validate-only Core startup; the API is healthy. The old `e8330656f4e9` pair remains retained and stopped.

The delta image changes only the two genuine API DLL/PDB products; all 150 actual image files match publication manifest `cc722ea4fc7c3094e02ddeae8fa5ab210fa34a099cf3747d8f6c58e1e9dae020`. Only the Procurement Nginx static alias changed, to `m0-p24-20261025`. All five actual public UI file hashes match the accepted live build. Four public pages returned 200 and four anonymous API checks returned 401. No deployment database changes, role grants, migrations or production business test writes ran.

[Private acceptance](P24-M0-COMPANY-REQUEST-SCOPE-PRIVATE-VERIFICATION-20261025.md) remains separate evidence: 105 focused/1,070 Host tests; Node22 TypeScript/live build, 85 units and 16 mocked browser cases; actual normal-cookie PostgreSQL HTTP and two real browser cases. Independent owner verification preserved ten initial requests, proved read/denied-All/browser zero effects, same-cookie B revocation with A retained, then one selected-A header/line and three audits with zero outbox. Earlier helper, fixture compiler and zero-test browser failures remain retained, not relabeled.

The bounded release makes own-request All reads respect the current permitted-branch intersection before count/page, retains company-owned plan/contract semantics, and requires a concrete branch for request writes. It does not complete whole M0/C01 or the Procurement gap register. Orders, receipts, invoices, dashboards and other branch-owned aggregation need separate contracts/proofs. Actual production-user CRUD UAT and business acceptance were not performed. No later Procurement schema, approval/Finance/Inventory workflow or new collaborator assignment is introduced.

## Published merge and hosted-CI boundary

ERP PR94 merged as `27eebe4a5e40f5dba95ecc3f4be4184e6b89913c` into `codex/procurement-scoped-exports-p24-20261025`; UI PR80 merged as `728c87e38af0ba5351f7c62c283ed4b0743ec185` into `codex/procurement-scoped-exports-ui-20261025`. Neither merge targets ERP/UI main or merges the held later Procurement stack. The deployed build remains the exact `8fe23143` / `bc871e75` source pair. No administrator bypass or new collaborator assignment is claimed.

The [hosted Actions job](https://github.com/georgemunganga/erp/actions/runs/40230917037/job/121282253282) for ERP PR94 failed with zero steps. Its annotation states: “The job was not started because your account is locked due to a billing issue.” Hosted CI is therefore **not green**. The actual local official SDK 105 focused/1,070 Host passes and private cookie/PostgreSQL/owner/browser acceptance remain separate evidence; they do not relabel the hosted result.
