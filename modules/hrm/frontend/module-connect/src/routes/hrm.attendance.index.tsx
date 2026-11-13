import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { AttendanceCorrection, RequestStatus } from "@/mock/types";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";


export const Route = createFileRoute("/hrm/attendance/")({
  head: () => ({
    meta: [
      { title: "Attendance corrections — Kuubu HRM" },
      { name: "description", content: "Review and correct clocking exceptions with a full audit trail." },
      { property: "og:title", content: "Attendance corrections — Kuubu HRM" },
      { property: "og:description", content: "Review and correct clocking exceptions with a full audit trail." },
    ],
  }),
  component: AttendanceList,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const mockStatus: Record<string, string> = {
  submitted: "Submitted",
  approved: "Approved",
  rejected: "Rejected",
  cancelled: "Cancelled",
  in_review: "In review",
  returned: "Returned",
};

function adaptCorrections(rows: unknown[]): AttendanceCorrection[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    const status = String(r.status ?? "");
    return {
      id: String(r.id),
      employeeId: String(r.workerId ?? ""),
      date: String(r.workDate ?? ""),
      recordedIn: typeof r.recordedClockIn === "string" ? String(r.recordedClockIn) : undefined,
      recordedOut: typeof r.recordedClockOut === "string" ? String(r.recordedClockOut) : undefined,
      claimedIn: typeof r.proposedClockIn === "string" ? String(r.proposedClockIn) : "—",
      claimedOut: typeof r.proposedClockOut === "string" ? String(r.proposedClockOut) : "—",
      reason: String(r.reason ?? ""),
      status: (mockStatus[status] ?? status) as RequestStatus,
      owner: String(r.workerName ?? ""),
      nextAction: status === "submitted" ? "Awaiting review" : "—",
      dueDate: String(r.workDate ?? ""),
      timeline: [],
    } satisfies AttendanceCorrection;
  });
}

function AttendanceList() {
  const state = useApi(
    async () => {
      const page = await realApi.timeCorrections();
      return adaptCorrections(page.items);
    },
    [],
  );
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Attendance"
        title="Attendance corrections"
        description="Every correction shows the recorded clock data next to what's claimed, so nothing gets approved blind."
        meta={<ScopeBadge />}
        primaryAction={
          <div className="flex items-center gap-2">
            <ImportDialog typeKey="attendance" onDone={() => state.reload()} />
            <Button asChild>
              <Link to="/hrm/attendance/new">Raise a correction</Link>
            </Button>
          </div>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<AttendanceCorrection>
            rows={rows.filter((r) => (view === "open" ? !["Approved", "Rejected", "Cancelled"].includes(r.status) : true))}
            savedViews={[
              { id: "all", label: "All corrections" },
              { id: "open", label: "Awaiting action" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference or employee"
            searchFields={(r) => `${r.id} ${r.owner} ${r.reason}`}
            filters={[
              { id: "status", label: "Status", options: ["Submitted", "In review", "Approved", "Returned"] as string[], match: (r, v) => r.status === v },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (r) => <Link to="/hrm/attendance/$id" params={{ id: r.id }} className="font-mono text-xs text-primary underline underline-offset-2">{r.id}</Link> },
              { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{r.owner}</span> },
              { id: "date", header: "Date", cell: (r) => r.date },
              { id: "recorded", header: "Recorded", cell: (r) => `${r.recordedIn ?? "—"}–${r.recordedOut ?? "—"}` },
              { id: "claimed", header: "Claimed", cell: (r) => `${r.claimedIn}–${r.claimedOut}` },
              { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
              { id: "next", header: "Next action", cell: (r) => <span className="block max-w-56 truncate text-xs">{r.nextAction} · due {r.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (r) => r.owner },
            ]}
            emptyBody="No attendance corrections match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
