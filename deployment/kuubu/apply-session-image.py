"""Apply the tested session repair to the private qualification API only.
Requires a fresh verified backup receipt. Never migrates schema or opens ingress.
"""
from pathlib import Path
import hashlib
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request

assert os.geteuid() == 0
os.umask(0o077)
root = Path('/opt/kuubu-qualification')
build = Path('/opt/kuubu-builds/runtime-1f54ff04-build01')
revision = '1f54ff046cf69b7b4fb8604da9ad9bf6bc7b2f63'
backup_path = Path(sys.argv[1]).resolve()
assert backup_path.parent.parent == root/'receipts'
assert time.time() - backup_path.stat().st_mtime < 900, 'A fresh verified backup is required'
backup = json.loads(backup_path.read_text())
assert backup['all_table_rows_and_owners_acls_match'] and backup['login_users'] == backup['sessions'] == 0
assert hashlib.sha256((backup_path.parent/'core-hrm.dump').read_bytes()).hexdigest() == backup['dump_sha256']
assert re.search(r'Failed:\s+0,\s+Passed:\s+1982,\s+Skipped:\s+0', (build/'build.log').read_text())
smoke = json.loads((build/'runtime-smoke-01.json').read_text())
image = json.loads(subprocess.check_output(['docker', 'image', 'inspect', 'kuubu/erp-api:1f54ff04-build01']))[0]
assert image['Config']['Labels']['org.opencontainers.image.revision'] == revision
assert smoke['success'] is True and smoke['source'] == revision and smoke['image'] == image['Id']
old_revision = subprocess.check_output(['docker', 'inspect', 'kuubu-qualification-api-1', '--format',
    '{{index .Config.Labels "org.opencontainers.image.revision"}}'], text=True).strip()
assert old_revision == '408da398210c6e5e220077f4000ecdfc6cedb0aa', 'Inspect the existing release before another transition'
receipt = root/'receipts/image_1f54ff04_01'
receipt.mkdir(mode=0o700)
previous = (root/'project.env').read_text()
(receipt/'project.previous.env').write_text(previous)
with (receipt/'previous-api.log').open('x') as output:
    subprocess.run(['docker', 'logs', 'kuubu-qualification-api-1'], stdout=output, stderr=subprocess.STDOUT, check=True)
assert len(re.findall(r'^KUUBU_ERP_IMAGE=.*$', previous, re.MULTILINE)) == 1
new = re.sub(r'^KUUBU_ERP_IMAGE=.*$', 'KUUBU_ERP_IMAGE='+image['Id'], previous, flags=re.MULTILINE)
stage = root/'project.session-candidate.env'
with stage.open('x') as output:
    output.write(new)
compose_base = ['docker', 'compose', '--env-file']
compose_file = ['-f', str(root/'compose.qualification.yml')]
subprocess.run(compose_base+[str(stage)]+compose_file+['config', '--quiet'], check=True)

def rollout(filename):
    with (receipt/filename).open('x') as output:
        subprocess.run(compose_base+[str(root/'project.env')]+compose_file+
            ['up', '-d', '--no-deps', '--wait', '--wait-timeout', '45', 'api'], stdout=output, stderr=subprocess.STDOUT, check=True)

def check_http():
    statuses = {}
    for path, expected in [('/health/ready', 200), ('/api/v1/hrm/auth/me', 200), ('/api/v1/hrm/auth/users', 401)]:
        try:
            with urllib.request.urlopen('http://127.0.0.1:28911'+path, timeout=10) as response:
                status, data = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, data = error.code, error.read()
        assert status == expected, 'Unexpected qualification HTTP response'
        if path.endswith('/me'):
            assert json.loads(data) == {'authenticated': False, 'user': None}
        statuses[path] = status
    return statuses

os.replace(stage, root/'project.env')
try:
    rollout('rollout.log')
    http = check_http()
    ports = json.loads(subprocess.check_output(['docker', 'inspect', 'kuubu-qualification-api-1', '--format', '{{json .NetworkSettings.Ports}}']))
    assert ports['8080/tcp'] == [{'HostIp': '127.0.0.1', 'HostPort': '28911'}]
    actual = subprocess.check_output(['docker', 'inspect', 'kuubu-qualification-api-1', '--format',
        '{{index .Config.Labels "org.opencontainers.image.revision"}}'], text=True).strip()
    assert actual == revision
except Exception:
    rollback = root/'project.session-rollback.env'
    with rollback.open('x') as output:
        output.write(previous)
    os.replace(rollback, root/'project.env')
    rollout('rollback.log')
    check_http()
    (receipt/'failed.json').write_text(json.dumps({'rolled_back': True, 'customer_ingress': False}))
    raise
(receipt/'result.json').write_text(json.dumps({'source': revision, 'image': image['Id'],
    'previous_source': old_revision, 'backup_receipt': str(backup_path), 'http': http,
    'loopback_only': True, 'schema_migration': False, 'customer_ingress': False}, indent=2)+'\n')
print('Verified session-repair image is healthy on loopback. No schema, identity or public ingress changes.')
print('Receipt:', receipt/'result.json')
