"""One-time isolated database and genuine Core/HRM migrations; never starts API."""
from pathlib import Path
import json
import os
import subprocess

assert os.geteuid() == 0
os.umask(0o077)
root = Path('/opt/kuubu-qualification')
prepared = json.loads((root/'prepared.json').read_text())
assert prepared['services_started'] is False
receipts = root/'receipts'
receipts.mkdir(mode=0o700, exist_ok=True)
# A partial attempt is evidence to inspect, not permission to rerun initialization.
with (receipts/'initialization-started.json').open('x') as output:
    json.dump({'application_source': prepared['application_source']}, output)
compose = ['docker', 'compose', '--env-file', str(root/'project.env'), '-f', str(root/'compose.qualification.yml')]

def run(name, command, data=None):
    with (receipts/(name+'.log')).open('x') as output:
        result = subprocess.run(command, input=data, text=True, stdout=output, stderr=subprocess.STDOUT)
    assert result.returncode == 0, name+' failed; inspect the retained root-private log before recovery'

run('database-start', compose+['up', '-d', '--wait', '--wait-timeout', '45', 'database'])
run('bootstrap-roles', compose+['exec', '-T', 'database', 'psql', '-X', '-v', 'ON_ERROR_STOP=1',
    '-U', 'kuubu_bootstrap_admin', '-d', 'kuubu', '--single-transaction'], (root/'private/bootstrap-roles.sql').read_text())
run('core-hrm-migrations', compose+['run', '--no-deps', '--name', 'kuubu-qualification-migrate-01', 'migrate'])
run('schema-history', compose+['exec', '-T', 'database', 'psql', '-X', '-v', 'ON_ERROR_STOP=1',
    '-U', 'kuubu_bootstrap_admin', '-d', 'kuubu', '-c',
    'SELECT "MigrationId" FROM hrm.__hrm_migrations ORDER BY "MigrationId";'])
with (receipts/'initialization-completed.json').open('x') as output:
    json.dump({'application_source': prepared['application_source'], 'genuine_core_hrm_migrations': True,
        'api_started': False, 'public_ingress': False, 'customer_identity_seeded': False}, output, indent=2)
print('Isolated database initialized and genuine Core/HRM migrations applied. API remains stopped.')
