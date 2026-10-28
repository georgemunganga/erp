# PROC-09A / M6A — Invoice history baseline review

## 1. Feature Identification

M6A captures a Procurement-owned invoice history checkpoint and obtains an independent Core review before that checkpoint can be used by later matching work.

## 2. Purpose & Business Outcome

The record explains which retained invoice, matching, claim, credit, handoff and manual-payment facts were classified at a specific instant. Approval attests the captured history; it does not qualify a new invoice or create available payable capacity.

## 3. Scope & Exclusions

This child feature covers exact inventory capture, one owner baseline, Core review, immutable command receipts and scoped recovery. It excludes qualified matching claims, Finance posting/payment, document review, supplier delivery and production activation.

## 4. Parent & Related Features

It is the owner prerequisite for [PROC-09](PROC-09-M6-PRINCIPAL-INVOICE-MATCHING.md). It reads retained Procurement invoice history and uses current Core identity, approvals and audit. It does not replace the invoice master or reinterpret old matching results.

## 5. Actors & Participants

A current Core principal opens the baseline; a distinct named Core principal reviews it. An employee or worker link is optional. Procurement owns the capture and classification; Core owns the case, assignment and decision.

## 6. Entry Points & Triggers

The existing Procurement API provides baseline Open, detail, assigned review, Decide and original-command-key GET routes. A reviewed policy and explicit current Core permissions precede Open.

## 7. Preconditions

The policy is published and effective. Physical schema, exact finite database grants, complete inventory scope and current tenant/company context are required. An unknown historical row blocks capture; an empty live claim table alone is not evidence of complete history.

## 8. Workflow

Open reads the bounded retained history, records each classified source row and its digest, opens a Core case and saves a keyed receipt in one transaction. The independent reviewer reads that captured set and records one keyed decision. Original-key GET recovers the committed response after a lost acknowledgement.

## 9. Status / State Transitions

The accepted private path is Open201 to Pending Core case, then independent Decide200 to Approved. A historical result remains readable after the write window closes. Return or Reject paths are separate and are not claimed by this checkpoint.

## 10. Permissions & Data Scope

Open, read and review require separate explicit Core actions. Company All permits a scoped historical receipt read but denies a new Open write. Recovery checks the original actor, operation and key; another actor cannot read that receipt.

## 11. Business Rules

Inventory identity, source version, classification and hashes must match the captured row set. The business capture instant can precede the shared audit save timestamp; every saved inventory row must share the same audited batch time. This exact condition supports historical recovery without weakening source digests.

## 12. Data Fields & Ownership

Procurement stores the baseline, classified inventory rows and Open/Decide receipts. The baseline binds a catalog manifest, inventory-set SHA-256 and Core case. Core owns stage, assignment, decision and completion key. The command receipt retains actor, request digest, response bytes/hash and case/decision links.

## 13. Screens & Data Views

This checkpoint proves the real-cookie API and persisted owner graph, not an original invoice-history UI journey. A future screen must show the captured classification, reviewer assignment, current state and historical result without presenting the baseline as a qualified invoice claim.

## 14. User Experience / Human Behaviour

The maker can recover a committed Open by its original key. The reviewer sees the assigned Pending case and approves independently. A denied All write and a foreign-key read provide explicit scope feedback.

## 15. Notifications & Communications

Owner audit records the capture and approval. No supplier or Finance message is established by this bounded test.

## 16. Documents & Attachments

M6A classifies retained references; it does not validate original invoice document versions or complete the independent unit-review path required by qualified M6 matching.

## 17. Exceptions & Edge Cases

Unknown or changed history, mismatched source hashes, wrong actor, stale case version, wrong company/tenant or incomplete physical grants fail closed. A keyed Open that committed despite a lost or failed readback must be recovered with that key; no fresh Open key is inferred.

## 18. Integrations / APIs / Events / Sandbox

Private acceptance used real Core password-session cookies, a no-network PostgreSQL clone, finite Core/Record/Execution roles and an HTTPS pass-through proxy with no identity injection or mocked business response. No production business API call occurred.

## 19. Audit / Security / Privacy

The private case records one Open and one independent Decide with exact actor, receipt and audit links. Raw credentials, cookies, inventory payloads and private database snapshots remain outside the repository. The docs reference protected receipt hashes only.

## 20. Reports / Dashboards / Metrics

The accepted example captured 21 classified inventory rows, one Core case, one reviewer decision and two command receipts. These are private fixture counts, not production invoice volume or qualified exposure.

## 21. Configuration & Localization

The synthetic `INVOICE_OWNER_BASELINE_REVIEW` policy was created, submitted and published through normal cookies. Its private rule is not an approved production threshold or company policy. Tenant/company scope comes from Core; no employee prerequisite is added.

## 22. Non-Functional Requirements

Open and Decide use durable original command keys and exact response recovery. The three owner tables have physical readiness checks and finite grants. The guarded migration Down vetoes retained owner evidence before destructive DDL.

## 23. Postconditions

The private baseline and its 21 rows remain unchanged after approval. The Core case, stage and assignment are Approved; one reviewer decision and Decide200 receipt are linked by the completion key. `qualifiedExposure=false` and Finance posting remains unavailable.

## 24. Failure & Recovery

The first private Open returned 503 with no baseline when twelve old inventory tables lacked Execution SELECT. Exact private SELECT grants were added. The same original Open key then committed 201 but readback returned 409 because audit save time followed capture time. Source `6287bd22` accepted that exact audited batch relationship; the same key recovered 200 without repeating Open. Both failures remain separate evidence.

## 25. Acceptance Criteria

Accepted evidence requires genuine SDK tests/publication, nonempty native migration Up/empty Down/Up plus retained-evidence veto, scoped schema restore review, real-cookie original-key recovery, independent Decide, owner graph, denied All write, foreign-key denial, normal logout and exact private role/ACL restoration. The [checkpoint](../gap-closure/M6A-INVOICE-HISTORY-PRIVATE-ACCEPTANCE-20261028.json) records the observed bounded result.

## 26. Test Scenarios

The accepted private sequence exercised policy publication, Open201, prior GET409, corrected original Open GET200, Company All own-key GET200, All Open422, reviewer detail, Decide200, Decide-key GET200, foreign Decide-key404 and final Approved detail. It did not test multi-branch/tenant adversaries, concurrency, Return/Reject, source mutation or Finance posting.

## 27. Ownership / Dependencies

Core retains account, company scope, policy reviewer assignment and decision ownership. Procurement owns the historical inventory and billing-capacity evidence; later qualified matching still needs complete owner reconciliation, original document/unit review and the M6B source bridge. PR #133 and source `6287bd22` are pending merge, not published main or production activation.

## 28. Open Decisions / Assumptions

This checkpoint establishes one private baseline approval only. A later release must prove current production history coverage, old/new image compatibility, document ownership, qualified claim capacity, Finance handoff and broader signed-in UAT before enabling matching. Raw ACL text may differ after GRANT/REVOKE while effective grants and column ACLs are equal; that representation difference is recorded rather than rewritten.
