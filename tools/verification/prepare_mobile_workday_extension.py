"""Generate a complete, exclusive compatibility extension from the owning SQL guard."""
from pathlib import Path
import argparse,re,hashlib,json
p=argparse.ArgumentParser();p.add_argument('--output',required=True,type=Path);a=p.parse_args()
root=Path(__file__).resolve().parents[2]
source=(root/'backend/hrm-api/src/Mightyfin.Erp.Hrm.Infrastructure/Data/MobileWorkdaySchema.cs').read_text()
match=re.search(r'public const string GuardSql="""\n(.*?)\n        """;',source,re.S);assert match
guard='\n'.join(line[8:] if line.startswith('        ') else line for line in match[1].splitlines())+'\n'
prefix=(Path(__file__).with_name('mobile-workday-schema-v1.sql.in')).read_text()
assert prefix.count('BEGIN;')==1 and 'COMMIT;' not in prefix and 'DROP TABLE' not in prefix
receipt="INSERT INTO hrm.__mobile_workday_schema(version,profile) VALUES(1,'manual-workday-v1') ON CONFLICT(version) DO NOTHING;\nCOMMIT;\n"
sql=prefix+guard+receipt
with a.output.open('x',encoding='utf8',newline='\n') as f:f.write(sql)
print(json.dumps({'output':str(a.output.resolve()),'sql_sha256':hashlib.sha256(sql.encode()).hexdigest(),'canonical_guard_sha256':hashlib.sha256(guard.encode()).hexdigest()}))
