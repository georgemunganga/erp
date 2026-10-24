#!/usr/bin/env python3
"""Pure/file-only renderer of blocked Core grants and rollback-only SQL proof.
Requires separate mapped-model/physical catalog capture and reviewed helper pins.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import runpy
from types import SimpleNamespace
import core_profile
from run import need,pinned,validate_output,FOUNDATION

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--expected-plan-sha256',required=True);p.add_argument('--output',type=Path,required=True);args=p.parse_args()
    plan_path=pinned({'path':str(args.plan),'sha256':args.expected_plan_sha256});plan=json.loads(plan_path.read_text())
    need(plan['status']=='REVIEWED_CORE_SESSION_MODEL_PROFILE_RENDER_ONLY'
         and plan['rendererSha256']==hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
         and plan['profileSha256']==hashlib.sha256(Path(__file__).with_name('core_profile.py').read_bytes()).hexdigest()
         and plan['foundationSha256']==hashlib.sha256(FOUNDATION.read_bytes()).hexdigest(),'reviewed-model-render-required')
    files={k:pinned(plan['files'][k]) for k in ['mappedCoreManifest','catalog','stagebSqlHelper']}
    validate_output(args.output,[plan_path,FOUNDATION,*files.values()],render=True)
    shared=SimpleNamespace(**runpy.run_path(str(FOUNDATION)))
    spec=importlib.util.spec_from_file_location('reviewed_stageb_sql',files['stagebSqlHelper']);stageb=importlib.util.module_from_spec(spec);spec.loader.exec_module(stageb)
    manifest=json.loads(files['mappedCoreManifest'].read_text());catalog=json.loads(files['catalog'].read_text())
    target=core_profile.core_targets(manifest,catalog,plan['database'])
    proof,transport=core_profile.core_proof(shared,stageb,manifest,catalog,plan['database'])
    validate_output(args.output,[plan_path,FOUNDATION,*files.values()],render=True);args.output.mkdir(mode=0o700)
    blocked="\\set ON_ERROR_STOP on\nDO $blocked$ BEGIN RAISE EXCEPTION 'CoreSessionProfileReviewOnlyDoNotExecute' USING ERRCODE='55000'; END $blocked$;\n"
    outputs={'core-grants.REVIEW.sql':blocked+shared.grant_sql(target),'core-profile-proof.rollback.sql':proof,
        'render.private.json':json.dumps({'status':'CORE_MODEL_RENDER_ONLY_NO_GRANTS_OR_PHYSICAL_READINESS','transport':transport,'physicalReady':False,'httpExecuted':False},indent=2)+'\n'}
    for name,text in outputs.items():
        fd=os.open(args.output/name,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
        with os.fdopen(fd,'w') as stream:stream.write(text)

if __name__=='__main__':main()
