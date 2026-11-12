import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import type { ColumnDef } from "@/platform/components/ListPage";

export const Route = createFileRoute("/hrm/offboarding/")({
  head: () => ({
    meta: [
      { title: "Offboarding — Kuubu HRM" },
      { name: "description", content: "Manage employee exits, checklist items and exit interviews." },
    ],
  }),
  component: OffboardingList,
});

interface OffboardingRequest {
  id: string;
  workerId: string;
  workerFullName: string;
  workerEmployeeNo: string;
  requestType: string;
  reason: string;
  noticeStartDate: string;
  lastWorkingDay: string;
  status: string;
  approvedBy: string | null;
  approverName: string | null;
  approvedAt: string | null;
  rejectionReason: string | null;
  cancelledReason: string | null;
  isFinalPayProcessed: boolean;
  checklistItemsCompleted: number;
  checklistItemsTotal: number;
  createdAt: string;
}

interface OffboardingResponse {
  items: OffboardingRequest[];
  total: number;
}

function adaptRequests(rows: unknown[]): OffboardingRequest[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    return {
      id: String(r.id ?? ""),
      workerId: String(r.workerId ?? ""),
      workerFullName: String(r.workerFullName ?? ""),
      workerEmployeeNo: String(r.workerEmployeeNo ?? ""),
      requestType: String(r.requestType ?? "resignation"),
      reason: String(r.reason ?? ""),
      noticeStartDate: String(r.noticeStartDate ?? ""),
      lastWorkingDay: String(r.lastWorkingDay ?? ""),
      status: String(r.status ?? "requested"),
      approvedBy: r.approvedBy ? String(r.approvedBy) : null,
      approverName: r.approverName ? String(r.approverName) : null,
      approvedAt: r.approvedAt ? String(r.approvedAt) : null,
      rejectionReason: r.rejectionReason ? String(r.rejectionReason) : null,
      cancelledReason: r.cancelledReason ? String(r.cancelledReason) : null,
      isFinalPayProcessed: Boolean(r.isFinalPayProcessed),
      checklistItemsCompleted: Number(r.checklistItemsCompleted ?? 0),
      checklistItemsTotal: Number(r.checklistItemsTotal ?? 0),
      createdAt: String(r.createdAt ?? ""),
    };
  });
}

const typeLabels: Record<string, string> = {
  resignation: "Resignation",
  termination: "Termination",
  retirement: "Retirement",
  redundancy: "Redundancy",
};

const statusLabels: Record<string, string> = {
  requested: "Requested",
  approved: "Approved",
  in_progress: "In progress",
  completed: "Completed",
  cancelled: "Cancelled",
};

function OffboardingList() {
  const state = useApi(
    async () => {
      const rows = await realApi.offboardingRequests();
      return adaptRequests(rows);
    },
    [],
  );
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState({
    workerId: "",
    requestType: "resignation",
    reason: "",
    noticeStartDate: "",
    lastWorkingDay: "",
  });

  const columns: ColumnDef<OffboardingRequest>[] = [
    {
      id: "worker",
      header: "Employee",
      cell: (row) => (
        <div>
          <Link
            to="/hrm/offboarding/$id"
            params={{ id: row.id }}
            className="font-medium text-primary hover:underline"
          >
            {row.workerFullName}
          </Link>
          <p className="text-xs text-muted-foreground">{row.workerEmployeeNo}</p>
        </div>
      ),
    },
    {
      id: "type",
      header: "Type",
      cell: (row) => <span className="text-sm">{typeLabels[row.requestType] ?? row.requestType}</span>,
    },
    {
      id: "status",
      header: "Status",
      cell: (row) => <StatusBadge status={statusLabels[row.status] ?? row.status} />,
    },
    {
      id: "lastWorkingDay",
      header: "Last working day",
      cell: (row) => <span className="text-sm tabular-nums">{row.lastWorkingDay || "—"}</span>,
    },
    {
      id: "checklist",
      header: "Checklist",
      cell: (row) => (
        <span className="text-sm tabular-nums">
          {row.checklistItemsCompleted}/{row.checklistItemsTotal}
        </span>
      ),
    },
    {
      id: "finalPay",
      header: "Final pay",
      cell: (row) => (
        <span className="text-sm">{row.isFinalPayProcessed ? "Processed" : "Pending"}</span>
      ),
    },
    {
      id: "createdAt",
      header: "Created",
      cell: (row) => <span className="text-sm text-muted-foreground">{row.createdAt}</span>,
    },
  ];

  const handleCreate = async () => {
    try {
      await realApi.createOffboarding({
        workerId: form.workerId,
        requestType: form.requestType,
        reason: form.reason,
        noticeStartDate: form.noticeStartDate,
        lastWorkingDay: form.lastWorkingDay,
      });
      setCreateOpen(false);
      state.reload();
    } catch (err) {
      console.error("Failed to create offboarding request", err);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Offboarding"
          title="Offboarding requests"
          description="Track employee exits, manage exit checklists and conduct exit interviews."
          primaryAction={
            <Button onClick={() => setCreateOpen(true)}>New offboarding</Button>
          }
        />
        <Async state={state}>
          {(rows) => (
            <ListPage<OffboardingRequest>
              rows={rows}
              columns={columns}
              searchPlaceholder="Search employee, type or status"
              searchFields={(r) => `${r.workerFullName} ${r.workerEmployeeNo} ${r.requestType} ${r.status}`}
              filters={[
                {
                  id: "status",
                  label: "Status",
                  options: Object.keys(statusLabels),
                  match: (r, v) => r.status === v,
                },
              ]}
              rowHref={(row) => `/hrm/offboarding/${row.id}`}
              emptyBody="No offboarding requests found."
            />
          )}
        </Async>
        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogContent className="max-w-md">
            <DialogHeader>
              <DialogTitle>New offboarding request</DialogTitle>
              <DialogDescription>Initiate the exit process for an employee.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <div className="grid gap-2">
                <Label htmlFor="offboard-worker">Employee ID</Label>
                <Input
                  id="offboard-worker"
                  placeholder="Worker GUID"
                  value={form.workerId}
                  onChange={(e) => setForm({ ...form, workerId: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="offboard-type">Request type</Label>
                <Select value={form.requestType} onValueChange={(v) => setForm({ ...form, requestType: v })}>
                  <SelectTrigger id="offboard-type">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="resignation">Resignation</SelectItem>
                    <SelectItem value="termination">Termination</SelectItem>
                    <SelectItem value="retirement">Retirement</SelectItem>
                    <SelectItem value="redundancy">Redundancy</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="offboard-reason">Reason</Label>
                <Input
                  id="offboard-reason"
                  placeholder="Reason for departure"
                  value={form.reason}
                  onChange={(e) => setForm({ ...form, reason: e.target.value })}
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="grid gap-2">
                  <Label htmlFor="offboard-notice-start">Notice start</Label>
                  <Input
                    id="offboard-notice-start"
                    type="date"
                    value={form.noticeStartDate}
                    onChange={(e) => setForm({ ...form, noticeStartDate: e.target.value })}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="offboard-last-day">Last working day</Label>
                  <Input
                    id="offboard-last-day"
                    type="date"
                    value={form.lastWorkingDay}
                    onChange={(e) => setForm({ ...form, lastWorkingDay: e.target.value })}
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
              <Button onClick={handleCreate} disabled={!form.workerId || !form.requestType}>
                Create offboarding
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </AppShell>
    </AuthGate>
  );
}
