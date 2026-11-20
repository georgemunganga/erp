import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle } from "lucide-react";
import { blockedTasks, displayName, lifecycleApi, overdueTasks, taskProgress, TODAY } from "@/mock/lifecycle";
import type { OffboardingCase } from "@/mock/lifecycle";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/lifecycle/offboarding")({
  head: () => ({
    meta: [
      { title: "Offboarding — Kuubu HRM" },
      { name: "description", content: "Separation cases with notice, last working date and clearance progress, owner, next action and due date." },
      { property: "og:title", content: "Offboarding — Kuubu HRM" },
      { property: "og:description", content: "Separation cases with notice, last working date and clearance progress, owner, next action and due date." },
    ],
  }),
  component: OffboardingRoute,
});

const closed = ["Completed", "Cancelled"];

/** The detail route nests under this one; show the case when one is open, the list otherwise. */
function OffboardingRoute() {
  const children = useChildMatches();
  if (children.length > 0) return <Outlet />;
  return <OffboardingList />;
}

function Clearance({ done, total }: { done: number; total: number }) {
  const pct = total === 0 ? 0 : Math.round((done / total) * 100);
  return (
    <span className="block w-28">
      <span className="tabular text-xs">
        {done} of {total} cleared
      </span>
      <span
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`${done} of ${total} clearance items done`}
        className="mt-1 block h-1.5 w-full overflow-hidden rounded-full bg-muted"
      >
        <span className="block h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
      </span>
    </span>
  );
}

function OffboardingList() {
  const state = useMock(() => lifecycleApi.offboardings());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Offboarding"
        description="Separation, notice and clearance in one case. Nothing closes until assets, access, handover, final pay and advances are all resolved."
      />
      <Async state={state}>
        {(rows) => {
          const attention = (c: OffboardingCase) =>
            !closed.includes(c.status) && (blockedTasks(c.clearance).length > 0 || overdueTasks(c.clearance).length > 0);

          return (
            <ListPage<OffboardingCase>
              rows={rows.filter((c) =>
                view === "attention"
                  ? attention(c)
                  : view === "open"
                    ? !closed.includes(c.status)
                    : view === "closed"
                      ? closed.includes(c.status)
                      : true,
              )}
              savedViews={[
                { id: "all", label: "All cases" },
                { id: "open", label: "In progress" },
                { id: "attention", label: "Blocked or overdue" },
                { id: "closed", label: "Closed" },
              ]}
              activeView={view}
              onViewChange={setView}
              searchPlaceholder="Search reference, employee or reason"
              searchFields={(c) => `${c.id} ${displayName(c)} ${c.reason} ${c.branch}`}
              filters={[
                {
                  id: "reason",
                  label: "Reason",
                  options: ["Resignation", "Fixed-term expiry", "End of engagement", "Retirement", "Mutual separation", "Redundancy"],
                  match: (c, v) => c.reason === v,
                },
                {
                  id: "status",
                  label: "Status",
                  options: ["Draft", "Ready", "Active", "Blocked", "Completed", "Cancelled"],
                  match: (c, v) => c.status === v,
                },
                {
                  id: "branch",
                  label: "Branch",
                  options: ["Lusaka HQ", "Ndola Plant", "Kitwe Depot", "Livingstone Works", "Chingola Office", "Solwezi Yard"],
                  match: (c, v) => c.branch === v,
                },
              ]}
              bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
              columns={[
                {
                  id: "ref",
                  header: "Reference",
                  cell: (c) => (
                    <Link
                      to="/hrm/lifecycle/offboarding/$id"
                      params={{ id: c.id }}
                      className="font-mono text-xs text-primary underline underline-offset-2"
                    >
                      {c.id}
                    </Link>
                  ),
                },
                {
                  id: "employee",
                  header: "Employee",
                  cell: (c) => (
                    <span className="block max-w-56 truncate">
                      {displayName(c)}
                      <span className="block truncate text-xs text-muted-foreground">{c.jobTitle}</span>
                    </span>
                  ),
                },
                { id: "reason", header: "Reason", cell: (c) => <span className="text-xs">{c.reason}</span> },
                { id: "last", header: "Last working date", cell: (c) => <span className="tabular text-xs">{c.lastWorkingDate}</span> },
                {
                  id: "clearance",
                  header: "Clearance",
                  cell: (c) => {
                    const p = taskProgress(c.clearance);
                    const b = blockedTasks(c.clearance).length;
                    return (
                      <span className="block">
                        <Clearance done={p.done} total={p.total} />
                        {b > 0 ? (
                          <span className="mt-1 inline-flex items-center gap-1 text-xs text-warning">
                            <AlertTriangle className="size-3.5 shrink-0" aria-hidden />
                            {b} blocked
                          </span>
                        ) : null}
                      </span>
                    );
                  },
                },
                { id: "status", header: "Status", cell: (c) => <StatusBadge status={c.status} /> },
                {
                  id: "next",
                  header: "Next action",
                  cell: (c) => (
                    <span className="block max-w-64 truncate text-xs" title={c.nextAction}>
                      {c.nextAction}
                      <span className="block truncate text-muted-foreground">
                        {c.owner} · due {c.dueDate}
                      </span>
                    </span>
                  ),
                },
                { id: "owner", header: "Owner", defaultVisible: false, cell: (c) => <span className="text-xs">{c.owner}</span> },
                { id: "notice", header: "Notice period", defaultVisible: false, cell: (c) => <span className="text-xs">{c.noticePeriod}</span> },
                { id: "noticeOn", header: "Notice given", defaultVisible: false, cell: (c) => <span className="tabular text-xs">{c.noticeGivenOn}</span> },
                { id: "finalPay", header: "Final pay run", defaultVisible: false, cell: (c) => <span className="text-xs">{c.finalPayRun}</span> },
                { id: "branch", header: "Branch", defaultVisible: false, cell: (c) => <span className="text-xs">{c.branch}</span> },
                { id: "rehire", header: "Rehire eligibility", defaultVisible: false, cell: (c) => <span className="text-xs">{c.rehireEligible}</span> },
              ]}
              emptyBody={`No separation cases match the current view. Today is ${TODAY}.`}
            />
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
