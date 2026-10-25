# R0 — P29 source compatibility and upgrade rehearsal

Status: source integration in progress; no schema execution, deployment or feature activation authorized by this document alone.

The live release is product `cb18353d` / UI `09212140`, image `89375b15`. Source starts from published ERP main `cafcffd2`; original checkout and held P30–P38 branches remain untouched. Reconcile the deployed Core/catalog/CSV/scope behavior into this isolated branch while retaining the newer main business rules. Do not replace newer authority or identity logic with an older compatibility implementation.

Core retains identity, roles, company/currency, branch authority, shared sessions/schema readiness, documents and audit infrastructure. Procurement owns its records and transitions. System users need no employee link. Preserve the current imported UI and its served build; this checkpoint does not rebuild the ERP shell or move global settings.

## Source scope and checks

- Preserve main's active/nonarchived-role hardening and existing P29/manual-payment authority conditions.
- Preserve the live server-driven action catalog/readback, explicit export grants, current Core scope, request All-list filtering/branch references and exact selected/all/search CSV behavior. Existing reviewed company-wide manual-payment contracts remain explicit; do not erase them by copying a narrower P24 allowlist.
- Add no entities, invented fields, historical migration edits or grants. Reuse existing 28-section Core/Procurement contracts; any new behavior needs its own specification.
- Pin source/API/UI/schema separately. Run actual source/model parity, relevant rules/authorization tests, full Host regression and native normal-session/owner proof. An endpoint, mocked UI, manifest or filename alone is not acceptance.

## Genuine forward migration sequence

Live P24 ends at `20261022144102_P24CorePrincipalRecurringPurchasePlanning`.

Procurement P29 checkpoint (five existing migrations in its own history):

1. `20261022161353_P25CorePrincipalRequestApproval`
2. `20261023125243_P27ManualPaymentRecords`
3. `20261023153226_P28ManualPaymentGovernance`
4. `20261023204131_P29CorePrincipalSourcingDrafts`
5. `20261023213118_P29CanonicalPrincipalSourceGuids`

The target also requires the separately owned Core migration `20261023130726_CoreCompanyTimeZone`, with its own Core history and model snapshot. It adds `hrm.legal_entities.time_zone_id` as `varchar(100) NOT NULL DEFAULT 'UTC'`. Its genuine Down migration rejects removing retained company date configuration when any company exists. Verify actual live Core history/physical columns before deciding whether it is pending; the source inventory alone cannot establish that. Rehearse its genuine owner-only forward path before target validate-only startup, retain company configuration and test backup restore/fix-forward. The five Procurement IDs are not the full upgrade.

P30 is a separate integration of conflicting draft ERP PR50, currently `d29c158e`, after the P29 source checkpoint is reviewed:

6. `20261023222748_P30PrincipalCommercialWorkflows`
7. `20261023234712_P30PrincipalCommercialAuthorityActions`

The adjacent JSON inventories Core organisation, Core approvals, Core documents and Procurement separately and records exact migration-file SHA-256 values and common/new/removed file sets. Review every changed common file, including a snapshot, against the actual live history and physical schema. A filename comparison does not certify the production schema. Keep later P36–P38 branches and matching UI releases held behind their own prerequisites.

## R0 execution gates

1. Inventory the actual live Core/Procurement/document/approval migrations and catalog with read-only access. Pin the exact target code/model and paired API/publisher.
2. Confirm a recoverable protected backup and restore it on isolated nonproduction storage with sufficient capacity. Retain credentials/customer data privately; publish only redacted evidence.
3. Apply genuine forward migrations in order to the representative restored P24 database. Verify retained IDs, versions, audit/history, scope, references, quantities/values, constraints and owner/finite runtime privileges. No API-startup DDL.
4. Rehearse backup restore and a compatible rollback or fix-forward path; test the old/new reader/writer compatibility honestly. Keep capability flags off until that workflow's own acceptance passes.
5. Execute one coherent normal-session UI/API/owner cohort, with negative scope, authority, retry and recovery cases. Record business UAT separately.
6. Only after these gates, plan a reviewed paired API/publisher release and relevant UI change with monitoring, rollback and postdeploy smoke tests. Never run two incompatible model writers together.

No new collaborator assignment or main merge is made here. Publish reviewed prerequisite merges and current ERP/UI main baselines before assigning the next task. Real scanner C04, policy decisions and external ledger adapters remain separately owned dependencies; pending ones must not block unrelated defined CRUD.

## Reconciliation observations

- Published main already owns shared sessions and schema startup. Its identity checks reject inactive/archived configured roles without reviving legacy CSV authority. Keep those stronger checks; copying the older deployed LocalIdentity file would regress them.
- Core's branch choices need the deployed additive `type` field for headquarters-aware defaults.
- The action catalog is derived from the current target registry, including newer payment actions plus explicit Vendor/Item exports. Do not pin the catalog to the old P24 action count or replace newer registry entries.
- Preserve P29's reviewed company-wide manual-payment scope while adding the deployed master/request readers. Readiness, permission, amount, authority and business-state guards remain separate. A scope-only test does not authorize a payment.
- Broader operational All behavior remains an M0 gap. This source checkpoint carries the deployed legacy concrete-branch fallback outside the reviewed All readers, together with the explicit request-write branch requirement; it does not declare all transaction routes company-wide.
- The Procurement source inventory found no removed migration files and no changed common historical migration/Designer files. Its target snapshot differs alongside five new P25–P29 migrations and guard helpers. The expanded four-context inventory additionally identifies CoreCompanyTimeZone and the Core organisation snapshot; never omit that Core requirement. Physical database/backup/restore/finite-role acceptance remains unexecuted.
