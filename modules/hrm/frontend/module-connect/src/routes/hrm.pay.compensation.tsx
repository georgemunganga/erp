import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { ChevronLeft, ChevronRight, Info, Pencil, ShieldAlert, Unplug } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
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
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ImportDialog } from "@/platform/components/ImportExport/ImportDialog";
import { realApi, useApi } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { feedback } from "@/platform/feedback";
import {
  demoEntityTree,
  flattenEntityTree,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";

export const Route = createFileRoute("/hrm/pay/compensation")({
  head: () => ({
    meta: [
      { title: "Compensation — Newworldcargo HRM" },
      {
        name: "description",
        content:
          "Per-worker salary structures and a live overview of configured benefits and claims.",
      },
      { property: "og:title", content: "Compensation — Newworldcargo HRM" },
      {
        property: "og:description",
        content:
          "Per-worker salary structures and a live overview of configured benefits and claims.",
      },
    ],
  }),
  component: CompensationPage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
const COMPENSATION_PAGE_SIZE = 25;

type Raw = Record<string, unknown>;
type CompensationState = {
  workers: Raw[];
  activeWorkers: Raw[];
  totalCount: number;
  page: number;
  pageSize: number;
  profiles: Raw[];
  components: Raw[];
  groups: Raw[];
  locations: Raw[];
};

/** Compa-ratio is shown as a number and a word — never a bare colour. */
function CompaRatio({ value }: { value: number }) {
  const label =
    value < 0.9 ? "Below band midpoint" : value > 1.1 ? "Above band midpoint" : "Around midpoint";
  const tone = value < 0.9 ? "text-warning" : value > 1.1 ? "text-info" : "";
  return (
    <span className="block">
      <span className={`tabular text-sm font-medium ${tone}`}>{value.toFixed(2)}</span>
      <span className="block text-[11px] text-muted-foreground">{label}</span>
    </span>
  );
}

/* ------------------------------------------------------------------ */

async function loadCompensation(params: Record<string, unknown> = {}): Promise<CompensationState> {
  if (!USE_REAL) return { workers: [], activeWorkers: [], totalCount: 0, page: 1, pageSize: COMPENSATION_PAGE_SIZE, profiles: [], components: [], groups: [], locations: [] };
  const [workers, activeWorkers, profiles, components, groups, locations] = await Promise.all([
    realApi.employees(params),
    loadActiveWorkers(),
    realApi.payrollProfiles(),
    realApi.payrollComponents(),
    realApi.payrollPayGroups(),
    realApi.locations(),
  ]);
  const workerItems = Array.isArray(workers) ? (workers as Raw[]) : ((workers?.items ?? []) as Raw[]);
  return {
    workers: workerItems,
    activeWorkers,
    totalCount: Array.isArray(workers) ? workerItems.length : Number(workers?.totalCount ?? workerItems.length),
    page: Array.isArray(workers) ? 1 : Number((workers as { page?: number })?.page ?? params.page ?? 1),
    pageSize: Array.isArray(workers) ? workerItems.length : Number((workers as { pageSize?: number })?.pageSize ?? params.pageSize ?? COMPENSATION_PAGE_SIZE),
    profiles: Array.isArray(profiles) ? (profiles as Raw[]) : [],
    components: Array.isArray(components) ? (components as Raw[]) : [],
    groups: Array.isArray(groups) ? (groups as Raw[]) : [],
    locations: Array.isArray(locations) ? (locations as Raw[]) : [],
  };
}

async function loadActiveWorkers(): Promise<Raw[]> {
  const all: Raw[] = [];
  for (let page = 1; ; page += 1) {
    const response = await realApi.employees({ status: "active", page, pageSize: 200 });
    const items = Array.isArray(response) ? response as Raw[] : (response.items ?? []) as Raw[];
    all.push(...items);
    if (Array.isArray(response) || items.length === 0 || all.length >= Number(response.totalCount ?? all.length)) break;
  }
  return all;
}

function WorkerPayDialog({
  worker,
  state,
  open,
  onOpenChange,
  onSaved,
}: {
  worker: Raw | null;
  state: { profiles: Raw[]; components: Raw[]; groups: Raw[] };
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSaved: () => void;
}) {
  const [payGroupId, setPayGroupId] = useState("");
  const [payBasis, setPayBasis] = useState<"salary" | "timesheet">("salary");
  const [overtimeCategory, setOvertimeCategory] = useState<"ordinary" | "watchperson-guard">("ordinary");
  const [weeklyOvertimeThresholdHours, setWeeklyOvertimeThresholdHours] = useState("48");
  const [monthlyOvertimeDivisor, setMonthlyOvertimeDivisor] = useState("208");
  const [effectiveFrom, setEffectiveFrom] = useState(
    () => new Date().toISOString().slice(0, 10),
  );
  const [values, setValues] = useState<Raw[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resolvedComponents, setResolvedComponents] = useState<Raw[]>([]);
  const [resolvedGroups, setResolvedGroups] = useState<Raw[]>([]);

  // Self-healing loader: the dialog fetches its own data when it opens, and
  // uses it to initialize values exactly once per open. It must NEVER re-run
  // the initialization after the user has started editing — so the reset is
  // guarded by a ref that flips when the profile is first applied.
  const initializedRef = useRef(false);
  const latestStateRef = useRef(state);
  latestStateRef.current = state;
  useEffect(() => {
    if (!open || !worker) {
      initializedRef.current = false;
      return;
    }
    let live = true;
    Promise.all([realApi.payrollComponents(), realApi.payrollProfiles(), realApi.payrollPayGroups()])
      .then(([components, profiles, groups]) => {
        if (!live) return;
        setResolvedComponents(Array.isArray(components) ? (components as Raw[]) : []);
        setResolvedGroups(Array.isArray(groups) ? (groups as Raw[]) : []);
        if (initializedRef.current) return; // user already editing — never overwrite
        initializedRef.current = true;
        const snap = latestStateRef.current;
        const wid = String(worker.id ?? "");
        const profilesArr = Array.isArray(profiles) ? (profiles as Raw[]) : [];
        const existing = profilesArr.find((p) => String(p.workerId) === wid);
        const groupList = Array.isArray(groups) && groups.length ? (groups as Raw[]) : snap.groups;
        const defaults = groupList.find((g) => Boolean(g.isDefault))?.id ?? groupList[0]?.id ?? "";
        setPayGroupId(existing ? String(existing.payGroupId ?? "") : String(defaults));
        const basis = String(existing?.payBasis ?? "salary").toLowerCase();
        setPayBasis(basis === "timesheet" ? "timesheet" : "salary");
        setEffectiveFrom(
          existing
            ? String(existing.effectiveFrom ?? new Date().toISOString().slice(0, 10))
            : new Date().toISOString().slice(0, 10),
        );
        const comps = Array.isArray(components) ? (components as Raw[]) : snap.components;
        const statutoryCodes = new Set(
          comps
            .filter((c) => Boolean(c.isStatutory) && Boolean(c.isActive))
            .map((c) => String(c.code ?? "")),
        );
        const existingBasis = String(existing?.payBasis ?? "salary").toLowerCase();
        setPayBasis(existingBasis === "timesheet" ? "timesheet" : "salary");
        const existingOvertimeCategory = String(existing?.overtimeCategory ?? "ordinary").toLowerCase();
        const normalizedOvertimeCategory = existingOvertimeCategory === "watchperson-guard" ? "watchperson-guard" : "ordinary";
        setOvertimeCategory(normalizedOvertimeCategory);
        setWeeklyOvertimeThresholdHours(String(existing?.weeklyOvertimeThresholdHours ?? (normalizedOvertimeCategory === "watchperson-guard" ? 60 : 48)));
        setMonthlyOvertimeDivisor(String(existing?.monthlyOvertimeDivisor ?? (normalizedOvertimeCategory === "watchperson-guard" ? 240 : 208)));
        setValues(
          comps
            .filter((c) => Boolean(c.isActive) && !c.isArchived)
            .map((comp) => {
              const code = String(comp.code ?? "");
              const existingValue = ((existing?.values as Raw[] | undefined) ?? []).find(
                (v) => String(v.componentId) === String(comp.id),
              );
              return {
                componentId: String(comp.id ?? ""),
                code,
                name: String(comp.name ?? code),
                isStatutory: statutoryCodes.has(code),
                isOptional: Boolean(comp.componentType !== "earning"),
                amount: existingValue ? String(existingValue.amount ?? "0") : "0",
              };
            }),
        );
      })
      .catch(() => {
        // Leave the dialog usable with the parent snapshot already passed.
      });
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, worker?.id]);

  if (!worker) return null;
  const workerId = String(worker.id ?? "");
  const workerName = String(worker.fullName ?? `${worker.firstName ?? ""} ${worker.lastName ?? ""}`);
  const openProfile = state.profiles.find((p) => String(p.workerId) === workerId);
  const statutoryCodes = new Set(
    state.components
      .filter((c) => Boolean(c.isStatutory) && Boolean(c.isActive))
      .map((c) => String(c.code ?? "")),
  );

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        // The async effect initializes the form exactly once per open, so the
        // synchronous handler never clobbers a user's in-flight selection.
        if (o && !initializedRef.current) initializedRef.current = true;
      }}
    >
      <DialogContent className="max-h-[90dvh] max-w-xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{workerName}</DialogTitle>
          <DialogDescription>
            {openProfile
              ? `Updating the open profile effective from ${String(openProfile.effectiveFrom)}. A run calculates on the profile open at the period start.`
              : "No pay profile exists yet — assigning one decides what the next run posts for this worker."}
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            if (!payGroupId) {
              setError("Pick the pay group this worker runs on.");
              return;
            }
            if (!/^\d{4}-\d{2}-\d{2}$/.test(effectiveFrom)) {
              setError("Effective from must be a date like 2026-09-01.");
              return;
            }
            const payload = values
              .filter((v) => !v.isStatutory || Number(v.amount) > 0)
              .map((v) => ({
                componentId: String(v.componentId),
                amount: Number(v.amount) || 0,
              }));
            const basicId = values.find((v) => String(v.code).toLowerCase() === "basic")?.componentId;
            const basic = basicId ? payload.find((v) => String(v.componentId) === String(basicId)) : undefined;
            if (!basic || !Number(basic.amount)) {
              setError("Basic pay is mandatory — every worker needs a starting basic.");
              return;
            }
            const weeklyThreshold = Number(weeklyOvertimeThresholdHours);
            const overtimeDivisor = Number(monthlyOvertimeDivisor);
            if (!weeklyThreshold || weeklyThreshold <= 0 || !overtimeDivisor || overtimeDivisor <= 0) {
              setError("Overtime weekly threshold and monthly divisor must be greater than zero.");
              return;
            }
            setBusy(true);
            try {
              await realApi.createPayrollProfile(workerId, {
                payGroupId,
                effectiveFrom,
                values: payload,
                payBasis,
                overtimeCategory,
                weeklyOvertimeThresholdHours: weeklyThreshold,
                monthlyOvertimeDivisor: overtimeDivisor,
              });
              feedback.saved(`${workerName}'s pay structure saved for the ${effectiveFrom} start date.`);
              onSaved();
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
          }
        }}
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="min-w-0">
            <Label>Pay group</Label>
            <Select value={payGroupId} onValueChange={setPayGroupId}>
              <SelectTrigger className="mt-1.5 w-full">
                <SelectValue placeholder="Pick the pay group this worker runs on" />
              </SelectTrigger>
              <SelectContent>
                {(resolvedGroups.length ? resolvedGroups : state.groups).map((g) => (
                  <SelectItem key={String(g.id)} value={String(g.id)}>
                    {String(g.name ?? g.code)}
                    {g.isDefault ? " — default" : ""}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-0">
            <Label htmlFor="cp-effective">Effective from</Label>
            <Input
              id="cp-effective"
              type="date"
              value={effectiveFrom}
              onChange={(e) => setEffectiveFrom(e.target.value)}
              className="mt-1.5"
            />
          </div>
        </div>
        <div className="rounded-md border border-border bg-surface-muted p-3">
          <Label className="flex items-center gap-2">
            Pay basis
            <span className="text-xs font-normal text-muted-foreground">— control, not a new pay mode yet</span>
          </Label>
          <div className="mt-2 flex flex-wrap gap-3">
            {(["salary", "timesheet"] as const).map((b) => (
              <label key={b} className="flex cursor-pointer items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="pay-basis"
                  checked={payBasis === b}
                  onChange={() => setPayBasis(b)}
                  aria-label={`Pay basis ${b}`}
                />
                {b === "salary" ? "Salary (monthly, per component)" : "Timesheet (when timesheet pay ships)"}
              </label>
            ))}
          </div>
          <p className="mt-2 text-xs text-muted-foreground">
            Nobody is paid hourly today. “Timesheet” is a planning flag HR can switch on ahead of
            the future timesheet-driven pay feature — every run still calculates salary-basis
            regardless of what is selected here.
          </p>
        </div>
        <div className="rounded-md border border-border bg-surface-muted p-3">
          <Label>Overtime policy</Label>
          <div className="mt-2 grid gap-3 sm:grid-cols-[1fr_120px_120px]">
            <Select
              value={overtimeCategory}
              onValueChange={(value) => {
                const next = value === "watchperson-guard" ? "watchperson-guard" : "ordinary";
                setOvertimeCategory(next);
                setWeeklyOvertimeThresholdHours(next === "watchperson-guard" ? "60" : "48");
                setMonthlyOvertimeDivisor(next === "watchperson-guard" ? "240" : "208");
              }}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="ordinary">Ordinary employee</SelectItem>
                <SelectItem value="watchperson-guard">Watchperson / guard</SelectItem>
              </SelectContent>
            </Select>
            <Input
              type="number"
              min={1}
              step="0.01"
              value={weeklyOvertimeThresholdHours}
              onChange={(e) => setWeeklyOvertimeThresholdHours(e.target.value)}
              aria-label="Weekly overtime threshold hours"
            />
            <Input
              type="number"
              min={1}
              step="0.01"
              value={monthlyOvertimeDivisor}
              onChange={(e) => setMonthlyOvertimeDivisor(e.target.value)}
              aria-label="Monthly overtime divisor"
            />
          </div>
          <p className="mt-2 text-xs text-muted-foreground">
            Ordinary employees use 48 weekly hours and basic / 208. Watchperson or guard profiles
            use 60 weekly hours and basic / 240 unless HR overrides the figures here.
          </p>
        </div>
        <div className="space-y-2">
          <Label>Component amounts — the opening figures a run posts to</Label>
          <div className="max-h-72 space-y-2 overflow-y-auto rounded-md border p-3">
            {values.map((v) => (
              <div key={String(v.componentId)} className="flex items-center gap-2">
                <span className={`min-w-0 flex-1 truncate text-sm ${v.isStatutory ? "text-muted-foreground" : ""}`}>
                  {String(v.name ?? v.code)}
                  {v.isStatutory ? (
                    <span className="ml-1.5 align-middle rounded-full border border-info/40 bg-info-soft px-1.5 py-0.5 text-[10px] text-info">
                      statutory
                    </span>
                  ) : null}
                </span>
                {v.isStatutory ? (
                  <span className="text-xs text-muted-foreground">
                    Computed from basic at run time
                  </span>
                ) : (
                  <Input
                    type="number"
                    min={0}
                    step="0.01"
                    value={String(v.amount)}
                    onChange={(e) =>
                      setValues((prev) =>
                        prev.map((x) =>
                          String(x.componentId) === String(v.componentId) ? { ...x, amount: e.target.value } : x,
                        ),
                      )
                    }
                    className="w-28"
                    aria-label={`Amount for ${String(v.name ?? v.code)}`}
                  />
                )}
              </div>
            ))}
            {!values.length ? (
              <p className="py-4 text-center text-xs text-muted-foreground">
                No active components exist yet — configure Salary components first.
              </p>
            ) : null}
          </div>
          <p className="text-xs text-muted-foreground">
            Statutory components (PAYE, NAPSA, NHIMA) compute themselves from basic pay — their
            rates live on the payroll setup screens and are not re-typed per worker.
          </p>
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
          <Button type="submit" disabled={busy}>
            {busy ? "Saving…" : openProfile ? "Update pay structure" : "Assign pay structure"}
          </Button>
        </div>
      </form>
    </DialogContent>
  </Dialog>
  );
}

/* ------------------------------------------------------------------ */

function CompensationPage() {
  const userRoles = new Set(useAuth().user?.roles ?? []);
  const canAct = userRoles.has("hr_admin") || userRoles.has("hr_ops") || userRoles.has("payroll");
  const [editingWorker, setEditingWorker] = useState<Raw | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("active");
  const [typeFilter, setTypeFilter] = useState("");
  const [orgFilter, setOrgFilter] = useState("");
  const [locationFilter, setLocationFilter] = useState("");
  const [gradeFilter, setGradeFilter] = useState("");
  const [payGroupFilter, setPayGroupFilter] = useState("");
  const [profileFilter, setProfileFilter] = useState("");
  const [page, setPage] = useState(1);

  const state = useApi(
    () =>
      loadCompensation({
        page,
        pageSize: COMPENSATION_PAGE_SIZE,
        ...(search.trim() ? { search: search.trim() } : {}),
        ...(statusFilter ? { status: statusFilter } : {}),
        ...(typeFilter ? { workerType: typeFilter } : {}),
        ...(orgFilter?.startsWith("entity:") ? { legalEntityId: orgFilter.slice(7) } : {}),
        ...(orgFilter && !orgFilter.startsWith("entity:") ? { orgUnitId: orgFilter } : {}),
        ...(locationFilter ? { locationId: locationFilter } : {}),
        ...(gradeFilter ? { grade: gradeFilter } : {}),
      }),
    [search, statusFilter, typeFilter, orgFilter, locationFilter, gradeFilter, page],
  );

  const treeState = useApi<OrgTreeNode[]>(async () => {
    if (USE_REAL) return (await realApi.entityTree()) as OrgTreeNode[];
    return demoEntityTree;
  }, []);

  const data = state.data ?? { workers: [], activeWorkers: [], totalCount: 0, page: 1, pageSize: COMPENSATION_PAGE_SIZE, profiles: [], components: [], groups: [], locations: [] };
  const entityUnits = flattenEntityTree(treeState.data ?? []);
  const entityTreeOptions = treeToSelectOptions(treeState.data ?? []).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));

  const visibleWorkers = data.workers.filter((w) => {
    const profile = data.profiles.find((p) => String(p.workerId) === String(w.id));
    if (payGroupFilter && String(profile?.payGroupId ?? "") !== payGroupFilter) return false;
    if (profileFilter === "assigned" && !profile) return false;
    if (profileFilter === "missing" && profile) return false;
    return true;
  });

  const profileFor = (w: Raw) => data.profiles.find((p) => String(p.workerId) === String(w.id));
  const basicComponent = data.components.find((c) => String(c.code ?? "").toLowerCase() === "basic");
  const activeWorkerCount = data.activeWorkers.length;
  const missingProfile = data.activeWorkers.filter((w) => !profileFor(w));
  const profilesMissingBasic = data.activeWorkers.filter((w) => {
    const profile = profileFor(w);
    if (!profile) return false;
    const values = (profile.values as Raw[] | undefined) ?? [];
    return !values.some((v) => {
      const sameComponent = basicComponent
        ? String(v.componentId) === String(basicComponent.id)
        : String(v.componentCode ?? "").toLowerCase() === "basic";
      return sameComponent && Number(v.amount ?? 0) > 0;
    });
  });
  const readyWorkerCount = Math.max(0, activeWorkerCount - missingProfile.length - profilesMissingBasic.length);
  const totalPages = Math.max(1, Math.ceil(data.totalCount / COMPENSATION_PAGE_SIZE));
  const pageStart = data.totalCount === 0 ? 0 : (page - 1) * COMPENSATION_PAGE_SIZE + 1;
  const pageEnd = Math.min(page * COMPENSATION_PAGE_SIZE, data.totalCount);
  const gradeOptions = Array.from(
    new Set(data.workers.map((w) => String(w.grade ?? "").trim()).filter(Boolean)),
  ).sort((a, b) => a.localeCompare(b));
  const resetPagedFilter = (setter: (value: string) => void) => (value: string) => {
    setter(value === "all" ? "" : value);
    setPage(1);
  };

  return (
    <AuthGate>
    <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Compensation"
        description="Manage the pay profiles and component amounts used by payroll. Benefit types, claims and allowances are managed on the Benefits page."
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
            <ShieldAlert className="size-3.5" aria-hidden />
            Restricted — visible to Payroll and HR only
          </span>
        }
      />
      <div className="-mt-4 mb-4 flex justify-end gap-2">
        <Button variant="outline" asChild><Link to="/hrm/benefits">Open Benefits</Link></Button>
        <ImportDialog
          typeKey="payroll-profiles"
          onDone={() => void state.reload()}
          demoSample={[
            { employeeNo: "EMP-001", payGroup: "Monthly ZMW", basic: "45000", "housing-allowance": "6750" },
          ]}
        />
      </div>

      {!USE_REAL ? (
        <p className="flex items-start gap-2 rounded-lg border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
          <Unplug className="mt-0.5 size-4 shrink-0" aria-hidden />
          Live API is off for this build. The real surfaces exist on the backend — this page shows
          the intended layout without connecting.
        </p>
      ) : null}

      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className="rounded-lg border bg-surface p-4">
          <p className="text-xs text-muted-foreground">Pay profiles ready</p>
          <p className="mt-1 text-2xl font-semibold tabular">{readyWorkerCount}/{activeWorkerCount}</p>
          <p className="mt-1 text-xs text-muted-foreground">Active workers with a usable pay profile. Release checks are separate.</p>
        </div>
        <div className="rounded-lg border bg-surface p-4">
          <p className="text-xs text-muted-foreground">Missing profile</p>
          <p className={`mt-1 text-2xl font-semibold tabular ${missingProfile.length ? "text-warning" : ""}`}>
            {missingProfile.length}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">These workers will not calculate correctly.</p>
        </div>
        <div className="rounded-lg border bg-surface p-4">
          <p className="text-xs text-muted-foreground">Missing basic pay</p>
          <p className={`mt-1 text-2xl font-semibold tabular ${profilesMissingBasic.length ? "text-warning" : ""}`}>
            {profilesMissingBasic.length}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">Basic pay is mandatory before running payroll.</p>
        </div>
      </div>

      <div className="mt-4 rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
        <p className="flex items-start gap-2 font-medium">
          <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
          The salary structure is what a run posts to
        </p>
        <p className="mt-1.5 pl-6">
          Every active worker should carry an open pay profile: a pay group, an effective date
          and component amounts. Basic pay is mandatory; statutory components (PAYE, NAPSA,
          NHIMA) compute themselves from basic at run time, so they never need re-typing here.
        </p>
      </div>

      <section className="mt-4 min-w-0 overflow-hidden rounded-xl border bg-surface shadow-sm">
      <div className="grid min-w-0 gap-3 border-b p-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="min-w-0 sm:col-span-2">
          <Label htmlFor="cp-search" className="sr-only">
            Search workers
          </Label>
          <Input
            id="cp-search"
            placeholder="Search by employee number, name or job title"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>
        <Select value={statusFilter || "all"} onValueChange={resetPagedFilter(setStatusFilter)}>
          <SelectTrigger aria-label="Status">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Status: all</SelectItem>
            <SelectItem value="active">Active</SelectItem>
            <SelectItem value="on-leave">On leave</SelectItem>
            <SelectItem value="notice">Notice period</SelectItem>
            <SelectItem value="pre-hire">Pre-hire</SelectItem>
            <SelectItem value="terminated">Terminated</SelectItem>
          </SelectContent>
        </Select>
        <Select value={typeFilter || "all"} onValueChange={resetPagedFilter(setTypeFilter)}>
          <SelectTrigger aria-label="Employment type">
            <SelectValue placeholder="Employment type" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Type: all</SelectItem>
            <SelectItem value="employee">Permanent</SelectItem>
            <SelectItem value="contingent">Contractor</SelectItem>
            <SelectItem value="intern">Intern</SelectItem>
            <SelectItem value="volunteer">Volunteer</SelectItem>
          </SelectContent>
        </Select>
        <Select value={locationFilter || "all"} onValueChange={resetPagedFilter(setLocationFilter)}>
          <SelectTrigger aria-label="Branch">
            <SelectValue placeholder="Branch" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Branch: all</SelectItem>
            {data.locations.map((location) => (
              <SelectItem key={String(location.id)} value={String(location.id)}>
                {String(location.name ?? location.code)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={orgFilter || "all"} onValueChange={resetPagedFilter(setOrgFilter)}>
          <SelectTrigger aria-label="Entity and department">
            <SelectValue placeholder="Entity / department" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Entity & branch: all</SelectItem>
            {entityTreeOptions.map((o) => (
              <SelectItem
                key={o.value}
                value={o.value}
                className={o.entity ? "font-semibold text-primary" : undefined}
              >
                {o.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={gradeFilter || "all"} onValueChange={resetPagedFilter(setGradeFilter)}>
          <SelectTrigger aria-label="Grade">
            <SelectValue placeholder="Grade" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Grade: all</SelectItem>
            {gradeOptions.map((g) => (
              <SelectItem key={g} value={g}>
                {g}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={payGroupFilter || "all"} onValueChange={resetPagedFilter(setPayGroupFilter)}>
          <SelectTrigger aria-label="Pay group">
            <SelectValue placeholder="Pay group" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Pay group: all</SelectItem>
            {data.groups.map((g) => (
              <SelectItem key={String(g.id)} value={String(g.id)}>
                {String(g.name ?? g.code)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={profileFilter || "all"} onValueChange={resetPagedFilter(setProfileFilter)}>
          <SelectTrigger aria-label="Pay profile">
            <SelectValue placeholder="Pay profile" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Profile: all</SelectItem>
            <SelectItem value="assigned">Assigned</SelectItem>
            <SelectItem value="missing">Missing</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <Async state={state} rows={5}>
        {(d) => (
          <div className="max-w-full overflow-x-auto">
            <RecordTable className="min-w-[1120px]">
              <caption className="sr-only">Workers and their open pay profiles</caption>
              <thead className="border-b bg-surface-muted">
                <tr>
                  {["Employee", "No.", "Department", "Branch", "Job title", "Pay group", "Effective from", "Pay basis", "Overtime", "Action"].map((h) => (
                    <th
                      key={h}
                      scope="col"
                      className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y">
                {visibleWorkers.map((w) => {
                  const profile = profileFor(w);
                  const group = data.groups.find(
                    (g) => String(g.id) === (profile ? String(profile.payGroupId) : ""),
                  );
                  return (
                    <tr key={String(w.id)} className="hover:bg-surface-muted">
                      <td className="max-w-52 truncate px-3 py-3 font-medium">{String(w.fullName ?? "")}</td>
                      <td className="px-3 py-3 font-mono text-xs text-muted-foreground">{String(w.employeeNo ?? "")}</td>
                      <td className="max-w-48 truncate px-3 py-3 text-muted-foreground">{String(w.orgUnitName ?? "—")}</td>
                      <td className="max-w-48 truncate px-3 py-3 text-muted-foreground">{String(w.locationName ?? "—")}</td>
                      <td className="max-w-48 truncate px-3 py-3 text-muted-foreground">{String(w.jobTitle ?? "—")}</td>
                      <td className="px-3 py-3">{profile ? String(group?.name ?? group?.code ?? "—") : <span className="text-warning">Not assigned</span>}</td>
                      <td className="px-3 py-3 font-mono text-xs">{profile ? String(profile.effectiveFrom) : "—"}</td>
                      <td className="px-3 py-3">
                        {profile && canAct ? (
                          <Select
                            value={String(profile.payBasis ?? "salary").toLowerCase() === "timesheet" ? "timesheet" : "salary"}
                          onValueChange={async (next: string) => {
                              const basis = next === "timesheet" ? "timesheet" : "salary";
                              try {
                                await realApi.setPayBasis(String(w.id), basis);
                                await state.reload();
                                feedback.saved(
                                  basis === "timesheet"
                                    ? "Timesheet flag set — salary-basis pay still applies until timesheet pay ships."
                                    : "Pay basis is salary — standard per-component calculation.",
                                );
                              } catch {
                                feedback.blocked("Pay basis was not updated.", "The HRM API rejected the change.");
                              }
                            }}
                          >
                            <SelectTrigger className="h-8 w-32">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="salary">Salary</SelectItem>
                              <SelectItem value="timesheet">Timesheet — not live</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : (
                          <span className="text-xs text-muted-foreground">{profile ? String(profile.payBasis ?? "salary") : "—"}</span>
                        )}
                      </td>
                      <td className="px-3 py-3">
                        {profile && canAct ? (
                          <Select
                            value={String(profile.overtimeCategory ?? "ordinary").toLowerCase() === "watchperson-guard" ? "watchperson-guard" : "ordinary"}
                            onValueChange={async (next: string) => {
                              const category = next === "watchperson-guard" ? "watchperson-guard" : "ordinary";
                              try {
                                await realApi.setOvertimePolicy(String(w.id), {
                                  overtimeCategory: category,
                                  weeklyOvertimeThresholdHours: category === "watchperson-guard" ? 60 : 48,
                                  monthlyOvertimeDivisor: category === "watchperson-guard" ? 240 : 208,
                                });
                                await state.reload();
                                feedback.saved(
                                  category === "watchperson-guard"
                                    ? "Overtime policy set to watchperson/guard: 60 weekly hours, basic divided by 240."
                                    : "Overtime policy set to ordinary: 48 weekly hours, basic divided by 208.",
                                );
                              } catch {
                                feedback.blocked("Overtime policy was not updated.", "The HRM API rejected the change.");
                              }
                            }}
                          >
                            <SelectTrigger className="h-8 w-40">
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="ordinary">Ordinary</SelectItem>
                              <SelectItem value="watchperson-guard">Watchperson / guard</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : (
                          <span className="text-xs text-muted-foreground">
                            {profile ? String(profile.overtimeCategory ?? "ordinary") : "—"}
                          </span>
                        )}
                      </td>
                      <td className="px-3 py-3 text-right">
                        {canAct ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-8"
                            onClick={() => setEditingWorker(w)}
                          >
                            <Pencil className="size-3.5" aria-hidden />
                            Edit pay
                            <span className="sr-only"> pay structure for {String(w.fullName ?? "")}</span>
                          </Button>
                        ) : (
                          <span className="text-xs text-muted-foreground">Read-only</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
                {!visibleWorkers.length ? (
                  <tr>
                    <td colSpan={10} className="px-3 py-8 text-center text-sm text-muted-foreground">
                      No workers match{search ? ` "${search}"` : ""}.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </RecordTable>
          </div>
        )}
      </Async>
      {USE_REAL ? (
        <div className="flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3 text-sm">
          <span className="text-muted-foreground">
            Showing {pageStart}-{pageEnd} of {data.totalCount} workers
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
      </section>

      <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
        <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
        Exact amounts stay off the screen by default — opening the edit dialog shows them and
        the change lands in the profile, never on a colleague's file they are not authorised to
        see.
      </p>
      <WorkerPayDialog
        worker={editingWorker}
        state={state.data ?? { profiles: [], components: [], groups: [] }}
        open={editingWorker !== null}
        onOpenChange={(o) => {
          if (!o) setEditingWorker(null);
        }}
        onSaved={() => void state.reload()}
      />
    </AppShell>
    </AuthGate>
  );
}
