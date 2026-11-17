# Employee import reference workflow

## 1. Outcome
Reviewed missing-branch creation in the existing employee import, with safe CSV header mapping and live trial acceptance.

## 2. Tier
Tier3 HR/tenant boundaries. No server authority or schema change.

## 3. Ownership
Core owns company/work locations/access; HR owns employee configuration. Reuse existing guarded location create API.

## 4. Entry
Kuubu company HR workspace -> Employees -> Import -> Preview -> Check missing data.

## 5. Actors
Authorized HR administrator; branch creation in SaaS requires explicitly selected company-wide HR access.

## 6. Permission boundary
No automatic grant expansion. Backend Core company/commit guards remain unchanged. Restricted branch grants see clear review-access guidance.

## 7. Company selection
Verified product company or server shell entity; ambiguous multi-company contexts stop. No first-row company inference.

## 8. Branch discovery
Trim/case-insensitive active name comparison within the selected company. Unknown IDs remain errors rather than proposed names.

## 9. Creation review
Show company and missing branches/departments/grades before Create and recheck.

## 10. Execution
Fresh read before writes; reject changed company/new unreviewed values. Sequential shared-service creates, followed by server preview.

## 11. Retries
Created values retained after partial success; explicit recheck recomputes remaining names. No automatic POST retry.

## 12. Read failures
Configuration/network/authorization failures block planning rather than becoming empty existing-data lists.

## 13. Grades
Existing employment positions are preserved. Malformed saved configuration fails closed.

## 14. Header mapping
Normalized exact keys/labels and explicit employee aliases. Unknown/ambiguous/duplicate source columns remain unmapped.

## 15. CSV sample
User CSV has32 headers/20 rows; branches Lusaka HQ/Kitwe/Ndola and employment labels Permanent/Contract/Part-time. Read references only; do not expose personal rows.

## 16. Ignored columns
Explicit list at mapping/preview. Only mapped supported fields are saved; legal entity never establishes company authority.

## 17. Employment semantics
Known labels map to canonical worker categories. Ambiguous Contract/Part-time require explicit category correction; no inferred contractor classification.

## 18. Data safety
Use clearly marked synthetic employees for trial persistence. No copied bank/government/person data or automatic login creation.

## 19. UI states
Loading/checking, missing-data review, no missing refs, access denied, partial creation, refreshed preview and import outcomes.

## 20. Persistence
Existing Core WorkLocations/OrgUnits, HR employment setup and Workers; no new tables.

## 21. Audit
Existing shared configuration services and product request/commit boundaries retained; evidence records synthetic run IDs and outcomes.

## 22. Concurrency
Re-read immediately before mutation. Shared unique location codes prevent same deterministic code duplication; error prompts fresh review.

## 23. Tests
Mapping aliases/unsupported headings, branch case/company filtering, unknown IDs, existing setup checks, TypeScript/build; live Codex browser full workflow.

## 24. Release
Fresh immutable web build/publication and proxy clone; retain API image and all other routes/mounts.

## 25. Rollback
Retain previous web/proxy. No database rollback; reviewed setup/test records are retained.

## 26. Acceptance
Test A owner access reviewed before any grant change; mixed case/errors/missing references/import persisted verification required.

## 27. Limitations
Do not claim arbitrary fields supported. Branch creation requires company-wide company-specific scope; field aliases never infer backend contract meaning.

## 28. Evidence
Record final commits/containers/ports/backup script and browser screenshots in MANUS_HANDOFF after acceptance.


## Live acceptance follow-up: persistent completion
The first live20-row create succeeded and read back20 unique synthetic employees,
18employee/2contingent; branches15LusakaHQ/2Kitwe/3Ndola,12departments. However the
embedded tool only emitted a transient toast and left the applied preview active.
Follow-up discards that preview after any apply attempt, blocks reusing its ID,
and shows a persistent confirmed/unconfirmed result. Confirmed partial results
keep only failed/unsubmitted rows for a fresh review. An uncertain response is
never shown as successful or auto-replayed. View employees and Import another file
provide explicit next actions. Modal imports retain their existing close behavior.
The next live acceptance must prove duplicate insert preview rejection and an
explicit match/update on the same20 synthetic records, with no extra employees.

## Access-recovery state retention
After the completion card was observed, it reverted to upload while the agent
performed read-only database verification. The user confirmed no manual refresh
or navigation. Product HR temporarily hides/restores children with React Activity;
its effect replay could re-run the embedded importer initialization and clear the
retained file/result. Initialization is now guarded once per embedded import type.
An actual company/session unmount still creates fresh state; a type change resets
normally. Explicit Import another file resets normally. Verify completion survives
a background-check interval in an isolated Codex browser tab before View employees.

## Initial schema loading
Live reopen exposed a file selection before the server schema finished loading.
Upload/drop/paste and template actions now wait for the authoritative schema;
no file is mapped to an empty field list or false unavailable warning.
