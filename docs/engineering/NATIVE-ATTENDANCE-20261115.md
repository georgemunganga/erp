# Native attendance integration

## 1. Identification
15 November2026, Tier3; Vcron native consumer of Kuubu Core/HR.
## 2. Outcome
Real employee sign-in, explicit company selection, authoritative attendance and retries.
## 3. Scope
Hosted Kuubu mobile profile only, opt-in. Existing local/OIDC/browser ERP routes unchanged.
## 4. Ownership
Laravel proves enrolled identity; Core membership/license/module/location grant authorizes;
HR explicitly links employees and owns attendance/calculation/audit.
## 5. Actors
Enrolled employee with active explicit company/module grant and linked employee record.
## 6. Entry
Native system-browser authorization, code+S256 PKCE, exact registered vcron callback.
## 7. Identity model
Public mobile client, no embedded secret; separate product:mobile-attendance scope.
## 8. Enrollment
Existing explicit product enrollment required. No email/domain/company inference.
## 9. Company selection
Verified membership directory returns bounded candidates; selection is narrowing only.
## 10. Employee scope
Exact Core principal-to-worker SubjectId binding, active employment, current owned location.
## 11. Permissions
Native request exposes only own-record routes even for an administrator; no admin role copied.
## 12. Token validation
Fresh pinned HTTPS provider observation, exact native client/scope/purpose/product,
bounded monotonic proof lifetime and current license/grant checked per operation.
## 13. Secrets
Native tokens in platform secure storage, not AsyncStorage/logs; no backend secret in APK.
## 14. Attendance reads
Read-only status/history; no use of legacy GetToday initializer on background requests.
## 15. Clock policy
One shift per company business day, server clock and configured timezone; overnight
open shift can close within24h. Older/inconsistent records require HR correction.
## 16. Replay
Clock-in bound to expected business date, clock-out to exact attendance ID. Per-worker
transaction lock and persisted-state convergence prevent repeated writes/audit effects.
## 17. Calculations
Existing HR hours/overtime calculations reused. No device-time, payroll approval or
payment decision on the mobile device. No GPS/biometrics collected by this feature.
## 18. Persistence
Existing attendance/HR audit tables; no default change to legacy behavior or migrations.
## 19. Concurrency
Shared company grant fence, exclusive worker punch fence, current admission recheck
before commit; rollback on lost authority or provider failure.
## 20. Errors
401 expired identity,403 denied company/employee,409 changed/replayed work context,
503 unavailable provider. No sample-data fallback or write replay on reconnection.
## 21. API
Versioned native companies/profile/history/clock operations behind the shared edge;
body/queries/headers strictly bounded and unknown operations rejected.
## 22. UI
Reuse existing native components/theme; browser sign-in, explicit company selector,
server refresh on app resume, clear employee-link and connection error states.
## 23. Events/audit
Existing authoritative attendance/calculation audits within the same transaction.
## 24. Threat model
Test forged scope/worker/client, different company/person, revoked membership/role/
license, expired/malformed proof, replay, midnight/overnight and lost responses.
## 25. Compatibility
Feature off by default; standalone/on-prem retains existing local/OIDC behavior.
Same release source can add native profiles later; no customer hostname/DNS mutation.
## 26. Deployment
Qualified exclusive releases for Laravel/API/edge and versioned native source/build;
preserve keys/data/browser cookies and prior consumers, backups and code rollback.
## 27. Acceptance
Isolated real PKCE/provider/Core/HR clock flow, native adapter/UI checks, unauthorized
and legacy regressions, public protocol and current app/Perfex health. Do not invent
production staff or claim an unperformed physical-device test.
## 28. Release evidence
Maintain exact source/image/config/native-build pins, proof and residual holds in handoff.
