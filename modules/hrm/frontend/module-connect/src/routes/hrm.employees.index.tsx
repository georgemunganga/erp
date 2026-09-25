/**
 * M18 — Employer-side employee directory with live admin CRUD.
 *
 * Real mode (VITE_USE_REAL_API=true): the list is fetched from the ASP.NET HRM
 * backend (GET /hrm/workers) with the filters passed as query parameters. HR
 * can search, filter by status and employment type, surface archived leavers,
 * and archive a worker in one step (POST /hrm/workers/{id}/archive — roles
 * hr_ops/hr_admin enforced on the server).
 *
 * The ListPage component filters its input client-side, so the page feeds it
 * the rows that the server already scoped (archived toggle, status, type) and
 * the in-page search doubles as the backend `search` param via a debounced
 * state the query depends on.
 *
 * Mock mode is untouched so the demo still renders its seeded catalogue.
 */
import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Archive, ChevronLeft, ChevronRight, FileSpreadsheet, Pencil, UserPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import type { ColumnDef } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { feedback } from "@/platform/feedback";
import { useAuth } from "@/platform/auth";
import { entities } from "@/mock/data";
import {
  demoEntityTree,
  flattenEntityTree,
  treePathLabel,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { api } from "@/mock/service";
import type { Employee } from "@/mock/types";
import { useMock } from "@/platform/use-mock";
import { adaptWorkers, realApi, useApi } from "@/platform/use-api";
import { ExportButton } from "@/platform/components/ImportExport/ExportButton";

export const Route = createFileRoute("/hrm/employees/")({
  head: () => ({
    meta: [
      { title: "Employees — HRM" },
      { name: "description", content: "Filterable employee directory across entities, branches and employment types." },
      { property: "og:title", content: "Employees — HRM" },
      { property: "og:description", content: "Filterable employee directory across entities, branches and employment types." },
    ],
  }),
  component: EmployeesPage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/** Admin roles that may archive a worker record (mirrors the backend). */
const ARCHIVE_ROLES = new Set(["hr_admin", "hr_ops"]);

const MOCK_TYPE_OPTIONS = ["Permanent", "Fixed term", "Contractor", "Intern", "Part time"];
const EMPLOYEE_PAGE_SIZE = 25;

/** Extended row kept in sync with the backend's WorkerDto fields we need. */
type EmployeeRow = Employee & { rawId?: string; isArchived?: boolean; rawStatus?: string };

function EmployeesPage() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [typeFilter, setTypeFilter] = useState<string>("");
  const [archived, setArchived] = useState(false);
  const [archiveTarget, setArchiveTarget] = useState<EmployeeRow | null>(null);
  const [archiving, setArchiving] = useState(false);
  const [page, setPage] = useState(1);

  // Mock-mode view chip (demo behaviour only — real mode uses the backend).
  const [view, setView] = useState("all");

  // Real backend: filters ride on the query params the ASP.NET list endpoint
  // already honours (search, status, workerType, includeArchived).
  const state = useApi(
    () =>
      USE_REAL
        ? realApi
            .employees({
              ...(search ? { search } : {}),
              ...(statusFilter ? { status: statusFilter } : {}),
              ...(typeFilter ? { workerType: typeFilter } : {}),
              ...(archived ? { includeArchived: "true" } : {}),
              page,
              pageSize: EMPLOYEE_PAGE_SIZE,
            })
            .then(
              (result) => ({
                items: (Array.from(result.items ?? []) as Array<Record<string, unknown>>).map(
                  (raw) =>
                    ({
                      ...(adaptWorkers([raw])[0] ?? ({} as Employee)),
                      rawId: String(raw.id ?? ""),
                      rawStatus: String(raw.status ?? ""),
                      isArchived: Boolean(raw.isArchived),
                    }) as EmployeeRow,
                ) as EmployeeRow[],
                totalCount: Number(result.totalCount ?? 0),
                page: Number((result as { page?: number }).page ?? page),
                pageSize: Number((result as { pageSize?: number }).pageSize ?? EMPLOYEE_PAGE_SIZE),
              }),
            )
        : Promise.resolve({ items: [] as EmployeeRow[], totalCount: 0, page: 1, pageSize: EMPLOYEE_PAGE_SIZE }),
    [search, statusFilter, typeFilter, archived, page],
  );

  const mockState = useMock(() => api.employees());
  const listState = USE_REAL ? { ...state, data: state.data?.items ?? null } : mockState;
  const rows: EmployeeRow[] = USE_REAL ? (state.data?.items ?? []) : mockState.data ?? [];
  const totalCount = USE_REAL ? (state.data?.totalCount ?? 0) : rows.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / EMPLOYEE_PAGE_SIZE));
  const pageStart = totalCount === 0 ? 0 : (page - 1) * EMPLOYEE_PAGE_SIZE + 1;
  const pageEnd = Math.min(page * EMPLOYEE_PAGE_SIZE, totalCount);

  const treeState = useApi<OrgTreeNode[]>(async () => {
    if (USE_REAL) return (await realApi.entityTree()) as OrgTreeNode[];
    return demoEntityTree;
  }, []);
  const entityTreeOptions = treeToSelectOptions(treeState.data ?? []).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));
  const entityUnits = flattenEntityTree(treeState.data ?? []);
  const unitByName = new Map(entityUnits.map((e) => [e.unitName, e]));

  const userRoles = new Set(useAuth().user?.roles ?? []);
  const showArchiveAction = !USE_REAL || userRoles.has("hr_admin") || userRoles.has("hr_ops");

  const views = USE_REAL
    ? [
        { id: "all", label: archived ? "Archived" : "All employees" },
        { id: "active", label: "Active only" },
        { id: "archived", label: archived ? "All employees" : "Archived" },
      ]
    : [
        { id: "all", label: "All employees" },
        { id: "active", label: "Active only" },
        { id: "ending", label: "Contracts ending" },
        { id: "prehire", label: "Pre-hire" },
      ];

  const columns: ColumnDef<EmployeeRow>[] = [
    {
      id: "name",
      header: "Employee",
      cell: (e) => (
        <div className="min-w-0 max-w-64">
          <Link
            to="/hrm/employees/$id"
            params={{ id: e.rawId ?? e.id }}
            className="block truncate font-medium text-primary underline-offset-2 hover:underline"
          >
            {e.fullName}
          </Link>
          <span className="block truncate text-xs text-muted-foreground">{e.employeeNo}</span>
        </div>
      ),
    },
    { id: "title", header: "Job title", cell: (e) => <span className="block max-w-56 truncate">{e.jobTitle}</span> },
    { id: "dept", header: "Department", cell: (e) => e.department },
    {
      id: "entity",
      header: "Entity",
      cell: (e) =>
        USE_REAL
          ? (unitByName.get(e.department)?.entityName ?? e.department).split(" ").slice(0, 2).join(" ")
          : entities.find((x) => x.id === e.entityId)?.name.split(" ").slice(0, 2).join(" "),
    },
    {
      id: "branch",
      header: "Branch",
      cell: (e) => e.branch,
    },
    {
      id: "type",
      header: "Type",
      cell: (e) => {
        const t = e.employmentType;
        const map: Record<string, string> = {
          employee: "Permanent",
          contingent: "Contractor",
          intern: "Intern",
          volunteer: "Part time",
        };
        return USE_REAL ? (map[t] ?? t) : t;
      },
    },
    {
      id: "status",
      header: "Status",
      cell: (e) => {
        const label = e.isArchived ? "Archived" : USE_REAL ? labelize(e.rawStatus) : e.status;
        return <StatusBadge status={label} />;
      },
    },
    { id: "grade", header: "Grade", defaultVisible: false, cell: (e) => e.grade },
    {
      id: "start",
      header: "Start date",
      defaultVisible: false,
      cell: (e) => e.startDate,
    },
    {
      id: "email",
      header: "Work email",
      defaultVisible: false,
      cell: (e) => (e.email ? e.email : <span className="text-muted-foreground">Not recorded</span>),
    },
    ...(showArchiveAction
      ? ([
          {
            id: "actions",
            header: "Actions",
            defaultVisible: USE_REAL,
            cell: (e) => (
              <div className="flex flex-wrap items-center gap-2">
                <Button size="sm" variant="outline" asChild>
                  <Link to="/hrm/employees/$id/edit" params={{ id: e.rawId ?? e.id }}>
                    <Pencil className="mr-1 size-3.5" aria-hidden />
                    Edit
                  </Link>
                </Button>
                <Button size="sm" variant="outline" disabled={e.isArchived} onClick={() => setArchiveTarget(e)}>
                  <Archive className="mr-1 size-3.5" aria-hidden />
                  {e.isArchived ? "Archived" : "Archive"}
                </Button>
              </div>
            ),
          },
        ] as ColumnDef<EmployeeRow>[])
      : []),
  ];

  // Real-mode view chips flip the archived filter at the API.
  const handleView = (next: string) => {
    setView(next);
    if (USE_REAL) {
      setArchived(next === "archived");
      setPage(1);
    }
  };

  const clientFilters = USE_REAL
    ? [
        {
          id: "status",
          label: "Status",
          options: ["Active", "On leave", "Notice period", "Pre-hire", "Terminated", "Archived"],
          match: (e: EmployeeRow, v: string) =>
            e.isArchived ? v === "Archived" : labelize(e.rawStatus) === v,
        },
        {
          id: "type",
          label: "Type",
          options: ["Permanent", "Contractor", "Intern", "Volunteer"],
          match: (e: EmployeeRow, v: string) =>
            ({
              Permanent: "employee",
              Contractor: "contingent",
              Intern: "intern",
              Volunteer: "volunteer",
            })[v] === e.employmentType,
        },
        {
          id: "entity",
          label: "Entity & branch",
          options: treeToSelectOptions(treeState.data ?? []).map((o) => o.value),
          treeOptions: entityTreeOptions,
          match: (e: EmployeeRow, v: string) =>
            v.startsWith("entity:")
              ? unitByName.get(e.department)?.entityId === v.slice(7)
              : e.department === v,
        },
      ]
    : [
        {
          id: "type",
          label: "Employment type",
          options: MOCK_TYPE_OPTIONS,
          match: (e: EmployeeRow, v: string) => e.employmentType === v,
        },
        {
          id: "status",
          label: "Status",
          options: ["Active", "On leave", "Notice period", "Pre-hire"],
          match: (e: EmployeeRow, v: string) => e.status === v,
        },
      ];

  const filtered = USE_REAL
    ? rows
    : rows.filter((e) =>
        view === "active"
          ? e.status === "Active"
          : view === "ending"
            ? Boolean(e.endDate)
            : view === "prehire"
              ? e.status === "Pre-hire"
              : true,
      );

  const archiveCount = rows.filter((r) => r.isArchived).length;

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="People"
          title="Employees"
          description={
            USE_REAL
              ? archived
                ? "Showing archived leavers — switch back to see the active roster."
                : `Company-wide employee records.${archiveCount > 0 ? ` ${archiveCount} archived leaver${archiveCount === 1 ? "" : "s"} can be surfaced with the Archived filter.` : ""} Add one at a time or import a batch from CSV.`
              : "Everyone in scope for your entity and branch access. Select rows for bulk actions."
          }
          primaryAction={
            <div className="flex items-center gap-2">
              <Button asChild variant="outline">
                <Link to="/hrm/employees/import">
                  <FileSpreadsheet className="mr-1 size-4" aria-hidden />
                  Import
                </Link>
              </Button>
              <ExportButton
                typeKey="workers"
                fileName="employees"
                filter={[
                  statusFilter ? `status=${statusFilter}` : "",
                  typeFilter ? `workerType=${typeFilter}` : "",
                  archived ? "includeArchived=true" : "",
                ].filter(Boolean).join("&") || undefined}
              />
              <Button asChild>
                <Link to="/hrm/employees/new">
                  <UserPlus className="mr-1 size-4" aria-hidden />
                  Add employee
                </Link>
              </Button>
            </div>
          }
        />

        <div className="mt-4 flex flex-wrap items-center gap-2">
          {views.map((v) => (
            <Button
              key={v.id}
              size="sm"
              variant={view === v.id ? "default" : "outline"}
              onClick={() => handleView(v.id)}
            >
              {v.label}
            </Button>
          ))}
        </div>

        <Async state={listState} rows={6}>
          {(rendered) => {
            const renderedRows: EmployeeRow[] = rendered;
            return (
              <div className="space-y-4">
                <ListPage
                  rows={renderedRows}
                  columns={columns}
                  savedViews={views}
                  activeView={view}
                  onViewChange={handleView}
                  searchPlaceholder={USE_REAL ? "Search name, employee number, NRC or email" : "Search name, number or job title"}
                  searchFields={(e) => `${e.fullName} ${e.employeeNo} ${e.jobTitle} ${e.email ?? ""} ${e.nationalId ?? ""}`}
                  filters={clientFilters}
                  emptyBody={
                    archived
                      ? "No archived employees — leavers will surface here when HR archives them."
                      : "No employees found for the current filters."
                  }
                  rowHref={(e) => (
                    <Link
                      to="/hrm/employees/$id"
                      params={{ id: e.rawId ?? e.id }}
                      className="text-xs font-medium text-primary underline underline-offset-2"
                    >
                      Open
                    </Link>
                  )}
                />
                {USE_REAL ? (
                  <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-card px-3 py-2 text-sm">
                    <span className="text-muted-foreground">
                      Showing {pageStart}-{pageEnd} of {totalCount} employees
                    </span>
                    <div className="flex items-center gap-2">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={page <= 1}
                        onClick={() => setPage((current) => Math.max(1, current - 1))}
                      >
                        <ChevronLeft className="mr-1 size-4" aria-hidden />
                        Previous
                      </Button>
                      <span className="min-w-24 text-center text-xs text-muted-foreground">
                        Page {page} of {totalPages}
                      </span>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        disabled={page >= totalPages}
                        onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
                      >
                        Next
                        <ChevronRight className="ml-1 size-4" aria-hidden />
                      </Button>
                    </div>
                  </div>
                ) : null}
              </div>
            );
          }}
        </Async>

        {archiveTarget && (
          <ArchiveDialog
            row={archiveTarget}
            busy={archiving}
            onClose={() => setArchiveTarget(null)}
            onArchive={async () => {
              setArchiving(true);
              try {
                await realApi.archiveWorker(archiveTarget.rawId ?? archiveTarget.id);
                setArchiveTarget(null);
                feedback.saved(`${archiveTarget.fullName} archived.`);
                state.reload();
              } catch (e) {
                feedback.blocked(
                  `${archiveTarget.fullName} could not be archived.`,
                  e instanceof Error ? e.message : "Check the error and try again.",
                );
              } finally {
                setArchiving(false);
              }
            }}
          />
        )}
      </AppShell>
    </AuthGate>
  );
}

/** Backend status values → human labels. */
function labelize(status: string | undefined) {
  const map: Record<string, string> = {
    active: "Active",
    "on-leave": "On leave",
    notice: "Notice period",
    "pre-hire": "Pre-hire",
    terminated: "Terminated",
    archived: "Archived",
  };
  return status ? (map[status] ?? status) : "Active";
}

function ArchiveDialog({
  row,
  busy,
  onClose,
  onArchive,
}: {
  row: EmployeeRow;
  busy: boolean;
  onClose: () => void;
  onArchive: () => Promise<void>;
}) {
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Archive {row.fullName}?</DialogTitle>
          <DialogDescription>
            The record becomes a historical one: it disappears from the active roster and can no longer be
            edited, but stays in the system for payroll history and reporting.
          </DialogDescription>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          <strong>{row.employeeNo}</strong> — {row.jobTitle || "no title recorded"}. You can surface archived
          records any time with the Archived filter.
        </p>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={onArchive} disabled={busy}>
            <Archive className="mr-1 size-4" aria-hidden />
            {busy ? "Archiving…" : "Archive"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
