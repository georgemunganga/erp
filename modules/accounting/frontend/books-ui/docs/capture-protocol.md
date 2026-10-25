# Books reference capture protocol

The user confirmed the selected Zoho Books Global organization reached its dashboard. The readiness gate is cleared. Continue only in that same organization using My Browser. Never return to or submit the initial Organization Setup form; do not click its “Get Started” action.

## Scope and identity

Use the same user-selected Global organization only. Confirm edition and org context from the authenticated UI without switching organizations. Do not use public regional documentation to infer tenant configuration. Keep organization identifiers, personal/contact details, account numbers and financial records out of sanitized documents.

## Navigation and interaction boundaries

Start with ordinary module navigation, lists, existing read-only details, tabs, reports and settings displays. Blank transaction forms and dialogs may be opened to inspect their fields and actions, then canceled without saving. Do not select live contact data, submit a financial entry, send a message, import/export tenant data, connect a bank, change accounting/security settings, approve, delete or make a payment. A broad request to test the UI does not override the need for exact authorization for consequential actions. Prototype synthetic data stays local and must never be entered into the live organization.

Inventory visible controls and record their entry path, prerequisite, category, observed result, cancel/back behavior and evidence status. Where interactions are ambiguous, re-check by visible labels or a direct observed route rather than clicking near a consequential button. If an accidental low-impact preference change occurs, restore it and verify the restored state; log it privately. Record unavoidable system access metadata changes transparently.

## Canonical references

Group screens into list, create/edit form, record detail, import/map/preview wizard, confirmation/status transition, allocation/matching, approval, communication/portal, payment/refund, document/template, settings, report and automation/integration. One canonical screen and reusable component map serves related workflows; capture meaningful variations only. Record prerequisites by plan, role, edition and module enablement, and mark unexplored actions unverified or blocked rather than guessing.

`docs/verified-screen-patterns.md` is the sanitized tenant evidence index. `reference-private/notes/tenant-capture-index.md` keeps a detailed private action log without tenant record values.

## Screenshot artifact limitation

The My Browser renderer shows screenshots inline in the task's browser-action results. In this task those results reported `Clean screenshot path:` with an empty value, and no image file was saved into `reference-private/screenshots/`. Do not claim a local screenshot-file archive or attach it as a deliverable. Preserve the result imagery within the task session only. Never reconstruct or fabricate a screenshot and label it as tenant evidence.

## Evidence labels

**Public-doc candidate** means the workflow is in public documentation but is not tenant-verified. **Tenant-verified read-only** means a screen/state was observed without business-data mutation. **Observed interaction** means a safe control was exercised. **Observed control, commit not executed** means a consequential action was not performed. **Blocked by safety boundary** means the next step could have consequences. **Unverified** means the evidence is absent. **Observed unavailable** requires visible evidence of plan, permission or feature limitation. **Synthetic demo** refers only to local prototype behavior.

## Prototype separation

Use fictional fixtures only. Make their synthetic/local nature visible in the app. Local create/edit/status/delete behaviors are simulations only; never call live Zoho or external services. Do not put references in `public/`, `src/`, website storage, a CDN, shared uploads, commits or the GitHub backup. Keep the website unpublished.
