#!/usr/bin/env python3
"""Run existing actual HTTP workflow probe with distinct finite identity in a NEW private database."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess

PRIVATE_PG = 'procurement-approval-rehearsal-postgres'


def hashes(directory):
    return {str(p.relative_to(directory)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(directory.rglob('*')) if p.is_file()}


def reject_links(directory):
    if directory.is_symlink() or any(p.is_symlink() for p in directory.rglob('*')):
        raise RuntimeError('Whole publication must contain actual regular files and directories')


def quote_identifier(value):
    return '"' + value.replace('"', '""') + '"'


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--mode', choices=['receiving', 'matching', 'rfq'], required=True)
    p.add_argument('--publication', type=Path, required=True, help='Complete already compiled/published tool, no individual DLL overlays')
    p.add_argument('--runtime-image', required=True, help='Existing immutable local .NET10 ASP.NET runtime sha256 ID')
    p.add_argument('--output', type=Path, required=True, help='New private evidence directory; never reused')
    p.add_argument('--browser', action='store_true', help='Foreground --serve over private Unix socket; only receiving/rfq')
    a = p.parse_args()
    if a.browser and a.mode not in ('receiving', 'rfq'):
        p.error('Browser transport supports only receiving or rfq; matching CLI unchanged')
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', a.runtime_image):
        p.error('Require preexisting immutable local runtime image ID; no image pull')
    required = ['ApprovalHttpPostgresProbe.dll', 'ApprovalHttpPostgresProbe.deps.json',
                'ApprovalHttpPostgresProbe.runtimeconfig.json', 'Mightyfin.Erp.Hrm.Api.dll']
    if a.output.exists() or not a.publication.is_dir() or not all((a.publication / name).is_file() for name in required):
        p.error('Require complete compiled publication and new private output path')
    reject_links(a.publication)
    if a.output.resolve().is_relative_to(a.publication.resolve()):
        p.error('Evidence/output cannot be inside the publication tree')
    if ',' in str(a.output.resolve()) or ',' in str(a.publication.resolve()):
        p.error('Unsupported Docker bind path')
    os.umask(0o077)
    a.output.mkdir(mode=0o700, parents=True)
    runtime = a.output / 'runtime'
    shutil.copytree(a.publication, runtime, symlinks=True)
    reject_links(runtime)
    frozen = hashes(runtime)

    def docker(*args, data=None, label=None):
        r = subprocess.run(['docker', *args], input=data, text=True, capture_output=True)
        if label:
            (a.output / (label + '.log')).write_text(r.stdout + r.stderr)
        if r.returncode:
            raise RuntimeError('Private operation failed; preserve first protected log/DB/role, no retry or cleanup')
        return r.stdout

    inspected = json.loads(docker('inspect', PRIVATE_PG))
    if len(inspected) != 1 or inspected[0]['Name'] != '/' + PRIVATE_PG or not inspected[0]['State']['Running'] or inspected[0]['HostConfig']['NetworkMode'] != 'none':
        raise RuntimeError('Require exact private running network-none PostgreSQL before any DDL')
    mount = [m['Source'] for m in inspected[0]['Mounts'] if m['Destination'] == '/var/run/postgresql' and m['Type'] == 'bind']
    if len(mount) != 1 or not Path(mount[0]).is_dir() or ',' in mount[0]:
        raise RuntimeError('Require existing private Unix socket mount; no TCP/public network change')
    config = dict(x.split('=', 1) for x in inspected[0]['Config'].get('Env', []) if '=' in x)
    owner = config.get('POSTGRES_USER', 'postgres')
    owner_password = config.get('POSTGRES_PASSWORD')
    if any(c in value for value in [owner, owner_password or ''] for c in ('\r', '\n', '\0')):
        raise RuntimeError('Unsupported multiline private authentication value')
    if not owner_password and config.get('POSTGRES_HOST_AUTH_METHOD') != 'trust':
        raise RuntimeError('Unsupported private bootstrap authentication; never change it')
    port = docker('exec', PRIVATE_PG, 'psql', '-X', '-At', '-v', 'VERBOSITY=terse', '-U', owner, '-d', 'postgres', '-c', 'SHOW port').strip()
    if not port.isdecimal() or not 0 < int(port) < 65536:
        raise RuntimeError('Invalid private port')
    docker('image', 'inspect', a.runtime_image)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d_%H%M%S') + '_' + secrets.token_hex(3)
    mode = {'receiving': 'recv', 'matching': 'match', 'rfq': 'rfq'}[a.mode]
    database = 'proc_approval_http_can_' + mode + '_' + stamp
    role = 'proc_approval_runtime_can_' + mode + '_' + stamp
    if max(len(database), len(role)) > 63:
        raise RuntimeError('Private identifier exceeds PostgreSQL limit')
    password = secrets.token_urlsafe(32)  # alphabet never contains SQL quotes/newlines
    if not re.fullmatch(r'[A-Za-z0-9_-]+', password):
        raise RuntimeError('Generated secret has unsupported encoding')
    provision = ('CREATE ROLE ' + quote_identifier(role) + ' LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD '\
                 + "'" + password + "';\nCREATE DATABASE " + quote_identifier(database) + ' OWNER ' + quote_identifier(owner) + ' TEMPLATE template0;\n'\
                 + 'REVOKE CONNECT ON DATABASE ' + quote_identifier(database) + ' FROM PUBLIC;\nGRANT CONNECT ON DATABASE '\
                 + quote_identifier(database) + ' TO ' + quote_identifier(role) + ';\n')
    # Fresh only. Existing names error without resetting anything. Password SQL is stdin only,
    # never an argv, console value or persisted source/provisioning file.
    docker('exec', '-i', PRIVATE_PG, 'psql', '-X', '-q', '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=terse',
           '-U', owner, '-d', 'postgres', data=provision, label='provision')

    def connection(user, secret):
        value = 'Host=/pgsocket;Port=' + port + ';Database=' + quote_identifier(database) + ';Username=' + quote_identifier(user)
        if secret:
            value += ';Password=' + quote_identifier(secret)
        return value + ';Pooling=false;Include Error Detail=false'

    browser = a.output / 'browser'
    container_name = 'proc-can-browser-' + mode + '-' + stamp if a.browser else None
    if a.browser:
        browser.mkdir(mode=0o700)
    env_file = a.output / 'runtime.env'
    env_file.write_text('PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION=' + connection(role, password)
        + '\nPROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION=' + connection(owner, owner_password) + '\n')
    if a.browser:
        with env_file.open('a') as env:
            env.write('PROCUREMENT_APPROVAL_HTTP_PROBE_SOCKET=/procurement-browser/probe.sock\nPROCUREMENT_APPROVAL_HTTP_PROBE_MANIFEST=/procurement-browser/fixture.json\n')
            env.write(('PROCUREMENT_RECEIVING_BROWSER_VERIFY_ON_STOP' if a.mode == 'receiving' else 'PROCUREMENT_RFQ_BROWSER_VERIFY_ON_STOP') + '=1\n')
    # No real source/restored/production connection or credentials are accepted as inputs.
    manifest = {'database': database, 'runtimeRole': role, 'mode': '--' + a.mode,
        'privateContainer': PRIVATE_PG, 'runtimeImage': a.runtime_image, 'networkMode': 'none',
        'runtimeFiles': frozen, 'browserMode': a.browser, 'sandboxContainer': container_name,
        'arguments': (['--serve', '--' + a.mode] if a.browser else ['--' + a.mode]),
        'independentBrowserOwnerVerificationRequired': a.browser,
        'browserFixtureManifest': str(browser.resolve() / 'fixture.json') if a.browser else None,
        'browserSocket': str(browser.resolve() / 'probe.sock') if a.browser else None, 'buildProvenanceRequiresParentReview': True, 'productionTouched': False,
        'limits': 'Synthetic fixture auth/HRM identity + explicitly approved sandbox manual policy; actual handlers/migrations/finite grants. No external Finance or AP success.'}
    (a.output / 'launch-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    cmd = ['docker', 'run', '--rm', '--pull=never', '--network=none', '--memory=1100m', '--cpus=1', '--ulimit', 'core=0',
        '--read-only', '--tmpfs', '/tmp:rw,nosuid,nodev,size=96m',
        '--env-file', str(env_file.resolve()), '--env', 'DOTNET_EnableDiagnostics=0',
        '--mount', 'type=bind,src=' + str(runtime.resolve()) + ',dst=/runtime,readonly',
        '--mount', 'type=bind,src=' + mount[0] + ',dst=/pgsocket,readonly', '--workdir', '/runtime',
        a.runtime_image, 'dotnet', '/runtime/ApprovalHttpPostgresProbe.dll', '--' + a.mode]
    if a.browser:
        # Insert Docker-only options before the immutable image. Never publish a port.
        at = cmd.index(a.runtime_image)
        cmd[at:at] = ['--name', container_name, '--stop-timeout', '90', '--mount', 'type=bind,src=' + str(browser.resolve()) + ',dst=/procurement-browser']
        cmd.insert(cmd.index('--' + a.mode), '--serve')
    with (a.output / 'verification.log').open('w') as log:
        run = subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT)
    unchanged = hashes(runtime) == frozen
    (a.output / 'completion.json').write_text(json.dumps({'exitCode': run.returncode, 'runtimeUnchanged': unchanged,
        'databaseAndRoleRetained': True, 'productionTouched': False}, indent=2) + '\n')
    if run.returncode or not unchanged:
        raise RuntimeError('Actual private workflow failed/changed; retain evidence and do not retry automatically')
    print('Private finite workflow browser fixture stopped; review protected owner completion proof.' if a.browser else 'Private finite workflow CLI finished; review protected log and independent owning evidence.')


if __name__ == '__main__':
    main()
