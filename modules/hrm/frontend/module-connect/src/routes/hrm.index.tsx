import { MaterialRequestShortcut } from "@/platform/components/MaterialRequestShortcut";
import { createFileRoute, Link } from "@tanstack/react-router";
import { Button } from "@/components/ui/button";
import { workspaces } from "@/mock/data";
import { api } from "@/mock/service";
import type { WorkItem } from "@/mock/types";
import { isPathEnabled } from "@/modules/hrm/scope";
import { useApp } from "@/platform/app-context";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { WorkQueue } from "@/platform/components/WorkQueue";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/")({
  head: () => ({
    meta: [
      { title: "Home — Newworldcargo HRM" },
      {
        name: "description",
        content: "Live HR work queue: exceptions, approvals, tasks and deadlines.",
      },
    ],
  }),
  component: Home,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
type Row = Record<string, unknown>;

const rows = (value: unknown): Row[] =>
  Array.isArray(value)
    ? (value as Row[])
    : value && typeof value === "object" && "items" in value
      ? (((value as { items?: unknown[] }).items ?? []) as Row[])
      : [];

const dueText = (value: unknown) => {
  if (!value) return "No due date";
  const date = new Date(String(value));
  return Number.isNaN(date.getTime()) ? String(value) : `Due ${date.toLocaleDateString("en-GB")}`;
};

async function loadHome(role: string) {
  if (!USE_REAL) {
    return {
      items: await api.workQueue(role as never),
      metrics: [
        { label: "Open leave requests", value: "4", hint: "Demo data" },
        { label: "Attendance exceptions", value: "2", hint: "Demo data" },
        { label: "HR cases open", value: "1", hint: "Demo data" },
        { label: "Employees in scope", value: "8", hint: "Demo data" },
      ],
    };
  }

  const results = await Promise.allSettled([
    realApi.workflowQueue(),
    realApi.leaveRequests({ status: "submitted" }),
    realApi.timeCorrections({ status: "submitted" }),
    realApi.experienceRequests({ status: "open" }),
    realApi.payrollRuns(),
    realApi.employees({ page: 1, pageSize: 1, includeArchived: false }),
    realApi.relationsCases({ status: "open" }),
  ]);
  const value = <T,>(index: number, fallback: T): T =>
    results[index]?.status === "fulfilled" ? (results[index].value as T) : fallback;
  const workflow = value(0, { items: [] });
  const leave = value(1, { items: [] });
  const corrections = value(2, { items: [] });
  const requests = value(3, { items: [] });
  const payroll = value(4, { items: [] });
  const workers = value(5, { items: [], totalCount: 0 });
  const cases = value(6, { items: [] });

  const now = Date.now();
  const items: WorkItem[] = [];
  for (const item of rows(workflow)) {
    const id = String(item.requestId ?? item.id ?? "");
    const dueAt = item.dueAt ? new Date(String(item.dueAt)).getTime() : NaN;
    items.push({
      id,
      band: "approval",
      title: `${String(item.workflowType ?? "HR")} decision`,
      context: `${String(item.subjectName ?? "Employee")} · ${String(item.status ?? "submitted")}`,
      due: dueText(item.dueAt),
      overdue: Number.isFinite(dueAt) && dueAt < now,
      to: "/hrm/approvals",
      roles: ["hr_admin", "hr_ops", "manager"],
    });
  }
  for (const item of rows(corrections)) {
    items.push({
      id: String(item.id),
      band: "exception",
      title: "Attendance correction awaiting review",
      context: `${String(item.workerName ?? "Employee")} · ${String(item.workDate ?? "")}`,
      due: dueText(item.createdAt),
      overdue: false,
      to: "/hrm/attendance",
      roles: ["hr_admin", "hr_ops"],
    });
  }
  for (const item of rows(requests).slice(0, 10)) {
    items.push({
      id: String(item.id),
      band: "task",
      title: String(item.subject ?? item.category ?? "HR request"),
      context: `${String(item.workerName ?? "Employee")} · ${String(item.status ?? "open")}`,
      due: dueText(item.dueAt),
      overdue: false,
      to: "/hrm/requests/$id",
      params: { id: String(item.id) },
      roles: ["hr_admin", "hr_ops"],
    });
  }
  for (const item of rows(payroll)
    .filter(
      (run) => !["released", "reconciled", "reversed"].includes(String(run.status).toLowerCase()),
    )
    .slice(0, 5)) {
    items.push({
      id: String(item.id),
      band: "deadline",
      title: `${String(item.periodLabel ?? item.reference ?? "Payroll run")} payroll`,
      context: String(item.status ?? "open"),
      due: dueText(item.payDate ?? item.updatedAt),
      overdue: false,
      to: "/hrm/payroll/runs/$id",
      params: { id: String(item.id) },
      roles: ["hr_admin", "payroll"],
    });
  }

  return {
    items,
    metrics: [
      {
        label: "Open leave requests",
        value: String(rows(leave).length),
        hint: "Submitted for decision",
      },
      {
        label: "Attendance exceptions",
        value: String(rows(corrections).length),
        hint: "Submitted corrections",
      },
      {
        label: "HR cases open",
        value: String(rows(cases).length),
        hint: "Restricted by your role",
      },
      {
        label: "Employees in scope",
        value: String(workers.totalCount ?? 0),
        hint: "Active tenant records",
      },
    ],
  };
}

function Home() {
  const { role } = useApp();
  const state = useApi(() => loadHome(role), [role]);
  const ws = workspaces.find((workspace) => workspace.id === role) ?? workspaces[0];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow={`${ws.label} workspace`}
          title="Home"
          description="Live tenant work is ordered by urgency. Resolve exceptions first, then approvals and operational tasks."
          primaryAction={
            <Button asChild>
              <Link to="/hrm/employees/new">Add employee</Link>
            </Button>
          }
        />
        <MaterialRequestShortcut />
        <Async state={state}>
          {(data) => (
            <WorkQueue
              items={data.items.filter((item) => isPathEnabled(item.to.split("/$")[0]))}
              metrics={data.metrics}
            />
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
