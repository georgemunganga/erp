import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/approvals")({
  head: () => ({
    meta: [
      { title: "Approvals — Kuubu HRM" },
      { name: "description", content: "Everything waiting on your decision, oldest and highest risk first." },
      { property: "og:title", content: "Approvals — Kuubu HRM" },
      { property: "og:description", content: "Everything waiting on your decision, oldest and highest risk first." },
    ],
  }),
  component: Approvals,
});

interface Row {
  id: string;
  kind: "Leave" | "Attendance" | "HR request" | "Workflow";
  title: string;
  employeeName: string;
  status: string;
  opened: string;
  to: string;
  // M27 P0 UX audit: workflow items carry a subject worker id so HR can act
  // on them from this page instead of being routed into a dead-end detail.
  subjectWorkerId?: string;
}

function decide(row: Row, action: "approve" | "reject", opts: { onDone: () => void; setBusy: (v: boolean) => void }): void {
  if (!row.subjectWorkerId) {
    // eslint-disable-next-line no-console
    console.warn("Workflow row has no subject worker id; cannot decide inline.");
    window.location.assign(row.to.replace("$id", row.id));
    return;
  }
  opts.setBusy(true);
  realApi
    .workflowDecide(row.id, action)
    .then(() => {
      opts.setBusy(false);
      opts.onDone();
    })
    .catch(() => {
      opts.setBusy(false);
      opts.onDone();
    });
}

const approvedStatuses: Record<string, string> = {
  pending: "Pending",
  submitted: "Submitted",
  open: "Open",
  "in-progress": "In progress",
  "in-review": "In review",
  "awaiting-employee": "Awaiting employee",
  returned: "Returned",
  approved: "Approved",
  rejected: "Rejected",
  resolved: "Resolved",
  closed: "Closed",
  cancelled: "Cancelled",
};

function labelStatus(raw: string): string {
  return approvedStatuses[String(raw ?? "").toLowerCase()] ?? String(raw ?? "").replace(/^(.)/, (c) => c.toUpperCase());
}

// M27 P0 UX audit: the backend returns statuses in lowercase ("pending",
// "submitted", "in review"...), so this predicate compared against mixed-case
// literals matched nothing and the Approvals page was always empty even when
// the dashboard count said otherwise.
function isDecidable(status: string): boolean {
  const s = String(status ?? "").toLowerCase();
  return s === "pending" || s === "submitted" || s === "in review" || s === "returned" || s === "open" || s === "in progress" || s === "awaiting employee";
}

async function loadQueue(): Promise<Row[]> {
  const [leave, corrections, exp, workflow] = await Promise.all([
    realApi.leaveRequests({ page: 1, pageSize: 50 }),
    realApi.timeCorrections({ page: 1, pageSize: 50 }),
    realApi.experienceRequests({ page: 1, pageSize: 50 }),
    realApi.workflowQueue(),
  ]);
  const rows: Row[] = [
    ...(Array.isArray(leave.items) ? leave.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.id ?? ""),
        kind: "Leave" as const,
        title: `${String(x.leaveTypeCode ?? "leave")} · ${Number(x.requestedDays ?? 0)} days`,
        employeeName: String(x.workerName ?? "Unknown"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 10) : "—",
        to: "/hrm/leave/$id",
      };
    }),
    ...(Array.isArray(corrections.items) ? corrections.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.id ?? ""),
        kind: "Attendance" as const,
        title: `Correction · ${String(x.workDate ?? String(x.date ?? String(x.claimDate ?? "—")))}`,
        employeeName: String(x.workerName ?? "Unknown"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 10) : "—",
        to: "/hrm/attendance/$id",
      };
    }),
    ...(Array.isArray(exp.items) ? exp.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.id ?? ""),
        kind: "HR request" as const,
        title: String(x.subject ?? "HR request"),
        employeeName: String(x.workerName ?? "Unknown"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.createdAt === "string" ? String(x.createdAt).slice(0, 10) : "—",
        to: "/hrm/requests/$id",
      };
    }),
    ...(Array.isArray(workflow.items) ? workflow.items : []).map((r) => {
      const x = r as Record<string, unknown>;
      return {
        id: String(x.requestId ?? ""),
        kind: "Workflow" as const,
        title: `${String(x.workflowType ?? "request")} · ${String(x.subjectName ?? "Workflow item")}`,
        employeeName: String(x.subjectName ?? x.currentApproverName ?? "Workflow queue"),
        status: labelStatus(String(x.status ?? "")),
        opened: typeof x.dueAt === "string" ? String(x.dueAt).slice(0, 10) : "—",
        to: "/hrm/approvals/$id",
        subjectWorkerId: typeof x.subjectWorkerId === "string" ? String(x.subjectWorkerId) : undefined,
      };
    }),
  ];
  return rows.filter((r) => isDecidable(r.status));
}

function Approvals() {
  const [tick, setTick] = useState(0);
  const [busy, setBusy] = useState<string | null>(null);
  const state = useApi(() => loadQueue(), [tick]);
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Approvals"
        title="Approvals"
        description="Everything waiting on your decision across leave, attendance, HR requests and workflow items, oldest first."
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<Row>
            rows={rows.filter((r) => (view === "open" ? true : r.kind === view))}
            savedViews={[
              { id: "all", label: "All types" },
              { id: "Leave", label: "Leave" },
              { id: "Attendance", label: "Attendance" },
              { id: "HR request", label: "HR requests" },
              { id: "Workflow", label: "Workflow" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee or title"
            searchFields={(r) => `${r.id} ${r.employeeName} ${r.title}`}
            filters={[
              { id: "kind", label: "Type", options: ["Leave", "Attendance", "HR request", "Workflow"], match: (r, v) => r.kind === v },
            ]}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (r) => (
                  <Link to={r.to} params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">
                    {r.id.slice(0, 13)}…
                  </Link>
                ),
              },
              { id: "kind", header: "Type", cell: (r) => r.kind },
              { id: "title", header: "Item", cell: (r) => <span className="block max-w-64 truncate">{r.title}</span> },
              {
                id: "employee",
                header: "Employee",
                cell: (r) => <span className="block max-w-56 truncate">{r.employeeName}</span>,
              },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "opened", header: "Opened", cell: (r) => r.opened },
              {
                id: "actions",
                header: "Decide",
                cell: (r) =>
                  r.kind === "Workflow" ? (
                    <span className="flex items-center gap-1">
                      <button
                        disabled={busy === r.id}
                        className="rounded border border-border bg-background px-2 py-0.5 text-xs font-medium text-green-700 hover:bg-green-50 disabled:opacity-50"
                        onClick={() => decide(r, "approve", { onDone: () => setTick((t) => t + 1), setBusy: (v) => setBusy(v ? r.id : null) })}
                      >
                        {busy === r.id ? "…" : "Approve"}
                      </button>
                      <button
                        disabled={busy === r.id}
                        className="rounded border border-border bg-background px-2 py-0.5 text-xs font-medium text-red-700 hover:bg-red-50 disabled:opacity-50"
                        onClick={() => decide(r, "reject", { onDone: () => setTick((t) => t + 1), setBusy: (v) => setBusy(v ? r.id : null) })}
                      >
                        {busy === r.id ? "…" : "Reject"}
                      </button>
                    </span>
                  ) : (
                    <Link to={r.to} params={{ id: r.id }} className="text-xs text-primary underline underline-offset-2">
                      Open →
                    </Link>
                  ),
              },
            ]}
            emptyBody="Nothing is waiting on a decision right now."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
