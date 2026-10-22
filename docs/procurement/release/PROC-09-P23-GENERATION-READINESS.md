# P23 original-unit review — generation readiness

Current status: P23 source is merged with [private milestone acceptance](PROC-11-P23-ACCEPTANCE-20261022.md). Production activation remains separate. The generation checkpoints below are historical; their earlier pending statements are not the current gate result.

## Source baseline and preservation

Branch: `codex/procurement-unit-review-core-principal`. Normal merge `dac5688` brings verified application baseline `5f199c84c95dab582f273d914652aa7d6a42a42e` into prior candidate `ce9ce58`. The merge needed no conflict resolution. Core stable account/employee alias resolution and the supplier separation controls remain, together with the additional original-unit-review DI, routes and truthful capability flag.

A byte comparison against that baseline covered all **137 tracked backend migration, Designer and ModelSnapshot files**; none changed. In particular, genuine P22 `20261022085116_P22CorePrincipalOwnership`, all earlier Procurement artifacts, HRM migrations/snapshot and Core migrations remain intact. The recurring commitment model is excluded; its migration must follow P23 separately.

## Expected model delta to review after genuine EF generation

| Owner table | Expected change |
| --- | --- |
| `invoice_unit_review_sources` | New append-only scoped original-invoice/mapping/issued-order/policy source, canonical maker principal, optional genuine worker, source/issued JSON and hashes, exact scoped ownership FKs/unique keys. |
| `invoice_unit_review_assignments` | New immutable source/Core-assignment principal pins with optional genuine Core-worker alias metadata for denial only. |
| `invoice_unit_review_outcomes` | New immutable terminal outcome; principal and optional worker; explicit evidence schema 3, JSON/hash and retained Core decision reference. |
| `invoice_unit_review_receipts` | New immutable actor-owned, idempotent command receipts with versioned source binding, request fingerprint and serialized response. |
| `operational_policy_identity_evidence` | Existing actor worker becomes nullable; CHECK admits identity schema 1 or 2 and the third policy key `INVOICE_UNIT_REVIEW`. Legacy schema 1 still requires its worker; no historical principal/worker or digest backfill. |
| `invoice_match_mapping_versions` | Add identity-schema discriminator with legacy default 1; make actor worker nullable, with version-specific principal/worker CHECK. New-version optional worker fields are genuine Core-link alias metadata, not active employment. Legacy v1 active-worker/digest semantics remain unchanged; no old JSON/hash rewriting. |
| `invoice_match_results` | Version-specific CHECK admits execution 2 with canonical Core principal/optional worker; retains execution 0/1 semantics. Review that EF emits only the expected constraint replacement here. |

Actual generated operations must match the owning model, including all new scoped keys, indexes and FKs. This table is an expected review scope, not generated SQL or a license to edit the snapshot manually.

## Exact generation procedure after the CPU/readiness handoff

1. Freeze a reviewed, clean source commit after completing the control/API issues below. Capture every existing migration/Designer/snapshot hash before generation. Do not import the recurring model. Run meaningful domain, host authorization and integration tests; prior 993 domain tests cannot be reused because this branch changes domain/application/infrastructure sources.
2. Use the already approved offline .NET 10 SDK image with the complete cached `/ef` tool and NuGet packages. Mount only this isolated branch at `/src`; use an unused protected evidence directory. Restore the owning project from the local cache first. Inside that SDK, run:

```bash
cd /src
I=backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Mightyfin.Erp.Procurement.Infrastructure.csproj
NUGET_PACKAGES=/root/.nuget/packages dotnet restore "$I" --source /root/.nuget/packages --disable-parallel -p:NuGetAudit=false
dotnet build "$I" --no-restore -c Release -m:1 -p:UseSharedCompilation=false
/ef/dotnet-ef migrations add P23CorePrincipalInvoiceUnitReview --project "$I" --startup-project "$I" --context ProcurementDbContext --configuration Release --no-build --output-dir Migrations
```

3. **Let EF assign the actual timestamp ID.** Discover the newly generated `*_P23CorePrincipalInvoiceUnitReview.cs` together with its Designer; record the real `[Migration]` ID and generated hashes. No timestamp or applied history is prescribed here. Compare the emitted operations and genuine snapshot delta against the table above; stop for unrelated changes. Preserve all earlier migration files byte-for-byte. Never hand-create Designer/snapshot content or insert migration history to simulate generation/application.
4. Review the new migration's downgrade protection before executing Down. Add a guard to the **new migration only**, grounded in actual owning audit/event formats. Then rebuild the infrastructure and run:

```bash
dotnet build "$I" --no-restore -c Release -m:1 -p:UseSharedCompilation=false
/ef/dotnet-ef migrations has-pending-model-changes --project "$I" --startup-project "$I" --context ProcurementDbContext --configuration Release --no-build
```

5. Generate forward, idempotent forward and reverse SQL using the real captured P23 ID and exact parent `20261022085116_P22CorePrincipalOwnership`. Keep those outputs protected with source hashes. Rehearse only on isolated PostgreSQL with retained source preservation: genuine Up, duplicate/invalid/scoped FK checks, no-op/replay/rollback behavior, unused Down/Up and retained-evidence Down veto. A failed first run is retained rather than overwritten or relabelled.
6. Capture exact live PostgreSQL CHECK/index/FK definitions and real migration history from that rehearsal into the owning readiness manifests. Empty manifests must continue to return unavailable. Generate P24 recurring only after reviewed P23 artifacts are committed.

## Concrete unresolved controls and contracts

- **No P23 build or runtime evidence exists yet.** Source tests and probes need compilation and execution after the release CPU handoff; the P22 test results do not establish P23 correctness.
- `ProcurementInvoiceUnitReviewSchemaReadiness.MigrationId`, `OperationalCheckDefinition` and its four new-table CHECK definitions and two altered mapping/execution CHECK definitions intentionally remain empty. This prevents activation. The operational-policy readiness branch must use the actual new identity CHECK only when the real P23 migration is installed, while retaining the existing P21/P22 path unchanged.
- A new source-only `sql/P23-principal-original-unit-review-down-guard.candidate.sql` now covers the expanded principal correction, including explicit new-version audit/outbox producers. It must be reviewed and embedded literally only after genuine generation, then pass actual used-row, audit-only and outbox-only downgrade denial rehearsals. The historical `P22-original-unit-review-down-guard.candidate.sql` remains unchanged and is insufficient for the expanded principal correction: it checks review tables/policy key but does not cover new identity-schema-2 policy evidence, mapping-schema-2 rows, or execution-version-2 results outside review. A new generated P23 Down must lock and veto all retained new-version rows **and their actual retained audit/outbox evidence**, including selectively removed primary rows, before any nullable worker becomes required or a discriminator is dropped. Never manufacture a worker/default UUID or downgrade old JSON/hash meaning.
- Current runtime default ACL grants Procurement tables broad `arwd`. Review readiness instead requires append-only review tables, audit/outbox and decision evidence, plus controlled Core case/stage/assignment writes in the same physical transaction. Migration DDL alone does not satisfy that authority contract. Design and rehearse the least-privilege owning role/adapter changes explicitly; do not weaken readiness or relabel the existing 83-table SQL grant check as P23 authority proof.
- Source detail/receipt contracts now expose `makerPrincipalId`, retained `identitySchemaVersion` 3 and nullable terminal `evidenceSchemaVersion` 3; receipt envelope remains 1. Pending receipts omit the terminal version; pending detail returns null. Producers bind metadata to the immutable owned source, and recovery/replay validates it. Agree the UI rollout against its explicit legacy compatibility path; no client inference or changed old evidence bytes is allowed.
- Principal-based named reviewer lookup exists; HR manager hierarchy is optional and must never become a prerequisite for an explicitly assigned active Core user. Department-grant lookup remains unavailable without an actual Core grant contract. Current scope, revocation, same-principal and known genuine shared-worker separation checks require negative tests.
- Invoice review does not supply the still-unavailable authenticated Finance historical-exposure owner. `invoiceQualifiedClaims`, invoice validation, AP posting and payment authority stay false where their independent prerequisites are absent. No positive Finance/bank evidence is introduced by these sources.
- Actual Core context insert provenance, pinned Core Documents access, source/mapping revisions, original quantities, same-transaction audit/approval/receipt effects, actor-owned recovery and permission revocation need a real finite-role PostgreSQL/API/browser vertical. Owner-only fixture success is not enough.

## Release evidence required

Record the generation source commit, actual migration ID, immutable pre/post migration inventory, generated SQL hashes, full domain/host test TRX, all full publication manifests, real finite-role privilege proof, private actual API/Docs/Approvals/browser results, used-downgrade veto and operational rollback/forward-recovery decision. Keep feature flags off until these gates pass. None is claimed complete by this alignment document.

## Genuine generation checkpoint — 22 October 2026

Reviewed source `a0ca734908395771ead3df8bcae1e16b2c5aefa7` compiled the owning infrastructure in the offline cached .NET 10 SDK with **0 errors and 8 nullable warnings**. Actual EF assigned migration **`20261022112145_P23CorePrincipalInvoiceUnitReview`**. The original emitted migration, Designer and snapshot are archived privately before any customization. Up, Designer and snapshot remain byte-identical to genuine generation; only the new Down prepends the reviewed literal retained-evidence lock/veto guard and removes the two automatically emitted Guid.Empty reverse-column defaults.

Independent source review confirmed the expected four tables, six restrictive scoped FKs, keys/indexes, two nullable actor workers, mapping discriminator default 1 and three successor CHECKs. The other 136 existing migration/Designer/snapshot artifacts are unchanged; the Procurement snapshot change is the genuine EF output. The observed migration ID is recorded here and in its emitted artifact, but runtime readiness constants remain empty until real PostgreSQL definition capture. No SQL has been applied. Full domain/host tests and complete publications follow under the exclusive SDK gate; this checkpoint is not their result.
