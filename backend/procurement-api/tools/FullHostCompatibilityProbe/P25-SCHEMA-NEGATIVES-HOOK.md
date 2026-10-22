# P25 private schema-readiness negatives: source-only integration hook

Base: `61817758410547970d51f98e308aa8c74906e580`. Source-only; no compilation,
SQL, native execution or readiness acceptance is claimed by this checkpoint.
No shared entry, fixture, application, migration or default registration changes.
This is the remaining Feature 26 schema-negative acceptance slice, not a new
workflow or provider. Finance/AP and native identity-provider proof stay absent.

## Root-owned opt-in integration

1. Add optional `P25_HTTP_VERIFY_SCHEMA_NEGATIVES`. Absent means no behavior
   change. Reject every present value other than exact `1` before bootstrap.
2. Call the new partial method only on the mode's freshly created private
   `proc_p25_http_...` DB, after genuine migrations and the two unique finite
   LOGIN grants, before direct readiness/default-host phases or any HTTP graph.
   If the privilege matrix is also enabled, run it first; preserve its declared
   fixture ACL baseline and all its receipt assertions.

   ```csharp
   await VerifySchemaNegativesAsync(fixture, roles, ownerConnection, result, Advance);
   ```

   These are the same local values used by `VerifyPrivilegeMatrixAsync`; root
   owns the actual flag parse/callsite. This commit intentionally adds no hook.
3. A future launcher must require actual successful compilation/publication
   pins and a NEW exact private DB/container/output; never run this against an
   accepted DB. Preserve source/evidence/global roles/other DBs and default-off
   production registration. No restart/reset/drop/repair fallback.
4. Require `principalApprovalSchemaNegativeVerification` exact status
   `PASS_PRIVATE_ACTUAL_LOGIN_SCHEMA_NEGATIVES_ONLY`, `caseCount == 5`, five
   unique exact case names below, and each per-case boolean true. Require all
   final safety booleans true and `physicalOidsRestored`, `productionTouched`,
   `externalFinanceAuthority`, `nativeIdentityProviderVerified` false. Main
   approval/replay/GET/raw storage assertions remain separate and mandatory.

## Five independent defects

| Name | Actual model/catalog binding | Missing object |
| --- | --- | --- |
| MissingLeafPrimaryKey | Receipt owning EF primary key, validated `contype=p`; precheck ZERO incoming FKs to receipt table immediately before drop | Receipt PK |
| MissingScopedForeignKey | Receipt owning EF FK to PurchaseRequest, validated `contype=f` | Scoped receipt-to-request FK |
| MissingPrincipalCheck | Owning PurchaseRequest check `ck_pr_original_principal_identity`, validated `contype=c` | Principal provenance CHECK |
| MissingPartialUniqueIndex | Owning SpendingAuthorityIdentityRecord model unique filtered `ux_authority_identity_origin`; actual valid/ready unique partial standalone index | Original authority evidence uniqueness |
| MissingP25History | Actual compiled migration ending `_P25CorePrincipalRequestApproval`; capture original stored ProductVersion | Exact genuine history row |

All tables used by these defects must be empty before preparation. Baselines
must be Record `(read=true,record=true,execute=false)` and Execution
`(true,false,true)` before AND after every defect. During each defect both
actual LOGIN public provider results must be `(false,false,false)`. The same
open finite connection verifies current/session role, non-elevation, effective
SELECT and a real `SELECT * LIMIT 0` before invoking the actual provider. There
is no `SET ROLE`, missing-SELECT substitute, fake provider, current actor grant
change, or business mutation.

## Restoration, capture, failure and limits

Owner autocommit DDL makes the defect visible to the distinct finite LOGIN.
Constraint definitions come from trusted `pg_get_constraintdef` on the exact
model-bound validated object; the index restore uses exact `pg_get_indexdef`.
No `CASCADE`, catalog writes, broad normalization or guessed reconstruction.
The missing history case restores only the captured original MigrationId and
ProductVersion, never a forged migration or version. Owner commands touch only
this NEW disposable fixture; this is an explicit history-negative test, not an
operational migration or evidence deletion procedure.

Finally restores the exact captured definition/history values, raw table and
column ACLs, and existing `PrincipalFootprintAsync` full logical data/schema/
index/sequence/privilege/global-role projection. Both finite readiness baselines
must return before continuing. The footprint excludes physical constraint/index
OIDs by design; recreated objects have new physical identities. No assertion
claims those OIDs, storage pages or transaction IDs were restored. Trusted
captured definitions and five case results are emitted only in the protected
probe result; no credentials/connection string/arbitrary exceptions are logged.
If DROP cannot be safely prepared, a command fails or exact restoration differs,
stop and retain the fixture/failure. No fallback DDL, constraint weakening,
accepted-DB repair or automatic retry. Actual PostgreSQL execution is still
required to establish positive compatibility, including definition equality.
