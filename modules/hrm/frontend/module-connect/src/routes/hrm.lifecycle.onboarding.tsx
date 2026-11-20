import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle } from "lucide-react";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/lifecycle/onboarding")({
  head: () => ({
    meta: [
      { title: "Onboarding — Kuubu HRM" },
      { name: "description", content: "Every joiner's onboarding case with progress, blockers, owner, next action and due date." },
      { property: "og:title", content: "Onboarding — Kuubu HRM" },
      { property: "og:description", content: "Every joiner's onboarding case with progress, blockers, owner, next action and due date." },
    ],
  }),
  component: OnboardingRoute,
});

/** The detail route nests under this one; show the case when one is open, the list otherwise. */
function OnboardingRoute() {
  const children = useChildMatches();
  if (children.length > 0) return <Outlet />;
  return <OnboardingList />;
}

function Progress({ done, total }: { done: number; total: number }) {
  const pct = total === 0 ? 0 : Math.round((done / total) * 100);
  return (
    <span className="block w-28">
      <span className="tabular text-xs">
        {done} of {total} done
      </span>
      <span
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`${done} of ${total} onboarding tasks done`}
        className="mt-1 block h-1.5 w-full overflow-hidden rounded-full bg-muted"
      >
        <span className="block h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
      </span>
    </span>
  );
}

interface OnboardingRow {
  id: string;
  employeeNo: string;
  personName: string;
  jobTitle: string;
  department: string;
  startDate: string;
  status: string;
  done: number;
  total: number;
  ready: boolean;
}

/**
 * M22: live onboarding inbox. The backend has a per-worker readiness plan
 * (assignment + NRC + TPIN + NAPSA number + bank detail), so the list is
 * derived from the worker directory with one plan fetch per active worker.
 */
function OnboardingList() {
  const state = useApi(
    async () => {
      const page = await realApi.employees({ status: "active", page: 1, pageSize: 200 });
      const workers = (page.items ?? []) as Record<string, unknown>[];
      // Pull every plan in parallel; a plan failure degrades to a plain row.
      const plans = await Promise.allSettled(
        workers.map((w) => realApi.onboardingPlan(String(w.id))),
      );
      return workers.map((w, i) => {
        const plan = plans[i].status === "fulfilled" ? plans[i].value : null;
        const ready = Boolean(plan?.isOnboarded);
        const done = plan?.tasksCompleted ?? 0;
        const total = plan?.tasksTotal ?? 5;
        return {
          id: String(w.id ?? ""),
          employeeNo: String(w.employeeNo ?? ""),
          personName: String(w.fullName ?? ""),
          jobTitle: String(w.jobTitle ?? ""),
          department: String(w.orgUnitName ?? ""),
          startDate: String(w.startDate ?? ""),
          status: ready ? "Ready" : "Active",
          done,
          total,
          ready,
        } satisfies OnboardingRow;
      });
    },
    [],
  );
  const [view, setView] = useState("all");

  const attention = useMemo(() => new Set(state.data?.filter((r) => !r.ready).map((r) => r.id) ?? []), [state.data]);

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Onboarding"
        description="One case per joiner, from accepted offer to the end of probation. Every row shows what happens next and how much of the statutory pack is complete."
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<OnboardingRow>
            rows={rows.filter((r) =>
              view === "attention" ? attention.has(r.id) : view === "open" ? !r.ready : view === "closed" ? r.ready : true,
            )}
            savedViews={[
              { id: "all", label: "All cases" },
              { id: "open", label: "In progress" },
              { id: "attention", label: "Needs attention" },
              { id: "closed", label: "Ready" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, joiner or role"
            searchFields={(r) => `${r.employeeNo} ${r.personName} ${r.jobTitle} ${r.department}`}
            filters={[
              {
                id: "department",
                label: "Department",
                options: Array.from(new Set(rows.map((r) => r.department).filter(Boolean))) as string[],
                match: (r, v) => r.department === v,
              },
            ]}
            columns={[
              {
                id: "ref",
                header: "Employee",
                cell: (r) => (
                  <Link
                    to="/hrm/lifecycle/onboarding/$id"
                    params={{ id: r.id }}
                    className="block max-w-56 truncate font-mono text-xs text-primary underline underline-offset-2"
                  >
                    {r.employeeNo}
                    <span className="block truncate text-foreground">{r.personName}</span>
                  </Link>
                ),
              },
              { id: "role", header: "Role", cell: (r) => <span className="block max-w-56 truncate">{r.jobTitle}</span> },
              { id: "start", header: "Start date", cell: (r) => <span className="tabular text-xs">{r.startDate}</span> },
              {
                id: "progress",
                header: "Checklist",
                cell: (r) => <Progress done={r.done} total={r.total} />,
              },
              {
                id: "blockers",
                header: "Blocked / overdue",
                cell: (r) =>
                  r.ready ? (
                    <span className="text-xs text-muted-foreground">None</span>
                  ) : (
                    <span className="inline-flex items-start gap-1.5 text-xs">
                      <AlertTriangle className="mt-0.5 size-3.5 shrink-0 text-warning" aria-hidden />
                      <span>{r.total - r.done} remaining</span>
                    </span>
                  ),
              },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              {
                id: "next",
                header: "Next action",
                cell: (r) => (
                  <span className="block max-w-64 truncate text-xs">
                    {r.ready ? "Onboarding complete" : "Complete the statutory pack"}
                    <span className="block truncate text-muted-foreground">
                      {r.department} · due by start date
                    </span>
                  </span>
                ),
              },
            ]}
            emptyBody="No onboarding cases match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
