import { createFileRoute, Link } from "@tanstack/react-router";
import { ChevronRight, TriangleAlert } from "lucide-react";
import { useMemo, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { entities } from "@/mock/data";
import type { OrgUnit } from "@/mock/structure";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { EmptyState } from "@/platform/components/States";
import { adaptOrgUnits, realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";
import { useAuth } from "@/platform/auth";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function CreateOrgUnitDialog({
  open,
  onOpenChange,
  onCreated,
  units,
  legalEntityId,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onCreated: () => void;
  units: OrgUnit[];
  legalEntityId?: string;
}) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [kind, setKind] = useState<"Entity" | "Branch" | "Department" | "Team">("Department");
  const [parentId, setParentId] = useState<string>("none");
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create organisation unit</DialogTitle>
          <DialogDescription>
            The new unit appears under the selected parent and rolls its headcount up to it.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="org-name">Name</Label>
            <Input id="org-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Credit department" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="org-code">Code</Label>
            <Input id="org-code" value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. CRED" />
          </div>
          <div className="space-y-1.5">
            <Label>Kind</Label>
            <Select value={kind} onValueChange={(v) => setKind(v as OrgUnit["kind"])}>
              <SelectTrigger className="w-full"><SelectValue placeholder="Kind" /></SelectTrigger>
              <SelectContent>
                {( ["Entity", "Branch", "Department", "Team"] as const ).map((k) => (
                  <SelectItem key={k} value={k}>{k}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Parent unit</Label>
            <Select value={parentId} onValueChange={setParentId}>
              <SelectTrigger className="w-full"><SelectValue placeholder="No parent (root)" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="none">No parent (root)</SelectItem>
                {units.map((u) => (
                  <SelectItem key={u.id} value={u.id}>{u.kind} — {u.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
          >
            Cancel
          </Button>
          <Button
            onClick={async () => {
              if (!name.trim() || !code.trim()) {
                feedback.blocked("Missing details", "Name and code are required.");
                return;
              }
              if (!legalEntityId) { feedback.blocked("Choose a company", "Select the legal entity before creating an organisation unit."); return; }
              try {
                await realApi.createOrgUnit({
                  legalEntityId,
                  code: code.trim(),
                  name: name.trim(),
                  unitType: kind,
                  parentId: parentId === "none" ? null : parentId,
                  effectiveFrom: new Date().toISOString().slice(0, 10),
                });
                feedback.submitted("Organisation unit", "The unit now appears in the structure.");
                onCreated();
                onOpenChange(false);
                setName("");
                setCode("");
              } catch (err) {
                feedback.blocked("Could not create unit", err instanceof Error ? err.message : "Unknown error");
              }
            }}
          >
            Create unit
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export const Route = createFileRoute("/hrm/people/org")({
  head: () => ({
    meta: [
      { title: "Organisation structure — Kuubu HRM" },
      {
        name: "description",
        content:
          "Indented tree of entities, branches, departments and teams with headcount, vacancies and the lead of each unit.",
      },
      { property: "og:title", content: "Organisation structure — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Indented tree of entities, branches, departments and teams with headcount, vacancies and the lead of each unit.",
      },
    ],
  }),
  component: OrgStructurePage,
});

interface TreeNode {
  unit: OrgUnit;
  children: TreeNode[];
}

interface FlatNode {
  unit: OrgUnit;
  hasChildren: boolean;
  depth: number;
  parentId?: string;
  posInSet: number;
  setSize: number;
}

function buildTree(units: OrgUnit[]): TreeNode[] {
  const byId = new Map<string, TreeNode>();
  for (const unit of units) byId.set(unit.id, { unit, children: [] });

  const roots: TreeNode[] = [];
  for (const unit of units) {
    const node = byId.get(unit.id);
    if (!node) continue;
    const parent = unit.parentId ? byId.get(unit.parentId) : undefined;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }
  return roots;
}

function flatten(
  nodes: TreeNode[],
  expanded: Set<string>,
  depth = 0,
  parentId?: string,
): FlatNode[] {
  return nodes.flatMap((node, index) => {
    const entry: FlatNode = {
      unit: node.unit,
      hasChildren: node.children.length > 0,
      depth,
      parentId,
      posInSet: index + 1,
      setSize: nodes.length,
    };
    const descendants =
      node.children.length && expanded.has(node.unit.id)
        ? flatten(node.children, expanded, depth + 1, node.unit.id)
        : [];
    return [entry, ...descendants];
  });
}

const collectIds = (nodes: TreeNode[]): string[] =>
  nodes.flatMap((n) => [n.unit.id, ...collectIds(n.children)]);

const kindTone: Record<OrgUnit["kind"], string> = {
  Entity: "border-primary/40 bg-primary-soft text-primary",
  Branch: "border-info/30 bg-info-soft text-info",
  Department: "bg-surface-muted text-muted-foreground",
  Team: "bg-surface-muted text-muted-foreground",
};

function UnitRow({
  unit,
  hasChildren,
  expanded,
}: {
  unit: OrgUnit;
  hasChildren: boolean;
  expanded: boolean;
}) {
  return (
    <span className="flex min-w-0 flex-1 flex-wrap items-center gap-x-2 gap-y-1">
      <ChevronRight
        aria-hidden
        className={`size-4 shrink-0 text-muted-foreground transition-transform ${
          hasChildren ? (expanded ? "rotate-90" : "") : "invisible"
        }`}
      />
      <span className="truncate font-medium">{unit.name}</span>
      <span
        className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${kindTone[unit.kind]}`}
      >
        {unit.kind}
      </span>
      <span className="text-xs text-muted-foreground">
        {unit.headcount} {unit.headcount === 1 ? "employee" : "employees"}
      </span>
      {unit.vacancies > 0 ? (
        <span className="inline-flex items-center gap-1 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning">
          <TriangleAlert aria-hidden className="size-3" />
          {unit.vacancies} {unit.vacancies === 1 ? "vacancy" : "vacancies"}
        </span>
      ) : null}
      <span className="truncate text-xs text-muted-foreground">
        Lead: {unit.lead.name}
        {unit.lead.interim ? " (interim)" : ""}
      </span>
    </span>
  );
}

function OrgTree({
  roots,
  selectedId,
  onSelect,
}: {
  roots: TreeNode[];
  selectedId: string;
  onSelect: (id: string) => void;
}) {
  const [expanded, setExpanded] = useState<Set<string>>(
    () => new Set(roots.flatMap((r) => [r.unit.id, ...r.children.map((c) => c.unit.id)])),
  );
  const itemRefs = useRef(new Map<string, HTMLLIElement>());

  const flat = useMemo(() => flatten(roots, expanded), [roots, expanded]);
  const activeId = flat.some((f) => f.unit.id === selectedId)
    ? selectedId
    : (flat[0]?.unit.id ?? "");

  const setOpen = (id: string, open: boolean) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (open) next.add(id);
      else next.delete(id);
      return next;
    });

  const move = (id: string | undefined) => {
    if (!id) return;
    onSelect(id);
    itemRefs.current.get(id)?.focus();
  };

  const onKeyDown = (event: ReactKeyboardEvent<HTMLLIElement>, entry: FlatNode) => {
    const index = flat.findIndex((f) => f.unit.id === entry.unit.id);
    const isOpen = expanded.has(entry.unit.id);
    let handled = true;

    switch (event.key) {
      case "ArrowDown":
        move(flat[index + 1]?.unit.id);
        break;
      case "ArrowUp":
        move(flat[index - 1]?.unit.id);
        break;
      case "ArrowRight":
        if (entry.hasChildren && !isOpen) setOpen(entry.unit.id, true);
        else if (entry.hasChildren) move(flat[index + 1]?.unit.id);
        break;
      case "ArrowLeft":
        if (entry.hasChildren && isOpen) setOpen(entry.unit.id, false);
        else move(entry.parentId);
        break;
      case "Home":
        move(flat[0]?.unit.id);
        break;
      case "End":
        move(flat[flat.length - 1]?.unit.id);
        break;
      case "Enter":
      case " ":
        if (entry.hasChildren) setOpen(entry.unit.id, !isOpen);
        onSelect(entry.unit.id);
        break;
      default:
        handled = false;
    }

    if (handled) {
      event.preventDefault();
      event.stopPropagation();
    }
  };

  const renderLevel = (nodes: TreeNode[], depth: number, parentId?: string, isRoot = false) => (
    <ul
      role={isRoot ? "tree" : "group"}
      aria-label={isRoot ? "Organisation structure" : undefined}
      className={isRoot ? "space-y-1" : "mt-1 space-y-1 border-l border-border pl-4"}
    >
      {nodes.map((node, index) => {
        const hasChildren = node.children.length > 0;
        const isOpen = expanded.has(node.unit.id);
        const isActive = activeId === node.unit.id;
        const entry: FlatNode = {
          unit: node.unit,
          hasChildren,
          depth,
          parentId,
          posInSet: index + 1,
          setSize: nodes.length,
        };

        return (
          <li
            key={node.unit.id}
            ref={(el) => {
              if (el) itemRefs.current.set(node.unit.id, el);
              else itemRefs.current.delete(node.unit.id);
            }}
            role="treeitem"
            aria-expanded={hasChildren ? isOpen : undefined}
            aria-selected={isActive}
            aria-level={depth + 1}
            aria-posinset={index + 1}
            aria-setsize={nodes.length}
            aria-label={`${node.unit.name}, ${node.unit.kind}, ${node.unit.headcount} ${
              node.unit.headcount === 1 ? "employee" : "employees"
            }, ${node.unit.vacancies} ${node.unit.vacancies === 1 ? "vacancy" : "vacancies"}, lead ${
              node.unit.lead.name
            }${node.unit.lead.interim ? " (interim)" : ""}`}
            tabIndex={isActive ? 0 : -1}
            onKeyDown={(event) => onKeyDown(event, entry)}
            className="group outline-none"
          >
            <span
              onClick={() => {
                onSelect(node.unit.id);
                if (hasChildren) setOpen(node.unit.id, !isOpen);
                itemRefs.current.get(node.unit.id)?.focus();
              }}
              className={`flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm group-focus-visible:ring-2 group-focus-visible:ring-primary ${
                isActive ? "bg-primary-soft" : "hover:bg-surface-muted"
              }`}
            >
              <UnitRow unit={node.unit} hasChildren={hasChildren} expanded={isOpen} />
            </span>
            {hasChildren && isOpen ? renderLevel(node.children, depth + 1, node.unit.id) : null}
          </li>
        );
      })}
    </ul>
  );

  return (
    <div>
      <div className="mb-3 flex flex-wrap gap-2">
        <Button variant="outline" size="sm" onClick={() => setExpanded(new Set(collectIds(roots)))}>
          Expand all
        </Button>
        <Button variant="outline" size="sm" onClick={() => setExpanded(new Set())}>
          Collapse all
        </Button>
      </div>
      {renderLevel(roots, 0, undefined, true)}
      <p className="mt-3 text-xs text-muted-foreground">
        Keyboard: up and down arrows move between units, right arrow opens a unit, left arrow closes
        it or moves to the parent, Home and End jump to the first and last unit, Enter or Space
        opens and selects.
      </p>
    </div>
  );
}

function UnitPanel({ unit, units }: { unit: OrgUnit; units: OrgUnit[] }) {
  const path: OrgUnit[] = [];
  let cursor: OrgUnit | undefined = unit;
  while (cursor) {
    path.unshift(cursor);
    const parentId: string | undefined = cursor.parentId;
    cursor = parentId ? units.find((u) => u.id === parentId) : undefined;
  }

  const facts: { label: string; value: string }[] = [
    { label: "Unit code", value: unit.code },
    { label: "Type", value: unit.kind },
    { label: "Location", value: unit.location },
    { label: "Employees", value: String(unit.headcount) },
    { label: "Starting soon", value: String(unit.incoming) },
    { label: "Leaving", value: String(unit.leavers) },
    { label: "Positions", value: String(unit.positions) },
    { label: "Vacancies", value: String(unit.vacancies) },
  ];

  return (
    <aside aria-label="Selected unit" className="rounded-lg border bg-surface p-5">
      <p className="text-xs text-muted-foreground">{path.map((p) => p.name).join(" › ")}</p>
      <h2 className="mt-1 text-lg font-semibold tracking-tight">{unit.name}</h2>

      <div className="mt-4 rounded-md border bg-surface-muted p-3">
        <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          Unit lead
        </p>
        <p className="mt-1 text-sm font-medium">
          {unit.lead.employeeId ? (
            <Link
              to="/hrm/employees/$id"
              params={{ id: unit.lead.employeeId }}
              className="text-primary underline underline-offset-2"
            >
              {unit.lead.name}
            </Link>
          ) : (
            unit.lead.name
          )}
          {unit.lead.interim ? (
            <span className="ml-2 text-xs text-warning">Interim cover</span>
          ) : null}
        </p>
        <p className="mt-1 text-sm text-muted-foreground">{unit.lead.title}</p>
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-4">
        {facts.map((f) => (
          <div key={f.label}>
            <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {f.label}
            </dt>
            <dd className="mt-1 text-sm">{f.value}</dd>
          </div>
        ))}
      </dl>

      {unit.note ? <p className="mt-4 text-sm text-muted-foreground">{unit.note}</p> : null}

      <div className="mt-4 space-y-2 text-sm">
        <Link to="/hrm/people/positions" className="block text-primary underline underline-offset-2">
          Positions in the establishment
        </Link>
        <Link to="/hrm/employees" className="block text-primary underline underline-offset-2">
          Employee directory
        </Link>
      </div>
    </aside>
  );
}

function OrgStructurePage() {
  const { coreWorkspace } = useAuth();
  const demo = useMock(() => import("@/mock/structure").then((m) => m.orgUnits));
  const [entityId, setEntityId] = useState("all");
  const [selectedId, setSelectedId] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [tick, setTick] = useState(0);
  const state = useApi(
    async (): Promise<OrgUnit[]> =>
      USE_REAL ? adaptOrgUnits(await realApi.entityTree()) : ([] as OrgUnit[]),
    [USE_REAL, tick],
  );

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="People"
        title="Organisation structure"
        description="Entity, branch, department and team, with the headcount and lead of each unit. Numbers roll up: a unit shows everything beneath it, so an entity total includes every branch and team below."
        primaryAction={
          <>
            <Button variant="outline" onClick={() => setCreateOpen(true)}>
              New unit
            </Button>
            <Button
              variant="ghost"
              onClick={() =>
                feedback.note(
                  "Export is not generated in this build.",
                  "It would produce the structure as it stands today, with an effective date on it.",
                )
              }
            >
              Export structure
            </Button>
          </>
        }
        meta={
          <Select value={entityId} onValueChange={setEntityId}>
            <SelectTrigger className="w-auto min-w-56" aria-label="Filter by legal entity">
              <SelectValue placeholder="Entity" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Entity: all</SelectItem>
              {USE_REAL
                ? Array.from(new Set(state.data?.filter((u) => u.kind === "Entity").map((u) => `${u.id}|${u.name}`) ?? []))
                    .sort()
                    .map((key) => {
                      const [eid, ...rest] = key.split("|");
                      return (
                        <SelectItem key={eid} value={eid} className="font-semibold text-primary">
                          {rest.join("|") || eid}
                        </SelectItem>
                      );
                    })
                : entities.map((e) => (
                    <SelectItem key={e.id} value={e.id}>
                      {e.name}
                    </SelectItem>
                  ))}
            </SelectContent>
          </Select>
        }
      />
      <Async state={state} rows={5}>
        {(units) => {
          const live = USE_REAL ? units : demo.data ?? [];
          const scoped = entityId === "all"
            ? live
            : live.filter((u) => u.entityId === entityId || u.id === entityId);
          const roots = buildTree(scoped);
          const selected = scoped.find((u) => u.id === selectedId) ?? scoped[0];

          if (!selected) {
            return (
              <EmptyState
                title="No organisation units in scope"
                body={
                  USE_REAL
                    ? "No units are visible for this entity with your current access. Choose another entity, or ask your HR administrator about your entity permissions."
                    : "The backend is not reachable in this build, so no units are available. Connect the live HRM API to see the organisation tree."
                }
              />
            );
          }

          return (
            <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
              <section aria-label="Organisation tree" className="rounded-lg border bg-surface p-5">
                <OrgTree
                  key={entityId}
                  roots={roots}
                  selectedId={selected.id}
                  onSelect={setSelectedId}
                />
              </section>
              <UnitPanel unit={selected} units={scoped} />
            </div>
          );
        }}
      </Async>
      {USE_REAL && (
        <CreateOrgUnitDialog
          open={createOpen}
          onOpenChange={setCreateOpen}
          units={state.data ?? []}
          legalEntityId={entityId !== "all" ? entityId : coreWorkspace?.companies.length === 1 ? coreWorkspace.companies[0].id : undefined}
          onCreated={() => setTick((t) => t + 1)}
        />
      )}
    </AppShell>
      </AuthGate>
  );
}
