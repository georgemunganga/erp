#!/usr/bin/env python3
"""Read-only owner baseline/final check for the P30 receiving normal-cookie UI proof.

Capture after the private fixture is written and before browser login. This tool
never creates business records or authenticates as an application user.
"""

import argparse
from collections import Counter
import decimal
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlparse

from verify_p30_po_cookie_owner import (DB, Failed, GUID, HEX, SYSTEM, TENANT,
    canonical_json, container_guard, digest, output_path, protected, query, require, write)

RECEIVING = ('principal_receiving_sources', 'principal_receiving_drafts',
    'principal_receiving_revisions', 'principal_receiving_line_bindings',
    'principal_receiving_facts', 'principal_receiving_receipts')
CHANGING = {f'procurement.{name}' for name in RECEIVING} | {
    'procurement.purchase_request_lines', 'procurement.purchase_order_lines',
    'procurement.audit_events', 'procurement.outbox_events'}
SELECTED = CHANGING | {'procurement.principal_order_source_bindings',
    'procurement.principal_order_issued_versions',
    'procurement.principal_order_acknowledgments', 'procurement.policy_versions'}
LOGIN_FIELDS = {'last_login_at', 'failed_login_count', 'locked_until', 'updated_at', 'version'}


def exact(value):
    try:
        answer = decimal.Decimal(str(value))
    except (decimal.InvalidOperation, TypeError, ValueError):
        raise Failed('ExactAmountInvalid') from None
    require(answer.is_finite(), 'ExactAmountInvalid')
    return answer


def typed_utc_instant(value):
    """System.Text.Json DateTimeOffset writes a UTC offset and trims zeros."""
    require(isinstance(value, str) and len(value) <= 40, 'ReceivedAtInvalid')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise Failed('ReceivedAtInvalid') from None
    require(parsed.tzinfo is not None and parsed.utcoffset().total_seconds() == 0,
        'ReceivedAtMustBeExplicitUtc')
    # Python has microseconds; UI Date.toISOString sends milliseconds. The
    # original instant is retained; no browser clock value is used as authority.
    stamp = parsed.astimezone(timezone.utc).strftime('%Y-%m-%dT%H:%M:%S')
    fraction = f'{parsed.microsecond:06d}'.rstrip('0')
    return stamp + ('.' + fraction if fraction else '') + '+00:00'


def command_canonical_json(value):
    # PolicyJsonDigest re-writes strings with Utf8JsonWriter's default encoder.
    # The PO subset has no offset timestamps; Receiving adds the escaped plus.
    return canonical_json(value).replace('+', r'\u002B')


def stored_json_object(value, error_code):
    # to_jsonb(row) retains a jsonb column as an object; text captures may
    # retain its JSON spelling. Both must represent the same exact object.
    if isinstance(value, str):
        try:
            value = json.loads(value, parse_float=decimal.Decimal, parse_int=decimal.Decimal)
        except ValueError:
            raise Failed(error_code) from None
    require(isinstance(value, dict), error_code)
    return value


def command_fingerprints(browser, fixture, receipt_id, line_id, bodies):
    create = bodies['create']
    require(set(create).issubset({'purchaseOrderId', 'issuedVersionId', 'siteId',
        'receivedAt', 'deliveryNoteRef', 'notes', 'lines'})
        and set(create['lines'][0]).issubset({'id', 'purchaseOrderLineId',
            'receivedQuantity', 'rejectedQuantity'}), 'CreateInputFieldsUnexpected')
    hydrated = {name: create.get(name) for name in ('purchaseOrderId', 'issuedVersionId',
        'siteId', 'deliveryNoteRef', 'notes')}
    hydrated['receivedAt'] = typed_utc_instant(create['receivedAt'])
    hydrated['lines'] = [{name: create['lines'][0].get(name) for name in
        ('id', 'purchaseOrderLineId', 'receivedQuantity', 'rejectedQuantity')}]
    values = {'Create': {'operation': 'Create', 'kind': 'Goods', 'input': hydrated}}
    for operation in ('Record', 'Inspect', 'Accept'):
        raw = bodies[operation.lower()]
        fields = ('version', 'evidenceReference') if operation == 'Record' else (
            'version', 'sourceVersion', 'quantity', 'evidenceReference')
        require(set(raw) == set(fields), 'ActionInputFieldsUnexpected')
        payload = {'operation': operation, 'id': receipt_id,
            'input': {field: raw[field] for field in fields}}
        if operation != 'Record':
            payload['lineId'] = line_id
        values[operation] = payload
    return {operation: hashlib.sha256(command_canonical_json(value).encode()).hexdigest()
        for operation, value in values.items()}


def boundary(fixture, proxy, fixture_sha, database):
    require(fixture.get('schemaVersion') == 3 and fixture.get('controlPlaneOnly') is True
            and fixture.get('database') == database and fixture.get('tenantId') == TENANT
            and DB.fullmatch(database) and fixture.get('browserAuthenticationRequired')
            == 'NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE', 'FixtureBoundary')
    fields = ('companyId', 'branchId', 'receiverPrincipalId', 'inspectorPrincipalId',
        'acceptorPrincipalId', 'approvedRequestId', 'approvedRequestLineId',
        'supplierId', 'issuedOrderId', 'issuedVersionId', 'issuedOrderLineId',
        'nativeGoodsOrderId')
    require(all(isinstance(fixture.get(name), str) and GUID.fullmatch(fixture[name])
        and fixture[name] != '00000000-0000-0000-0000-000000000000' for name in fields)
        and HEX.fullmatch(fixture.get('challenge', '')), 'FixtureIdentifiers')
    require(proxy.get('schemaVersion') == 3 and proxy.get('transport') == 'normal-local-cookie-https-v1'
        and proxy.get('fixtureSha256') == fixture_sha and proxy.get('database') == database
        and proxy.get('apiUrl') == fixture.get('apiUrl')
        and proxy.get('injectsIdentityHeaders') is False
        and proxy.get('mocksBusinessResponses') is False
        and proxy.get('browserAcceptanceVerified') is False, 'ProxyBoundary')
    origin = urlparse(proxy.get('origin', ''))
    require(origin.scheme == 'https' and origin.hostname == '127.0.0.1'
        and origin.port and not origin.username and not origin.password, 'ProxyLoopbackBoundary')


def snapshot(container, database, fixture):
    container_guard(container)
    metadata = query(container, database, """BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT jsonb_build_object('database',current_database(),
 'system',(SELECT system_identifier::text FROM pg_control_system()),
 'tcp',inet_server_addr(),'version',current_setting('server_version_num')::integer,
 'tables',(SELECT jsonb_agg(jsonb_build_object('schema',schemaname,'name',tablename)
 ORDER BY schemaname,tablename) FROM pg_tables WHERE (schemaname IN ('procurement','core_approval')
 OR (schemaname='hrm' AND tablename IN ('local_users','local_sessions',
 'hr_user_branch_assignments','tenant_role_assignments','work_locations','legal_entities')))
 AND tablename !~ '^__'));
ROLLBACK;""")
    require(len(metadata) == 1 and metadata[0].get('database') == database
        and metadata[0].get('system') == SYSTEM and metadata[0].get('tcp') is None
        and 170000 <= metadata[0].get('version', 0) < 180000, 'PrivateOwnerBoundary')
    tables = metadata[0].get('tables')
    require(isinstance(tables, list) and len(tables) >= 50, 'OwnerTableCatalogMissing')
    names = [(item.get('schema'), item.get('name')) for item in tables]
    require(len(names) == len(set(names)) and all(re.fullmatch(r'[a-z_][a-z_0-9]*', schema or '')
        and re.fullmatch(r'[a-z_][a-z_0-9]*', table or '') for schema, table in names)
        and SELECTED.issubset({'.'.join(pair) for pair in names}), 'OwnerTableCatalogChanged')
    sql = ['BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;']
    for schema, table in names:
        sql.append(f"SELECT jsonb_build_object('table','{schema}.{table}',"
            f"'rows',(SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)"
            f' FROM "{schema}"."{table}" t));')
    sql.append('ROLLBACK;')
    result = query(container, database, '\n'.join(sql))
    require(len(result) == len(names), 'OwnerSnapshotCount')
    groups = {}
    for item, pair in zip(result, names):
        name = '.'.join(pair)
        require(item.get('table') == name and isinstance(item.get('rows'), list), 'OwnerSnapshotShape')
        groups[name] = item['rows']
    rows = {}
    browser_actors = {fixture['receiverPrincipalId'], fixture['inspectorPrincipalId'],
        fixture['acceptorPrincipalId']}
    for name, group in groups.items():
        def stable(row):
            if name == 'hrm.local_users' and row.get('id') in browser_actors:
                return {key: value for key, value in row.items() if key not in LOGIN_FIELDS}
            return row
        rows[name] = {item.get('id', item.get('Id')): digest(stable(item)) for item in group}
        require(len(rows[name]) == len(group), 'DuplicateOwnerRowId')
    return {'schemaVersion': 3, 'database': database, 'system': SYSTEM, 'tableDigestRows': rows,
        'selectedRows': {name: groups[name] for name in SELECTED},
        'sessions': groups['hrm.local_sessions']}


def one(rows, name, value):
    found = [row for row in rows if row.get(name) == value]
    require(len(found) == 1, 'ExactOwnerRowMissing')
    return found[0]


def source_line_acceptance(old, new, acceptor):
    require(exact(old['accepted_quantity']) == 0 and exact(new['accepted_quantity']) == 1,
        'ExactAcceptedCounterMissing')
    # Both counters are advanced in the acceptance SaveChanges transaction.
    # Its ordinary audit metadata must identify that same accepting principal.
    require(new.get('updated_by') == acceptor
        and new.get('version') == old['version'] + 1,
        'SourceLineAcceptanceAuditChanged')
    require(isinstance(new.get('updated_at'), str), 'SourceLineAcceptanceTimeMissing')
    updated = datetime.fromisoformat(new['updated_at'].replace('Z', '+00:00'))
    require(updated.tzinfo is not None and updated.utcoffset().total_seconds() == 0,
        'SourceLineAcceptanceTimeInvalid')
    require(isinstance(old.get('created_at'), str), 'SourceLineCreationTimeMissing')
    created = datetime.fromisoformat(old['created_at'].replace('Z', '+00:00'))
    require(created.tzinfo is not None and updated >= created,
        'SourceLineAcceptanceBeforeCreation')
    if old.get('updated_at') is not None:
        previous = datetime.fromisoformat(old['updated_at'].replace('Z', '+00:00'))
        require(previous.tzinfo is not None and updated >= previous,
            'SourceLineAcceptanceTimeReversed')
    omitted = {'accepted_quantity', 'updated_at', 'updated_by', 'version'}
    require({k: v for k, v in old.items() if k not in omitted}
        == {k: v for k, v in new.items() if k not in omitted},
        'SourceLineCommercialFieldsChanged')
    return updated


def compare(fixture, before, after, browser, fixture_sha):
    require(browser.get('status') == 'PASS_NORMAL_COOKIE_P30_RECEIVING_UI_ONLY'
        and browser.get('fixtureSha256') == fixture_sha
        and browser.get('challenge') == fixture['challenge']
        and browser.get('receivingBrowserActionsVerified') is True
        and browser.get('allSessionsRevoked') is True, 'BrowserControlNotAccepted')
    receipt_id = browser.get('receiptId')
    keys = [browser.get(name) for name in ('createCommandKey', 'recordCommandKey',
        'inspectCommandKey', 'acceptCommandKey')]
    require(isinstance(receipt_id, str) and GUID.fullmatch(receipt_id)
        and len(set(keys)) == 4 and all(isinstance(key, str) and GUID.fullmatch(key) for key in keys),
        'BrowserCommandIdentity')
    try:
        bodies = {name: json.loads(browser[name + 'RequestBody'], parse_float=decimal.Decimal)
            for name in ('create', 'record', 'inspect', 'accept')}
    except (KeyError, ValueError, TypeError):
        raise Failed('BrowserCommandBodiesMissing') from None
    create = bodies['create']
    require(isinstance(create, dict) and create.get('purchaseOrderId') == fixture['issuedOrderId']
        and create.get('issuedVersionId') == fixture['issuedVersionId']
        and create.get('siteId') == fixture['branchId']
        and isinstance(create.get('lines'), list) and len(create['lines']) == 1
        and create['lines'][0].get('purchaseOrderLineId') == fixture['issuedOrderLineId']
        and exact(create['lines'][0].get('receivedQuantity')) == 1
        and exact(create['lines'][0].get('rejectedQuantity')) == 0,
        'BrowserOriginalGoodsDraftBodyInvalid')
    require(bodies['record'].get('evidenceReference') == 'PRIVATE-REC:browser-physical-observation'
        and bodies['inspect'].get('evidenceReference') == 'PRIVATE-REC:browser-inspection'
        and exact(bodies['inspect'].get('quantity')) == 1
        and bodies['accept'].get('evidenceReference') == 'PRIVATE-REC:browser-acceptance'
        and exact(bodies['accept'].get('quantity')) == 1,
        'BrowserActionBodiesInvalid')
    previous, current = before['tableDigestRows'], after['tableDigestRows']
    require(previous.keys() == current.keys(), 'OwningTableCatalogChanged')
    for name in previous:
        if name == 'hrm.local_sessions':
            require(all(current[name].get(id) == value for id, value in previous[name].items()),
                'ExistingLocalSessionChanged')
            continue
        if name not in CHANGING:
            require(previous[name] == current[name], 'ExistingBusinessOrCoreIdentityChanged')
        else:
            require(previous[name].keys() <= current[name].keys(), 'ExistingBusinessRowDeleted')
            if name not in ('procurement.purchase_request_lines', 'procurement.purchase_order_lines'):
                require(all(current[name][id] == value for id, value in previous[name].items()),
                    'ExistingBusinessHistoryChanged')
    previous_rows, current_rows = before['selectedRows'], after['selectedRows']
    source_updates = []
    for name, expected_id in (('purchase_request_lines', fixture['approvedRequestLineId']),
        ('purchase_order_lines', fixture['issuedOrderLineId'])):
        table = 'procurement.' + name
        old = one(previous_rows[table], 'id', expected_id)
        new = one(current_rows[table], 'id', expected_id)
        source_updates.append(source_line_acceptance(old, new, fixture['acceptorPrincipalId']))
        require(all(current[table][id] == value for id, value in previous[table].items()
            if id != expected_id), 'OtherSourceLineChanged')
    require(len(set(source_updates)) == 1, 'SourceCountersNotUpdatedTogether')
    new = {name: [row for row in current_rows['procurement.' + name]
        if row['id'] not in previous['procurement.' + name]] for name in RECEIVING}
    require({name: len(items) for name, items in new.items()} == {
        'principal_receiving_sources': 1, 'principal_receiving_drafts': 1,
        'principal_receiving_revisions': 2, 'principal_receiving_line_bindings': 2,
        'principal_receiving_facts': 3, 'principal_receiving_receipts': 4},
        'ExactNewReceivingRowGraph')
    source = new['principal_receiving_sources'][0]
    draft = new['principal_receiving_drafts'][0]
    bindings = new['principal_receiving_line_bindings']
    binding = one(current_rows['procurement.principal_order_source_bindings'], 'id',
        source.get('source_binding_id'))
    issued = one(current_rows['procurement.principal_order_issued_versions'], 'id',
        fixture['issuedVersionId'])
    acknowledgment = one(current_rows['procurement.principal_order_acknowledgments'], 'id',
        source.get('acknowledgment_id'))
    require(source.get('source_json') and hashlib.sha256(source['source_json'].encode()).hexdigest()
        == source.get('source_sha256'), 'RetainedReceivingSourceDigestInvalid')
    try:
        original_source = json.loads(source['source_json'], parse_float=decimal.Decimal)
    except (ValueError, TypeError):
        raise Failed('RetainedReceivingSourceMalformed') from None
    commercial = original_source.get('Commercial')
    require(isinstance(commercial, dict)
        and original_source.get('SourceBindingId') == binding['id']
        and original_source.get('OriginalDirectSourceSha256')
            == source.get('original_source_sha256') == binding.get('original_direct_source_sha256')
        and original_source.get('SubmissionId') == source.get('submission_id')
        and original_source.get('FundingEvidenceId') == source.get('funding_evidence_id')
        and original_source.get('SupplierAcknowledgmentId') == acknowledgment['id']
        and original_source.get('Buyer', {}).get('PrincipalId') == binding.get('buyer_principal_id')
        and original_source.get('Buyer', {}).get('WorkerAliasId') is None
        and original_source.get('Requester', {}).get('WorkerAliasId') is None
        and commercial.get('TenantId') == TENANT
        and commercial.get('LegalEntityId') == fixture['companyId']
        and commercial.get('BranchId') == fixture['branchId']
        and commercial.get('PurchaseOrderId') == source.get('purchase_order_id')
        and commercial.get('PurchaseOrderLineId') == fixture['issuedOrderLineId']
        and commercial.get('PurchaseRequestLineId') == fixture['approvedRequestLineId']
        and commercial.get('IssuedVersionId') == fixture['issuedVersionId']
        and commercial.get('IssuedSnapshotSha256')
            == source.get('issued_snapshot_sha256') == issued.get('commercial_snapshot_sha256')
        and commercial.get('SourceEvidenceSha256') == source.get('original_source_sha256')
        and commercial.get('IssuedRevision') == 1
        and commercial.get('Kind') == 1
        and commercial.get('CategoryCode') == source.get('category_code')
        and commercial.get('PurchaseType') == source.get('purchase_type') == 'GOODS'
        and commercial.get('CurrencyCode') == source.get('currency_code') == 'ZMW'
        and exact(commercial.get('AuthorizedQuantity')) == 1
        and exact(commercial.get('NetUnitPrice')) == decimal.Decimal('0.0001')
        and source.get('original_source_sha256') == issued.get('original_direct_source_sha256')
        and acknowledgment.get('issued_version_id') == fixture['issuedVersionId']
        and acknowledgment.get('purchase_order_id') == fixture['issuedOrderId']
        and acknowledgment.get('outcome') == 'Accepted'
        and acknowledgment.get('channel') == 'ManualRecord',
        'RetainedCommercialSourcePinsInvalid')
    require(len({row.get('line_id') for row in bindings}) == 1
        and all(row.get('draft_id') == receipt_id
            and row.get('source_state_id') == source.get('id')
            and row.get('purchase_order_line_id') == fixture['issuedOrderLineId']
            and row.get('purchase_order_id') == fixture['issuedOrderId']
            and row.get('issued_version_id') == fixture['issuedVersionId']
            and row.get('branch_id') == fixture['branchId']
            and exact(row.get('quantity')) == 1
            and exact(row.get('rejected_quantity')) == 0
            for row in bindings), 'RetainedDraftLineBindingsInvalid')
    line_id = bindings[0]['line_id']
    require(browser.get('inspectTargetLineId') == line_id
        and browser.get('acceptTargetLineId') == line_id,
        'BrowserTargetLineChanged')
    fingerprints = command_fingerprints(browser, fixture, receipt_id, line_id, bodies)
    require(source.get('purchase_order_id') == fixture['issuedOrderId']
        and source.get('purchase_order_line_id') == fixture['issuedOrderLineId']
        and source.get('issued_version_id') == fixture['issuedVersionId']
        and source.get('purchase_request_line_id') == fixture['approvedRequestLineId']
        and source.get('branch_id') == fixture['branchId']
        and source.get('kind') == 'Goods' and source.get('sequence') == 3
        and exact(source.get('accepted_quantity')) == 1
        and exact(source.get('accepted_net_amount')) == decimal.Decimal('0.0001'),
        'BrowserReceivingSourceInvalid')
    require(draft.get('id') == receipt_id and draft.get('purchase_order_id') == fixture['issuedOrderId']
        and draft.get('kind') == 'Goods' and draft.get('state') == 'Recorded'
        and draft.get('creator_principal_id') == fixture['receiverPrincipalId']
        and draft.get('creator_worker_alias_id') is None, 'BrowserGoodsDraftInvalid')
    expected = {'Create': fixture['receiverPrincipalId'], 'Record': fixture['receiverPrincipalId'],
        'Inspect': fixture['inspectorPrincipalId'], 'Accept': fixture['acceptorPrincipalId']}
    command_keys = dict(zip(expected, keys))
    receipts = {row.get('operation'): row for row in new['principal_receiving_receipts']}
    require(set(receipts) == set(expected) and len(receipts) == 4
        and all(row.get('draft_id') == receipt_id
            and row.get('branch_id') == fixture['branchId']
            and row.get('command_key') == command_keys[operation]
            and row.get('actor_principal_id') == expected[operation]
            and row.get('response_status') == (201 if operation == 'Create' else 200)
            and row.get('request_sha256') == fingerprints[operation]
            and hashlib.sha256(row.get('response_json', '').encode()).hexdigest()
                == row.get('response_sha256')
            for operation, row in receipts.items()), 'ExactCommandReceiptsInvalid')
    facts = {row.get('kind'): row for row in new['principal_receiving_facts']}
    kinds = {'Observation': ('Record', decimal.Decimal(1), decimal.Decimal(0)),
        'Inspection': ('Inspect', decimal.Decimal(1), decimal.Decimal(0)),
        'Acceptance': ('Accept', decimal.Decimal(1), decimal.Decimal('0.0001'))}
    require(set(facts) == set(kinds) and len(facts) == 3
        and all(row.get('operation') == operation
            and row.get('actor_principal_id') == expected[operation]
            and row.get('command_key') == command_keys[operation]
            and row.get('quantity') is not None and exact(row['quantity']) == quantity
            and row.get('net_amount') is not None and exact(row['net_amount']) == amount
            and row.get('draft_id') == receipt_id
            and row.get('source_state_id') == source['id']
            and row.get('branch_id') == fixture['branchId']
            and row.get('actor_worker_alias_id') is None
            and row.get('draft_line_binding_id') in {binding['id'] for binding in bindings}
            for kind, row in facts.items()
            for operation, quantity, amount in (kinds[kind],)),
        'ExactReceivingFactsInvalid')
    require(all(hashlib.sha256(row.get('payload_json', '').encode()).hexdigest()
        == row.get('payload_sha256') and row.get('sequence') == sequence
        for sequence, row in enumerate((facts['Observation'], facts['Inspection'],
            facts['Acceptance']), start=1)), 'OrderedFactPayloadDigestInvalid')
    policy_ids = {row.get('policy_version_id') for row in facts.values()}
    require(len(policy_ids) == 1, 'ReceivingPolicyChangedAcrossFacts')
    policy = one(current_rows['procurement.policy_versions'], 'id', next(iter(policy_ids)))
    require(policy.get('policy_key') == 'RECEIVING' and policy.get('state') == 'Published'
        and policy.get('published_at') and policy.get('published_by'),
        'IndependentReceivingPolicyPublicationMissing')
    policy_rules = stored_json_object(policy['rules_json'], 'ReceivingPolicyRulesMalformed')
    policy_sha = hashlib.sha256(canonical_json(policy_rules).encode()).hexdigest()
    payloads = {}
    for kind, row in facts.items():
        try:
            payload = json.loads(row['payload_json'], parse_float=decimal.Decimal)
        except (ValueError, TypeError):
            raise Failed('ReceivingFactPayloadMalformed') from None
        operation = kinds[kind][0]
        actor_field = {'Observation': 'Receiver', 'Inspection': 'Inspector',
            'Acceptance': 'Actor'}[kind]
        require(payload.get('Id') == row['id']
            and payload.get('Source') == original_source
            and payload.get('CommandKey') == command_keys[operation]
            and payload.get('Sequence') == row['sequence']
            and payload.get('PolicyVersionId') == policy['id']
            and payload.get('PolicyRulesSha256') == policy_sha
            and payload.get(actor_field, {}).get('PrincipalId') == expected[operation]
            and payload.get(actor_field, {}).get('WorkerAliasId') is None
            and payload.get('EvidenceReference') == bodies[operation.lower()]['evidenceReference'],
            'ReceivingPayloadOriginalEvidenceChanged')
        payloads[kind] = payload
    require(payloads['Observation'].get('ReceiptId') == receipt_id
        and payloads['Observation'].get('ReceiptLineId') == line_id
        and payloads['Observation'].get('SiteId') == fixture['branchId']
        and payloads['Observation'].get('DeliveryNoteReference') == create.get('deliveryNoteRef')
        and exact(payloads['Observation'].get('ReceivedQuantity')) == 1
        and exact(payloads['Observation'].get('InitiallyRejectedQuantity')) == 0
        and payloads['Inspection'].get('ObservationId') == facts['Observation']['id']
        and exact(payloads['Inspection'].get('InspectedQuantity')) == 1
        and exact(payloads['Inspection'].get('QualifiedQuantity')) == 1
        and payloads['Acceptance'].get('ObservationId') == facts['Observation']['id']
        and payloads['Acceptance'].get('InspectionId') == facts['Inspection']['id']
        and payloads['Acceptance'].get('AcceptanceRecordId') == line_id
        and exact(payloads['Acceptance'].get('Quantity')) == 1
        and exact(payloads['Acceptance'].get('NetAmount')) == decimal.Decimal('0.0001'),
        'ReceivingPayloadOriginalPhysicalChainChanged')
    revisions = {row.get('command_key'): row for row in new['principal_receiving_revisions']}
    require(set(revisions) == {command_keys['Create'], command_keys['Record']}
        and revisions[command_keys['Create']].get('revision') == 1
        and revisions[command_keys['Record']].get('revision') == 2
        and all(row.get('actor_principal_id') == fixture['receiverPrincipalId']
            and row.get('draft_id') == receipt_id
            and hashlib.sha256(row.get('snapshot_json', '').encode()).hexdigest()
                == row.get('snapshot_sha256') for row in revisions.values())
        and {row.get('draft_revision_id') for row in bindings}
            == {row['id'] for row in revisions.values()}, 'ExactDraftRevisionHistoryInvalid')
    require(Counter(row.get('draft_revision_id') for row in bindings)
        == Counter({row['id']: 1 for row in revisions.values()}),
        'OneExactBindingPerRevisionRequired')
    for operation, key in (('Create', command_keys['Create']), ('Record', command_keys['Record'])):
        revision = revisions[key]
        try:
            snapshot = json.loads(revision['snapshot_json'], parse_float=decimal.Decimal)
        except (ValueError, TypeError):
            raise Failed('RetainedDraftSnapshotMalformed') from None
        require(snapshot.get('Id') == receipt_id
            and snapshot.get('Version') == (1 if operation == 'Create' else 2)
            and snapshot.get('State') == ('Draft' if operation == 'Create' else 'Recorded')
            and snapshot.get('PurchaseOrderId') == fixture['issuedOrderId']
            and snapshot.get('IssuedVersionId') == fixture['issuedVersionId']
            and snapshot.get('SiteId') == fixture['branchId']
            and snapshot.get('Creator', {}).get('PrincipalId') == fixture['receiverPrincipalId']
            and isinstance(snapshot.get('Lines'), list) and len(snapshot['Lines']) == 1
            and snapshot['Lines'][0].get('Id') == line_id
            and snapshot['Lines'][0].get('Source') == original_source
            and exact(snapshot['Lines'][0].get('Quantity')) == 1
            and exact(snapshot['Lines'][0].get('RejectedQuantity')) == 0,
            'RetainedDraftSnapshotSourceChanged')
    expected_audit = Counter()
    def audit(actor, record_type, record_id, action='create'):
        expected_audit[(actor, record_type, record_id, action)] += 1
    receiver = fixture['receiverPrincipalId']
    inspector = fixture['inspectorPrincipalId']
    acceptor = fixture['acceptorPrincipalId']
    audit(receiver, 'PrincipalReceivingSourceState', source['id'])
    audit(receiver, 'PrincipalReceivingDraftHead', receipt_id)
    for revision in revisions.values():
        audit(receiver, 'PrincipalReceivingDraftRevision', revision['id'])
    for binding in bindings:
        audit(receiver, 'PrincipalReceivingDraftLineBinding', binding['id'])
    audit(receiver, 'PrincipalReceivingCommandReceipt', receipts['Create']['id'])
    audit(receiver, 'PrincipalReceivingSourceState', source['id'], 'update')
    audit(receiver, 'PrincipalReceivingDraftHead', receipt_id, 'update')
    audit(receiver, 'PrincipalReceivingFact', facts['Observation']['id'])
    audit(receiver, 'PrincipalReceivingCommandReceipt', receipts['Record']['id'])
    audit(inspector, 'PrincipalReceivingSourceState', source['id'], 'update')
    audit(inspector, 'PrincipalReceivingFact', facts['Inspection']['id'])
    audit(inspector, 'PrincipalReceivingCommandReceipt', receipts['Inspect']['id'])
    audit(acceptor, 'PrincipalReceivingSourceState', source['id'], 'update')
    audit(acceptor, 'PrincipalReceivingFact', facts['Acceptance']['id'])
    audit(acceptor, 'PrincipalReceivingCommandReceipt', receipts['Accept']['id'])
    audit(acceptor, 'PurchaseOrderLine', fixture['issuedOrderLineId'], 'update')
    audit(acceptor, 'PurchaseRequestLine', fixture['approvedRequestLineId'], 'update')
    for name in ('audit_events', 'outbox_events'):
        table = 'procurement.' + name
        added = [row for row in current_rows[table] if row['id'] not in previous[table]]
        require(len(added) == 4 if name == 'outbox_events' else bool(added),
            'AuditOutboxMissingOrDuplicated')
        if name == 'outbox_events':
            expected_events = {('receiving.principal.' + key.replace('-', '') + '.v2',
                'procurement.fulfilment.accepted.v2' if operation == 'Accept'
                else 'procurement.receiving.changed.v2')
                for operation, key in command_keys.items()}
            require({(row.get('idempotency_key'), row.get('event_type')) for row in added}
                == expected_events, 'ExactReceivingOutboxCommands')
            require(all(row.get('aggregate_type') == 'PrincipalReceiving'
                and row.get('aggregate_id') == receipt_id
                and row.get('idempotency_key') in {
                    'receiving.principal.' + key.replace('-', '') + '.v2' for key in keys}
                for row in added), 'OutboxDeclaredOnly')
            for row in added:
                operation = next(operation for operation, key in command_keys.items()
                    if row['idempotency_key'] == 'receiving.principal.'
                        + key.replace('-', '') + '.v2')
                payload = stored_json_object(row['payload_json'], 'OutboxPayloadMalformed')
                require(payload == {'id': receipt_id, 'operation': operation,
                    'commandKey': command_keys[operation],
                    'revision': 1 if operation == 'Create' else 2,
                    'financePostingPerformed': False, 'stockPostingPerformed': False},
                    'ExactOutboxCommandPayloadChanged')
        else:
            require(Counter((row.get('actor_subject_id'), row.get('record_type'),
                row.get('record_id'), row.get('action')) for row in added)
                == expected_audit, 'ExactReceivingAuditGraph')
            receipt_correlations = {operation: one(added, 'record_id', receipt['id']).get('correlation_id')
                for operation, receipt in receipts.items()}
            require(all(isinstance(value, str) and value for value in receipt_correlations.values())
                and len(set(receipt_correlations.values())) == 4,
                'DistinctReceivingCommandAuditCorrelations')
            def audit_operation(row):
                actor = row.get('actor_subject_id')
                record = row.get('record_type')
                record_id = row.get('record_id')
                action = row.get('action')
                if actor == inspector:
                    return 'Inspect'
                if actor == acceptor:
                    return 'Accept'
                if record == 'PrincipalReceivingCommandReceipt':
                    return next(operation for operation, receipt in receipts.items()
                        if receipt['id'] == record_id)
                if record == 'PrincipalReceivingDraftRevision':
                    revision_key = one(new['principal_receiving_revisions'], 'id', record_id)['command_key']
                    return 'Create' if revision_key == command_keys['Create'] else 'Record'
                if record == 'PrincipalReceivingDraftLineBinding':
                    revision_id = one(bindings, 'id', record_id)['draft_revision_id']
                    return 'Create' if revision_id == revisions[command_keys['Create']]['id'] else 'Record'
                if record == 'PrincipalReceivingFact':
                    return 'Record'
                if record in ('PrincipalReceivingSourceState', 'PrincipalReceivingDraftHead'):
                    return 'Create' if action == 'create' else 'Record'
                raise Failed('UnknownReceivingAuditRecord')
            require(all(row.get('correlation_id') == receipt_correlations[audit_operation(row)]
                for row in added), 'AuditCommandCorrelationChanged')
            require(all(row.get('tenant_id') == TENANT
                and row.get('legal_entity_id') == fixture['companyId']
                and row.get('actor_subject_id') in {fixture['receiverPrincipalId'],
                    fixture['inspectorPrincipalId'], fixture['acceptorPrincipalId']}
                for row in added), 'AuditActorOrScopeChanged')
    before_sessions = {row['id'] for row in before['sessions']}
    new_sessions = [row for row in after['sessions'] if row['id'] not in before_sessions]
    require(len(new_sessions) == 3
        and Counter(row.get('local_user_id') for row in new_sessions)
            == Counter((fixture['receiverPrincipalId'], fixture['inspectorPrincipalId'],
                fixture['acceptorPrincipalId']))
        and all(row.get('revoked_at') is not None for row in new_sessions),
        'NormalLocalSessionsNotRevoked')
    return {'schemaVersion': 3, 'status': 'PASS_P30_RECEIVING_OWNER_FACTS',
        'database': fixture['database'], 'system': SYSTEM, 'fixtureSha256': fixture_sha,
        'receiptId': receipt_id, 'nativeHistoryUnchanged': True, 'exactNewFactsAndReceipts': True,
        'sourceCountersAcceptedOne': True, 'outboxNotPosting': True,
        'realLocalSessionsRevoked': True, 'productionTouched': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('capture', 'check-baseline', 'verify'))
    parser.add_argument('--fixture', type=Path, required=True)
    parser.add_argument('--proxy-receipt', type=Path, required=True)
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--browser-result', type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--container', required=True)
    parser.add_argument('--database', required=True)
    args = parser.parse_args()
    try:
        fixture, fixture_sha = protected(args.fixture)
        proxy, _ = protected(args.proxy_receipt)
        boundary(fixture, proxy, fixture_sha, args.database)
        if args.mode == 'capture':
            output_path(args.baseline, args.fixture)
            facts = snapshot(args.container, args.database, fixture)
            require(not facts['sessions'], 'SessionsExistBeforeBrowser')
            require(not any(row.get('purchase_order_line_id') == fixture['issuedOrderLineId']
                for row in facts['selectedRows']['procurement.principal_receiving_sources']),
                'BrowserSourceAlreadyConsumed')
            write(args.baseline, {'schemaVersion': 3, 'fixtureSha256': fixture_sha,
                'challenge': fixture['challenge'], 'facts': facts})
            print('P30_RECEIVING_PRIVATE_BASELINE_CAPTURED')
        else:
            baseline, _ = protected(args.baseline, 8_000_000)
            require(baseline.get('schemaVersion') == 3 and baseline.get('fixtureSha256') == fixture_sha
                and baseline.get('challenge') == fixture['challenge'], 'BaselineBinding')
            if args.mode == 'check-baseline':
                require(snapshot(args.container, args.database, fixture) == baseline['facts'],
                    'PreBrowserOwnerBaselineChanged')
                print('P30_RECEIVING_PRIVATE_BASELINE_UNCHANGED')
            else:
                require(args.browser_result and args.output, 'FinalInputsRequired')
                output_path(args.output, args.fixture)
                browser, _ = protected(args.browser_result, 65536)
                completion, _ = protected(Path(fixture['completionPath']), 4096)
                require(completion == {'schemaVersion': 3, 'challenge': fixture['challenge'],
                    'status': 'BrowserFinished'}, 'ControlPlaneCompletionMismatch')
                facts = snapshot(args.container, args.database, fixture)
                result = compare(fixture, baseline['facts'], facts, browser, fixture_sha)
                write(args.output, result)
                print('P30_RECEIVING_PRIVATE_OWNER_VERIFIED')
    except (Failed, KeyError, TypeError, OverflowError, ValueError) as error:
        print('P30_RECEIVING_PRIVATE_OWNER_DENIED:'
            + (str(error) if isinstance(error, Failed) else 'InvalidEvidenceShape'), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
