# ERP Core: organization and shared settings

## Purpose

ERP Core is the entry point for data and configuration reused by HRM, Procurement, Finance, and future modules. Its UI is at `/core`. The first implementation reuses the live editors and APIs that were built under HRM, while presenting them in an ERP Core workspace. It does not create duplicate company, branding, user, or department records.

## Ownership

| Capability | Long-term owner | Current implementation |
| --- | --- | --- |
| Tenant/company branding | ERP Core | HRM branding API, reused by Procurement |
| Legal entities, branches, departments | ERP Core | HRM admin configuration API |
| User identities and organization access | ERP identity/Core | Existing identity provider and HRM admin routes |
| Tax rates and withholding (TDS) | Finance | Planned; no Core editor yet |
| Currency reference and display defaults | ERP Core, with Finance rules for accounting | Planned; no Core editor yet |
| Payment terms | Finance | Planned shared reference |
| Vendor portal and punchout catalogs | Procurement | Procurement workspace previews |
| Number series, PDF templates, email templates, web tabs | ERP Core platform | Planned; no persisted Core editor yet |
| Procurement approval and invoice rules | Procurement | Procurement module settings |
| HR policies and payroll setup | HRM | HRM configuration |

## Rules

The ERP home page is the sign-in entry point. After authentication, users with the current `hr_admin` role see the module picker; other admitted workforce users enter the HRM dashboard. Core settings require `hr_admin` until a shared ERP administrator role and cross-module permission service are implemented. The existing HRM identity endpoints remain the current system of record for user and role changes.

Organization, branding, users, roles, and their detail pages have one UI home under `/core`. Old `/hrm/configuration/*` URLs redirect to Core for existing bookmarks. HRM configuration lists only HRM-specific policies and links to Core for shared settings.

1. The Core navigation may link to a module-owned editor, but that module remains the system of record for its rule. A link is not a duplicated setting.
2. Do not enable a Save action for a planned setting until its persistence, permissions, audit, versioning, and consumer contract exist.
3. Organization context uses stable legal entity and branch IDs. UI selection is a convenience; every API validates scope and access server-side.
4. Preserve the existing HRM API routes as compatibility adapters while moving organization and branding ownership to Core. Do not copy their tables and create competing masters.
5. A change to shared branding must appear in HRM, Procurement, sign-in, and later modules through the same source record.

## Next backend milestone

Create versioned `/api/core` endpoints and a shared organization service around the existing legal entity, location, department, and branding tables. Migrate consumers to those endpoints. Then add persisted platform configuration for currency references, reporting tags, document numbering, templates, and notification preferences with entity scope and audit history. Finance and Procurement continue to own their respective tax/payment and supplier-portal policies.
