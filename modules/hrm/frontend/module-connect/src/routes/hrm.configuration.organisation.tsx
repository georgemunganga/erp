import { createFileRoute, Link, Navigate } from "@tanstack/react-router";
import { CalendarClock, History, Info, Lock, Pencil, Plus, ShieldAlert, Trash2 } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type {
  LegalEntityConfig,
  LocationKind,
  OrgUnitConfig,
  ScheduledChange,
  WorkLocation,
} from "@/mock/adminconfig";
import {
  adminConfigApi,
  canDeleteUnit,
  shortEntityName,
  todayIso,
  unitState,
  unitStateExplanation,
} from "@/mock/adminconfig";
import { ConfigurationShell } from "@/modules/core/ConfigurationShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import type { ColumnDef } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { DetailSection } from "@/platform/components/RecordDetail";
import { EmptyState } from "@/platform/components/States";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import type { TimelineEvent } from "@/mock/types";
import { realApi, useApi } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { feedback } from "@/platform/feedback";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";

const description =
  "Maintain legal entities, work locations, departments and cost centres. Every change carries an effective date, so amending the structure never rewrites what was true before.";

export const Route = createFileRoute("/hrm/configuration/organisation")({
  head: () => ({
    meta: [
      { title: "Organisation setup — Newworldcargo HRM" },
      { name: "description", content: description },
      { property: "og:title", content: "Organisation setup — Newworldcargo HRM" },
      { property: "og:description", content: description },
    ],
  }),
  component: () => <Navigate to="/core/workspace/settings/organization" replace />,
});

const fmt = (iso: string) =>
  new Date(`${iso}T00:00:00`).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

interface DraftClosure {
  id: string;
  unitId: string;
  unitName: string;
  effectiveFrom: string;
  reason: string;
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const kindMap: Record<string, LocationKind> = {
  branch: "Office",
  headoffice: "Head office",
  plant: "Plant",
  depot: "Depot",
  yard: "Yard",
  office: "Office",
  works: "Works",
};

function adaptEntities(rows: unknown[]): LegalEntityConfig[] {
  return (rows as Record<string, unknown>[]).map((e) => ({
    id: String(e.id ?? ""),
    entityId: String(e.id ?? ""),
    registeredName: String(e.registeredName ?? ""),
    country: String(e.countryCode ?? "ZM"),
    legalIdLabel: String(e.pacraNumber ? "PACRA" : "TPIN"),
    legalId: String(e.pacraNumber ?? e.tpin ?? "—"),
    currency: String(e.currency ?? "ZMW"),
    payrollCountryPack: String(e.countryCode ?? "ZM") === "ZM" ? "Zambian payroll pack" : "—",
    registeredAddress: "—",
    employees: 0,
    branches: 0,
    effectiveFrom: String(e.createdAt ?? "").slice(0, 10) || todayIso,
  })) as LegalEntityConfig[];
}

type OrgUnitWithEntity = OrgUnitConfig & { _entityName?: string };
function adaptUnits(rows: unknown[]): OrgUnitWithEntity[] {
  return (rows as Record<string, unknown>[]).map((u) => ({
    id: String(u.id ?? ""),
    name: String(u.name ?? ""),
    code: String(u.code ?? ""),
    costCentre: String(u.costCentreRef ?? "—"),
    entityId: String(u.legalEntityId ?? ""),
    branch: "—",
    parent: u.parentId ? String(u.parentId) : undefined,
    employees: 0,
    positions: 0,
    references: [],
    effectiveFrom: String(u.effectiveFrom ?? "").slice(0, 10) || todayIso,
    effectiveTo: u.effectiveTo ? String(u.effectiveTo).slice(0, 10) : undefined,
  })) as OrgUnitWithEntity[];
}

type WorkLocationWithEntity = WorkLocation & { _entityName?: string };
function adaptLocations(rows: unknown[]): WorkLocationWithEntity[] {
  return (rows as Record<string, unknown>[]).map((l) => ({
    id: String(l.id ?? ""),
    entityId: String(l.legalEntityId ?? ""),
    _entityName: String(l.legalEntityName ?? ""),
    name: String(l.name ?? ""),
    code: String(l.code ?? ""),
    kind: kindMap[String(l.type ?? "")] ?? "Office",
    address: String(l.addressLine ?? l.city ?? ""),
    timeZone: "Africa/Lusaka",
    employees: 0,
    positions: 0,
    effectiveFrom: String(l.createdAt ?? "").slice(0, 10) || todayIso,
  })) as WorkLocationWithEntity[];
}

const closureReasons = [
  "Merged into another unit",
  "Work location closing",
  "Function moved to another entity",
  "Created in error and never used",
];

/* -------------------------------------------------------------------------- */

function CloseUnitForm({
  units,
  onAdd,
  onRefresh,
}: {
  units: OrgUnitConfig[];
  onAdd: (draft: DraftClosure) => void;
  onRefresh?: () => void;
}) {
  const [unitId, setUnitId] = useState("");
  const [date, setDate] = useState("");
  const [reason, setReason] = useState(closureReasons[0]);
  const [busy, setBusy] = useState(false);
  const unit = units.find((u) => u.id === unitId);
  const invalidDate = Boolean(date) && date < todayIso;

  return (
    <form
      className="space-y-4"
      onSubmit={async (e) => {
        e.preventDefault();
        if (!unit || !date || invalidDate) return;
        if (!USE_REAL) {
          onAdd({
            id: `draft-${unit.id}-${date}`,
            unitId: unit.id,
            unitName: `${unit.name} (${unit.code})`,
            effectiveFrom: date,
            reason,
          });
          setUnitId("");
          setDate("");
          return;
        }
        setBusy(true);
        try {
          await realApi.closeOrgUnit(unit.id, date, reason);
          feedback.saved(`Closed ${unit.name} from ${fmt(date)}.`, () => undefined);
          setUnitId("");
          setDate("");
          onRefresh?.();
        } catch (err) {
          feedback.note(
            "Could not close the unit.",
            err instanceof Error ? err.message : "Server rejected the closure.",
          );
        } finally {
          setBusy(false);
        }
      }}
    >
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="min-w-0">
          <Label htmlFor="close-unit">Unit to close</Label>
          <Select value={unitId} onValueChange={setUnitId}>
            <SelectTrigger id="close-unit" className="mt-1.5 w-full">
              <SelectValue placeholder="Choose a department or cost centre" />
            </SelectTrigger>
            <SelectContent>
              {units.map((u) => (
                <SelectItem key={u.id} value={u.id}>
                  {u.name} — {u.code}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="min-w-0">
          <Label htmlFor="close-date">Closed from</Label>
          <Input
            id="close-date"
            type="date"
            value={date}
            min={todayIso}
            onChange={(e) => setDate(e.target.value)}
            className="mt-1.5"
            aria-describedby="close-date-hint"
            aria-invalid={invalidDate || undefined}
          />
          <p id="close-date-hint" className="mt-1 text-xs text-muted-foreground">
            The unit stays readable for every date before this one.
          </p>
        </div>
        <div className="min-w-0">
          <Label htmlFor="close-reason">Reason</Label>
          <Select value={reason} onValueChange={setReason}>
            <SelectTrigger id="close-reason" className="mt-1.5 w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {closureReasons.map((r) => (
                <SelectItem key={r} value={r}>
                  {r}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {invalidDate ? (
        <p className="flex items-start gap-2 rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
          <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />A closure cannot be
          backdated here. Correcting a past date is a separate, audited amendment because it changes
          records that have already been reported.
        </p>
      ) : null}

      {unit ? (
        <div className="rounded-md border bg-surface-muted px-3 py-2 text-sm">
          <p className="font-medium">
            {unit.employees} employee{unit.employees === 1 ? "" : "s"} and {unit.positions} position
            {unit.positions === 1 ? "" : "s"} sit under {unit.name}.
          </p>
          <p className="mt-1 text-muted-foreground">
            {canDeleteUnit(unit)
              ? "Nothing references this unit, so it can also be removed outright."
              : `Closing does not move them. Reassign them before ${
                  unit.name
                } stops being in force, or the affected records will have no unit on the effective date.`}
          </p>
        </div>
      ) : null}

      <Button type="submit" variant="outline" disabled={!unit || !date || invalidDate || busy}>
        {busy ? "Saving…" : USE_REAL ? "Close unit from date" : "Add to draft changes"}
      </Button>
    </form>
  );
}

/* -------------------------------------------------------------------------- */

function NewLocationDialog({
  open,
  onOpenChange,
  entities,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  entities: LegalEntityConfig[];
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [entityId, setEntityId] = useState(entities[0]?.entityId ?? "");
  const [kind, setKind] = useState("branch");
  const [city, setCity] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!USE_REAL) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>New work location</DialogTitle>
          <DialogDescription>
            Register a branch, plant or office the company operates from. It can be created before
            it opens and it stays readable after it closes.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            if (!name.trim() || !code.trim()) return;
            setBusy(true);
            try {
              await realApi.createLocation({
                code: code.trim().toUpperCase(),
                name: name.trim(),
                legalEntityId: entityId,
                type: kind,
                city: city.trim() || undefined,
              });
              onSaved();
              setName("");
              setCode("");
              setCity("");
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the location.");
            } finally {
              setBusy(false);
            }
          }}
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="min-w-0">
              <Label htmlFor="loc-name">Location name</Label>
              <Input
                id="loc-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="mt-1.5"
                required
              />
            </div>
            <div className="min-w-0">
              <Label htmlFor="loc-code">Code</Label>
              <Input
                id="loc-code"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                className="mt-1.5"
                placeholder="e.g. KITWE"
                required
              />
            </div>
            <div className="min-w-0">
              <Label htmlFor="loc-entity">Legal entity</Label>
              <Select value={entityId} onValueChange={setEntityId}>
                <SelectTrigger id="loc-entity" className="mt-1.5 w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {entities.map((e) => (
                    <SelectItem key={e.entityId} value={e.entityId}>
                      {e.registeredName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-0">
              <Label htmlFor="loc-kind">Type</Label>
              <Select value={kind} onValueChange={setKind}>
                <SelectTrigger id="loc-kind" className="mt-1.5 w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="branch">Branch</SelectItem>
                  <SelectItem value="headoffice">Head office</SelectItem>
                  <SelectItem value="plant">Plant</SelectItem>
                  <SelectItem value="office">Office</SelectItem>
                  <SelectItem value="yard">Yard</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="min-w-0">
            <Label htmlFor="loc-city">City</Label>
            <Input id="loc-city" value={city} onChange={(e) => setCity(e.target.value)} className="mt-1.5" />
          </div>
          {error ? (
            <p className="rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
              {error}
            </p>
          ) : null}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy || !name.trim() || !code.trim()}>
              {busy ? "Saving…" : "Create location"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/* -------------------------------------------------------------------------- */

function EntityTable({ rows, asAt, canAct }: { rows: LegalEntityConfig[]; asAt: string; canAct: boolean }) {
  return (
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[52rem] text-left text-sm">
        <caption className="sr-only">
          Legal entities with their registration details and effective dates
        </caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Registered name
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Country
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Legal identifier
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Currency
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Effective from
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Status
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Action
            </th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((e) => (
            <tr key={e.id} className="align-top hover:bg-surface-muted">
              <th scope="row" className="px-3 py-3 text-left font-medium">
                <span className="block max-w-72">{e.registeredName}</span>
                <span className="mt-0.5 block text-xs font-normal text-muted-foreground">
                  {e.registeredAddress}
                </span>
                {e.note ? (
                  <span className="mt-1 block max-w-96 text-xs font-normal text-muted-foreground">
                    {e.note}
                  </span>
                ) : null}
              </th>
              <td className="px-3 py-3">{e.country}</td>
              <td className="px-3 py-3">
                <span className="block font-mono text-xs">{e.legalId}</span>
                <span className="block text-xs text-muted-foreground">{e.legalIdLabel}</span>
              </td>
              <td className="px-3 py-3">
                <span className="block">{e.currency}</span>
                <span className="block text-xs text-muted-foreground">{e.payrollCountryPack}</span>
              </td>
              <td className="px-3 py-3 whitespace-nowrap">{fmt(e.effectiveFrom)}</td>
              <td className="px-3 py-3">
                <StatusBadge status={unitState(e, asAt)} />
                <span className="mt-1 block text-xs text-muted-foreground">
                  {e.employees} employee{e.employees === 1 ? "" : "s"} · {e.branches} location
                  {e.branches === 1 ? "" : "s"}
                </span>
              </td>
              <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-8"
                    onClick={() =>
                      feedback.note(
                        `Editing ${e.registeredName}.`,
                        "An entity change needs an effective date — it decides which payroll rules apply from when.",
                      )
                    }
                  >
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> {e.registeredName}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function LocationTable({ rows, asAt, canAct }: { rows: WorkLocationWithEntity[]; asAt: string; canAct: boolean }) {
  return (
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[48rem] text-left text-sm">
        <caption className="sr-only">
          Branches and work locations grouped by the entity that operates them
        </caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Location
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Entity
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Type
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              In force
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              People
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Status
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Action
            </th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((l) => (
            <tr key={l.id} className="align-top hover:bg-surface-muted">
              <th scope="row" className="px-3 py-3 text-left font-medium">
                <span className="block">{l.name}</span>
                <span className="mt-0.5 block font-mono text-xs font-normal text-muted-foreground">
                  {l.code}
                </span>
                <span className="mt-0.5 block max-w-72 text-xs font-normal text-muted-foreground">
                  {l.address} · {l.timeZone}
                </span>
              </th>
              <td className="px-3 py-3">
                {l._entityName
                  ? shortEntityName(l._entityName || l.entityId)
                  : shortEntityName(l.entityId)}
              </td>
              <td className="px-3 py-3">{l.kind}</td>
              <td className="px-3 py-3 whitespace-nowrap">
                <span className="block">{fmt(l.effectiveFrom)}</span>
                <span className="block text-xs text-muted-foreground">
                  {l.effectiveTo ? `until ${fmt(l.effectiveTo)}` : "no end date"}
                </span>
              </td>
              <td className="px-3 py-3">
                {l.employees} employee{l.employees === 1 ? "" : "s"}
                <span className="block text-xs text-muted-foreground">
                  {l.positions} position{l.positions === 1 ? "" : "s"}
                </span>
              </td>
              <td className="px-3 py-3">
                <StatusBadge status={unitState(l, asAt)} />
                {l.note ? (
                  <span className="mt-1 block max-w-72 text-xs text-muted-foreground">
                    {l.note}
                  </span>
                ) : null}
              </td>
                            <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-8"
                    onClick={() =>
                      feedback.note(
                        `Editing ${l.name}.`,
                        "A location change moves people between calendars and holiday sets from the date you give.",
                      )
                    }
                  >
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> {l.name}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>              </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* -------------------------------------------------------------------------- */

function ScheduledChangeItem({
  change,
  draft,
  onRemove,
}: {
  change: ScheduledChange;
  draft?: boolean;
  onRemove?: () => void;
}) {
  return (
    <li className="rounded-lg border bg-surface p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-medium">{change.change}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">
            {change.scope} · {change.unit}
          </p>
        </div>
        <span className="shrink-0">
          <StatusBadge status={draft ? "Draft" : change.state} />
        </span>
      </div>
      <dl className="mt-3 grid gap-x-6 gap-y-2 text-xs sm:grid-cols-3">
        <div>
          <dt className="text-muted-foreground">Takes effect</dt>
          <dd className="font-medium">{fmt(change.effectiveFrom)}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Raised by</dt>
          <dd className="font-medium">
            {change.requestedBy} on {fmt(change.requestedOn)}
          </dd>
        </div>
        <div>
          <dt className="text-muted-foreground">Employees affected</dt>
          <dd className="font-medium">{change.employeesAffected}</dd>
        </div>
      </dl>
      <p className="mt-2 text-sm text-muted-foreground">{change.impact}</p>
      {draft ? (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-warning/40 bg-warning-soft px-2.5 py-0.5 text-xs font-medium text-warning">
            <ShieldAlert className="size-3.5" aria-hidden />
            Local draft — not saved anywhere
          </span>
          {onRemove ? (
            <Button variant="ghost" size="sm" className="h-7 px-2 text-xs" onClick={onRemove}>
              Discard draft
            </Button>
          ) : null}
        </div>
      ) : null}
    </li>
  );
}

/* -------------------------------------------------------------------------- */

async function loadOrganisation() {
  if (!USE_REAL) return adminConfigApi.organisation();
  const [rawEntities, units, locations, workerPage] = await Promise.all([
    realApi.legalEntities(),
    realApi.orgUnits(),
    realApi.locations(),
    realApi.employees({ page: 1, pageSize: 500, includeArchived: false }),
  ]);
  const entities = Array.isArray(rawEntities) ? rawEntities : (rawEntities as { items?: unknown[] }).items ?? [];
  const adaptedEntities = adaptEntities(entities);
  const unitsArr = Array.isArray(units) ? units : (units as { items?: unknown[] }).items ?? [];
  const locationsArr = Array.isArray(locations) ? locations : (locations as { items?: unknown[] }).items ?? [];
  const adaptedUnits = adaptUnits(unitsArr);
  const adaptedLocations = adaptLocations(locationsArr);
  const workers = (workerPage.items ?? []) as Record<string, unknown>[];
  const locationEntity = new Map(locationsArr.map((row) => {
    const location = row as Record<string, unknown>;
    return [String(location.id ?? ""), String(location.legalEntityId ?? "")];
  }));
  for (const unit of adaptedUnits)
    unit.employees = workers.filter((worker) => String(worker.orgUnitId ?? "") === unit.id).length;
  for (const location of adaptedLocations)
    location.employees = workers.filter((worker) => String(worker.locationId ?? "") === location.id).length;
  for (const entity of adaptedEntities) {
    entity.branches = adaptedLocations.filter((location) => location.entityId === entity.id).length;
    entity.employees = workers.filter((worker) => locationEntity.get(String(worker.locationId ?? "")) === entity.id).length;
  }
  return {
    entities: adaptedEntities,
    units: adaptedUnits as OrgUnitConfig[],
    locations: adaptedLocations as WorkLocation[],
    scheduled: [] as ScheduledChange[],
    audit: [] as TimelineEvent[],
  };
}

export function OrganisationConfig() {
  const state = useApi(loadOrganisation);
  const userRoles = new Set(useAuth().user?.roles ?? []);
  const canAct = userRoles.has("hr_admin") || userRoles.has("hr_ops");
  const [asAt, setAsAt] = useState(todayIso);
  const [drafts, setDrafts] = useState<DraftClosure[]>([]);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [newLocation, setNewLocation] = useState(false);
  const refresh = state.reload;

  const columns: ColumnDef<OrgUnitWithEntity>[] = [
    {
      id: "unit",
      header: "Department",
      cell: (u) => (
        <div className="min-w-0 max-w-64">
          <span className="block truncate font-medium">{u.name}</span>
          <span className="block truncate font-mono text-xs text-muted-foreground">{u.code}</span>
          {u.parent ? (
            <span className="block truncate text-xs text-muted-foreground">Under {u.parent}</span>
          ) : null}
        </div>
      ),
    },
    {
      id: "cost",
      header: "Cost centre",
      cell: (u) => <span className="font-mono text-xs">{u.costCentre}</span>,
    },
    {
      id: "entity",
      header: "Entity",
      cell: (u) =>
        u._entityName
          ? shortEntityName(u._entityName || u.entityId)
          : shortEntityName(u.entityId),
    },
    { id: "branch", header: "Location", cell: (u) => u.branch },
    {
      id: "inuse",
      header: "In use by",
      cell: (u) =>
        u.references.length ? (
          <ul className="min-w-0 max-w-56 space-y-0.5 text-xs">
            {u.references.map((r) => (
              <li key={r} className="truncate">
                {r}
              </li>
            ))}
          </ul>
        ) : (
          <span className="text-xs text-muted-foreground">Nothing references it</span>
        ),
    },
    {
      id: "effective",
      header: "In force",
      cell: (u) => (
        <span className="whitespace-nowrap">
          <span className="block">{fmt(u.effectiveFrom)}</span>
          <span className="block text-xs text-muted-foreground">
            {u.effectiveTo ? `until ${fmt(u.effectiveTo)}` : "no end date"}
          </span>
        </span>
      ),
    },
    {
      id: "status",
      header: "Status",
      cell: (u) => {
        const draft = drafts.find((d) => d.unitId === u.id);
        return (
          <div className="min-w-0 max-w-56 space-y-1">
            <StatusBadge status={unitState(u, asAt)} />
            {draft ? (
              <span className="block text-xs text-warning">
                Draft closure from {fmt(draft.effectiveFrom)}
              </span>
            ) : null}
          </div>
        );
      },
    },
    {
      id: "employees",
      header: "Employees",
      defaultVisible: false,
      cell: (u) => u.employees,
    },
    { id: "positions", header: "Positions", defaultVisible: false, cell: (u) => u.positions },
    {
      id: "note",
      header: "Administrator note",
      defaultVisible: false,
      cell: (u) =>
        u.note ? (
          <span className="block max-w-72 text-xs">{u.note}</span>
        ) : (
          <span className="text-xs text-muted-foreground">None</span>
        ),
    },
  ];

  return (
    <AuthGate>
      <ConfigurationShell>
      <PageHeader
        eyebrow="Configuration"
        title="Organisation setup"
        description={description}
        primaryAction={
          <Button onClick={() => setNewLocation(true)} disabled={!canAct}>
            <Plus className="size-4" aria-hidden />
            Add work location
          </Button>
        }
        meta={
          <>
            <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
              Live tenant structure
            </span>
            <Link
              to="/hrm/people/org"
              className="rounded-full border bg-surface px-2.5 py-0.5 text-xs font-medium text-primary underline-offset-2 hover:underline"
            >
              Open the read-only organisation explorer
            </Link>
          </>
        }
      />

      <div className="space-y-6">
        <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
          <p className="flex items-start gap-2 font-medium">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
            Structure is effective-dated, so changing it never rewrites history
          </p>
          <p className="mt-1.5 pl-6">
            Renaming a department, moving a cost centre or closing a location applies from the date
            you give and no earlier. A payslip, leave record or headcount report produced before
            that date keeps the unit it was actually posted to. That is why units are closed or
            superseded rather than deleted.
          </p>
        </div>

        <section aria-label="Structure as at a date" className="rounded-lg border bg-surface p-4">
          <div className="flex flex-wrap items-end gap-4">
            <div className="min-w-0">
              <Label htmlFor="as-at">Show the structure as at</Label>
              <Input
                id="as-at"
                type="date"
                value={asAt}
                onChange={(e) => setAsAt(e.target.value || todayIso)}
                className="mt-1.5 w-48"
                aria-describedby="as-at-hint"
              />
            </div>
            <Button
              variant="outline"
              onClick={() => setAsAt(todayIso)}
              disabled={asAt === todayIso}
            >
              <CalendarClock className="size-4" aria-hidden />
              Back to today
            </Button>
          </div>
          <p id="as-at-hint" className="mt-2 text-xs text-muted-foreground">
            {asAt === todayIso
              ? `Showing what is in force today, ${fmt(todayIso)}. Move the date to see the structure as it was, or as it will be once scheduled changes take effect.`
              : `Showing the structure as at ${fmt(asAt)}. Statuses below are worked out from that date, not from today.`}
          </p>
        </section>

        <Async state={state} rows={6}>
          {(data) => {
            const closable = data.units.filter((u) => !u.effectiveTo);
            const blocked = data.units.filter((u) => !canDeleteUnit(u));
            const removable = data.units.filter((u) => canDeleteUnit(u));
            const draftChanges: ScheduledChange[] = drafts.map((d) => ({
              id: d.id,
              scope: "Department" as const,
              unit: d.unitName,
              change: `Close ${d.unitName} and stop new assignments from the effective date`,
              effectiveFrom: d.effectiveFrom,
              requestedBy: "You, in this browser session",
              requestedOn: todayIso,
              state: "Draft" as const,
              employeesAffected: data.units.find((u) => u.id === d.unitId)?.employees ?? 0,
              impact: `Reason recorded: ${d.reason}. Everything posted to the unit before ${fmt(
                d.effectiveFrom,
              )} stays attached to it.`,
            }));

            const future = [...data.scheduled].sort((a, b) =>
              a.effectiveFrom.localeCompare(b.effectiveFrom),
            );

            return (
              <div className="space-y-6">
                <DetailSection
                  id="profile"
                  title="Legal entities"
                  description="Who employs people, under which registration and in which currency. An entity is never deleted once it has employed anyone."
                >
                  <EntityTable rows={data.entities} asAt={asAt} canAct={canAct && !USE_REAL} />
                </DetailSection>

                <DetailSection
                  id="locations"
                  title="Branches and work locations"
                  description="Where employees are based. A location can exist in configuration before it opens and stays readable after it closes."
                >
                  <LocationTable rows={data.locations} asAt={asAt} canAct={canAct && !USE_REAL} />
                  <NewLocationDialog
                    open={newLocation}
                    onOpenChange={setNewLocation}
                    entities={data.entities}
                    onSaved={() => {
                      refresh();
                      feedback.saved("Location created. It is now available for placement.");
                    }}
                  />
                </DetailSection>

                <DetailSection
                  id="departments"
                  title="Departments and cost centres"
                  description="The reporting and costing spine. The In use column shows what still points at each unit, which decides whether it can be removed at all."
                >
                  <ListPage
                    rows={data.units}
                    columns={columns}
                    searchPlaceholder="Search department, code or cost centre"
                    searchFields={(u) =>
                      `${u.name} ${u.code} ${u.costCentre} ${u.branch} ${shortEntityName(u.entityId)}`
                    }
                    filters={[
                      {
                        id: "entity",
                        label: "Entity",
                        options: Array.from(
                          new Set(data.entities.map((e) => shortEntityName(e.id))),
                        ),
                        match: (u, v) => shortEntityName(u.entityId) === v,
                      },
                      {
                        id: "state",
                        label: "Status",
                        options: ["Active", "Closing", "Closed", "Not yet in force"],
                        match: (u, v) => unitState(u, asAt) === v,
                      },
                      {
                        id: "use",
                        label: "In use",
                        options: ["Referenced elsewhere", "Nothing references it", "Has employees"],
                        match: (u, v) =>
                          v === "Referenced elsewhere"
                            ? !canDeleteUnit(u)
                            : v === "Has employees"
                              ? u.employees > 0
                              : canDeleteUnit(u),
                      },
                    ]}
                    bulkActions={USE_REAL ? [] : [
                      { label: "Export selection", onSelect: () => undefined },
                      { label: "Add to structure review", onSelect: () => undefined },
                    ]}
                    rowHref={(u) =>
                      !USE_REAL && canDeleteUnit(u) ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-8 text-xs"
                          onClick={() => setDeleting(u.name)}
                        >
                          <Trash2 className="size-3.5" aria-hidden />
                          Delete
                          <span className="sr-only"> {u.name}</span>
                        </Button>
                      ) : !canDeleteUnit(u) ? (
                        <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-xs text-muted-foreground">
                          <Lock className="size-3.5 shrink-0" aria-hidden />
                          Close only
                        </span>
                      ) : null
                    }
                    emptyBody="No units match this view. Clear a filter, or move the as-at date."
                  />
                </DetailSection>

                <DetailSection
                  title="Removing a unit safely"
                  description="Deletion is only offered where nothing at all points at the unit."
                >
                  <div className="space-y-4">
                    <p className="flex items-start gap-2 rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
                      <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                      <span>
                        {blocked.length} of {data.units.length} units cannot be deleted because
                        employees, positions or configuration still reference them. Deleting one
                        would leave those records pointing at a unit that no longer exists, and
                        would break historical reporting. Close the unit from a date instead — it
                        stops being selectable, and everything already posted to it stays readable.
                      </span>
                    </p>
                    <div className="grid gap-3 sm:grid-cols-2">
                      <div className="rounded-md border bg-surface-muted p-3 text-sm">
                        <h3 className="font-medium">Close only ({blocked.length})</h3>
                        <ul className="mt-1.5 space-y-1 text-xs text-muted-foreground">
                          {blocked.slice(0, 4).map((u) => (
                            <li key={u.id}>
                              <span className="font-medium text-foreground">{u.name}</span> —{" "}
                              {u.references.join(", ")}
                            </li>
                          ))}
                          {blocked.length > 4 ? <li>and {blocked.length - 4} more</li> : null}
                        </ul>
                      </div>
                      <div className="rounded-md border bg-surface-muted p-3 text-sm">
                        <h3 className="font-medium">Safe to remove ({removable.length})</h3>
                        <ul className="mt-1.5 space-y-1 text-xs text-muted-foreground">
                          {removable.length ? (
                            removable.map((u) => (
                              <li key={u.id}>
                                <span className="font-medium text-foreground">{u.name}</span> —
                                never used since it was created on {fmt(u.effectiveFrom)}
                              </li>
                            ))
                          ) : (
                            <li>Nothing can be removed outright at the moment.</li>
                          )}
                        </ul>
                      </div>
                    </div>
                    <CloseUnitForm
                      units={closable}
                      onAdd={(d) =>
                        setDrafts((s) => [...s.filter((x) => x.unitId !== d.unitId), d])
                      }
                      onRefresh={refresh}
                    />
                    <p aria-live="polite" className="text-xs text-muted-foreground">
                      {drafts.length === 0
                        ? "No draft changes in this session."
                        : `${drafts.length} draft change${
                            drafts.length === 1 ? "" : "s"
                          } held in this browser only. Nothing has been saved.`}
                    </p>
                  </div>
                </DetailSection>

                <DetailSection
                  title="Scheduled changes"
                  description="Structural changes that are recorded but not yet in force. Until the effective date arrives, none of them affects an employee record, a report or a pay run."
                >
                  {future.length === 0 && draftChanges.length === 0 ? (
                    <EmptyState
                      title="Nothing is scheduled"
                      body="Every recorded change is already in force. New changes appear here as soon as they are given a future effective date."
                    />
                  ) : (
                    <ul className="space-y-3">
                      {draftChanges.map((c) => (
                        <ScheduledChangeItem
                          key={c.id}
                          change={c}
                          draft
                          onRemove={() => setDrafts((s) => s.filter((d) => d.id !== c.id))}
                        />
                      ))}
                      {future.map((c) => (
                        <ScheduledChangeItem key={c.id} change={c} />
                      ))}
                    </ul>
                  )}
                </DetailSection>

                <DetailSection
                  title="Change history"
                  description="Who changed the structure, when, and why. The audit trail is written for every structural change and cannot be edited."
                >
                  <div className="flex flex-wrap items-center gap-2 pb-3 text-xs text-muted-foreground">
                    <History className="size-3.5" aria-hidden />
                    Showing the last {data.audit.length} structural changes across all three
                    entities.
                  </div>
                  <StatusTimeline events={data.audit} title="Recent structural changes" />
                  <dl className="mt-5 grid gap-3 border-t pt-4 text-xs sm:grid-cols-2">
                    {(["Active", "Closing", "Closed", "Not yet in force"] as const).map((s) => (
                      <div key={s} className="min-w-0">
                        <dt className="font-medium">{s}</dt>
                        <dd className="text-muted-foreground">{unitStateExplanation[s]}</dd>
                      </div>
                    ))}
                  </dl>
                </DetailSection>
              </div>
            );
          }}
        </Async>
      </div>

      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(o) => !o && setDeleting(null)}
        title={`Delete ${deleting ?? "this unit"}?`}
        consequence="The unit is removed from the structure permanently. Only a unit with no employees, no history and no open positions can be deleted."
        detail={
          <span className="block text-xs text-muted-foreground">
            To retire a unit that has been used, close it with an effective date instead. Closing
            keeps its history; deleting does not.
          </span>
        }
        confirmLabel="Delete unit"
        cancelLabel="Keep it"
        destructive
        onConfirm={() => {
          const name = deleting;
          setDeleting(null);
          feedback.removed(`${name} deleted.`, () => feedback.note(`${name} restored.`));
        }}
      />
    </ConfigurationShell>
      </AuthGate>
  );
}
