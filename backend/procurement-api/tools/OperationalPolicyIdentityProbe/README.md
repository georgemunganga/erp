# Operational policy identity PostgreSQL probe

Status: compiled and executed successfully against private PostgreSQL 17 on 22 October 2026, with generated P31. Evidence: `artifacts/operational-policy-identity/private/20261022_042007/` (bounded result and source hashes). Build: zero warnings/errors. All seven assertion groups below passed. This is owner-level persistence evidence; no production operation is authorized by this README.

## Boundaries

This executable uses the actual Procurement EF context, generated migrations, identity codec and SaveChanges guards against one **fresh, existing disposable database**. It is an owner-level persistence probe: it does not run Core, authenticate people, execute HTTP endpoints, establish finite-role grants, validate operational receiving/matching rules, or prove that a policy author is authorized. The synthetic worker IDs are observed-fixture inputs only. The sample rules object is expressly persistence-only, not a usable receiving or matching policy.

It requires `ConnectionStrings__Procurement`, a loopback PostgreSQL host or `/authority-identity-private-socket`, and a database named `operational_policy_identity_*` (ASCII letters/digits/underscore, at most 63 characters). Arguments, a remote host, or an existing user table are rejected. The runner supplies the fresh database and retains the database and logs for evidence. The tool never creates/drops a database, disconnects another client, deletes a container, resumes a failed fixture or prints connection strings.

## Actual assertions verified

1. Apply the compiled P30→P31 migration, unused Down and Up, then return to P30 for raw legacy intake. Confirm compiled Down includes locks and the exact evidence/audit retention message.
2. Raw-owner seed one old `RECEIVING` and one `INVOICE_MATCHING` policy at P30. Upgrade and compare **all original row columns**; require zero backfilled identity rows. Current EF creation deliberately is not used for legacy data.
3. Persist one new policy of each key using actual SaveChanges: original Created metadata and observed maker; exact Submitted rules/revision/time; independently observed Published worker. Capture uses intended committed revision `original Version+1`; SaveChanges remains authoritative. Independently reload all six rows and verify publication, storage metadata and PostgreSQL microsecond round trips.
4. Reject adoption of either legacy draft without original evidence, and reject context update and delete of retained evidence with the exact append-only code.
5. Rollback-only raw SQL denials require actual SQLSTATE **and named constraint**: foreign owner, cross-key owner, foreign same-stage Created lineage, wrong Created stage, wrong Submitted stage; duplicate origin, publication and action revision; empty worker CHECK. Temporary removal of a published row is owner-only inside a rolled-back transaction to isolate the submitted FK.
6. Actual used migration Down must raise SQLSTATE 55000 with `Retained operational policy identity prevents destructive downgrade.` A second owner-only rollback simulation removes action rows leaf-first but retains their automatic audit, then executes the compiled Down script and requires the same veto.
7. Require policies, evidence, audit, outbox, complete migration-history rows and Procurement relation ownership/ACL footprint unchanged after denials; recheck all original legacy rows. Require six retained actions, final P31 history and no pending model changes.

The raw negative rows intentionally have stale digests after changing a tested SQL key. SQL constraints enforce structure/ownership; the application codec separately authenticates exact evidence values. A raw SQL named-constraint result does not claim SQL verifies cryptographic hashes.

## Parent-controlled execution

Build only after the actual Stage2 mapping and generated P31 migration are integrated. Use existing cached packages with one serialized SDK process and the coordinator's CPU/memory limits. Run a precompiled DLL on the private network-none/socket setup with the explicit disposable owner connection. Preserve the first failure, exact source commit/image, complete console log and resulting private database. Do not retry merely because a long build observation timed out.

Expected successful output is one bounded JSON summary (`passed:true`). Unexpected errors fail nonzero; preserve the private runner logs securely since PostgreSQL exceptions may include synthetic SQL/table diagnostics. The retained result applies to the exact source hashes captured by the runner. No HTTP, browser, current identity, deployment or operational `CanPublish` claim follows from a persistence pass.
