import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { ArrowRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import { lifecycleApi, TODAY } from "@/mock/lifecycle";
import type { MovementRecord } from "@/mock/lifecycle";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/lifecycle/movements")({
  head: () => ({
    meta: [
      { title: "Movements — Kuubu HRM" },
      { name: "description", content: "Effective-dated promotions, transfers, secondments and manager changes: current assignment against the proposed one." },
      { property: "og:title", content: "Movements — Kuubu HRM" },
      { property: "og:description", content: "Effective-dated promotions, transfers, secondments and manager changes: current assignment against the proposed one." },
    ],
  }),
  component: MovementsRoute,
});

/** The guided flow nests under this route; show it when it's open, the list otherwise. */
function MovementsRoute() {
  const children = useChildMatches();
  if (children.length > 0) return <Outlet />;
  return <MovementsList />;
}

const name = (id: string) => employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

/** The one fact that actually changes, by movement type. */
function facet(m: MovementRecord, side: "current" | "proposed") {
  const a = m[side];
  switch (m.type) {
    case "Promotion":
      return `${a.grade} · ${a.jobTitle}`;
    case "Manager change":
      return a.manager;
    case "Secondment":
      return `${a.branch} · ${a.entity}`;
    case "Transfer":
    default:
      return `${a.branch} · ${a.costCentre}`;
  }
}

const openStatuses = ["Draft", "Submitted", "In review", "Returned"];

function MovementsList() {
  const state = useMock(() => lifecycleApi.movements());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Movements"
        description="Promotion, transfer, secondment and manager change — each one effective-dated. Approved movements are held as pending future changes and applied on the effective date; history is never overwritten."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/lifecycle/movements/new">Raise a movement</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<MovementRecord>
            rows={rows.filter((m) =>
              view === "pending"
                ? openStatuses.includes(m.status)
                : view === "future"
                  ? m.status === "Approved" && m.effectiveFrom > TODAY
                  : view === "closed"
                    ? ["Rejected", "Cancelled"].includes(m.status)
                    : true,
            )}
            savedViews={[
              { id: "all", label: "All movements" },
              { id: "pending", label: "Awaiting a decision" },
              { id: "future", label: "Approved, not yet effective" },
              { id: "closed", label: "Rejected or cancelled" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee or change"
            searchFields={(m) => `${m.id} ${name(m.employeeId)} ${m.type} ${m.headline}`}
            filters={[
              {
                id: "type",
                label: "Type",
                options: ["Promotion", "Transfer", "Secondment", "Manager change"],
                match: (m, v) => m.type === v,
              },
              {
                id: "status",
                label: "Status",
                options: ["Submitted", "In review", "Approved", "Returned", "Rejected"],
                match: (m, v) => m.status === v,
              },
              {
                id: "timing",
                label: "Timing",
                options: ["Already effective", "Pending future change"],
                match: (m, v) => (v === "Pending future change" ? m.effectiveFrom > TODAY : m.effectiveFrom <= TODAY),
              },
            ]}
            bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
            columns={[
              { id: "ref", header: "Reference", cell: (m) => <span className="font-mono text-xs">{m.id}</span> },
              {
                id: "employee",
                header: "Employee",
                cell: (m) => (
                  <span className="block max-w-56 truncate">
                    {name(m.employeeId)}
                    <span className="block truncate text-xs text-muted-foreground">{m.current.jobTitle}</span>
                  </span>
                ),
              },
              { id: "type", header: "Type", cell: (m) => <span className="text-xs">{m.type}</span> },
              {
                id: "change",
                header: "Current → proposed",
                cell: (m) => (
                  <span className="block max-w-72 text-xs" title={`${facet(m, "current")} → ${facet(m, "proposed")}`}>
                    <span className="block truncate text-muted-foreground">Now: {facet(m, "current")}</span>
                    <span className="mt-0.5 flex items-center gap-1 font-medium">
                      <ArrowRight className="size-3 shrink-0" aria-hidden />
                      <span className="truncate">{facet(m, "proposed")}</span>
                    </span>
                  </span>
                ),
              },
              {
                id: "effective",
                header: "Effective from",
                cell: (m) => (
                  <span className="block text-xs">
                    <span className="tabular">{m.effectiveFrom}</span>
                    {m.effectiveTo ? <span className="block text-muted-foreground">returns {m.effectiveTo}</span> : null}
                    {m.status === "Approved" && m.effectiveFrom > TODAY ? (
                      <span className="block text-muted-foreground">pending future change</span>
                    ) : null}
                  </span>
                ),
              },
              { id: "status", header: "Status", cell: (m) => <StatusBadge status={m.status} /> },
              {
                id: "next",
                header: "Next action",
                cell: (m) => (
                  <span className="block max-w-64 truncate text-xs" title={m.nextAction}>
                    {m.nextAction}
                    <span className="block truncate text-muted-foreground">
                      {m.owner} · due {m.dueDate}
                    </span>
                  </span>
                ),
              },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (m) => <span className="text-xs">{m.owner}</span> },
              { id: "reason", header: "Reason", defaultVisible: false, cell: (m) => <span className="block max-w-72 text-xs">{m.reason}</span> },
              { id: "position", header: "Position", defaultVisible: false, cell: (m) => <span className="font-mono text-xs">{m.current.positionId} → {m.proposed.positionId}</span> },
              { id: "manager", header: "Manager", defaultVisible: false, cell: (m) => <span className="text-xs">{m.current.manager} → {m.proposed.manager}</span> },
              { id: "raised", header: "Raised by", defaultVisible: false, cell: (m) => <span className="text-xs">{m.raisedBy} on {m.raisedOn}</span> },
            ]}
            emptyBody={`No movements match the current view. Today is ${TODAY}.`}
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
