# P24 recurring planning: Core principal ownership

Status: source prepared; no SDK, PostgreSQL, Playwright or migration-generation run for this correction.
Prepared integration merges root `d6602c3` normally into the earlier combined review/recurring source.
It preserves the genuine P22 migration, designer and snapshot; it is not a deployable final model.

## Contract

`RecurringPurchaseCommitment.OwnerPrincipalId` is a required genuine Core user UUID.
`OwnerWorkerId` is nullable historical HRMS metadata. `OwnerSubjectId` and `CreatedBy` retain canonical
original actor attribution. New records are server-stamped from resolved `scope.PrincipalId`, optional
`scope.WorkerId`, current company and authorized branch. DTOs accept no owner fields. Response detail
adds required `ownerPrincipalId`, changes `ownerWorkerId` to UUID-or-null, and keeps existing exact
amount, dates, status, number and version fields.

Reads/edits/close use stable principal ownership and current tenant/company/branch permissions.
Employee deactivation or link changes do not transfer records or revoke an active Core user's
schedule rights. Department-only scope remains explicitly unsupported because this plan has no
department field; it is denied, not silently broadened to company scope. Core permission/account
revocation, foreign ownership, selected branch and stale versions continue to deny access.

The owning context checks added identity against its real scope and persisted modified identity
against both EF original/current values. A detached DTO cannot forge original owner, employee,
branch or creator to transfer a record. Missing/foreign/stale versions raise concurrency errors.
Every accepted PUT, including unchanged values, advances exactly once; close advances once too.
Same-transaction before/after audit versions match the eventual persisted version. Physical deletion
is still prohibited. Planning creates no PO, bill, payable, reservation or payment.

## Expected genuine P24 delta

Generate from the actual reviewed P23 snapshot after that separate milestone is built and rehearsed.
Do not generate P23/P24 together from this combined model. No manual snapshot edits or fabricated
migration IDs. Recurring has never been deployed, so no guessed owner backfill is required.

The new table includes required UUID owner principal, nullable UUID worker, original subject, normal
lifecycle/scope fields, supplier/optional-contract references, exact amount precision, dates and
closure fields. Index by tenant/company/principal/next date; unique human number stays scoped.
The schedule CHECK requires nonzero principal, optional nonzero worker, canonical subject equal to
principal UUID text, supported frequency/date/amount/state and coherent closure fields. Inspect
actual generated NOT NULL/FK/index semantics and PostgreSQL CHECK output; reject unrelated deltas.

Add first-operation locked Down veto for any retained recurring row (including Closed) and meaningful
owned plan audit evidence before dropping the table. Fresh Up twice, used-P23 clone Up twice,
unused Down/Up and used-evidence Down denial must preserve whole prior data/catalog/history.
Retain all P01–P23 migration artifacts byte-for-byte and capture real schema readiness inventory.

## Application and concurrency preservation

Recurring create/update and contract supplier updates retain SERIALIZABLE transactions. Both open
and closed plans prevent reparenting their contract to another supplier. No approval, ledger or
contract engine is duplicated. Rehearse actual create versus contract-reparent overlap using the
existing independent row-lock barrier and observed PostgreSQL blocking; task scheduling alone is
not proof. Confirm downstream records before and after the race remain unchanged.

## Verification prepared, not executed

- Existing mapped recurring cases now seed real persisted Core buyer role permissions and branch
  assignments. Tests revoke the stored edit action; header roles do not grant/revoke real authority.
- New no-employee Core user CRUD covers default company currency, ignored forged client ownership,
  own reload, unchanged CAS successor, stale/foreign/cross-owner denial, current permission revocation,
  update/close/read-only history and no Finance effect.
- Employee deactivation preserves historical metadata and active system-user access; a later plan
  records null employee rather than inventing a replacement.
- Owning-store tests cover missing/forged principal, detached principal/worker/creator/branch forgery,
  stale CAS, foreign-company attachment and unchanged full record/audit/outbox snapshots.
- Actual PostgreSQL HTTP verifier explicitly uses the existing synthetic `SystemActor` Core account
  with no employee and its actual active role grants. It creates/reloads/updates/no-op/closes a plan,
  checks stale/cross-owner/foreign denials and independently reloads real owner/null employee.
- The verifier still requires genuine P23/P24 migration integration before current-model capability
  checks pass. Its existing P21/P22 baseline and CHECK inventory must be extended explicitly using
  captured catalog facts, not bypassed. No runtime success is claimed yet.

## UI hand-off

Use required `ownerPrincipalId` and nullable `ownerWorkerId`; authorize create/edit using current
principal and named permissions, never employee presence. Bind context and unknown-write journals
to principal/company/branch/auth epoch. Keep strict exact +1 receipts, no automatic retry after an
unknown result, real closed-history read-only state, shared components and simple owner wording.
The UI owner is preparing source separately; actual browser proof follows the real final API/schema.
