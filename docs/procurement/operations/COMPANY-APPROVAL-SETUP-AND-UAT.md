# Company approval setup and minimum-use acceptance

This guide uses the deployed Core directory and version2 Procurement request rules. It is a configuration/UAT checklist, not approval of any particular company policy. No actual people or limits are invented.

## Where responsibility lives

Core manages login users, role permissions, organisation/branches and access scope. A person can use Procurement with current assigned permission even without an HR employee. HR manages employment, job titles and reporting lines. An HR designation or department-manager pointer does not grant login permissions or spending authority.

Procurement owns purchasing policy, request/PO routes and finite spending authority. The Core workflow settings directory selects module, record and trigger and opens the owning module's settings; it does not duplicate that module's rules. Accounts approval/independent confirmation uses the shared Core approval engine. Finance will own AP, ledger, bank execution and reconciliation.

## Configuration order

1. In Core, confirm company, default currency and branches. Create/confirm the actual login users. Assign appropriate requester, Procurement, supervisor, executive and Accounts actions and record scope. Use separate independent people where policy requires; several titles held by one person must not create invented independent approvals.
2. Confirm organisation departments and membership. HR reporting data is used only by the legacy line-manager strategy. Version2 named-user/role steps do not require employee records. Department conditions use the request's existing department reference; a user without such a reference cannot match a department-specific condition. Do not interpret a role name as department data scope or automatically grant authority from an HR manager field.
3. Open [Core → Settings → Workflows](https://erp.amizopower.co.zm/core/workspace/settings/workflows). Choose Procurement → Purchase request → When submitted → Buying rules. Choose a version2 draft; existing legacy drafts are preserved rather than silently converted.
4. Configure ordered steps with actual Core users or roles. For ordinary requests, use the approved supervisor then executive chain. For supervisor requesters, add a higher-priority requester-role rule going directly to the actual executive, avoiding self-approval. Keep exactly one unrestricted lower-priority fallback. Use department/branch conditions where appropriate. Higher numeric priority wins; equal-priority overlapping routes are rejected. One with several eligible role members is ambiguous: select the actual person or use All only if all really must approve.
5. Configure the approved finite spending limits in Purchase Request approval settings. A routing binding alone grants neither permission nor amount authority. Check branch/currency/effective dates and current active users. The company-wide policy reviewer must have company-wide configuration scope. Preview, submit for review and use a different authorised user to publish. Confirm the effective date before testing.
6. Confirm the real budget/funding procedure and procurement method/category defaults. Do not mark funding as verified because a preview or UI save succeeded. Configure corresponding PO rules/authority before issuing orders.
7. Open Payment request → Before payment is recorded in the Core directory. Configure the existing invoice-backed payment policy with actual Accounts approver, external payment recorder and independent confirmer. Before/post-delivery timing must follow actual invoice/receipt policy. This flow records a payment performed outside the ERP; it executes no bank transfer or Finance posting. **A general PO advance without a supplier invoice is not yet implemented.** Do not create false invoices to bypass that gap.
8. Review real setup and the acceptance evidence below before a separately controlled workflow activation. Current flags remain unchanged by the technical release. No administrator should toggle flags to bypass unresolved policy or authority.

## Signed-in company UAT required before minimum launch

Record actor, branch/scope, actual business reference, result and evidence for each case. Use authorised test records; keep confidential evidence outside GitHub.

- Core-only user without an employee can create/read/edit the permitted supplier/item/request records and is denied unassigned routes and out-of-scope branches. All-company selection is a reading context; a branch-required transaction still needs its actual branch.
- Ordinary requester submits, supervisor approves, executive approves; Procurement can proceed only after the required completed chain. Supervisor-created request routes directly to the authorised executive. Self-approval, early executive action and revoked permissions/roles fail.
- Return → edit → resubmit pins a fresh approved policy/identity snapshot; retried commands do not duplicate approvals. Mixed line routes, changes and cancellations follow documented controls.
- Approved request → linked PO → required PO approval → issue → partial receipt/service acceptance → invoice → matching/exception resolution works, preserving original layout, versions, audit and source links.
- Independent Accounts approval → external payment record → independent confirmation works for a real permitted invoice; duplicate command/payment evidence is blocked. Payment visibility must not imply a bank transfer or ledger posting.
- Cross-branch/supplier access, inactive user, missing authority, insufficient funding, failed integration and reload/retry cases preserve data and report actionable errors.

Anonymous production smoke and mocked-API browser tests establish technical rendering/access behaviour; they do not replace these signed-in company cases or owner acceptance. Track remaining issues190/192/145 until their actual acceptance criteria are satisfied.
