# PROC-BE-03 — First Procurement CRUD operations

**Document type:** Backend implementation record and UI contract

**Status:** Source implementation built and tested; not deployed

**Prepared:** 4 October 2026
**Hierarchy:** ERP → Procurement → Vendor, Items and Requests → Draft CRUD operations

This milestone exposes the first records needed by the simple Procurement screens. It implements draft preparation and safe read paths. It does not authorize suppliers, publish catalog prices, submit requests, reserve budgets, approve purchases or issue orders. Those transitions require the policy and workflow decisions in the child specifications.

## 1. Feature Identification

`PROC-BE-03` groups the first API operations of `PROC-01` Vendor Management, `PROC-04` Items and Catalog, and `PROC-02` Purchase Requests. The route prefix is `/api/procurement/v1` in the shared ERP host.

## 2. Purpose and Business Outcome

Users can create and revisit useful draft data behind the existing ERP login. The UI can replace placeholder lists and forms with real scoped records while high-risk decisions remain controlled.

## 3. Scope and Exclusions

In scope: vendor draft/profile/contact/address/qualification entry; item maintenance and draft catalog prices; own purchase-request draft and line editing; list/detail/search; optimistic version checks. Excluded: supplier activation, bank instructions, catalog publication, PR submission, approval, budgets, sourcing, PO, receipt, invoice and payment.

## 4. Parent and Related Features

The parent is [PROC-00](PROC-00-PROCUREMENT-MODULE-ARCHITECTURE-AND-CAPABILITY-MAP.md). Data and workflow requirements remain in [PROC-01](PROC-01-SUPPLIER-ONBOARDING-AND-MANAGEMENT.md), [PROC-04](PROC-04-PROCUREMENT-ITEMS-AND-CATALOG.md), [PROC-02](PROC-02-PURCHASE-REQUISITION-AND-LINE-ROUTING.md), and [PROC-BE-02](PROC-BE-02-SHARED-HOST-ACCESS.md).

## 5. Actors and Participants

An active employee may prepare their own vendor or request draft. A procurement buyer/manager may maintain entity-scoped vendor and item data. The existing HRMS identity, organization and worker records determine access. Finance, Inventory and Assets remain external owners of their ledgers.

## 6. Entry Points and Triggers

The UI uses `/vendors`, `/items`, `/catalog` and `/requests` under the API prefix. A user opens a list, selects a record, saves a draft, adds a child record or withdraws a draft. The ERP shell sends its selected company/branch headers.

## 7. Preconditions

The shared host has `ConnectionStrings:Procurement`, the Procurement schema migration has been applied, the caller is authenticated with a tenant and permitted role, and the selected company is valid. Business reads and writes require an active HRMS worker mapped to that company; the technical metadata route is exempt.

## 8. Workflow

Authenticate → resolve company/branch → check action permission → resolve an active worker for business routes → validate input/references → load scoped record → check record owner/state/version → save with audit metadata → return the new version. A save conflict asks the user to refresh.

## 9. Status / State Transitions

Vendors start `Draft` and may be withdrawn with a reason; this milestone does not activate them. Items and catalog prices stay inactive/unpublished through steward edits; item deactivation and draft price withdrawal are supported. Requests start `Draft` and may be `Withdrawn`; line removal is represented as `Cancelled` rather than physical deletion. Submission is absent.

## 10. Permissions and Data Scope

Named policies distinguish vendor read/create/edit/manage, item read/manage and request read/create/edit. The first implementation uses explicit role grants in the ERP host; these need tenant-configurable grants before full rollout. EF filters every business query by tenant and legal entity. Employee request reads/edits are own-record only. Ordinary vendor reads show orderable approved/active suppliers or the caller's draft; full vendor details and broad entity lists require vendor-manage. Supplier portal access is not included.

## 11. Business Rules

Unapproved vendors cannot be made orderable through these routes. Item maintenance cannot publish an item or price. A request line needs a positive quantity and valid catalog/supplier reference if supplied. Approved/orderable supplier status is required for a preferred supplier. A withdrawn or non-draft request cannot be edited here. Record versions are mandatory for edits.

## 12. Data Fields and Ownership

The P1–P3 migration in [PROC-BE-01](PROC-BE-01-FIELD-COVERAGE-AND-SCHEMA-GATES.md) contains the vendor profile and child fields, item/catalog fields, and request header/line fields. Procurement owns these records. HRMS owns employee status and worker mapping; Organization owns entities/branches; Finance owns budget and AP; Inventory owns stock; Assets owns the asset register. Bank/remittance details are outside this API.

## 13. Screens and Data Views

Vendor and Item lists support search, paging and detail. The employee catalog is a separate safe view of published eligible entries; it will be empty until a later publication workflow exists. Requests expose own list and detail with lines. Each UI should show its empty state with Add/Create actions and display a clear refresh action for version conflicts.

## 14. User Experience / Human Behaviour

Use plain labels such as “Vendor”, “Item”, “Request” and “Save draft”. A saved draft remains editable; users must see when a record is unavailable, inactive or already changed by someone else. The UI should not show Submit/Approve/Publish actions until their backend transitions exist.

## 15. Notifications and Communications

No business notification is sent by draft saves. Notifications begin with submission/assignment milestones. Validation and conflict responses are returned to the initiating user.

## 16. Documents and Attachments

Qualification evidence can reference a document record, but document upload and scanning are a later integration. The UI should avoid implying that an unpersisted attachment is accepted evidence.

## 17. Exceptions and Edge Cases

The API rejects duplicate vendor registration keys, duplicate item codes, invalid references, stale versions, writes by inactive workers, and edits to locked states. Similar vendor names are surfaced for review instead of silently merged. A missing or unauthorized record returns a non-disclosing not-found response where appropriate.

## 18. Integrations / APIs / Events / Sandbox

The existing HRMS API hosts the routes and supplies local-session/OIDC authentication and HRMS worker lookup. Procurement uses its own EF context and migration history. No Finance, Inventory or external event is emitted by a draft operation. Use a disposable database and test identities to verify routes before any production migration.

## 19. Audit / Security / Privacy

The Procurement context captures actor, changed fields, record ID, version and correlation metadata. It forbids physical deletion of business records. Sensitive vendor tax/payment verification fields are excluded from ordinary vendor detail. Tenant/entity filters and worker checks are server-side; browser headers are selections only.

## 20. Reports / Dashboards / Metrics

These APIs support basic draft counts and list views. Cycle-time, spend and approval KPIs require later committed/approved states and defined metric rules.

## 21. Configuration and Localization

Role grants are initial bootstrap values. Approval thresholds, required quotes, currencies, category policies, numbering format and tax treatment remain configuration decisions; none is approved by this milestone.

## 22. Non-Functional Requirements

Lists page at a bounded size. Writes use optimistic concurrency; scoped reads and database constraints provide defense in depth. Health readiness requires the Procurement database when the module is configured. File size, throughput and retention targets await business volumes.

## 23. Postconditions

A successful draft save returns a persisted ID and current version inside the selected tenant/entity. No commercial commitment, budget reservation, supplier activation or AP liability is created.

## 24. Failure and Recovery

On validation failure, preserve the user's form data and show the field problem. On a version conflict, reload before retrying. If the Procurement database is unavailable, readiness fails and the UI should offer retry; it must not pretend a draft was saved.

## 25. Acceptance Criteria

1. An active mapped employee can save and reopen their own request draft; inactive employees cannot mutate records.
2. One tenant/entity cannot read or change another tenant/entity's records.
3. Ordinary employee vendor reads do not reveal other people's drafts or restricted profile fields.
4. Draft edits require the current version; stale saves return a conflict.
5. A draft operation cannot activate a vendor, publish a catalog entry, submit/approve a PR or issue a PO.
6. The host builds, route tests pass and a disposable PostgreSQL migration remains in sync with the model.

## 26. Test Scenarios

The HRMS suite passed 409 tests and the Procurement persistence suite passed 5 tests on 4 October 2026; the focused Procurement access suite passed 4 after the last route change. Automated access tests cover named role grants, confined branch/company denial, active-worker mutation, and cross-company business-read denial. Route-level HTTP integration cases for duplicate keys, version conflicts, line cancellation, vendor redaction and catalog eligibility remain a release gate before live UI wiring.

## 27. Ownership / Dependencies

Procurement engineering owns routes and schema; ERP identity/HRMS owns worker and role claims; Organization owns entity mapping; product/Finance owners decide submission and budget policy. The UI team can wire lists/forms to these draft routes only after the environment's migration and rollout gates are met.

## 28. Open Decisions / Assumptions

Decide tenant-configurable role grants and data scopes; supplier application/approval actors; vendor duplicate review rules; item activation and catalog publication; PR required fields, budget source and approval route; document upload; production migration/rollout timing. These decisions block the corresponding transitions, not draft preparation.

## Route summary

| Area | Initial routes | State limit |
|---|---|---|
| Vendors | `GET/POST /vendors`, `GET/PUT /vendors/{id}`, `POST /vendors/{id}/contacts|sites|qualifications|withdraw` | Draft preparation, scoped reads and withdrawal |
| Items | `GET/POST /items`, `GET/PUT /items/{id}`, `POST /items/{id}/deactivate`, nested catalog-entry draft routes | No activation or publication |
| Employee catalog | `GET /catalog` | Eligible published entries only |
| Requests | `GET/POST /requests`, `GET/PUT /requests/{id}`, `POST /requests/{id}/withdraw` | Own drafts only |

## Live UI integration gate

The separately deployed `module-connect` Procurement UI currently stores transactions, role selection and company selection in browser data. Its company IDs and permission persona are demonstration values. [PROC-BE-04](PROC-BE-04-LIVE-IDENTITY-AND-COMPANY-CONTEXT.md) introduces the live session and company bridge. The three CRUD screens must then use these routes, keep draft-only actions visible, preserve unsaved form values on errors, and show explicit loading/empty/forbidden/stale states. A failed API request must not silently switch an authenticated user back to demonstration records. Until the screen wiring and rollout are complete, the live site remains a UI demonstration even though backend source routes exist.
