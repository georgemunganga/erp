"""Prepare an exclusive source snapshot for the deployed SaaS baseline plus owner policy.
No customer checkout, database or retained publication is changed.
"""
from pathlib import Path
import argparse,subprocess,tarfile,hashlib,json

parser=argparse.ArgumentParser()
parser.add_argument('--output',required=True,type=Path)
parser.add_argument('--baseline',default='d41303dd56ec0196e040044fbc55d0ae5faddbe7')
parser.add_argument('--policy-source',default='f081d5a34e4ed0e0c1ecf6604e62ba81938bf90d')
args=parser.parse_args();repo=Path(__file__).resolve().parents[2]
def git(*a):return subprocess.check_output(['git','-C',str(repo),*a])
base=git('rev-parse','--verify',args.baseline+'^{commit}').decode().strip()
shared=git('rev-parse','--verify',args.policy_source+'^{commit}').decode().strip()
p=args.output.resolve();p.mkdir();source=p/'source';source.mkdir();archive=p/'baseline.tar'
subprocess.run(['git','-C',str(repo),'archive','--format=tar','-o',str(archive),base],check=True)
with tarfile.open(archive) as t:
 for x in t.getmembers():assert (source/x.name).resolve().is_relative_to(source) and (x.isfile() or x.isdir())
 t.extractall(source,filter='data')
names=['backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/CoreOwnerModuleAccess.cs',
       'backend/hrm-api/src/Mightyfin.Erp.Hrm.Api/CoreProductSessionAdmission.cs',
       'backend/hrm-api/src/Mightyfin.Erp.Hrm.Tests/CoreOwnerModuleAccessTests.cs']
changes=[]
for name in names:
 target=source/name;before=hashlib.sha256(target.read_bytes()).hexdigest() if target.exists() else None
 data=git('show',shared+':'+name);target.write_bytes(data)
 changes.append({'path':name,'before_sha256':before,'after_sha256':hashlib.sha256(data).hexdigest()})
(p/'source-manifest.json').open('x').write(json.dumps({'baseline':base,'owner_policy_source':shared,'changes':changes},indent=2))
print(json.dumps({'baseline':base,'policy_source':shared,'source':str(source),'changed_paths':len(changes)}))
