#!/usr/bin/env python3
"""Same-session current-Core export-grant revocation probe, private fixture only.

The separate fixture controller owns the Core CAS. This process never writes SQL.
"""
import hashlib
import json
import os
from pathlib import Path
import time

from verify_http import (Client, GUID, VENDOR_HEADER, admission, export, need,
                         protected)


def fresh(path: Path) -> bool:
    return path.is_absolute() and not path.exists() and path.parent.stat().st_mode & 0o777 == 0o700


def run() -> None:
    fixture_bytes = protected(os.environ['BE01_PRIVATE_FIXTURE'])
    output = Path(os.environ['BE01_PRIVATE_REVOCATION_RESULT'])
    config = admission(fixture_bytes, str(output))
    request_path = Path(config['revocationRequestPath'])
    receipt_path = Path(config['revocationReceiptPath'])
    need(fresh(request_path) and fresh(receipt_path) and
         request_path.parent == output.parent == receipt_path.parent,
         'revocation-handshake-paths')
    actor = config['actors']['reader']
    need(GUID.fullmatch(actor['principalId']) is not None and
         'procurement-vendor-export' in actor['currentActions'],
         'revocation-original-grant')
    client = Client(config, actor)
    client.login()
    export_id, rows, digest = export(client, 'vendors', VENDOR_HEADER)
    challenge = hashlib.sha256(fixture_bytes + export_id.encode()).hexdigest()
    request = {'schemaVersion': 1, 'challenge': challenge, 'principalId': actor['principalId'],
               'action': 'procurement-vendor-export', 'expectedPermissionsSha256':
               config['revocationExpectedPermissionsSha256']}
    with request_path.open('x') as stream:
        os.chmod(request_path, 0o600)
        json.dump(request, stream, sort_keys=True)
        stream.write('\n')
    deadline = time.monotonic() + 45
    while not receipt_path.exists() and time.monotonic() < deadline:
        time.sleep(0.2)
    need(receipt_path.exists(), 'core-revocation-receipt-timeout')
    receipt = json.loads(protected(str(receipt_path)))
    need(receipt == {'schemaVersion': 1, 'challenge': challenge,
                     'principalId': actor['principalId'],
                     'action': 'procurement-vendor-export',
                     'oldPermissionsSha256': config['revocationExpectedPermissionsSha256'],
                     'newPermissionsSha256': config['revocationNewPermissionsSha256'],
                     'status': 'CURRENT_CORE_ROLE_CAS_REVOKED'},
         'core-revocation-receipt')
    code, headers, _ = client.request('/api/procurement/v1/vendors/export')
    need(code == 403 and not any(k.lower() == 'x-export-id' for k in headers),
         'stale-cookie-grant-not-rechecked')
    client.logout()
    result = {'schemaVersion': 1, 'status': 'SAME_COOKIE_CORE_REVOCATION_HTTP_ONLY_OWNER_PENDING',
              'database': config['databaseName'], 'productCommit': config['productSourceCommit'],
              'exports': [{'role': 'reader', 'kind': 'vendors', 'scope': 'branch',
                           'query': '', 'rowCount': len(rows), 'exportId': export_id,
                           'payloadSha256': digest}],
              'revokedAction': 'procurement-vendor-export',
              'deniedStatus': code, 'browserUiVerified': False, 'productionTouched': False}
    with output.open('x') as stream:
        os.chmod(output, 0o600)
        json.dump(result, stream, indent=2, sort_keys=True)
        stream.write('\n')


if __name__ == '__main__':
    run()
