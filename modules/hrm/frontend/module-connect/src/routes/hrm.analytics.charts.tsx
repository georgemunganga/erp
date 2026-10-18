import { RecordTable } from "@/platform/components/RecordTable";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Briefcase, Building2, CalendarClock, Clock4, GraduationCap, Users } from "lucide-react";

interface Workforce {
  activeCount: number;
  preHireCount: number;
  archivedCount: number;
  monthlyTrend: { month: string; activeCount: number; joined: number; left: number }[];
  turnoverRatePct: number;
}
interface LeavePanel {
  byType: { leaveType: string; requestedDays: number; approvedDays: number; requests: number; approved: number }[];
  approvalRatePct: number;
}
interface PayrollRun {
  periodLabel: string;
  status: string;
  totalGross: number;
  totalDeductions: number;
  totalNet: number;
  totalEmployerCost: number;
  employeeCount: number;
}
interface PayrollPanel {
  runs: PayrollRun[];
  grossTotalLast6: number;
  employerCostTotalLast6: number;
}
interface PerformancePanel {
  byRating: { rating: string; count: number }[];
  cycles: number;
  assessments: number;
  finalized: number;
  completionRatePct: number;
}
interface RecruitmentPanel {
  openRequisitions: number;
  openVacancies: number;
  candidatesInPipeline: number;
  offersPending: number;
  hired: number;
  stageFunnel: { stage: string; count: number }[];
}
interface AttendancePanel {
  byStatus: { derivedStatus: string; days: number }[];
  averageDailyHours: number;
  totalOvertimeHours: number;
}
export interface Dashboard {
  asAt: string;
  workforce: Workforce;
  leave: LeavePanel;
  payroll: PayrollPanel;
  performance: PerformancePanel;
  recruitment: RecruitmentPanel;
  attendance: AttendancePanel;
}

/* ------------------------------------------------------------------ */

const fmt = new Intl.NumberFormat("en-GB");
const fmtDec = new Intl.NumberFormat("en-GB", { maximumFractionDigits: 1 });

function fmtMoney(value: number) {
  if (!Number.isFinite(value)) return "—";
  return `K${fmt.format(Math.round(value))}`;
}

const RATING_COLORS: Record<string, string> = {
  "1": "#b91c1c",
  "2": "#f97316",
  "3": "#eab308",
  "4": "#65a30d",
  "5": "#15803d",
};
function ratingColor(rating: string) {
  return RATING_COLORS[rating] ?? "#64748b";
}

function KpiCard({
  icon,
  label,
  value,
  sub,
  tone = "neutral",
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  sub?: string;
  tone?: "neutral" | "good" | "warn";
}) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        <span className="text-muted-foreground/70">{icon}</span>
        {label}
      </div>
      <p className={`mt-2 text-2xl font-bold ${tone === "good" ? "text-green-700" : tone === "warn" ? "text-amber-700" : "text-foreground"}`}>
        {value}
      </p>
      {sub ? <p className="mt-1 text-xs text-muted-foreground">{sub}</p> : null}
    </div>
  );
}

function PanelCard({
  title,
  children,
  empty,
}: {
  title: string;
  children: React.ReactNode;
  empty?: string;
}) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <p className="text-sm font-semibold text-foreground">{title}</p>
      <div className="mt-3">{children}</div>
      {empty ? <p className="text-xs text-muted-foreground">{empty}</p> : null}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Panels                                                               */
/* ------------------------------------------------------------------ */

function WorkforcePanel({ wf }: { wf: Workforce }) {
  const trend = wf.monthlyTrend ?? [];
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <KpiCard icon={<Users className="h-4 w-4" />} label="Active workers" value={fmt.format(wf.activeCount)} />
        <KpiCard icon={<GraduationCap className="h-4 w-4" />} label="Pre-hire" value={fmt.format(wf.preHireCount)} />
        <KpiCard icon={<Building2 className="h-4 w-4" />} label="Archived" value={fmt.format(wf.archivedCount)} />
        <KpiCard
          icon={<Briefcase className="h-4 w-4" />}
          label="Turnover (annualised)"
          value={`${fmtDec.format(wf.turnoverRatePct)}%`}
          sub="Leavers over trailing 12 months ÷ average headcount"
        />
      </div>
      <PanelCard title="Headcount trend — trailing 12 months" empty={!trend.length ? "No monthly headcount data yet." : undefined}>
        {trend.length ? (
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={trend} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Line type="monotone" dataKey="activeCount" name="Active" stroke="#2563eb" strokeWidth={2} />
                <Line type="monotone" dataKey="joined" name="Joined" stroke="#16a34a" strokeWidth={2} />
                <Line type="monotone" dataKey="left" name="Left" stroke="#dc2626" strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        ) : null}
      </PanelCard>
    </div>
  );
}

function LeavePanelView({ leave }: { leave: LeavePanel }) {
  const rows = leave.byType ?? [];
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <KpiCard
          icon={<CalendarClock className="h-4 w-4" />}
          label="Requests"
          value={fmt.format(rows.reduce((s, r) => s + r.requests, 0))}
          sub={`${fmt.format(rows.reduce((s, r) => s + r.approved, 0))} approved`}
        />
        <KpiCard
          icon={<Clock4 className="h-4 w-4" />}
          label="Requested days"
          value={fmtDec.format(rows.reduce((s, r) => s + r.requestedDays, 0))}
        />
        <KpiCard
          icon={<CalendarClock className="h-4 w-4" />}
          label="Approved days"
          value={fmtDec.format(rows.reduce((s, r) => s + r.approvedDays, 0))}
        />
        <KpiCard
          icon={<Briefcase className="h-4 w-4" />}
          label="Approval rate"
          value={`${fmtDec.format(leave.approvalRatePct)}%`}
        />
      </div>
      <PanelCard title="Leave taken by type (days)" empty={!rows.length ? "No leave requests recorded yet." : undefined}>
        {rows.length ? (
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={rows} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="leaveType" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="requestedDays" name="Requested" fill="#94a3b8" />
                <Bar dataKey="approvedDays" name="Approved" fill="#2563eb" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : null}
      </PanelCard>
    </div>
  );
}

function PayrollPanelView({ payroll }: { payroll: PayrollPanel }) {
  const runs = payroll.runs ?? [];
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-2">
        <KpiCard icon={<Briefcase className="h-4 w-4" />} label="Gross — last 6 runs" value={fmtMoney(payroll.grossTotalLast6)} />
        <KpiCard
          icon={<Building2 className="h-4 w-4" />}
          label="Employer cost — last 6 runs"
          value={fmtMoney(payroll.employerCostTotalLast6)}
        />
      </div>
      <PanelCard title="Payroll cost per run (last 6 runs)" empty={!runs.length ? "No completed payroll runs yet." : undefined}>
        {runs.length ? (
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={runs} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="periodLabel" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip formatter={(v: number) => fmtMoney(v)} />
                <Bar dataKey="totalEmployerCost" name="Employer cost" fill="#7c3aed" />
                <Bar dataKey="totalGross" name="Gross" fill="#0ea5e9" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : null}
      </PanelCard>
      {runs.length ? (
        <div className="overflow-x-auto rounded-md border">
          <RecordTable variant="compact" className="w-full text-xs">
            <thead>
              <tr className="border-b bg-muted/50 text-left">
                <th className="p-2 font-semibold">Period</th>
                <th className="p-2 font-semibold">Status</th>
                <th className="p-2 text-right font-semibold">Employees</th>
                <th className="p-2 text-right font-semibold">Gross</th>
                <th className="p-2 text-right font-semibold">Deductions</th>
                <th className="p-2 text-right font-semibold">Net</th>
                <th className="p-2 text-right font-semibold">Employer cost</th>
              </tr>
            </thead>
            <tbody>
              {runs.map((r) => (
                <tr key={r.periodLabel} className="border-b last:border-0">
                  <td className="p-2 font-medium">{r.periodLabel}</td>
                  <td className="p-2 capitalize">{r.status}</td>
                  <td className="p-2 text-right">{fmt.format(r.employeeCount)}</td>
                  <td className="p-2 text-right">{fmtMoney(r.totalGross)}</td>
                  <td className="p-2 text-right">{fmtMoney(r.totalDeductions)}</td>
                  <td className="p-2 text-right">{fmtMoney(r.totalNet)}</td>
                  <td className="p-2 text-right">{fmtMoney(r.totalEmployerCost)}</td>
                </tr>
              ))}
            </tbody>
          </RecordTable>
        </div>
      ) : null}
    </div>
  );
}

function PerformancePanelView({ perf }: { perf: PerformancePanel }) {
  const rows = perf.byRating ?? [];
  const empty = !rows.length || !perf.cycles;
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <KpiCard icon={<Briefcase className="h-4 w-4" />} label="Cycles" value={fmt.format(perf.cycles)} />
        <KpiCard icon={<Users className="h-4 w-4" />} label="Assessments" value={fmt.format(perf.assessments)} />
        <KpiCard icon={<GraduationCap className="h-4 w-4" />} label="Finalized" value={fmt.format(perf.finalized)} />
        <KpiCard
          icon={<CalendarClock className="h-4 w-4" />}
          label="Completion rate"
          value={`${fmtDec.format(perf.completionRatePct)}%`}
        />
      </div>
      <PanelCard
        title="Assessment distribution by final rating"
        empty={empty ? "No finalized assessments yet — ratings populate as cycles close." : undefined}>
        {rows.length ? (
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={rows} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="rating" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="count" name="Count" fill="#64748b">
                  {rows.map((row) => (
                    <Cell key={row.rating} fill={ratingColor(row.rating)} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : null}
      </PanelCard>
    </div>
  );
}

function RecruitmentPanelView({ rec }: { rec: RecruitmentPanel }) {
  const funnel = rec.stageFunnel ?? [];
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">
        <KpiCard icon={<Briefcase className="h-4 w-4" />} label="Open requisitions" value={fmt.format(rec.openRequisitions)} />
        <KpiCard icon={<Building2 className="h-4 w-4" />} label="Open vacancies" value={fmt.format(rec.openVacancies)} />
        <KpiCard icon={<Users className="h-4 w-4" />} label="In pipeline" value={fmt.format(rec.candidatesInPipeline)} />
        <KpiCard icon={<CalendarClock className="h-4 w-4" />} label="Offers pending" value={fmt.format(rec.offersPending)} />
        <KpiCard icon={<GraduationCap className="h-4 w-4" />} label="Hired" value={fmt.format(rec.hired)} tone="good" />
      </div>
      <PanelCard title="Candidate funnel by stage" empty={!funnel.length ? "No candidates recorded yet." : undefined}>
        {funnel.length ? (
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={funnel} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="stage" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="count" name="Candidates" fill="#0891b2" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : null}
      </PanelCard>
    </div>
  );
}

function AttendancePanelView({ att }: { att: AttendancePanel }) {
  const rows = att.byStatus ?? [];
  const empty = !rows.length;
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <KpiCard
          icon={<Clock4 className="h-4 w-4" />}
          label="Avg daily hours"
          value={fmtDec.format(att.averageDailyHours)}
          sub="Trailing 30 days"
        />
        <KpiCard
          icon={<CalendarClock className="h-4 w-4" />}
          label="Overtime hours"
          value={fmtDec.format(att.totalOvertimeHours)}
          sub="Trailing 30 days"
        />
        <KpiCard icon={<Users className="h-4 w-4" />} label="Status splits" value={fmt.format(rows.length)} sub="Presence statuses recorded" />
      </div>
      <PanelCard title="Attendance by status (trailing 30 days)" empty={empty ? "No attendance records yet." : undefined}>
        {rows.length ? (
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={rows} margin={{ top: 4, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="derivedStatus" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="days" name="Days" fill="#059669" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        ) : null}
      </PanelCard>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Charts root                                                          */
/* ------------------------------------------------------------------ */

export function Charts({ data }: { data: Dashboard }) {
  return (
    <>
      <WorkforcePanel wf={data.workforce} />
      <LeavePanelView leave={data.leave} />
      <PayrollPanelView payroll={data.payroll} />
      <PerformancePanelView perf={data.performance} />
      <RecruitmentPanelView rec={data.recruitment} />
      <AttendancePanelView att={data.attendance} />
    </>
  );
}
