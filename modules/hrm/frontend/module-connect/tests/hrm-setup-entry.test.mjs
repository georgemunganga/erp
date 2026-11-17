import { test } from "node:test";
import assert from "node:assert/strict";
import { shouldOpenHrmSetup } from "../src/modules/hrm/setup-entry.ts";
import { normalizeWorkerImportType, cleanWorkerImportValue } from "../src/platform/components/ImportExport/worker-import-values.ts";
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

test("import formatting keeps important signs, invalid input and identifier letters", () => {
 assert.equal(cleanWorkerImportValue("basicSalary", "-1,200.50"), "-1200.50");
 assert.equal(cleanWorkerImportValue("basicSalary", "bad1200"), "bad1200");
 assert.equal(cleanWorkerImportValue("basicSalary", "1.2.3"), "1.2.3");
 assert.equal(cleanWorkerImportValue("tpin", "ABC1234567890"), "ABC1234567890");
 assert.equal(cleanWorkerImportValue("napsaNumber", "NAPSA-001"), "NAPSA-001");
 assert.equal(cleanWorkerImportValue("nrc", "bad123456/78/1"), "bad123456/78/1");
 assert.equal(cleanWorkerImportValue("phone", "+260 (97) 123-4567"), "+260971234567");
 assert.equal(cleanWorkerImportValue("workerType", " PERMANENT "), "employee");
});
