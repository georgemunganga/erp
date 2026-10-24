import { FormSelect } from "@/components/ui/form-select";
import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { AttendanceEntryDialog } from "@/platform/components/AttendanceEntryDialog";
import { activeOn, attendanceEmployees, saveAttendance, timesError, todayLocal, type AttendanceTimes, type SavedAttendance } from "@/platform/attendance-entry";
import { hrmApi } from "@/platform/api-client";
import { useAuth } from "@/platform/auth";
import { useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/attendance/bulk")({ head: () => ({ meta: [{ title: "Add bulk attendance — HR workspace" }] }), component: BulkAttendance });

function BulkAttendance() {
  const { user } = useAuth();
  const allowed = user?.roles.some(r => ["hr_ops", "hr_admin", "timesheet_operator"].includes(r));
  return <AuthGate><AppShell>{allowed ? <EntryTable /> : <p role="alert">Attendance entry requires timesheet or HR operations access.</p>}</AppShell></AuthGate>;
}
function EntryTable() {
  const [date, setDate] = useState(todayLocal);
  const [branch, setBranch] = useState("");
  const [department, setDepartment] = useState("");
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("");
  const [recordFilter, setRecordFilter] = useState("");
  const [drafts, setDrafts] = useState<Record<string, Record<string, AttendanceTimes>>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [dialog, setDialog] = useState(false);
  const employees = useApi(attendanceEmployees, []);
  const attendance = useApi(() => hrmApi.get<SavedAttendance[]>("/hrm/time/attendance", { from: date, to: date }), [date]);
  const records = useMemo(() => new Map((attendance.data ?? []).map(a => [a.workerId, a])), [attendance.data]);
  const workers = (employees.data ?? []).filter(w => activeOn(w, date));
  const branches = Array.from(new Map(workers.map(w => [w.locationId || "unassigned", w.locationName || "Unassigned"])).entries());
  const departments = Array.from(new Map(workers.filter(w => !branch || (w.locationId || "unassigned") === branch).map(w => [w.orgUnitId || "unassigned", w.orgUnitName || "Unassigned"])).entries());
  const visible = workers.filter(w => (!branch || (w.locationId || "unassigned") === branch) && (!department || (w.orgUnitId || "unassigned") === department)
    && (!status || w.status === status) && `${w.fullName} ${w.employeeNo}`.toLowerCase().includes(query.toLowerCase())
    && (!recordFilter || (recordFilter === "recorded" ? records.has(w.id) : !records.has(w.id))));
  const current = drafts[date] ?? {};
  const hasTimes = (v: AttendanceTimes) => !!(v.clockIn || v.clockOut);
  const filled = visible.filter(w => !records.has(w.id) && current[w.id] && hasTimes(current[w.id]));
  const hidden = Object.entries(current).filter(([id, v]) => hasTimes(v) && !records.has(id) && !visible.some(w => w.id === id)).length;
  const otherDates = Object.entries(drafts).filter(([d, rows]) => d !== date && Object.values(rows).some(hasTimes)).length;
  const loading = employees.loading || attendance.loading;
  const loadError = employees.error || attendance.error || (employees.degraded || attendance.degraded ? "Attendance data could not be loaded." : "");
  const dirty = Object.values(drafts).some(rows => Object.values(rows).some(hasTimes));
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = ""; };
    window.addEventListener("beforeunload", warn); return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);
  function change(id: string, field: keyof AttendanceTimes, value: string) {
    setDrafts(prev => ({ ...prev, [date]: { ...prev[date], [id]: { ...(prev[date]?.[id] ?? { clockIn: "", clockOut: "" }), [field]: value } } }));
    setError("");
  }
  async function save() {
    const invalid = filled.find(w => timesError(current[w.id]));
    if (invalid) { setError(`${invalid.employeeNo}: ${timesError(current[invalid.id])}`); return; }
    setBusy(true); setError("");
    try {
      const saved = await saveAttendance(date, filled.map(w => ({ workerId: w.id, ...current[w.id] })));
      setDrafts(prev => { const next = { ...prev[date] }; for (const w of filled) delete next[w.id]; return { ...prev, [date]: next }; });
      toast.success(`${saved.length} attendance record${saved.length === 1 ? "" : "s"} saved.`); attendance.reload();
    } catch (e) { setError(e instanceof Error ? e.message : "Unable to save attendance."); }
    finally { setBusy(false); }
  }
  return <>
    <PageHeader eyebrow="Time & attendance" title="Add bulk attendance" description="Choose a date, filter the employee list, and enter clock-in and clock-out times in the table." primaryAction={<Button onClick={() => setDialog(true)} disabled={busy}>Add attendance</Button>} />
    <div className="space-y-4" data-testid="bulk-attendance-page">
      <div className="flex flex-wrap gap-3 text-sm"><Link className="text-primary underline" to="/hrm/time/timesheets">Back to timesheets</Link><Link className="text-primary underline" to="/hrm/time/attendance/import">Import spreadsheet</Link></div>
      <div className="grid gap-3 rounded-lg border bg-card p-4 sm:grid-cols-2 lg:grid-cols-3">
        <label>Work date<Input aria-label="Work date" type="date" required max={todayLocal()} value={date} disabled={busy} onChange={e => { if (e.target.value) { setDate(e.target.value); setError(""); } }} /></label>
        <label>Branch<FormSelect aria-label="Branch" className="block h-10 w-full rounded-md border bg-background px-2" value={branch} disabled={busy} onChange={e => { setBranch(e.target.value); setDepartment(""); }}><option value="">All branches</option>{branches.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</FormSelect></label>
        <label>Department<FormSelect aria-label="Department" className="block h-10 w-full rounded-md border bg-background px-2" value={department} disabled={busy} onChange={e => setDepartment(e.target.value)}><option value="">All departments</option>{departments.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</FormSelect></label>
        <label>Search employees<Input aria-label="Search employees" placeholder="Name or employee number" value={query} disabled={busy} onChange={e => setQuery(e.target.value)} /></label>
        <label>Employee status<FormSelect aria-label="Employee status" className="block h-10 w-full rounded-md border bg-background px-2" value={status} disabled={busy} onChange={e => setStatus(e.target.value)}><option value="">All active statuses</option><option value="active">Active</option><option value="on-leave">On leave</option><option value="notice">Notice period</option></FormSelect></label>
        <label>Attendance<FormSelect aria-label="Attendance filter" className="block h-10 w-full rounded-md border bg-background px-2" value={recordFilter} disabled={busy} onChange={e => setRecordFilter(e.target.value)}><option value="">All employees</option><option value="missing">Not recorded</option><option value="recorded">Recorded</option></FormSelect></label>
      </div>
      <p className="text-sm text-muted-foreground">Use the date the shift started. A clock-out earlier than clock-in means the following day. Existing records are shown for reference; use attendance corrections to change them.</p>
      {loading && <p role="status">Loading attendance…</p>}
      {(error || loadError) && <div role="alert" className="rounded-md border border-danger p-3 text-danger">{error || loadError}{loadError && <Button variant="outline" onClick={() => { employees.reload(); attendance.reload(); }}>Retry</Button>}</div>}
      <div className="overflow-x-auto rounded-lg border" aria-busy={loading}>
        <RecordTable className="w-full min-w-[850px] text-left text-sm"><caption className="p-3 text-left font-medium">{visible.length} employees · {date}</caption>
          <thead className="bg-muted"><tr>{["Employee", "Branch", "Department", "Clock in", "Clock out", "Attendance"].map(h => <th key={h} className="p-3" scope="col">{h}</th>)}</tr></thead>
          <tbody>{visible.map(w => {
            const recorded = records.get(w.id); const times = current[w.id] ?? { clockIn: "", clockOut: "" };
            return <tr key={w.id} className="border-t" data-employee={w.employeeNo}>
              <th scope="row" className="p-3 font-normal"><span className="block font-medium">{w.fullName}</span><span className="text-xs text-muted-foreground">{w.employeeNo}</span></th>
              <td className="p-3">{w.locationName || "Unassigned"}</td><td className="p-3">{w.orgUnitName || "Unassigned"}</td>
              <td className="p-3"><Input aria-label={`Clock in for ${w.employeeNo}`} type="time" value={recorded ? (recorded.clockIn ?? "").slice(0,5) : times.clockIn} disabled={!!recorded || loading || !!loadError || busy} onChange={e => change(w.id, "clockIn", e.target.value)} /></td>
              <td className="p-3"><Input aria-label={`Clock out for ${w.employeeNo}`} type="time" value={recorded ? (recorded.clockOut ?? "").slice(0,5) : times.clockOut} disabled={!!recorded || loading || !!loadError || busy} onChange={e => change(w.id, "clockOut", e.target.value)} /></td>
              <td className="p-3">{recorded ? `Recorded · ${recorded.totalHours.toFixed(2)} hours` : times.clockIn && times.clockOut && times.clockOut < times.clockIn ? "Next-day clock-out" : "Not recorded"}</td>
            </tr>;
          })}</tbody>
        </RecordTable>{!loading && !visible.length && <p className="p-4">No employees match these filters.</p>}
      </div>
      <div className="sticky bottom-0 flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-background p-4">
        <div><p>{filled.length} filled row{filled.length === 1 ? "" : "s"} in this view</p>{hidden > 0 && <p className="text-sm text-muted-foreground">{hidden} other draft rows are hidden by filters and will not be saved.</p>}{otherDates > 0 && <p className="text-sm text-muted-foreground">Unsaved entries are also held for {otherDates} other date(s) in this page.</p>}</div>
        <Button onClick={save} disabled={busy || loading || !!loadError || !filled.length || filled.length > 500}>{busy ? "Saving…" : `Save ${filled.length} attendance record${filled.length === 1 ? "" : "s"}`}</Button>
      </div>
    </div>
    {dialog && <AttendanceEntryDialog initialDate={date} onClose={() => setDialog(false)} onSaved={() => { attendance.reload(); toast.success("Attendance saved."); }} />}
  </>;
}
