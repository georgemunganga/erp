import { test } from "node:test";
import assert from "node:assert/strict";
import { shouldOpenHrmSetup } from "../src/modules/hrm/setup-entry.ts";
import { normalizeWorkerImportType } from "../src/platform/components/ImportExport/worker-import-values.ts";
const pending = {real:true, product:true, roles:["hr_admin"], confined:false, status:"pending"};
test("new SaaS company admin enters setup, completed company passes through", () => {
 assert.equal(shouldOpenHrmSetup(pending),true);
 assert.equal(shouldOpenHrmSetup({...pending,status:"complete"}),false);
});
test("employees, restricted branches and unknown checks never enter configuration", () => {
 for(const change of [{roles:["employee"]},{confined:true},{confined:undefined},{status:undefined},{real:false}])
  assert.equal(shouldOpenHrmSetup({...pending,...change}),false);
});
test("legacy organization setup and HR operations still work", () => {
 assert.equal(shouldOpenHrmSetup({...pending,product:false,roles:[]}),true);
 assert.equal(shouldOpenHrmSetup({...pending,roles:["hr_ops"]}),true);
});
test("employee spreadsheet labels normalize before preview and apply", () => {
 for(const [label,code] of [[" Permanent ","employee"],["EMPLOYEE","employee"],["Contractor","contingent"],["Contingent","contingent"],["Intern","intern"],["Volunteer","volunteer"]])
  assert.equal(normalizeWorkerImportType(label),code);
 for(const label of ["Fixed term","Part time","Unknown","toString",""])
  assert.equal(normalizeWorkerImportType(label),label);
});
