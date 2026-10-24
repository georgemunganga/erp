import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { realApi, useApi, type ReportingLineDto } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import type { ColumnDef } from "@/platform/components/ListPage";

export const Route = createFileRoute("/hrm/reporting")({
  head: () => ({
    meta: [
      { title: "Reporting lines — Newworldcargo HRM" },
      { name: "description", content: "Define who reports to whom: set and change reporting lines per worker, unit or search." },
    ],
  }),
  component: ReportingPage,
});

type ReportingLine = ReportingLineDto & { id: string };

interface ReportingLineList {
  items: ReportingLine[];
  total: number;
}

interface ManagerOption {
  id: string;
  employeeNo: string;
  fullName: string;
  jobTitle: string;
  orgUnitName: string;
}

/** Search workers to use as managers via the workers search endpoint. */
async function searchManagers(query: string): Promise<ManagerOption[]> {
  try {
    const res = await realApi.employees({ search: query, page: 1, pageSize: 10 });
    const items = (res.items ?? []) as Array<Record<string, unknown>>;
    return items.map((w) => ({
      id: String(w.id ?? ""),
      employeeNo: String(w.employeeNo ?? ""),
      fullName: String(w.fullName ?? ""),
      jobTitle: String(w.jobTitle ?? ""),
      orgUnitName: String(w.orgUnitName ?? ""),
    }));
  } catch {
    return [];
  }
}

function ChangeManagerDialog({
  rows,
  initialManagerId,
  onDone,
}: {
  rows: ReportingLine[];
  initialManagerId: string | null;
  onDone: () => void;
}) {
  const [query, setQuery] = useState("");
  const [options, setOptions] = useState<ManagerOption[]>([]);
  const [picked, setPicked] = useState<string | null>(initialManagerId);
  const [pickedOption, setPickedOption] = useState<ManagerOption | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const isBulk = rows.length > 1;

  async function lookup(value: string) {
    setQuery(value);
    if (value.trim().length < 2) {
      setOptions([]);
      return;
    }
    const found = await searchManagers(value.trim());
    setOptions(found);
  }

  async function save() {
    setBusy(true);
    setFailure(null);
    try {
      await realApi.updateReportingLines({
        workerIds: rows.map((r) => r.workerId),
        managerId: picked,
        reason: isBulk ? `Bulk reporting-line update for ${rows.length} worker(s)` : undefined,
      });
      onDone();
    } catch (e) {
      setFailure(e instanceof Error ? e.message : "Failed to update reporting line.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onDone()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{isBulk ? "Change reporting line" : "Set reporting line"}</DialogTitle>
          <DialogDescription>
            {isBulk
              ? `Applying to ${rows.length} selected worker(s): ${rows.map((r) => r.fullName).join(", ")}`
              : `Choose a manager for ${rows[0].fullName}. Their current assignment must be active.`}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="manager-search">Search for a manager</Label>
            <Input
              id="manager-search"
              placeholder="Type a name (min. 2 characters)…"
              value={query}
              onChange={(e) => void lookup(e.target.value)}
              autoComplete="off"
            />
            <ul className="max-h-48 overflow-auto rounded-md border bg-background">
              {options.map((o) => (
                <li key={o.id}>
                  <button
                    type="button"
                    className={`w-full px-3 py-2 text-left text-sm hover:bg-muted ${
                      picked === o.id ? "bg-muted font-medium" : ""
                    }`}
                    onClick={() => {
                      setPicked(o.id);
                      setPickedOption(o);
                      setQuery(`${o.fullName} (${o.employeeNo})`);
                      setOptions([]);
                    }}
                  >
                    <span className="block font-medium">{o.fullName}</span>
                    <span className="block text-xs text-muted-foreground">
                      {o.jobTitle || "—"} · {o.orgUnitName || "—"}
                    </span>
                  </button>
                </li>
              ))}
              {options.length === 0 && query.trim().length >= 2 ? (
                <li className="px-3 py-2 text-xs text-muted-foreground">No matches yet — keep typing.</li>
              ) : null}
            </ul>
            <Select
              value={picked === null ? "none" : "set"}
              onValueChange={(v) => {
                if (v === "none") {
                  setPicked(null);
                  setPickedOption(null);
                  setQuery("");
                }
              }}
            >
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Or pick an option…" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Remove reporting line (no manager)</SelectItem>
              </SelectContent>
            </Select>
          </div>
          {failure ? (
            <p className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">{failure}</p>
          ) : null}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onDone} disabled={busy}>Cancel</Button>
          <Button onClick={save} disabled={busy || picked === undefined}>
            {busy ? "Saving…" : "Save reporting line"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ReportingPage() {
  const { user } = useAuth();
  const permitted = import.meta.env.VITE_USE_REAL_API !== "true" || user?.roles.some((role) => role === "hr_ops" || role === "hr_admin");
  return <AppShell><AuthGate>{permitted ? <ReportingContent /> : <p role="alert">Reporting-line access is not assigned.</p>}</AuthGate></AppShell>;
}

function ReportingContent() {
  const [orgUnitId, setOrgUnitId] = useState<string | undefined>(undefined);
  const state = useApi<ReportingLineList>(
    async () => {
      const result = await realApi.reportingLines(orgUnitId ? { orgUnitId } : {});
      return { ...result, items: result.items.map((row) => ({ ...row, id: row.workerId })) };
    },
    [orgUnitId],
  );
  const [dialogRows, setDialogRows] = useState<ReportingLine[] | null>(null);

  // Units used in rows become the filter options (keeps the filter in sync
  // with what actually exists in the result set).
  const units = state.data?.items
    ? Array.from(
        new Map(
          state.data.items
            .filter((r) => r.orgUnitId && r.orgUnitName)
            .map((r) => [r.orgUnitId, r.orgUnitName] as [string, string]),
        ),
      ).map(([id, name]) => ({ id, name }))
    : [];

  const columns: ColumnDef<ReportingLine>[] = [
    {
      id: "employee",
      header: "Employee",
      cell: (r) => (
        <div>
          <p className="font-medium">{r.fullName}</p>
          <p className="text-xs text-muted-foreground">{r.employeeNo}</p>
        </div>
      ),
      defaultVisible: true,
    },
    {
      id: "unit",
      header: "Unit",
      cell: (r) => (
        <div>
          <p>{r.orgUnitName}</p>
          {r.managerNamePath ? (
            <p className="text-xs text-muted-foreground">{r.managerNamePath}</p>
          ) : null}
        </div>
      ),
      defaultVisible: true,
    },
    {
      id: "role",
      header: "Role",
      cell: (r) => (
        <div>
          <p>{r.jobTitle || "—"}</p>
          <p className="text-xs text-muted-foreground">Grade {r.grade || "—"}</p>
        </div>
      ),
      defaultVisible: true,
    },
    {
      id: "status",
      header: "Status",
      cell: (r) => <StatusBadge status={r.status} />,
      defaultVisible: true,
    },
    {
      id: "manager",
      header: "Reports to",
      cell: (r) => (r.managerName ? <p className="font-medium">{r.managerName}</p> : <p className="italic text-muted-foreground">No manager</p>),
      defaultVisible: true,
    },
  ];

  const filters = [
    {
      id: "unit",
      label: "Unit",
      options: units.map((u) => u.name),
      match: (row: ReportingLine, value: string) => row.orgUnitName === value,
    },
    {
      id: "manager",
      label: "Has manager",
      options: ["Yes", "No"],
      match: (row: ReportingLine, value: string) =>
        value === "Yes" ? row.managerId !== null : row.managerId === null,
    },
  ];

  return (
    <>
        <PageHeader
          eyebrow="Organisation"
          title="Reporting lines"
          description="Who reports to whom. Select one or more workers and set their manager, or remove a reporting line."
        />
        <div className="mt-4 flex items-center gap-2">
          <Select
            value={orgUnitId ?? "all"}
            onValueChange={(v) => {
              setOrgUnitId(v === "all" ? undefined : v);
            }}
          >
            <SelectTrigger className="w-64">
              <SelectValue placeholder="Filter by unit" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All units</SelectItem>
              {units.map((u) => (
                <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <Async state={state}>
          {(list) => (
            <ListPage<ReportingLine>
              rows={list.items}
              columns={columns}
              filters={filters}
              searchFields={(r) => `${r.fullName} ${r.employeeNo} ${r.orgUnitName} ${r.managerName ?? ""} ${r.jobTitle ?? ""}`}
              searchPlaceholder="Search employees, units, managers…"
              bulkActions={[
                {
                  label: "Change reporting line…",
                  onSelect: (ids) => {
                    const picked = list.items.filter((r) => ids.includes(r.workerId));
                    if (picked.length > 0) setDialogRows(picked);
                  },
                },
              ]}
              rowHref={(r) => (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setDialogRows([r])}
                  onPointerDown={(e) => e.stopPropagation()}
                >
                  {r.managerId ? "Change manager" : "Set manager"}
                </Button>
              )}
            />
          )}
        </Async>
        {dialogRows ? (
          <ChangeManagerDialog
            rows={dialogRows}
            initialManagerId={dialogRows.length === 1 ? dialogRows[0].managerId : null}
            onDone={() => {
              setDialogRows(null);
              state.reload();
            }}
          />
        ) : null}
    </>
  );
}
