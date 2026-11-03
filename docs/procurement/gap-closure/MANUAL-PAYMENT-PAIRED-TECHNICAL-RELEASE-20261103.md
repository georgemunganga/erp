# Manual payment paired technical release — 3 November 2026

**Deployed, bounded technical acceptance; payment processing remains off.** Tracks [issue158](https://github.com/georgemunganga/erp/issues/158), with parent [issue145](https://github.com/georgemunganga/erp/issues/145) still **NOT READY** for whole-module/business-owner acceptance. Root executed and reviewed the release. This documentation change performs no runtime, database, grant or activation operation.

## Published and deployed pins

| Boundary | Exact pin |
| --- | --- |
| Published ERP main | `61d5a608f9cc6069bcf8d06ef51bdca036fd8d47`, including payment PR159, policy-retirement PR164, status PR166 and reviewed finite-grant PR167 |
| Published UI main | `1e7922ae178351fa006bdf4a9aaeb44bc903f94e`, including UI PR102 |
| Deployed backend product | `53ed4d5486173580660cb3ea571c3703f227c85a` |
| New production API | `be45e03038a596a7e52ee5deab74a0300493eb34849fe33cc79d99aa687577d3` |
| New image | `sha256:eb2077c982a7e24722c1afeb1a3206b9c5dfea2e67af4f984089800bc24400aa` |
| Deployed preserved UI product | `567e318105687b655a886e79c07eebd2b6bf4563`; alias `payment-write-567e3181-20261103/` |
| Processing/configuration | `Procurement:ManualPayments:Enabled=false`; actual production payment-review policy counts are zero, including published/currently effective policies |

The alias identifies the product, not business activation. All production application Env and HostConfig remain exact; PG/publisher identities/configuration and 231 user tables are unchanged. No production schema migration, new Core user grant, employee prerequisite, business policy/threshold or Finance/bank operation was supplied by this cutover. Old API `60c738a2...`, image `ef146c6e...`, UI `payment-read-ed044-20261103/`, failed candidate containers and release evidence remain retained.

## Verification and explicit boundaries

- Final backend source `53ed4d54` passed 361 focused Host cases; latest UI `567e3181` passed typecheck/live build and 172 unchanged tests across eight payment/scope files. These are compiled/source checks, not production CRUD.
- Private run03 backend `d479eb29` passed normal Core-cookie policy/native HTTP and finite-role/current-action/branch revocation negatives. Four workerless Core accounts completed six original-UI forms on product `ed044333`: Create, Revise, Submit, independent Approve, external Record and independent Confirm, ending ConfirmedManually version6. Independent HTTP/browser owner reconciliation preserved original row multiplicities across 238 fixture tables and the 197-table source, including the original seven requests/28 receipts; no Finance payment-status rows were added. All 17 synthetic sessions from the combined cohort are now revoked; the original 192 sessions remain unchanged. Private policy API `85d1...` is stopped. The later UI `567e3181` normal-cookie label READ against `d479eb29` preserved the record/tabs and displayed human branch/status labels without business mutations. These private pins are not relabelled as a final53ed or signed-in production write rerun.
- Before traffic changed, the actual final full-governance provider returned true on the production finite execution connection in the second canary. All four configured DSNs were forced read-only, no application server/auth/customer transaction ran in the canary, and the stopped successful result is retained. The first negative remains separate evidence.
- Fresh 16:11 encrypted online database+globals capture passed authenticated hash/catalog integrity within the approved freshness window. No new full-restore rehearsal is claimed for this release.
- Successful cutover run02 passed internal healthy API/auth denial before the Procurement-only atomic Nginx alias switch and config test/reload. Post-cutover checks matched all 150 live managed API files, five primary UI and37 compatibility assets, 15 public route/auth responses, and seven fresh anonymous browser renders with zero page errors. Public root health paths remain404; internal health remains healthy. Anonymous renders are not signed-in production business/UAT acceptance.

Private test policy values and grants remain synthetic fixture inputs, not approved production purchasing limits. Finance continues to own posting, payment execution and reconciliation; ConfirmedManually does not mean Finance Paid or prove a bank settlement. No verified document upload/scan/attachment transport is claimed.

## Reviewed finite-role prerequisite

The initial full-governance read-only canary failed closed on 11 missing technical Procurement rights. [PR167](https://github.com/georgemunganga/erp/pull/167) supplies the reviewed [forward](../../../backend/procurement-api/ops/payment-write-finite-grants.psql), [ACL projection](../../../backend/procurement-api/ops/payment-write-finite-grants-acl.psql) and [rollback](../../../backend/procurement-api/ops/payment-write-finite-grants-rollback.psql) scripts. Root applied the exact owner transaction before the accepted retry:

- INSERT on `manual_payment_requests`, `manual_payment_decisions`, `manual_payment_receipts`, `manual_payment_workflow_cases`, `manual_payment_core_decisions`, `manual_payment_date_observations`, `operational_policy_identity_evidence` and `policy_versions`.
- UPDATE on `manual_payment_requests` and `policy_versions`.
- Column UPDATE on `supplier_invoices.id`, the narrow PostgreSQL invoice-lock prerequisite.

All are on the existing finite execution role, non-grantable. The complete before/after ACL projection proves exactly 11 additions, no removed rights, unchanged security metadata and no other privilege/ownership/membership/default-ACL change. No broad HR/password/payroll SELECT, Core identity write or Finance payment-status write was added. The new positive actual production provider proof follows those exact grants; source/readiness alone was not used to justify activation. The frozen preparation plan's earlier no-grant assumption is superseded only by this explicitly reviewed prerequisite; the cutover scripts themselves apply no grants.

## Controlled rollback and retry

This was **not a first-attempt pass**. Cutover attempt01 detected a public Procurement UI 500 and rolled back both the API and alias, with no rollback errors. The old service/alias passed recovery checks; all old/new containers, files and receipts were retained under `cutover-attempt01-rolled-back/`.

The staged UI root was 0700: the process umask 077 filtered its requested 0755 and the staging loop set child permissions without revisiting the root. Root changed **only that artifact root directory** to 0755 and verified parent traversal and all 42 manifest entries unchanged. No UI product, content hash or backend source changed. A separately approved run02 used a distinct failed-candidate fallback name and passed cutover, public verification and anonymous browser checks. The earlier500, full rollback and repair receipt remain evidence; they are not overwritten by the final pass.

## Production policy setup before any activation

The existing code/forms already support the manual-payment review policy and current Core assignments. The remaining setup is actual configuration, not another generic payment engine:

1. The accountable business owner supplies its own reviewed `MANUAL_PAYMENT_REVIEW` policy scope/currency/exact limits and independent author/reviewer publication through the existing governed policy lifecycle. Do not copy fixture amounts, invent a policy ID or treat technical grants as business approval.
2. Assign current Core accounts the required duties/actions and company/branch access, with creator/approver and recorder/confirmer independence (including denial-only worker aliases). An employee/HR/payroll link is unnecessary.
3. Root separately reviews the real published policy, current actor/scope controls and activation/signed-in production acceptance. Until then the processing flag remains false; retained READ/history/original-key recovery stays available. This release does not perform those policy/grant/enablement steps.

## Evidence anchors and remaining holds

Raw files, identities, cookies, DSNs, business bodies and backup keys remain protected under `procurement-manual-payment-write-paired-release-20261103_run01`; only receipt hashes are published here.

| Protected receipt | SHA256 |
| --- | --- |
| `CUTOVER_COMPLETE.private.json` | `393d866d76751ab7405404a20231a86a370a4f8a60f6248b930ab0790a733117` |
| `PUBLIC_VERIFIED.private.json` | `bd428b549f644fa08986126c7302d5783ce2de988c41d002e91cb97f8a868397` |
| `PUBLIC_BROWSER_COMPLETE.private.json` | `53a94dac0f63d4eb3a7ea62bd69df003a37d7443cea2310b24be0800fb573107` |
| `PUBLIC_BROWSER_VERIFIED.private.json` | `346dd1e41e47e7cacdb1104cc60188285140ff4e366818bea701dd0bdd35de5b` |
| `canary-accepted.private.json` | `b2a2c6e83eace59693acb703c50370663d02339cd92222a008c8683de28c0651` |
| `technical-grant-prerequisite/technical-grant-accepted.private.json` | `e2d3897583028ba3c810233a51eac4d3c23e687e9c07e6bcc300ab71b816d350` |
| `ui-root-permission-repair.private.json` | `687d219add162f02ac409692737aeeb66eb37f20dd6406a6c1b27181608c1d37` |
| `cutover-attempt01-rolled-back/ROLLBACK_RETAINED.private.json` | `d417d737f906ad67ff9824519dbdf1fe05bc03958fc43dba0bc5343d08ae5779` |
| `backup-integrity.private.json` | `3706ca1084feabb92757cf2bbaf1e2427263ac7e41964f67d0ae2d3774439c83` |
| `production-policy-counts-readonly.private.json` | `73f0b9e7f8c4f0bd49c977e2e0db09c748991c659c70c7395efff924ed2596a2` |

Parent145 remains NOT READY; no business-owner sign-off or hosted-CI green is claimed. Payment production policy/activation/signed-in acceptance and wider alternate/concurrency/UAT criteria remain separate. Sourcing PR165 remains unaccepted source: 59 new test cases written, no passing SDK/genuine migration/native/UI checkpoint. Its first SDK attempt ended in managed compiler OOM **before EF**, so no generated migration is claimed; Root's paused private fixture PG supports the separate SDK retry and does not affect production. Scanner153 stays external with no verified attachments. Formal round submission/opening/evaluation, award/PO integration and the other [UAT gaps](PROCUREMENT-UAT-AND-GAPS-20261027.md) remain open.
