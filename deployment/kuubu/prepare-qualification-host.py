"""Initial private configuration only; never starts services or applies schema.
Run as root after the uniquely tagged image passes its tests and PDF smoke.
"""
from pathlib import Path
import json
import os
import secrets
import shutil
import subprocess

assert os.geteuid() == 0
os.umask(0o077)
root = Path('/opt/kuubu-qualification')
assert not root.exists(), 'Inspect the existing qualification directory before any update'
image = 'kuubu/erp-api:408da398-build01'
revision = '408da398210c6e5e220077f4000ecdfc6cedb0aa'
metadata = json.loads(subprocess.check_output(['docker', 'image', 'inspect', image]))[0]
assert metadata['Config']['Labels']['org.opencontainers.image.revision'] == revision
app_uid = int(subprocess.check_output(['docker', 'run', '--rm', '--entrypoint', 'id', image, '-u']))
assert app_uid != 0
for kind, name in [('volume', 'kuubu-qualification_pgdata'), ('network', 'kuubu-qualification_database')]:
    result = subprocess.run(['docker', kind, 'inspect', name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    assert result.returncode != 0, 'Existing qualification resources require inspection'
subprocess.run(['docker', 'pull', 'postgres:17'], check=True, stdout=subprocess.DEVNULL)
postgres = json.loads(subprocess.check_output(['docker', 'image', 'inspect', 'postgres:17']))[0]
pg_uid = int(subprocess.check_output(['docker', 'run', '--rm', '--entrypoint', 'id', 'postgres:17', '-u', 'postgres']))
root.mkdir(mode=0o700)
private = root/'private'
private.mkdir(mode=0o700)
shutil.copyfile(Path(__file__).with_name('compose.qualification.yml'), root/'compose.qualification.yml')
bootstrap, owner, runtime = (secrets.token_hex(32) for _ in range(3))
password = private/'postgres-bootstrap.password'
password.write_text(bootstrap+'\n')
os.chown(password, pg_uid, -1)
password.chmod(0o400)
common = '''ASPNETCORE_ENVIRONMENT=Production
ERP__AuthMode=local
ERP__Database__MigrateOnStartup=false
HRM__DefaultTenantId=kuubu-qualification
HRM__AllowedOrigins=https://www.kuubuerp.com
HRM__LocalAdminEmail=
HRM__LocalAdminPassword=
HRM__IdentityBaseUrl=
HRM__NotificationFallback=off
ConnectionStrings__Procurement=
'''
for filename, username, value in [('owner.env', 'kuubu_schema_owner', owner), ('runtime.env', 'kuubu_core_runtime', runtime)]:
    (private/filename).write_text(common+'ConnectionStrings__Hrm=Host=database;Port=5432;Database=kuubu;Username='+username+';Password='+value+'\n')
# This private SQL contains generated credentials. Feed it through privileged psql
# stdin after database initialization; never print it or place it in a repository.
(private/'bootstrap-roles.sql').write_text(
    "CREATE ROLE kuubu_schema_owner LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '"+owner+"';\n"
    "CREATE ROLE kuubu_core_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD '"+runtime+"';\n"
    "ALTER DATABASE kuubu OWNER TO kuubu_schema_owner;\n"
    "REVOKE ALL ON DATABASE kuubu FROM PUBLIC;\n"
    "GRANT CONNECT ON DATABASE kuubu TO kuubu_core_runtime;\n"
    "REVOKE CREATE ON SCHEMA public FROM PUBLIC;\n")
(root/'project.env').write_text('KUUBU_ERP_IMAGE='+metadata['Id']+'\nKUUBU_POSTGRES_IMAGE='+postgres['RepoDigests'][0]+'\n')
subprocess.run(['docker', 'compose', '--env-file', str(root/'project.env'), '-f', str(root/'compose.qualification.yml'), 'config', '--quiet'], check=True)
(root/'prepared.json').write_text(json.dumps({'application_source': revision, 'api_image': metadata['Id'], 'postgres_image': postgres['RepoDigests'][0], 'api_uid': app_uid, 'postgres_uid': pg_uid, 'services_started': False}, indent=2)+'\n')
print('Private qualification configuration prepared with pinned images. No services or migrations started. No identities seeded.')
