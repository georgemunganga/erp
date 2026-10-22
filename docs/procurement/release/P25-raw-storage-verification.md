# P25 raw storage verification integration

Status: **source only; not compiled or executed**. This is a tool-only addition to
`FullHostCompatibilityProbe`, based on published checkpoint `b871caff`. It changes
no application logic, model, migration, provider readiness or production flags.

## Shared fixture contract

The actual HTTP fixture is owned by the `PrincipalApprovalHttpVerification`
partial class. It supplies its real current Core/account, permission and scope
records; this file creates no workers, approvals, funding or financial records.

The first source slice can call:

```csharp
await VerifyRequestRawStorageAsync(owner, actualRequestId, actualTenant,
    actualCompany, actualCurrentCasVersion);
```

This runs four original-request CHECK negatives against one existing workerless
principal request. It requires no missing/default checker, submission or assignment
IDs. It is appropriate for the initial real create/submit plus unavailable-Finance
negative milestone.

The complete fixture can later call:

```csharp
await VerifyRawStorageAsync(owner, new PrincipalApprovalStorageGraph(
    requestId, checkerSnapshotId, authorityCreatedEvidenceId, submissionId,
    assignmentEvidenceId, tenantId, legalEntityId,
    retainedSubmittedBusinessVersion, currentRequestCasVersion));
```

All five IDs must exist in the same scoped real graph. The retained submitted
business version and current request CAS version are separate values. The owning
connection is passed separately and must never be serialized into this graph.

This source depends on the HTTP partial's shared
`PrincipalFootprintAsync(NpgsqlConnection)` implementation. Merge that coherent
fixture checkpoint before compilation; do not add a second graph factory or
replace its footprint with a constant/pass-through stub.

## Raw PostgreSQL checks

The complete method contains **35** independently rolled-back faults: 34 actual
named `23514` CHECK failures across the eight affected constraints and one actual
`23502` required-column failure. These are proposed counts until execution.

Before each fault, PostgreSQL must accept a scoped no-op UPDATE of exactly one
real baseline row. An empty UPDATE is a failure. The single mutation then must
raise the exact expected SQLSTATE and constraint or column name. Finally, the
transaction rolls back and the shared whole-store footprint must equal its prior
value. A wrong error, unexpected success or changed footprint stops the run.

The owner connection is independently restricted to the fixed private PG system,
Unix sockets, the `proc_p25_http_` database prefix, and actual postgres session/user.
These are owner-storage checks, not finite-role or authorization evidence.

Important boundaries:

- No random GUID is inserted as a positive worker identity. Current rows must be
  genuinely workerless; zero aliases occur only as deliberately invalid values.
- The alternate principal for mismatch tests is the actual captured independent
  checker, not an invented account.
- Missing actor-principal metadata in a NOT NULL column is reported as `23502`,
  not falsely credited to a CHECK.
- A supported legacy assignment version can satisfy database shape checks while
  its current envelope is invalid. No false `23514` downgrade test is invented;
  the application/owner digest and envelope guards cover that separate boundary.
- Legacy positive compatibility is not certified by mutating a current row.

## Genuine current-graph downgrade refusal

The full method also requires `ERP_PRINCIPAL_P25_DOWN_SQL` to point to a read-only
mounted copy of the actual emitted P25 Down SQL. It checks the exact SHA-256
`1b298d0a0fb8ccd42ab1f0b93260ef02d89896c1a21b3209728286e75492ba1c`.

All original statements remain in order. A separate `P0001` sentinel before the
original COMMIT forces rollback even if the owning veto unexpectedly permits
DDL. Only the original `55000` and exact owning message can satisfy the test.
Explicit rollback and complete footprint equality follow every attempt.

This is **dependent current-graph** refusal: original request, checker, submission,
assignment, audit and event evidence may all trigger the veto. It proves neither
five independent row-family branches nor that any one isolated branch suffices.
The result explicitly reports zero independently isolated family branches.

Storage-only minimal family fixtures may be added separately where safe, with
their CHECK/FK validity and limits explicit. They must never be relabelled as a
real current-domain approval or Finance outcome.

## Evidence and release limits

`PrincipalRawStorageVerification` is cleared before a run and populated only after
all its requested checks finish. The caller may serialize it into protected
acceptance evidence. No connection strings or row payloads are logged by this file.

No SQL, SDK/build, Docker, runtime or production operation was performed while
authoring this source. Complete compilation, current mapped HTTP fixture execution,
raw cases, owning source preservation and independent review remain pending.
Finance owner remains unavailable; these checks enable no ledger, payment,
funding, approval provider or production workflow.
