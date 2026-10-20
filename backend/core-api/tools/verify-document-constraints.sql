-- Run only after applying Core document P01 to a disposable restored database.
-- Fixtures are rolled back. This verifies relational controls InMemory tests cannot enforce.
\set ON_ERROR_STOP on
BEGIN;
DO $verify$
DECLARE
    entity uuid := '00000000-0000-0000-0000-000000000001';
    doc uuid := gen_random_uuid();
    ver uuid := gen_random_uuid();
BEGIN
    IF current_database() NOT LIKE '%rehearsal%' THEN
        RAISE EXCEPTION 'Use a disposable rehearsal database';
    END IF;
    INSERT INTO core_document.documents
      ("Id", "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "Classification",
       "CreatedBy", "CreatedAt", "IsRetired", "LegalHold", "Version")
    VALUES (doc, 'constraint-test', entity, 'procurement', 'invoice', gen_random_uuid(), 'confidential',
      'test', now(), false, false, 1);
    BEGIN
        INSERT INTO core_document.versions
          ("Id", "TenantId", "LegalEntityId", "DocumentId", "Number", "FileName", "MediaType", "SizeBytes",
           "Sha256", "StorageKey", "CreatedBy", "CreatedAt", "ScanState", "ScanAttempts", "Version")
        VALUES (ver, 'foreign-tenant', entity, doc, 1, 'test.pdf', 'application/pdf', 12,
          repeat('a',64), 'constraint-test/' || ver, 'test', now(), 0, 0, 1);
        RAISE EXCEPTION 'Cross-tenant version was accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    INSERT INTO core_document.versions
      ("Id", "TenantId", "LegalEntityId", "DocumentId", "Number", "FileName", "MediaType", "SizeBytes",
       "Sha256", "StorageKey", "CreatedBy", "CreatedAt", "ScanState", "ScanAttempts", "Version")
    VALUES (ver, 'constraint-test', entity, doc, 1, 'test.pdf', 'application/pdf', 12,
      repeat('a',64), 'constraint-test/' || ver, 'test', now(), 0, 0, 1);
    BEGIN
        UPDATE core_document.versions SET "LegalEntityId" = gen_random_uuid() WHERE "Id" = ver;
        RAISE EXCEPTION 'Cross-company version was accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO core_document.versions
        SELECT gen_random_uuid(), "TenantId", "LegalEntityId", "DocumentId", "Number", "FileName", "MediaType",
          "SizeBytes", "Sha256", 'different-key', "CreatedBy", "CreatedAt", "ScanState", "ScanAttempts",
          "ScanLeaseId", "ScanStartedAt", "ScanLeaseExpiresAt", "Version"
        FROM core_document.versions WHERE "Id" = ver;
        RAISE EXCEPTION 'Duplicate version number was accepted';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        DELETE FROM core_document.documents WHERE "Id" = doc;
        RAISE EXCEPTION 'Document with retained versions was deleted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO core_document.scan_evidence
          ("Id", "TenantId", "LegalEntityId", "VersionId", "Attempt", "Verdict", "Sha256", "Engine", "Definitions", "StartedAt", "FinishedAt")
        VALUES (gen_random_uuid(), 'foreign-tenant', entity, ver, 1, 1, repeat('a',64), 'test', 'test', now(), now());
        RAISE EXCEPTION 'Cross-tenant scan evidence was accepted';
    EXCEPTION WHEN foreign_key_violation THEN NULL;
    END;
    RAISE NOTICE 'Document relational constraints passed';
END $verify$;
ROLLBACK;
