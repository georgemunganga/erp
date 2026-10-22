# P25 current account, scope and authority negatives

Status: source only, uncompiled and unexecuted. Based on reviewed root `33419347`.
Only two new tool partial files and this note are owned by this checkpoint. No
shared fixture, entry point, application, migrations or activation flags change.

## Hook 1: before the main terminal decision

In `VerifyPrivateManualApprovalAsync`, after the real assignment detail has been
read and the exact pending decision body/UUID are available, call:

```csharp
await VerifyCurrentAccountAndScopeDenialsAsync(fixture, ownerConnection, client,
    send, caseId, assignmentId, decisionBody, decideKey, countExtraHttp, result);
```

The root owns `countExtraHttp`: pass a callback that increments the existing
`actualHttpRequests` counter. This avoids changing the shared `PrincipalSend`
contract. Four account commands use `send` and are counted normally; two selected
scope commands use the real client with explicit foreign headers and invoke the
callback exactly once after each actual response. Do not add six again manually.

The private owning fixture changes only the approver's one exact Core field for
each of inactive, locked, archived and foreign tenant. Parameterized SQL includes
actual account ID, original tenant and original field value. `finally` reads the
actual value, restores only the exact temporary fault, refuses a third value and
requires the complete original footprint. Timestamp values use database precision
and typed DateTimeOffset reads; no account or worker is replaced.

Two actual foreign Core fixture records are constructed once and retained: an
unassigned legal entity and an unassigned branch in the original company. Actual
POSTs with the pending command's body/key and those selected scopes must be403
without business/audit/receipt effects. They are not new permission grants. These
constructor rows belong to the new private database only.

The six-case result is written only after all checks complete. The unchanged
subsequent main decision is the positive confirmation that restoration did not
revoke the legitimate principal's access.

## Hook 2: last, after accepted main graph and raw checks

Call after the first graph's owner reload, acknowledgement, replay and35 raw
checks, while its private host is still alive:

```csharp
await VerifyAuthorityRevocationDenialAsync(fixture, ownerConnection, client, send,
    requestId, openKey, opened, grantId, result);
```

This creates a **second genuine pending request** through existing API create,
submit, labelled manual checker, open and detail. It reuses the published policy,
independently activated category and exact finite authority; no policy or source
status is seeded. It verifies the new retained assignment actually references the
same grant before revocation.

The independent config reviewer temporarily receives exactly
`procurement-authority-revoke` on its actual existing custom role. This is a
private fixture prerequisite to exercise the real public command. The exact
original role bytes are restored in `finally`, without overwriting a concurrent
third permission value. This role restoration does not erase legitimate authority
API writes or attempt to claim the whole database reverted to before revocation.

Actual GET grant plus POST revoke must show Active -> Revoked with returned CAS.
The original maker's original Open GET acknowledgement must still return its
exact retained result. A new decision on the second pending case must then return
409 with exact `ApprovalAssigneeAuthorityUnavailable`: the real
`ValidateAssignmentAsync -> ResolveAsync` path finds no eligible active authority.
An unrelated state/permission conflict does not pass this test. Complete owning
footprint equality is required around that denial.

Fresh owner reads require the grant remains Revoked, the second source remains
Submitted/Pending, and only its one Open receipt exists, with no Core decision or
Procurement outcome. The extra source/pin/assignment/Core case and legitimate
revocation history remain as evidence; they are not deleted or reset. Ten API
calls in this hook go through `send` and are automatically counted.

## Acceptance limits

These16 new API calls are proposed until actual execution. Preserve each first
failure. No API, source effect, schema or guard is weakened. Actual aliases, maker
offboarding, legacy worker execution, identity provider/browser recovery and the
remaining finite-privilege matrix stay separate gates. The manual checker remains
explicit private recorded evidence, with external Finance authority false. The
cohort does not activate a production feature or supply Finance budgets/AP/ledger.
