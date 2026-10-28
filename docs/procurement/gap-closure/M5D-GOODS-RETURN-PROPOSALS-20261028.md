# M5d — Accepted Goods return requests

Status: private return-proposal and write-off history acceptance verified; actual
return execution and production deployment remain open. Change owner: Root. Tier3 / GW-ENG-STD-001 applies. This supplements
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
Normal-cookie proposal CRUD and independently replayed native reconciliation
now pass. Production rollout and wider native UI acceptance remain open; no
Goods balances changed.

## Accepted native proposal and write-off history

The current API product `414ca94f` passed **148/148** focused host/Core tests,
no-pending-model and publication checks. A pre-runtime review fixed the final
receipt factory's missing explicit tenant/company fields; source guards remain
strict. The generated migration SQL is byte-identical to the accepted SDK02
rehearsal. No application business request preceded that correction.

Two normal-cookie API phases passed against the isolated native database.
The first performed Create/Revise/Cancel and original-key recovery, with scoped
All/other-actor/stale/quantity/key-conflict denials. Independent owner verification
confirmed one cancelled proposal, three revisions/receipts/outbox events and
nine audit rows, preserving all accepted Goods/source/PO/PR quantities.

The second used a fresh API with new writes disabled. Existing detail/list and
all three original command bodies stayed readable; a new Create returned503.
Independent owner verification confirmed all216 non-auth tables unchanged,
one revoked creator session and exactly attributed authentication effects.
All three cohort sessions across the phases are revoked. Both private API/proxy
pairs stopped, and the complete original role-table fingerprint was restored.
The limited private ACL additions remain documented in the clone.

The initial native attempt encountered a stopped local test proxy before login.
Its independent capture proved all220 tables unchanged and no business command.
The failed result is retained; a new, separately reviewed test run passed. Owner
comparison exempts only the exact cohort's verified authentication metadata;
unrelated retained sessions remain unchanged. No business command was replayed
to obtain owner acceptance.

A separate review agent replayed both owner verifiers and reproduced their
receipt bytes. This acceptance covers proposals/history, not dispatch, stock,
credit, Finance posting, full native UI, production or whole-module UAT.

## Published prerequisites

ERP PR127 merged as `33f08bf8585b5931fb07be5eb451a9b098a6749f`;
Procurement UI PR92 merged as `47cac6a13a24e9db113383f8a9cf5b319494d526`.
Start collaboration from current main descendants. Production remains unchanged.
