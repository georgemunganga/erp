-- Synthetic fixture only. Source-reviewed against e752ed1/P24 model and old scoped FKs.
-- NO P25 column, authority, policy, source-state or acceptance-effect backfill.
-- Root executes only on a private restored P24 copy after independent review.
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '5s';
DO $fixture_guard$
BEGIN
    IF current_database() NOT LIKE 'procurement_p25_restore_%' THEN
        RAISE EXCEPTION 'P25FixturePrivateRestoredDatabaseRequired';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM procurement.__procurement_migrations h
        WHERE coalesce(to_jsonb(h)->>'MigrationId',to_jsonb(h)->>'migration_id')='20261020114243_P24PurchaseOrderCommercialExecution')
        OR EXISTS (SELECT 1 FROM procurement.__procurement_migrations h
        WHERE coalesce(to_jsonb(h)->>'MigrationId',to_jsonb(h)->>'migration_id') LIKE '%P25%') THEN
        RAISE EXCEPTION 'P25FixtureRequiresP24BeforeUpgrade';
    END IF;
    IF EXISTS (SELECT 1 FROM procurement.suppliers WHERE tenant_id='p25-legacy-rehearsal')
        OR EXISTS (SELECT 1 FROM procurement.purchase_requests WHERE tenant_id='p25-legacy-rehearsal') THEN
        RAISE EXCEPTION 'P25FixtureAlreadyPresentDoNotOverwrite';
    END IF;
END $fixture_guard$;

-- Scope/worker/site UUIDs below are retained synthetic legacy references only.
-- Procurement has no cross-Core FKs here; they do not create a Core company/person/site.
INSERT INTO procurement.suppliers
    (id,tenant_id,legal_entity_id,version,created_at,created_by,number,legal_name,display_name,country_code,
     status,is_local,is_orderable,is_preferred,is_strategic)
VALUES ('25000000-0000-0000-0000-000000000001','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',1,
    '2026-09-01T08:00:00Z','synthetic-legacy-author','P25-LEGACY-SUP-01','Synthetic Legacy Supplier','Legacy supplier — preservation','ZM',
    'Inactive',true,false,false,false);

INSERT INTO procurement.purchase_requests
    (id,tenant_id,legal_entity_id,version,created_at,created_by,number,requester_worker_id,currency_code,purpose,status)
VALUES ('25000000-0000-0000-0000-000000000002','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',3,
    '2026-09-02T08:00:00Z','synthetic-legacy-author','P25-LEGACY-PR-01','25000000-0000-0000-0000-000000000101','ZMW',
    'Synthetic retained legacy goods and service quantities; not new approval authority','Partially Fulfilled');

INSERT INTO procurement.purchase_request_lines
    (id,tenant_id,legal_entity_id,version,created_at,created_by,purchase_request_id,line_number,description,unit_code,
     requested_quantity,estimated_unit_price,approved_quantity,allocated_quantity,ordered_quantity,accepted_quantity,cancelled_quantity,approval_status,route)
VALUES
    ('25000000-0000-0000-0000-000000000003','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',4,
     '2026-09-02T08:01:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000002',1,'Legacy goods line — keep punctuation / α','EA',
     12.0000,125.1250,12.0000,12.0000,12.0000,7.2500,0.0000,'Approved','DirectPurchase'),
    ('25000000-0000-0000-0000-000000000004','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',5,
     '2026-09-02T08:02:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000002',2,'Legacy service line','HOUR',
     8.0000,88.8888,8.0000,8.0000,8.0000,3.1250,0.0000,'Approved','DirectPurchase');

INSERT INTO procurement.purchase_orders
    (id,tenant_id,legal_entity_id,version,created_at,created_by,number,supplier_id,currency_code,buyer_subject_id,status,current_revision,
     ship_to_ref,bill_to_ref,payment_term_ref)
VALUES ('25000000-0000-0000-0000-000000000005','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',6,
    '2026-09-03T08:00:00Z','synthetic-legacy-author','P25-LEGACY-PO-01','25000000-0000-0000-0000-000000000001','ZMW',
    'synthetic-legacy-buyer','Partially Received',0,'Legacy ship-to reference','Legacy bill-to reference','NET30-LEGACY');

INSERT INTO procurement.purchase_order_lines
    (id,tenant_id,legal_entity_id,version,created_at,created_by,purchase_order_id,purchase_request_line_id,line_number,description,unit_code,
     quantity,unit_price,tax_amount,accepted_quantity,invoiced_quantity,is_active)
VALUES
    ('25000000-0000-0000-0000-000000000006','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',7,
     '2026-09-03T08:01:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000005','25000000-0000-0000-0000-000000000003',1,
     'Legacy received goods','EA',12.0000,125.1250,12.1234,7.2500,3.5000,true),
    ('25000000-0000-0000-0000-000000000007','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',8,
     '2026-09-03T08:02:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000005','25000000-0000-0000-0000-000000000004',2,
     'Legacy certified service','HOUR',8.0000,88.8888,4.5678,3.1250,1.1250,true);

INSERT INTO procurement.goods_receipts
    (id,tenant_id,legal_entity_id,version,created_at,created_by,number,purchase_order_id,status,site_ref,delivery_note_ref,receiver_subject_id,received_at)
VALUES ('25000000-0000-0000-0000-000000000008','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',9,
    '2026-09-04T09:01:00Z','synthetic-legacy-author','P25-LEGACY-GRN-01','25000000-0000-0000-0000-000000000005','Partially Accepted',
    '25000000-0000-0000-0000-000000000102','Legacy delivery note 001','synthetic-legacy-receiver','2026-09-04T09:00:00Z');

INSERT INTO procurement.goods_receipt_lines
    (id,tenant_id,legal_entity_id,version,created_at,created_by,goods_receipt_id,purchase_order_line_id,received_quantity,accepted_quantity,
     rejected_quantity,status,batch_ref,serial_ref,rejection_reason)
VALUES ('25000000-0000-0000-0000-000000000009','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',10,
    '2026-09-04T09:02:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000008','25000000-0000-0000-0000-000000000006',
    10.5000,7.2500,1.2500,'Partially Accepted','LEGACY-BATCH-001','LEGACY-SERIAL-α','Legacy rejection narrative; do not overwrite');

INSERT INTO procurement.receipt_inspections
    (id,tenant_id,legal_entity_id,version,created_at,created_by,goods_receipt_line_id,status,inspected_quantity,defect_quantity,
     inspector_subject_id,checklist_ref,notes,inspected_at)
VALUES ('25000000-0000-0000-0000-000000000010','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',11,
    '2026-09-04T10:01:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000009','Partially Accepted',10.5000,1.2500,
    'synthetic-legacy-inspector','legacy-checklist/scan-001','Legacy inspector''s original evidence — α','2026-09-04T10:00:00Z');

INSERT INTO procurement.service_acceptances
    (id,tenant_id,legal_entity_id,version,created_at,created_by,purchase_order_line_id,status,period_from,period_to,
     accepted_quantity,accepted_amount,owner_subject_id,deliverable_ref,reason,certified_at)
VALUES ('25000000-0000-0000-0000-000000000011','p25-legacy-rehearsal','25000000-0000-0000-0000-000000000100',12,
    '2026-10-01T12:31:00Z','synthetic-legacy-author','25000000-0000-0000-0000-000000000007','Accepted','2026-09-01','2026-09-30',
    3.1250,277.7775,'synthetic-legacy-service-owner','LEGACY-SERVICE-DELIVERABLE-01',
    repeat('Retained legacy service reason. ',60),'2026-10-01T12:30:00Z');
COMMIT;
