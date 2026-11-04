# Core and Procurement workflow routing — production release

Released 4 November 2026. Source prerequisites: ERP PR193 (Core workflow directory), [ERP PR194](https://github.com/georgemunganga/erp/pull/194) (Core request routing), and [UI PR107](https://github.com/georgemunganga/module-connect/pull/107). Published ERP main at release: `b291b64e790707ff259f0d65d6330a0f6a522186`; Procurement UI main: `0b1143a31fcdf3bc12d9e558f79614e08bc280b4`.

## Deployed artifacts

- API: accepted c93 baseline plus the Core directory and M2 request routing. Runtime source `30bba4c4` on published `release/core-routing-c93-20261104`; verified release descendant `e340a6f46ada8b2402b04eea8a7734c67004f348` changes only the metadata test fixture. Image `sha256:3da35edf636057662ef30918399397e4a82fc928878fe23ec471a7f08b20293f`; container `9674e99096b9de2f29f5bc72029b5582963b406ea6895b5fc1458dbf4ff00c7f`. Seven changed executable/symbol/manifest files exactly verified. Unchanged dependencies were source-qualified against c93 and copied independently from its exact deployed image; changed projects were compiled afresh. This is an incremental qualified build, not a clean dependency rebuild.
- Core frontend: product `dd7e96b4cfe98051194e3c5709f87b10cd67aa77`, unchanged in M2, built from the published ERP source. Runtime `/home/amizo/erp-core-workflow-routing-release-20261104/.output/server/index.mjs`, via systemd `40-core-workflow-release.conf`.
- Procurement frontend: product `12633f91733f5eae355d1377d6ba9194461910a8`, `/home/amizo/procurement-ui/releases/core-routing-12633f91-20261104`. Original imported layouts and ERP controls retained. Prior public assets were independently copied before publication to preserve open browser sessions. Existing retained assets/publications were not modified or hardlinked.
- Actual production database remains **239 tables**. No migration, database grant, company policy, user role, spending authority or business activation flag changed. Pending advanced-sourcing/B1c schema is excluded from this backport.

## Actual acceptance evidence

| Check | Result | Qualification |
| --- | --- | --- |
| Main-source backend | 2,033 Procurement +49 focused Host pass | Local SDK; prior failed runs retained |
| Deployed c93 backport | 2,028 Procurement +49 focused Host pass | Actual separate backport compilation/tests; fewer cases reflect excluded B1c |
| Procurement UI unit checks | 984 /90 files pass | Existing +new UI contracts |
| UI TypeScript and both production frontend builds | Pass | Procurement live-mode static; Core Node SSR |
| Workflow browser tests | 21 unique cases pass across runs | 17 existing +4 new; mocked APIs, not signed-in company acceptance |
| Read-only live-database candidate | Both health checks200; four protected API endpoints401 anonymously | Every database connection forced read-only; import maintenance disabled only in second canary; production flags preserved |
| Deployed public file hashes | 266 /266 pass | All46 Procurement product/compatibility files and220 new Core public files |
| Deployed actual Chromium | 17 /17 pages pass; zero page errors | Anonymous; Core, HRM and Procurement routes including new workflow entry/settings |
| Production API/config | Healthy; exact seven changed files; original HostConfig and application/business settings retained | Only new image/source metadata differs |

Earlier canary1 health was unhealthy because the enabled cleanup task could not write through the intentionally read-only connection; the second read-only canary disabled only its maintenance task and passed. The first frontend check saw the old HTML immediately after nginx reload and automatically restored the old frontend; a bounded exact-asset retry succeeded. The first file-verification attempt ran during that restored interval and failed as expected. These attempts remain retained separately. The c93 metadata fixture initially missed an existing barrier interface member; corrected fixture then passed all49 selected cases. None of those attempts is labelled as a passing run.

Private evidence: `/home/amizo/erp-backups/core-routing-release-20261104_run01`; release index SHA256 `a7c687b2bb9f23907fd14e31c28310213d4379ea54d81d3f64daa7e00ab180dc`. M2 source evidence is separately retained at `/home/amizo/erp-backups/procurement-core-routing-m2-20261104_run01`. Credentials/cookies/customer records are excluded from these public notes. Hosted CI for PR194 was still queued at release; no hosted-green claim is made.

## Rollback and operations

API rollback container `erp-hrm-api-core-routing-rollback-20261104` remains stopped and retained, with its exact c93 image/configuration. Stop/rename the new container before restoring the old name and starting it; verify readiness before traffic. Restore saved nginx configuration for Procurement. Remove only the new `40-core-workflow-release.conf`, reload systemd and restart the web service for the prior Core frontend. No database rollback is required. Protected native publications, private databases, rollback images, document storage and the outbox publisher are retained. Owned SDK/read-only canary containers and the3310 frontend canary were stopped/retired after evidence capture.

## Owner-launch verdict

**This milestone is deployed; the whole Procurement module is not accepted as complete.** Approvals/direct orders/manual payments remain in their previous controlled activation state. Actual company approvers, finite limits, approved business policy/funding process and signed-in end-to-end acceptance must be supplied/verified before activation. General PO/request-backed advance payments without an invoice remain an implementation gap; do not fabricate invoices. Department head/employee pointers do not themselves grant ERP authority. Wider issue145 and owner-launch190 remain open. See the [company configuration and UAT guide](../operations/COMPANY-APPROVAL-SETUP-AND-UAT.md).
