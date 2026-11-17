-- Classic local-only ERP extension. Does not modify the ERP migration history,
-- existing business tables, local accounts, worker links, roles or browser sessions.
-- Apply only with the verified owning connection after backup/restore rehearsal.
BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('greenwebb.classic.local-mobile-schema.v1',0));
CREATE TABLE IF NOT EXISTS hrm.__local_mobile_schema (
    version integer PRIMARY KEY,
    profile text NOT NULL,
    applied_at timestamp with time zone NOT NULL DEFAULT now()
);
DO $guard$
BEGIN
    IF to_regclass('hrm.local_mobile_sessions') IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM hrm.__local_mobile_schema WHERE version=1 AND profile='classic-local-mobile-v1'
    ) THEN RAISE EXCEPTION 'Unrecognized mobile session table; refusing adoption'; END IF;
    IF EXISTS (SELECT 1 FROM hrm.__local_mobile_schema WHERE version<>1 OR profile<>'classic-local-mobile-v1')
        THEN RAISE EXCEPTION 'Unrecognized mobile schema receipt'; END IF;
END $guard$;
CREATE TABLE IF NOT EXISTS hrm.local_mobile_sessions (
    id uuid PRIMARY KEY,
    tenant_id text NOT NULL,
    local_user_id uuid NOT NULL REFERENCES hrm.local_users(id) ON DELETE RESTRICT,
    worker_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL REFERENCES hrm.legal_entities(id) ON DELETE RESTRICT,
    origin text NOT NULL,
    password_version text NOT NULL,
    access_hash text NOT NULL UNIQUE,
    refresh_hash text NOT NULL UNIQUE,
    access_expires_at timestamp with time zone NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    revoked_at timestamp with time zone NULL,
    created_at timestamp with time zone NOT NULL,
    created_by text NOT NULL,
    updated_at timestamp with time zone NULL,
    updated_by text NULL,
    is_archived boolean NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_local_mobile_sessions_tenant_user_expiry
    ON hrm.local_mobile_sessions(tenant_id,local_user_id,expires_at);
INSERT INTO hrm.__local_mobile_schema(version,profile)
    VALUES(1,'classic-local-mobile-v1') ON CONFLICT(version) DO NOTHING;
COMMIT;
