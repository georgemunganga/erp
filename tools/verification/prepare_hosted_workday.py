"""Create a fresh focused SaaS source snapshot, preserving the deployed model/profile."""
from pathlib import Path
import argparse, hashlib, json, subprocess, re

root=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser();p.add_argument('--output',required=True);args=p.parse_args()
out=Path(args.output).resolve()
subprocess.run(['python',str(root/'tools/verification/prepare_owner_policy.py'),'--output',str(out)],check=True)
source=out/'source';manifest=json.loads((out/'source-manifest.json').read_text());changes=[]
prefix='backend/hrm-api/src/Mightyfin.Erp.Hrm.'
paths=['Domain/Entities/MobileWorkdayEvent.cs','Application/Time/MobileWorkdayRules.cs',
       'Infrastructure/MobileWorkdayStore.cs','Infrastructure/Data/HrmDbContext.MobileWorkday.cs',
       'Infrastructure/Data/MobileWorkdaySchema.cs','Api/MobileWorkdayApi.cs','Api/NativeMobileWorkday.cs',
       'Api/NativeAttendanceApi.cs','Tests/MobileWorkdayRulesTests.cs','Tests/MobileWorkdayStoreTests.cs',
       'Tests/NativeAttendanceHttpTests.cs','Tests/NativeWorkdayHttpTests.cs']
for name in paths:
 path=prefix+name;target=source/path;old=target.read_bytes() if target.exists() else b''
 target.write_bytes((root/path).read_bytes())
 changes.append({'path':path,'before_sha256':hashlib.sha256(old).hexdigest(),'after_sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
path=prefix+'Infrastructure/Data/HrmDbContext.cs';target=source/path;s=target.read_text(encoding='utf-8-sig');old=target.read_bytes()
assert 'public sealed partial class HrmDbContext' in s
before='    private void FillTenantIds()\n    {'
assert s.count(before)==1
s=s.replace(before,before+'\n        if(ChangeTracker.Entries<MobileWorkdayEvent>().Any(e=>e.State is EntityState.Modified or EntityState.Deleted))\n            throw new Mightyfin.Erp.Hrm.Application.DomainException("workday-evidence-immutable","Append a new workday action instead of changing its history.");',1)
before='    protected override void OnModelCreating(ModelBuilder modelBuilder)\n    {'
assert s.count(before)==1
s=s.replace(before,before+'\n        ConfigureMobileWorkday(modelBuilder);',1)
target.write_text(s,encoding='utf8')
changes.append({'path':path,'before_sha256':hashlib.sha256(old).hexdigest(),'after_sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
# The runtime checks its compiled model against the owning snapshot before startup.
# Extend the DEPLOYED snapshot with only this feature's generated entity blocks;
# importing the current full snapshot would wrongly import unrelated old-instance schema.
snapshot_path=prefix+'Infrastructure/Migrations/HrmDbContextModelSnapshot.cs'
canonical=(root/snapshot_path).read_text(encoding='utf-8-sig')
blocks=re.findall(r'^            modelBuilder\.Entity\("Mightyfin\.Erp\.Hrm\.Domain\.Entities\.MobileWorkdayEvent", b =>\n                \{\n.*?^                \}\);\n',canonical,re.M|re.S)
assert len(blocks)==2
target=source/snapshot_path;old=target.read_bytes();snapshot=target.read_text(encoding='utf-8-sig')
assert 'Entities.MobileWorkdayEvent' not in snapshot
marker='#pragma warning restore 612, 618';assert snapshot.count(marker)==1
snapshot=snapshot.replace(marker,'\n'.join(blocks)+'\n'+marker,1)
target.write_text(snapshot,encoding='utf8')
changes.append({'path':snapshot_path,'before_sha256':hashlib.sha256(old).hexdigest(),'after_sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
migration='20261117202222_MobileWorkdayEvents'
path=prefix+'Infrastructure/Migrations/'+migration+'.cs';target=source/path;assert not target.exists();target.write_bytes((root/path).read_bytes())
changes.append({'path':path,'before_sha256':None,'after_sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
designer=snapshot.replace('using Microsoft.EntityFrameworkCore.Infrastructure;','using Microsoft.EntityFrameworkCore.Infrastructure;\nusing Microsoft.EntityFrameworkCore.Migrations;',1)
designer=designer.replace('[DbContext(typeof(HrmDbContext))]','[DbContext(typeof(HrmDbContext))]\n    [Migration("'+migration+'")]',1)
designer=designer.replace('partial class HrmDbContextModelSnapshot : ModelSnapshot','partial class MobileWorkdayEvents',1).replace('override void BuildModel(','override void BuildTargetModel(',1)
path=prefix+'Infrastructure/Migrations/'+migration+'.Designer.cs';target=source/path;assert not target.exists();target.write_text(designer,encoding='utf8')
changes.append({'path':path,'before_sha256':None,'after_sha256':hashlib.sha256(target.read_bytes()).hexdigest()})
manifest.update(workday_source=subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip(),
                workday_changes=changes,profile='hosted-mobile-workday-v1',full_current_model_migrated=False)
(out/'workday-source-manifest.json').open('x').write(json.dumps(manifest,indent=2))
print(json.dumps({'source':str(source),'focused_paths':len(changes),'existing_deployed_profile_preserved':True}))
