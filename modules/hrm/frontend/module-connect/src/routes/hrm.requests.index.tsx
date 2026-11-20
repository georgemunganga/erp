import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/requests/")({
  head: () => ({
    meta: [
      { title: "HR requests — Kuubu HRM" },
      { name: "description", content: "Raise and track HR cases: letters, data changes and queries." },
      { property: "og:title", content: "HR requests — Kuubu HRM" },
      { property: "og:description", content: "Raise and track HR cases: letters, data changes and queries." },
    ],
  }),
  component: RequestsList,
});

/** Backend statuses open | in-progress | awaiting-employee | resolved | closed. */
const statusLabel: Record<string, string> = {
  open: "Open",
  "in-progress": "In progress",
  "awaiting-employee": "Awaiting employee",
  resolved: "Resolved",
  closed: "Closed",
};

const categoryLabel: Record<string, string> = {
  payroll: "Payroll",
  benefits: "Benefits",
  contract: "Contract",
  "data-change": "Data change",
  "employment-letter": "Employment letter",
  other: "Other",
};

export interface HrCase {
  id: string;
  employeeId: string;
  employeeName: string;
  category: string;
  subject: string;
  status: string;
  confidentiality: string;
  opened: string;
  nextAction: string;
}

function adapt(rows: unknown[]): HrCase[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    const status = String(r.status ?? "");
    return {
      id: String(r.id ?? ""),
      employeeId: String(r.workerId ?? ""),
      employeeName: String(r.workerName ?? "Unknown"),
      category: categoryLabel[String(r.category ?? "")] ?? String(r.category ?? "Other"),
      subject: String(r.subject ?? ""),
      status: statusLabel[status] ?? status,
      confidentiality: String(r.confidentiality ?? "normal"),
      opened: typeof r.createdAt === "string" ? String(r.createdAt).slice(0, 10) : "—",
      nextAction:
        status === "open" || status === "in-progress" || status === "awaiting-employee"
          ? status === "awaiting-employee"
            ? "Awaiting employee reply"
            : "Awaiting HR action"
          : "Closed",
    } satisfies HrCase;
  });
}

function RequestsList() {
  // M25: employee-scoped inbox — GET /hrm/me/requests is keyed on the caller's
  // OIDC subject, so an employee can never see another worker's cases. HR
  // roles keep the company-wide list on the admin `experienceRequests` shape.
  const state = useApi(async () => adapt((await realApi.myRequests()).items), []);
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="HR requests"
        title="HR requests"
        description="Every request in one thread: conversation, status and next update together, never scattered across email."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/requests/new">Raise a request</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<HrCase>
            rows={rows.filter((r) =>
              view === "open"
                ? ["Open", "In progress", "Awaiting employee"].includes(r.status)
                : view === "resolved"
                  ? ["Resolved"].includes(r.status)
                  : true,
            )}
            savedViews={[
              { id: "all", label: "All requests" },
              { id: "open", label: "Awaiting action" },
              { id: "resolved", label: "Resolved" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee, category or subject"
            searchFields={(r) => `${r.id} ${r.employeeName} ${r.category} ${r.subject}`}
            filters={[
              {
                id: "category",
                label: "Category",
                options: Object.values(categoryLabel) as string[],
                match: (r, v) => r.category === v,
              },
              {
                id: "confidentiality",
                label: "Confidentiality",
                options: ["Normal", "Confidential"],
                match: (r, v) => (v === "Confidential" ? r.confidentiality === "confidential" : r.confidentiality !== "confidential"),
              },
            ]}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (r) => (
                  <Link
                    to="/hrm/requests/$id"
                    params={{ id: r.id }}
                    className="font-mono text-xs text-primary underline underline-offset-2"
                  >
                    {r.id.slice(0, 8)}…
                  </Link>
                ),
              },
              {
                id: "employee",
                header: "Employee",
                cell: (r) => (
                  <span className="block max-w-56 truncate">
                    {r.employeeName || "\u2014 HR-initiated"}
                  </span>
                ),
              },
              { id: "category", header: "Category", cell: (r) => r.category },
              {
                id: "subject",
                header: "Subject",
                cell: (r) => (
                  <span className="block max-w-64 truncate">
                    {r.subject}
                    {r.confidentiality === "confidential" ? (
                      <span className="ml-1 rounded-full border border-warning/40 bg-warning-soft px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-warning">
                        Confidential
                      </span>
                    ) : null}
                  </span>
                ),
              },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              {
                id: "next",
                header: "Next action",
                cell: (r) => (
                  <span className="block max-w-56 truncate text-xs">
                    {r.nextAction} · opened {r.opened}
                  </span>
                ),
              },
            ]}
            emptyBody="No HR requests match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
