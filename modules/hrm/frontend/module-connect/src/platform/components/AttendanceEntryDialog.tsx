import { FormSelect } from "@/components/ui/form-select";
import { useState } from "react";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { useApi } from "@/platform/use-api";
import { activeOn, attendanceEmployees, saveAttendance, timesError, todayLocal } from "@/platform/attendance-entry";

export function AttendanceEntryDialog({ onClose, onSaved, initialDate }: { onClose: () => void; onSaved: (date: string) => void; initialDate?: string }) {
  const employees = useApi(attendanceEmployees, []);
  const [date, setDate] = useState(initialDate || todayLocal());
  const [query, setQuery] = useState("");
  const [workerId, setWorkerId] = useState("");
  const [clockIn, setClockIn] = useState("");
  const [clockOut, setClockOut] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const eligible = (employees.data ?? []).filter(w => activeOn(w, date));
  const filtered = eligible.filter(w => w.id === workerId || `${w.fullName} ${w.employeeNo}`.toLowerCase().includes(query.toLowerCase()));
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const problem = timesError({ clockIn, clockOut });
    if (problem) { setError(problem); return; }
    setBusy(true); setError("");
    try { await saveAttendance(date, [{ workerId, clockIn, clockOut }]); onSaved(date); onClose(); }
    catch (e) { setError(e instanceof Error ? e.message : "Unable to save attendance."); }
    finally { setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }}><DialogContent className="sm:max-w-lg">
    <DialogHeader><DialogTitle>Add attendance</DialogTitle><DialogDescription>Record one employee’s clock-in and clock-out times. For an overnight shift, use the date the shift started.</DialogDescription></DialogHeader>
    <form onSubmit={submit} className="space-y-4">
      <label className="block space-y-1">Work date<Input type="date" aria-label="Work date" required max={todayLocal()} value={date} disabled={busy} onChange={e => { setDate(e.target.value); setWorkerId(""); }} /></label>
      <Input aria-label="Search employees" placeholder="Search name or employee number" value={query} disabled={busy} onChange={e => setQuery(e.target.value)} />
      <label className="block space-y-1">Employee<FormSelect aria-label="Employee" required className="block w-full rounded-md border bg-background p-2" value={workerId} disabled={busy || employees.loading} onChange={e => setWorkerId(e.target.value)}>
        <option value="">{employees.loading ? "Loading employees…" : "Select employee"}</option>{filtered.map(w => <option key={w.id} value={w.id}>{w.fullName} · {w.employeeNo}</option>)}
      </FormSelect></label>
      <div className="grid grid-cols-2 gap-3"><label>Clock in<Input aria-label="Clock in" type="time" required value={clockIn} disabled={busy} onChange={e => setClockIn(e.target.value)} /></label><label>Clock out<Input aria-label="Clock out" type="time" required value={clockOut} disabled={busy} onChange={e => setClockOut(e.target.value)} /></label></div>
      {clockIn && clockOut && clockOut < clockIn && <p className="text-sm">Clock-out is on the following day.</p>}
      {(error || employees.error || employees.degraded) && <p role="alert" className="text-danger">{error || employees.error || "Unable to load employees."}</p>}
      <p className="text-xs text-muted-foreground">Recorded attendance is changed through attendance corrections. Any overtime will need review.</p>
      <div className="flex justify-end gap-2"><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Cancel</Button><Button type="submit" disabled={busy || employees.loading || !workerId}>{busy ? "Saving…" : "Save attendance"}</Button></div>
    </form>
  </DialogContent></Dialog>;
}
