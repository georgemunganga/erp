# Procurement PO and Approval Settings Integration

Status: source merge only; combined build/tests, restricted-runtime HTTP and browser gates remain open. No migration, runtime activation or production write.

## Inputs and isolation

- PO source checkpoint: `92abe51`.
- Settings source checkpoint: `af15adf` (settings `722d6e9`, shared guards `12566ec`, documentation `f6d4472`, fixture corrections `f84cd1f`/`9a45836`, actual settings probe `af15adf`).
- Integration occurs on a separate branch/worktree; the root's frozen PO verification checkout is unchanged.
- Related milestone: [module-connect#5](https://github.com/georgemunganga/module-connect/issues/5).

## Conflict resolutions

| Conflict | Resolution |
|---|---|
| Authority list | Retain named settings views, exact maximum-amount strings, current allowed actions and complete-family guards. Add explicit typed action filtering before count/page. |
| Capability HTTP fixture tenant claim | Retain the PO checkpoint's already-correct tenant claim; do not duplicate claims. |
| Probe command line | Preserve default PR verification, `--serve`, `--orders` and `--settings`. Modes are mutually exclusive, including duplicate arguments. Preserve all existing PO read/replay/branch-fence calls. |

No PO execution source is replaced wholesale. Source command validation, canonical maker/checker checks, Core session fence and exact execution money DTOs remain unchanged. Authority family loads use each grant's actual principal, permission, action and reference, including rows outside current display scope; UI views never substitute a filtered family for publication validation.

## Exact typed authority contracts

| Transaction | Action | Permission | Retained amount basis |
|---|---|---|---|
| PR | `PurchaseRequest.Approve` | `procurement-approval-decide` | `EstimatedLineBase` |
| PO | `PurchaseOrder.Approve` | `procurement-po-approve` | `ApprovedLineBase` |

PO approval limits currently apply to approved line base. Finance separately confirms commercial **gross**, including tax. Do not infer that a base-value approval limit covers tax/freight. Whether a future policy should approve gross is a business/configuration decision; changing it requires explicit compatible versioned policy/grant/evidence work, not rewriting existing pins.

## Settings UI contract

- `GET /configuration/spending-authorities` defaults to the PR contract. `?action=PurchaseOrder.Approve` explicitly selects PO grants. Both action and corresponding permission are filtered before total/count/page.
- The list always returns `createAction`, `createAmountBasis`, `canCreate`, current actor and scope metadata, including empty/read-only pages. The UI locks new draft kind to the selected exact contract.
- `GET /configuration/spending-authorities/lookups/{kind}` accepts the same action selector. People candidates require that contract's current approval permission; a PR permission does not substitute for PO or vice versa. Existing tenant/company/branch bounds, canonical-account checks and bounded lookup pagination remain.
- Direct detail and commands preserve each stored grant's actual tuple. Unknown/mismatched tuples must remain read-only in the UI. New PO drafts explicitly send `action=PurchaseOrder.Approve`; PR default behavior remains.
- `MaximumAmount` uses the settings converter for its existing 18,4 **authority limit** contract. It is not applied to calculated eight-decimal gross or to PO execution projections. Legacy numeric limit input remains accepted where exact; output remains a string.
- Shared nonmutating `AssertCan*` guards drive settings allowed actions and actual commands; current API identity/reference/independence checks still apply at execution.

## Source-authored verification and remaining gates

Two additional real-route tests cover default PR vs explicit PO same-reference families and action-specific named-person eligibility. The actual PostgreSQL settings probe also exercises separate typed PR/PO drafts, exact maximum strings and correct list contracts; the PO probe calls that same bounded settings check before its existing commercial workflow. These additions have not been executed in this worktree.

The inherited settings evidence remains historical evidence for its original source. This integration does not claim the combination passes. Required next gates: full Procurement tests, affected host HTTP tests, all probe modes, restricted PostgreSQL runtime queries/commands, P24 compatibility/model checks in the root sequence, and UI/Playwright against the combined API. No model or migration file is changed by this integration, so the independently generated PO migration can be applied additively after review.
