import { createFileRoute, Link, Outlet, useChildMatches, useLocation } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { money, payrollRunApi } from "@/mock/payrollrun";
import type { PayRun } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/payroll/runs")({
  head: () => ({
    meta: [
      { title: "Pay runs — Kuubu HRM" },
      {
        name: "description",
        content: "Every pay run with its stage, control totals, owner and next action.",
      },
      { property: "og:title", content: "Pay runs — Kuubu HRM" },
      {
        property: "og:description",
        content: "Every pay run with its stage, control totals, owner and next action.",
      },
    ],
  }),
  component: RunsList,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const finished = new Set(["Paid", "Closed", "Reversed"]);

type ListedPayRun = PayRun & {
  backendStatus: string;
  paymentStatus: string;
  exceptionCount: number;
  createdAt?: string;
};

/** M27: adapt persisted runs, including live control totals and payment state. */
function adaptRunRows(rows: unknown[]): ListedPayRun[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    const backendStatus = String(r.status ?? "draft");
    const statusMap: Record<string, PayRun["status"]> = {
      draft: "Draft",
      locked: "Draft",
      calculating: "Calculating",
      calculated: "Calculated",
      "in-review": "In review",
      approved: "Approved",
      released: "Paid",
      closed: "Closed",
      reversed: "Reversed",
    };
    const status = statusMap[backendStatus] ?? "Draft";
    const payment = String(r.paymentStatus ?? "not-created");
    const exceptionCount = Number(r.exceptionCount ?? 0);
    const nextAction =
      backendStatus === "draft"
        ? "Lock inputs"
        : backendStatus === "locked"
          ? "Calculate run"
          : backendStatus === "calculated"
            ? exceptionCount
              ? "Resolve exceptions"
              : "Approve run"
            : backendStatus === "in-review"
              ? "Awaiting approval"
            : backendStatus === "approved"
              ? "Release payslips"
            : backendStatus === "released" && payment === "not-created"
                ? "Generate payment file"
                : backendStatus === "released" && payment === "released"
                  ? "Reconcile bank result"
                  : backendStatus === "closed"
                    ? "Cycle complete"
                    : `Payment: ${payment}`;
    return {
      id: String(r.id ?? ""),
      period: String(r.periodLabel ?? ""),
      entityId: String(r.legalEntityId ?? r.entityId ?? ""),
      payGroup: String(r.payGroup ?? r.payGroupName ?? "Monthly ZMW"),
      currency: "ZMW",
      entityName: String(r.entityName ?? r.legalEntityName ?? "Payroll scope"),
      included: Number(r.employeeCount ?? 0),
      excluded: [],
      stages: [],
      timeline: [],
      totals: {
        headcount: Number(r.employeeCount ?? 0),
        gross: Number(r.totalGross ?? 0),
        deductions: Number(r.totalDeductions ?? 0),
        employerCost: Number(r.totalEmployerCost ?? 0),
        net: Number(r.totalNet ?? 0),
      },
      status,
      nextAction,
      dueDate: r.createdAt ? new Date(String(r.createdAt)).toLocaleDateString("en-GB") : "controlled workflow",
      branchId: r.locationId ? String(r.locationId) : undefined,
      owner: String(r.preparedBySubjectId ?? "Payroll officer"),
      preparedBy: String(r.preparedBySubjectId ?? ""),
      approvedBy: r.approvedBySubjectId ? String(r.approvedBySubjectId) : undefined,
      backendStatus,
      paymentStatus: payment,
      exceptionCount,
      createdAt: r.createdAt ? String(r.createdAt) : undefined,
    } satisfies ListedPayRun;
  });
}

function adaptMockRunRows(rows: PayRun[]): ListedPayRun[] {
  return rows.map((run) => ({
    ...run,
    backendStatus: run.status.toLowerCase(),
    paymentStatus: finished.has(run.status) ? "released" : "not-created",
    exceptionCount: 0,
  }));
}

let liveLocationsCache: { id: string; name: string }[] | null = null;

function branchName(branchId: string): string | undefined {
  const loc = liveLocationsCache?.find((l) => l.id === branchId);
  return loc?.name;
}

function RunsList() {
  const location = useLocation();
  const state = useApi(async (): Promise<ListedPayRun[]> => {
    if (!USE_REAL) return adaptMockRunRows(await payrollRunApi.runs());
    if (!liveLocationsCache) {
      const raw = await realApi.locations().catch(() => ({ items: [] as unknown[] }));
      liveLocationsCache = (Array.isArray(raw)
        ? raw
        : ((raw as Record<string, unknown>)?.items as unknown[]) ?? []).map((l) => ({
        id: String((l as Record<string, unknown>).id ?? ""),
        name: String((l as Record<string, unknown>).name ?? ""),
      }));
    }
    const result = await realApi.payrollRuns();
    return adaptRunRows(result.items ?? []);
  // This parent route stays mounted while its new/detail child routes are open.
  // Refetch when navigation returns here so a run created in a child is visible
  // immediately instead of requiring a browser refresh.
  }, [location.pathname]);
  const [view, setView] = useState("open");
  // `/payroll/runs/$id` is generated as a child of this route, so hand the
  // screen over whenever a run is open.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Payroll"
          title="Pay runs"
          description="A run moves through fixed stages. Calculating, approving, releasing payslips and paying people are deliberately separate steps."
          primaryAction={
            <Button asChild>
              <Link to="/hrm/payroll/runs/new">Start a pay run</Link>
            </Button>
          }
        />
        <Async state={state} rows={4}>
          {(rows) => {
            const visibleRows = rows.filter((r) =>
              view === "open"
                ? !finished.has(r.status)
                : view === "finished"
                  ? finished.has(r.status)
                  : true,
            );
            const statuses = Array.from(new Set(rows.map((r) => r.status))).filter(Boolean);
            const entities = Array.from(new Set(rows.map((r) => r.entityName).filter(Boolean)));
            const branches = Array.from(
              new Set(
                rows
                  .map((r) => (r.branchId ? (branchName(r.branchId) ?? "Branch") : "Organisation-wide"))
                  .filter(Boolean),
              ),
            );
            const filters = [
              {
                id: "status",
                label: "Status",
                options: statuses,
                match: (r: ListedPayRun, v: string) => r.status === v,
              },
              ...(entities.length > 1
                ? [
                    {
                      id: "entity",
                      label: "Entity",
                      options: entities,
                      match: (r: ListedPayRun, v: string) => r.entityName === v,
                    },
                  ]
                : []),
              ...(branches.length > 1
                ? [
                    {
                      id: "branch",
                      label: "Branch",
                      options: branches,
                      match: (r: ListedPayRun, v: string) =>
                        (r.branchId ? (branchName(r.branchId) ?? "Branch") : "Organisation-wide") === v,
                    },
                  ]
                : []),
            ];
            return (
            <ListPage<ListedPayRun>
              rows={visibleRows}
              savedViews={[
                { id: "open", label: `Needs action (${visibleRows.length})` },
                { id: "finished", label: `Paid or closed (${rows.filter((r) => finished.has(r.status)).length})` },
                { id: "all", label: "All runs" },
              ]}
              activeView={view}
              onViewChange={setView}
              searchPlaceholder="Search reference, period, branch or status"
              searchFields={(r) =>
                `${r.id} ${r.period} ${r.entityName} ${r.payGroup} ${r.status} ${r.backendStatus} ${
                  r.branchId ? (branchName(r.branchId) ?? "") : "organisation-wide"
                }`
              }
              filters={filters}
              columns={[
                {
                  id: "ref",
                  header: "Run",
                  cell: (r) => (
                    <span className="block min-w-0 max-w-64">
                      <Link
                        to="/hrm/payroll/runs/$id"
                        params={{ id: r.id }}
                        className="block truncate font-medium text-primary underline-offset-2 hover:underline"
                      >
                        {r.period} · {r.payGroup}
                      </Link>
                      <span className="block truncate font-mono text-xs text-muted-foreground">
                        {r.id}
                      </span>
                    </span>
                  ),
                },
                {
                  id: "entity",
                  header: "Scope",
                  cell: (r) => (
                    <span className="block max-w-48 truncate">
                      {r.entityName === "Payroll scope" && r.branchId
                        ? "Branch payroll"
                        : r.entityName}
                    </span>
                  ),
                },
                {
                  id: "branch",
                  header: "Branch",
                  defaultVisible: true,
                  cell: (r) =>
                    r.branchId ? (
                      <span className="block max-w-40 truncate text-xs text-muted-foreground">
                        {branchName(r.branchId) ?? "Branch"}
                      </span>
                    ) : (
                      <span className="text-xs text-muted-foreground">Organisation-wide</span>
                    ),
                },
                {
                  id: "people",
                  header: "Employees",
                  cell: (r) => (
                    <span className="tabular">{r.totals.headcount || r.included || "—"}</span>
                  ),
                },
                {
                  id: "gross",
                  header: "Gross",
                  cell: (r) => (
                    <span className="tabular">
                      {r.totals.gross ? money(r.totals.gross, r.currency) : "—"}
                    </span>
                  ),
                },
                {
                  id: "net",
                  header: "Net",
                  cell: (r) => (
                    <span className="tabular font-medium">
                      {r.totals.net ? money(r.totals.net, r.currency) : "—"}
                    </span>
                  ),
                },
                { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                {
                  id: "exceptions",
                  header: "Exceptions",
                  defaultVisible: false,
                  cell: (r) => (
                    <span className={r.exceptionCount ? "text-warning-foreground" : "text-muted-foreground"}>
                      {r.exceptionCount || "None"}
                    </span>
                  ),
                },
                {
                  id: "payment",
                  header: "Payment",
                  defaultVisible: false,
                  cell: (r) => (
                    <span className="block max-w-36 truncate text-xs text-muted-foreground">
                      {r.paymentStatus.replaceAll("-", " ")}
                    </span>
                  ),
                },
                {
                  id: "next",
                  header: "Next action",
                  cell: (r) => (
                    <span className="block max-w-56 truncate text-xs">
                      {r.nextAction} · due {r.dueDate}
                    </span>
                  ),
                },
                { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
                {
                  id: "prepared",
                  header: "Prepared by",
                  defaultVisible: false,
                  cell: (r) => r.preparedBy,
                },
              ]}
              emptyBody="No pay runs match the current view."
            />
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
