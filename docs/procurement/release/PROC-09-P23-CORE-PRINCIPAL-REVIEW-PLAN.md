# P23 — Core principal original-unit review

Status: source candidate; build, generated migration, private PostgreSQL rehearsal and browser gates NOT RUN.

## Baseline and ownership

Isolated branch `codex/procurement-unit-review-core-principal` normally merges genuine P22 root `d6602c3` with original review source `f4af7d4` (merge `e593ea0`). It deliberately excludes recurring model changes. Core owns accounts, grants, company/branch scope, Approvals and Documents. Procurement owns mapping, original-unit evidence and commercial rules.

## Current source alignment

Normal merge `dac5688` incorporates verified backend `5f199c84c95dab582f273d914652aa7d6a42a42e` into this isolated P23 branch. The merge was clean; its host integration retains both stable Core alias resolution and original-unit-review registration. All 137 tracked backend migration/designer/snapshot files from that baseline remain byte-identical. No SDK, EF generation, database or browser job ran for this alignment. See [P23 generation readiness](PROC-09-P23-GENERATION-READINESS.md) for the current schema scope, generation procedure and release blockers. Older PROC-11-P22 filenames below are historical source plans; P22 now means the released Core-principal ownership migration.

## Identity contracts

- Current active Core account and actual action grants authorize every new effect. Named reviewers need no employee profile. An employee link is optional metadata, never a substitute principal.
- Operational policy evidence version 1 remains worker-pinned with unchanged JSON/digest semantics. New version 2 captures canonical principal and optional genuine worker. A lineage cannot mix identity schema versions; publication still requires distinct principal and denies aliases when both known worker IDs match.
- Invoice mapping identity version 1 remains the existing snapshot shape; optional new properties are omitted. New version 2 binds canonical principal and optional worker into its hashed JSON. No historical backfill, guessed worker or empty UUID occurs.
- New original-unit review source and terminal evidence use identity version 3. Explicit version is mandatory; missing, changed and downgraded source evidence fails verification. Existing retained version 2 still requires genuine worker-pinned identities and original byte/hash semantics; it is not upgraded or relabelled.
- New comparison execution version 2 admits optional worker with canonical Core actor; retained execution 1 still requires worker. Original source quantities, documents, actual issued order, policy, scope, CAS and matching tolerance guards remain.
- Retained assignment metadata remains immutable after HR changes. Current actor must still be exactly the assigned Core principal with current grants/scope. Shared known employee aliases cannot make the maker an independent reviewer.

## Atomic effects and recovery

Core case/decision insertion remains observed from the actual enlisted Core context using the same physical connection and transaction. No fabricated Core row or completion receipt is accepted. Command recovery binds original key, principal, exact request fingerprint and source; changed employee metadata neither transfers command ownership nor rewrites history. Financial posting and unavailable exposure owners remain fail closed.

## Schema/release sequence

1. Build source and run targeted domain/API/security tests under root CPU gate.
2. Generate genuine P23 from this branch without recurring model. Review nullable optional workers, mapping identity version default 1 and new versioned CHECKs. Preserve all committed P01–P22 migration artifacts and HRM snapshot.
3. Rehearse Up, invalid inserts, no-op/replay concurrency, unused Down and used retained-evidence Down veto on isolated PostgreSQL. Down must veto retained principal-only evidence, never create fake employee IDs.
4. Capture exact PostgreSQL CHECK manifests for original-unit and operational-policy readiness; current empty original-unit manifest intentionally keeps live execution disabled.
5. Run actual no-employee Core-account API/Docs/Approvals and browser milestones before enabling. Existing pure fixture tests are not production authorization evidence.
6. Generate P24 recurring separately after P23 is reviewed and committed.

## Added source acceptance cases

Workerless named maker/reviewers complete actual Core domain stages; immutable original worker metadata can coexist with currently absent HR links; self/shared-worker aliases deny; new mapping actor tamper and invalid schema deny; old mapping JSON omits new fields; policy v2 workerless lifecycle and schema downgrade deny; source version omission/downgrade deny; model CHECKs require principal shape and nullable worker restrictions.

## Holds

No SDK, migration generation/application, runtime result, deployment or browser claim is made by this source change. Current company/branch grant semantics are reused. Department-grant reviewer lookup is unavailable until an explicit Core contract exists; HR home department is not inferred as a grant.
