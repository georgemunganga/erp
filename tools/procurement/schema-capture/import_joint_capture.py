#!/usr/bin/env python3
"""Offline importer. No database calls, feature configuration or source mutation."""
import argparse,hashlib,json,os,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
API=Path('backend/hrm-api/src/Mightyfin.Erp.Hrm.Api')
CLASSES={'p36':'ProcurementRfqRecommendationSchemaCapture.cs','p37':'ProcurementPrincipalGoodsReturnSchemaCapture.cs'}
MIGRATIONS={'p36':'20261025014631_P36HumanRecommendationDraft','p37':'20261025020252_P37PrincipalGoodsReturnDrafts'}
HEX=re.compile(r'[0-9a-f]{64}\Z')
def need(ok,code):
    if not ok:raise ValueError(code)
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def load_pin(pin):
    p=Path(pin['path']);need(p.is_absolute() and p.is_file() and not p.is_symlink() and HEX.fullmatch(pin.get('sha256','')) and digest(p)==pin['sha256'],'changed-or-missing-input')
    return json.loads(p.read_text())
def names(source,field):
    m=re.search(r'\b'+field+r'\s*=\s*new HashSet<string>\(StringComparer.Ordinal\)\s*\{(.*?)\};',source,re.S)
    need(m is not None,'source-inventory-missing-'+field)
    values=re.findall(r'"([^"]+)"',m[1]);need(len(values)==len(set(values)),'duplicate-source-name')
    return set(values)
def expected_indexes(phase,source):
    if phase=='p37':return names(source,'IndexNames')
    path=ROOT/'backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations'/ (MIGRATIONS[phase]+'.cs')
    migration=path.read_text().split('protected override void Down')[0]
    explicit=re.findall(r'migrationBuilder.CreateIndex\(\s*name: \"([^\"]+)\"',migration)
    keys=re.findall(r'table\.(?:PrimaryKey|UniqueConstraint)\(\"([^\"]+)\"',migration)
    need(explicit and keys and len(explicit+keys)==len(set(explicit+keys)),'genuine-index-inventory-required')
    return set(explicit+keys)

def canonical(snapshot,phase,source):
    catalog=snapshot['catalog'];checks=names(source,'CheckNames');foreign=names(source,'ForeignNames');parents=names(source,'ParentNames' if phase=='p36' else 'OverlayKeyNames')
    owned={x.rsplit('.',1)[0] for x in checks};result={'checks':{},'foreignKeys':{},'parentKeys':{},'indexes':[]}
    for row in catalog['constraints']:
        schema,table,name,kind,definition,valid,deferred,initial=row
        full='.'.join(row[:3]);tablekey=schema+'.'+table
        if tablekey in owned and kind=='c':
            need(full not in result['checks'] and isinstance(definition,str) and definition.startswith('CHECK ('),'invalid-check')
            result['checks'][full]=definition
        if (tablekey in owned and kind=='f') or full in parents:
            target='parentKeys' if full in parents else 'foreignKeys'
            need(full not in result[target] and valid is True,'invalid-constraint-state')
            expected_deferred=phase=='p37' and name=='fk_pgret_current_revision'
            need(deferred is expected_deferred and initial is expected_deferred,'wrong-deferrability')
            need(kind==('u' if target=='parentKeys' else 'f') and definition.startswith('UNIQUE (' if target=='parentKeys' else 'FOREIGN KEY ('),'wrong-constraint-kind')
            result[target][full]={'definition':definition,'validated':valid,'deferrable':deferred,'initiallyDeferred':initial}
    need(set(result['checks'])==checks and set(result['foreignKeys'])==foreign and set(result['parentKeys'])==parents,'exact-constraint-inventory-required')
    for row in catalog['indexes']:
        need(len(row)==4,'invalid-index-row')
        if '.'.join(row[:2]) in owned:result['indexes'].append(row)
    index_names=[x[2] for x in result['indexes']]
    need(len(index_names)==len(set(index_names)) and all(isinstance(x[3],str) and x[3].startswith('CREATE ') for x in result['indexes']),'invalid-index-definition')
    need(set(index_names)==expected_indexes(phase,source),'exact-index-inventory-required')
    return result

def verify_capture(actual,snapshots,sources):
    need(set(actual)=={'p36','p37'},'exact-two-phases-required')
    for phase in ('p36','p37'):
        expected=canonical(snapshots[phase],phase,sources[phase])
        need(actual[phase]==expected,'canonical-capture-mismatch-'+phase)
    return actual

def render(source,phase,capture):
    fields={'Checks':capture['checks'],('ForeignKeys' if phase=='p36' else 'ForeignDefinitions'):{k:v['definition'] for k,v in capture['foreignKeys'].items()},('ParentKeys' if phase=='p36' else 'OverlayKeys'):{k:v['definition'] for k,v in capture['parentKeys'].items()}}
    if phase=='p37':fields['IndexDefinitions']={row[2]:row[3] for row in capture['indexes']}
    for field,values in fields.items():
        pattern=r'(internal static readonly IReadOnlyDictionary<string,string> '+field+r'\s*=\s*)new Dictionary<string,string>\([^;]*\);'
        body='new Dictionary<string,string>(StringComparer.Ordinal)\n    {\n'+''.join('        ['+json.dumps(k)+'] = '+json.dumps(v)+',\n' for k,v in sorted(values.items()))+'    };'
        source,count=re.subn(pattern,lambda m:m[1]+body,source,count=1)
        need(count==1,'empty-source-dictionary-required-'+field)
    source=source.replace('// Empty physical dictionaries deliberately keep the provider unavailable.','// Physical definitions imported verbatim from the accepted private PostgreSQL owner roundtrip.').replace('// Definitions MUST come from actual PostgreSQL capture; empty is unavailable.','// Definitions imported verbatim from the accepted private PostgreSQL owner roundtrip.')
    return source

def execute(plan):
    need(plan.get('status')=='REVIEWED_OFFLINE_JOINT_CAPTURE_IMPORT' and plan.get('sourceCommit')=='7e167e4491cdb2cd65b9afefe94b01710bcd3c44' and plan.get('migrationIds')==MIGRATIONS,'reviewed-exact-lineage-plan-required')
    for relative,expected in plan['migrationFiles'].items():
        need(relative.startswith('backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations/') and '..' not in Path(relative).parts and digest(ROOT/relative)==expected,'migration-source-changed')
    need(set(plan['migrationFiles'])=={str(Path('backend/procurement-api/src/Mightyfin.Erp.Procurement.Infrastructure/Migrations')/(mid+'.cs')) for mid in MIGRATIONS.values()},'exact-two-migration-pins-required')
    result=load_pin(plan['result']);need(result.get('status')=='PASS_PRIVATE_P36_P37_JOINT_EMPTY_FAMILY_ROUNDTRIP_ONLY' and result.get('sourceTables')==253 and result.get('p36Tables')==257 and result.get('p37Tables')==260 and result.get('sourceRowsAclHistoryUnchanged') is True and result.get('globalRolesUnchanged') is True,'actual-owner-pass-required')
    need(result.get('physicalCaptureSha256')==plan['capture']['sha256'],'result-capture-binding-required')
    capture=load_pin(plan['capture']);snapshots={k:load_pin(plan['snapshots'][k]) for k in CLASSES};sources={}
    for phase,name in CLASSES.items():
        path=ROOT/API/name;need(digest(path)==plan['sourceFiles'][name],'source-file-changed');sources[phase]=path.read_text()
        need('MigrationId = "'+MIGRATIONS[phase]+'"' in sources[phase],'source-migration-changed')
    verify_capture(capture,snapshots,sources)
    output=Path(plan['output']);need(output.is_absolute() and output.parent==Path('/home/amizo/erp-backups') and re.fullmatch(r'procurement-p36-p37-capture-candidate-20261025_run[0-9]{2}',output.name) and not output.exists() and output.parent.resolve()==output.parent,'fresh-private-output-required')
    os.umask(0o077);output.mkdir(mode=0o700)
    for phase,name in CLASSES.items():(output/name).write_text(render(sources[phase],phase,capture[phase]))
    (output/'provenance.json').write_text(json.dumps({'status':'OFFLINE_CAPTURE_CANDIDATE_ONLY','inputs':plan,'outputSha256':{n:digest(output/n) for n in CLASSES.values()},'featureEnabled':False,'finiteRoleProven':False,'runtimeAccepted':False},indent=2)+'\n')

def main():
    p=argparse.ArgumentParser();p.add_argument('--plan',type=Path,required=True);p.add_argument('--plan-sha256',required=True);a=p.parse_args()
    need(HEX.fullmatch(a.plan_sha256) and digest(a.plan)==a.plan_sha256,'plan-sha-required');execute(json.loads(a.plan.read_text()))
if __name__=='__main__':main()
