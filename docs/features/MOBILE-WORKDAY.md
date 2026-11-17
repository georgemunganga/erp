# Vcron workday basics — staged implementation

## 1. Purpose
Record explicit employee breaks, departures, returns and work reports through the
existing HRM owner, preserving current ERP check-in/out and payroll behaviour.

## 2. Classification and authority
Tier3 employee data. Existing verified mobile identity, explicit employee/company
binding and current module/branch access remain mandatory at every read/write.

## 3. Actors
Employee declares their own activity; existing company administration owns access.
No mobile request chooses another employee, tenant, principal or location grant.

## 4. Initial scope
Manual break start/end, departure with reason, return, and a self-reported work
summary/blockers. Reuse existing attendance record IDs. No automatic checkout,
automatic absence decision, payroll deduction or device-management enforcement.

## 5. UI journey
Check in normally; open the current workday; declare a break or departure; resume
or return; check out normally; submit a report then or later. Completed shifts
cannot accept new presence transitions. A report must never block checkout.

## 6. Ownership
HRM owns the workday ledger. Core owns identities, company membership and grants.
Laravel commercial entitlements remain separate. No new identity system is added.

## 7. Entities
MobileWorkdayEvent references tenant, company, worker, attendance, location, actor,
request ID/hash, revision, server UTC time, kind, resulting declared state and
bounded report/reason fields. No employee password or coordinate is included.

## 8. States
An open mobile shift initially has declared state working. Break start moves to
on_break; break end resumes working. Departure moves working to away; return
resumes working. Existing attendance checkout makes the presented state closed.
These states are employee declarations, not independently verified presence.

## 9. Work report
Summary3–2000 characters; optional blockers up to1000 characters. Departure reason
requires at least3 and at most300 characters. Reports remain explicitly
self-reported; ERP metric availability is not_connected. No invented document
counts or manager approval is implied. Revisions append instead of overwriting.

## 10. Business rules
Presence transitions require an open, non-archived mobile attendance row, in the
worker's current company access, no older than24 hours. Closed shifts accept only
reports. Reports do not reopen time or replace original clock timestamps.

## 11. API
Optional ERP:MobileWorkday:Enabled defaults false. Hosted routes are
/api/mobile/v1/hrm/me/workdays/{attendanceId}; local standalone routes are
/mobile/v1/workdays/{attendanceId}. GET reads; POST /commands mutates. Commands
contain request_id, expected_revision, kind and optional reason/summary/blockers.
Unknown/duplicate JSON properties and caller-selected authority are rejected.

## 12. Capability negotiation
Only enabled installations advertise workday_ready. Older clients ignore the
optional field; newer clients must keep ordinary attendance usable when absent.
No background tracking is implicitly enabled by the workday capability.

## 13. Transaction boundary
Commands hold the existing attendance-worker advisory lock, plus the company
shared lock, so clock-out and workday actions serialize. PostgreSQL business reads
occur after acquiring locks. The attendance row is locked against legacy edits.
Authority is checked again before applying and before commit.

## 14. Idempotency and concurrency
Unique tenant/worker/request ID and tenant/attendance/revision indexes. The same
request/hash returns its accepted revision with current state; changed payloads
under an old request ID conflict. A stale expected revision cannot overwrite work.
No automatic replay of uncertain writes with a different request identifier.

## 15. Data scope
Store reads check exact tenant/worker/attendance and the row's company-owned
location. Both mobile login profiles reuse their current admission. Cookie or
forged tenant/worker headers cannot contribute authority.

## 16. Audit
Append one consequential audit entry atomically with each accepted action. Audit
metadata contains IDs, kind, revision, state and affects_payroll=false; report text
stays in its scoped owning record. Existing evidence cannot be edited/deleted.

## 17. Payroll compatibility
This stage never changes AttendanceRecord clock values, totals, overtime decisions,
compensation or payroll runs. UI must call existing durations elapsed shift time,
not verified/paid work. Paid/unpaid break rules need explicit employer policy and
their own tested HRM calculation changes before use in payroll.

## 18. Native monitoring stage
Introduce a separately negotiated, optional site-check capability only after the
manual workflows pass. Start with explicit foreground evidence and permission/
service health. Background observation requires transparent employee opt-in,
site policy, native permission setup and real-device qualification. It must remain
observe-only; missing evidence means unknown, never an invented departure time.

## 19. Monitoring exclusions
No off-duty route history, covert collection, uninstall prevention on personal
phones, automatic wage deductions or use of missing telemetry as proof of fraud.
Device-management restrictions are a separate organisational rollout.

## 20. Metrics stage
Adapters must count authoritative, attributable module events within an explicit
report period; handle duplicates/reversals and distinguish unavailable from zero.
Do not present source code for an unqualified module as a live metric integration.

## 21. Storage and migrations
Additive ledger only. Full-model EF migration and narrow compatibility extension
must be reviewed independently. Existing customer checkout and business schema
remain intact. Never apply a full current-model migration to an older instance.

## 22. Older installations
Existing clients, sessions, check-in/out and integrations remain unchanged while
the capability is off. Classic and hosted publication profiles need explicit
shared-file/hash manifests and isolated schema rehearsal before enablement.

## 23. Failure handling
Invalid input400, denied scope403, stale/invalid transition409, unavailable503.
Licensing/identity outages must not masquerade as a request for a new password.
UI preserves unsent report edits; local draft loss cannot erase server history.

## 24. Notifications and external effects
No emails, push campaigns, manager escalations or purchases in this initial stage.
New notifications require a separately qualified delivery workflow.

## 25. Acceptance
State transitions; multiple breaks/returns; report after checkout; duplicate and
stale actions; cross-worker/company/tenant rejection; malformed bodies; rollback;
append-only history; concurrent checkout; existing attendance/payroll unchanged.
Then real PostgreSQL and installed Android on isolated SaaS/standalone fixtures.

## 26. Operations and rollback
Feature off restores old mobile behaviour without deleting history. Retain the
old API/proxy and immutable publications. Back up and rehearse schema first;
verify old browser/native entry and on-prem customizations after any rollout.

## 27. Release status
Implementation in progress. No live enablement or migration claimed by this file.
Exact commits, retained evidence, holds and installed versions go in MANUS_HANDOFF.

## 28. Further decisions
Employer break policy, approved off-site work, site coordinates/radii, monitoring
consent/retention/access, manager review, physical-device coverage and production
distribution must be decided explicitly. They do not block testing manual basics.
