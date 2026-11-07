"""Own the lifetime of fresh loopback fixtures and temporary SSH forwards; retain evidence."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time
import urllib.request

parser = argparse.ArgumentParser()
for name in ['workspace', 'foundation', 'website', 'php', 'ssh-host']:
    parser.add_argument('--'+name, required=True)
args = parser.parse_args()
os.umask(0o077)
workspace = Path(args.workspace).resolve()
assert Path(args.workspace).is_absolute() and not workspace.exists()
for port in (8101, 8120):
    with socket.socket() as probe: assert probe.connect_ex(('127.0.0.1', port)) != 0, 'Fixture port is occupied'
workspace.mkdir(mode=0o700, parents=True)
provider = workspace/'provider'
foundation = Path(args.foundation).resolve()
children = []
logs = []
try:
    with (workspace/'prepare.log').open('x', encoding='utf-8') as output:
        subprocess.run([sys.executable, str(foundation/'tools/product-identity-acceptance.py'), '--workspace', str(provider),
            '--php', args.php, '--prepare-only'], cwd=foundation, stdout=output, stderr=subprocess.STDOUT, check=True)
        subprocess.run([sys.executable, str(foundation/'tools/seed-core-product-interop.py'), '--workspace', str(provider),
            '--php', args.php], cwd=foundation, stdout=output, stderr=subprocess.STDOUT, check=True)
    env = os.environ | json.loads((provider/'private-environment.json').read_text())
    env['FOUNDATION_FIXTURE_FRAMEWORK_ROUTER'] = str(foundation/'vendor/laravel/framework/src/Illuminate/Foundation/resources/server.php')
    log = (workspace/'php.log').open('x', encoding='utf-8'); logs.append(log)
    php = subprocess.Popen([args.php, '-S', '127.0.0.1:8101', str(provider/'router.php')], cwd=foundation/'public', env=env, stdout=log, stderr=subprocess.STDOUT)
    children.append(php)
    manifest = json.loads((provider/'core-interop.json').read_text())
    log = (workspace/'relay.log').open('x', encoding='utf-8'); logs.append(log)
    node = subprocess.Popen([shutil.which('node'), str(Path(__file__).with_name('relay.mjs')), '--workspace', str(provider),
        '--website', str(Path(args.website).resolve()), '--backend', str(foundation), '--php', args.php,
        '--python', sys.executable], stdout=log, stderr=subprocess.STDOUT)
    children.append(node)
    for attempt in range(60):
        assert php.poll() is None and node.poll() is None, 'A loopback fixture stopped'
        try:
            request = urllib.request.Request('http://127.0.0.1:8120/health', headers={'X-Kuubu-Test-Fixture': manifest['fixture_marker']})
            with urllib.request.urlopen(request, timeout=2) as response:
                assert json.load(response)['fixture_marker'] == manifest['fixture_marker']
            break
        except OSError:
            time.sleep(.2)
    else:
        raise RuntimeError('Loopback relay did not become ready')
    log = (workspace/'tunnel.log').open('x', encoding='utf-8'); logs.append(log)
    tunnel = subprocess.Popen(['ssh', '-N', '-T', '-o', 'BatchMode=yes', '-o', 'ExitOnForwardFailure=yes',
        '-R', '127.0.0.1:28101:127.0.0.1:8101', '-R', '127.0.0.1:28120:127.0.0.1:8120', args.ssh_host],
        stdout=log, stderr=subprocess.STDOUT)
    children.append(tunnel)
    time.sleep(1)
    assert tunnel.poll() is None, 'SSH forwards did not start'
    (workspace/'ready.json').write_text(json.dumps({'scope': 'synthetic-provider-interop-only', 'fixture_marker': manifest['fixture_marker'],
        'pids': [child.pid for child in children], 'local_ports': [8101, 8120], 'remote_loopback_ports': [28101, 28120]}, indent=2))
    print('Fresh Laravel/Kuubu relay fixture and loopback-only SSH forwards are ready.', flush=True)
    while all(child.poll() is None for child in children): time.sleep(1)
    raise RuntimeError('A qualification child stopped; inspect retained logs')
except KeyboardInterrupt:
    print('Stopping owned qualification processes; retaining all evidence.', flush=True)
finally:
    for child in reversed(children):
        if child.poll() is None:
            child.terminate()
            try: child.wait(timeout=10)
            except subprocess.TimeoutExpired: child.kill(); child.wait(timeout=10)
    for log in logs: log.close()
    (workspace/'stopped.json').write_text(json.dumps({'all_owned_children_stopped': all(child.poll() is not None for child in children)}, indent=2))
