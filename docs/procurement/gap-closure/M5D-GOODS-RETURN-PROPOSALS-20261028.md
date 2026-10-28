# M5d — Accepted Goods return requests

Status: source and private migration verified; native return workflow acceptance
and deployment remain open. Change owner: Root. Tier3 / GW-ENG-STD-001 applies. This supplements
PROC-09's 28-section receiving contract and M5b/M5c evidence.

## Outcome and boundary

An authorised Core account can select a verified accepted Goods fact and create,
view, revise or cancel its own return request. A request is a proposal: it does
not dispatch goods, reverse acceptance, change invoice eligibility, create a
supplier credit, send a supplier notification, or post to Inventory/Finance.
These later transitions require separate execution and consumption contracts.

The original receiving screen supplies a contextual Returns tab and a link from
accepted Goods evidence. The editor retains the imported layout and ERP shared
controls; no replacement receiving screen is introduced. No mandatory employee
record is added. Core account permission and assigned-branch scope remain the
server authority.

## State, access and records

Draft -> revised Draft or Cancelled. Each command records an immutable exact
revision, actor-owned response receipt, audit event and internal outbox event.
The creator alone can read/revise/recover these drafts under current permissions
and branch access. A lost response is checked by its original command key; the
browser never automatically sends the write again. Changed-payload key reuse,
stale revision, unrelated actor/branch and malformed journal block the action.

Every draft pins tenant/company/branch, immutable acceptance fact and digest,
receiving source/digest, physical receipt HEADER, order/line/issued version and
accepted quantity. The accepted line ID is distinct from its receipt header.
The existing source verifier checks the complete issued/PR/Core chain and
receiving ledger. Historical labels derive from the issued snapshot and
physical observation. Draft quantity must be positive, exactly representable
and no greater than the selected acceptance; multiple proposals reserve no
quantity and must not be counted as executed returns.

Company All is allowed only for the reviewed collection and eligible-Goods
GETs. Detail/recovery and writes use a concrete permitted branch. Writes also
reject the raw All header when middleware supplies a default. Read/recovery
readiness remains independent of the new-work switch.

## Database and reliability

Genuine EF migration `20261028180000_AddPrincipalGoodsReturnDrafts` adds three
owned tables: drafts, revisions and command receipts. Scoped model keys and an
exact ownership overlay bind source, fact, receipt and the current revision.
Existing source tables receive only three named supporting owner keys.
The first Down statement locks the owned evidence and refuses any retained
return row or matching audit/outbox marker; it never discards business history.

Save guards reject rewrites of immutable evidence, source relabelling, orphan
revisions/receipts/events, changed cancellation quantity and a return proposal
transaction mixed with receiving or order/request mutations. Physical/finite
role readiness uses the configured execution identity on the actual serializable
transaction; no database-owner fallback or global grant is introduced.

## Acceptance and open work

Required: source/persistence rules, actor/scope/permission negatives, exact
receipt/header binding, read/write switch behaviour, mocked original UI forms,
uncertain-save recovery, genuine schema/no-pending-model checks, isolated
nonempty Up/empty Down/retained-marker veto, and native normal-cookie/API-owner
verification. Record actual passes and limits before claiming acceptance.

Full return execution remains open: independent approval, consumption custody,
verified dispatch, append-only negative facts, atomic accepted-counter effects,
replacement/credit links and downstream reconciliation. Matching likewise needs
versioned principal source links and a genuine complete exposure owner; an
empty local query is not proof that accepted goods are unconsumed. Finance/AP
and Inventory remain owners of their own ledgers.

## Verified source checkpoint

Procurement regression **1,501/1,501**, focused host/permissions **139/139**, and
Core role readback **9/9** passed. The first host run caught missing Core action
registration (117/119); that actual defect was fixed with explicit empty-bootstrap
grants. The Core role editor can enumerate these through its shared catalog.
EF reports no pending model changes and published the API.

UI TypeScript, **622/622** unit/contract checks, actor utilities **4/4** and the
production build (112 routes) passed. Focused browser tests passed38/39; the
Company All mock context had not reflected the newly selected branch. Its
fixture-only correction passed1/1. These are separate runs, not full native UI
acceptance or a single39-green run.

On a new isolated M5b clone, official Up verified the three tables,27 exact
constraints (3CHECK,12FK,4overlay keys,8modeled keys),19 exact indexes and all
column definitions. Old217table fingerprints were preserved, allowing only the
pinned history row. Empty Down restored the baseline; re-Up restored identical
catalog capture. An uncommitted audit marker blocked Down with SQLSTATE55000;
rollback removed it, leaving the same captured footprint. Source-clone rows
were unchanged. Separate review verified the bound SQL/manifest and receipt
hashes. This is not a source ACL/schema or finite-role runtime grant proof.

See the [verification manifest](M5D-GOODS-RETURN-VERIFICATION-20261028.json).
Normal-cookie proposal CRUD, independent native workflow reconciliation and
production rollout remain explicit admission holds; no Goods balances changed.
