DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core_approval') THEN
        CREATE SCHEMA core_approval;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS core_approval."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core_approval') THEN
            CREATE SCHEMA core_approval;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE TABLE core_approval.cases (
        "Id" uuid NOT NULL,
        "SourceModule" character varying(60) NOT NULL,
        "SourceType" character varying(80) NOT NULL,
        "SourceId" uuid NOT NULL,
        "SourceVersion" bigint NOT NULL,
        "PolicyVersionId" uuid NOT NULL,
        "PolicyVersionNumber" integer NOT NULL,
        "MakerSubjectId" character varying(150) NOT NULL,
        "State" character varying(30) NOT NULL,
        "OpenedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "CompletionKey" character varying(150),
        "TenantId" character varying(150) NOT NULL,
        "LegalEntityId" uuid NOT NULL,
        "Version" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_cases" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_cases_TenantId_LegalEntityId_Id" UNIQUE ("TenantId", "LegalEntityId", "Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE TABLE core_approval.delegations (
        "Id" uuid NOT NULL,
        "DelegatorSubjectId" character varying(150) NOT NULL,
        "DelegateSubjectId" character varying(150) NOT NULL,
        "PermissionKey" character varying(150) NOT NULL,
        "EffectiveFrom" timestamp with time zone NOT NULL,
        "EffectiveTo" timestamp with time zone NOT NULL,
        "Active" boolean NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "TenantId" character varying(150) NOT NULL,
        "LegalEntityId" uuid NOT NULL,
        "Version" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_delegations" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_delegations_TenantId_LegalEntityId_Id" UNIQUE ("TenantId", "LegalEntityId", "Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE TABLE core_approval.stages (
        "Id" uuid NOT NULL,
        "CaseId" uuid NOT NULL,
        "Sequence" integer NOT NULL,
        "Code" character varying(100) NOT NULL,
        "Mode" character varying(20) NOT NULL,
        "RequiredApprovals" integer NOT NULL,
        "State" character varying(30) NOT NULL,
        "DueAt" timestamp with time zone,
        "TenantId" character varying(150) NOT NULL,
        "LegalEntityId" uuid NOT NULL,
        "Version" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_stages" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_stages_TenantId_LegalEntityId_Id" UNIQUE ("TenantId", "LegalEntityId", "Id"),
        CONSTRAINT "FK_stages_cases_TenantId_LegalEntityId_CaseId" FOREIGN KEY ("TenantId", "LegalEntityId", "CaseId") REFERENCES core_approval.cases ("TenantId", "LegalEntityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE TABLE core_approval.assignments (
        "Id" uuid NOT NULL,
        "CaseId" uuid NOT NULL,
        "StageId" uuid NOT NULL,
        "AssigneeSubjectId" character varying(150) NOT NULL,
        "DelegatedFromSubjectId" character varying(150),
        "PermissionKey" character varying(150) NOT NULL,
        "State" character varying(30) NOT NULL,
        "TenantId" character varying(150) NOT NULL,
        "LegalEntityId" uuid NOT NULL,
        "Version" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_assignments" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_assignments_TenantId_LegalEntityId_Id" UNIQUE ("TenantId", "LegalEntityId", "Id"),
        CONSTRAINT "FK_assignments_stages_TenantId_LegalEntityId_StageId" FOREIGN KEY ("TenantId", "LegalEntityId", "StageId") REFERENCES core_approval.stages ("TenantId", "LegalEntityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE TABLE core_approval.decisions (
        "Id" uuid NOT NULL,
        "CaseId" uuid NOT NULL,
        "StageId" uuid NOT NULL,
        "AssignmentId" uuid NOT NULL,
        "ActorSubjectId" character varying(150) NOT NULL,
        "Action" character varying(20) NOT NULL,
        "Reason" character varying(2000),
        "IdempotencyKey" character varying(150) NOT NULL,
        "DecidedAt" timestamp with time zone NOT NULL,
        "TenantId" character varying(150) NOT NULL,
        "LegalEntityId" uuid NOT NULL,
        "Version" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_decisions" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_decisions_TenantId_LegalEntityId_Id" UNIQUE ("TenantId", "LegalEntityId", "Id"),
        CONSTRAINT "FK_decisions_assignments_TenantId_LegalEntityId_AssignmentId" FOREIGN KEY ("TenantId", "LegalEntityId", "AssignmentId") REFERENCES core_approval.assignments ("TenantId", "LegalEntityId", "Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE INDEX "IX_assignments_TenantId_LegalEntityId_CaseId_AssigneeSubjectId~" ON core_approval.assignments ("TenantId", "LegalEntityId", "CaseId", "AssigneeSubjectId", "State");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE INDEX "IX_assignments_TenantId_LegalEntityId_StageId" ON core_approval.assignments ("TenantId", "LegalEntityId", "StageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE UNIQUE INDEX "IX_cases_TenantId_LegalEntityId_SourceModule_SourceType_Source~" ON core_approval.cases ("TenantId", "LegalEntityId", "SourceModule", "SourceType", "SourceId", "SourceVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE INDEX "IX_decisions_TenantId_LegalEntityId_AssignmentId" ON core_approval.decisions ("TenantId", "LegalEntityId", "AssignmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE UNIQUE INDEX "IX_decisions_TenantId_LegalEntityId_CaseId_IdempotencyKey" ON core_approval.decisions ("TenantId", "LegalEntityId", "CaseId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE INDEX "IX_delegations_TenantId_LegalEntityId_DelegatorSubjectId_Permi~" ON core_approval.delegations ("TenantId", "LegalEntityId", "DelegatorSubjectId", "PermissionKey", "EffectiveFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    CREATE UNIQUE INDEX "IX_stages_TenantId_LegalEntityId_CaseId_Sequence" ON core_approval.stages ("TenantId", "LegalEntityId", "CaseId", "Sequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId" = '20261019183218_P01CoreApprovalFoundation') THEN
    INSERT INTO core_approval."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019183218_P01CoreApprovalFoundation', '10.0.11');
    END IF;
END $EF$;
COMMIT;
