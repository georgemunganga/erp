"""Pure restore equivalence projections; no I/O, SQL, roles or execution on import."""
import json
import re


def require(ok, message):
    if not ok:
        raise RuntimeError(message)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'))


def database_acl(items):
    if items is None:
        return None  # NULL is not permission-equivalent to captured explicit grants.
    require(isinstance(items, list), 'Database ACL must be an array or null.')
    result = set()
    for item in items:
        # The pinned actual database ACL uses these existing unquoted role names.
        # Unknown ACL identifier syntax requires a new reviewed metadata capture.
        match = re.fullmatch(r'([A-Za-z0-9_]*)=([CTc*]*)/([A-Za-z0-9_]+)', item) if isinstance(item, str) else None
        require(match is not None, 'Unsupported database ACL representation.')
        grantee, privileges, grantor = match.groups()
        require(not privileges.startswith('*'), 'Invalid database ACL grant option.')
        tokens = re.findall(r'[CTc]\*?', privileges)
        require(''.join(tokens) == privileges, 'Invalid database ACL token sequence.')
        for token in tokens:
            result.add((grantee, grantor, token[0], token.endswith('*')))
    return sorted(result)


def ordered_records(items):
    require(isinstance(items, list) and all(isinstance(x, dict) for x in items), 'Expected complete metadata records.')
    return sorted(items, key=canonical)


def database_projection(record, owner):
    require(isinstance(record, dict) and isinstance(record.get('database'), dict), 'Database properties missing.')
    require(isinstance(owner, str) and owner, 'Resolved database owner required.')
    db = record['database']
    # Name and physical catalog/XID/OID counters are deliberately not semantic
    # restore identity. Preserve all captured owner/permission/locale properties.
    fields = ('encoding', 'datcollate', 'datctype', 'datlocprovider', 'datlocale', 'daticurules',
              'datcollversion', 'datallowconn', 'datconnlimit', 'datistemplate', 'dathasloginevt')
    require(all(key in db for key in fields) and 'datacl' in db and 'settings' in record,
            'Complete database semantic properties required.')
    return {'owner': owner, 'properties': {key: db[key] for key in fields},
            'acl': database_acl(db['datacl']), 'settings': ordered_records(record['settings'])}


def verify_database(expected, expected_owner, actual):
    require(isinstance(actual, dict) and isinstance(actual.get('owner'), str), 'Actual resolved owner missing.')
    left = database_projection(expected, expected_owner)
    right = database_projection(actual, actual['owner'])
    require(left == right, 'Fresh restore database owner/ACL/locale/provider/settings mismatch.')
    return right


def original_privilege_projection(boundary, expected_tables, expected_columns):
    require(isinstance(boundary, dict), 'Original privilege boundary missing.')
    fields = ('schemas', 'originalObjects', 'originalColumnAcls', 'defaultAcls', 'memberships', 'roles')
    require(all(key in boundary for key in fields), 'Complete original privilege fields required.')
    require(len(boundary['originalObjects']) == expected_tables and len(boundary['originalColumnAcls']) == expected_columns,
            'Exact original object/column inventory required.')
    require(all(x.get('owner') for x in boundary['originalObjects']) and all(x.get('column') for x in boundary['originalColumnAcls']),
            'Original object or column missing after restore.')
    # Only these two intentionally bounded private placeholder roles differ in
    # login/elevation flags. Keep every other role property and membership exact.
    private_flags = {'login', 'superuser', 'createDb', 'createRole', 'replication', 'bypassRls'}
    roles = []
    for row in boundary['roles']:
        require(isinstance(row, dict) and isinstance(row.get('name'), str), 'Role metadata missing.')
        roles.append({key: value for key, value in row.items()
                      if not (row['name'] in ('erp', 'procurement_api') and key in private_flags)})
    require({'erp', 'procurement_api'} <= {x['name'] for x in roles}, 'Both expected owner/runtime roles required.')
    return {**{key: ordered_records(boundary[key]) for key in fields if key != 'roles'},
            'roles': ordered_records(roles)}


def verify_restored_privileges(production, restored, expected_tables, expected_columns):
    left = original_privilege_projection(production, expected_tables, expected_columns)
    right = original_privilege_projection(restored, expected_tables, expected_columns)
    require(left == right, 'Restored original schema/object/column/default ACL/role membership differs from production.')
    return right
