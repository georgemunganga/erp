#!/usr/bin/env python3
"""Run the compiled canonical draft CRUD probe in a fresh private DB; optional Unix-socket browser mode."""
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


def no_symlinks(directory):
    if directory.is_symlink() or any(p.is_symlink() for p in directory.rglob('*')):
        raise RuntimeError('Publication must contain only actual directories and regular files')


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--publication', type=Path, required=True, help='Complete already compiled/published tool output')
    p.add_argument('--runtime-image', required=True, help='Existing local immutable .NET10 ASP.NET image ID')
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--database', help='New procurement_crud_http_* name; default UTC timestamp')
    p.add_argument('--browser', action='store_true', help='After CLI PASS serve only the existing fixed Unix socket')
    p.add_argument('--recurring', action='store_true', help='Also prove recurring planning with a separate finite private Procurement login')
    a = p.parse_args()
    database = a.database or ('procurement_crud_http_' + datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d_%H%M%S'))
    if not re.fullmatch(r'procurement_crud_http_[a-z0-9_]{1,42}', database) or len(database) > 63:
        p.error('Require bounded new disposable CRUD database name')
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', a.runtime_image):
        p.error('Require an existing immutable runtime image ID; no pull is allowed')
    required = ['ProcurementCrudHttpProbe.dll', 'ProcurementCrudHttpProbe.deps.json',
                'ProcurementCrudHttpProbe.runtimeconfig.json', 'Mightyfin.Erp.Hrm.Api.dll']
    if a.output.exists() or not a.publication.is_dir() or not all((a.publication / f).is_file() for f in required):
        p.error('Require complete frozen publication and new protected output path')
    no_symlinks(a.publication)
    if a.output.resolve().is_relative_to(a.publication.resolve()):
        p.error('Output must be outside the publication tree')
    os.umask(0o077)
    a.output.mkdir(mode=0o700, parents=True)
    runtime = a.output / 'runtime'
    # Preserve then reject links, including any introduced during the source preflight.
    shutil.copytree(a.publication, runtime, symlinks=True)
    no_symlinks(runtime)
    frozen = hashes(runtime)
    browser = a.output / 'browser'
    if a.browser:
        browser.mkdir(mode=0o700)

    def docker(*args, data=None, label=None):
        r = subprocess.run(['docker', *args], input=data, text=True, capture_output=True)
        if label:
            (a.output / (label + '.log')).write_text(r.stdout + r.stderr)
        if r.returncode:
            raise RuntimeError('Private operation failed; retained protected evidence/DB, no reset or retry')
        return r.stdout

    meta = json.loads(docker('inspect', PRIVATE_PG))
    if len(meta) != 1 or meta[0]['Name'] != '/' + PRIVATE_PG or not meta[0]['State']['Running'] or meta[0]['HostConfig']['NetworkMode'] != 'none':
        raise RuntimeError('Require exact running private network-none PostgreSQL before any DDL')
    mounts = [m['Source'] for m in meta[0]['Mounts'] if m['Destination'] == '/var/run/postgresql' and m['Type'] == 'bind']
    if len(mounts) != 1 or not Path(mounts[0]).is_dir():
        raise RuntimeError('Existing private Unix socket bind required; never enable TCP or host networking')
    settings = dict(x.split('=', 1) for x in meta[0]['Config'].get('Env', []) if '=' in x)
    user = settings.get('POSTGRES_USER', 'postgres')
    password = settings.get('POSTGRES_PASSWORD')
    if not password and settings.get('POSTGRES_HOST_AUTH_METHOD') != 'trust':
        raise RuntimeError('Unsupported private authentication configuration; do not alter it')
    port = docker('exec', PRIVATE_PG, 'psql', '-X', '-At', '-v', 'VERBOSITY=terse', '-U', user, '-d', 'postgres', '-c', 'SHOW port').strip()
    if not port.isdecimal() or not 0 < int(port) < 65536:
        raise RuntimeError('Invalid private PostgreSQL port')
    docker('image', 'inspect', a.runtime_image)
    # createdb refuses an existing target. No source/restore DB, DROP/reset or session termination.
    docker('exec', PRIVATE_PG, 'createdb', '-U', user, database, label='create-db')
    recurring_role = None
    recurring_password = None
    if a.recurring:
        recurring_role = 'procurement_plan_rt_' + secrets.token_hex(8)
        recurring_password = secrets.token_urlsafe(32)
        if not re.fullmatch(r'[A-Za-z0-9_-]+', recurring_password):
            raise RuntimeError('Unsupported generated private secret encoding')
        # Generated identifiers/secrets only. SQL stdin is never persisted or printed.
        docker('exec', '-i', PRIVATE_PG, 'psql', '-X', '-q', '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=terse',
               '-U', user, '-d', 'postgres', data='CREATE ROLE "' + recurring_role
               + '" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD '
               + "'" + recurring_password + "';")


    def quote(value):
        return '"' + value.replace('"', '""') + '"'

    connection = 'Host=/authority-identity-private-socket;Port=' + port + ';Database=' + quote(database) + ';Username=' + quote(user)
    if password:
        connection += ';Password=' + quote(password)
    connection += ';Pooling=false;Include Error Detail=false'
    env = os.environ.copy()
    env['ConnectionStrings__Procurement'] = connection
    for key in ['PROCUREMENT_CRUD_BROWSER_SOCKET', 'PROCUREMENT_CRUD_BROWSER_MANIFEST', 'PROCUREMENT_CRUD_RECURRING', 'PROCUREMENT_CRUD_RECURRING_CONNECTION']:
        env.pop(key, None)
    cmd = ['docker', 'run', '--rm', '--pull=never', '--network=none', '--memory=768m', '--cpus=1',
           '--read-only', '--tmpfs', '/tmp:rw,nosuid,nodev,size=64m',
           '--mount', 'type=bind,src=' + str(runtime.resolve()) + ',dst=/crud-runtime,readonly',
           '--mount', 'type=bind,src=' + mounts[0] + ',dst=/authority-identity-private-socket,readonly',
           '--env', 'ConnectionStrings__Procurement', '--env', 'DOTNET_EnableDiagnostics=0', '--workdir', '/crud-runtime']
    if a.recurring:
        env['PROCUREMENT_CRUD_RECURRING'] = '1'
        env['PROCUREMENT_CRUD_RECURRING_CONNECTION'] = ('Host=/authority-identity-private-socket;Port=' + port
            + ';Database=' + quote(database) + ';Username=' + quote(recurring_role)
            + ';Password=' + quote(recurring_password) + ';Pooling=false;Include Error Detail=false')
        cmd += ['--env', 'PROCUREMENT_CRUD_RECURRING', '--env', 'PROCUREMENT_CRUD_RECURRING_CONNECTION']
    if a.browser:
        env['PROCUREMENT_CRUD_BROWSER_SOCKET'] = '/crud-browser-socket/api.sock'
        env['PROCUREMENT_CRUD_BROWSER_MANIFEST'] = '/crud-browser-socket/fixture.json'
        cmd += ['--mount', 'type=bind,src=' + str(browser.resolve()) + ',dst=/crud-browser-socket',
                '--env', 'PROCUREMENT_CRUD_BROWSER_SOCKET', '--env', 'PROCUREMENT_CRUD_BROWSER_MANIFEST']
    cmd += [a.runtime_image, 'dotnet', '/crud-runtime/ProcurementCrudHttpProbe.dll']
    manifest = {'database': database, 'privateContainer': PRIVATE_PG, 'runtimeImage': a.runtime_image,
                'networkMode': 'none', 'browserUnixSocketOnly': a.browser, 'runtimeFiles': frozen,
                'finiteRole': False, 'recurringRequested': a.recurring, 'recurringRuntimeRole': recurring_role, 'recurringFiniteDatabaseHttpProofRequiresToolPass': a.recurring, 'productionTouched': False,
                'buildProvenanceRequiresParentReview': True,
                'limits': 'Synthetic HRM/owner supplier fixture, draft CRUD only; no approval/Finance/receiving/AP claim'}
    (a.output / 'launch-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    with (a.output / 'probe.log').open('w') as log:
        run = subprocess.run(cmd, env=env, stdout=log, stderr=subprocess.STDOUT)
    unchanged = hashes(runtime) == frozen
    (a.output / 'completion.json').write_text(json.dumps({'exitCode': run.returncode, 'runtimeUnchanged': unchanged,
        'databaseRetained': True, 'recurringRequested': a.recurring, 'recurringRoleRetained': recurring_role, 'productionTouched': False}, indent=2) + '\n')
    if run.returncode or not unchanged:
        raise RuntimeError('Private CRUD run failed/changed; preserve first log/database, no automatic retry')
    # Do not print the entire HTTP log or any credential/configuration/row payload.
    print('Private canonical CRUD runner completed; review protected exact assertions/log and retained DB.')


if __name__ == '__main__':
    main()
