# PROC-07 Contracts, Frameworks and Recurring Commitments

**Status:** Draft for business review · **Version:** 0.1 · **Hierarchy:** Procurement → Contracts → Commercial Agreement Management → Operations · **Framework:** [ERP 28-section standard](../hrm/feature-specifications/ERP_Feature_Specification_Framework_Enterprise_Integration_Updated.docx) · **Module:** [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md)

**Implementation note (18 October 2026):** The shared ERP host now has an own-record contract draft API under `/api/procurement/v1/contracts`. It can create, revise and cancel a draft against an available supplier. The first contract version is stored with the header. No approval, signature, activation, drawdown or recurring payable is enabled by this draft API.

## 1. Feature Identification

`PROC-07`, advanced phase unless contract buying is moved earlier. Business owner: contract/procurement lead; legal and Finance reviewers; product/data/technical owners to name. Operations: `O1` draft/review, `O2` approve/sign/activate, `O3` release/track ceiling, `O4` amend, `O5` renew/expire/terminate, `O6` monitor recurring commitment.

## 2. Purpose and Business Outcome

Retain approved supplier terms and prevent purchases or renewals outside them. A framework agreement sets reusable terms; a recurring commitment plans periodic spend; a blanket PO is a separate ordering instrument. Success is traceable utilization and timely renewal decisions.

## 3. Scope and Exclusions

Includes metadata, signed versions, rate schedules, obligations, value ceiling, releases, amendments, renewal alerts and recurring schedule reference. Legal drafting advice, e-sign provider, blanket PO issue, AP recurring bill and payment execution are separate.

## 4. Parent and Related Features

Parent Contracts; consumes PROC-06 award or approved direct route and PROC-01 supplier; PROC-02 lines may route to contract release, PROC-08 PO consumes terms. Finance owns budget/AP; shared documents store signed versions.

## 5. Actors and Participants

Contract owner, buyer, supplier contact, legal reviewer, Finance/budget owner, signing authority, requester, workflow service, auditor and renewal operator.

## 6. Entry Points and Triggers

Sourcing award, proposed direct agreement, renewal notice date, utilization threshold, rate change, supplier suspension, amendment request, recurring cycle and contract expiry.

## 7. Preconditions

Eligible supplier, valid entity/category, approved source/authority, legal review where required, signed document or authorized substitute, effective dates and ceiling/rate basis. Missing signature or expired supplier blocks activation/use per policy.

## 8. Workflow

`O1`: draft agreement with terms, obligations, rate cards and ceiling; review. `O2`: approve and record signed version, activate at effective date. `O3`: PR/PO release checks eligible item/date/remaining ceiling and allocates utilization once. `O4`: material terms create amendment/version and reapproval/signature. `O5`: notify renewal owner, decide extend/renegotiate/expire/terminate, preserve prior terms. `O6`: recurring schedule raises forecast/task; Finance handles actual recurring bill.

## 9. Status and State Transitions

Draft → Review → Approved → Awaiting Signature → Active → Expiring → Renewed/Expired/Terminated/Closed; amendment is Draft Amendment → Approved/Signed → Active new version. Each transition has actor, terms/evidence, effective instant, SLA and escalation. Expired terms cannot be used for new release unless explicit grace policy exists.

## 10. Permissions and Data Scope

Contract owner drafts within entity/category; legal/Finance/signing authority review their fields; buyer sees authorized terms for release; supplier sees only its executed shared version. Requester cannot alter terms. Sensitive rates and documents are field scoped.

## 11. Business Rules

`BR-PROC-CON-001` Active signed/approved version required for release; `002` release cannot exceed ceiling/period/rate scope without amendment or exception; `003` new terms effective only after required approval/signature; `004` amendment never rewrites historic PO terms; `005` recurring schedule is not AP liability; `006` supplier suspension holds new releases for review.

## 12. Data Fields and Ownership

Procurement owns contract ID/type/version, supplier/entity, owner, dates, rate schedule, ceiling/currency, linked award/PO/release, obligations, renewal, amendment and utilization. Shared document service owns file bytes; Finance owns commitments and recurring bills. PO owns blanket releases; contract stores references.

## 13. Screens and Data Views

Contract register, draft/review, signed-version detail, rate/ceiling utilization, linked PO releases, obligations, amendment comparison, renewal calendar and exceptions.

## 14. User Experience and Human Behaviour

Show effective terms and remaining ceiling at the point of buying. Warn before using expiring terms and distinguish contract ceiling, Finance budget and blanket PO remaining value. Avoid presenting an unsigned approval as an executed agreement.

## 15. Notifications and Communications

Review/signature tasks, renewal and expiry lead-time alerts, utilization threshold, obligation due, amendment approval and supplier suspension. Recipients and channels are configured; confidential terms remain behind scoped links.

## 16. Documents and Attachments

Draft, signed agreement, exhibits, rate cards, amendments, supplier acknowledgment and legal review evidence are scanned, versioned, classified, retained and linked to each effective contract version.

## 17. Exceptions and Edge Cases

Multi-entity agreement, currency escalation clause, partial award, retroactive signed date, price schedule gap, ceiling exhaustion, renewal after expiry, supplier merger/suspension, disputed obligation and open PO after termination.

## 18. Integrations, APIs, Events and Sandbox

Consume supplier, award, document/signature, Finance commitment and PO release refs. Publish contract activated/amended/expiring/terminated facts after commit with tenant/entity, contract/version, effective time, correlation/idempotency. Sandbox tests ceiling races and late signed document.

## 19. Audit, Security and Privacy

Append review, signature reference, rate/ceiling change, release, renewal and termination with actor/reason. Restrict contract terms by entity/category/supplier and log export; prior signed version immutable.

## 20. Reports, Dashboards and Metrics

Active/expiring contracts, utilization, off-contract purchasing, renewal timeliness, obligation breaches and recurring forecast. Define committed versus invoiced versus paid values and currency conversion.

## 21. Configuration and Localization

Contract types, approval/signature gates, notice period, ceiling/materiality, recurrence, rate/UOM/currency, locale and document template are effective-dated; legal clauses remain reviewed documents, not hardcoded text.

## 22. Non-Functional Requirements

Concurrent releases cannot exceed ceiling. Signed files have integrity/version evidence. Alerts, external signature callbacks and utilization reconciliation are observable. Targets await volume and legal retention policy.

## 23. Postconditions

Active version has signed/approved evidence and usable rates/ceiling; every release references exact version and reduces available capacity; expiry/termination blocks new release while history persists.

## 24. Failure and Recovery

Signature outage leaves Awaiting Signature. Unknown release result is reconciled by idempotency key before retry; failed Finance commitment holds release. Erroneous published terms are superseded by approved amendment, not edited.

## 25. Acceptance Criteria

Given unsigned or expired contract, new release blocks. Given remaining ceiling smaller than requested release, allocation blocks or routes exception. Given signed amendment, future PO uses new version and old PO retains old terms. Given recurring date, task/forecast appears without creating an AP bill.

## 26. Test Scenarios

Test contract from award, multi-rate schedule, ceiling boundary/concurrency, amendment, renewal/expiry, supplier suspension, multi-entity access, signature callback replay and open PO after termination.

## 27. Ownership and Dependencies

Contract owner governs terms, Legal signs off clauses, Finance owns financial commitment, Procurement owns agreement metadata. Depends on PROC-01/03/06, document/signature service; PROC-08 consumes rates and releases.

## 28. Open Decisions and Assumptions

Confirm contract versus Finance ownership, signature requirements, ceiling and utilization basis, multi-entity terms, blanket PO relationship, recurrence, renewals and retention. Owners: Procurement, Legal and Finance; resolve before implementation.
