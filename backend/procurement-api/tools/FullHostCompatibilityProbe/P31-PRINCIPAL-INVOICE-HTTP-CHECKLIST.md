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

Current source checkpoints: the route journey and finite privilege manifest
are authored but have not been compiled or executed. The P31 physical capture
is still empty; the owner must capture the genuine generated 13-table DDL and
rerun all readiness checks before this cohort can advance past its first
stage. The private document directory must be an existing 0700 child of the
protected result directory. No scanner socket is installed on this host, so
the entire positive cohort must remain held.

The source fixture must refuse any phase it cannot execute genuinely. A
private scanner socket must be supplied through `P31_HTTP_SCANNER_SOCKET` and
the Core document directory through `P31_HTTP_DOCUMENT_DIRECTORY`. The
current host has no installed or running ClamAV daemon, so positive P31
acceptance remains blocked until a private real scanner is supplied. Do not
insert a `Clean` verdict or replace the scanner/readiness service. A
tools-only synthetic transport test can test failure handling separately,
but cannot satisfy this actual HTTP profile. No part of this checklist
declares production scanning, Finance posting, or P31 live.
