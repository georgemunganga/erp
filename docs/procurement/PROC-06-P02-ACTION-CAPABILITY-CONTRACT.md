# PROC-06-P02 — Governed RFQ action and availability prerequisite

Status: Source authored for review; no build/test execution, migration, endpoint or production activation. Based on central P26 source cc9f94a. This supplements the 28-section governed draft adapter plan; it does not implement that adapter.

## Named actions and existing Core ownership

`procurement-sourcing-convert` and `procurement-sourcing-cancel` are separate existing-catalog action keys with empty bootstrap grants. Existing sourcing-read/create/edit and all historical role defaults remain unchanged. Core `JobsAdminServiceImpl.PermissionKeys` and its existing procurement-category normalization already derive their supported actions from `ProcurementActionCatalog`; no second picker, role store or permission validator is added.

An active configured Core role must explicitly grant the new action plus module access for ordinary users. Convert does not imply cancel, nor PR/PO/publication authority. Current configured grants must still be resolved by the future transaction adapter; old claims and metadata are not command authorization. Central existing ERP administrator handling remains unchanged. No new grant is written or backfilled, and no sourcing publication action is registered.

## Narrow technical capability

- Options: `ProcurementSourcingDraftExecutionOptions` bound from `Procurement:SourcingDrafts`.
- `Enabled` defaults false. `ExecutionDatabaseConfigured` is derived from the existing dedicated `ConnectionStrings:ProcurementApprovalExecution`, overriding any configuration self-declaration.
- Additive metadata: `capabilities.sourcingSourceDrafts` requires no pending Procurement migrations, enabled option, dedicated execution connection and `IProcurementSourcingDraftSchemaReadiness.IsReadyAsync`.
- Production composition currently registers `UnavailableProcurementSourcingDraftSchemaReadiness`, which returns false because the governed RFQ model, exact constraints, privileges and runtime adapter do not exist. No configuration switch can make that missing implementation ready. The true-readiness implementation must arrive with independently reviewed schema/adapter work; test-only readiness is not a production provider.
- Existing `capabilities.sourcingDraft` remains the unchanged legacy draft-CRUD flag; global `sourcing` remains false. No endpoint, publication, dispatch or Finance effect is introduced.

No extra permission hints are synthesized from stale metadata claims. The existing current Core permission/context contract remains the UI source; real future commands repeat identity, exact action and retained record-scope checks. Technical capability does not express per-source business eligibility or spending authority.

## Authored verification (not run)

`ProcurementConfiguredPermissionsTests`: five ordinary role bootstraps and legacy action combinations deny both new actions; empty/unknown/legacy configured permissions retain old fallback without gaining them; explicit convert-only/cancel-only resolve independently; current removal, inactive/archived/missing roles deny; no PO/approval action is added.

`JobsAdminTests.Existing_Core_role_CRUD_roundtrips_explicit_governed_sourcing_actions`: real existing Core service create/update/list accepts the exact new actions, preserves independent selection and rejects unsupported publication action. No parallel role service.

`ProcurementSourcingCapabilityHttpTests`: real mapped metadata with disabled flag, missing execution connection, unavailable schema and pending migrations keeps capability false; test-only complete technical gates can expose availability while legacy draft flag remains separate and global sourcing/orders remain false. Default production readiness remains false. Existing approval/order metadata fixtures register the disabled new options so unrelated capabilities retain their fixture composition.

No model changes, migrations, SDK/DB/browser jobs or deployment were performed. Next integration must preserve the then-current central P26 schema and complete verified RFQ restore, durable bindings/receipts, legacy isolation, current owner/scope/Finance checks, concurrency and real runtime gates before enabling a governed draft route.
