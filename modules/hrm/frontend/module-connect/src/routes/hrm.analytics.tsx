import { createFileRoute } from "@tanstack/react-router";
import { Component, Suspense, lazy, useEffect, useState } from "react";
import {
  AlertTriangle,
  Briefcase,
  Building2,
  CalendarClock,
  Clock4,
  GraduationCap,
  Users,
} from "lucide-react";
import { hrmApi } from "@/platform/api-client";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/analytics")({
  head: () => ({
    meta: [
      { title: "HR analytics — Kuubu HRM" },
      {
        name: "description",
        content:
          "HR analytics dashboard — workforce headcount and trend, leave utilisation, payroll cost, performance ratings, recruitment funnel and attendance for the trailing period.",
      },
    ],
  }),
  component: AnalyticsPage,
});

/* ------------------------------------------------------------------ */
/* Backend payload shapes (M40 DashboardDto)                           */
/* ------------------------------------------------------------------ */

interface Workforce {
  activeCount: number;
  preHireCount: number;
  archivedCount: number;
  averageTenureYears: number;
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
  payDate: string | null;
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
interface Dashboard {
  asAt: string;
  workforce: Workforce;
  leave: LeavePanel;
  payroll: PayrollPanel;
  performance: PerformancePanel;
  recruitment: RecruitmentPanel;
  attendance: AttendancePanel;
}

/* ------------------------------------------------------------------ */
/* Lazy chart bundle — recharts is only ever imported AFTER mount, so
   it never participates in SSR/hydration and cannot crash the page.  */
/* ------------------------------------------------------------------ */

const Charts = lazy(
  async () => {
    const mod = await import(/* webpackChunkName: "hrm-analytics-charts" */ "@/routes/hrm.analytics.charts");
    return { default: mod.Charts };
  },
);

/* ------------------------------------------------------------------ */
/* Small display helpers                                                */
/* ------------------------------------------------------------------ */

const fmt = new Intl.NumberFormat("en-GB");
const fmtDec = new Intl.NumberFormat("en-GB", { maximumFractionDigits: 1 });

function fmtMoney(value: number) {
  if (!Number.isFinite(value)) return "—";
  return `K${fmt.format(Math.round(value))}`;
}

/** Rating bar palette — consistent from worst to best. */
const RATING_COLORS: Record<string, string> = {
  "1": "#b91c1c",
  "2": "#f97316",
  "3": "#eab308",
  "4": "#65a30d",
  "5": "#15803d",
};

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

/** Plain skeleton rendered while data is loading. */
function EmptyDashboard() {
  return (
    <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
      {Array.from({ length: 6 }).map((_, i) => (
        <KpiCard key={i} icon={<AlertTriangle className="h-4 w-4" />} label="Loading…" value="—" />
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/* Charts error boundary — a chart failure must degrade gracefully,
   never take the whole page down.                                     */
/* ------------------------------------------------------------------ */

interface ErrorBoundaryProps {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}
interface ErrorBoundaryState {
  error: Error | null;
}

class ChartsErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  override state: ErrorBoundaryState = { error: null };
  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }
  override render() {
    if (this.state.error) {
      return (
        this.props.fallback ?? (
          <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-xs text-amber-800">
            Charts could not be rendered. The data tables below remain available.
          </p>
        )
      );
    }
    return this.props.children;
  }
}

/* ------------------------------------------------------------------ */
/* Page                                                                 */
/* ------------------------------------------------------------------ */

function AnalyticsPage() {
  const [state, setState] = useState<{
    data: Dashboard | null;
    error: Error | null;
    loading: boolean;
  }>({ data: null, error: null, loading: true });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await hrmApi.get<Dashboard>("/hrm/analytics/dashboard");
        if (!cancelled) setState({ data, error: null, loading: false });
      } catch (e) {
        if (!cancelled) setState({ data: null, error: e as Error, loading: false });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <AppShell>
      <AuthGate>
        <PageHeader
          title="HR analytics"
          description="Company-wide HR indicators drawn live from the payroll, time, recruitment and performance records."
        />
        {state.loading ? (
          <EmptyDashboard />
        ) : state.error ? (
          <p className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
            Could not load the analytics dashboard ({state.error.message}). Please try again.
          </p>
        ) : (
          <div className="space-y-6">
            <p className="text-xs text-muted-foreground">
              Data as at{" "}
              {new Date(state.data!.asAt).toLocaleString(undefined, {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </p>
            <ChartsErrorBoundary>
              <Suspense fallback={<EmptyDashboard />}>
                <Charts data={state.data!} />
              </Suspense>
            </ChartsErrorBoundary>
          </div>
        )}
      </AuthGate>
    </AppShell>
  );
}
