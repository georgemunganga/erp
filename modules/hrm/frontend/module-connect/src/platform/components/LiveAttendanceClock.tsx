import { RecordTable } from "./RecordTable";
import { useState } from "react";
import { Link } from "@tanstack/react-router";
import { Button } from "@/components/ui/button";
import { hrmApi } from "@/platform/api-client";
import { useAuth } from "@/platform/auth";
import { realApi, useApi } from "@/platform/use-api";
import { Async } from "./Async";
import { AppShell } from "./AppShell";
import { AuthGate } from "./AuthGate";
import { PageHeader } from "./PageHeader";
import { AttendanceEntryDialog } from "./AttendanceEntryDialog";

type Attendance = { id: string; workDate: string; clockIn: string | null; clockOut: string | null; totalHours: number; derivedStatus: string };
type Identity = { linked: boolean; worker: { fullName: string; employeeNo: string } | null };
export function LiveAttendanceClock() {
  const identity = useApi(() => hrmApi.get<Identity>("/hrm/me/"), []);
  const { user } = useAuth();
  const canManage = user?.roles.some(r => ["hr_admin", "hr_ops"].includes(r));
  const [entry, setEntry] = useState(false);
  return <AuthGate><AppShell>
    <PageHeader eyebrow="Attendance" title="Clock in and out" description="Record your own attendance and review your recent clock-in and clock-out times."
      primaryAction={canManage ? <div className="flex flex-wrap gap-2"><Button onClick={() => setEntry(true)}>Add attendance</Button><Button asChild variant="outline"><Link to="/hrm/time/attendance/bulk">Add bulk attendance</Link></Button></div> : undefined} />
    <Async state={identity}>{data => data.linked && data.worker
      ? <OwnClock name={data.worker.fullName} employeeNo={data.worker.employeeNo} />
      : <section className="space-y-3 rounded-lg border bg-card p-5" aria-label="Employee account link">
        <h2 className="text-lg font-semibold">Employee record needed for personal clock-in</h2>
        <p>Your login is not linked to an employee record. Ask HR to link it to your existing employee profile to record your own time.</p>
        {canManage && <p>You can still record attendance for employees using Add attendance or Add bulk attendance above.</p>}
        {canManage && <Button asChild variant="outline"><Link to="/hrm/time/timesheets">View timesheets</Link></Button>}
      </section>}
    </Async>
    {entry && <AttendanceEntryDialog onClose={() => setEntry(false)} onSaved={() => identity.reload()} />}
  </AppShell></AuthGate>;
}
function OwnClock({ name, employeeNo }: { name: string; employeeNo: string }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const attendance = useApi(async () => {
    const now = new Date(); const to = now.toISOString().slice(0, 10);
    now.setUTCDate(now.getUTCDate() - 6);
    const rows = await realApi.myAttendance({ from: now.toISOString().slice(0, 10), to }) as Attendance[];
    return { date: to, rows: [...rows].sort((a, b) => b.workDate.localeCompare(a.workDate)) };
  }, []);
  async function punch(clockIn: boolean) {
    setBusy(true); setError(""); setNotice("");
    try {
      if (clockIn) await realApi.clockMyselfIn(); else await realApi.clockMyselfOut();
      setNotice(clockIn ? "Clock-in recorded." : "Clock-out recorded."); attendance.reload();
    } catch (e) { setError(e instanceof Error ? e.message : "Unable to record attendance. Please try again."); }
    finally { setBusy(false); }
  }
  return <div className="space-y-4">
    <p className="font-medium">{name} · {employeeNo}</p>
    {error && <p role="alert" className="text-danger">{error}</p>}
    {notice && <p role="status">{notice}</p>}
    <Async state={attendance}>{({ date, rows }) => {
      const today = rows.find(r => r.workDate === date);
      const finished = !!today?.clockOut; const started = !!today?.clockIn;
      return <>
        <section aria-label="Today" className="space-y-3 rounded-lg border bg-card p-5">
          <h2 className="font-semibold">Today · {date}</h2>
          <p>{finished ? "Clocked out" : started ? "Clocked in" : "Not clocked in yet"}</p>
          <p>Clock in: {today?.clockIn?.slice(0, 5) || "—"} · Clock out: {today?.clockOut?.slice(0, 5) || "—"}</p>
          {finished && <p>Recorded hours: {today?.totalHours.toFixed(2)}</p>}
          <Button disabled={busy || finished} onClick={() => punch(!started)}>{busy ? "Recording…" : finished ? "Attendance complete" : started ? "Clock out" : "Clock in"}</Button>
          <p className="text-sm text-muted-foreground">Times are shown in UTC, as recorded by the attendance system.</p>
          <Link to="/hrm/attendance/new" className="text-sm text-primary underline">Raise a correction</Link>
        </section>
        <section aria-label="Last seven days" className="space-y-3">
          <h2 className="font-semibold">Last 7 days</h2>
          {!rows.length ? <p>No attendance recorded in the last seven days.</p> : <div className="overflow-x-auto rounded-lg border"><RecordTable className="w-full text-left text-sm">
            <thead><tr>{["Date", "Clock in (UTC)", "Clock out (UTC)", "Recorded hours", "Status"].map(h => <th key={h} scope="col" className="p-3">{h}</th>)}</tr></thead>
            <tbody>{rows.map(r => <tr key={r.id} className="border-t"><th scope="row" className="p-3 font-normal">{r.workDate}</th><td className="p-3">{r.clockIn?.slice(0, 5) || "—"}</td><td className="p-3">{r.clockOut?.slice(0, 5) || "—"}</td><td className="p-3">{r.clockIn && r.clockOut ? r.totalHours.toFixed(2) : "—"}</td><td className="p-3">{r.derivedStatus}</td></tr>)}</tbody>
          </RecordTable></div>}
        </section>
      </>;
    }}</Async>
  </div>;
}
