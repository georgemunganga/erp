#!/usr/bin/env python3
"""Independent read-only P30 service-cookie owner proof. No business writes."""
import argparse
from collections import Counter
import decimal
import hashlib
import json
from pathlib import Path
import sys
from urllib.parse import urlparse
from verify_p30_po_cookie_owner import DB, Failed, GUID, HEX, SYSTEM, TENANT, canonical_json, protected, output_path, write, require
from verify_p30_receiving_cookie_owner import snapshot as receiving_snapshot, exact, one, RECEIVING, CHANGING


def boundary(fixture, proxy, sha, database):
    require(fixture.get('schemaVersion') == 3 and fixture.get('controlPlaneOnly') is True
        and fixture.get('database') == database and fixture.get('tenantId') == TENANT and DB.fullmatch(database)
        and fixture.get('browserAuthenticationRequired') == 'NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE', 'ServiceFixtureBoundary')
    fields = ('companyId', 'branchId', 'receiverPrincipalId', 'serviceOwnerPrincipalId',
        'approvedRequestId', 'approvedRequestLineId', 'supplierId', 'issuedOrderId',
        'issuedVersionId', 'issuedOrderLineId', 'nativeGoodsOrderId', 'nativeServiceOrderId')
    require(all(isinstance(fixture.get(field), str) and GUID.fullmatch(fixture[field])
        and fixture[field] != '00000000-0000-0000-0000-000000000000' for field in fields)
        and len({fixture['issuedOrderId'],fixture['nativeGoodsOrderId'],fixture['nativeServiceOrderId']}) == 3
        and HEX.fullmatch(fixture.get('challenge', '')), 'ServiceFixtureIdentifiers')
    require(proxy.get('schemaVersion') == 3 and proxy.get('transport') == 'normal-local-cookie-https-v1'
        and proxy.get('fixtureSha256') == sha and proxy.get('database') == database
        and proxy.get('apiUrl') == fixture.get('apiUrl') and proxy.get('injectsIdentityHeaders') is False
        and proxy.get('mocksBusinessResponses') is False and proxy.get('browserAcceptanceVerified') is False,
        'ServiceProxyBoundary')
    origin = urlparse(proxy.get('origin', ''))
    require(origin.scheme == 'https' and origin.hostname == '127.0.0.1'
        and origin.port and not origin.username and not origin.password, 'ServiceProxyLoopback')


def snapshot(container, database, fixture):
    # The accepted goods owner snapshot hashes the entire Procurement/Core graph.
    # Its only actor-specific adjustment excludes login counters; map both actor
    # slots to the genuine ServiceOwner. No identity/grant/branch field is excluded.
    equivalent = {**fixture, 'inspectorPrincipalId': fixture['serviceOwnerPrincipalId'],
        'acceptorPrincipalId': fixture['serviceOwnerPrincipalId']}
    return receiving_snapshot(container, database, equivalent)


def unique_json(raw, expected):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'DuplicateServiceCommandField')
            result[key] = value
        return result
    try:
        value = json.loads(raw, parse_float=decimal.Decimal, parse_int=decimal.Decimal, object_pairs_hook=unique)
    except (ValueError, TypeError, decimal.InvalidOperation):
        raise Failed('ServiceCommandMalformed') from None
    require(isinstance(value, dict) and set(value) == expected, 'ServiceCommandFieldsChanged')
    return value


def fingerprints(browser, fixture, service_id):
    create = unique_json(browser['createRequestBody'], {'purchaseOrderId','issuedVersionId',
        'purchaseOrderLineId','deliverableReference','periodFrom','periodTo','quantity','notes'})
    certify = unique_json(browser['certifyRequestBody'], {'version','sourceVersion','quantity','evidenceReference'})
    require(create['purchaseOrderId'] == fixture['issuedOrderId']
        and create['issuedVersionId'] == fixture['issuedVersionId']
        and create['purchaseOrderLineId'] == fixture['issuedOrderLineId']
        and create['deliverableReference'] == 'PRIVATE_BROWSER_SERVICE_HALF'
        and exact(create['quantity']) == decimal.Decimal('0.5')
        and create['notes'] is None
        and isinstance(create['periodFrom'], str) and create['periodFrom'] == create['periodTo']
        and len(create['periodFrom']) == 10
        and certify['evidenceReference'] == 'PRIVATE-REC:browser-service-half'
        and exact(certify['quantity']) == decimal.Decimal('0.5')
        and exact(certify['version']) == 1 and exact(certify['sourceVersion']) == 0,
        'ServiceBrowserCommandValuesChanged')
    create_value = {'operation':'Create','kind':'Service','input':create}
    certify_value = {'operation':'Certify','id':service_id,'input':certify}
    return (hashlib.sha256(canonical_json(create_value).encode()).hexdigest(),
        hashlib.sha256(canonical_json(certify_value).encode()).hexdigest(),create,certify)


def compare(fixture, before, after, browser, fixture_sha):
    require(browser.get('schemaVersion') == 3 and browser.get('status') == 'PASS_NORMAL_COOKIE_P30_SERVICE_UI_ONLY'
        and browser.get('fixtureSha256') == fixture_sha and browser.get('challenge') == fixture['challenge']
        and browser.get('serviceBrowserActionsVerified') is True and browser.get('allSessionsRevoked') is True
        and browser.get('supplierSendPerformed') is False and browser.get('stockPostingPerformed') is False
        and browser.get('financePostingPerformed') is False and browser.get('productionTouched') is False,
        'ServiceBrowserControlNotAccepted')
    service_id = browser.get('serviceId')
    keys = (browser.get('createCommandKey'),browser.get('certifyCommandKey'))
    require(isinstance(service_id, str) and GUID.fullmatch(service_id)
        and all(isinstance(key, str) and GUID.fullmatch(key) for key in keys) and keys[0] != keys[1],
        'ServiceCommandIdentity')
    create_sha,certify_sha,create,certify = fingerprints(browser,fixture,service_id)
    previous,current = before['tableDigestRows'],after['tableDigestRows']
    require(previous.keys() == current.keys(), 'ServiceOwnerCatalogChanged')
    for name in previous:
        if name == 'hrm.local_sessions':
            require(all(current[name].get(id) == value for id,value in previous[name].items()),
                'ExistingLocalSessionChanged')
        elif name not in CHANGING:
            require(previous[name] == current[name], 'NativeGoodsServiceOrOtherHistoryChanged')
        else:
            require(previous[name].keys() <= current[name].keys(), 'ExistingBusinessRowDeleted')
            if name not in ('procurement.purchase_request_lines','procurement.purchase_order_lines'):
                require(all(current[name][id] == value for id,value in previous[name].items()),
                    'NativeGoodsServiceOrOtherHistoryChanged')
    old,new = before['selectedRows'],after['selectedRows']
    def added(table):
        name='procurement.'+table
        return [row for row in new[name] if row['id'] not in previous[name]]
    for table,id in (('purchase_request_lines',fixture['approvedRequestLineId']),
        ('purchase_order_lines',fixture['issuedOrderLineId'])):
        name='procurement.'+table;old_line=one(old[name],'id',id);new_line=one(new[name],'id',id)
        require(exact(old_line['accepted_quantity']) == 0
            and exact(new_line['accepted_quantity']) == decimal.Decimal('0.5'), 'ServiceCountersChanged')
        excluded={'accepted_quantity','updated_at','version'}
        require({k:v for k,v in old_line.items() if k not in excluded}
            == {k:v for k,v in new_line.items() if k not in excluded}
            and all(current[name][other] == value for other,value in previous[name].items() if other != id),
            'ServiceOriginalLineCommercialFieldsChanged')
    require({name:len(added(name)) for name in RECEIVING} == {
        'principal_receiving_sources':1,'principal_receiving_drafts':1,
        'principal_receiving_revisions':2,'principal_receiving_line_bindings':2,
        'principal_receiving_facts':1,'principal_receiving_receipts':2}, 'ServiceNewOwnerRowCounts')
    source=added('principal_receiving_sources')[0];draft=added('principal_receiving_drafts')[0]
    revisions=added('principal_receiving_revisions');bindings=added('principal_receiving_line_bindings')
    fact=added('principal_receiving_facts')[0];receipts=added('principal_receiving_receipts')
    binding=one(new['procurement.principal_order_source_bindings'],'id',source.get('source_binding_id'))
    issued=one(new['procurement.principal_order_issued_versions'],'id',fixture['issuedVersionId'])
    acknowledgment=one(new['procurement.principal_order_acknowledgments'],'id',source.get('acknowledgment_id'))
    require(source.get('source_json') and hashlib.sha256(source['source_json'].encode()).hexdigest()
        == source.get('source_sha256'), 'ServiceSourceDigestInvalid')
    try: original_source=json.loads(source['source_json'],parse_float=decimal.Decimal)
    except (ValueError,TypeError): raise Failed('ServiceSourceMalformed') from None
    commercial=original_source.get('Commercial')
    require(isinstance(commercial,dict)
        and original_source.get('SourceBindingId') == binding['id']
        and original_source.get('OriginalDirectSourceSha256')
            == source.get('original_source_sha256') == binding.get('original_direct_source_sha256')
        and original_source.get('SupplierAcknowledgmentId') == acknowledgment['id']
        and original_source.get('Buyer',{}).get('WorkerAliasId') is None
        and original_source.get('Requester',{}).get('WorkerAliasId') is None
        and commercial.get('TenantId') == TENANT
        and commercial.get('LegalEntityId') == fixture['companyId']
        and commercial.get('BranchId') == fixture['branchId']
        and commercial.get('PurchaseOrderId') == fixture['issuedOrderId']
        and commercial.get('PurchaseOrderLineId') == fixture['issuedOrderLineId']
        and commercial.get('PurchaseRequestLineId') == fixture['approvedRequestLineId']
        and commercial.get('IssuedVersionId') == fixture['issuedVersionId']
        and commercial.get('IssuedSnapshotSha256')
            == source.get('issued_snapshot_sha256') == issued.get('commercial_snapshot_sha256')
        and commercial.get('SourceEvidenceSha256') == source.get('original_source_sha256')
        and commercial.get('PurchaseType') == source.get('purchase_type') == 'SERVICE'
        and commercial.get('CurrencyCode') == source.get('currency_code') == 'ZMW'
        and exact(commercial.get('AuthorizedQuantity')) == 1
        and exact(commercial.get('NetUnitPrice')) == decimal.Decimal('0.0001')
        and acknowledgment.get('issued_version_id') == fixture['issuedVersionId']
        and acknowledgment.get('purchase_order_id') == fixture['issuedOrderId']
        and acknowledgment.get('outcome') == 'Accepted'
        and acknowledgment.get('channel') == 'ManualRecord',
        'ServiceRetainedCommercialSourcePinsInvalid')
    require(source.get('purchase_order_id') == fixture['issuedOrderId']
        and source.get('purchase_order_line_id') == fixture['issuedOrderLineId']
        and source.get('purchase_request_line_id') == fixture['approvedRequestLineId']
        and source.get('issued_version_id') == fixture['issuedVersionId']
        and source.get('branch_id') == fixture['branchId'] and source.get('kind') == 'Service'
        and source.get('sequence') == 1
        and exact(source.get('accepted_quantity')) == decimal.Decimal('0.5')
        and exact(source.get('accepted_net_amount')) == decimal.Decimal('0.00005'), 'ServiceSourceExactFacts')
    require(draft.get('id') == service_id and draft.get('purchase_order_id') == fixture['issuedOrderId']
        and draft.get('kind') == 'Service' and draft.get('state') == 'Certified'
        and draft.get('creator_principal_id') == fixture['receiverPrincipalId']
        and draft.get('creator_worker_alias_id') is None and draft.get('revision') == 2,
        'ServiceDraftRetainedState')
    require(len({row.get('line_id') for row in bindings}) == 1
        and all(row.get('draft_id') == service_id and row.get('source_state_id') == source['id']
            and row.get('purchase_order_line_id') == fixture['issuedOrderLineId']
            and row.get('purchase_order_id') == fixture['issuedOrderId']
            and row.get('issued_version_id') == fixture['issuedVersionId']
            and row.get('branch_id') == fixture['branchId']
            and exact(row.get('quantity')) == decimal.Decimal('0.5')
            and exact(row.get('rejected_quantity')) == 0 for row in bindings),
        'ServiceDraftBindingsChanged')
    receipt_by_operation={row.get('operation'):row for row in receipts}
    require(set(receipt_by_operation) == {'Create','Certify'}
        and len(receipt_by_operation) == 2
        and all(row.get('draft_id') == service_id and row.get('branch_id') == fixture['branchId']
            and row.get('command_key') == (keys[0] if operation == 'Create' else keys[1])
            and row.get('actor_principal_id') == (fixture['receiverPrincipalId'] if operation == 'Create' else fixture['serviceOwnerPrincipalId'])
            and row.get('response_status') == (201 if operation == 'Create' else 200)
            and row.get('request_sha256') == (create_sha if operation == 'Create' else certify_sha)
            and hashlib.sha256(row.get('response_json','').encode()).hexdigest() == row.get('response_sha256')
            for operation,row in receipt_by_operation.items()), 'ServiceCommandReceiptsChanged')
    require(fact.get('kind') == 'Acceptance' and fact.get('operation') == 'Certify'
        and fact.get('actor_principal_id') == fixture['serviceOwnerPrincipalId']
        and fact.get('actor_worker_alias_id') is None and fact.get('command_key') == keys[1]
        and fact.get('draft_id') == service_id and fact.get('source_state_id') == source['id']
        and fact.get('branch_id') == fixture['branchId']
        and exact(fact.get('quantity')) == decimal.Decimal('0.5')
        and exact(fact.get('net_amount')) == decimal.Decimal('0.00005')
        and hashlib.sha256(fact.get('payload_json','').encode()).hexdigest() == fact.get('payload_sha256'),
        'ServiceAcceptanceFactChanged')
    try: payload=json.loads(fact['payload_json'],parse_float=decimal.Decimal)
    except (ValueError,TypeError): raise Failed('ServiceAcceptancePayloadMalformed') from None
    policy=one(new['procurement.policy_versions'],'id',fact.get('policy_version_id'))
    require(policy.get('policy_key') == 'RECEIVING' and policy.get('state') == 'Published'
        and policy.get('published_at') and policy.get('published_by'),
        'IndependentServicePolicyPublicationMissing')
    try: policy_rules=json.loads(policy['rules_json'],parse_float=decimal.Decimal,parse_int=decimal.Decimal)
    except (ValueError,TypeError): raise Failed('ServicePolicyRulesMalformed') from None
    policy_sha=hashlib.sha256(canonical_json(policy_rules).encode()).hexdigest()
    require(payload.get('Id') == fact['id'] and payload.get('AcceptanceRecordId') == service_id
        and payload.get('Quantity') is not None and exact(payload['Quantity']) == decimal.Decimal('0.5')
        and exact(payload.get('NetAmount')) == decimal.Decimal('0.00005')
        and payload.get('Actor',{}).get('PrincipalId') == fixture['serviceOwnerPrincipalId']
        and payload.get('Actor',{}).get('WorkerAliasId') is None
        and payload.get('CommandKey') == keys[1]
        and payload.get('Source') == original_source
        and payload.get('PolicyVersionId') == policy['id']
        and payload.get('PolicyRulesSha256') == policy_sha
        and payload.get('EvidenceReference') == certify['evidenceReference'], 'ServiceAcceptancePayloadChanged')
    require(Counter(row.get('command_key') for row in revisions) == Counter(keys)
        and {row.get('revision') for row in revisions} == {1,2}
        and all(row.get('draft_id') == service_id
            and row.get('actor_principal_id') == (fixture['receiverPrincipalId'] if row['revision'] == 1 else fixture['serviceOwnerPrincipalId'])
            and hashlib.sha256(row.get('snapshot_json','').encode()).hexdigest() == row.get('snapshot_sha256')
            for row in revisions)
        and {row.get('draft_revision_id') for row in bindings} == {row['id'] for row in revisions},
        'ServiceRevisionChainChanged')
    for revision in revisions:
        try: retained=json.loads(revision['snapshot_json'],parse_float=decimal.Decimal)
        except (ValueError,TypeError): raise Failed('ServiceRetainedSnapshotMalformed') from None
        ordinal=revision['revision']
        require(retained.get('Id') == service_id and retained.get('Version') == ordinal
            and retained.get('State') == ('Draft' if ordinal == 1 else 'Certified')
            and retained.get('Source') == original_source
            and retained.get('DeliverableReference') == create['deliverableReference']
            and retained.get('PeriodFrom') == create['periodFrom']
            and retained.get('PeriodTo') == create['periodTo']
            and exact(retained.get('Quantity')) == decimal.Decimal('0.5')
            and retained.get('Creator',{}).get('PrincipalId') == fixture['receiverPrincipalId'],
            'ServiceRetainedRevisionSnapshotChanged')
    outbox=added('outbox_events');audit=added('audit_events')
    require(len(outbox) == 2 and all(row.get('aggregate_type') == 'PrincipalReceiving'
        and row.get('aggregate_id') == service_id for row in outbox)
        and {row.get('idempotency_key') for row in outbox}
            == {'receiving.principal.'+key.replace('-','')+'.v2' for key in keys},
        'ServiceOutboxChanged')
    for row in outbox:
        key=keys[0] if row.get('event_type') == 'procurement.receiving.changed.v2' else keys[1]
        operation='Create' if key == keys[0] else 'Certify'
        try: value=json.loads(row['payload_json'])
        except (ValueError,TypeError): raise Failed('ServiceOutboxPayloadMalformed') from None
        require(value == {'id':service_id,'operation':operation,'commandKey':key,
            'revision':1 if operation == 'Create' else 2,
            'financePostingPerformed':False,'stockPostingPerformed':False}
            and row.get('event_type') == ('procurement.receiving.changed.v2' if operation == 'Create'
                else 'procurement.fulfilment.accepted.v2'), 'ServiceOutboxClaimsChanged')
    expected_audit=Counter()
    def audit_line(actor,type,id,action='create'): expected_audit[(actor,type,id,action)] += 1
    receiver=fixture['receiverPrincipalId'];owner=fixture['serviceOwnerPrincipalId']
    audit_line(receiver,'PrincipalReceivingSourceState',source['id'])
    audit_line(receiver,'PrincipalReceivingDraftHead',service_id)
    audit_line(receiver,'PrincipalReceivingDraftRevision',one(revisions,'revision',1)['id'])
    audit_line(receiver,'PrincipalReceivingDraftLineBinding',one(bindings,'draft_revision_id',one(revisions,'revision',1)['id'])['id'])
    audit_line(receiver,'PrincipalReceivingCommandReceipt',receipt_by_operation['Create']['id'])
    audit_line(owner,'PrincipalReceivingSourceState',source['id'],'update')
    audit_line(owner,'PrincipalReceivingDraftHead',service_id,'update')
    audit_line(owner,'PrincipalReceivingDraftRevision',one(revisions,'revision',2)['id'])
    audit_line(owner,'PrincipalReceivingDraftLineBinding',one(bindings,'draft_revision_id',one(revisions,'revision',2)['id'])['id'])
    audit_line(owner,'PrincipalReceivingFact',fact['id'])
    audit_line(owner,'PrincipalReceivingCommandReceipt',receipt_by_operation['Certify']['id'])
    audit_line(owner,'PurchaseOrderLine',fixture['issuedOrderLineId'],'update')
    audit_line(owner,'PurchaseRequestLine',fixture['approvedRequestLineId'],'update')
    require(Counter((row.get('actor_subject_id'),row.get('record_type'),row.get('record_id'),row.get('action'))
        for row in audit) == expected_audit and all(row.get('tenant_id') == TENANT
        and row.get('legal_entity_id') == fixture['companyId'] for row in audit),
        'ServiceExactAuditGraphChanged')
    correlations={operation:one(audit,'record_id',receipt['id']).get('correlation_id')
        for operation,receipt in receipt_by_operation.items()}
    require(all(isinstance(value,str) and value for value in correlations.values())
        and len(set(correlations.values())) == 2,
        'ServiceAuditCorrelationsMissing')
    require(all(row.get('correlation_id') == correlations['Create' if row.get('actor_subject_id') == receiver else 'Certify']
        for row in audit), 'ServiceAuditCorrelationChanged')
    prior={row['id'] for row in before['sessions']}
    sessions=[row for row in after['sessions'] if row['id'] not in prior]
    require(len(sessions) == 2 and Counter(row.get('local_user_id') for row in sessions)
        == Counter((receiver,owner)) and all(row.get('revoked_at') is not None for row in sessions),
        'ServiceRealSessionsNotRevoked')
    return {'schemaVersion':3,'status':'PASS_P30_SERVICE_OWNER_FACTS','database':fixture['database'],
        'system':SYSTEM,'fixtureSha256':fixture_sha,'serviceId':service_id,
        'nativeGoodsAndServiceHistoryUnchanged':True,'exactHalfServiceCertified':True,
        'twoRealSessionsRevoked':True,'productionTouched':False}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode',choices=('capture','check-baseline','verify'))
    for flag in ('fixture','proxy-receipt','baseline','container','database'):
        parser.add_argument('--'+flag,required=True)
    parser.add_argument('--browser-result');parser.add_argument('--output')
    args=parser.parse_args()
    try:
        fixture_path=Path(args.fixture);baseline_path=Path(args.baseline)
        fixture,sha=protected(fixture_path);proxy,_=protected(Path(args.proxy_receipt))
        boundary(fixture,proxy,sha,args.database)
        if args.mode == 'capture':
            output_path(baseline_path,fixture_path)
            facts=snapshot(args.container,args.database,fixture)
            require(not facts['sessions'], 'SessionsExistBeforeServiceBrowser')
            require(not any(row.get('purchase_order_line_id') == fixture['issuedOrderLineId']
                for row in facts['selectedRows']['procurement.principal_receiving_sources']),
                'ServiceBrowserSourceAlreadyConsumed')
            write(baseline_path,{'schemaVersion':3,'fixtureSha256':sha,'challenge':fixture['challenge'],'facts':facts})
            print('P30_SERVICE_PRIVATE_BASELINE_CAPTURED')
        else:
            baseline,_=protected(baseline_path,8_000_000)
            require(baseline.get('schemaVersion') == 3 and baseline.get('fixtureSha256') == sha
                and baseline.get('challenge') == fixture['challenge'], 'ServiceBaselineBinding')
            if args.mode == 'check-baseline':
                require(snapshot(args.container,args.database,fixture) == baseline['facts'],
                    'PreBrowserServiceBaselineChanged')
                print('P30_SERVICE_PRIVATE_BASELINE_UNCHANGED')
            else:
                require(args.browser_result and args.output, 'ServiceFinalInputsRequired')
                output_path(Path(args.output),fixture_path)
                browser,_=protected(Path(args.browser_result),65536)
                completion,_=protected(Path(fixture['completionPath']),4096)
                require(completion == {'schemaVersion':3,'challenge':fixture['challenge'],
                    'status':'BrowserFinished'},'ServiceControlPlaneCompletionMismatch')
                facts=snapshot(args.container,args.database,fixture)
                result=compare(fixture,baseline['facts'],facts,browser,sha)
                write(Path(args.output),result)
                print('P30_SERVICE_PRIVATE_OWNER_VERIFIED')
    except (Failed, KeyError, TypeError, OverflowError, ValueError) as error:
        print('P30_SERVICE_PRIVATE_OWNER_DENIED:'
            +(str(error) if isinstance(error,Failed) else 'InvalidEvidenceShape'),file=sys.stderr)
        return 1
    return 0

if __name__ == '__main__': sys.exit(main())
