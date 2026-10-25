# P37 Goods Return Draft private HTTP hook

**State:** The current API passed26 focused and1344 Host tests; its coherent
API/tool publication passed. Native01 failed after47 HTTP observations at the
predecessor P30 readiness gate, before any P37 operation. Independent read-only
inspection found that the private PO fixture lacked SELECT on all three retained
P33 order-history tables required by the existing P30 guard. The fixture now
requests only those three reads for its PO profile; application guards and write
grants are unchanged. A new genuine tool build and fresh native cohort are still
required. The failed database, logs and result remain preserved. Both flags and
ordinary current actor admission remain required.

Native02 passed165 actual HTTP observations through purchasing and receiving,
then stopped at the probe's direct P37 readiness call outside HTTP middleware.
That DI scope had no resolved Procurement identity, which the real execution
transaction requires. The probe now checks concrete registration separately and
requires availability through the real scoped `/meta` request. Read-only
inspection found the P37 migration, captured constraints and finite grants
present. Application readiness and authority checks remain unchanged; a fresh
tool build and Native03 are required before claiming a P37 operation.

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
actions to the workerless receiver, read/edit to a separate same-branch
noncreator, and scoped read only to an outsider at a different branch. It adds SELECT/INSERT on the three P37 owned tables and only
the six declared head UPDATE columns to the existing finite execution role.
It sets `Procurement:PrincipalGoodsReturnDrafts:Enabled=true` only for its
exclusive P37 opt-in; the normal composition derives
`ExecutionDatabaseConfigured` from the actual execution connection.
This combined cohort necessarily gives that role the P30 rights needed to
create the original acceptance; it **does not** prove an independent P37 role
can run with P30 source READ alone. That least-privilege positive/negative grant
matrix is a separate finite-role proof.

The merged source registers the concrete provider in normal Program composition,
with the option default false; mapped routes fail closed through ReturnRun before
any operation when options/readiness are absent. The new availability metadata
uses only the actual concrete provider, finite execution store and captured
physical checks. It does not authorize a record action. This private opt-in
requires the normal concrete provider's IsConfiguredAsync before the first P37
POST and never substitutes a ready provider. Source integration does not prove
that actual configured readiness or HTTP will succeed; a current product/tool
compile and fresh cohort are still required.

## Actual HTTP assertions authored

- Current workerless Core receiver sees the accepted Goods fact in the scoped
  eligible selector. A read-only other-branch actor cannot see the Draft; an
  ungranted actor cannot create it. Verified eligible/list/detail labels bind
  original issued supplier/PO line and actual observation note/time.
- Create returns an internal Draft with no dispatch/stock/Finance flags. Exact
  Create POST replay retains its 201 body and GET command recovery returns
  the original body. A reused key with changed quantity conflicts.
- Scoped list/detail and durable Revise 0.5 to 0.75 are read back. Revise
  replay/recovery retain the 200 body. Over-fact quantity and stale CAS deny
  without any row effect.
- A same-branch different creator with Read/Edit can read the verified Draft but
  receives no Edit/Cancel hints and cannot revise it (404). Removing the original
  creator's current Core Edit grant keeps its read grant and GET detail available,
  returns an empty allowedActions array, and denies a new edit and recovery of
  its prior Edit receipt. The fixture restores exactly that grant afterward.
- Cancel retains 0.75 and its evidence reference. Its replay/recovery and
  final scoped detail has no actions. All raw equality checks are confined to
  original acknowledgement replay/recovery; current GET views are compared by
  typed source/state/version/quantity fields and labels. Original acknowledgements
  must contain neither dynamic actions nor labels. Fresh owner read finds one head, three
  immutable revisions, three command receipts and three metadata-only outbox
  messages with all external-effect flags false.
- A repeatable-read full-row hash of every non-P37 business table, excluding
  only the owned P37 rows, their audit/outbox companions and the exact controlled
  `p30_receiving_actor` fixture Core role-grant row, remains equal before and after the Draft
  lifecycle. This checks P30 source/counters plus any Inventory/Finance rows.
  The separate owner verifier must additionally bind exact original row IDs,
  immutable snapshots, actor/command keys, audit/outbox and role-grant restoration.

## Remaining acceptance gates

1. Compile/test this combined source and publish a coherent current product/tool
   graph. Existing SDK04 acceptance predates these integration changes.
2. Run the fresh actual native cohort with the real provider and separate
   current Core principals, then independent read-only persisted owner proof.
3. The accepted private finite SQL matrix is not an application/login/HTTP proof;
   this combined P30 setup role retains predecessor setup writes and does not
   replace the narrower source-read P37 runtime acceptance requirement.
4. No normal-cookie/Returns UI run or production authorization is established.

No Inventory movement, supplier communication, credit note, Finance posting,
return approval, dispatch or production release is claimed by this Draft hook.
