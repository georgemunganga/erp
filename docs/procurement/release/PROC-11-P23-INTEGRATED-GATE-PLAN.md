# P23 principal review and Core document retention — integrated gate plan

## Candidate and evidence boundary

Normal merge `5371d9deca544208944ec5a184a30eab48315990` combines P23 source `4f8216b` with independently reviewed Core document-use source `c8d4f1b`, without conflicts. No model, migration, Designer or snapshot changes are intended. The 139 tracked migration artifacts must remain byte-identical to `4f8216b` before and after every job.

This candidate has **not been built or tested**. The retained 1194-domain-test pass belongs to `22a10d2`, before the document integration; the interrupted run did not execute host tests, publications or final EF checks. The original failed run and interruption evidence remain immutable. Neither result certifies this candidate.

## Serialized SDK gate after CPU handoff

1. Freeze the exact candidate HEAD and full backend source hashes in a fresh private evidence directory; capture disk space and the 139-artifact inventory. Use cached SDK image `sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510`, network disabled, one CPU, 1200 MB memory, existing NuGet cache and complete cached EF tool. No parallel runtime/browser jobs. Stop on the first failure; preserve its actual TRX/log and source identity.
2. Offline restore the following projects, using `--source /root/.nuget/packages --disable-parallel -p:NuGetAudit=false`:
   - Procurement.Tests
   - Core.Documents.Tests (Core retirement and retained-reader source changed)
   - Core.Approvals.Tests (shared transaction composition changed)
   - Hrm.Tests
   - ProcurementCrudHttpProbe, FullHostCompatibilityProbe and ApprovalHttpPostgresProbe.
3. Run all Procurement domain tests, all Core Documents tests and all Core Approvals tests. Run Hrm.Tests with the retained filter `FullyQualifiedName~Procurement|FullyQualifiedName~HrmHandwrittenModelContractTests|FullyQualifiedName~Loan|FullyQualifiedName~InvoiceUnitReview`. Use Release, `--no-restore -m:1 -p:UseSharedCompilation=false`, separate actual TRX files and counters. Do not assume old totals; the new document-access tests must be discovered. Keep inherited warnings separate from new warnings.
4. Publish the complete Hrm.Api, ProcurementCrudHttpProbe, FullHostCompatibilityProbe and ApprovalHttpPostgresProbe to four new evidence directories. Record each relative file's byte length and SHA-256, bound to the actual source commit; no DLL overlays or modification of earlier publications.
5. Build Procurement.Infrastructure and run genuine `dotnet-ef migrations has-pending-model-changes` with its own startup project/context. Also verify the unchanged Core Documents model with its genuine context/design-time setup. Any pending model is a failure, not a reason to hand-edit the snapshot.
6. Generate scripts from actual `20261022085116_P22CorePrincipalOwnership` to actual `20261022112145_P23CorePrincipalInvoiceUnitReview`, its unused-only reverse, and the forward idempotent script. Compare genuine migration artifacts before/after; capture script hashes. Do not generate another migration ID or apply SQL in this SDK job.
7. Require unchanged source hashes, unchanged migration artifacts, successful actual tests/publications/model checks, and a terminal non-OOM container inspection. A partial/interrupted run remains partial.

## Separate private PostgreSQL and workflow gate

Only after the SDK gate and a separate runtime slot: use a new bounded disposable private database and distinct bootstrap/runtime identities. Apply genuine owner migrations, preserve prior-data proofs and test unused Down/retained-evidence Down veto. Capture exact PostgreSQL P23 CHECK/FK/index definitions and actual migration history before filling the currently empty readiness pins; any pin update requires a fresh source-bound publication/gate.

Run the newly published `ApprovalHttpPostgresProbe.dll --unit-reviews` with `PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION` and `PROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION` supplied privately. The database name must satisfy the tool's `proc_approval_http_` restriction. It must use actual finite execution-role grants, real Core identity/scope checks, real owner tables and actual mapped HTTP handlers. Disposable fixture grants are not production provisioning.

Required assertions include real Open and exact pin reload/replay; independent workerless-principal decision; wrong/current revoked grants and scope; exact document owner/hash/scan/set mismatch denial; owning source save without pins; rollback of document pins/Core case/source/receipt together; shared-acquisition versus exclusive-retirement ordering; committed retained-pin retirement denial; receipt recovery after unknown outcome. Extend any missing authored acceptance case before claiming the full workflow passes.

Real upload → real ClamD scan → Available → mapping → Open → purpose download → independent decision is a distinct operational acceptance path; synthetic pre-scanned fixtures cannot certify the scanner or mounts. Current `--unit-reviews` is standalone, not a browser serve mode. A later browser case needs an explicit reviewed bounded fixture/transport contract; do not claim existing 13 P22 browser cases cover P23.

Core remains the document/approval owner. Review completion grants no Finance/AP/payment authority; unknown Finance historical exposure stays blocked. Flags and retirement grants remain default off until the applicable gates and deployment review pass. No production writes are authorized by this plan.
