#!/usr/bin/env python3
"""Fill live HRM profile details from the preserved Frappe employee archive.

Existing HRM values win. Duplicate NRCs stay in the archive for HR review.
Requires psycopg from the private migration venv used for the original import.
"""

import argparse
from collections import Counter
import json
import re
import subprocess

import psycopg


def value(source, key):
    result = source.get(key)
    return str(result).strip() if result is not None and str(result).strip() else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    env = dict(x.split('=', 1) for x in json.loads(subprocess.check_output(
        ['docker', 'inspect', 'c9075454e328_erp-hrm-postgres', '--format', '{{json .Config.Env}}'], text=True
    )) if '=' in x)
    with psycopg.connect(host='127.0.0.1', port=15432, dbname=env['POSTGRES_DB'],
                         user=env['POSTGRES_USER'], password=env['POSTGRES_PASSWORD']) as db:
        with db.cursor() as cursor:
            cursor.execute('''SELECT w.id, w.nrc, w.profile_details_json, r.payload
                FROM hrm.workers w JOIN hrm.legacy_frappe_records r ON r.worker_id=w.id
                WHERE r.source_type='Employee' AND w.tenant_id='amipower' ORDER BY w.employee_no''')
            rows = cursor.fetchall()
            if len(rows) != 122:
                raise RuntimeError('Expected 122 archived employee profiles')
            ids = Counter(value(payload, 'custom_id_number') for _, _, _, payload in rows
                          if value(payload, 'custom_id_number'))
            updated = nrc_filled = duplicate_nrc = 0
            for worker_id, current_nrc, details_json, source in rows:
                details = json.loads(details_json) if details_json else {}
                if not isinstance(details, dict):
                    raise RuntimeError('Existing profile details are not a JSON object')
                mapping = {
                    'salutation': value(source, 'salutation'),
                    'gender': value(source, 'gender'),
                    'maritalStatus': value(source, 'marital_status'),
                    'residentialAddress': value(source, 'current_address'),
                    'postalAddress': value(source, 'permanent_address'),
                    'bloodGroup': value(source, 'blood_group'),
                    'passportExpiry': value(source, 'valid_upto') if value(source, 'passport_number') else None,
                    'legacyEmploymentType': value(source, 'employment_type'),
                    'legalEntityName': value(source, 'company'),
                    'probationEndsOn': value(source, 'scheduled_confirmation_date'),
                    'confirmedOn': value(source, 'final_confirmation_date'),
                    'attendanceDeviceId': value(source, 'attendance_device_id'),
                    'holidayCalendar': value(source, 'holiday_list'),
                    'shiftPattern': value(source, 'default_shift'),
                    'costCentre': value(source, 'payroll_cost_center'),
                    'noticePeriodDays': source.get('notice_number_of_days') or None,
                }
                for key, field_value in mapping.items():
                    if field_value is not None and not details.get(key):
                        details[key] = field_value
                source_nrc = value(source, 'custom_id_number')
                nrc = current_nrc
                if source_nrc and re.fullmatch(r'\d{6}/\d{2}/\d', source_nrc):
                    if ids[source_nrc] > 1:
                        duplicate_nrc += 1
                    elif not current_nrc:
                        nrc = source_nrc
                        nrc_filled += 1
                new_details = json.dumps(details, ensure_ascii=False, separators=(',', ':'))
                if nrc != current_nrc or new_details != (details_json or ''):
                    updated += 1
                    if args.apply:
                        cursor.execute('''UPDATE hrm.workers
                            SET nrc=%s, profile_details_json=%s, updated_at=now(), updated_by=%s
                            WHERE id=%s''', (nrc, new_details, 'frappe-profile-backfill-20260925', worker_id))
            print(json.dumps({'profiles_checked': len(rows), 'profiles_to_update': updated,
                              'unique_nrc_filled': nrc_filled, 'duplicate_nrc_profiles_held': duplicate_nrc,
                              'applied': args.apply}))
            if not args.apply:
                db.rollback()


if __name__ == '__main__':
    main()
