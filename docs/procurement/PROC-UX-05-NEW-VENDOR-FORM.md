# PROC-UX-05 — New Vendor form

**Status:** UI handoff draft, 3 October 2026. Applies to `/procurement/suppliers/new` in the Procurement frontend demo.

The form opens from **Vendors → Add vendor**. Primary Contact is visible first: salutation, first name, last name, company name, display name with suggestions or free entry, email, work phone, mobile and vendor language. The current legal entity determines the country and initial currency.

**Add more details** has seven tabs:

| Tab | Inputs |
| --- | --- |
| Other Details | Company ID/registration, tax ID and tax-rate reference, currency, payment terms, supplied categories, portal-access request. A tax group must be configured in Finance settings for multiple taxes. |
| Address | Billing address and optional separate shipping address. |
| Contact Persons | Add and remove additional named contacts with role and email. The primary contact is included automatically. |
| Bank Details | Bank name and masked account reference (last four digits only). Full instructions use the restricted Finance channel. |
| Custom Fields | Name/value pairs saved with the vendor for review. |
| Reporting Tags | Name/value pairs saved with the vendor for reporting. |
| Remarks | Internal onboarding notes. |

Documents are selected below the tabs, with a maximum of **10 files at 10 MB each**. The user classifies selected names as company registration, bank confirmation (or Kenya-specific evidence), or Other. This frontend demo stores **names, sizes and classifications only**; it does not upload file contents or establish that evidence is valid. Required qualifications remain Missing until an authorized review records evidence. Portal access is a request, not an active supplier login.

**Save vendor for review** validates company/display name, company ID, primary contact identity and email, additional contact emails, masked bank reference and field/tag pairs. It flags duplicate display names or company IDs for human review. The resulting vendor is **More information required** when required document classes are absent, otherwise **Under diligence**. It cannot be selected for a purchase order until supplier activation controls pass. The profile displays the saved details and preserves an activity event.

Production implementation still requires server storage, secure document upload/scanning, supplier identity provisioning, Finance bank verification, tax/payment-term reference validation, and permission enforcement.
