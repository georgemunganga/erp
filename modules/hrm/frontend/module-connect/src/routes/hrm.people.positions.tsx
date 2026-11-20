import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { AlertTriangle, BadgeCheck } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { entities } from "@/mock/data";
import {
  demoEntityTree,
  fetchOrgUnits,
  flattenEntityTree,
  treePathLabel,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import type { Position } from "@/mock/structure";
import {
  licenceAttention,
  positionIncumbentName,
  shortEntityName,
  structureApi,
} from "@/mock/structure";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import type { ColumnDef } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/people/positions")({
  head: () => ({
    meta: [
      { title: "Positions — Kuubu HRM" },
      {
        name: "description",
        content:
          "Establishment register of positions: vacant, filled, frozen and closed, with incumbency, funding and mandatory licence status.",
      },
      { property: "og:title", content: "Positions — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Establishment register of positions: vacant, filled, frozen and closed, with incumbency, funding and mandatory licence status.",
      },
    ],
  }),
  component: PositionsRoute,
});

/** The position detail route nests under this one, so hand over when a child matches. */
function PositionsRoute() {
  const children = useChildMatches();
  return children.length ? <Outlet /> : <PositionsList />;
}

const views = [
  { id: "all", label: "All positions" },
  { id: "open", label: "Vacant and frozen" },
  { id: "attention", label: "Critical and licensed" },
  { id: "establishment", label: "Off establishment or unfunded" },
  { id: "filled", label: "Filled" },
];

const matchView = (p: Position, view: string) => {
  if (view === "open") return p.status === "Vacant" || p.status === "Frozen";
  if (view === "attention") return p.critical || Boolean(p.licence);
  if (view === "establishment") return !p.withinEstablishment || !p.funded;
  if (view === "filled") return p.status === "Filled";
  return true;
};

function EstablishmentFlag({ position }: { position: Position }) {
  if (!position.withinEstablishment) {
    return <span className="text-warning">Outside establishment</span>;
  }
  return position.funded ? (
    <span>Funded, within establishment</span>
  ) : (
    <span className="text-warning">Within establishment, not funded</span>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/** Live jobs-catalogue row adapted to the positions UI shape. */
interface JobRow extends Position {}

function adaptJob(j: Record<string, unknown>, units: Record<string, string>): JobRow {
  const id = String(j.id ?? "");
  return {
    id,
    positionNo: String(j.code ?? ""),
    jobTitle: String(j.title ?? ""),
    grade: j.grade ? String(j.grade) : "—",
    entityId: String(j.legalEntityId ?? ""),
    branch: "—",
    department: units[String(j.orgUnitId ?? "")] ?? "—",
    team: "",
    incumbentId: null,
    status: String(j.status ?? "active") === "inactive" ? "Closed" : "Vacant",
    critical: false,
    licence: null,
    fte: 1,
    withinEstablishment: true,
    funded: true,
    jobFamily: "—",
    reportsTo: "",
    effectiveFrom: "",
    _unitId: String(j.orgUnitId ?? ""),
  } as unknown as JobRow;
}

function PositionsList() {
  const mockState = useMock(() => structureApi.positions());
  const [view, setView] = useState("all");
  const [createOpen, setCreateOpen] = useState(false);
  const [editJob, setEditJob] = useState<{ id: string; title: string; grade: string; orgUnitId: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const state = useApi(async () => {
    const [jobs, units, entities] = await Promise.all([
      realApi.jobs({ includeInactive: true }),
      realApi.orgUnits(),
      fetchOrgUnits(),
    ]);
    const unitMap: Record<string, string> = {};
    const unitEntity: Record<string, string> = {};
    for (const u of (units ?? []) as Record<string, unknown>[]) {
      unitMap[String(u.id ?? "")] = String(u.name ?? "");
      unitEntity[String(u.id ?? "")] = String(u.legalEntityId ?? "");
    }
    return ((jobs ?? []) as Record<string, unknown>[]).map((j) =>
      adaptJob({ ...j, legalEntityId: unitEntity[String(j.orgUnitId ?? "")] ?? j.legalEntityId }, unitMap),
    );
  }, [createOpen, editJob, busy]);

  const unitsState = useApi(() => realApi.orgUnits(), [createOpen]);
  const unitRows = ((unitsState.data ?? []) as Record<string, unknown>[]) ?? [];

  const treeState = useApi<OrgTreeNode[]>(async () => {
    if (import.meta.env.VITE_USE_REAL_API === "true") return (await realApi.entityTree()) as OrgTreeNode[];
    return demoEntityTree;
  }, [createOpen]);
  const entityUnits = flattenEntityTree(treeState.data ?? []);
  const entityFilterOptions = treeToSelectOptions(treeState.data ?? []).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));

  const refresh = () => setBusy((b) => !b);

  const columns: ColumnDef<Position>[] = [
    {
      id: "position",
      header: "Position",
      cell: (p) => (
        <div className="min-w-0 max-w-72">
          <Link
            to="/hrm/people/positions/$id"
            params={{ id: p.id }}
            className="block truncate font-medium text-primary underline-offset-2 hover:underline"
          >
            {p.jobTitle}
          </Link>
          <span className="block truncate text-xs text-muted-foreground">{p.positionNo}</span>
          <span className="mt-1 flex flex-wrap gap-1">
            {p.critical ? (
              <span className="inline-flex items-center gap-1 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning">
                <AlertTriangle aria-hidden className="size-3" />
                Critical position
              </span>
            ) : null}
            {p.licence ? (
              <span
                className={
                  licenceAttention(p)
                    ? "inline-flex items-center gap-1 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning"
                    : "inline-flex items-center gap-1 rounded-full border bg-surface-muted px-2 py-0.5 text-[11px] font-medium text-muted-foreground"
                }
              >
                <BadgeCheck aria-hidden className="size-3" />
                Licence: {p.licence.status.toLowerCase()}
                {p.licence.holderExpiry ? ` — ${p.licence.holderExpiry}` : ""}
              </span>
            ) : null}
          </span>
        </div>
      ),
    },
    { id: "grade", header: "Grade", cell: (p) => p.grade },
    { id: "entity", header: "Entity", cell: (p) => shortEntityName(p.entityId) },
    { id: "branch", header: "Branch", cell: (p) => p.branch },
    {
      id: "department",
      header: "Department",
      cell: (p) => (
        <div className="min-w-0 max-w-48">
          <span className="block truncate">{p.department}</span>
          <span className="block truncate text-xs text-muted-foreground">{p.team}</span>
        </div>
      ),
    },
    {
      id: "incumbent",
      header: "Incumbent",
      cell: (p) =>
        p.incumbentId ? (
          <Link
            to="/hrm/employees/$id"
            params={{ id: p.incumbentId }}
            className="text-primary underline-offset-2 hover:underline"
          >
            {positionIncumbentName(p)}
          </Link>
        ) : (
          <span className="text-muted-foreground">No incumbent</span>
        ),
    },
    { id: "status", header: "Status", cell: (p) => <StatusBadge status={p.status} /> },
    { id: "fte", header: "FTE", defaultVisible: false, cell: (p) => p.fte.toFixed(1) },
    {
      id: "establishment",
      header: "Establishment",
      defaultVisible: false,
      cell: (p) => <EstablishmentFlag position={p} />,
    },
    {
      id: "licence",
      header: "Mandatory licence",
      defaultVisible: false,
      cell: (p) =>
        p.licence ? (
          <div className="min-w-0 max-w-64">
            <span className="block truncate">{p.licence.name}</span>
            <span className="block truncate text-xs text-muted-foreground">
              {p.licence.holderExpiry ? `Expires ${p.licence.holderExpiry}` : "No certified holder"}
            </span>
          </div>
        ) : (
          <span className="text-muted-foreground">None required</span>
        ),
    },
    { id: "family", header: "Job family", defaultVisible: false, cell: (p) => p.jobFamily },
    {
      id: "reportsTo",
      header: "Reports to",
      defaultVisible: false,
      cell: (p) => <span className="block max-w-56 truncate">{p.reportsTo}</span>,
    },
    {
      id: "effective",
      header: "Effective from",
      defaultVisible: false,
      cell: (p) => p.effectiveFrom,
    },
    {
      id: "actions",
      header: "",
      defaultVisible: false,
      cell: (p) => (
        <div className="flex items-center gap-2">
          {USE_REAL ? (
            <Button
              size="sm"
              variant="ghost"
              onClick={() =>
                setEditJob({
                  id: p.id,
                  title: p.jobTitle,
                  grade: p.grade === "—" ? "" : p.grade,
                  orgUnitId: (p as unknown as { _unitId?: string })._unitId ?? "",
                })
              }
            >
              Edit
            </Button>
          ) : null}
          <Link
            to="/hrm/people/positions/$id"
            params={{ id: p.id }}
            className="text-xs font-medium text-primary underline underline-offset-2"
          >
            Open
          </Link>
        </div>
      ),
    },
  ];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="People"
          title="Positions"
          description={
            USE_REAL
              ? "The jobs catalogue: every position title in the organisation, its grade and reporting department. Fill a position by hiring an employee against it on the Employees page."
              : "The establishment register: every position, whether it is filled, and whether it is funded. A position exists independently of the employee who occupies it, so vacancies, freezes and closures stay visible."
          }
          primaryAction={
            <Button onClick={() => (USE_REAL ? setCreateOpen(true) : feedback.submitted("New position started.", "A position needs a grade and a cost centre before it can be filled or budgeted."))}>
              Create position
            </Button>
          }
        />
        <Async state={state} rows={6}>
          {(rows) => {
            const vacant = rows.filter((p) => p.status === "Vacant").length;
            const critical = rows.filter((p) => p.critical).length;
            const licences = rows.filter((p) => licenceAttention(p)).length;
            const unfunded = rows.filter((p) => !p.funded || !p.withinEstablishment).length;

            return (
              <div className="space-y-4">
                <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Vacant positions
                    </dt>
                    <dd className="mt-1 text-lg font-semibold">{vacant}</dd>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Critical positions
                    </dt>
                    <dd className="mt-1 text-lg font-semibold">{critical}</dd>
                  </div>
                  <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-warning">
                      Licences needing action
                    </dt>
                    <dd className="mt-1 text-lg font-semibold text-warning">{licences}</dd>
                    <p className="mt-1 text-xs text-warning">
                      A lapsed mandatory licence makes the incumbent not fit to work in the position,
                      even though the position itself is unchanged.
                    </p>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Unfunded or off establishment
                    </dt>
                    <dd className="mt-1 text-lg font-semibold">{unfunded}</dd>
                  </div>
                </dl>

                <ListPage
                  rows={rows.filter((p) => matchView(p, view))}
                  columns={columns}
                  savedViews={views}
                  activeView={view}
                  onViewChange={setView}
                  searchPlaceholder="Search position number, title, department or incumbent"
                  searchFields={(p) =>
                    `${p.positionNo} ${p.jobTitle} ${p.jobFamily} ${p.department} ${p.team} ${p.branch} ${
                      positionIncumbentName(p) ?? "vacant"
                    }`
                  }
                  filters={[
                    {
                      id: "entity",
                      label: "Entity & branch",
                      options: treeToSelectOptions(treeState.data ?? []).map((o) => o.value),
                      treeOptions: entityFilterOptions,
                      match: (p, v) =>
                        v.startsWith("entity:")
                          ? p.entityId === v.slice(7)
                          : (p as unknown as { _unitId?: string })._unitId === v,
                    },
                    {
                      id: "status",
                      label: "Status",
                      options: ["Vacant", "Filled", "Frozen", "Closed"],
                      match: (p, v) => p.status === v,
                    },
                    {
                      id: "department",
                      label: "Department (tree)",
                      options: entityUnits.map((e) => e.unitName),
                      treeOptions: entityUnits.map((e) => ({
                        value: e.unitName,
                        label: treePathLabel(e.path),
                      })),
                      match: (p, v) => p.department === v,
                    },
                    {
                      id: "establishment",
                      label: "Establishment",
                      options: [
                        "Funded and established",
                        "Not funded",
                        "Outside establishment",
                        "Critical position",
                      ],
                      match: (p, v) =>
                        v === "Funded and established"
                          ? p.funded && p.withinEstablishment
                          : v === "Not funded"
                            ? !p.funded
                            : v === "Outside establishment"
                              ? !p.withinEstablishment
                              : p.critical,
                    },
                    {
                      id: "licence",
                      label: "Mandatory licence",
                      options: ["Licence required", "Licence needs action", "No licence required"],
                      match: (p, v) =>
                        v === "Licence required"
                          ? Boolean(p.licence)
                          : v === "Licence needs action"
                            ? licenceAttention(p)
                            : !p.licence,
                    },
                  ]}
                  bulkActions={[
                    { label: "Export selection", onSelect: () => undefined },
                    { label: "Add to establishment review", onSelect: () => undefined },
                  ]}
                  rowHref={() => undefined}
                  emptyBody="No positions match this view. Clear a filter, or switch back to all positions."
                />
              </div>
            );
          }}
        </Async>

        {USE_REAL ? (
          <>
            <CreateJobDialog
              open={createOpen}
              onOpenChange={setCreateOpen}
              unitRows={unitRows}
              onSaved={refresh}
            />
            {editJob ? (
              <EditJobDialog
                job={editJob}
                onOpenChange={(open) => !open && setEditJob(null)}
                unitRows={unitRows}
                onSaved={refresh}
              />
            ) : null}
          </>
        ) : null}
      </AppShell>
    </AuthGate>
  );
}

interface JobDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  unitRows: Record<string, unknown>[];
  onSaved: () => void;
}

function JobForm({
  unitRows,
  code,
  setCode,
  title,
  setTitle,
  grade,
  setGrade,
  orgUnitId,
  setOrgUnitId,
}: {
  unitRows: Record<string, unknown>[];
  code: string;
  setCode: (v: string) => void;
  title: string;
  setTitle: (v: string) => void;
  grade: string;
  setGrade: (v: string) => void;
  orgUnitId: string;
  setOrgUnitId: (v: string) => void;
}) {
  return (
    <div className="grid gap-4 py-2">
      <div className="grid gap-2">
        <Label htmlFor="job-code">Code</Label>
        <Input id="job-code" placeholder="e.g. ACC-001" value={code} onChange={(e) => setCode(e.target.value)} />
      </div>
      <div className="grid gap-2">
        <Label htmlFor="job-title">Job title</Label>
        <Input id="job-title" placeholder="e.g. Accountant I" value={title} onChange={(e) => setTitle(e.target.value)} />
      </div>
      <div className="grid gap-2">
        <Label htmlFor="job-grade">Grade</Label>
        <Input id="job-grade" placeholder="e.g. G4" value={grade} onChange={(e) => setGrade(e.target.value)} />
      </div>
      <div className="grid gap-2">
        <Label>Department (organisation unit)</Label>
        <Select value={orgUnitId || undefined} onValueChange={setOrgUnitId}>
          <SelectTrigger>
            <SelectValue placeholder="Optional — pick a department" />
          </SelectTrigger>
          <SelectContent>
            {unitRows.map((u) => (
              <SelectItem key={String(u.id ?? "")} value={String(u.id ?? "")}>
                {String(u.name ?? "")}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}

function CreateJobDialog({ open, onOpenChange, unitRows, onSaved }: JobDialogProps) {
  const [saving, setSaving] = useState(false);
  const [code, setCode] = useState("");
  const [title, setTitle] = useState("");
  const [grade, setGrade] = useState("");
  const [orgUnitId, setOrgUnitId] = useState("");
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create position</DialogTitle>
          <DialogDescription>
            Create a new position in the jobs catalogue. It becomes fillable the moment it exists;
            hire an employee against it from the Employees page.
          </DialogDescription>
        </DialogHeader>
        <JobForm
          unitRows={unitRows}
          code={code}
          setCode={setCode}
          title={title}
          setTitle={setTitle}
          grade={grade}
          setGrade={setGrade}
          orgUnitId={orgUnitId}
          setOrgUnitId={setOrgUnitId}
        />
        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={async () => {
              if (!code.trim() || !title.trim()) {
                feedback.blocked("Error", "Code and job title are required.");
                return;
              }
              setSaving(true);
              try {
                const body: Record<string, unknown> = { code: code.trim(), title: title.trim() };
                if (grade.trim()) body.grade = grade.trim();
                if (orgUnitId) body.orgUnitId = orgUnitId;
                await realApi.createJob(body);
                feedback.submitted("Position created", `${code.trim()} — ${title.trim()} is now on the catalogue.`);
                setCode("");
                setTitle("");
                setGrade("");
                setOrgUnitId("");
                onSaved();
                onOpenChange(false);
              } catch (err) {
                feedback.blocked("Error", err instanceof Error ? err.message : "Could not create the position.");
              } finally {
                setSaving(false);
              }
            }}
            disabled={saving}
          >
            {saving ? "Saving…" : "Create position"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function EditJobDialog({
  job,
  onOpenChange,
  unitRows,
  onSaved,
}: {
  job: { id: string; title: string; grade: string; orgUnitId: string };
  onOpenChange: (open: boolean) => void;
  unitRows: Record<string, unknown>[];
  onSaved: () => void;
}) {
  const [saving, setSaving] = useState(false);
  const [title, setTitle] = useState(job.title);
  const [grade, setGrade] = useState(job.grade);
  const [orgUnitId, setOrgUnitId] = useState(job.orgUnitId);
  const [open, setOpen] = useState(true);
  if (!open) return null;
  return (
    <Dialog open={open} onOpenChange={(next) => { setOpen(next); onOpenChange(next); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Edit position</DialogTitle>
          <DialogDescription>
            Update the title, grade or department. Existing employees keep their assignments;
            use Close position to retire it instead.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="grid gap-2">
            <Label htmlFor="job-title-edit">Job title</Label>
            <Input id="job-title-edit" value={title} onChange={(e) => setTitle(e.target.value)} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="job-grade-edit">Grade</Label>
            <Input id="job-grade-edit" value={grade} onChange={(e) => setGrade(e.target.value)} />
          </div>
          <div className="grid gap-2">
            <Label>Department (organisation unit)</Label>
            <Select value={orgUnitId || undefined} onValueChange={setOrgUnitId}>
              <SelectTrigger>
                <SelectValue placeholder="Optional — pick a department" />
              </SelectTrigger>
              <SelectContent>
                {unitRows.map((u) => (
                  <SelectItem key={String(u.id ?? "")} value={String(u.id ?? "")}>
                    {String(u.name ?? "")}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={async () => {
              setSaving(true);
              try {
                const body: Record<string, unknown> = {
                  title: title.trim(),
                  grade: grade?.trim() || null,
                  orgUnitId: orgUnitId || null,
                };
                await realApi.updateJob(job.id, body);
                feedback.submitted("Position updated", `${title.trim()} was saved.`);
                onSaved();
                onOpenChange(false);
              } catch (err) {
                feedback.blocked("Error", err instanceof Error ? err.message : "Could not save the position.");
              } finally {
                setSaving(false);
              }
            }}
            disabled={saving}
          >
            {saving ? "Saving…" : "Save position"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
