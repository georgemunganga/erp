# MANUS-CORE-BUILD-03 — restore the shared ERP frontend verification gate

**Assigned:** 22 October 2026 UTC, during the Procurement24-hour completion window.
**Owner:** Manus / Greenwebb. Coordinator continues Procurement implementation in parallel.
**Status:** Ready for pickup from published main; use the refreshed baselines below and in MANUS_HANDOFF.md.
**Outcome:** The existing shared ERP/Core login frontend passes TypeScript and its production client/SSR build without weakening authorization or changing payroll rules. This unblocks verification of the small canonical-login context notification in coordinator ERP PR17.

## Start and boundaries

- Primary repository `georgemunganga/erp`, fresh `origin/main`, containing published main `d2d45117d050357d84a63d90313782955bc80700` or its merged descendant. Record the exact starting SHA.
- Source: existing `modules/hrm/frontend/module-connect/`. Do not copy Procurement UI here or create another app.
- Procurement UI main baseline is `georgemunganga/module-connect` commit `0adb132fe721de5e74551f1fa5758c30b11fb013`; it is reference only for this task. No changes in that repository.
- Read AGENTS.md, GW-ENG-STD-001 and the existing UI standards. Preserve published history and all other work.
- Do not cherry-pick PR17 or held Core/Procurement branches. The coordinator will combine the narrowly reviewed patch with its auth producer. Do not edit auth.tsx, oidc.ts, auth-context.ts, migrations, APIs, permission grants, tenants, payroll calculations, deployment or production data.

## Confirmed starting failure

Coordinator verified that the unchanged-lockfile frontend completes a production client/SSR Vite build, but TypeScript reports28 errors in10 existing HRM files. None originates in the new auth-context producer. Reproduce and preserve the initial diagnostics before edits. Do not solve this with ts-ignore, any casts, skipped files, weakened types, removed UI controls, disabled tests or altered compiler strictness.

## Ordered implementation checkpoints

1. **Reproduce:** Record Node/dependency lock hashes, source SHA, TypeScript command, diagnostics and build behavior. Use Node22 and the existing package manager/lockfile. No dependency upgrade is expected.
2. **Shared component contracts:** Repair the branch-access page to use the actual PageHeader action prop, actual AsyncState callback signature and actual EmptyState body prop. Preserve all access controls and actions. Fix duplicate amount/note object spreads in benefit assignments without losing existing draft values.
3. **Typed domain reads:** Repair unknown benefit/reporting/org-chart API result handling against the actual existing DTO/client contracts. Restore the missing reporting unit options from the actual organization data; never hard-code fictional departments. Repair the role draft request type by matching the actual request shape, preserving grants/scope.
4. **Identity and payroll presentation:** Audit the existing OidcUser identity contract and backend principal contract before replacing invalid user.id accesses. Keep the exact canonical principal needed for maker/checker comparisons. Fix the stale payroll function arity and releasedBy display only against real supported DTO fields; no guessed authority or calculations. Repair invalid org-chart RoleGate usage using the existing supported access API, retaining the intended restricted access.
5. **Verify:** TypeScript zero errors, unchanged-lockfile production client+SSR build, relevant existing authorization/component tests and targeted browser smoke. Report mocked and real API evidence separately. Preserve first failures. Use one browser worker and no retries; test on your own machine or isolated fixtures, never production writes.
6. **Submit:** Focused commits and a draft ERP PR titled MANUS-CORE-BUILD-03 with exact source/commands/results, before/after diagnostics, UI behavior evidence and remaining limitations. Coordinator independently reviews, merges and deploys. No force-push/rebase of published history.

## Initial affected files

Paths relative to `modules/hrm/frontend/module-connect/`:

- `src/platform/components/PeriodBenefitAssignments.tsx`
- `src/routes/hrm.benefits.tsx`
- `src/routes/hrm.configuration.branch-access.tsx`
- `src/routes/hrm.configuration.roles.tsx`
- `src/routes/hrm.org-chart.tsx`
- `src/routes/hrm.payroll.exceptions.tsx`
- `src/routes/hrm.payroll.runs.$id.edit.tsx`
- `src/routes/hrm.payroll.runs.$id.tsx`
- `src/routes/hrm.reporting.tsx`

The diagnostics may count10 files including another related typed client; derive the actual inventory from your reproduced run. Supporting existing client types/components may be changed only when the real contract justifies it. List any proposed expansion before editing unrelated modules.

## Acceptance and non-overlap

All original pages retain their useful controls, loading/empty/error paths and current permission/data-scope behavior. No authentication/role bypass, invented DTO properties, fake records or backend authorization change. Browser screenshots alone do not establish access safety. This task repairs the shared frontend verification gate; it does not certify payroll correctness or Procurement completion.

The coordinator owns vendor/settings/items/requests/receiving/sourcing/orders/invoices, canonical migrations and Procurement browser integration. Do not duplicate that work. Global settings remain Core-owned. Do not implement the older queued settings milestone; that shell is already under coordinator verification.
