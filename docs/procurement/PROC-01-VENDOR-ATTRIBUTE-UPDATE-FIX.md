# PROC-01 — persist editable vendor attributes

Status: source candidate; tests authored, not executed. Based on released-P19-compatible ERP main `cea6e5d`. No migration, deployment or Finance authority.

1. **Feature identification:** Vendor draft update, existing `PUT /api/procurement/v1/vendors/{id}`; fix Attributes previously accepted but silently ignored.
2. **Purpose:** A saved custom field or reporting tag survives reload with the requested value.
3. **Scope/exclusions:** Existing SupplierAttribute only; no Core metadata engine, bank/payment data, document uploads, approval rules or UI replacement.
4. **Parent:** PROC-01 supplier management, PROC-BE-03 CRUD and original seven-tab vendor editor.
5. **Actors:** Existing employee-backed vendor editor/manager, under current permissions; workerless administrator remains view-only.
6. **Entry:** Existing create and versioned update bodies, then existing detail GET.
7. **Preconditions:** Current tenant/company scope, editable owned/permitted vendor, current parent Version, valid complete header and attributes.
8. **Workflow:** Validate entire body → scoped vendor/reference checks → update header and keyed attribute patch → one SaveChanges → reload.
9. **States:** Existing Draft / More Information Required editable states only. Submitted/approved/active records remain protected.
10. **Permissions/scope:** Existing vendor-edit and record-owner/manage checks; no new grants. Child lookup is through the scoped owning supplier, never supplied child IDs.
11. **Rules:** Only exact `CustomField` and `ReportingTag` kinds; trim keys, require1–120characters; maximum100supplied attributes; reject duplicate trimmed `(Kind,Key)` before changing anything. Existing case-sensitive key semantics stay unchanged.
12. **Data/ownership:** Procurement retains SupplierAttribute ID/history. Existing keys update only FieldValue; new keys receive server-generated IDs, authenticated scope and parent SupplierId. No row is removed or moved between suppliers.
13. **Screens/API:** Existing vendor detail returns the persisted attributes and child Version; update response returns new parent Version. Existing editor can send the same attribute contract.
14. **UX:** Preserve entered values on validation/conflict; absent metadata must never erase fields the editor does not own.
15. **Communications:** Existing API validation/conflict responses; no new notification or success on a failed write.
16. **Documents:** No upload, qualification or document mutation.
17. **Edges:** Null list, omitted list, empty list, duplicate/long keys, unsupported kinds, null/blank value, stale Version, foreign company/record, extra client child ID, submitted supplier.
18. **Integration contract:** `Attributes` is a **keyed patch, not full replacement**. Omitted/null/`[]` leaves all existing keys unchanged. Supplied exact `(Kind,trimmed Key)` updates/adds that key; null/blank Value explicitly stores an empty value while retaining row identity. Extra JSON `id` is not a DTO field and cannot select/move an attribute. Whole-list deletion/retirement is unsupported by the existing schema and must not be inferred.
19. **Audit/security:** Child-only edits advance parent CAS. Existing append audit records capture supplier/attribute changes in the same SaveChanges transaction. Approved evidence is neither deleted nor rewritten. No Finance balance/verification is created.
20. **Reports:** Existing detail read only; no new reporting authority.
21. **Configuration:** No settings, policy or migration required. Core company/currency remains authoritative.
22. **Nonfunctional:** At most100patch rows; reuse existing child collection/unique index and optimistic concurrency. No new service or store.
23. **Postconditions:** Reload returns updated values and retained IDs; omitted keys and other suppliers remain unchanged.
24. **Failure/recovery:** Validation occurs before mutations. Existing stale/DB conflict behavior remains. No new idempotency receipt; a lost response stays uncertain and requires authorized read-only reconciliation, not automatic retry.
25. **Acceptance:** Create→update→reload; new/existing keys; child-only CAS; omission/null/empty preservation; explicit clearing; whole-request rejection; cross-scope/owner denial; submitted-state protection.
26. **Tests:** Dedicated partial `ProcurementVendorAttributeHttpTests.cs` reuses the existing mapped TestServer/ScopeMiddleware fixture, synthetic outer authentication and InMemory persistence. It does not prove PostgreSQL constraints, real account authority, browser UX or production persistence. Parent runs builds and actual CRUD/browser gates separately.
27. **Ownership:** Backend agent owns VendorRoutes + dedicated tests/this note. Manus owns UI editor; no overlapping frontend edits. Coordinator owns verification, commit and release.
28. **Decisions:** Explicit patch semantics accepted by coordinator. Retained attribute removal would require a separate versioned schema/contract; do not use physical deletion, magic tombstones or empty-list replacement to simulate it.
