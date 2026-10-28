# M5c — Service acceptance uses the Core company date

Change reference: M5c, follow-up to PROC-09 receiving. Engineering owner: Root.
Classification: Tier3, existing GreenWebb standard and PROC-09 28-section feature
contract apply. GitHub issues are disabled on the ERP repository; this change
record supplies the traceable requirement and acceptance scope.

## Problem and outcome

A service period was rejected or allowed according to UTC even when Core's
selected company was on another calendar day. A permitted service owner must
be able to certify a completed period through the company's current local day,
while a future company-local end date remains blocked. Goods workflows and
the imported UI layout are unchanged.

## Ownership and workflow

The existing selected-company/branch checks, current Core account permissions,
independence rules, active receiving policy, issued source/version checks and
capacity checks run before certification. The API reads Core's persisted
company time zone in its already-enlisted context using the shared
`CoreCompanyBusinessDateProvider`. No client date, zone or UTC offset is
accepted. No second company/calendar master is introduced.

One server time observation is normalized to PostgreSQL microseconds. The
acceptance retains schema version, tenant, company, company-local date, IANA
zone ID, observed UTC offset and exact UTC observation. The acceptance timestamp
uses that same observation. The end date must be on/before the retained local
date; invalid/missing company configuration fails closed before persistence.
An attempted future period returns a safe action message asking the user to
choose today or an earlier company-local date. No new state/approval bypass,
Finance posting, inventory posting or business authority is introduced.

Retained evidence is checked for source scope, schema, UTC timestamp, bounded
offset, local-date consistency and matching acceptance instant. A later company
zone change must not reinterpret a completed acceptance. The stored offset
keeps the evidence independent of later time-zone database changes.

## Historical and release compatibility

Older accepted Service facts without an observation retain their original UTC
validation rule. New service commands always require an observation. Goods
acceptances must have none. Only this new nullable property is suppressed when
null, preserving existing Goods/legacy Service serialization and digests;
other null fields retain their original serialization.

No EF model/schema change is intended. The witness lives in the existing
immutable typed evidence payload. Earlier strict readers do not understand a
new nonnull observation: after such writes, rollback must retain a compatible
reader or use a fix-forward release. Do not roll back to the M5b reader and
silently hide/alter new Service facts. No migration resets historical evidence.

## Verification scope

Required automated checks cover Lusaka's day ahead of UTC, a negative-offset
day behind UTC, non-hour offsets, company scope, changed company zone,
inconsistent retained observations, precision, tampering, future periods and
legacy byte/digest compatibility. Run the Procurement regression suite, focused
host/Core provider tests and EF pending-model check on the exact committed
source. Record actual results in the accompanying verification manifest.

These tests do not prove a real signed-in Service UI/API journey or whole-module
UAT. The private M5b Goods source is classified Goods and must not be relabelled
or mutated into Service for a test. An end-to-end Service acceptance needs a
normally created and approved Service demand/order source. Returns/corrections,
matching, deployment and the wider UAT roadmap remain open.

## Verified result

Committed product `406dd1bd` passed **1,483/1,483** Procurement regression tests,
**21/21** focused host/Core-date checks and **4/4** receiving HTTP regression
checks. EF reported no pending model changes. A separate private .NET harness
passed **1/1** using the actual retained native M5b Goods acceptance: the new
codec verified its original digest and re-encoded identical bytes. No database
writes or business-command replays were involved. Independent source review
found no concrete defect. No real native Service journey or deployment is
claimed by these results.
