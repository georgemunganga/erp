# Procurement CSV private verification sources

These are the sanitized source files used for the private BE-01 Vendor and Item CSV acceptance. They contain no fixture, credential, database capture, CSV payload, browser result, or runtime receipt. They are a reference for reproducing the **same reviewed private cohort**, not a production launcher or a claim that a later product revision passed.

| Published file | Original private source SHA-256 | Change on import |
| --- | --- | --- |
| `verify_http.py` | `84c8ea6a1fdfc9fb93037a6a409207fa140b4484acbc49229d64fe49eecc66e0` | Byte-identical |
| `verify_owner.py` | `db92c04ee0c2fedfdf2a0399fc426a7314abe7a604b4e519fa51f7b108d5f74f` | Byte-identical |
| `verify_revocation.py` | `db9c668466c0a5e0aafdc7a82518170fe3166385bca5436a683b0bc33df64cc5` | Byte-identical |
| `test_source.py` | `d7e07bc343b1f170d2827c0672556b4ea8e15446b724d997f16f60a564d81919` | Byte-identical |
| `browser-export.spec.ts` | `72efd7828fc8749d6aa373a3686ba49608f20a6b1b8b813655724bced624d6fe` | Byte-identical |
| `playwright.be01-private.config.ts` | `84f654d7069e30af29ccfc02cef1244ab5da48ebb5c9d8d4371360831a5ee225` | Only `testDir` changed from the private absolute source directory to `.` |

The original source and superseded failed versions are retained under `/home/amizo/erp-backups/procurement-scoped-exports-acceptance-prep-20261025_run01`. Runtime fixtures, credentials, logs, SQL captures, and receipts remain outside Git under `/home/amizo/erp-backups/procurement-csv-export-runtime-prep-20261025_run02`. A separate reviewed launcher supplies the protected manifest, exact private PG identity, real local-password cookie hosts, loopback TLS proxy, and finite database roles. This directory intentionally does not create or seed a database.

The verified sequence was native HTTP exports and independent owner snapshots, a same-cookie current-role revocation with one Core CAS, then a separate normal-cookie browser export and owner reconciliation. The browser uses the still-authorized approver for Vendor after reader revocation and the manager for Item. Item All must return 422 at 10,001 rows; a real `BE01FIELD` search then enables View export. The browser proof hashes `Download.createReadStream()` bytes. Playwright's `Response.body()` may issue a second credentialed GET for a download and must not be used to count an export. Owner reconciliation checks the two generated audit rows and unchanged master data. The private result receipts, not these source files alone, establish execution.

Run `python3 test_source.py` for five pure parser and negative-control checks. A browser run additionally requires the accepted staged live UI, existing Playwright dependencies and browser binary, a protected `BE01_PRIVATE_FIXTURE`, `BE01_PRIVATE_CREDENTIALS`, and `BE01_PRIVATE_BROWSER_RESULT` prefix, plus the backend network namespace and reviewed loopback TLS transport. The Playwright config runs one Chromium worker, zero retries, and excludes retained `superseded*/` backups. It accepts only the private self-signed browser certificate; the launcher must separately bind the proxy certificate and source assets. No API mocks or identity headers are part of this proof.
