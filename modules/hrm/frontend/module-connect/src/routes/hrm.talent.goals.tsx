import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { employees } from "@/mock/data";
import type { Goal } from "@/mock/talent";
import { ME, talentApi } from "@/mock/talent";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/talent/goals")({
  head: () => ({
    meta: [
      { title: "Goals — Kuubu HRM" },
      {
        name: "description",
        content:
          "My goals and my team's goals: measure, weighting, target against current, progress and the organisational goal each one is aligned to.",
      },
      { property: "og:title", content: "Goals — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "My goals and my team's goals: measure, weighting, target against current, progress and the organisational goal each one is aligned to.",
      },
    ],
  }),
  component: GoalsPage,
});

const name = (id: string) => employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

function WeightingSummary({ rows }: { rows: Goal[] }) {
  const byEmployee = new Map<string, number>();
  rows.forEach((g) => byEmployee.set(g.employeeId, (byEmployee.get(g.employeeId) ?? 0) + g.weighting));

  return (
    <>
      {[...byEmployee.entries()].map(([id, total]) => (
        <span
          key={id}
          className="inline-flex items-center gap-1.5 rounded-full border bg-surface px-2.5 py-0.5 text-xs"
        >
          <span className="font-medium">{name(id)}</span>
          <span className="text-muted-foreground">
            scorecard weighting {total}% of 100%{total === 100 ? " — fully allocated" : " — not yet balanced"}
          </span>
        </span>
      ))}
    </>
  );
}

function GoalsPage() {
  const state = useMock(() => talentApi.goals());
  const [view, setView] = useState("mine");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Talent · Performance"
        title="Goals"
        description="Each goal carries the measure it is judged on, the weighting it holds in the cycle scorecard, and the organisational goal it rolls up to. Team views show progress only — ratings are never shown in a list."
        primaryAction={<Button>Add a goal</Button>}
      />
      <Async state={state}>
        {(all) => {
          const rows = all.filter((g) =>
            view === "mine"
              ? g.employeeId === ME
              : view === "team"
                ? g.scope === "team"
                : view === "open"
                  ? g.status === "Active" || g.status === "Draft"
                  : true,
          );
          const weighted = rows.filter((g) => g.status !== "Cancelled");

          return (
            <div className="space-y-4">
              <section
                aria-label="Scorecard weighting"
                className="rounded-lg border bg-surface-muted px-4 py-3"
              >
                <h2 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Scorecard weighting, 2026 performance cycle
                </h2>
                <div className="mt-2 flex flex-wrap gap-2">
                  <WeightingSummary rows={weighted} />
                </div>
                <p className="mt-2 text-xs text-muted-foreground">
                  Cancelled goals are excluded from the weighting. Reweighting a goal is recorded on the goal itself.
                </p>
              </section>
              <ListPage<Goal>
                  rows={rows}
                  savedViews={[
                    { id: "mine", label: "My goals" },
                    { id: "team", label: "My team's goals" },
                    { id: "open", label: "Still in play" },
                    { id: "all", label: "All goals" },
                  ]}
                  activeView={view}
                  onViewChange={setView}
                  searchPlaceholder="Search goal, measure or employee"
                  searchFields={(g) => `${g.id} ${g.title} ${g.measure} ${name(g.employeeId)} ${g.cycle}`}
                  emptyBody="No goals match this view. Try 'All goals', or clear the filters."
                  filters={[
                    {
                      id: "status",
                      label: "Status",
                      options: ["Draft", "Active", "Achieved", "Missed", "Cancelled"],
                      match: (g, v) => g.status === v,
                    },
                    {
                      id: "cycle",
                      label: "Cycle",
                      options: ["2026 performance cycle"],
                      match: (g, v) => g.cycle === v,
                    },
                  ]}
                  bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
                  columns={[
                    {
                      id: "ref",
                      header: "Reference",
                      cell: (g) => <span className="font-mono text-xs text-muted-foreground">{g.id}</span>,
                    },
                    {
                      id: "goal",
                      header: "Goal",
                      cell: (g) => (
                        <div className="max-w-80 space-y-0.5">
                          <p className="font-medium">{g.title}</p>
                          <p className="text-xs text-muted-foreground">{name(g.employeeId)}</p>
                          <p className="text-xs text-muted-foreground">Measure: {g.measure}</p>
                          <p className="text-xs text-muted-foreground">
                            {g.alignedTo ? `Aligned to: ${g.alignedTo.title}` : "Not aligned to a parent goal"}
                          </p>
                          {g.note ? <p className="text-xs text-muted-foreground">{g.note}</p> : null}
                        </div>
                      ),
                    },
                    {
                      id: "weighting",
                      header: "Weighting",
                      cell: (g) => (
                        <span className="tabular text-sm">
                          {g.weighting}%
                          <span className="sr-only"> of the cycle scorecard</span>
                        </span>
                      ),
                    },
                    {
                      id: "progress",
                      header: "Target and progress",
                      cell: (g) => (
                        <div className="w-44 space-y-1">
                          <p className="text-xs text-muted-foreground">
                            Target {g.target} · now {g.current}
                          </p>
                          <Progress
                            value={g.progress}
                            aria-label={`${g.title}: ${g.progress}% of target`}
                            className="h-2"
                          />
                          <p className="tabular text-xs">{g.progress}% of target</p>
                        </div>
                      ),
                    },
                    { id: "status", header: "Status", cell: (g) => <StatusBadge status={g.status} /> },
                    {
                      id: "owner",
                      header: "Owner",
                      cell: (g) => <span className="block max-w-40 truncate text-xs">{g.owner}</span>,
                    },
                    {
                      id: "next",
                      header: "Next action",
                      cell: (g) => (
                        <span className="block max-w-56 text-xs">
                          {g.nextAction} · due {g.dueDate}
                        </span>
                      ),
                    },
                    { id: "cycle", header: "Cycle", defaultVisible: false, cell: (g) => g.cycle },
                    {
                      id: "aligned",
                      header: "Aligned parent goal",
                      defaultVisible: false,
                      cell: (g) => g.alignedTo?.title ?? "—",
                    },
                    {
                      id: "updated",
                      header: "Last updated",
                      defaultVisible: false,
                      cell: (g) => g.lastUpdated,
                    },
                ]}
              />
            </div>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
