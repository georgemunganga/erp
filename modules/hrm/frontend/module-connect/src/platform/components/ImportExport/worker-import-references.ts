export type ImportColumnField = {key:string; label:string};
const normalized = (value:string) => value.trim().toLowerCase().replace(/[^a-z0-9]/g, "");
const aliases: Record<string,string> = {
 employeeid:"employeeNo", employeenumber:"employeeNo", employeeno:"employeeNo",
 workemail:"email", emailaddress:"email", nrcnumber:"nrc", hiredate:"startDate",
 employmenttype:"workerType", workertype:"workerType", department:"orgUnitName",
 departmentname:"orgUnitName", branch:"locationId", branchname:"locationId",
 location:"locationId", locationname:"locationId", branchid:"locationId",
};
/** Exact normalized keys/labels and declared aliases only. Unknown columns stay visible/unmapped. */
export function autoMapImportColumns(columns:string[], fields:ImportColumnField[]):Record<string,string> {
 const map:Record<string,string>={}; const claimed=new Set<string>();
 for(const column of columns) {
  const key=normalized(column);
  const exact=fields.filter(field=>normalized(field.key)===key);
  const alias=Object.hasOwn(aliases,key)?fields.filter(field=>field.key===aliases[key]):[];
  const labels=fields.filter(field=>normalized(field.label)===key);
  const candidates=exact.length?exact:alias.length?alias:labels;
  map[column]=candidates.length===1&&!claimed.has(candidates[0].key)?candidates[0].key:"__skip__";
  if(map[column]!=="__skip__") claimed.add(map[column]);
 }
 return map;
}
export type ImportBranchReference={id:string;name:string;legalEntityId:string;isArchived?:boolean};
/** Unknown IDs cannot be converted into new branch names. Only named values can be proposed. */
export function missingImportBranches(rows:Record<string,string>[], locations:ImportBranchReference[], companyId:string):string[] {
 const found=new Set<string>(); const result:string[]=[];
 const available=locations.filter(location=>location.legalEntityId===companyId&&!location.isArchived);
 for(const row of rows) {
  const name=(row.locationId??"").trim(); const key=name.toLowerCase();
  if(!name||/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(name)||found.has(key)) continue;
  found.add(key);
  if(!available.some(location=>location.name.trim().toLowerCase()===key)) result.push(name);
 }
 return result;
}
