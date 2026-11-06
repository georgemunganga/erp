"""Root-private backup and real restore to a new database; retains both artifacts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import os
import subprocess
import uuid
import urllib.request
import urllib.error

assert os.geteuid() == 0
os.umask(0o077)
root = Path('/opt/kuubu-qualification')
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'_'+uuid.uuid4().hex[:8]
target = root/'receipts'/('recovery_'+stamp)
target.mkdir(mode=0o700)
restore = 'kuubu_restore_'+stamp.lower()
compose = ['docker', 'compose', '--env-file', str(root/'project.env'), '-f', str(root/'compose.qualification.yml'), 'exec', '-T', 'database']

def sql(database, statement):
    return subprocess.check_output(compose+['psql', '-X', '-A', '-t', '-v', 'ON_ERROR_STOP=1', '-U',
        'kuubu_bootstrap_admin', '-d', database, '-c', statement], text=True).strip()

def snapshot(database):
    tables = json.loads(sql(database, "SELECT json_agg(tablename ORDER BY tablename) FROM pg_tables WHERE schemaname='hrm'"))
    rows = {}
    for table in tables:
        quoted = '"'+table.replace('"', '""')+'"'
        rows[table] = sql(database, 'SELECT count(*)::text || chr(58) || md5(coalesce(string_agg(row_to_json(t)::text, chr(10) ORDER BY row_to_json(t)::text), '+"''"+')) FROM hrm.'+quoted+' t')
    return {'rows': rows, 'table_owners_acls': sql(database, "SELECT json_agg(x ORDER BY x.relname) FROM (SELECT c.relname, pg_get_userbyid(c.relowner) AS owner, c.relacl::text AS acl FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='hrm' AND c.relkind IN ('r','p')) x"),
        'column_acls': sql(database, "SELECT json_agg(x ORDER BY x.relname,x.attname) FROM (SELECT c.relname,a.attname,a.attacl::text AS acl FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='hrm' AND a.attacl IS NOT NULL) x")}

before = snapshot('kuubu')
assert before['rows']['local_users'].startswith('0:') and before['rows']['local_sessions'].startswith('0:')
dump = target/'core-hrm.dump'
with dump.open('xb') as output, (target/'dump.log').open('xb') as errors:
    subprocess.run(compose+['pg_dump', '-U', 'kuubu_bootstrap_admin', '-d', 'kuubu', '-Fc'], stdout=output, stderr=errors, check=True)
subprocess.run(compose+['createdb', '-U', 'kuubu_bootstrap_admin', '-O', 'kuubu_schema_owner', '-T', 'template0', restore], check=True)
sql(restore, 'REVOKE ALL ON DATABASE "'+restore+'" FROM PUBLIC; REVOKE ALL ON DATABASE "'+restore+'" FROM kuubu_core_runtime;')
with dump.open('rb') as data, (target/'restore.log').open('xb') as output:
    subprocess.run(compose+['pg_restore', '-U', 'kuubu_bootstrap_admin', '-d', restore, '--exit-on-error'], stdin=data, stdout=output, stderr=subprocess.STDOUT, check=True)
after = snapshot(restore)
assert before == after, 'Restored records, table owners/ACLs or column ACLs differ'
http = {}
for path, expected in [('/health/ready', 200), ('/api/v1/hrm/auth/me', 200), ('/api/v1/hrm/auth/users', 401)]:
    try:
        with urllib.request.urlopen('http://127.0.0.1:28911'+path) as response:
            status, body = response.status, response.read().decode()
    except urllib.error.HTTPError as error:
        status, body = error.code, error.read().decode()
    assert status == expected, path+' returned '+str(status)
    if path.endswith('/me'):
        assert json.loads(body) == {'authenticated': False, 'user': None}
    http[path] = status
receipt = {'restore_database': restore, 'dump_sha256': hashlib.sha256(dump.read_bytes()).hexdigest(),
    'core_hrm_table_count': len(before['rows']), 'all_table_rows_and_owners_acls_match': True,
    'login_users': 0, 'sessions': 0, 'http': http, 'off_host_recovery': False,
    'scope': 'Isolated qualification database; not tenant or business acceptance'}
(target/'receipt.json').write_text(json.dumps(receipt, indent=2)+'\n')
(target/'snapshot.json').write_text(json.dumps(before, indent=2)+'\n')
print(json.dumps({'receipt': str(target/'receipt.json'), **receipt}))
