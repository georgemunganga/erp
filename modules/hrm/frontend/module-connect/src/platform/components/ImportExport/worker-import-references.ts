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

export type ImportCompletion = {
 confirmed:boolean; created:number; updated:number; skipped:number; message?:string;
 remainingRows:Record<string,string>[];
 issues:{row:number;status:string;message?:string}[];
};
/** Reconcile server outcomes with the reviewed rows; never reuse an applied preview. */
export function importCompletion(previewRows:Record<string,unknown>[], inputRows:Record<string,string>[], response:unknown):ImportCompletion {
 if(!response || typeof response!=="object") throw new Error("Import response was incomplete.");
 const value=response as {created?:unknown;updated?:unknown;skipped?:unknown;rowOutcomes?:unknown};
 for(const key of ["created","updated","skipped"] as const)
  if(!Number.isInteger(value[key]) || (value[key] as number)<0) throw new Error("Import response was incomplete.");
 if(!Array.isArray(value.rowOutcomes)) throw new Error("Import outcomes were unavailable.");
 const remaining=new Set<number>();
 previewRows.forEach((row,index)=>{if(String(row.status).toLowerCase()==="error") remaining.add(index);});
 const issues:{row:number;status:string;message?:string}[]=[];
 for(const item of value.rowOutcomes) {
  if(!item || typeof item!=="object" || !Number.isInteger(item.row) || typeof item.status!=="string") throw new Error("Import outcomes were incomplete.");
  if(item.status!=="ok") { remaining.add(item.row-2); issues.push({row:item.row,status:item.status,message:typeof item.message==="string"?item.message:undefined}); }
 }
 return {confirmed:true,created:value.created as number,updated:value.updated as number,skipped:value.skipped as number,
  remainingRows:[...remaining].sort((a,b)=>a-b).map(index=>inputRows[index]).filter(Boolean),issues};
}
