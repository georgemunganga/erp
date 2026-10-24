# Private P31 principal invoice HTTP cohort (source checkpoint)

This is a new, exclusive opt-in on a fresh `proc_p31_http_*` database. It must
not run on a P30 database, reuse a browser fixture, or replace the normal
`IProcurementPrincipalInvoiceSchemaReadiness` provider with a positive stub.
P31 options remain false outside this profile; a complete PostgreSQL capture
and finite-role checks must make the normal host genuinely ready.

The authored probe uses the existing private Core-principal subject transport,
not a normal browser cookie. It exercises the actual Program routes and
finite PostgreSQL roles, then reloads owners from a fresh context. Separate
normal-cookie/browser evidence is still required for a UI release.

1. Admit the exact private PostgreSQL 17 socket/cluster, new empty database,
   root-owned current migration chain, then create distinct finite record and
   execution roles. Require normal host P25/P30/receiving/P31 metadata and
   concrete readiness. Keep Core actors workerless and current branch-scoped.
2. Through actual APIs, publish P25 direct-buy, P30 PO, receiving, invoice
   matching, and unit-review policies with independent maker/reviewer actions.
   Create a new approved GOODS request for quantity `1` at `100`, issue/ack
   its real PO, and record positive goods acceptances `0.25` and `0.75` through
   the real receiving workflow. The supplier is independently approved.
3. Capture a `Received` original invoice for `0.5` at `100`. Upload a synthetic
   PDF through the actual Core document route, retain its scanned Available
   version and hash. Review opening must atomically acquire one Core-owned
   `use_sets`/`use_pins` record through the shared fenced owner service; later
   decision/comparison/reads recheck that exact retained set. No direct
   document row or status insert, no fake document readiness, no broad
   invoice/PO read for reviewer. Revoking finite `use_pins` INSERT must close
   Open while leaving ordinary read readiness intact.
4. Map all invoice lines to issued PO2 and the first `0.25` acceptance using
   the exact idempotency key and mapping/state CAS. Reopen via the real named
   Core unit-review task; the independent reviewer downloads the pinned
   original, attests exact original units, and approves. Compare: persist a
   named discrepancy/owner and `ComparisonBlocked`, zero claims/Finance effects.
5. Record a correction request; remap with the second `0.75` acceptance,
   retaining old mapping/review/issues; open and independently approve a new
   unit review; compare to a qualified exact `0.5` claim. Verify exposure
   sequence, complete owner watermark, exact receipt bytes and GET recovery.
6. Run stale CAS, altered same-key payload, self-review, wrong branch,
   missing original/pin and ungranted action negatives. Capture full owner
   footprint before/after each denied command; no AP, posting, bank/payment or
   inventory stock effects. Fresh owner-context reload validates every source,
   review, comparison, claim, discrepancy, receipt and Core decision.

Current source checkpoint, 24 October 2026: the route journey and finite
privilege manifest are authored but P31 SDK, private PostgreSQL, normal HTTP
and browser evidence have not run. Genuine EF migration
`20261024014732_P31PrincipalInvoiceMatching` generated thirteen new tables,
three existing-parent unique keys and no existing column changes; its first
Down statement vetoes retained P31 data with SQLSTATE `55000`. It has not
been privately applied or rehearsed. The P31 physical capture remains empty,
so the normal provider and capability must remain unavailable even if options
are set. Capture the actual CHECK/FK/index/privilege definitions and prove
empty/partial/complete schema, all thirteen retained-family Down vetoes,
same-command Map/Open/Compare ordering, and owner reload before advancing.
The private document directory must be an existing 0700 child of the
protected result directory. No scanner socket is installed on this host, so
the entire positive cohort remains held.

Core Documents owns the original bytes, Available scan state, retained use
set/pins and download audit. Open must acquire exact `use_sets`/`use_pins`
through the existing Core owner service in the same enlisted serializable
transaction as its P31 review source/case/receipt. Decision, comparison,
purpose reads and recovery recheck those pins. Finite role admission must
prove Core use-set/pin INSERT for Open; revoking either denies Open without
partial effects. Ordinary read/recovery needs SELECT but no download-event
INSERT. Purpose download needs separate append-only `core_document.record_events`
INSERT and must prove its audit event without granting UPDATE/DELETE/TRUNCATE.
The assigned reviewer needs only current purpose scope, not general invoice,
PO, supplier or document administration rights.

The positive typed flow is Received invoice → first mapped acceptance →
independent original-unit review → blocked comparison with durable discrepancy
and zero claim → correction request → new mapping revision using the later
positive acceptance → new independent review → qualified exact claim. Preserve
both review histories, discrepancy actions, exposure cursor and exact
actor-owned recovery. This is a Procurement match and claim, not a Finance
handoff, AP posting, bank transfer or payment status.

The source fixture must refuse any phase it cannot execute genuinely. A
private scanner socket must be supplied through `P31_HTTP_SCANNER_SOCKET` and
the Core document directory through `P31_HTTP_DOCUMENT_DIRECTORY`. The
current host has no installed or running ClamAV daemon, so positive P31
acceptance remains blocked until a private real scanner is supplied. Do not
insert a `Clean` verdict or replace the scanner/readiness service. A
tools-only synthetic transport test can test failure handling separately,
but cannot satisfy this actual HTTP profile. No part of this checklist
declares production scanning, Finance posting, or P31 live.
