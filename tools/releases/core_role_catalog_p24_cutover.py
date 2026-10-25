#!/usr/bin/env python3
"""Reviewed-plan-only Core catalog API/publisher/frontend cutover. No DB or static writes.

Only inspected current containers are cloned in memory. Never print or persist
Config, HostConfig, Env, Docker errors, or bearer/session data.
"""
import argparse
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import urllib.error

os.umask(0o077)
HERE = Path(__file__).resolve().parent
PLAN = HERE / 'plan.incomplete.json'
STATE = HERE / 'state.private.json'
BACKUP = HERE / 'frontend-override.original.private'
API = 'erp-hrm-api'
PUB = 'erp-hrm-outbox-publisher'
WRITERS = (PUB, API)
CONFIG_FIELDS = set('Hostname Domainname User AttachStdin AttachStdout AttachStderr ExposedPorts Tty OpenStdin StdinOnce Env Cmd Healthcheck ArgsEscaped Image Volumes WorkingDir Entrypoint NetworkDisabled MacAddress OnBuild Labels StopSignal StopTimeout Shell'.split())
HOST_FIELDS = set('Binds ContainerIDFile LogConfig NetworkMode PortBindings RestartPolicy AutoRemove VolumeDriver VolumesFrom ConsoleSize CapAdd CapDrop CgroupnsMode Dns DnsOptions DnsSearch ExtraHosts GroupAdd IpcMode Privileged PublishAllPorts ReadonlyRootfs SecurityOpt UTSMode UsernsMode ShmSize Runtime Isolation CpuShares Memory NanoCpus CgroupParent BlkioWeight BlkioWeightDevice BlkioDeviceReadBps BlkioDeviceReadIOps BlkioDeviceWriteBps BlkioDeviceWriteIOps CpuPeriod CpuQuota CpuRealtimeRuntime CpusetCpus CpusetMems Devices DeviceCgroupRules DeviceRequests MemoryReservation MemorySwap MemorySwappiness OomKillDisable PidsLimit Ulimits CpuCount CpuPercent IOMaximumIOps IOMaximumBandwidth MaskedPaths ReadonlyPaths Init Mounts Annotations StorageOpt Tmpfs KernelMemory KernelMemoryTCP'.split())

def insist(ok, message):
    if not ok:
        raise RuntimeError(message)

def canon(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()

def sha(value):
    return hashlib.sha256(value).hexdigest()

def file_sha(path):
    return sha(Path(path).read_bytes())

def artifact_inventory(root):
    """Hash every output file and symlink target without following linked dirs."""
    root = Path(root)
    insist(root.is_dir() and not root.is_symlink(), 'Frontend output root is not a real directory')
    entries = {}
    for current, dirs, files in os.walk(root, followlinks=False):
        for name in sorted(dirs + files):
            item = Path(current) / name
            relative = item.relative_to(root).as_posix()
            metadata = item.lstat()
            if stat.S_ISLNK(metadata.st_mode):
                entries[relative] = {'kind': 'symlink', 'target': os.readlink(item)}
                if name in dirs:
                    dirs.remove(name)
            elif stat.S_ISREG(metadata.st_mode):
                entries[relative] = {'kind': 'file', 'sha256': file_sha(item),
                                     'bytes': metadata.st_size,
                                     'mode': stat.S_IMODE(metadata.st_mode)}
            elif stat.S_ISDIR(metadata.st_mode):
                entries[relative] = {'kind': 'directory', 'mode': stat.S_IMODE(metadata.st_mode)}
            else:
                raise RuntimeError('Special frontend output node refused')
    return entries

def save_state(value):
    temp = STATE.with_suffix('.tmp')
    with temp.open('xb') as stream:
        stream.write(canon(value) + b'\n')
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temp, STATE)
    fd = os.open(HERE, os.O_DIRECTORY)
    os.fsync(fd)
    os.close(fd)

def advance(value):
    temp = STATE.with_suffix('.tmp')
    with temp.open('wb') as stream:
        stream.write(canon(value) + b'\n')
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temp, STATE)
    fd = os.open(HERE, os.O_DIRECTORY)
    os.fsync(fd)
    os.close(fd)

def docker(*args):
    subprocess.run(['docker', *args], stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, check=True, timeout=70)

def inspect(identifier):
    raw = subprocess.check_output(['docker', 'inspect', identifier],
                                  stderr=subprocess.DEVNULL, timeout=15)
    rows = json.loads(raw)
    insist(len(rows) == 1, 'Docker identity unavailable')
    return rows[0]

def docker_create(name, body):
    class Unix(http.client.HTTPConnection):
        def connect(self):
            self.sock = socket.socket(socket.AF_UNIX)
            self.sock.settimeout(30)
            self.sock.connect('/var/run/docker.sock')
    conn = Unix('localhost', timeout=30)
    conn.request('POST', '/containers/create?name=' + urllib.parse.quote(name),
                 canon(body), {'Content-Type': 'application/json'})
    reply = conn.getresponse()
    raw = reply.read()
    status = reply.status
    conn.close()
    insist(status == 201, 'Docker candidate create failed; no engine error body printed')
    identifier = json.loads(raw)['Id']
    insist(re.fullmatch('[0-9a-f]{64}', identifier) is not None, 'Invalid candidate ID')
    return identifier

def nondefault(value):
    return value not in (None, False, 0, '', [], {})

PUBLISH_LABEL = 'amipower.core-catalog.publish-sha256'
OWNED_LABEL = 'amipower.core-catalog.cutover-plan-sha256'

def candidate_config(old, image, plan_hash, publish_hash):
    labels = old['Config'].get('Labels') or {}
    insist(PUBLISH_LABEL not in labels and OWNED_LABEL not in labels,
           'Reserved candidate label already exists on old writer')
    result = dict(old['Config'])
    result['Image'] = image
    result['Labels'] = {**labels, PUBLISH_LABEL: publish_hash, OWNED_LABEL: plan_hash}
    return result

def clone_payload(old, image, plan_hash, publish_hash):
    c, h = old['Config'], old['HostConfig']
    insist(not any(nondefault(v) for k, v in c.items() if k not in CONFIG_FIELDS), 'Unreviewed container Config field')
    insist(not any(nondefault(v) for k, v in h.items() if k not in HOST_FIELDS), 'Unreviewed container HostConfig field')
    cfg = {k: v for k, v in candidate_config(old, image, plan_hash, publish_hash).items()
           if k in CONFIG_FIELDS}
    host = {k: v for k, v in h.items() if k in HOST_FIELDS}
    networks = {}
    for network, value in old['NetworkSettings']['Networks'].items():
        insist(value.get('IPAMConfig') is None, 'Static IP needs separate review')
        aliases = [a for a in value.get('Aliases') or [] if a not in (old['Id'], old['Id'][:12])]
        networks[network] = {'Aliases': aliases}
        for key in ('DriverOpts', 'GwPriority', 'Links'):
            if value.get(key) is not None:
                networks[network][key] = value[key]
    return {**cfg, 'HostConfig': host, 'NetworkingConfig': {'EndpointsConfig': networks}}

def owned_candidate(x, old, name, p, plan_hash):
    """Only an exact image, ownership label, and old host configuration may be removed."""
    if x['Name'] != '/' + name or x['Image'] != p['candidateImageId']:
        return False
    if sha(canon(x['HostConfig'])) != p['writers'][name]['hostConfigSha256']:
        return False
    expected = candidate_config(old, p['candidateImageId'], plan_hash,
                                p['_publishManifestSha256'])
    return x['Config'] == expected and x['Config'].get('Labels', {}).get(OWNED_LABEL) == plan_hash

def loaded_plan(reviewed_hash):
    insist(re.fullmatch('[0-9a-f]{64}', reviewed_hash or '') is not None,
           'Exact reviewed plan SHA-256 required')
    insist(file_sha(PLAN) == reviewed_hash, 'Reviewed plan bytes changed')
    p = json.loads(PLAN.read_text())
    insist(file_sha(Path(__file__).resolve()) == p['scriptSha256'], 'Reviewed script changed')
    insist(p['status'] == 'REVIEWED_FOR_EXECUTION', 'Plan is incomplete')
    insist(p['oldImageId'] == 'sha256:f768eea40d381429c4546218916142da34d2bdcf29db6b6b27810fd3c54a067d', 'Old image changed')
    insist(re.fullmatch('sha256:[0-9a-f]{64}', p.get('candidateImageId') or '') is not None, 'Candidate image not pinned')
    insist(p['candidateImageId'] != p['oldImageId'], 'Candidate equals old image')
    receipt = p.get('candidateProvenance') or {}
    path = Path(receipt.get('path') or '')
    insist(path.is_absolute() and path.is_file() and not path.is_symlink(), 'Genuine publication receipt missing')
    insist(file_sha(path) == receipt.get('sha256'), 'Publication receipt changed')
    proof = json.loads(path.read_text())
    insist(proof.get('status') == 'PASS_GENUINE_CORE_CATALOG_API_PUBLICATION', 'Publication not accepted')
    insist(proof.get('baseImageId') == p['oldImageId'] and proof.get('candidateImageId') == p['candidateImageId'], 'Image provenance mismatch')
    image = inspect(p['candidateImageId'])
    base = inspect(p['oldImageId'])
    insist(image['Id'] == p['candidateImageId'] and base['Id'] == p['oldImageId'], 'Image identity changed')
    layers = image['RootFS']['Layers']
    insist(layers[:len(base['RootFS']['Layers'])] == base['RootFS']['Layers'], 'Candidate is not layered on P24 image')
    manifest = proof.get('publishManifestSha256')
    insist(re.fullmatch('[0-9a-f]{64}', manifest or '') is not None, 'Publication manifest missing')
    insist(image['Config'].get('Labels', {}).get(PUBLISH_LABEL) == manifest, 'Image publication label mismatch')
    p['_publishManifestSha256'] = manifest  # In memory only; reviewed plan bytes stay unchanged.
    return p

def baseline(p):
    found = {}
    for name in WRITERS:
        x = inspect(name)
        pin = p['writers'][name]
        insist(x['Id'] == pin['id'] and x['Image'] == p['oldImageId'], 'Old writer identity changed')
        insist(sha(canon(x['Config'])) == pin['configSha256'], 'Old writer Config changed')
        insist(sha(canon(x['HostConfig'])) == pin['hostConfigSha256'], 'Old writer HostConfig changed')
        insist(x['State']['Running'], 'Old writer not running')
        insist(sorted(x['NetworkSettings']['Networks']) == pin['networks'], 'Old writer networks changed')
        found[name] = x
    insist(inspect(API)['State'].get('Health', {}).get('Status') == 'healthy', 'Old API unhealthy')
    override = Path(p['frontend']['override'])
    insist(file_sha(override) == p['frontend']['oldOverrideSha256'], 'Frontend override changed')
    insist(file_sha(p['nginxConfig']) == p['nginxSha256'], 'Nginx aliases changed')
    repo = Path(p['frontend']['repo'])
    head = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip()
    insist(head == p['frontend']['sourceCommit'], 'Frontend source changed')
    dirty = subprocess.check_output(['git', '-C', str(repo), 'status', '--porcelain'], text=True)
    insist(not dirty, 'Frontend source not clean')
    insist(file_sha(p['frontend']['entry']) == p['frontend']['entrySha256'], 'Frontend artifact changed')
    inventory_spec = p['frontend']['outputInventory']
    manifest = Path(inventory_spec['path'])
    insist(manifest.is_absolute() and manifest.is_file() and not manifest.is_symlink(),
           'Frontend output inventory missing')
    insist(file_sha(manifest) == inventory_spec['sha256'], 'Frontend inventory evidence changed')
    actual = artifact_inventory(p['frontend']['outputRoot'])
    insist(actual == json.loads(manifest.read_text())['entries'] and
           sha(canon(actual)) == inventory_spec['treeSha256'],
           'Complete frontend output graph changed')
    preservation = p['frontend']['oldAssetPreservation']
    insist(file_sha(preservation['path']) == preservation['sha256'],
           'Old hashed-asset preservation proof changed')
    return found

def wait_api(identifier):
    for _ in range(45):
        x = inspect(identifier)
        if x['State'].get('Running') and x['State'].get('Health', {}).get('Status') == 'healthy':
            return
        time.sleep(2)
    raise RuntimeError('Candidate API did not become healthy')

def wait_publisher(identifier):
    for _ in range(5):
        x = inspect(identifier)
        insist(x['State'].get('Running') and x['RestartCount'] == 0, 'Publisher exited or restarted')
        time.sleep(2)

def frontend_override(p):
    f = p['frontend']
    return ('[Service]\nWorkingDirectory=' + f['workdir'] + '\nExecStart=\nExecStart=' +
            f['node'] + ' ' + f['entry'] + '\n').encode()

def local_get(path):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen('http://127.0.0.1:3300' + path, timeout=5) as reply:
                insist(reply.status == 200, 'Frontend route not healthy')
                return
        except urllib.error.HTTPError as error:
            insist(error.code in (502, 503, 504), 'Frontend route returned an unexpected status')
        except (urllib.error.URLError, TimeoutError, ConnectionResetError):
            pass
        time.sleep(0.25)
    raise RuntimeError('Frontend did not become ready within 30 seconds')

def catalog_denies_anonymous():
    connection = http.client.HTTPConnection('127.0.0.1', 18080, timeout=5)
    try:
        connection.request('GET', '/api/core/v1/role-permissions',
                           headers={'Accept': 'application/json'})
        reply = connection.getresponse()
        reply.read(1024)  # Never print or retain a response body.
        insist(reply.status == 401, 'New admin catalog route must deny anonymous GET with 401')
    finally:
        connection.close()

def restore(p, state):
    # Restore only our exact candidates and the two preserved old IDs.
    errors = []
    for name in WRITERS:
        identifier = state['candidateIds'].get(name)
        try:
            old = inspect(p['writers'][name]['id'])
            insist(old['Id'] == p['writers'][name]['id'] and old['Image'] == p['oldImageId'],
                   'Retained old writer identity changed')
            try:
                x = inspect(identifier or name)
            except subprocess.CalledProcessError:
                x = None  # Exact candidate was already removed by a prior rollback attempt.
            if x is not None and x['Id'] != old['Id']:
                insist(identifier is None or x['Id'] == identifier,
                       'Recorded candidate ID changed')
                insist(owned_candidate(x, old, name, p, state['reviewedPlanSha256']),
                       'Candidate ownership/image/host configuration mismatch')
                if identifier is None:
                    state['candidateIds'][name] = x['Id']  # Recover exact owned create-before-write window.
                    advance(state)
                if x['State']['Running']:
                    docker('stop', '--time', '30', x['Id'])
                docker('rm', x['Id'])
        except Exception as exc:
            errors.append(name + ':' + type(exc).__name__)
    for name in (API, PUB):
        try:
            old_id = p['writers'][name]['id']
            x = inspect(old_id)
            insist(x['Id'] == old_id and x['Image'] == p['oldImageId'], 'Retained old identity changed')
            if x['Name'] != '/' + name:
                insist(x['Name'] == '/' + state['retainedNames'][name], 'Retained name changed')
                docker('rename', old_id, name)
            if not inspect(old_id)['State']['Running']:
                docker('start', old_id)
            if name == API:
                wait_api(old_id)
            else:
                wait_publisher(old_id)
        except Exception as exc:
            errors.append(name + ':' + type(exc).__name__)
    if state.get('overrideSwitched'):
        try:
            insist(file_sha(BACKUP) == p['frontend']['oldOverrideSha256'], 'Override backup changed')
            target = Path(p['frontend']['override'])
            pending = target.with_suffix('.rollback-pending')
            insist(not pending.exists(), 'Unowned pending override')
            pending.write_bytes(BACKUP.read_bytes())
            pending.chmod(target.stat().st_mode & 0o777)
            os.replace(pending, target)
            docker_systemctl('daemon-reload')
            docker_systemctl('restart', 'amipower-erp-web.service')
        except Exception as exc:
            errors.append('frontend:' + type(exc).__name__)
    state['phase'] = 'ROLLED_BACK' if not errors else 'ROLLBACK_INCOMPLETE'
    state['rollbackErrors'] = errors
    advance(state)
    insist(not errors, 'Rollback incomplete; inspect private state')

def docker_systemctl(*args):
    subprocess.run(['systemctl', *args], stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL, check=True, timeout=70)

def cutover(p):
    insist(not STATE.exists() and not BACKUP.exists(), 'Cutover evidence already exists')
    old = baseline(p)
    with BACKUP.open('xb') as stream:
        stream.write(Path(p['frontend']['override']).read_bytes())
        stream.flush()
        os.fsync(stream.fileno())
    BACKUP.chmod(0o600)
    insist(file_sha(BACKUP) == p['frontend']['oldOverrideSha256'], 'Override backup mismatch')
    stamp = str(int(time.time()))
    state = {'phase': 'PREPARED', 'oldIds': {n: p['writers'][n]['id'] for n in WRITERS},
             'retainedNames': {n: n + '-before-core-catalog-' + stamp for n in WRITERS},
             'candidateIds': {}, 'overrideSwitched': False,
             'reviewedPlanSha256': file_sha(PLAN)}
    save_state(state)
    try:
        for name in WRITERS:
            docker('stop', '--time', '30', p['writers'][name]['id'])
        state['phase'] = 'OLD_STOPPED'; advance(state)
        for name in (API, PUB):
            docker('rename', p['writers'][name]['id'], state['retainedNames'][name])
            identifier = docker_create(name, clone_payload(old[name], p['candidateImageId'],
                state['reviewedPlanSha256'], p['_publishManifestSha256']))
            state['candidateIds'][name] = identifier
            state['phase'] = 'CANDIDATE_CREATED'; advance(state)
        docker('start', state['candidateIds'][API])
        wait_api(state['candidateIds'][API])
        catalog_denies_anonymous()
        docker('start', state['candidateIds'][PUB])
        wait_publisher(state['candidateIds'][PUB])
        for name in (API, PUB):
            x = inspect(state['candidateIds'][name])
            insist(owned_candidate(x, old[name], name, p, state['reviewedPlanSha256']),
                   'Candidate Config/HostConfig/image/labels mismatch')
            insist(sorted(x['NetworkSettings']['Networks']) == p['writers'][name]['networks'], 'Candidate networks changed')
        state['phase'] = 'BACKEND_HEALTHY'; advance(state)
        override = Path(p['frontend']['override'])
        pending = override.with_suffix('.core-catalog-pending')
        insist(not pending.exists(), 'Unowned pending override')
        with pending.open('xb') as stream:
            stream.write(frontend_override(p))
            stream.flush()
            os.fsync(stream.fileno())
        pending.chmod(override.stat().st_mode & 0o777)
        # Record the restoration obligation before the atomic override swap.
        state['overrideSwitched'] = True; state['phase'] = 'FRONTEND_SWITCHING'; advance(state)
        os.replace(pending, override)
        state['phase'] = 'FRONTEND_SWITCHED'; advance(state)
        docker_systemctl('daemon-reload')
        docker_systemctl('restart', 'amipower-erp-web.service')
        for route in ('/sign-in', '/core/workspace/settings'):
            local_get(route)
        insist(sha(canon(artifact_inventory(p['frontend']['outputRoot']))) ==
               p['frontend']['outputInventory']['treeSha256'],
               'Frontend output changed during cutover')
        insist(file_sha(p['nginxConfig']) == p['nginxSha256'], 'Nginx aliases changed')
        state['phase'] = 'CUTOVER_COMPLETE'; advance(state)
    except BaseException:
        restore(p, state)
        raise

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=('self-test', 'preflight', 'cutover', 'rollback'))
    parser.add_argument('--reviewed-plan-sha256')
    args = parser.parse_args()
    if args.mode == 'self-test':
        plan_hash, publish_hash = 'b' * 64, 'c' * 64
        image = 'sha256:' + '1' * 64
        sample = {'Id': 'a' * 64, 'Config': {'Image': 'old', 'Env': ['A=x'],
                  'Entrypoint': ['dotnet'], 'Labels': {'original': 'retained'}},
                  'HostConfig': {'PortBindings': {'8080/tcp': [{'HostIp': '127.0.0.1', 'HostPort': '18080'}]}},
                  'NetworkSettings': {'Networks': {'hrm-api_default': {'IPAMConfig': None, 'Aliases': ['a' * 64, 'api']}}}}
        payload = clone_payload(sample, image, plan_hash, publish_hash)
        insist(payload['Image'] == image and payload['Env'] == ['A=x'], 'Config preservation test')
        insist(payload['HostConfig'] == sample['HostConfig'], 'Host preservation test')
        insist(payload['NetworkingConfig']['EndpointsConfig']['hrm-api_default']['Aliases'] == ['api'], 'No dynamic ID alias test')
        insist(payload['Labels'] == {'original': 'retained', PUBLISH_LABEL: publish_hash,
                                    OWNED_LABEL: plan_hash}, 'Only two candidate labels may be added')
        p = {'candidateImageId': image, '_publishManifestSha256': publish_hash,
             'writers': {'api': {'hostConfigSha256': sha(canon(sample['HostConfig']))}}}
        candidate = {'Name': '/api', 'Image': image, 'Config': candidate_config(sample, image, plan_hash, publish_hash),
                     'HostConfig': sample['HostConfig']}
        insist(owned_candidate(candidate, sample, 'api', p, plan_hash), 'Owned unrecorded candidate test')
        alien = json.loads(json.dumps(candidate)); alien['Config']['Labels'][OWNED_LABEL] = 'd' * 64
        insist(not owned_candidate(alien, sample, 'api', p, plan_hash), 'Wrong plan label must be denied')
        alien = json.loads(json.dumps(candidate)); alien['HostConfig']['PortBindings'] = {}
        insist(not owned_candidate(alien, sample, 'api', p, plan_hash), 'Wrong host config must be denied')
        alien = json.loads(json.dumps(candidate)); alien['Image'] = 'sha256:' + 'e' * 64
        insist(not owned_candidate(alien, sample, 'api', p, plan_hash), 'Wrong image must be denied')
        sample['NetworkSettings']['Networks']['hrm-api_default']['IPAMConfig'] = {'IPv4Address': '1.2.3.4'}
        try:
            clone_payload(sample, image, plan_hash, publish_hash)
            raise AssertionError('Static IP unexpectedly accepted')
        except RuntimeError:
            pass
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'output'; root.mkdir()
            (root / 'asset.js').write_text('immutable asset')
            (root / 'outside').symlink_to('/tmp', target_is_directory=True)
            entries = artifact_inventory(root)
            insist(entries['asset.js']['kind'] == 'file' and
                   entries['outside'] == {'kind': 'symlink', 'target': '/tmp'} and
                   len(entries) == 2, 'Output inventory followed an external symlink')
        print('10 pure source guards passed')
        return
    p = loaded_plan(args.reviewed_plan_sha256)
    if args.mode == 'preflight':
        baseline(p)
        print('PASS_READONLY_REVIEWED_CORE_CATALOG_CUTOVER_ADMISSION')
    elif args.mode == 'cutover':
        cutover(p)
        print('CUTOVER_COMPLETE_BACKEND_AND_FRONTEND')
    else:
        insist(STATE.exists(), 'No owned cutover state')
        state = json.loads(STATE.read_text())
        insist(state['reviewedPlanSha256'] == file_sha(PLAN), 'State/plan review hash mismatch')
        insist(state['oldIds'] == {n: p['writers'][n]['id'] for n in WRITERS}, 'State/plan old IDs mismatch')
        restore(p, state)
        print('ROLLBACK_COMPLETE')

if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        print('STOPPED_' + type(exc).__name__ + ': ' + str(exc), file=sys.stderr)
        sys.exit(1)
