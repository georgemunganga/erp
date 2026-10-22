# Supplier known-person alias control

Status: source implementation; fresh backend, private PostgreSQL and browser release gates pending.

## Correction

Supplier decision permissions remain owned by the current active Core account, its current stored action grants and selected company/branch. An employee is optional. The optional HR projection `WorkerId` continues to mean an active, valid worker in the selected company for existing operational consumers.

`ProcurementRequestScope.CoreLinkedWorkerId` separately retains the current validated Core account's existing optional worker reference, normalizing an empty GUID to absent. It binds once with the verified principal and has no public setter. It is not an HTTP input, claim, response field, employment assertion, scope grant or approval authority.

For **new suppliers**, the existing nullable `ProposedByWorkerId` stores this genuine creation-time Core link when present, otherwise the existing validated HR projection. This is optional known-person alias metadata: it need not imply active HR status or an employee home in this company. The change adds no schema, fake worker, backfill or lookup of a historical maker's mutable current account. Older null original pins remain unknown.

A decision is denied when the acting subject created the supplier, or when a retained nonzero original worker reference matches either the acting current Core link or its validated HR projection. Two absent references never establish an alias. The shared guard covers detail action flags, return, approve, reject, qualification review, suspend and reactivate; current action permissions and all other transaction guards remain required independently.

## Authored evidence

- Existing seven mapped supplier alias/workerless cases remain intact.
- Thirty additional mapped cases cover all six guarded decision operations after inactive, archived, other-company or missing HR projection, plus creation while already inactive. They check Core context remains accessible with `workerId: null`, retained genuine creation reference, unavailable decision flags, HTTP403 and unchanged supplier/qualification/decision/audit/outbox footprint.
- Three scope tests cover bind-once/subject validation and zero-to-null normalization without populating the operational WorkerId.
- The actual private PostgreSQL CRUD probe adds creation during inactive HR using two existing Core accounts sharing the existing genuine worker reference. It checks actual context, persisted original reference, disabled flags, approve/return denials and independent before/after PostgreSQL footprint. Its private fixture identity/status changes are restored in `finally`; no grants or production identity handler changes are added.

No SDK/build/browser or actual probe has run for this source change yet. Release requires independent source review, the complete backend suite, fresh actual PostgreSQL/API probe, and retained workerless supplier browser flow against the newly published source.
