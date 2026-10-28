# M5b receiving — verified private Goods slice

Status: merged; private Goods API acceptance passed. Not deployed and not whole-module UAT sign-off.

## Published baseline and ownership

ERP PR125 merged as `3bf6a65443240b68cc5283b178f29050981d9832`.
Original UI PR91 merged as `086ee259c269a46e905bf76532532119609bc1db`
in `georgemunganga/module-connect`. Root owns receiving follow-up. The two
merged M5b remote branches were removed after exact-tip, ancestry and open-PR
checks; commits and local history remain available.

The existing PROC-09 28-section specification remains the business reference.
Core owns accounts, permissions, organisational scope and company calendars.
Employees are optional aliases. Procurement owns receiving evidence and
acceptance decisions; Inventory, Assets and Finance retain their ledgers.

## Implemented behaviour

- Six receiving tables retain sources, facts, drafts, revisions, command receipts
  and line bindings. Issued-order authority is reverified before adoption.
- Genuine EF migration `20261028160000_AddPrincipalReceivingExecution` includes
  eleven deferred ownership constraints and five supporting parent indexes.
  Down locks and refuses retained graph/audit/outbox evidence; it never resets
  balances or deletes business history.
- API commands support draft creation/revision/cancellation, physical receipt,
  inspection and independent Goods acceptance. Current Core permissions and
  selected branch, version checks, exact-decimal capacity, audit/outbox and
  original-key recovery are enforced server-side.
- Five reviewed collections support Company All. Writes require an explicit
  permitted branch, including when middleware supplies a default branch.
- Read/recovery readiness is independent of the new-work switch. Configured
  and actual enlisted database connections undergo readiness checks.
- Original receiving screens, settings forms, ERP controls and layouts remain.
  Core-account schema2 policies coexist with retained schema1 employee policies.
  The session-scoped uncertain-save journal checks original commands using GET;
  it never silently reposts a write.

## Verified evidence

Backend source `6b8553afdf4d2a7db22fc763a018012ddb05c61f` passed
**1,478/1,478** Procurement regression tests, **58/58** focused host tests and
**27/27** focused receiving tests. EF reported no pending model changes,
generated official Up/Down SQL and published the API (150 files).

On an isolated nonempty clone, Up verified all six table/CHECK shapes, eleven
ownership bindings and five indexes. Empty Down restored all 211 original
per-table row fingerprints; re-Up restored the captured receiving footprint.
An uncommitted audit-only marker produced SQLSTATE55000 and prevented Down;
disconnecting rolled the marker back. This covers the audit-only veto, not
every possible retained business-graph veto.

UI source `0145bce0828c954d9f035ca706bcb7b3ebf55b8a` passed Node22 TypeScript,
**607/607** unit/contract tests and the production build. **27/27** mocked
receiving browser cases passed; settings passed **6/6** after correcting a
test-only custom-Select assertion. These separate runs are not a single
33-green invocation or a full444-scenario rerun.

A real normal-cookie API test completed policy create/review/publish and
Goods Create/Revise/Record/Inspect/Accept, original-key recovery, denied access,
stale-version and overcapacity checks. Independent read-only reconciliation
accepted five command receipts, three typed facts, one issued source chain,
exact counter/net deltas, policy identity lineage and audit/outbox effects.
Original failure evidence remains: five authentication LastSeenAt audits were
accepted only by a constrained successor proving exact session identity,
changed-field scope and the published five-minute refresh behaviour. No
business commands were replayed to obtain acceptance.

All five test sessions were logged out, zero active cohort sessions remained,
the private API/proxy stopped and three temporary role fixtures were restored.
The complete role-assignment fingerprint matched its original baseline.
Private clone ACL additions remain recorded for reproducibility. See the
[verification manifest](M5B-RECEIVING-VERIFICATION-20261028.json).

## Remaining work and release boundary

M5c addresses company-local service-period validation and retained date
observations. Real Service acceptance, corrections/returns, the complete live
UI journey, all branch/tenant combinations and downstream integration remain
separate acceptance work. No Finance/Inventory posting or payment execution is
implied by a receiving outbox event.

This evidence does not establish every function/default/global privilege,
production readiness or whole Procurement UAT. Production remains M4;
rollout requires current-row preflight, a recoverable backup, finite grants,
paired API/UI publication and post-deploy smoke checks. Synthetic test policies
and roles are not production configuration.
