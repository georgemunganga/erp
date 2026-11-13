import { LiveAttendanceClock } from "@/platform/components/LiveAttendanceClock";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  Coffee,
  Info,
  LogIn,
  LogOut,
  MapPin,
  RefreshCw,
  WifiOff,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { timeclockApi } from "@/mock/timeclock";
import type { ClockState, DaySummary, PunchEvent, PunchKind, TodayRecord } from "@/mock/timeclock";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/attendance/clock")({
  head: () => ({
    meta: [
      { title: "Clock in and out — Kuubu HRM" },
      {
        name: "description",
        content:
          "Clock in, take your break and clock out, with today's punches, how each one was captured, and any day that still needs a correction.",
      },
      { property: "og:title", content: "Clock in and out — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Clock in, take your break and clock out, with today's punches, how each one was captured, and any day that still needs a correction.",
      },
    ],
  }),
  component: import.meta.env.VITE_USE_REAL_API === "true" ? LiveAttendanceClock : ClockPage,
});

const ME = "Chanda Mwansa-Chileshe";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
const DEFAULT_WORKER = "self";

/** Backend "HH:mm" → minutes past midnight. */
function hhmmToMinutes(t: unknown): number | null {
  const s = typeof t === "string" ? t : null;
  if (!s) return null;
  if (!/^\d{2}:\d{2}$/.test(s)) return null;
  const [h, mm] = s.split(":").map(Number);
  return h * 60 + mm;
}

/** Adapts the backend "today" attendance record into the mock TodayRecord shape. */
function adaptToday(raw: unknown, fallback: TodayRecord): TodayRecord {
  const r = raw as Record<string, unknown>;
  const clockInMin = hhmmToMinutes(r.clockIn);
  const clockOutMin = hhmmToMinutes(r.clockOut);
  const state: ClockState = clockInMin === null ? "out" : clockOutMin === null ? "in" : "out";
  const punches: PunchEvent[] = [];
  if (clockInMin !== null) {
    punches.push({
      id: `real-in-${String(r.id ?? "")}`,
      kind: "in",
      at: `${String(r.workDate ?? "")}T${String(r.clockIn)}:00+02:00`,
      localTime: String(r.clockIn),
      actor: String(r.workerId ?? DEFAULT_WORKER),
      event: "Clocked in",
      after: `captured as ${String(r.source ?? "manual")}`,
    });
  }
  if (clockOutMin !== null) {
    punches.push({
      id: `real-out-${String(r.id ?? "")}`,
      kind: "out",
      at: `${String(r.workDate ?? "")}T${String(r.clockOut)}:00+02:00`,
      localTime: String(r.clockOut),
      actor: String(r.workerId ?? DEFAULT_WORKER),
      event: "Clocked out",
      after: `captured as ${String(r.source ?? "manual")}`,
    });
  }
  return {
    ...fallback,
    state,
    punches,
    workedMinutesAtLoad: Number(r.totalHours ?? 0) * 60,
    nowMinutesAtLoad:
      clockInMin === null ? 0 : Math.max(clockInMin, clockOutMin ?? clockInMin),
    clockedInAt: clockInMin === null || clockOutMin !== null ? null : String(r.clockIn),
  };
}

const pad = (n: number) => String(n).padStart(2, "0");

/** Minutes past midnight rendered as a 24-hour clock time. */
function timeLabel(minutes: number) {
  const m = ((minutes % 1440) + 1440) % 1440;
  return `${pad(Math.floor(m / 60))}:${pad(m % 60)}`;
}

function duration(minutes: number) {
  const m = Math.max(0, Math.round(minutes));
  return `${Math.floor(m / 60)}h ${pad(m % 60)}m`;
}

function isoAt(date: string, minutes: number) {
  return `${date}T${timeLabel(minutes)}:00+02:00`;
}

const punchLabel: Record<PunchKind, string> = {
  in: "Clocked in",
  "break-start": "Break started",
  "break-end": "Break ended",
  out: "Clocked out",
};

/* --------------------------------------------------------------- today */

function TodayPanel({
  record,
  reload,
}: {
  record: TodayRecord;
  reload: () => void;
}) {
  const [state, setState] = useState<ClockState>(record.state);
  const [punches, setPunches] = useState<PunchEvent[]>(record.punches);
  const [offline, setOffline] = useState(false);
  const [lastAction, setLastAction] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Accumulated minutes outside the segment currently running.
  const [workedAccum, setWorkedAccum] = useState(0);
  const [breakAccum, setBreakAccum] = useState(record.breakMinutesTaken);
  const [segmentStart, setSegmentStart] = useState(
    record.nowMinutesAtLoad - record.workedMinutesAtLoad,
  );

  // The mock clock advances with real time so elapsed time is honest, not frozen.
  const mountedAt = useRef(Date.now());
  const [, forceTick] = useState(0);
  useEffect(() => {
    const t = window.setInterval(() => forceTick((n) => n + 1), 15000);
    return () => window.clearInterval(t);
  }, []);
  const now = record.nowMinutesAtLoad + Math.floor((Date.now() - mountedAt.current) / 60000);

  const worked = workedAccum + (state === "in" ? now - segmentStart : 0);
  const onBreak = breakAccum + (state === "break" ? now - segmentStart : 0);
  const queued = punches.filter((p) => p.queued);

  const addPunch = (kind: PunchKind, at: number) => {
    const punch: PunchEvent = {
      id: `p-${kind}-${at}-${Math.random().toString(36).slice(2, 7)}`,
      kind,
      at: isoAt(record.date, at),
      localTime: timeLabel(at),
      actor: ME,
      event: punchLabel[kind],
      after: record.source.label,
      queued: offline || undefined,
      reason: offline
        ? "Queued — will sync when you reconnect. The time shown is the time you pressed the button on this device."
        : undefined,
    };
    setPunches((p) => [...p, punch]);
  };

  const toggleClock = async () => {
    try {
      setBusy(true);
      if (USE_REAL) {
        if (state === "out") {
          await realApi.clockMyselfIn();
          setSegmentStart(now);
          setState("in");
          addPunch("in", now);
          setLastAction(`Clocked in at ${timeLabel(now)}.`);
          reload();
          return;
        }
        await realApi.clockMyselfOut();
        setWorkedAccum((w) => w + (now - segmentStart));
        setState("out");
        addPunch("out", now);
        setLastAction(`Clocked out at ${timeLabel(now)}.`);
        reload();
        return;
      }
      if (state === "out") {
        setSegmentStart(now);
        setState("in");
        addPunch("in", now);
        setLastAction(`Clocked in at ${timeLabel(now)}.`);
        return;
      }
      if (state === "break") {
        // Clocking out while on a break closes the break at the same minute.
        setBreakAccum((b) => b + (now - segmentStart));
        addPunch("break-end", now);
        addPunch("out", now);
        setState("out");
        setLastAction(`Break ended and clocked out at ${timeLabel(now)}.`);
        return;
      }
      setWorkedAccum((w) => w + (now - segmentStart));
      setState("out");
      addPunch("out", now);
      setLastAction(`Clocked out at ${timeLabel(now)}.`);
    } catch (e) {
      setLastAction(e instanceof Error ? e.message : "Failed to record punch.");
    } finally {
      setBusy(false);
    }
  };

  const toggleBreak = () => {
    if (state === "in") {
      setWorkedAccum((w) => w + (now - segmentStart));
      setSegmentStart(now);
      setState("break");
      addPunch("break-start", now);
      setLastAction(`Break started at ${timeLabel(now)}.`);
      return;
    }
    if (state === "break") {
      setBreakAccum((b) => b + (now - segmentStart));
      setSegmentStart(now);
      setState("in");
      addPunch("break-end", now);
      setLastAction(`Break ended at ${timeLabel(now)}.`);
    }
  };

  const sync = () => {
    setPunches((list) =>
      list.map((p) =>
        p.queued
          ? {
              ...p,
              queued: undefined,
              reason: `Recorded on your device at ${p.localTime}; reached the system at ${timeLabel(now)}. The recorded time is unchanged.`,
            }
          : p,
      ),
    );
    setLastAction(
      `${queued.length} queued punch${queued.length === 1 ? "" : "es"} sent. Recorded times were kept.`,
    );
  };

  const statusLine =
    state === "in"
      ? `Clocked in since ${record.clockedInAt ?? timeLabel(segmentStart)}`
      : state === "break"
        ? `On break since ${timeLabel(segmentStart)}`
        : punches.some((p) => p.kind === "out")
          ? `Clocked out at ${punches.filter((p) => p.kind === "out").slice(-1)[0]?.localTime}`
          : "Not clocked in yet";

  const StatusIcon = state === "in" ? CheckCircle2 : state === "break" ? Coffee : Clock;
  const statusTone =
    state === "in"
      ? "border-success/30 bg-success-soft text-success"
      : state === "break"
        ? "border-info/30 bg-info-soft text-info"
        : "border-border bg-muted text-muted-foreground";

  const breakLine =
    state === "break"
      ? `On break now — ${duration(onBreak)} taken of the ${record.shift.breakMinutes} unpaid minutes.`
      : onBreak > 0
        ? `${duration(onBreak)} of break taken of the ${record.shift.breakMinutes} unpaid minutes due.`
        : `Break not taken yet — ${record.shift.breakMinutes} unpaid minutes are due before ${record.shift.breakDueBy}.`;

  return (
    <section aria-label="Today" className="space-y-4">
      <div className="rounded-lg border bg-surface p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {record.dayLabel}
            </p>
            <p className="mt-2 flex flex-wrap items-center gap-2">
              <span
                className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${statusTone}`}
              >
                <StatusIcon className="size-3.5 shrink-0" aria-hidden />
                {statusLine}
              </span>
            </p>
            <p className="mt-3 text-3xl font-semibold tabular tracking-tight" aria-live="off">
              {duration(worked)}
            </p>
            <p className="text-sm text-muted-foreground">
              worked today of {record.shift.paidHours} scheduled hours
            </p>
          </div>

          <div className="flex w-full shrink-0 flex-col gap-2 sm:w-auto">
            <Button size="lg" className="h-14 w-full text-base sm:w-56" onClick={toggleClock} disabled={busy}>
              {state === "out" ? (
                <>
                  <LogIn className="size-5" aria-hidden /> {busy ? "Recording…" : "Clock in"}
                </>
              ) : (
                <>
                  <LogOut className="size-5" aria-hidden /> {busy ? "Recording…" : "Clock out"}
                </>
              )}
            </Button>
            <Button
              variant="outline"
              className="w-full sm:w-56"
              onClick={toggleBreak}
              disabled={state === "out"}
            >
              <Coffee className="size-4" aria-hidden />
              {state === "break" ? "End break" : "Start break"}
            </Button>
            {state === "out" ? (
              <p className="text-xs text-muted-foreground sm:w-56">
                Clock in before you can start a break.
              </p>
            ) : null}
          </div>
        </div>

        <p role="status" aria-live="polite" className="sr-only">
          {lastAction ?? statusLine}
        </p>

        <dl className="mt-5 grid gap-3 border-t pt-4 sm:grid-cols-2">
          <div>
            <dt className="text-xs text-muted-foreground">Scheduled shift today</dt>
            <dd className="text-sm font-medium">
              {record.shift.name} ·{" "}
              <span className="tabular">
                {record.shift.start}–{record.shift.end}
              </span>
            </dd>
            <dd className="text-xs text-muted-foreground">{record.shift.location}</dd>
          </div>
          <div>
            <dt className="text-xs text-muted-foreground">Break</dt>
            <dd className="text-sm font-medium">{breakLine}</dd>
          </div>
        </dl>
      </div>

      {/* How this punch is captured — stated, not hidden. */}
      <div className="rounded-lg border bg-surface-muted p-4">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <MapPin className="size-4 shrink-0" aria-hidden />
          Captured as {record.source.label}
        </h3>
        <ul className="mt-2 space-y-1.5 text-xs text-muted-foreground">
          <li>{record.source.device}</li>
          <li className="flex gap-2">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>{record.source.locationDetail}</span>
          </li>
          <li className="flex gap-2">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>{record.source.consentNote}</span>
          </li>
        </ul>

        <div className="mt-4 flex flex-wrap items-center gap-3 border-t pt-3">
          <Switch id="offline" checked={offline} onCheckedChange={setOffline} />
          <Label htmlFor="offline" className="text-xs font-medium">
            Simulate no connection (demonstration only)
          </Label>
        </div>
        {offline ? (
          <p className="mt-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
            <WifiOff className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            <span>
              You are shown as offline. Punches are still accepted — they are held on this device
              with the time you pressed the button and sent when you reconnect.
            </span>
          </p>
        ) : null}
      </div>

      {queued.length ? (
        <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-warning">
            <WifiOff className="size-4 shrink-0" aria-hidden />
            Queued — will sync when you reconnect
          </h3>
          <ul className="mt-2 space-y-1 text-xs text-warning">
            {queued.map((p) => (
              <li key={p.id}>
                {p.event} at <span className="tabular font-medium">{p.localTime}</span> — held on
                this device. This time is what gets recorded, not the time it arrives.
              </li>
            ))}
          </ul>
          <Button
            variant="outline"
            size="sm"
            className="mt-3"
            onClick={sync}
            disabled={offline}
            aria-describedby={offline ? "sync-hint" : undefined}
          >
            <RefreshCw className="size-4" aria-hidden />
            Send {queued.length} queued punch{queued.length === 1 ? "" : "es"}
          </Button>
          {offline ? (
            <p id="sync-hint" className="mt-2 text-xs text-warning">
              Turn the connection back on to send these.
            </p>
          ) : null}
        </div>
      ) : null}

      <div className="rounded-lg border bg-surface p-5">
        <StatusTimeline events={punches} title="Today's punches" />
        {punches.length === 1 ? (
          <p className="mt-3 text-xs text-muted-foreground">
            Break start, break end and clock out will appear here as you record them.
          </p>
        ) : null}
      </div>
    </section>
  );
}

/* ---------------------------------------------------------- last 7 days */

const hours = (h: number | null) => (h === null ? "Not known" : `${h.toFixed(1)} h`);

function difference(d: DaySummary) {
  if (d.actualHours === null) return "Cannot be worked out";
  if (d.scheduledHours === 0 && d.actualHours === 0) return "—";
  const diff = d.actualHours - d.scheduledHours;
  if (Math.abs(diff) < 0.05) return "On plan";
  return `${diff > 0 ? "+" : "−"}${Math.abs(diff).toFixed(1)} h`;
}

function DayStatus({ d }: { d: DaySummary }) {
  if (d.status === "Exception") {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
        <AlertTriangle className="size-3.5 shrink-0" aria-hidden />
        {d.exception?.kind ?? "Exception"}
      </span>
    );
  }
  if (d.status === "Resolved") {
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
        <CheckCircle2 className="size-3.5 shrink-0" aria-hidden />
        Resolved
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
      {d.status === "Rest day" ? (
        <Clock className="size-3.5 shrink-0" aria-hidden />
      ) : (
        <CheckCircle2 className="size-3.5 shrink-0" aria-hidden />
      )}
      {d.status}
    </span>
  );
}

function RecentDays({ rows, cutoff }: { rows: DaySummary[]; cutoff: string }) {
  const [view, setView] = useState("all");
  const open = useMemo(
    () => rows.filter((r) => r.status === "Exception" && !r.exception?.correctionRef),
    [rows],
  );

  return (
    <section aria-label="Last seven days" className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold">Last 7 days</h2>
          <p className="mt-1 max-w-2xl text-xs text-muted-foreground">
            Scheduled against actual hours. Anything flagged here is what payroll will use unless it
            is corrected first.
          </p>
        </div>
      </div>

      {open.length ? (
        <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            {open.length} day{open.length === 1 ? "" : "s"} still{" "}
            {open.length === 1 ? "needs" : "need"} a correction. The payroll cutoff is {cutoff} —
            after that, these days are paid on the recorded hours and any fix lands in the following
            pay run.
          </span>
        </p>
      ) : null}

      <ListPage<DaySummary>
        rows={rows.filter((r) => (view === "attention" ? r.status === "Exception" : true))}
        savedViews={[
          { id: "all", label: "All 7 days" },
          { id: "attention", label: "Needs attention" },
        ]}
        activeView={view}
        onViewChange={setView}
        searchPlaceholder="Search day or shift"
        searchFields={(r) => `${r.dayLabel} ${r.shift} ${r.status} ${r.exception?.kind ?? ""}`}
        filters={[
          {
            id: "status",
            label: "Status",
            options: ["Clean", "Exception", "Resolved", "Rest day"],
            match: (r, v) => r.status === v,
          },
        ]}
        columns={[
          {
            id: "day",
            header: "Day",
            cell: (r) => (
              <span className="block">
                <span className="block font-medium">{r.dayLabel}</span>
                <span className="block text-xs text-muted-foreground tabular">{r.date}</span>
              </span>
            ),
          },
          {
            id: "shift",
            header: "Shift",
            cell: (r) => <span className="block max-w-40">{r.shift}</span>,
          },
          {
            id: "recorded",
            header: "Recorded",
            cell: (r) => <span className="tabular">{r.recorded}</span>,
          },
          {
            id: "scheduled",
            header: "Scheduled",
            cell: (r) => <span className="tabular">{r.scheduledHours.toFixed(1)} h</span>,
          },
          {
            id: "actual",
            header: "Actual",
            cell: (r) => <span className="tabular">{hours(r.actualHours)}</span>,
          },
          {
            id: "difference",
            header: "Difference",
            cell: (r) => <span className="tabular">{difference(r)}</span>,
          },
          { id: "status", header: "Status", cell: (r) => <DayStatus d={r} /> },
          {
            id: "detail",
            header: "What happens next",
            defaultVisible: false,
            cell: (r) => (
              <span className="block max-w-80 text-xs text-muted-foreground">
                {r.exception?.consequence ?? r.resolvedNote ?? "Nothing to do."}
              </span>
            ),
          },
        ]}
        rowHref={(r) =>
          r.status === "Exception" ? (
            r.exception?.correctionRef ? (
              <span className="block max-w-48 text-right text-xs text-muted-foreground">
                <span className="font-mono">{r.exception.correctionRef}</span> ·{" "}
                {r.exception.correctionStatus} · {r.exception.correctionOwner} · due{" "}
                {r.exception.correctionDue}
              </span>
            ) : (
              <Link
                to="/hrm/attendance/new"
                className="whitespace-nowrap text-xs font-medium text-primary underline underline-offset-2"
              >
                Raise a correction
                <span className="sr-only"> for {r.dayLabel}</span>
              </Link>
            )
          ) : null
        }
        emptyBody="No days match the current view."
      />

      <ul className="space-y-2">
        {rows
          .filter((r) => r.exception)
          .map((r) => (
            <li
              key={r.id}
              className="rounded-lg border border-warning/40 bg-warning-soft p-3 text-xs text-warning"
            >
              <p className="flex gap-2 font-medium">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                <span>
                  {r.dayLabel} — {r.exception?.kind}
                </span>
              </p>
              <p className="mt-1 pl-5.5">{r.exception?.detail}</p>
              <p className="mt-1 pl-5.5">{r.exception?.consequence}</p>
            </li>
          ))}
      </ul>
    </section>
  );
}

/* ------------------------------------------------------------------ page */

function ClockPage() {
  const mockToday = useMemo(() => timeclockApi.today(), []);
  const today = useApi(
    async () =>
      USE_REAL
        ? adaptToday(await realApi.myAttendanceToday(), await mockToday)
        : await mockToday,
    [],
  );
  const recent = useMock(() => timeclockApi.recent());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Attendance"
        title="Clock in and out"
        description="Record your own time. Every punch shows how it was captured, and any day that does not add up is flagged here before payroll sees it."
        meta={
          <>
            <span className="inline-flex items-center gap-1.5 rounded-full border border-info/30 bg-info-soft px-2.5 py-0.5 text-xs font-medium text-info">
              <MapPin className="size-3.5" aria-hidden />
              Web · Lusaka HQ (on-site network)
            </span>
            <Link
              to="/hrm/attendance"
              className="text-xs font-medium text-primary underline underline-offset-2"
            >
              View my corrections
            </Link>
          </>
        }
      />

      <Async state={today} rows={3}>
        {(record) => <TodayPanel record={record} reload={today.reload} />}
      </Async>

      <Async state={recent} rows={4}>
        {(rows) => <RecentDays rows={rows} cutoff="17:00 on 5 August 2026" />}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
