#!/usr/bin/env python3
"""One-time, repeat-safe Frappe personnel import for the local Amizo HRM.

Run with --dry-run first. --apply inserts one atomic PostgreSQL batch after a
database backup has been taken. Frappe is only read. Credentials are loaded
from the existing site config and Docker environment, never command arguments.
"""

import argparse
import base64
import collections
import datetime as dt
import hashlib
import json
import mimetypes
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import uuid

import pymysql
import psycopg
from psycopg.types.json import Jsonb


SITE = Path('/home/amizo/frappe-bench/sites/admin.amizopower.co.zm')
DOCUMENTS = Path(os.environ.get('FRAPPE_IMPORT_DOCUMENTS', '/home/amizo/hrm-documents'))
CONTAINER_DOCUMENTS = Path('/tmp/erp-docs')
ACTOR = 'frappe-import-20260923'
NAMESPACE = uuid.UUID('f4d7df72-626f-482a-8fcb-0e58cf1f7061')


def uid(kind, key):
    return uuid.uuid5(NAMESPACE, f'admin.amizopower.co.zm:{kind}:{key}')


def clean(value):
    if value is None:
        return None
    value = str(value).strip()
    return value or None


def iso(value):
    return value.isoformat() if value else None


def pg_connection():
    env = dict(item.split('=', 1) for item in json.loads(subprocess.check_output(
        ['docker', 'inspect', 'c9075454e328_erp-hrm-postgres', '--format', '{{json .Config.Env}}'], text=True
    )) if '=' in item)
    return psycopg.connect(host='127.0.0.1', port=15432,
                           dbname=os.environ.get('FRAPPE_IMPORT_PG_DATABASE', env['POSTGRES_DB']),
                           user=env['POSTGRES_USER'], password=env['POSTGRES_PASSWORD'])


def source_connection():
    config = json.loads((SITE / 'site_config.json').read_text())
    return pymysql.connect(host='localhost', user=config['db_name'], password=config['db_password'],
                           database=config['db_name'], cursorclass=pymysql.cursors.DictCursor,
                           charset='utf8mb4')


def rows(source, table):
    with source.cursor() as cursor:
        cursor.execute(f'SELECT * FROM `{table}`')
        return cursor.fetchall()


def user_snapshot(user):
    # Credentials, reset tokens, API keys, IP restrictions and session data
    # cannot be transferred to a different authentication system.
    excluded = {'new_password', 'reset_password_key', 'api_key', 'api_secret',
                'last_ip', 'restrict_ip', 'last_known_versions', 'home_settings'}
    return {key: value for key, value in user.items() if key not in excluded}


def unusable_password_hash():
    # Same PBKDF2 encoding as LocalPasswordHash.Hash, with an undisclosed random
    # password. Accounts remain disabled until HR assigns roles and resets them.
    salt = secrets.token_bytes(16)
    key = hashlib.pbkdf2_hmac('sha256', secrets.token_bytes(32), salt, 210_000, 32)
    return f'pbkdf2-sha256$210000${base64.b64encode(salt).decode()}${base64.b64encode(key).decode()}'


def document_source(row):
    url = clean(row.get('file_url'))
    if not url or not url.startswith(('/private/files/', '/files/')):
        raise ValueError('Employee attachment has no local Frappe file URL')
    segment = 'private/files' if url.startswith('/private/files/') else 'public/files'
    base = (SITE / segment).resolve()
    path = (base / url.rsplit('/', 1)[-1]).resolve()
    if not path.is_relative_to(base) or not path.is_file():
        raise ValueError('Employee attachment is missing or escapes the Frappe files directory')
    return path


def insert_entity(cursor, table, values):
    columns = ', '.join(values)
    placeholders = ', '.join(['%s'] * len(values))
    cursor.execute(f'INSERT INTO hrm.{table} ({columns}) VALUES ({placeholders})', tuple(values.values()))


def main():
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--dry-run', action='store_true')
    mode.add_argument('--apply', action='store_true')
    args = parser.parse_args()

    with source_connection() as source:
        employees = rows(source, 'tabEmployee')
        users = rows(source, 'tabUser')
        files = [f for f in rows(source, 'tabFile') if f['attached_to_doctype'] == 'Employee']
        children = {kind: rows(source, table) for kind, table in {
            'education': 'tabEmployee Education',
            'external_work_history': 'tabEmployee External Work History',
            'internal_work_history': 'tabEmployee Internal Work History',
            'checkin': 'tabEmployee Checkin',
            'promotion': 'tabEmployee Promotion',
        }.items()}

    if len(employees) != 122 or len({e['name'] for e in employees}) != len(employees):
        raise RuntimeError('Source employee count or unique employee numbers changed; re-audit before import')
    source_ids = {e['name'] for e in employees}
    if any(e.get('reports_to') and e['reports_to'] not in source_ids for e in employees):
        raise RuntimeError('Unresolved Frappe manager reference')
    if any(f.get('attached_to_name') not in source_ids for f in files):
        raise RuntimeError('Unresolved employee attachment reference')

    work_email_counts = collections.Counter(clean(e.get('company_email')).lower() for e in employees
                                            if clean(e.get('company_email')))
    phone_counts = collections.Counter(clean(e.get('cell_number')) for e in employees
                                       if clean(e.get('cell_number')))
    paths = {f['name']: document_source(f) for f in files}
    departments = sorted({clean(e.get('department')) for e in employees if clean(e.get('department'))})
    branches = sorted({clean(e.get('branch')) for e in employees if clean(e.get('branch'))})
    summary = {
        'employees': len(employees), 'active': sum(e['status'] == 'Active' for e in employees),
        'leavers': sum(e['status'] == 'Left' for e in employees), 'users_metadata': len(users),
        'disabled_user_accounts': sum(u['name'] not in ('Administrator', 'Guest') for u in users),
        'departments': len(departments), 'branches': len(branches),
        'files': len(files), 'file_bytes': sum(p.stat().st_size for p in paths.values()),
        'history': {kind: len(items) for kind, items in children.items()},
        'single_name_profiles': sum(not clean(e.get('last_name')) for e in employees),
        'shared_work_email_profiles': sum(bool(clean(e.get('company_email'))) and
                                          work_email_counts[clean(e.get('company_email')).lower()] > 1
                                          for e in employees),
        'shared_phone_profiles': sum(bool(clean(e.get('cell_number'))) and
                                     phone_counts[clean(e.get('cell_number'))] > 1 for e in employees),
    }
    print(json.dumps(summary, indent=2))
    if args.dry_run:
        return

    DOCUMENTS.mkdir(mode=0o700, parents=True, exist_ok=True)
    os.chmod(DOCUMENTS, 0o700)
    copied = []
    now = dt.datetime.now(dt.timezone.utc)
    try:
        with pg_connection() as target:
            with target.cursor() as cursor:
                cursor.execute('SELECT DISTINCT tenant_id FROM hrm.local_users')
                tenants = [r[0] for r in cursor.fetchall()]
                if tenants != ['amipower']:
                    raise RuntimeError('Unexpected HRM tenant; refusing to import')
                cursor.execute('SELECT count(*) FROM hrm.workers')
                existing = cursor.fetchone()[0]
                if existing:
                    raise RuntimeError(f'HRM already has {existing} workers; refusing a second import')
                cursor.execute('SELECT count(*) FROM hrm.legal_entities')
                if cursor.fetchone()[0]:
                    raise RuntimeError('HRM already has legal entities; reconcile organization first')
                tenant = tenants[0]
                common = dict(tenant_id=tenant, created_at=now, created_by=ACTOR, is_archived=False)
                legal_id = uid('legal_entity', 'Amizo Power')
                insert_entity(cursor, 'legal_entities', dict(id=legal_id, code='AP',
                    registered_name='Amizo Power', trading_name='Amizo Power', currency='ZMW',
                    country_code='ZM', is_default=True, **common))
                dept_ids = {name: uid('department', name) for name in departments}
                for name, dept_id in dept_ids.items():
                    insert_entity(cursor, 'org_units', dict(id=dept_id, code='FRAPPE-' + name.upper().replace(' ', '-')[:60],
                        name=name, legal_entity_id=legal_id, unit_type='department',
                        effective_from=dt.date(2000, 1, 1), status='active', **common))
                branch_ids = {name: uid('branch', name) for name in branches}
                for name, branch_id in branch_ids.items():
                    insert_entity(cursor, 'work_locations', dict(id=branch_id,
                        code='FRAPPE-' + name.upper().replace(' ', '-')[:60], name=name,
                        legal_entity_id=legal_id, type='branch', **common))

                # The source archive keeps every Frappe profile field and all
                # imported child metadata; app profile columns only get verified
                # mappings. No Frappe credential or login session is copied.
                cursor.execute('''CREATE TABLE IF NOT EXISTS hrm.legacy_frappe_records (
                    tenant_id text NOT NULL, source_type text NOT NULL, source_key text NOT NULL,
                    worker_id uuid NULL REFERENCES hrm.workers(id), payload jsonb NOT NULL,
                    imported_at timestamptz NOT NULL,
                    PRIMARY KEY (tenant_id, source_type, source_key))''')
                def archive(kind, key, payload, worker_id=None):
                    cursor.execute('''INSERT INTO hrm.legacy_frappe_records
                        (tenant_id, source_type, source_key, worker_id, payload, imported_at)
                        VALUES (%s, %s, %s, %s, %s, %s)''',
                        (tenant, kind, key, worker_id, Jsonb(payload, dumps=lambda v: json.dumps(v, default=str)), now))

                for e in employees:
                    work_email = clean(e.get('company_email'))
                    phone = clean(e.get('cell_number'))
                    if work_email and work_email_counts[work_email.lower()] > 1:
                        work_email = None
                    if phone and phone_counts[phone] > 1:
                        phone = None
                    worker_id = uid('employee', e['name'])
                    is_left = e['status'] == 'Left'
                    insert_entity(cursor, 'workers', dict(id=worker_id, employee_no=e['name'],
                        first_name=clean(e.get('first_name')) or clean(e.get('employee_name')) or e['name'],
                        middle_name=clean(e.get('middle_name')), last_name=clean(e.get('last_name')) or '',
                        email=work_email, personal_email=clean(e.get('personal_email')),
                        phone=phone, passport_no=clean(e.get('passport_number')),
                        date_of_birth=iso(e.get('date_of_birth')), worker_type='employee',
                        status='terminated' if is_left else 'active',
                        org_unit_id=dept_ids.get(clean(e.get('department'))),
                        location_id=branch_ids.get(clean(e.get('branch'))),
                        job_title=clean(e.get('designation')), start_date=e.get('date_of_joining'),
                        end_date=e.get('relieving_date') if is_left else None,
                        **(common | {'is_archived': is_left})))
                    archive('Employee', e['name'], e, worker_id)
                    contact = clean(e.get('person_to_be_contacted'))
                    if contact:
                        insert_entity(cursor, 'emergency_contacts', dict(id=uid('emergency', e['name']),
                            worker_id=worker_id, relationship=clean(e.get('relation')) or 'Unspecified',
                            full_name=contact, phone=clean(e.get('emergency_phone_number')),
                            is_primary=True, **common))

                for e in employees:
                    if clean(e.get('reports_to')):
                        cursor.execute('UPDATE hrm.workers SET manager_id=%s WHERE id=%s',
                                       (uid('employee', e['reports_to']), uid('employee', e['name'])))

                for kind, items in children.items():
                    for item in items:
                        parent = item.get('parent') or item.get('employee')
                        worker_id = uid('employee', parent) if parent in source_ids else None
                        archive(kind, item['name'], item, worker_id)
                        if not worker_id:
                            continue
                        if kind == 'education' and clean(item.get('school_univ')) and clean(item.get('qualification')):
                            insert_entity(cursor, 'education', dict(id=uid(kind, item['name']), worker_id=worker_id,
                                institution=clean(item['school_univ']), qualification=clean(item['qualification']),
                                field_of_study=clean(item.get('maj_opt_subj')), grade=clean(item.get('class_per')),
                                end_year=item.get('year_of_passing') or None, **common))
                        elif kind == 'external_work_history' and clean(item.get('company_name')):
                            insert_entity(cursor, 'external_work_history', dict(id=uid(kind, item['name']),
                                worker_id=worker_id, company=clean(item['company_name']),
                                role=clean(item.get('designation')), **common))
                        elif kind == 'internal_work_history' and clean(item.get('department')):
                            insert_entity(cursor, 'internal_work_history', dict(id=uid(kind, item['name']),
                                worker_id=worker_id, org_unit_name=clean(item['department']),
                                role=clean(item.get('designation')), start_date=iso(item.get('from_date')),
                                end_date=iso(item.get('to_date')), **common))

                for f in files:
                    source_path = paths[f['name']]
                    suffix = source_path.suffix.lower() or '.bin'
                    dest_name = f'{uid("file", f["name"])}{suffix}'
                    destination = DOCUMENTS / dest_name
                    if destination.exists():
                        raise RuntimeError('Destination employee document already exists')
                    shutil.copyfile(source_path, destination)
                    os.chmod(destination, 0o600)
                    copied.append(destination)
                    worker_id = uid('employee', f['attached_to_name'])
                    archive('File', f['name'], f, worker_id)
                    insert_entity(cursor, 'worker_documents', dict(id=uid('file', f['name']),
                        worker_id=worker_id, category='id' if 'nrc' in f['file_name'].lower() else 'evidence',
                        title=clean(f.get('file_name')) or 'Frappe employee document',
                        file_name=clean(f.get('file_name')) or dest_name,
                        content_type=mimetypes.guess_type(source_path.name)[0] or 'application/octet-stream',
                        size_bytes=destination.stat().st_size,
                        storage_path=str(CONTAINER_DOCUMENTS / dest_name),
                        classification='restricted', is_latest=True, **common))
                for user in users:
                    linked = next((e['name'] for e in employees if e.get('user_id') == user['name']), None)
                    archive('User', user['name'], user_snapshot(user), uid('employee', linked) if linked else None)
                    if user['name'] in ('Administrator', 'Guest'):
                        continue
                    email = clean(user.get('email')) or clean(user.get('name'))
                    if not email or '@' not in email:
                        raise RuntimeError('Frappe human user has no usable email')
                    insert_entity(cursor, 'local_users', dict(id=uid('user', user['name']),
                        email=email, normalized_email=email.upper(),
                        display_name=clean(user.get('full_name')) or email,
                        password_hash=unusable_password_hash(), roles_csv='',
                        worker_id=uid('employee', linked) if linked else None,
                        is_active=False, must_change_password=True, failed_login_count=0,
                        **common))
        print('APPLIED: one committed HRM transaction; imported user accounts are disabled with no roles')
    except Exception:
        for path in copied:
            path.unlink(missing_ok=True)
        raise


if __name__ == '__main__':
    main()
