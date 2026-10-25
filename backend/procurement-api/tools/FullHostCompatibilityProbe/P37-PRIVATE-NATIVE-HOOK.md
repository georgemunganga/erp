# P37 Goods Return Draft private HTTP hook

**State:** Source only, uncompiled and unrun. The accepted joint P36/P37 SDK04
tests are not a P37 HTTP result. This hook is held until genuine P37 physical
capture, finite role proof and ordinary Program composition are reviewed.

## Exclusive profile and prerequisite graph

Set `P37_HTTP_VERIFY_GOODS_RETURN_DRAFTS=1` only with the existing real
`P30_HTTP_VERIFY_PRINCIPAL_PO=1` and
`P30_HTTP_VERIFY_PRINCIPAL_RECEIVING=1` on a fresh private
`proc_p37_http_return_<bounded-suffix>` database under the allowlisted private
PG17 Unix socket. No RFQ, invoice, browser, retained-cancel or other optional
cohort may share it. The probe uses the P30 native API to create an approved PR,
active supplier, issued manual-ack PO, receiving policy, actual physical Goods
observation/inspection and two actual Goods Acceptance facts. It selects the
retained 0.9999 acceptance fact by owning source/PO line. Owner-only reads select
the fact ID; every positive P37 transition uses the HTTP routes.

The probe-only fixture adds explicit current Core return read/create/edit
actions to the workerless receiver and scoped read only to an outsider at a
different branch. It adds SELECT/INSERT on the three P37 owned tables and only
the six declared head UPDATE columns to the existing finite execution role.
This combined cohort necessarily gives that role the P30 rights needed to
create the original acceptance; it **does not** prove an independent P37 role
can run with P30 source READ alone. That least-privilege positive/negative grant
matrix is a separate finite-role proof.

Current production composition deliberately registers neither the P37 provider
nor its routes. P37 capture dictionaries are empty and the default option is
off. With this flag, the probe demands the normal host's concrete
`ProcurementPrincipalGoodsReturnStoreReadiness` and its actual configured
physical/finite readiness before any P37 POST. An absent provider or missing
physical capture is a controlled failure, never a substituted ready result.
Root must later review and wire `ProcurementPrincipalGoodsReturnComposition`
and `MapReturnRoutes` only after genuine joint schema/ACL acceptance. The probe
must then be rebuilt against that exact current API product graph.

## Actual HTTP assertions authored

- Current workerless Core receiver sees the accepted Goods fact in the scoped
  eligible selector. A read-only other-branch actor cannot see the Draft; an
  ungranted actor cannot create it.
- Create returns an internal Draft with no dispatch/stock/Finance flags. Exact
  Create POST replay retains its 201 body and GET command recovery returns
  the original body. A reused key with changed quantity conflicts.
- Scoped list/detail and durable Revise 0.5 to 0.75 are read back. Revise
  replay/recovery retain the 200 body. Over-fact quantity and stale CAS deny
  without any row effect.
- Removing the current Core Edit grant denies a new edit and recovery of its
  prior receipt. The fixture restores exactly that grant afterward.
- Cancel retains 0.75 and its evidence reference. Its replay/recovery and
  final scoped detail remain stable. Fresh owner read finds one head, three
  immutable revisions, three command receipts and three metadata-only outbox
  messages with all external-effect flags false.
- A repeatable-read full-row hash of every non-P37 business table, excluding
  only the owned P37 rows, their audit/outbox companions and the exact controlled
  `p30_receiving_actor` fixture Core role-grant row, remains equal before and after the Draft
  lifecycle. This checks P30 source/counters plus any Inventory/Finance rows.
  The separate owner verifier must additionally bind exact original row IDs,
  immutable snapshots, actor/command keys, audit/outbox and role-grant restoration.

## Remaining acceptance gates

1. Apply the genuine P36/P37 migrations to a fresh isolated PG clone, capture
   the exact canonical P37 CHECK/FK/index definitions, and populate the
   owning readiness dictionaries. No model text is a substitute.
2. Prove a finite role with P30 source READ, the exact P37 own-write column
   matrix and denied immutable/source/Finance rights. This combined P30 setup
   profile alone cannot prove that narrower role.
3. Register normal Program composition and routes behind the reviewed
   default-off option; compile/publish the current product graph, then run a
   fresh native cohort and independent read-only owner reconciliation. Preserve
   first failures and never reset a failed database.

No Inventory movement, supplier communication, credit note, Finance posting,
return approval, dispatch or production release is claimed by this Draft hook.
