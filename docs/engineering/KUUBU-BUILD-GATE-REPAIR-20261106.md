# Kuubu Docker build gate repair

Scope: existing HRM/Core/Procurement packaging and regression fixtures. No business
API, permission resolver, native schema inspector, migration, production grant or
activation flag is changed. This does not certify shared SaaS tenancy or enable a
Procurement workflow.

## Observed failures

The original Docker context omitted Core projects referenced by HRM. Candidate
6798d909 adds that existing directory and reaches compilation, but the full HRM
test gate failed39 of1990 cases. A clean build from documented qualified release
e340a6f4 also failed39 of1972. Neither published an API image. These failed logs
remain retained independently from subsequent attempts.

| Failure family | Cause and correction | Preserved boundary |
| --- | --- | --- |
| Order/sourcing metadata503 | History-only fixtures omitted newly required plan/round services. Register disabled options and explicitly unavailable readiness adapters. | Existing metadata assertions remain; added checks require plan/round capabilities to stay false. No native schema readiness is fabricated. |
| Planning HTTP500/400 | Draft fixtures omitted plan readiness and the transaction dependency used by route binding, and valid requests omitted the now-required selected branch. | Only the in-memory draft store opts into draft reads. Review readiness remains false, execution options stay off, and the unused native transaction points at a closed local port. A missing-branch create must return400 without changing the business footprint. Exact-amount422, audit, revision, line-retention and stale-write assertions remain. |
| Identity SQL expectation | A whole-query assertion confused legitimate approval-engine DML with prohibited owning-role membership. | The Core identity no-write fragment still excludes approval DML, while the complete guard must retain approval schema ownership/membership/SET denial. No SQL guard changes. |
| Legacy admin readback | Expected grants omitted newer explicit-only plan, quotation and round actions from the exclusion list. | Every such action remains explicitly denied to the legacy administrator; session/readback equality and ordinary legacy permissions remain checked. No grant is expanded. |
| Noble runtime packaging | Actual ASP.NET Noble package policy reports no Chromium candidate. The API invokes only the standalone WeasyPrint CLI. Remove the unused Chromium install/symlink; preserve the existing WeasyPrint installation and native libraries. | No PDF renderer is replaced. A generated PDF from the final nonroot runtime is still required. |

## Evidence and operating limits

The isolated SDK runner uses10.0.403, matching the earlier Docker build. It owns a
new mutable working context and separate write-once receipts for each attempt.
No retained publication is changed or hardlinked to build outputs. SDK/container
cache is not a release artifact. No customer data or live database is used.

The first focused attempt caught a missing namespace import in the new helper.
After correction, the next attempt passed60 of64 focused cases; the remaining
planning cases exposed the missing branch selection. Subsequent results must be
recorded from completed process receipts, not inferred from these edits.

Focused attempt03 completed successfully:64 passed,0 failed,0 skipped,45 seconds.
It retains the existing validation/record/audit assertions and adds the explicit
missing-branch/no-mutation denial. Full HRM regression then passed1990 tests,
0 failed/0 skipped,6m52s, using the actual compiled source143c2503.
Full log SHA256:
`922172de1004ff8406b99996b940a1b9372cacc3da224c2cddeda9e4ec46ad03`.
Focused pass log SHA256:
`b873d64ad85e8271f425ad3472451a191e09e9fca399ecc1901fbc9de679b457`.

Hosted run42227118300 did not execute its backend steps. Its check annotation
states: "The job was not started because your account is locked due to a billing issue."
The hosted result is a platform failure, distinct from the passing isolated
server evidence. It is not described as hosted CI success.

Published main-line test fix is143c2503; Noble packaging fix is6da95d41. The
qualified-baseline candidate is6dc66ec548ca0c1ae9c2bb2ef06878ad78845335 on
`release/kuubu-baseline-20261106`. Its only differences from e340a6f4 are six test
files and the Dockerfile; application/business source is unchanged. Candidate
archive SHA256:
`b9958c8a3df40bd6faca98b69ba7dccc14329f15c403f279c8ab44d02ff327ba`.

The Noble package observation was made in the actual selected base image,
`mcr.microsoft.com/dotnet/aspnet:10.0-noble`, digest
`sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c`.
WeasyPrint's standalone native-library requirements are documented by its owner:
https://doc.courtbouillon.org/weasyprint/stable/first_steps.html.

The selected production-compatible executable remains distinct from main, whose
advanced B1c release holds stay in force. Passing this packaging gate does not
authorize wholesale main deployment, native financial activation, another
organization's access or an automatic invoice/payment/workspace side effect.
