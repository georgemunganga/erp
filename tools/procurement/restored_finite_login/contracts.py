"""Thin pure LOGIN adapter over separately hash-pinned, reviewed StageB helpers."""
import copy
import hashlib
import pathlib
import re

ROOT = pathlib.Path('/home/amizo/erp-backups')
SWITCH = re.compile(r'\bSET\s+(?:(?:LOCAL|SESSION)\s+)?ROLE\b|\bSET\s+SESSION\s+AUTHORIZATION\b', re.I)

def need(ok, code):
    if not ok:
        raise RuntimeError(code)

def names(database):
    need(isinstance(database, str) and re.fullmatch(r'proc_p30_login_20261024_[0-9]{2}', database), 'Fresh bounded LOGIN database required')
    suffix = database[-2:]
    return ('p30_record_login_' + suffix, 'p30_execute_login_' + suffix)

def validate_output(path, inputs, root=ROOT):
    need(path.is_absolute() and path.parent == root and '..' not in path.parts
         and re.fullmatch(r'procurement-p30-finite-login-20261024_run[0-9]{2}', path.name), 'Private direct-child output required')
    current = pathlib.Path('/')
    for part in path.parent.parts[1:]:
        current /= part
        need(current.is_dir() and not current.is_symlink(), 'No symlinked private ancestor')
    need(path.parent.resolve() == path.parent and not path.exists() and not path.is_symlink(), 'Fresh output required')
    for item in inputs:
        item = item.resolve()
        need(path != item and path not in item.parents and item not in path.parents, 'Output/input overlap')

def login_proof(helper, matrix, catalog, which, alias):
    need(which in ('record', 'execution') and re.fullmatch(r'p30_' + ('record' if which == 'record' else 'execute') + r'_login_[0-9]{2}', alias), 'Exact role/proof tuple required')
    role = 'procurement_api' if which == 'record' else 'procurement_execution_api'
    original = helper.proof_sql(role, matrix, catalog, which)
    header = 'SET SESSION AUTHORIZATION ' + helper.ident(role) + ';\n'
    denial = "BEGIN EXECUTE 'SET ROLE erp'; RAISE EXCEPTION 'UnexpectedFinitePrivilegeSuccess' USING ERRCODE='55000'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;"
    old_identity = 'IF session_user<>' + helper.literal(role) + ' OR current_user<>' + helper.literal(role)
    need(original.count(header) == original.count(denial) == original.count(old_identity) == 1, 'Reviewed proof adaptation boundary changed')
    replacement = "IF pg_has_role(current_user,'erp','MEMBER') OR pg_has_role(current_user,'erp','SET') THEN RAISE EXCEPTION 'UnexpectedOwnerRoleCapability' USING ERRCODE='55000'; END IF;"
    text = original.replace(header, '', 1).replace(denial, replacement, 1).replace(old_identity,
        'IF session_user<>' + helper.literal(alias) + ' OR current_user<>' + helper.literal(alias), 1)
    need(not SWITCH.search(text) and text.startswith('BEGIN;\n') and text.endswith('ROLLBACK;\n'), 'Authenticated proof cannot switch identity')
    return text, {'baseSha256': hashlib.sha256(original.encode()).hexdigest(), 'adaptedSha256': hashlib.sha256(text.encode()).hexdigest(),
                  'removedExactSessionHeader': 1, 'replacedExactOwnerSwitchNegativeWithCapabilityDenial': 1, 'identityAssertionRebound': True}

def alias_targets(acl, matrix, catalog, database):
    record, execution = names(database)
    original = acl.expected_targets(matrix, catalog, 'procurement_api', 'procurement_execution_api_p30_01')
    replace = {'procurement_api': record, 'procurement_execution_api_p30_01': execution}
    return {kind: {tuple(replace.get(v, v) if i == acl.GRANTEE[kind] else v for i, v in enumerate(row)) for row in rows}
            for kind, rows in original.items()}

def verify_acl(acl, before, after, expected):
    b, a = acl.tuples(before), acl.tuples(after)
    aliases = {r[acl.GRANTEE[k]] for k, rows in expected.items() for r in rows}
    for kind in ('table', 'column', 'schema'):
        index = acl.GRANTEE[kind]
        need(not any(row[index] in aliases for row in b[kind]), 'New alias has existing ACL')
        need(a[kind] == b[kind] | expected[kind], 'Exact alias-only ACL union differs: ' + kind)
    need(a['default'] == b['default'], 'All owner/grantee default ACLs must remain exact')

def grant_sql(expected):
    def q(value):
        need(isinstance(value, str) and value and '\x00' not in value, 'Invalid identifier')
        return '"' + value.replace('"', '""') + '"'
    result = ['BEGIN;', "SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='90s';"]
    allowed = {'SELECT','INSERT','UPDATE','DELETE','REFERENCES','TRIGGER','TRUNCATE','USAGE'}
    for kind in ('table', 'column', 'schema'):
        for row in sorted(expected[kind]):
            need(row[-1] is False and row[-2] in allowed, 'No grant options/unknown privileges')
            if kind == 'table':
                s, t, grantee, _, p, _ = row
                result.append(f'GRANT {p} ON TABLE {q(s)}.{q(t)} TO {q(grantee)};')
            elif kind == 'column':
                s, t, c, grantee, _, p, _ = row
                result.append(f'GRANT {p} ({q(c)}) ON TABLE {q(s)}.{q(t)} TO {q(grantee)};')
            else:
                s, grantee, _, p, _ = row
                result.append(f'GRANT {p} ON SCHEMA {q(s)} TO {q(grantee)};')
    # Run as the pinned private bootstrap superuser; PostgreSQL records object owner
    # as grantor. Exact expanded ACL comparison verifies this rather than assuming it.
    result.append('COMMIT;')
    return '\n'.join(result) + '\n'

def structural(raw):
    value = copy.deepcopy(raw)
    for row in value['tables']: row[3] = None
    for row in value['columns']: row[8] = None
    for row in value['schemas']: row[2] = None
    # Defaults are deliberately retained here as well as in exact ACL comparison.
    return value

def first_local_scram(rows, database, roles):
    """Conservative simple-HBA admission; never interprets regex/group/include selectors."""
    def match(values, value):
        need(isinstance(values, list) and values and all(isinstance(x, str) and (x == 'all' or re.fullmatch(r'[a-z][a-z0-9_]*', x)) for x in values), 'Unsupported HBA selector requires review')
        return 'all' in values or value in values
    need(isinstance(rows, list) and rows, 'Reviewed HBA rules missing')
    selected = []
    for role in roles:
        found = None
        for row in sorted(rows, key=lambda x: x['ruleNumber']):
            need(row['error'] is None, 'Invalid HBA rule')
            if row['type'] != 'local': continue
            if match(row['database'], database) and match(row['users'], role):
                found = row; break
        need(found is not None and found['authMethod'] == 'scram-sha-256', 'First matching local rule must require SCRAM')
        selected.append(found['ruleNumber'])
    return selected
