import { FormSelect } from "@/components/ui/form-select";
import { RecordTable } from "./RecordTable";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardHeader, CardTitle, CardContent, CardDescription } from "@/components/ui/card";
import { hrmApi } from "@/platform/api-client";
import { useApi } from "@/platform/use-api";

type Period = { id:string; periodLabel:string; startDate:string; endDate:string };
type Benefit = { id:string; name:string; includeInPayroll:boolean; isActive:boolean };
type Row = { workerId:string; employeeNo:string; workerName:string; locationId:string|null; branch:string; orgUnitId:string|null; department:string; amount:number|null; note:string; recurringMonthlyAmount:number; claimManaged:boolean };
type Batch = { payPeriodId:string; periodLabel:string; currency:string; benefitTypeId:string; benefitName:string; editable:boolean; lockReason:string|null; rows:Row[] };
type Draft = { amount:string; note:string };
export async function benefitPayrollPeriods() {
  const groups=await hrmApi.get<{id:string;name:string}[]>("/hrm/payroll/pay-groups");
  const lists = await Promise.all(groups.map(async group => {
    const rows = await hrmApi.get<Period[]>(`/hrm/payroll/pay-groups/${group.id}/periods`);
    return rows.map(period => ({ ...period, periodLabel: `${period.periodLabel} — ${group.name}` }));
  }));
  return lists.flat().sort((a, b) => b.startDate.localeCompare(a.startDate));
}
export function PeriodBenefitAssignments({types}:{types:Benefit[]}) {
  const choices=types.filter(t=>t.isActive&&t.includeInPayroll);
  const options=useApi(benefitPayrollPeriods,[]);
  const [periodId,setPeriodId]=useState("");const [typeId,setTypeId]=useState("");
  const [batch,setBatch]=useState<Batch|null>(null);const [loading,setLoading]=useState(false);const [busy,setBusy]=useState(false);const [error,setError]=useState("");
  const [query,setQuery]=useState("");const [branch,setBranch]=useState("");const [department,setDepartment]=useState("");
  const [drafts,setDrafts]=useState<Record<string,Draft>>({});const [refresh,setRefresh]=useState(0);
  const dirty=Object.keys(drafts).length>0;
  useEffect(()=>{ if(!dirty)return;const warn=(e:BeforeUnloadEvent)=>{e.preventDefault();e.returnValue="";};window.addEventListener("beforeunload",warn);return()=>window.removeEventListener("beforeunload",warn);},[dirty]);
  useEffect(()=>{let current=true;setBatch(null);setError("");if(!periodId||!typeId){setLoading(false);return;}setLoading(true);
    hrmApi.get<Batch>(`/hrm/payroll/periods/${periodId}/benefits/${typeId}`).then(b=>{if(current)setBatch(b);}).catch(e=>{if(current)setError(e.message);}).finally(()=>{if(current)setLoading(false);});return()=>{current=false;};
  },[periodId,typeId,refresh]);
  function select(kind:"period"|"type",value:string){if(dirty&&!window.confirm("Discard unsaved benefit assignments before changing the selection?"))return;setDrafts({});setBranch("");setDepartment("");if(kind==="period")setPeriodId(value);else setTypeId(value);}
  const rows=batch?.rows??[];
  const branches=Array.from(new Map(rows.map(r=>[r.locationId||"unassigned",r.branch])).entries());
  const departments=Array.from(new Map(rows.filter(r=>!branch||(r.locationId||"unassigned")===branch).map(r=>[r.orgUnitId||"unassigned",r.department])).entries());
  const visible=rows.filter(r=>(!branch||(r.locationId||"unassigned")===branch)&&(!department||(r.orgUnitId||"unassigned")===department)&&`${r.workerName} ${r.employeeNo}`.toLowerCase().includes(query.toLowerCase()));
  const changed=visible.filter(r=>drafts[r.workerId]);const hidden=Object.keys(drafts).filter(id=>!visible.some(r=>r.workerId===id)).length;
  function edit(row:Row,field:keyof Draft,value:string){setDrafts(prev=>({...prev,[row.workerId]:Object.assign({amount:row.amount==null?"":String(row.amount),note:row.note},prev[row.workerId],{[field]:value})}));setError("");}
  async function save(){
    if(!batch||!changed.length)return;
    const invalid=changed.find(r=>{const amount=drafts[r.workerId].amount;return amount!==""&&(!/^\d+(\.\d{1,2})?$/.test(amount)||Number(amount)>999999999);});
    if(invalid){setError(`${invalid.employeeNo}: enter a non-negative amount with no more than two decimal places.`);return;}
    setBusy(true);setError("");
    try{await hrmApi.put(`/hrm/payroll/periods/${periodId}/benefits/${typeId}`,{rows:changed.map(r=>({workerId:r.workerId,amount:drafts[r.workerId].amount===""?null:Number(drafts[r.workerId].amount),note:drafts[r.workerId].note}))});
      setDrafts(prev=>{const next={...prev};for(const row of changed)delete next[row.workerId];return next;});toast.success(`Saved ${changed.length} assignments for ${batch.periodLabel}.`);setRefresh(n=>n+1);
    }catch(e){setError(e instanceof Error?e.message:"Unable to save assignments.");}finally{setBusy(false);}
  }
  return <Card data-testid="period-benefit-assignments"><CardHeader><CardTitle>Once-off payroll assignment</CardTitle><CardDescription>Choose the benefit and payroll month, then enter amounts only for the employees receiving it. Amounts are paid in full for that period and go through payroll approval.</CardDescription></CardHeader><CardContent className="space-y-4">
    <div className="grid gap-3 md:grid-cols-2"><label>Benefit type<FormSelect aria-label="Once-off benefit type" className="block h-10 w-full rounded-md border bg-background px-2" disabled={busy} value={typeId} onChange={e=>select("type",e.target.value)}><option value="">Select benefit</option>{choices.map(t=><option key={t.id} value={t.id}>{t.name}</option>)}</FormSelect></label><label>Payroll month / period<FormSelect aria-label="Benefit payroll period" className="block h-10 w-full rounded-md border bg-background px-2" disabled={busy||options.loading} value={periodId} onChange={e=>select("period",e.target.value)}><option value="">Select payroll period</option>{(options.data??[]).map(p=><option key={p.id} value={p.id}>{p.periodLabel} ({p.startDate} – {p.endDate})</option>)}</FormSelect></label></div>
    <p className="text-sm text-muted-foreground">A monthly assignment replaces any recurring amount for the same benefit and employee in that period. Enter 0 to pay none; clear a saved amount to remove the assignment and restore any recurring amount. Other months are unaffected.</p>
    {(error||options.error||options.degraded)&&<p role="alert" className="text-danger">{error||options.error||"Payroll periods could not be loaded."}</p>}
    {loading&&<p role="status">Loading assignments…</p>}
    {batch&&<>
      <p>{batch.benefitName} · {batch.periodLabel} · {batch.currency} · {batch.editable?"Open for entry":"Read-only"}</p>{batch.lockReason&&<p role="status" className="rounded-md border p-3">{batch.lockReason}</p>}
      <div className="grid gap-3 md:grid-cols-3"><label>Search employees<Input aria-label="Search monthly employees" value={query} disabled={busy} onChange={e=>setQuery(e.target.value)} placeholder="Name or employee number" /></label><label>Branch<FormSelect aria-label="Monthly assignment branch" className="block h-9 w-full rounded-md border bg-background px-2" disabled={busy} value={branch} onChange={e=>{setBranch(e.target.value);setDepartment("");}}><option value="">All branches</option>{branches.map(([id,name])=><option key={id} value={id}>{name}</option>)}</FormSelect></label><label>Department<FormSelect aria-label="Monthly assignment department" className="block h-9 w-full rounded-md border bg-background px-2" disabled={busy} value={department} onChange={e=>setDepartment(e.target.value)}><option value="">All departments</option>{departments.map(([id,name])=><option key={id} value={id}>{name}</option>)}</FormSelect></label></div>
      <div className="overflow-x-auto rounded-lg border"><RecordTable className="w-full min-w-[760px] text-left text-sm"><caption className="p-3 text-left">{visible.length} eligible employees</caption><thead><tr>{["Employee","Branch / department",`Amount (${batch.currency})`,"Note / trip calculation","Saved"].map(h=><th key={h} scope="col" className="p-3">{h}</th>)}</tr></thead><tbody>{visible.map(r=>{const draft=drafts[r.workerId];return <tr key={r.workerId} data-employee={r.employeeNo} className="border-t"><th scope="row" className="p-3 font-normal">{r.workerName}<span className="block text-xs text-muted-foreground">{r.employeeNo}</span></th><td className="p-3">{r.branch}<span className="block text-xs">{r.department}</span></td><td className="p-3"><Input type="number" min="0" step="0.01" aria-label={`Benefit amount for ${r.employeeNo}`} value={draft?.amount??(r.amount==null?"":String(r.amount))} disabled={busy||!batch.editable||r.claimManaged} onChange={e=>edit(r,"amount",e.target.value)} />{r.recurringMonthlyAmount>0&&<span className="text-xs">Recurring: {r.recurringMonthlyAmount.toFixed(2)}</span>}</td><td className="p-3"><Input aria-label={`Benefit note for ${r.employeeNo}`} maxLength={500} value={draft?.note??r.note} disabled={busy||!batch.editable||r.claimManaged} onChange={e=>edit(r,"note",e.target.value)} placeholder="e.g. 11 trips × K250" /></td><td className="p-3">{r.amount==null?"Not assigned":r.amount.toFixed(2)}{r.claimManaged&&<span className="block text-xs text-muted-foreground">Edit or delete claims in Claims</span>}{draft&&<span className="block text-xs">Unsaved change</span>}</td></tr>;})}</tbody></RecordTable>{!visible.length&&<p className="p-4">No employees match these filters.</p>}</div>
      <div className="flex flex-wrap items-center justify-between gap-3"><p className="text-sm">{changed.length} changed rows in this view{hidden?` · ${hidden} hidden drafts will not be saved`:""}</p><Button onClick={save} disabled={busy||!batch.editable||!changed.length||changed.length>500}>{busy?"Saving…":`Save ${changed.length} assignments`}</Button></div>
    </>}
  </CardContent></Card>;
}
