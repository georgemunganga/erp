# Quotation draft workflow verification — 3 November 2026

Status: isolated API and complete normal-cookie UI command journey passed with native owner reconciliation. Retained B/C Company All browser reads and paired release are still being verified. This is bounded quotation-draft evidence, not whole Procurement UAT or business-owner acceptance.

## Implemented behaviour

Current Core users with explicit quotation permissions can discover retained RFQs, record contact made outside ERP, capture full supplier offer and exact line terms, revise or cancel drafts, read retained versions and recover their original command result. An employee link is optional. The RFQ maker's own authoring permissions remain separate.

ERP PR152 adds verified source item lines and closes two real integration defects found by native testing. Fresh quotation writes reject the raw Company All selection, including legacy middleware fallback to a default branch. Finite retained quotation GET routes support Company All within current Core branch assignments. Historical demand validation binds only the already-authorized source branch and leaves the original request scope unchanged. The shared demand loader's other consumers and constraints are unchanged.

UI PR99 connects these workflows in the preserved ERP-adapted RFQ screens and shared controls. It retains full offer terms, exact decimal strings, complete active-line replacement, cancellation evidence and a reference-only interrupted-save journal. It also fixes detail/history retry states and uses plain status labels and help text.

## Verification and evidence boundaries

- Backend source `7f25efefebd7215151d318159a56dae00d9703c3`: 236 focused Host cases passed with analyzers, including all previous 231 stable identities and five retained-scope cases. Existing warnings remain. The isolated SDK container exited zero without OOM; after its tool client exited143, a separate verifier reconciled the exact TRX, source pins, protected publications and fresh private API publication without rerunning tests. Binding: `0cea83c3cd3c434f2a4f398a0cf1d890b9c121ccf721281ead6e1f1c5302aecd`.
- UI initial source `07befac24c1742bb38397134c17d18c703e79b3d`: 59 unit checks, TypeScript/live build and39 mocked Playwright cases passed (17 quotation plus22 existing RFQ cases). Followup `86f3f1614f84edcd2945b650c739d1fdbfb6f900`:65 unit checks, TypeScript/live build and19 quotation browser cases passed. These browser suites mock transport. Existing bundle-size warning remains.
- Genuine isolated Core-cookie API requests verified contact201, full draft create201, exact replay, changed-payload409 and original-response recovery201. Current-user-B and C both have no employee link and neither is the original RFQ maker.
- Corrected private build passed13 scope/revision/storage checks: fresh All write422 with unchanged six-table fingerprints; retained All detail/source reads; actor-private recovery; independent-handler revision; stale409; version reads; READ and original recovery without INSERT; fresh RECORD503 without INSERT. The clone-local privilege fixture was restored.
- Five reassignment checks passed: moving B to another genuine same-company fixture branch removed its source list, denied direct history/recovery404 and the old selected branch403; restoring the assignment restored access200. No user/branch changes were made in production.
- Seven capture-disabled checks passed: retained history and original-create replay, cancellation, exact offer/lines/documents/totals retention, original-key recovery and prior-version reads. Cancellation does not depend on new capture being enabled.
- Independent native reconciliation verified one contact, five retained versions, one bid/line and six corresponding audit/outbox pairs; response/request/snapshot hashes, command uniqueness, workerless actor attribution and pending/no-send effects matched. Two versions were genuine pre-fix raw-All revisions made during failing tests; their records and receipts remain preserved. Failed attempts are not relabeled passes.

The actual original UI confirmed outside-contact201 and full quotation-create201 as B, then reloaded the complete offer. The initial fresh test stopped before C's revision because its strict label locator did not find prefilled textareas. Three read-only normal-cookie diagnostics proved current C authorization, editor state and accessible textbox names, with no writes or page errors. The failed receipt and original keys are preserved; contact/create were never repeated.

A C-only continuation on the same saved draft passed Revise200, original-version history, Cancel200 and cancelled-record reload. It preserved full offer/received-at/other line terms and produced authoritative payable26.59595 from exact input strings. The continuation receipt is `c3479f9a3c792c29b3e66632e3b820241a1d1ec735b68b34d5830e96d2110a63`. Test-only UI commit `552c071a444ea3d13e614ee64eb326b6d5873de1` repairs future harness locators and step diagnostics; application sources are identical to the tested86 build.

Independent owner reconciliation verified exactly one contact, three retained versions, one bid/line and four audit/outbox pairs. All four original browser command keys and raw HTTP response hashes match the owning database receipts. No hidden/repeated business save, supplier submission, email delivery or Finance settlement occurred. A separate temporary Core worker-link fixture confirmed retained READ and original-key201 recovery survive optional employee-link changes; the original null link was restored, the synthetic worker archived and historical attribution unchanged.

The browser used a separate untouched template clone, real Core login/session cookies and the exact UI build behind an HTTPS bridge inside the API's network-none namespace. Responses were not mocked or substituted, and identity aliases were not injected. Retained B/C Company All browser checks and authentication-cohort cleanup remain separate final verification steps.

## Scope and remaining gates

Production quotation recording/capture switches remain off at this checkpoint. No production data, permissions, migrations, publisher or API/UI release changed as part of the private tests. Test policies are not approved company spending rules.

Nonempty quotation attachments remain unavailable pending the actual shared Core document-use integration; no verified file evidence is fabricated. Supplier portal submission, sealed opening, evaluation, award and award-to-PO are later M7 milestones. Finance ledger/bank execution remains Finance-owned. Whole-module UAT retains the additional gaps in [the master assessment](PROCUREMENT-UAT-AND-GAPS-20261027.md).

GitHub Actions is billing-blocked; local/private verification is recorded separately from CI. Full private records, input pins, original keys and failed evidence are retained under protected server evidence paths rather than committed with credentials or supplier bodies.
