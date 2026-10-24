#!/usr/bin/env python3
"""Read-only owner baseline/final check for the P30 receiving normal-cookie UI proof.

Capture after the private fixture is written and before browser login. This tool
never creates business records or authenticates as an application user.
"""

import argparse
from collections import Counter
import decimal
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlparse

from verify_p30_po_cookie_owner import (DB, Failed, GUID, HEX, SYSTEM, TENANT,
    container_guard, digest, output_path, protected, query, require, write)

RECEIVING = ('principal_receiving_sources', 'principal_receiving_drafts',
    'principal_receiving_revisions', 'principal_receiving_line_bindings',
    'principal_receiving_facts', 'principal_receiving_receipts')
CHANGING = {f'procurement.{name}' for name in RECEIVING} | {
    'procurement.purchase_request_lines', 'procurement.purchase_order_lines',
    'procurement.audit_events', 'procurement.outbox_events'}
LOGIN_FIELDS = {'last_login_at', 'failed_login_count', 'locked_until', 'updated_at', 'version'}


def exact(value):
    try:
        answer = decimal.Decimal(str(value))
    except (decimal.InvalidOperation, TypeError, ValueError):
        raise Failed('ExactAmountInvalid') from None
    require(answer.is_finite(), 'ExactAmountInvalid')
    return answer


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
        and CHANGING.issubset({'.'.join(pair) for pair in names}), 'OwnerTableCatalogChanged')
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
    for name, group in groups.items():
        stable = lambda row: {key: value for key, value in row.items() if key not in LOGIN_FIELDS} if name == 'hrm.local_users' else row
        rows[name] = {item.get('id', item.get('Id')): digest(stable(item)) for item in group}
        require(len(rows[name]) == len(group), 'DuplicateOwnerRowId')
    return {'schemaVersion': 3, 'database': database, 'system': SYSTEM, 'tableDigestRows': rows,
        'selectedRows': {name: groups[name] for name in CHANGING},
        'sessions': groups['hrm.local_sessions']}


def one(rows, name, value):
    found = [row for row in rows if row.get(name) == value]
    require(len(found) == 1, 'ExactOwnerRowMissing')
    return found[0]


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
        if name not in CHANGING:
            require(previous[name] == current[name], 'ExistingBusinessOrCoreIdentityChanged')
        else:
            require(previous[name].keys() <= current[name].keys(), 'ExistingBusinessRowDeleted')
            if name not in ('procurement.purchase_request_lines', 'procurement.purchase_order_lines'):
                require(all(current[name][id] == value for id, value in previous[name].items()),
                    'ExistingBusinessHistoryChanged')
    previous_rows, current_rows = before['selectedRows'], after['selectedRows']
    for name, expected_id in (('purchase_request_lines', fixture['approvedRequestLineId']),
        ('purchase_order_lines', fixture['issuedOrderLineId'])):
        table = 'procurement.' + name
        old = one(previous_rows[table], 'id', expected_id)
        new = one(current_rows[table], 'id', expected_id)
        require(exact(old['accepted_quantity']) == 0 and exact(new['accepted_quantity']) == 1,
            'ExactAcceptedCounterMissing')
        omitted = {'accepted_quantity', 'updated_at', 'version'}
        require({k: v for k, v in old.items() if k not in omitted}
            == {k: v for k, v in new.items() if k not in omitted}, 'SourceLineCommercialFieldsChanged')
        require(all(current[table][id] == value for id, value in previous[table].items()
            if id != expected_id), 'OtherSourceLineChanged')
    new = {name: [row for row in current_rows['procurement.' + name]
        if row['id'] not in previous['procurement.' + name]] for name in RECEIVING}
    require({name: len(items) for name, items in new.items()} == {
        'principal_receiving_sources': 1, 'principal_receiving_drafts': 1,
        'principal_receiving_revisions': 2, 'principal_receiving_line_bindings': 2,
        'principal_receiving_facts': 3, 'principal_receiving_receipts': 4},
        'ExactNewReceivingRowGraph')
    source = new['principal_receiving_sources'][0]
    draft = new['principal_receiving_drafts'][0]
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
    require({row.get('operation') for row in new['principal_receiving_receipts']} == set(expected)
        and {row.get('command_key') for row in new['principal_receiving_receipts']} == set(keys)
        and all(row.get('draft_id') == receipt_id and row.get('branch_id') == fixture['branchId']
            and row.get('actor_principal_id') == expected[row['operation']]
            and row.get('response_status') == (201 if row['operation'] == 'Create' else 200)
            and HEX.fullmatch(row.get('request_sha256', ''))
            and hashlib.sha256(row.get('response_json', '').encode()).hexdigest()
                == row.get('response_sha256') for row in new['principal_receiving_receipts']),
        'ExactCommandReceiptsInvalid')
    require(Counter(row.get('kind') for row in new['principal_receiving_facts'])
        == Counter({'Observation': 1, 'Inspection': 1, 'Acceptance': 1})
        and {row.get('command_key') for row in new['principal_receiving_facts']}
            == set(keys[1:])
        and all(row.get('draft_id') == receipt_id and row.get('source_state_id') == source['id']
            and row.get('branch_id') == fixture['branchId']
            and row.get('actor_worker_alias_id') is None for row in new['principal_receiving_facts']),
        'ExactReceivingFactsInvalid')
    for name in ('audit_events', 'outbox_events'):
        table = 'procurement.' + name
        added = [row for row in current_rows[table] if row['id'] not in previous[table]]
        require(len(added) == 4 if name == 'outbox_events' else bool(added),
            'AuditOutboxMissingOrDuplicated')
        if name == 'outbox_events':
            require(all(row.get('aggregate_type') == 'PrincipalReceiving'
                and row.get('aggregate_id') == receipt_id
                and row.get('idempotency_key') in {
                    'receiving.principal.' + key.replace('-', '') + '.v2' for key in keys}
                and json.loads(row.get('payload_json', '{}')).get('financePostingPerformed') is False
                and json.loads(row.get('payload_json', '{}')).get('stockPostingPerformed') is False
                for row in added), 'OutboxDeclaredOnly')
        else:
            require(all(row.get('tenant_id') == TENANT
                and row.get('legal_entity_id') == fixture['companyId']
                and row.get('actor_subject_id') in {fixture['receiverPrincipalId'],
                    fixture['inspectorPrincipalId'], fixture['acceptorPrincipalId']}
                for row in added), 'AuditActorOrScopeChanged')
    before_sessions = {row['id'] for row in before['sessions']}
    new_sessions = [row for row in after['sessions'] if row['id'] not in before_sessions]
    require(len(new_sessions) >= 3 and all(row.get('revoked_at') is not None for row in new_sessions),
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
