import {test} from "node:test";
import assert from "node:assert/strict";
import {autoMapImportColumns,missingImportBranches} from "../src/platform/components/ImportExport/worker-import-references.ts";
const fields=[{key:"employeeNo",label:"Employee number"},{key:"firstName",label:"First name"},{key:"lastName",label:"Last name"},{key:"email",label:"Work email"},{key:"workerType",label:"Employment type"},{key:"orgUnitName",label:"Department"},{key:"locationId",label:"Branch"},{key:"nrc",label:"NRC"},{key:"startDate",label:"Start date"},{key:"basicSalary",label:"Basic salary"}];
test("the user's underscore headings map only to supported, intended fields",()=>{
 const map=autoMapImportColumns(["employee_id","first_name","last_name","email","nrc_number","branch","department","employment_type","hire_date","basic_salary","gender","date_of_birth","legal_entity","bank_account_number","contract_end_date"],fields);
 assert.deepEqual(Object.values(map),["employeeNo","firstName","lastName","email","nrc","locationId","orgUnitName","workerType","startDate","basicSalary","__skip__","__skip__","__skip__","__skip__","__skip__"]);
});
test("unknown or duplicate source columns never claim an unrelated field",()=>{
 assert.deepEqual(autoMapImportColumns(["banana","FIRST NAME","first_name","pay_frequency"],fields),{banana:"__skip__","FIRST NAME":"firstName",first_name:"__skip__",pay_frequency:"__skip__"});
});
test("missing branches compare company-owned names regardless of case and whitespace",()=>{
 const rows=[{locationId:" Lusaka HQ "},{locationId:"lusaka hq"},{locationId:"Kitwe"},{locationId:"Ndola"}];
 assert.deepEqual(missingImportBranches(rows,[{id:"1",name:"LUSAKA HQ",legalEntityId:"A"},{id:"2",name:"Kitwe",legalEntityId:"B"}],"A"),["Kitwe","Ndola"]);
});
test("unknown IDs, blanks and archived names cannot become inferred branch duplicates",()=>{
 const rows=[{locationId:"00000000-0000-0000-0000-000000000000"},{locationId:""},{locationId:"Ndola"}];
 assert.deepEqual(missingImportBranches(rows,[{id:"1",name:"Ndola",legalEntityId:"A",isArchived:true}],"A"),["Ndola"]);
});
