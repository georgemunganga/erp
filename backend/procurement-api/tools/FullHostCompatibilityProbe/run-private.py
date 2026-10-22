#!/usr/bin/env python3
"""Clone the approved private canonical backup, then run the compiled actual-Program smoke offline."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import shutil

CONTAINER = 'procurement-approval-rehearsal-postgres'
SOURCE = 'proc_release_backup_20261022_canonical_0619'


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--publish-dir', type=Path, required=True)
    p.add_argument('--runtime-image', required=True, help='Existing local immutable sha256 image; never pulled')
    p.add_argument('--target-db', required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--diagnose-hrm-model', action='store_true', help='Read-only source model operations; never start Program or migrate')
    a = p.parse_args()
    if not re.fullmatch(r'proc_release_backup_20261022_smoke_[a-z0-9_]{1,28}', a.target_db) or len(a.target_db) > 63:
        p.error('Require new bounded smoke clone name')
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', a.runtime_image):
        p.error('Require existing immutable local runtime image ID')
    if a.publish_dir.is_symlink() or not a.publish_dir.is_dir():
        p.error('Require an ordinary compiled publication directory')
    if a.output.resolve().is_relative_to(a.publish_dir.resolve()):
        p.error('Evidence/output must be outside the immutable publication directory')
    required = ['FullHostCompatibilityProbe.dll', 'FullHostCompatibilityProbe.runtimeconfig.json',
                'FullHostCompatibilityProbe.deps.json', 'Mightyfin.Erp.Hrm.Api.dll']
    if not all((a.publish_dir / name).is_file() for name in required) or a.output.exists():
        p.error('Require full compiled publication and unused protected evidence path')
    os.umask(0o077)
    a.output.mkdir(parents=True, mode=0o700)

    def docker(*args, data=None, label=None):
        r = subprocess.run(['docker', *args], input=data, text=True, capture_output=True)
        if label:
            (a.output / (label + '.log')).write_text(r.stdout + r.stderr)
        if r.returncode:
            raise RuntimeError('Private operation failed; retain clone/evidence, do not print restricted diagnostics')
        return r.stdout

    info = json.loads(docker('inspect', CONTAINER))
    if len(info) != 1 or not info[0]['State']['Running'] or info[0]['HostConfig']['NetworkMode'] != 'none':
        raise RuntimeError('Require exact running network-none private PostgreSQL before clone DDL')
    socket = [m['Source'] for m in info[0]['Mounts'] if m['Destination'] == '/var/run/postgresql' and m['Type'] == 'bind']
    if len(socket) != 1 or not Path(socket[0]).is_dir():
        raise RuntimeError('Require the existing private bound Unix socket; never enable TCP networking')
    docker('image', 'inspect', a.runtime_image)  # --pull never below; no network dependency.
    exists = docker('exec', '-i', CONTAINER, 'psql', '-X', '-U', 'postgres', '-d', 'postgres',
                    '-qAt', '-v', 'ON_ERROR_STOP=1', '-v', 'VERBOSITY=terse',
                    data="SELECT count(*) FROM pg_database WHERE datname='" + SOURCE + "';")
    if exists.strip() != '1':
        raise RuntimeError('Approved retained canonical source is missing')
    # createdb refuses an existing target. Never DROP, terminate sessions, alter source or roles.
    docker('exec', '-i', CONTAINER, 'createdb', '-U', 'postgres', '--template=' + SOURCE, a.target_db,
           label='clone')
    # Freeze every loaded artifact before launch; later builds cannot replace running DLLs.
    if any(path.is_symlink() for path in a.publish_dir.rglob('*')):
        raise RuntimeError('Publication must contain regular files, not external configuration links')
    runtime = a.output / 'runtime'
    shutil.copytree(a.publish_dir, runtime)
    def runtime_hashes():
        return {str(path.relative_to(runtime)): hashlib.sha256(path.read_bytes()).hexdigest()
                for path in runtime.rglob('*') if path.is_file()}
    manifest = runtime_hashes()
    (a.output / 'run-manifest.json').write_text(json.dumps({'source': SOURCE, 'target': a.target_db,
        'runtimeImage': a.runtime_image, 'networkMode': 'none', 'publishedArtifacts': manifest,
        'limit': 'Owner credential on new private clone only; no finite-role or production claim'}, indent=2) + '\n')
    # Only the Unix socket is shared. No Docker socket, host network, source data directory,
    # actual configuration directory or unrestricted source database connection is mounted.
    connection = 'Host=/private-pg;Database=' + a.target_db + ';Username=postgres;Pooling=false;Include Error Detail=false'
    r = subprocess.run(['docker', 'run', '--rm', '--pull=never', '--network=none', '--memory=512m', '--cpus=1',
        '--read-only', '--tmpfs', '/tmp:rw,nosuid,nodev,size=64m',
        '--mount', 'type=bind,src=' + str(runtime.resolve()) + ',dst=/app,readonly',
        '--mount', 'type=bind,src=' + socket[0] + ',dst=/private-pg,readonly',
        '--mount', 'type=bind,src=' + str(a.output.resolve()) + ',dst=/evidence',
        '--env', 'ERP_HOST_SMOKE_CONNECTION=' + connection,
        '--env', 'ERP_HOST_SMOKE_RESULT=/evidence/result.json',
        '--env', 'DOTNET_EnableDiagnostics=0', '--workdir', '/app', a.runtime_image,
        'dotnet', '/app/FullHostCompatibilityProbe.dll'] + (['--diagnose-hrm-model'] if a.diagnose_hrm_model else []), text=True, capture_output=True)
    (a.output / 'host-smoke.log').write_text(r.stdout + r.stderr)
    if runtime_hashes() != manifest:
        raise RuntimeError('Frozen smoke runtime changed; no compatibility result can be accepted')
    if r.returncode and not (a.diagnose_hrm_model and r.returncode == 2):
        raise RuntimeError('Actual Program smoke blocked/failed; restricted summary/log and clone retained')
    result = json.loads((a.output / 'result.json').read_text())
    expected = 'OWNER_MODEL_DIAGNOSTIC_ONLY_PROGRAM_NOT_STARTED' if a.diagnose_hrm_model else 'PASS'
    if result.get('status') != expected or result.get('allTableValuesAndSchemasUnchanged') is not True:
        raise RuntimeError('Actual Program smoke did not prove unchanged clone')
    print('Private owner model diagnostic complete; Program not started; protected summary retained.' if a.diagnose_hrm_model else 'PASS private offline actual Program smoke; protected summary retained, no ERP rows output.')


if __name__ == '__main__':
    main()
