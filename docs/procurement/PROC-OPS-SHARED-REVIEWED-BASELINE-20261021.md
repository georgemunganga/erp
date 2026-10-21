# Shared reviewed source baseline — 21 October 2026

Status: proposed source consolidation for review. This branch has not updated main,
changed production configuration/schema or performed a deployment.

## Exact boundary

- Start: canonical `origin/main` `440123cfea9169d74743222dc0390578d2858bd5`.
- Ordinary non-fast-forward merge: released P19-compatible access hotfix/documentation
  `d26390a`, whose compiled backend source is `a7645e2`.
- Preserve all six main-only commits: `3f2b489`, `05ddea3`, `264b121`, `1013866`,
  `3d7ec71` (Greenwebb README name) and `440123c` (ERP Manus coordination).
- Do not merge the P20–P29 draft financial/workflow chain (ERP PRs3–8), newer model
  snapshots, financial authority changes, RFQ publication work or feature activation.
- Do not copy/re-import the separate Procurement frontend or replace the tracked HRM
  frontend snapshot. The existing handoff retains that repository boundary.

Two add/add conflicts arose only in the loan compatibility probe Program/README,
which main had at its older 1013866 version and the hotfix restored/strengthened.
Resolution retains the released, strict e68416f/a7645e2 probe files. No application
source conflict or new application logic was introduced. The operational document
merged main recovery history and hotfix provenance. Existing Manus instructions,
claim log and Greenwebb README remain from main; coordinator owns their next update.

## Source verification and inherited evidence

The **entire backend tree** is byte-identical to d26390a after merge. Every changed
source hash in the released hotfix manifest matches, protected P19 Program/routes/
roles/leave/PDF files remain unchanged, and no Procurement/Core migration is added.
The only retained HRM migration addition is the already-live LoanOpeningBalances.
No SDK/database/browser job is needed merely to repeat an identical backend tree.

Applicable existing source-scoped results:107 focused host tests +28 retained
Procurement tests, strict loan contract/JIT, full142-file host/image equality,
private clone startup with unchanged schema/data and the bounded access release.
See [access release record](PROC-CORE-ADMIN-READONLY-ACCESS.md#access-only-release-verification--21-october-2026)
and [source reconstruction audit](PROC-OPS-V36-ACCESS-HOTFIX-SOURCE-AUDIT.md).
These are inherited exact-source results, **not new tests on this documentation merge**.
The earlier failed compile and clone-start attempts remain recorded.

## Milestones and remaining work

| Milestone | This baseline |
|---|---|
| Released P19 draft Procurement and retained HRM compatibility | Preserved |
| Employee-less ERP administrator supported GET review / denied commands | Reviewed released access fix included |
| Manus vendor integration/layout parity | In progress; ERP PR9 is a claim/status-only draft, not an accepted UI implementation |
| P20–P29 governed procurement, schema and authority work | Separate draft branches/PRs; not part of this merge |
| Vendor imported-layout preservation | Required in existing UI; no new layout or frontend copy introduced here |
| Whole Procurement completion | Not achieved; existing roadmap/feature contracts remain authoritative |

After coordinator review, merge this branch normally into main without rewriting
published history, then publish a precise Manus start SHA and UI dependency SHA.
No source merge implies deploying unfinished capabilities or changing production
flags. The user has not been impersonated and no employee/role link is fabricated.
