import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { Async } from "@/platform/components/Async";
import { useAuth } from "@/platform/auth";
import { hrmApi } from "@/platform/api-client";
import { useApi, realApi } from "@/platform/use-api";
import { todayLocal, type AttendanceEmployee } from "@/platform/attendance-entry";

type Leave = { id: string; workerId: string; employeeNo: string; workerName: string; leaveTypeCode: string; startDate: string; endDate: string; days: number; status: string; note: string; evidenceAttached: boolean };
type LeaveType = { code: string; name: string; isActive: boolean; requiresEvidence: boolean };
type Form = { workerId: string; leaveTypeCode: string; startDate: string; endDate: string; note: string; evidenceAttached: boolean };
type Quote = { days: number; available: number; excludedDates: string[] };
const api = "/hrm/time/recorded-leave";
export const Route = createFileRoute("/hrm/leave/recorded")({ head: () => ({ meta: [{ title: "Recorded leave — HR workspace" }] }), component: Page });
function Page() {
  const { user } = useAuth();
  return <AuthGate><AppShell>{user?.roles.some(r => ["hr_admin", "hr_ops"].includes(r)) ? <Records /> : <p role="alert">Recording leave is available to HR administrators and HR operations staff.</p>}</AppShell></AuthGate>;
}
async function workers() {
  const all: AttendanceEmployee[] = [];
  for (let page = 1; ; page++) {
    const data = await hrmApi.get<{ items: AttendanceEmployee[]; totalCount: number }>("/hrm/workers", { page, pageSize: 100 });
    all.push(...data.items); if (!data.items.length || all.length >= data.totalCount) break;
  }
  return all.filter(w => w.status !== "archived").sort((a,b) => a.fullName.localeCompare(b.fullName));
}
function Records() {
  const state = useApi(() => hrmApi.get<Leave[]>(api), []);
  const employees = useApi(workers, []);
  const types = useApi(async () => { const result = await realApi.leaveTypes({ includeInactive: true }); return (Array.isArray(result) ? result : (result as { items?: LeaveType[] }).items ?? []) as LeaveType[]; }, []);
  const [search, setSearch] = useState("");
  const [month, setMonth] = useState(todayLocal().slice(0,7));
  const [status, setStatus] = useState("");
  const [branch, setBranch] = useState("");
  const [form, setForm] = useState<{ row?: Leave; readOnly?: boolean } | null>(null);
  const [cancel, setCancel] = useState<Leave | null>(null);
  const branches = Array.from(new Map((employees.data ?? []).map(w => [w.locationId || "unassigned", w.locationName || "Unassigned"])).entries());
  return <>
    <PageHeader eyebrow="Time & attendance · HR administration" title="Recorded leave" description="Record leave already taken or already approved and in progress, without the employee request workflow." primaryAction={<Button onClick={() => setForm({})}>Record leave</Button>} />
    <p className="mb-4 text-sm text-muted-foreground">Entries are saved as approved and deducted from leave balances. Corrections and cancellations are audited. Payroll locks still apply.</p>
    <div className="mb-4 flex flex-wrap gap-4 text-sm"><Link className="text-primary underline" to="/hrm/leave/approvals">Employee request approvals</Link><Link className="text-primary underline" to="/hrm/configuration/leave-types">Configure leave types</Link><Link className="text-primary underline" to="/hrm/leave/allocations">Leave allocation</Link></div>
    <div className="mb-4 grid gap-3 rounded-lg border p-4 sm:grid-cols-2 lg:grid-cols-4">
      <label>Search employees<Input aria-label="Search recorded leave" value={search} onChange={e => setSearch(e.target.value)} placeholder="Name or employee number" /></label>
      <label>Month<Input aria-label="Leave month" type="month" value={month} onChange={e => setMonth(e.target.value)} /></label>
      <label>Branch<select aria-label="Branch" className="block h-9 w-full rounded-md border bg-background px-2" value={branch} onChange={e => setBranch(e.target.value)}><option value="">All branches</option>{branches.map(([id,name]) => <option key={id} value={id}>{name}</option>)}</select></label>
      <label>Status<select aria-label="Leave status" className="block h-9 w-full rounded-md border bg-background px-2" value={status} onChange={e => setStatus(e.target.value)}><option value="">All statuses</option><option value="approved">Approved</option><option value="cancelled">Cancelled</option></select></label>
    </div>
    <Async state={state}>{rows => {
      const visible = rows.filter(r => (!status || r.status === status) && (!month || (r.startDate.slice(0,7) <= month && r.endDate.slice(0,7) >= month)) && `${r.workerName} ${r.employeeNo}`.toLowerCase().includes(search.toLowerCase()) && (!branch || (employees.data?.find(w => w.id === r.workerId)?.locationId || "unassigned") === branch));
      return <div className="overflow-x-auto rounded-lg border"><RecordTable className="w-full min-w-[800px] text-left text-sm"><caption className="p-3 text-left">{visible.length} recorded leave entries</caption><thead><tr>{["Employee", "Leave type", "Dates", "Days", "Status", "Actions"].map(h => <th key={h} className="p-3" scope="col">{h}</th>)}</tr></thead><tbody>{visible.map(r => <tr key={r.id} className="border-t" data-record={r.id}><th className="p-3 font-normal" scope="row">{r.workerName}<span className="block text-xs text-muted-foreground">{r.employeeNo}</span></th><td className="p-3">{types.data?.find(t => t.code === r.leaveTypeCode)?.name || r.leaveTypeCode}</td><td className="p-3">{r.startDate} – {r.endDate}</td><td className="p-3">{r.days}</td><td className="p-3">{r.status}</td><td className="p-3"><div className="flex gap-2"><Button variant="outline" size="sm" onClick={() => setForm({ row:r, readOnly:true })}>View</Button>{r.status === "approved" && <><Button variant="outline" size="sm" onClick={() => setForm({ row:r })}>Edit</Button><Button variant="outline" size="sm" onClick={() => setCancel(r)}>Cancel record</Button></>}</div></td></tr>)}</tbody></RecordTable>{!visible.length && <p className="p-4">No recorded leave matches these filters.</p>}</div>;
    }}</Async>
    {form && <Editor row={form.row} readOnly={form.readOnly} employees={employees.data ?? []} types={(types.data ?? []).filter(t => t.isActive)} loading={employees.loading || types.loading} loadError={employees.error || types.error || (employees.degraded || types.degraded ? "Employee or leave type data could not be loaded." : "")} close={() => setForm(null)} saved={() => { state.reload(); setForm(null); toast.success("Approved leave recorded."); }} />}
    {cancel && <CancelRecord row={cancel} close={() => setCancel(null)} saved={() => { setCancel(null); state.reload(); toast.success("Leave cancelled and balance restored."); }} />}
  </>;
}
function Editor({ row, readOnly, employees, types, loading, loadError, close, saved }: { row?: Leave; readOnly?: boolean; employees: AttendanceEmployee[]; types: LeaveType[]; loading: boolean; loadError: string; close: () => void; saved: () => void }) {
  const [value, setValue] = useState<Form>(row ? { workerId:row.workerId, leaveTypeCode:row.leaveTypeCode, startDate:row.startDate, endDate:row.endDate, note:row.note, evidenceAttached:row.evidenceAttached } : { workerId:"", leaveTypeCode:"", startDate:todayLocal(), endDate:todayLocal(), note:"", evidenceAttached:false });
  const [search, setSearch] = useState(""); const [quote, setQuote] = useState<Quote | null>(null); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  function change<K extends keyof Form>(key: K, field: Form[K]) { setValue(v => ({ ...v, [key]:field })); setQuote(null); setError(""); }
  async function review() { setBusy(true); setError(""); try { setQuote(await hrmApi.post<Quote>(api+"/quote",value)); } catch(e) { setError(e instanceof Error ? e.message : "Unable to count leave days."); } finally { setBusy(false); } }
  async function submit(e: React.FormEvent) { e.preventDefault(); if(!quote)return; setBusy(true); setError(""); try { if(row)await hrmApi.put(api+"/"+row.id,value);else await hrmApi.post(api,value); saved(); } catch(e) {setError(e instanceof Error ? e.message : "Unable to record leave.");} finally {setBusy(false);} }
  const frozen = busy || readOnly;
  return <Dialog open onOpenChange={open => {if(!open && !busy)close();}}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl"><DialogHeader><DialogTitle>{readOnly ? "Recorded leave details" : row ? "Edit recorded leave" : "Record leave"}</DialogTitle><DialogDescription>{readOnly ? "Recorded by HR outside the employee request workflow." : "Record an already approved absence. This saves directly as approved and updates the employee’s leave balance."}</DialogDescription></DialogHeader>
    <form className="space-y-4" onSubmit={submit}>
      {!row && <Input aria-label="Find employee" placeholder="Search employee name or number" value={search} disabled={busy} onChange={e => setSearch(e.target.value)} />}
      <label className="block">Employee<select aria-label="Employee" required className="block h-10 w-full rounded-md border bg-background px-2" value={value.workerId} disabled={!!frozen || !!row || loading} onChange={e => change("workerId",e.target.value)}><option value="">Select employee</option>{employees.filter(w => w.id === value.workerId || `${w.fullName} ${w.employeeNo}`.toLowerCase().includes(search.toLowerCase())).map(w => <option key={w.id} value={w.id}>{w.fullName} · {w.employeeNo}</option>)}</select></label>
      <label className="block">Leave type<select aria-label="Leave type" required className="block h-10 w-full rounded-md border bg-background px-2" value={value.leaveTypeCode} disabled={!!frozen || loading} onChange={e => change("leaveTypeCode",e.target.value)}><option value="">Select leave type</option>{row && !types.some(t => t.code === row.leaveTypeCode) && <option value={row.leaveTypeCode}>{row.leaveTypeCode} (inactive)</option>}{types.map(t => <option key={t.code} value={t.code}>{t.name}</option>)}</select></label>
      {!readOnly && <p className="text-xs text-muted-foreground">If a type such as Study Leave is missing, configure it in Leave types first. Enter non-consecutive days as separate records.</p>}
      <div className="grid grid-cols-2 gap-3"><label>Start date<Input aria-label="Start date" type="date" required max={todayLocal()} value={value.startDate} disabled={!!frozen} onChange={e => change("startDate",e.target.value)} /></label><label>End date<Input aria-label="End date" type="date" required min={value.startDate} value={value.endDate} disabled={!!frozen} onChange={e => change("endDate",e.target.value)} /></label></div>
      {value.endDate > todayLocal() && <p className="text-sm">This records approved leave that is still in progress.</p>}
      <label className="block">Approval reference / reason<textarea aria-label="Approval reference or reason" required maxLength={1000} className="block min-h-20 w-full rounded-md border bg-background p-2" value={value.note} disabled={!!frozen} onChange={e => change("note",e.target.value)} /></label>
      <label className="flex gap-2"><input type="checkbox" checked={value.evidenceAttached} disabled={!!frozen} onChange={e => change("evidenceAttached",e.target.checked)} />Supporting evidence is on file</label>
      {(error || loadError) && <p role="alert" className="text-danger">{error || loadError}</p>}
      {readOnly && row && <p>{row.days} days · {row.status}</p>}
      {quote && <div role="status" className="space-y-1 rounded-md border p-3"><p className="font-semibold">{quote.days} leave days</p><p>Available balance before this entry: {quote.available + (row?.status === "approved" && row.leaveTypeCode === value.leaveTypeCode ? row.days : 0)} days</p><p className="text-sm">{quote.excludedDates.length ? `Excluded weekends/holidays: ${quote.excludedDates.join(", ")}` : "No weekends or holidays excluded."}</p></div>}
      <div className="flex flex-wrap justify-end gap-2"><Button type="button" variant="outline" onClick={close} disabled={busy}>{readOnly ? "Close" : "Cancel"}</Button>{!readOnly && <><Button type="button" variant="outline" onClick={review} disabled={busy || loading || !!loadError || !value.workerId || !value.leaveTypeCode}>Check days</Button><Button type="submit" disabled={busy || !quote || !!loadError}>{busy ? "Working…" : row ? "Save correction" : "Save approved leave"}</Button></>}</div>
    </form>
  </DialogContent></Dialog>;
}
function CancelRecord({ row, close, saved }: { row:Leave; close:()=>void; saved:()=>void }) {
  const [reason,setReason]=useState(""); const [busy,setBusy]=useState(false);const [error,setError]=useState("");
  async function cancel(e:React.FormEvent){e.preventDefault();setBusy(true);try{await hrmApi.post(api+"/"+row.id+"/cancel",{action:"cancel",reason});saved();}catch(e){setError(e instanceof Error?e.message:"Unable to cancel leave.");}finally{setBusy(false);}}
  return <Dialog open onOpenChange={open=>{if(!open&&!busy)close();}}><DialogContent><DialogHeader><DialogTitle>Cancel recorded leave</DialogTitle><DialogDescription>{row.workerName}: {row.startDate} to {row.endDate}. This restores {row.days} leave days and retains the record for audit.</DialogDescription></DialogHeader><form onSubmit={cancel} className="space-y-4"><Input aria-label="Cancellation reason" required maxLength={1000} placeholder="Reason for cancellation" value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} />{error&&<p role="alert" className="text-danger">{error}</p>}<div className="flex justify-end gap-2"><Button type="button" variant="outline" onClick={close} disabled={busy}>Keep record</Button><Button type="submit" disabled={busy||!reason.trim()}>Confirm cancellation</Button></div></form></DialogContent></Dialog>;
}
