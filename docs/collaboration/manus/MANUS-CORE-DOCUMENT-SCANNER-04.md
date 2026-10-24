# MANUS-CORE-DOCUMENT-SCANNER-04 — isolated ClamAV scanner verification

**Status:** `READY_UNACKNOWLEDGED` — ready for an available Manus worker to claim. No implementation acknowledgment or scanner result has been observed in the checked published task/progress records or ERP PRs as of 24 October 2026. This records the coordinator’s evidence search; it does not establish whether work has started elsewhere. Keep `MANUS-CORE-BUILD-03` and its owned files separate. Use a separate branch/checkout on adequate non-production hardware and serialize heavy jobs on any shared host.

**Minimum published starting point:** a fresh checkout of published ERP `main` at `1657398e865245bc03ec39efbdf97766b0b9dd71` or a later reviewed published descendant. Record the exact checkout SHA and the reviewed image digest. This document is additive; it does not authorize cherry-picking the coordinator's held Procurement/P30 branches or changing `MANUS_HANDOFF.md`.

**Outcome:** On separate non-production hardware, demonstrate that the existing `ClamdDocumentScanner` correctly handles a real official ClamAV daemon and current official signature databases through its Unix socket. Produce portable, independently reviewable evidence for a clean synthetic file, the official EICAR test file, failed scans, content integrity, definition freshness and definition changes. This is a Core document scanner adapter milestone. The actual Core Document HTTP/API activation and any Procurement invoice document release remain coordinator-owned follow-up gates.

## Ownership and pickup

Claim this task in a focused draft ERP PR titled `MANUS-CORE-DOCUMENT-SCANNER-04` and record the exact published main SHA. Own the scanner harness, runbook and `docs/collaboration/manus/scanner-04-progress.md`. A focused adapter/protocol fix is allowed when actual results demonstrate a defect; preserve the public `IDocumentScanner` semantics and fail-closed behavior, add the relevant test and explain the change for independent review. Do not edit the current Core frontend assignment or shared handoff/status files. The coordinator reviews and merges; do not deploy or activate uploads.

## P31 dependency and requested worker report

This is the highest outstanding Core prerequisite for P31’s genuine positive invoice-document HTTP cohort. P31 now has independently reviewed private physical-schema evidence, but that cannot establish a real clean scan, fresh definitions, authorized download or invoice workflow acceptance. The coordinator continues P31 finite-grant, consumer, recovery and private owner verification independently; no held Procurement branch is needed to complete this Core task. The separate Core frontend build task does not need to finish before a scanner worker starts on separate hardware.

An available worker should acknowledge pickup in a focused draft ERP PR and `scanner-04-progress.md`, then report the exact source SHA, official image digest, actual signature update/serial/time, private socket ownership/mode, configured limits, commands, first failures, structured real verdicts and protected evidence manifest. Link completed/unproven cases to the acceptance table below. Report an explicit hardware, image, update or socket blocker if execution cannot proceed. Do not mark a worker assigned/running or the task accepted from this status note, a task-queue merge, fake-socket unit tests, a cached historical scanner README or an unverified external claim.

The worker’s real-daemon evidence remains subject to distinct review. The coordinator owns later Core Document HTTP composition and P31 execution; neither this task nor its results authorize production configuration, roles, data or uploads to change.

## Existing contract and boundaries

- Reuse `backend/core-api/src/Mightyfin.Erp.Core.Documents.Infrastructure/ClamdDocumentScanner.cs` and its `IDocumentScanner` contract. The published adapter accepts an absolute Unix socket path, bounded timeout and definition age, sends `zINSTREAM`, hashes the streamed bytes with SHA-256, checks exact length/hash, obtains `VERSION` before and after, and returns `Clean` only for a complete `stream: OK` reply with unchanged fresh definitions. It returns `Infected` for a bounded `FOUND` reply; malformed, unavailable, timed-out, stale or changed-definition cases fail closed.
- The present invoice-document host wiring at `backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/ProcurementInvoiceDocumentRoutes.cs` supplies UTC, a 30-second timeout and a 48-hour maximum definition age when `CoreDocuments:Enabled` is configured. The direct adapter test must not imply that this HTTP route or document storage is ready in production.
- Existing fake-Unix-socket unit tests in `ClamdDocumentScannerTests.cs` remain useful for malformed protocol and deterministic definition-change cases. Report them as synthetic protocol tests, separate from real-daemon results. Do not substitute a fake `Clean` response for real ClamAV evidence.
- Do not edit UI, permissions, workflow, supplier/invoice behavior, document owner rules, database models/migrations, production configuration, or production files. Do not send customer documents or real malware to the test daemon. No production deployment or production scanner switch is part of this milestone.

## Isolated hardware and pinned daemon

1. Use an isolated non-production host with a recorded OS/kernel, CPU, available memory/disk and container runtime. The [official ClamAV Docker guide](https://docs.clamav.net/manual/Installing/Docker.html) notes substantial signature memory use and recommends configuring **4 GB RAM** for its container. Verify real headroom before pulling or starting it; do not contend with the coordinator's constrained ERP build/database host.
2. Choose an official `clamav/clamav` release image and record the exact immutable image digest and image provenance before use. A moving `latest`/`stable` tag alone is insufficient evidence. Record container configuration, mount paths, image digest, engine version and final test SHA without printing registry credentials. Use an isolated signature volume; no business document volume is mounted into ClamAV.
3. Obtain and update official signature databases with FreshClam. Record its update outcome, database serial and timestamp from the daemon's `VERSION` response, and the configured UTC interpretation. Keep the signature volume separate from the socket directory. A preloaded image alone does not prove fresh definitions. [ClamAV's FreshClam documentation](https://docs.clamav.net/manual/Usage/Configuration.html) describes its update role.
4. Expose only a local Unix socket to the direct test process. Make the socket directory private and grant the test identity only the required connect access. Do not publish ClamAV TCP ports; the [official ClamD protocol documentation](https://docs.clamav.net/manual/Usage/ClamdProtocol.html) confirms Unix-socket `INSTREAM` support and that TCP transport lacks built-in encryption/authentication. Record actual socket ownership/mode and the resolved absolute path without exposing secrets.
5. Set daemon `StreamMaxLength` at least as high as each tested file, while keeping file sizes inside the adapter's 100 MiB absolute cap and the configured document limit. Record the reviewed values and test file sizes. Do not silently treat a ClamD size-limit error as a clean scan.

## Direct test sequence and expected results

Use only generated harmless test content and the [official EICAR Anti-Malware Test File](https://www.eicar.org/download-anti-malware-testfile/). Keep the EICAR file on the isolated host; do not commit its bytes or use real malware. Exercise the public `IDocumentScanner.ScanAsync` method against the real pinned daemon/socket and save structured outcomes without file contents.

| Case | Evidence and required result |
|---|---|
| Fresh clean synthetic file | Real daemon returns `Clean`; observed SHA-256 equals the independently computed input hash, exact byte count is consumed, and engine/definition evidence is present. |
| Official EICAR file | Real daemon returns `Infected`, never `Clean`; retain the signature label only if bounded and non-sensitive. |
| Missing or inaccessible socket; daemon stopped | `Failed` with an actionable unavailable/I/O code; no content becomes available. Restore daemon, then repeat clean and EICAR to show recovery. |
| Timeout and invalid/size-limit reply | `Failed`, never `Clean`. Use a controlled protocol fault fixture where a real daemon cannot deterministically produce the reply; label that evidence synthetic. |
| Wrong expected SHA-256, short stream, extra bytes | Against the real daemon, each mismatch returns `Failed` and does not release the content, even if the daemon would otherwise answer `OK`. |
| Missing, malformed, zero, more than 15 minutes future or stale definition evidence | `Failed` before clean release. The existing contract permits up to 15 minutes of future clock skew; verify that boundary rather than changing the policy. Test stale time using a controlled `TimeProvider` with a real daemon, and label the injected clock. Keep the current bounded freshness policy explicit. |
| Definition serial/version changes between the two `VERSION` calls | `Failed` with `scanner-definitions-changed`, never `Clean`. Prefer a controlled real FreshClam reload while scanning if it can be reproduced safely; otherwise preserve the deterministic fake-socket unit result and mark the real-daemon change case **unproven**. Do not claim it passed from ordinary clean/EICAR scans. |
| Definition update and repeat | After a real FreshClam update/reload, repeat clean and EICAR; record both pre/post daemon version strings and times, with no assumption that a higher serial alone authorizes release. If official definitions are already current, record the verified no-op and mark the real change case unproven. Never downgrade or fabricate signature timestamps to manufacture a change. |

The adapter's existing fake-socket tests should also remain green for truncated, duplicate, overlong and ambiguous responses, caller cancellation and bounded timeout. A direct daemon pass does not replace those negative tests.

## Reproducible run and evidence

Prepare a short runbook and a test harness in a separate isolated checkout. Pin the source commit, official container digest, signature database state, scanner options, synthetic file hashes/lengths and test commands. Use one scanner process/worker at a time unless the hardware profile explicitly supports more. Keep commands and evidence paths portable through documented environment variables; never hard-code production paths, customer identifiers or credentials.

Store a protected evidence bundle with: hardware/container inventory, image digest, sanitized FreshClam/ClamD startup and update logs, socket ownership/mode, option values, source/fixture hashes, test command/exit status, verdict/failure code/engine/definition serial and timestamps for each case, plus a SHA-256 manifest over the evidence files. Do not include document bytes, EICAR bytes, auth tokens, personal data or unrestricted daemon logs. Retain first failures and their corrections; do not overwrite failed runs. Distinguish real-daemon, injected-clock and fake-protocol cases in every result.

## Review and completion gate

Manus should submit a focused draft ERP PR and protected evidence reference after rechecking the published baseline. A distinct reviewer must verify the official image/signature provenance, Unix-socket isolation, actual direct adapter verdicts, integrity/freshness negatives, resource use, failure recovery and evidence hashes. Report unproven cases explicitly. The coordinator owns any later Core Document API activation and release decision. This task is complete only when the reviewed direct-scanner evidence meets the table above and the original adapter contract remains intact; it does **not** certify production document uploads, permissions, migrations, invoice workflows or deployment.

A real reload-race case may remain explicitly unproven if official definitions are already current or the race cannot be reproduced safely. Its deterministic protocol regression remains required. Actual clean/EICAR scans, integrity/freshness failure cases, isolation and recovery evidence are mandatory; do not replace them with simulated verdicts.
