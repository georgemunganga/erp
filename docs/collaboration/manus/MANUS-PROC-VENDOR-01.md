# MANUS-PROC-VENDOR-01 — complete vendor editor and supported workflows

**Assigned to:** Manus / Greenwebb. **State:** Ready to resume.  
**Starting code:** current published main containing ERP `3f71e41556064f96cf09b34e09fcb7d01971439b` and UI `4f7054a52e54d8fc68da0f0868dc8eeefc3f0007`.  
**Controlling scope:** [ERP handoff](../../../MANUS_HANDOFF.md). Existing 28-section PROC-01 and PROC-UX-05 specifications remain authoritative.

Complete the whole milestone. Each checkpoint has a concrete deliverable; a form screenshot alone does not finish a workflow. Preserve the merged vendor detail layout and administrator view-only behavior. Do not broaden into backend migrations, shared infrastructure, settings or other modules.

## Ordered checkpoints

| Checkpoint | Deliverable | Exit evidence |
| --- | --- | --- |
| V1 — Contract and field audit | Map every original editor field to real DTO, owner, validation, permissions and supported read/write path. Record unsupported fields explicitly. | ERP field/API matrix and gap register; exact starting commits. |
| V2 — Original editor | Reuse primary-contact/company header and seven original tabs: Other Details, Address, Contact Persons, Bank Details, Custom Fields, Reporting Tags, Remarks. Keep ERP controls/branding and restored detail view. | Desktop/mobile screenshots, keyboard/tab checks, no duplicate frontend or browser-local save fallback. |
| V3 — Supported CRUD | Complete the operation register below using actual APIs and optimistic versions. Preserve edits on failure and never turn a failed edit load into create. | Browser create/read/refresh/edit/withdraw and detail maintenance; exact request/response assertions. |
| V4 — Business lifecycle | Complete submission, review decisions, qualification review, hold and reactivation according to server action flags and existing rules. Preserve actor/reason/version requirements. | Positive and denied workflow cases, state visibility after reload, no self-authorized activation. |
| V5 — Recovery and isolation | Company/account/record changes, late reads/mutations, duplicate clicks, lost save response, stale version, permission loss and partial detail loading. | Synthetic delayed/failure cases; no writes under stale scope or automatic duplicate retry. |
| V6 — Verification and handover | TypeScript, focused units, live-mode build and Playwright; ERP evidence PR linked to actual UI PR. Update milestone log with every remaining gap. | Commands/results/commits, screenshots and failed traces; distinguish mocked HTTP from actual API/database evidence. |

## CRUD and workflow operation register

| Record / workflow | Operations to complete | Required safeguards |
| --- | --- | --- |
| Vendor directory | List, search, supported filters, paging, empty/error/denied states; open actual saved IDs | Company scope; no demo fallback; create/import controls only with supported authorization/contracts. |
| Vendor draft | Create, read, edit, withdraw with reason | Core company currency; server validation; ID/version consistency; no hard deletion or fake save success. |
| Contact persons | Add, read, edit, remove with reason | Parent version, required contact fields, primary-contact rules; preserve fields the form does not own. |
| Addresses/sites | Add, read, edit, remove with reason | Parent version, address type/country/primary rules; safe edits and no cross-vendor reuse. |
| Qualifications | Add, read, edit, remove; review where authorized | Date validation, parent version, review status/action guards; no invented upload completion. |
| Supplier submission | Draft/returned record to review; return corrections and resubmit | Required category/contact/business justification from actual contracts; submission never means approved. |
| Review decision | Return for information, reject, approve | Server permissions/action flags, required reasons and duplicate review; segregation rules stay server-owned. |
| Supplier hold/reactivation | Read open obligations, place on hold, resolve and reactivate where permitted | Required checks/reasons and current version; no silent supplier substitution or bypass. |
| Detail tabs and toolbar | Preserve directory, overview, Comments, Transactions, Mails, Statement and original toolbar structure | Supported actions use real contracts; unsupported Finance/bank/email/portal/clone/delete paths are explicitly unavailable. |

## Completion rules

- Mark each operation **Verified**, **Gap**, or **N/A with reason**. For a Gap record the exact missing contract/owner, affected scenario and required follow-up; it is not completed functionality.
- No production test writes, identity/role changes, migration application, deployments or capability activation. Use synthetic fixtures or a coordinator-assigned disposable backend.
- Browser tests: one worker, no automatic retries; preserve failures before another run overwrites their output. Include editor/detail desktop/mobile, validation, denied/admin, version conflicts, delayed scope, duplicate/lost-response cases and every supported lifecycle above.
- Mocked HTTP verifies the UI contract only. Only an assigned actual API/database environment can prove persistence; label evidence honestly.
- Submit focused new PRs against current main. Do not merge the old development branch ancestry. Record actual starting and final commits in the ERP log.
- The coordinator reviews and merges accepted work before publishing the next baseline. `MANUS-PROC-SETTINGS-02` remains queued until then.
