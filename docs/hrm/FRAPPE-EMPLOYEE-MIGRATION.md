# Frappe employee profile audit (23 September 2026)

Source: the local Frappe site `admin.amizopower.co.zm`, read from its MariaDB database. This document contains counts and field mappings only; the restricted source exports are under `/home/amizo/erp-backups/20260923-frappe-people-inventory/`.

## What exists

| Source data | Count | Migration treatment |
| --- | ---: | --- |
| Employees | 122 (115 Active, 7 Left) | Preserve Frappe employee number as the stable source key. |
| Frappe users | 16 (including Administrator and Guest) | Keep separate from employees; 10 employees link to a Frappe user. Do not copy credentials or create accounts automatically. |
| Employee-linked files | 19 files for 17 employees | Import through the HRM document service, retaining private classification and source employee number. |
| Education / external / internal history rows | 14 / 18 / 27 | Import after worker identities are established. |
| Departments / job titles | 10 populated department names / 47 distinct job titles | Reconcile departments with HRM org units before assigning workers. |

## Profile coverage and field mapping

| Frappe field | Populated | HRM destination or decision |
| --- | ---: | --- |
| `name` | 122 | `Worker.EmployeeNo`; keep the `HR-EMP-` number. |
| `first_name`, `last_name` | 122, 116 | `Worker.FirstName`, `Worker.LastName`; six single-name profiles need an explicit mononym rule, not a fabricated surname. |
| `date_of_joining` | 122 | `Worker.StartDate`. |
| `status` | 122 | Active to active; Left to terminated/archived through the HRM lifecycle, preserving the historical leaving date where available. |
| `department`, `designation`, `reports_to` | 110, 112, 88 | Org unit, job title, manager. Create/map org units first; resolve managers by source employee number in a second pass. |
| `company_email`, `personal_email`, `cell_number` | 15, 14, 45 | Work email, personal email, phone. Leave missing contacts empty; do not invent email addresses or phone numbers. Resolve repeated shared addresses and phone numbers before enforcing person-unique identifiers. |
| `date_of_birth`, `gender` | 122, 122 | Birth date exists in HRM; gender needs a deliberately scoped profile field if HR needs it. Treat both as sensitive profile data. |
| `employment_type`, `contract_end_date` | 99, inspect separately | Map to HRM worker/contract type and assignment dates; Frappe values (Contract, Full-time, Piecework, Probation) do not map one-to-one to `workerType`. |
| `custom_id_number`, `passport_number` | 74, 6 | Verify the meaning/format of the custom ID before mapping it to NRC. Passport number has a direct HRM field. |
| `person_to_be_contacted`, `emergency_phone_number` | 12, 11 | HRM emergency contact child records; check relationship before import. |
| `current_address`, `permanent_address` | 3, 1 | HRM lacks person address fields; add only if needed by onboarding/payroll or contact workflows. |
| `bank_ac_no`, `ctc` | 16, 92 | Keep out of the initial roster import. Bank details require restricted handling. Frappe CTC must not be treated as HRM basic salary without confirming pay period and meaning. |

The existing HRM profile already supports NRC, passport, TPIN, NAPSA, NHIMA, nationality, date of birth, emergency contacts, bank details, education, work history, documents, assignments and managers. The larger gap is in migration coverage: the current shared employee import asks for work email and phone, although most Frappe employees lack them, and it does not import status, personal email, date of birth, passport, emergency contacts, manager or documents. The older CSV endpoint permits missing email/phone but still requires a last name and does not preserve leaver status or all profile fields.

## Import result (23 September 2026)

The one-time migration at `scripts/import_frappe_people.py` was rehearsed against a restored copy of the live HRM database, then applied to the `amipower` tenant as one transaction. The live HRM database was backed up first at `/home/amizo/erp-backups/20260923-frappe-import/hrm-before-import.dump`. This historical data load writes directly to the HRM schema; it does not create new HRM workflow requests or retroactive approval/audit events.

| HRM data | Imported |
| --- | ---: |
| Workers | 122: 115 active and 7 archived/terminated |
| Legal entity / departments / branches | 1 / 10 / 4 |
| Manager links / emergency contacts | 88 / 12 |
| Education / external / internal work history | 11 / 16 / 26 |
| Private employee documents | 19 files, stored persistently under `/home/amizo/hrm-documents` |
| Disabled Frappe user accounts | 14, with no HRM roles and no transferable credentials |
| Restricted legacy source records | 230 employee, user, attachment, history, check-in and promotion records |

The legacy source records reside in `hrm.legacy_frappe_records` for fields with no safe one-to-one HRM mapping. This includes bank and CTC fields, gender and addresses, custom ID values, shared work contact details, three education rows lacking an institution, two external history rows lacking a company, one internal history row lacking a department, 13 check-ins, and one promotion. The original Frappe database and backup remain untouched. Source login secrets, reset tokens, API keys, and session/IP information were not transferred.

Six single-name workers retain an empty last-name field rather than an invented surname. Duplicate work email or phone values were left blank in active HRM identity fields and retained in the restricted source record, so later account linking cannot accidentally identify the wrong person. Before using payroll bank details or past CTC values, HR must verify the account holder and salary basis. The 14 imported accounts require explicit role assignment, activation, and a new HRM password setup before anyone can use them.

## Import readiness at the start of the migration

At the start, HRM had zero workers, zero org units, zero legal entities and zero work locations. The shared import screen would have accepted at most 14 source rows on its required email/phone fields before duplicate checks; three work email values and two phone values are shared between employees. Six employees have no last name. Therefore the migration used a dedicated one-time adapter with a source-record archive instead of the shared import screen.

For the continuing HRM module work, surface and verify the staged legacy fields through restricted profile screens and extend the normal import flow to support missing contact details and mononyms. User activation stays separate from the roster import.

## Employee profile follow-up (25 September 2026)

The live HRM worker profile now carries the source salutation, gender, marital status, addresses, blood group, employment label, company, confirmation dates, notice period, shift, holiday calendar, attendance device and cost centre as restricted profile details. The employee detail API loads education and work history collections, and the profile and edit screens display or edit the available fields. The new-employee form supports these core personal details, optional pre-hire contacts and identifiers, emergency contact, and single-name employees. This follow-up was applied after a fresh HRM database backup at `/home/amizo/erp-backups/20260925-employee-profile/hrm-before-profile.dump`.

The source `custom_id_number` values had the Zambian NRC format. Seventy-two unique values were populated into HRM NRC. Two employees (`HR-EMP-00005` and `HR-EMP-00028`) share a source NRC; their HRM NRC remains empty until HR verifies which record is correct. Frappe bank details and CTC remain in restricted legacy records pending account-holder and pay-basis verification. Source fields that were blank stay blank rather than being inferred.
