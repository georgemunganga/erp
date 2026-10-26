# M3b release rehearsal — 26 October 2026

Status: private migration and backup restore verified; application acceptance and production deployment pending.

The principal-based direct purchase-order draft retains the existing ERP identity, branch scopes, approved requisition sources and quantity allocation ledger. It adds controlled create/read/cancel and command recovery to the existing interface. It does not activate order issue, payment execution or unconfigured approval policies.

## Verified evidence

- Genuine EF compilation and SQL export at source `a61f2ec4b27cbe5470de76380464ae59d1ab4e27` found no pending model changes. Compiled migration receipt SHA-256: `c4e47241a393d7c360cceac12f70ef95bc80aa2247a1b3173825da6844c1ee71`.
- Migration `20261026074403_M3bPrincipalDirectOrderDraft` passed Up, empty Down, and Up on the isolated PostgreSQL cluster. Original-column row projections for all 198 baseline tables were preserved. Both new evidence tables remained empty; only the expected migration history entry was appended. Receipt: `f6efa3687b5fe9fb96d25a25b695f21b948d366f8d3fe7752cbbd4cf43398454`.
- A separate transactional audit-marker test executed the full generated Down, received SQLSTATE `55000`, and rolled back. Post-test data matched the prior accepted Up state. This proves the audit-marker veto; populated order/binding/receipt veto cases remain pending.
- All three actual PostgreSQL CHECK definitions were captured and committed in `62bff76521b65747018d535e6f76ab45d0f8fd25`. Readiness still requires the real schema, finite database roles, prior approval/source readiness and feature configuration.
- UI source `af55587861793a7c0dee434bb9270b890dbe1ce8` passed the relevant TypeScript program, 67 focused unit tests, genuine live build and seven mocked browser cases. Evidence: `44272bb567e4e035d9638f04b5b4cac2f433eb58b3edf74c2f88a7a07abb7850`. [UI PR 84](https://github.com/georgemunganga/module-connect/pull/84) remains draft pending real-API browser acceptance. Whole-ERP TypeScript verification is not claimed.
- Fresh consistent production backup SHA-256 `46e7fbc1c57bfe4c19a4741034d223cc7ebb30f68281e35588061dec90a1475e` restored successfully into a new isolated database. All 191 COPY-table row multisets matched the exact archive; it contained zero sequence sets. Restore receipt: `64a891138fc7fb18f14d2ddd51823483640323746b6e0b57438265e3401e74a5`. Private ownership was deliberately mapped to PostgreSQL administration; production role/ACL restoration is not certified by this test. Original ownership/ACL metadata remains in the protected archive.

The restore used admitted verifier source `80cde069d7aa2037e9159f601ed1f282f3a9b9c1c3b4c02dc1b18d680900605c`. A later source-only refinement for sequence TOC support did not execute in that run. Protected raw evidence and row hashes remain outside Git.

## Remaining release gates

1. Compile and test the final backend, including the null nested-input validation fix and captured CHECK manifest; export the complete ordered production upgrade SQL.
2. Verify current principal-based draft workflows through real authentication, finite-role connections and browser actions, including recovery, residual quantities and denied access.
3. Rehearse the complete P24-to-current upgrade on the fresh restored backup and verify existing records and finite grants.
4. Verify production password authentication, prepare the reviewed application image and static release, deploy with rollback retained, then run production smoke checks.

Production schema, credentials, images and UI aliases were unchanged by these rehearsals. This checkpoint does not declare all Procurement milestones or UAT complete.
