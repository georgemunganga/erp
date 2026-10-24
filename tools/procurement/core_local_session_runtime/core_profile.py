"""Core-owned pure mapped-profile validation. SQL rendering reuses PR70, not a second engine."""
import re

TABLES={'local_users','local_sessions','tenant_role_assignments','audit_entries','privileged_action_events',
        'legal_entities','work_locations','org_units','hr_user_branch_assignments','workers','__hrm_migrations'}
USER_UPDATES={'failed_login_count','locked_until','last_login_at','password_hash','must_change_password','password_changed_at'}
SESSION_UPDATES={'last_seen_at','revoked_at'}
INSERTS={'local_sessions','audit_entries','privileged_action_events'}
WORKER_READS={'id','tenant_id','is_archived','status','location_id','org_unit_id','manager_id'}

def need(ok,code):
    if not ok:raise RuntimeError(code)

def aliases(database):
    need(isinstance(database,str) and re.fullmatch(r'proc_p30_core_session_20261024_[0-9]{2}',database),'fresh-Core-session-database-required')
    return tuple('p30_'+kind+'_login_'+database[-2:] for kind in ['core_session','record','execute'])

def validate(manifest,catalog):
    need(set(manifest)=={'SchemaVersion','Owner','Scope','Relations','PhysicalSchemaVerified','RuntimeReady'}
         and manifest['SchemaVersion']==1 and manifest['Owner']=='Core'
         and manifest['Scope']=='PRIVATE_LOCAL_SESSION_AND_CURRENT_CORE_READS_ONLY'
         and manifest['PhysicalSchemaVerified'] is False and manifest['RuntimeReady'] is False,
         'mapped-model-only-Core-manifest-required')
    relations=manifest['Relations'];need(isinstance(relations,list) and len(relations)==11,'exact-eleven-Core-relations-required')
    columns={(r[0],r[1],r[3]) for r in catalog['columns']}
    need({r['Table'] for r in relations}==TABLES,'unknown-or-missing-Core-relation')
    for row in relations:
        table=row['Table']
        need(set(row)=={'Schema','Table','Select','Insert','Update'} and row['Schema']=='hrm','Core-owning-schema-required')
        for operation in ['Select','Insert','Update']:
            values=row[operation]
            need(isinstance(values,list) and len(values)==len(set(values)) and all(isinstance(c,str) and ('hrm',table,c) in columns for c in values),'unknown-or-duplicate-mapped-Core-column')
        need(set(row['Update'])==(USER_UPDATES if table=='local_users' else SESSION_UPDATES if table=='local_sessions' else set()),'bounded-auth-session-update-only')
        need(bool(row['Insert'])==(table in INSERTS),'only-session-and-two-audit-inserts')
        if table in {'audit_entries','privileged_action_events'}:need(row['Select']==[],'append-only-audit-not-readable-by-auth-role')
        else:need(bool(row['Select']),'actual-Core-read-projection-required')
        if table=='workers':need(set(row['Select'])==WORKER_READS,'worker-metadata-only-no-sensitive-fields')
        if table=='__hrm_migrations':need(set(row['Select'])=={'MigrationId','ProductVersion'},'history-read-only-exact-columns')
        if table=='local_users':need({'id','tenant_id','password_hash','roles_csv','worker_id','is_active','is_archived','locked_until'}<=set(row['Select']),'real-owning-auth-account-reads-required')
        if table=='local_sessions':need({'id','tenant_id','local_user_id','token_hash','expires_at','revoked_at','last_seen_at'}<=set(row['Select']),'real-owning-session-reads-required')
    return relations

def core_targets(manifest,catalog,database):
    role=aliases(database)[0];relations=validate(manifest,catalog)
    owners={(r[0],r[1]):r[2] for r in catalog['tables']};schemas={r[0]:r[1] for r in catalog['schemas']}
    need(schemas['hrm']!=role,'Core-role-cannot-own-schema')
    result={'table':set(),'column':set(),'schema':{('hrm',role,schemas['hrm'],'USAGE',False)}}
    for row in relations:
        key=('hrm',row['Table']);need(key in owners and owners[key]!=role,'Core-role-cannot-own-its-tables')
        for operation in ['Select','Insert','Update']:
            for column in row[operation]:
                result['column'].add(('hrm',row['Table'],column,role,owners[key],operation.upper(),False))
    return result

def stageb_matrix(manifest,catalog):
    mapped={('hrm',r['Table']):r for r in validate(manifest,catalog)}
    result={}
    for schema,table,*_ in catalog['tables']:
        row=mapped.get((schema,table))
        result[schema+'.'+table]={'expectedAfter':{'recordTablePrivileges':[],
            'recordColumnPrivileges':({op.upper():row[op] for op in ['Select','Insert','Update'] if row[op]} if row else {}),
            'executionTablePrivileges':[],'executionColumnPrivileges':{}}}
    need(len(result)==218,'existing-exact218-store-matrix-required')
    return result

def core_proof(shared,stageb,manifest,catalog,database):
    """Bounded adapter of reviewed full218/4097 proof; no alternate privilege engine.
    StageB's table-INSERT negative needs three exact column-INSERT replacements.
    """
    role=aliases(database)[0];logical='p30_record_login_'+database[-2:]
    matrix=stageb_matrix(manifest,catalog)
    proof,base=shared.login_proof(stageb,matrix,catalog,'record',logical)
    old='IF session_user<>'+stageb.literal(logical)+' OR current_user<>'+stageb.literal(logical)
    need(proof.count(old)==1,'shared-authenticated-identity-boundary-changed')
    proof=proof.replace(old,'IF session_user<>'+stageb.literal(role)+' OR current_user<>'+stageb.literal(role),1)
    # Core needs USAGE only on hrm. Keep StageB's full predicate/expectation
    # engine, but resolve privilege targets through catalog OIDs so checking a
    # forbidden Procurement table cannot itself require USAGE on that schema.
    name_lookup="split_part(key,'.',1)||'.'||quote_ident(split_part(key,'.',2))"
    oid_lookup="(SELECT c.oid FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname=split_part(key,'.',1) AND c.relname=split_part(key,'.',2))"
    need(proof.count(name_lookup)==2 and proof.count('END=allowed')==1,'shared-effective-privilege-expression-changed')
    proof=proof.replace(name_lookup,oid_lookup).replace('END=allowed','END IS NOT DISTINCT FROM allowed',1)
    physical={(r[0],r[1],r[3]):r[4] for r in catalog['columns']}
    extra=[];replaced=0
    for row in validate(manifest,catalog):
        relation=stageb.table_name('hrm.'+row['Table'])
        if row['Insert']:
            first=next(c[3] for c in catalog['columns'] if c[:2]==['hrm',row['Table']])
            deny_sql='INSERT INTO '+relation+' ('+stageb.ident(first)+') SELECT NULL WHERE false'
            deny="BEGIN EXECUTE "+stageb.literal(deny_sql)+"; RAISE EXCEPTION 'UnexpectedFinitePrivilegeSuccess' USING ERRCODE='55000'; EXCEPTION WHEN insufficient_privilege THEN NULL; END;"
            need(proof.count(deny)==1,'shared-column-INSERT-boundary-changed')
            values=[]
            for column in row['Insert']:
                type_name=physical[('hrm',row['Table'],column)]
                need(isinstance(type_name,str) and re.fullmatch(r'[A-Za-z0-9_ (),\[\]]+',type_name),'reviewed-physical-type-required')
                values.append('NULL::'+type_name)
            positive='INSERT INTO '+relation+' ('+','.join(stageb.ident(c) for c in row['Insert'])+') SELECT '+','.join(values)+' WHERE false'
            proof=proof.replace(deny,'EXECUTE '+stageb.literal(positive)+';',1);replaced+=1
        for column in row['Update']:
            update='UPDATE '+relation+' SET '+stageb.ident(column)+'='+stageb.ident(column)+' WHERE false'
            extra.append('EXECUTE '+stageb.literal(update)+';')
    need(replaced==3 and proof.count('\nEND $finite$;\nROLLBACK;\n')==1,'exact-three-Core-column-inserts-required')
    proof=proof.replace('\nEND $finite$;\nROLLBACK;\n','\n'+'\n'.join(extra)+'\nEND $finite$;\nROLLBACK;\n',1)
    need(not shared.SWITCH.search(proof),'Core-proof-must-never-switch-role')
    return proof,{'sharedBase':base,'columnInsertReplacements':3,'boundedPositiveColumnUpdates':len(extra),'privilegeNameToOidLookups':2,'nullSafeExpectedComparison':1,
                  'owner':'Core','scope':'PRIVATE_ZERO_ROW_SQL_NOT_HTTP_OR_AUDIT_PROOF'}

def verify_container(row,identifier,name,image,source,pub,socket,inputs,result):
    need(row['Id']==identifier and row['Name']=='/'+name and row['Image']==image
         and row['Config'].get('Labels',{}).get('p30.core-session.source')==source,'exact-created-Core-container-required')
    h=row['HostConfig']
    need(h['NetworkMode']=='none' and h['ReadonlyRootfs'] and not h['Privileged'] and not h.get('PortBindings') and not h.get('CapAdd')
         and h['NanoCpus']==1000000000 and h['Memory']==h['MemorySwap']==1100*1024*1024 and h['PidsLimit']==128
         and h['Tmpfs']=={'/tmp':'rw,nosuid,nodev,size=64m'},'bounded-private-Core-runtime-required')
    binds=[m for m in row['Mounts'] if m['Type']=='bind'];other=[m for m in row['Mounts'] if m['Type']!='bind']
    need(len(binds)==4 and {(m['Source'],m['Destination'],m['RW']) for m in binds}=={
        (str(pub),'/app',False),(str(socket),'/private-pg-socket',False),(str(inputs),'/evidence',False),(str(result),'/result',True)}
         and len(other)<=1 and all(m['Type']=='tmpfs' and m['Destination']=='/tmp' and m['RW'] for m in other),'exact-private-Core-mounts-required')
