-- Qualification startup only. This does not enable operational ERP workflows.
BEGIN;
GRANT USAGE ON SCHEMA hrm TO kuubu_core_runtime;
GRANT SELECT ON hrm.__hrm_migrations, hrm.tenant_role_assignments,
  hrm.audit_entries, hrm.local_users, hrm.local_sessions TO kuubu_core_runtime;
-- Existing startup creates role catalogue definitions, never user memberships.
GRANT INSERT ON hrm.tenant_role_assignments, hrm.audit_entries TO kuubu_core_runtime;
DO $qualification$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='kuubu_core_runtime'
    AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))
    OR pg_has_role('kuubu_core_runtime', 'kuubu_schema_owner', 'MEMBER')
    OR has_database_privilege('kuubu_core_runtime', 'kuubu', 'CREATE')
    OR has_schema_privilege('kuubu_core_runtime', 'hrm', 'CREATE')
    OR has_schema_privilege('kuubu_core_runtime', 'public', 'CREATE') THEN
    RAISE EXCEPTION 'Runtime role has excessive ownership or DDL capability';
  END IF;
  IF EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
    WHERE n.nspname='hrm' AND c.relkind IN ('r','p')
    AND pg_get_userbyid(c.relowner) <> 'kuubu_schema_owner') THEN
    RAISE EXCEPTION 'Core/HRM tables have unexpected owners';
  END IF;
  IF (SELECT count(*) FROM hrm.local_users) <> 0
    OR (SELECT count(*) FROM hrm.local_sessions) <> 0 THEN
    RAISE EXCEPTION 'Unexpected preexisting identities or sessions';
  END IF;
END
$qualification$;
SET LOCAL ROLE kuubu_core_runtime;
-- Native denial probes, not privilege metadata alone. Each denied statement must
-- fail before altering rows; unexpected success aborts the entire transaction.
DO $denials$
BEGIN
  BEGIN
    CREATE TABLE hrm.qualification_forbidden_ddl(id int);
    RAISE EXCEPTION 'Runtime DDL unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
  BEGIN
    DELETE FROM hrm.audit_entries;
    RAISE EXCEPTION 'Runtime audit deletion unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
  BEGIN
    UPDATE hrm.audit_entries SET action='forbidden';
    RAISE EXCEPTION 'Runtime audit update unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
  BEGIN
    DELETE FROM hrm.local_users;
    RAISE EXCEPTION 'Runtime identity deletion unexpectedly succeeded';
  EXCEPTION WHEN insufficient_privilege THEN NULL; END;
END
$denials$;
RESET ROLE;
SELECT count(*) AS genuine_migrations FROM hrm.__hrm_migrations;
SELECT count(*) AS login_users FROM hrm.local_users;
COMMIT;
