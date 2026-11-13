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

export const Route = createFileRoute("/hrm/performance/")({
  head: () => ({
    meta: [
      { title: "Performance cycles — Kuubu HRM" },
      { name: "description", content: "Manage review cycles, goals and assessments for every employee." },
    ],
  }),
  component: PerformanceList,
});

interface PerformanceCycle {
  id: string;
  name: string;
  periodType: string;
  startDate: string;
  endDate: string;
  status: string;
  goalCount: number;
  assessmentCount: number;
}

function adaptCycles(rows: unknown[]): PerformanceCycle[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    return {
      id: String(r.id ?? ""),
      name: String(r.name ?? ""),
      periodType: String(r.periodType ?? "annual"),
      startDate: String(r.startDate ?? ""),
      endDate: String(r.endDate ?? ""),
      status: String(r.status ?? "draft"),
      goalCount: Number(r.goalCount ?? 0),
      assessmentCount: Number(r.assessmentCount ?? 0),
    };
  });
}

const periodLabels: Record<string, string> = {
  annual: "Annual",
  "semi-annual": "Semi-annual",
  quarterly: "Quarterly",
  custom: "Custom",
};

const statusLabels: Record<string, string> = {
  draft: "Draft",
  active: "Active",
  assessments_open: "Assessments open",
  assessments_due: "Assessments due",
  completed: "Completed",
  closed: "Closed",
};

function PerformanceList() {
  const state = useApi(
    async () => {
      const rows = await realApi.performanceCycles();
      return adaptCycles(rows);
    },
    [],
  );
  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState({
    name: "",
    periodType: "annual",
    startDate: "",
    endDate: "",
  });

  const columns: ColumnDef<PerformanceCycle>[] = [
    {
      id: "name",
      header: "Cycle",
      cell: (row) => (
        <div>
          <Link
            to="/hrm/performance/$id"
            params={{ id: row.id }}
            className="font-medium text-primary hover:underline"
          >
            {row.name}
          </Link>
          <p className="text-xs text-muted-foreground">{periodLabels[row.periodType] ?? row.periodType}</p>
        </div>
      ),
    },
    {
      id: "period",
      header: "Period",
      cell: (row) => (
        <span className="text-sm text-muted-foreground">{row.startDate} → {row.endDate}</span>
      ),
    },
    {
      id: "status",
      header: "Status",
      cell: (row) => <StatusBadge status={statusLabels[row.status] ?? row.status} />,
    },
    {
      id: "goals",
      header: "Goals",
      defaultVisible: false,
      cell: (row) => <span className="text-sm tabular-nums">{row.goalCount}</span>,
    },
    {
      id: "assessments",
      header: "Assessments",
      cell: (row) => <span className="text-sm tabular-nums">{row.assessmentCount}</span>,
    },
  ];

  const handleCreate = async () => {
    try {
      await realApi.createPerformanceCycle({
        name: form.name,
        periodType: form.periodType,
        startDate: form.startDate,
        endDate: form.endDate,
      });
      setCreateOpen(false);
      state.reload();
    } catch (err) {
      console.error("Failed to create cycle", err);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Performance"
          title="Performance cycles"
          description="Review cycles define the period in which employees set goals and are assessed against them."
          primaryAction={
            <Button onClick={() => setCreateOpen(true)}>New cycle</Button>
          }
        />
        <Async state={state}>
          {(rows) => (
            <ListPage<PerformanceCycle>
              rows={rows}
              columns={columns}
              searchPlaceholder="Search cycle or status"
              searchFields={(r) => `${r.name} ${r.status} ${r.periodType}`}
              filters={[
                {
                  id: "status",
                  label: "Status",
                  options: Object.keys(statusLabels),
                  match: (r, v) => r.status === v,
                },
              ]}
              rowHref={(row) => `/hrm/performance/${row.id}`}
              emptyBody="No performance cycles found."
            />
          )}
        </Async>
        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogContent className="max-w-md">
            <DialogHeader>
              <DialogTitle>New performance cycle</DialogTitle>
              <DialogDescription>Define the review period for goal-setting and assessments.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <div className="grid gap-2">
                <Label htmlFor="cycle-name">Cycle name</Label>
                <Input
                  id="cycle-name"
                  placeholder="e.g. Annual Review 2026"
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="cycle-period">Period type</Label>
                <Select value={form.periodType} onValueChange={(v) => setForm({ ...form, periodType: v })}>
                  <SelectTrigger id="cycle-period">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="annual">Annual</SelectItem>
                    <SelectItem value="semi-annual">Semi-annual</SelectItem>
                    <SelectItem value="quarterly">Quarterly</SelectItem>
                    <SelectItem value="custom">Custom</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="grid gap-2">
                  <Label htmlFor="cycle-start">Start date</Label>
                  <Input
                    id="cycle-start"
                    type="date"
                    value={form.startDate}
                    onChange={(e) => setForm({ ...form, startDate: e.target.value })}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="cycle-end">End date</Label>
                  <Input
                    id="cycle-end"
                    type="date"
                    value={form.endDate}
                    onChange={(e) => setForm({ ...form, endDate: e.target.value })}
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setCreateOpen(false)}>Cancel</Button>
              <Button onClick={handleCreate} disabled={!form.name || !form.startDate || !form.endDate}>
                Create cycle
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </AppShell>
    </AuthGate>
  );
}
