# P32 sourcing identity readiness correction

The private P32 commercial native cohort failed at HTTP request 76 while creating a genuinely governed sourcing policy. The route returned `503 sourcing-policy-identity-unavailable`. The retained result is `procurement-p32-commercial-native-http-prep-20261025_run01/result.private.json` (SHA-256 `8fd495dddcfc9f6c6d90d81872c7f59139b0f00809ebb70e15aa01ee802bf770`). No P32 Draft or commercial outcome was created by that failed cohort.

The P32 migration `20261024182504_P32PrincipalCommercialEvidence` adds the mapped unique index `ux_pr32_published_identity_target` to `procurement.sourcing_policy_identity_evidence`. The sourcing identity readiness provider still required the P30 model's three unique indexes, so it rejected the valid P32 model before checking the route. This source correction requires P30 and P32 migration history, the exact four mapped unique indexes, all seven physical unique key/index names, and the PostgreSQL 17 definition of the P32 index captured read-only from the failed private cohort. Existing finite-role, column, check, foreign-key, and transaction checks remain in force.

This is a source correction only. Tests and a fresh private HTTP cohort remain pending; the failed result is preserved. It does not activate P32, establish a supplier award, or prove a production deployment.
