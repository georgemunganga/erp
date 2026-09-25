import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, Ban, Check, Info, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { money } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { realApi, useApi } from "@/platform/use-api";
import {
  demoEntityTree,
  flattenEntityTree,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/payroll/runs/new")({
  head: () => ({
    meta: [
      { title: "Start a pay run — New World Cargo HRM" },
      { name: "description", content: "Open a pay period: choose the entity and pay group, confirm who is in and who is deliberately out, and check readiness before calculating." },
      { property: "og:title", content: "Start a pay run — New World Cargo HRM" },
      { property: "og:description", content: "Open a pay period, confirm the population, and check readiness before calculating." },
    ],
  }),
  component: NewRun,
});

const ENTITIES = [
  { id: "ent-zm1", name: "New World Cargo Zambia Ltd", currency: "ZMW" },
  { id: "ent-zm2", name: "New World Cargo Services Zambia Ltd", currency: "ZMW" },
  { id: "ent-zm3", name: "New World Cargo Holdings Zambia Ltd", currency: "ZMW" },
];

const PAY_GROUPS = ["Monthly salaried", "Monthly — management", "Weekly — site crew"];

/** Mock mode deliberately starts empty; it must never be mistaken for production data. */
const DEMO_READINESS: ReadinessItem[] = [];
const DEMO_POPULATION: PopulationRow[] = [];

function ReadinessRow({ item }: { item: { id: string; label: string; detail: string; state: "pass" | "warn" } }) {
  const pass = item.state === "pass";
  return (
    <li className="flex items-start gap-2 rounded-md border p-3">
      {pass ? (
        <Check className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
      ) : (
        <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
      )}
      <span className="min-w-0">
        <span className="block text-sm font-medium">
          {item.label}
          <span className={`ml-2 text-xs font-normal ${pass ? "text-success" : "text-warning"}`}>
            {pass ? "Ready" : "Needs attention"}
          </span>
        </span>
        <span className="block text-xs text-muted-foreground">{item.detail}</span>
      </span>
    </li>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

type ReadinessItem = { id: string; label: string; detail: string; state: "pass" | "warn" };
type PopulationRow = {
  id: string;
  name: string;
  employeeNo: string;
  payGroup: string;
  branch: string;
  department: string;
  status: string;
  startDate: string;
  endDate: string;
  in: boolean;
  note: string;
};

type LiveWorker = {
  id?: string;
  fullName?: string;
  employeeNo?: string;
  status?: string;
  workerType?: string;
  startDate?: string | null;
  endDate?: string | null;
  bankDetails?: Array<{ isPrimary?: boolean }>;
  orgUnitName?: string | null;
  locationName?: string | null;
};

type LiveProfile = {
  workerId?: string;
  payGroupId?: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  payGroupName?: string | null;
};

type LivePeriod = {
  id: string;
  periodLabel: string;
  startDate?: string | null;
  endDate?: string | null;
  cutoffDate: string;
  payDate: string;
  status: string;
};

function dateOnly(value: unknown): string | null {
  const text = String(value ?? "").trim();
  if (!text) return null;
  if (/^\d{4}-\d{2}-\d{2}/.test(text)) return text.slice(0, 10);
  const slash = text.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
  return slash ? `${slash[3]}-${slash[1]}-${slash[2]}` : null;
}

function dateInput(value: unknown): string {
  return dateOnly(value) ?? "";
}

function periodKey(periodLabel: string): string | null {
  if (/^\d{4}-\d{2}$/.test(periodLabel)) return periodLabel;
  const match = periodLabel.trim().match(/^([A-Za-z]{3,})\s+(\d{4})$/);
  if (!match) return null;
  const month = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"]
    .findIndex((name) => match[1].toLowerCase().startsWith(name));
  return month < 0 ? null : `${match[2]}-${String(month + 1).padStart(2, "0")}`;
}

function monthEnd(periodLabel: string): string {
  const key = periodKey(periodLabel);
  if (!key) return "9999-12-31";
  const [year, month] = key.split("-").map(Number);
  return new Date(Date.UTC(year, month, 0)).toISOString().slice(0, 10);
}

function overlapsPeriod(start: string | null, end: string | null, periodStart: string, periodEnd: string) {
  return (!start || start <= periodEnd) && (!end || end >= periodStart);
}

function NewRun() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [entityId, setEntityId] = useState(ENTITIES[0].id);
  const [payGroup, setPayGroup] = useState(PAY_GROUPS[0]);
  const [period, setPeriod] = useState("2026-08");
  const [payDate, setPayDate] = useState("2026-08-28");
  const [cutoff, setCutoff] = useState("2026-08-24");
  const [excluded, setExcluded] = useState<string[]>(
    DEMO_POPULATION.filter((p) => !p.in).map((p) => p.name),
  );
  const [note, setNote] = useState("");
  const [populationSearch, setPopulationSearch] = useState("");
  const [populationPayGroup, setPopulationPayGroup] = useState("all");
  const [populationBranch, setPopulationBranch] = useState("all");
  const [populationDepartment, setPopulationDepartment] = useState("all");
  const [populationStatus, setPopulationStatus] = useState("all");
  const [populationEligibility, setPopulationEligibility] = useState("all");

  const setup = useApi(
    async () => {
      const groups = (await realApi.payrollPayGroups()) as unknown as { id?: string; name?: string }[];
      const groupId = groups[0]?.id ?? "";
      const groupName = groups[0]?.name ?? "";
      const periods = groupId
        ? ((await realApi.payrollPayGroupPeriods(groupId)) as unknown as LivePeriod[])
        : [];
      const tree = USE_REAL
        ? ((await realApi.entityTree()) as unknown as OrgTreeNode[])
        : demoEntityTree;
      let workers: LiveWorker[] = [];
      if (USE_REAL) {
        const first = await realApi.employees({ includeArchived: "true", page: 1, pageSize: 100 });
        workers = ((first.items ?? []) as LiveWorker[]).slice();
        const total = Number(first.totalCount ?? workers.length);
        const pages = Math.ceil(total / 100);
        for (let page = 2; page <= pages; page += 1) {
          const next = await realApi.employees({ includeArchived: "true", page, pageSize: 100 });
          workers.push(...((next.items ?? []) as LiveWorker[]));
        }
      }
      const profiles = USE_REAL ? (await realApi.payrollProfiles()) as LiveProfile[] : [];
      const components = USE_REAL ? await realApi.payrollComponents() : [];
      const rules = USE_REAL ? await realApi.payrollContributionRules() : [];
      const slabs = USE_REAL ? await realApi.payrollTaxSlabs("2026") : [];
      return { periods, groupId, groupName, tree, workers, profiles, components, rules, slabs };
    },
    [],
  );
  useEffect(() => {
    if (USE_REAL && setup.data?.periods.length && !setup.data.periods.some((candidate) => candidate.periodLabel === period)) {
      setPeriod(setup.data.periods[0].periodLabel);
    }
  }, [period, setup.data]);

  const placementUnits = flattenEntityTree(setup.data?.tree ?? demoEntityTree);
  const placementOptions = treeToSelectOptions(setup.data?.tree ?? demoEntityTree).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));
  const chosenPeriod =
    setup.data?.periods.find((p) => p.periodLabel === period) ??
    (setup.data?.periods ?? [])[0];
  const periodStart = dateInput(chosenPeriod?.startDate) || `${periodKey(period) ?? period}-01`;
  const periodEnd = dateInput(chosenPeriod?.endDate) || monthEnd(period);
  const entity = ENTITIES.find((e) => e.id === entityId) ?? ENTITIES[0];
  const entityEntityId = placementUnits.find((p) => p.unitType === "entity")?.entityId ?? entityId;
  const periodCutoff = dateInput(chosenPeriod?.cutoffDate);
  const periodPayDate = dateInput(chosenPeriod?.payDate);
  useEffect(() => {
    if (periodCutoff) setCutoff(periodCutoff);
    if (periodPayDate) setPayDate(periodPayDate);
  }, [chosenPeriod?.id, periodCutoff, periodPayDate]);
  const effectiveCutoff = cutoff || periodCutoff;
  const effectivePayDate = payDate || periodPayDate;
  const dateOverrideProblem = USE_REAL && chosenPeriod &&
    ((periodCutoff && cutoff !== periodCutoff) || (periodPayDate && payDate !== periodPayDate))
    ? "These dates belong to the selected pay period. Change them here only to review a correction; save the approved dates in Payroll Setup before opening the run."
    : null;

  const livePopulation = useMemo<PopulationRow[]>(() => {
    const data = setup.data;
    if (!USE_REAL || !data) return [];
    return data.workers.map((worker) => {
      const workerId = String(worker.id ?? "");
      const workerName = String(worker.fullName ?? worker.employeeNo ?? "Unnamed worker");
      const profiles = data.profiles.filter((profile) =>
        String(profile.workerId ?? "") === workerId &&
        String(profile.payGroupId ?? "") === String(data.groupId ?? "") &&
        overlapsPeriod(dateOnly(profile.effectiveFrom), dateOnly(profile.effectiveTo), periodStart, periodEnd),
      );
      const profile = profiles.sort((a, b) => String(b.effectiveFrom ?? "").localeCompare(String(a.effectiveFrom ?? "")))[0];
      const workerStatus = String(worker.status ?? "").toLowerCase();
      const workerInPeriod = overlapsPeriod(dateOnly(worker.startDate), dateOnly(worker.endDate), periodStart, periodEnd);
      const statusEligible = ["active", "on-leave", "notice"].includes(workerStatus);
      const eligible = Boolean(profile) && workerInPeriod && statusEligible;
      const note = eligible
        ? `${workerStatus || "Active"}, payroll profile effective for ${period}`
        : !profile
          ? `No payroll profile for this pay group effective in ${period}`
          : !workerInPeriod
            ? `Employment dates do not overlap ${period}`
            : `Worker status is ${worker.status ?? "not active"}`;
      return {
        id: workerId,
        name: workerName,
        employeeNo: String(worker.employeeNo ?? "—"),
        payGroup: String(profile?.payGroupName ?? data.groupName ?? "Not assigned"),
        branch: String(worker.locationName ?? "Not assigned"),
        department: String(worker.orgUnitName ?? "Not assigned"),
        status: String(worker.status ?? "Unknown"),
        startDate: dateInput(worker.startDate) || "—",
        endDate: dateInput(worker.endDate) || "—",
        in: eligible,
        note,
      };
    });
  }, [period, periodEnd, periodStart, setup.data]);

  const population = USE_REAL ? livePopulation : DEMO_POPULATION;
  const effectiveExcluded = USE_REAL ? population.filter((p) => !p.in).map((p) => p.id) : excluded;
  const included = population.filter((p) => !effectiveExcluded.includes(p.id));
  const populationOptions = useMemo(() => ({
    payGroups: [...new Set(population.map((row) => row.payGroup))].sort(),
    branches: [...new Set(population.map((row) => row.branch))].sort(),
    departments: [...new Set(population.map((row) => row.department))].sort(),
    statuses: [...new Set(population.map((row) => row.status))].sort(),
  }), [population]);
  const filteredPopulation = useMemo(() => {
    const query = populationSearch.trim().toLowerCase();
    return population.filter((row) => {
      const matchesText = !query || [row.name, row.employeeNo, row.payGroup, row.branch, row.department].join(" ").toLowerCase().includes(query);
      const matchesEligibility = populationEligibility === "all" || (populationEligibility === "eligible" ? row.in : !row.in);
      return matchesText &&
        (populationPayGroup === "all" || row.payGroup === populationPayGroup) &&
        (populationBranch === "all" || row.branch === populationBranch) &&
        (populationDepartment === "all" || row.department === populationDepartment) &&
        (populationStatus === "all" || row.status === populationStatus) &&
        matchesEligibility;
    });
  }, [population, populationBranch, populationDepartment, populationEligibility, populationPayGroup, populationSearch, populationStatus]);
  const liveWorkers = setup.data?.workers ?? [];
  const includedWorkerIds = new Set(included.map((p) => p.id));
  const includedWorkers = liveWorkers.filter((worker) => includedWorkerIds.has(String(worker.id ?? "")));
  const overlappingProfilesByWorker = new Map<string, number>();
  for (const profile of setup.data?.profiles ?? []) {
    const workerId = String(profile.workerId ?? "");
    if (workerId && String(profile.payGroupId ?? "") === String(setup.data?.groupId ?? "") &&
        overlapsPeriod(dateOnly(profile.effectiveFrom), dateOnly(profile.effectiveTo), periodStart, periodEnd)) {
      overlappingProfilesByWorker.set(workerId, (overlappingProfilesByWorker.get(workerId) ?? 0) + 1);
    }
  }
  const duplicateProfileWorkers = [...overlappingProfilesByWorker.values()].filter((count) => count > 1).length;
  const readiness = useMemo<ReadinessItem[]>(() => {
    if (!USE_REAL) return DEMO_READINESS;
    const componentCount = setup.data?.components.length ?? 0;
    const ruleCount = setup.data?.rules.length ?? 0;
    const slabCount = setup.data?.slabs.length ?? 0;
    const bankReady = included.length > 0 && includedWorkers.length === included.length &&
      includedWorkers.every((worker) => worker.bankDetails?.some((bank) => bank.isPrimary));
    const priorPeriods = (setup.data?.periods ?? [])
      .filter((candidate) => candidate.periodLabel !== chosenPeriod?.periodLabel && dateInput(candidate.endDate) !== null && dateInput(candidate.endDate)! < periodStart)
      .sort((a, b) => String(b.endDate ?? "").localeCompare(String(a.endDate ?? "")));
    const previous = priorPeriods[0];
    const previousClosed = Boolean(previous && ["closed", "locked"].includes(String(previous.status).toLowerCase()));
    return [
      { id: "country-pack", label: "Country pack active for the period", detail: `${componentCount} salary components, ${ruleCount} contribution rules and ${slabCount} tax slabs loaded for ${period}.`, state: componentCount > 0 && ruleCount > 0 && slabCount > 0 ? "pass" : "warn" },
      { id: "attendance-approval", label: "Attendance approved to cutoff", detail: "No persisted timesheet approval status is exposed for this period. Attendance is currently attendance-only; overtime approvals are tracked separately.", state: "warn" },
      { id: "bank-details", label: "Bank details present and verified", detail: included.length === 0 ? "No eligible workers with payroll profiles were found for this period." : `${includedWorkers.filter((worker) => worker.bankDetails?.some((bank) => bank.isPrimary)).length} of ${included.length} eligible workers have a primary bank account.`, state: bankReady ? "pass" : "warn" },
      { id: "duplicate-pay-groups", label: "No employee on two pay groups", detail: duplicateProfileWorkers === 0 ? "No overlapping payroll profiles were found for the selected pay group." : `${duplicateProfileWorkers} worker(s) have overlapping payroll profiles and need review.`, state: duplicateProfileWorkers === 0 ? "pass" : "warn" },
      { id: "previous-period", label: "Previous period closed", detail: previous ? `${previous.periodLabel} is ${previous.status}.` : "No prior period is registered for this pay group; verify the opening payroll period manually.", state: previousClosed ? "pass" : "warn" },
    ];
  }, [chosenPeriod?.periodLabel, duplicateProfileWorkers, included, includedWorkers, period, periodStart, setup.data]);
  const estimate = included.length * 20_878.88;
  const dateProblem = useMemo(
    () => effectiveCutoff && effectivePayDate && effectiveCutoff > effectivePayDate
      ? "The attendance approval deadline is after the pay date, so approved time would miss this run."
      : dateOverrideProblem,
    [dateOverrideProblem, effectiveCutoff, effectivePayDate],
  );

  const steps: FlowStep[] = [
    {
      id: "period",
      title: "Choose the period and pay group",
      purpose: "Which employer, which group of employees, and the dates that bound the run.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <div>
            <Label htmlFor="entity">Legal entity</Label>
            <Select
              value={USE_REAL && setup.data?.tree ? `entity:${entityEntityId}` : entityId}
              onValueChange={(v) =>
                v.startsWith("entity:") ? setEntityId(v.slice(7)) : setEntityId(v)
              }
            >
              <SelectTrigger id="entity" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {placementOptions.map((o) => (
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
            <p className="mt-1 text-xs text-muted-foreground">
              The entity is the employer of record, so it decides the currency ({entity.currency}) and
              which statutory rules apply. Branches and departments sit under their entity in the list below.
            </p>
          </div>

          <div>
            <Label htmlFor="group">Pay group</Label>
            <Select value={USE_REAL && setup.data?.periods.length ? setup.data.groupId : payGroup} onValueChange={setPayGroup}>
              <SelectTrigger id="group" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {(USE_REAL && setup.data?.periods.length
                  ? [{ id: setup.data.groupId, name: "Monthly ZMW" }]
                  : PAY_GROUPS.map((g) => ({ id: g, name: g }))
                ).map((g) => (
                  <SelectItem key={g.id} value={g.id}>
                    {g.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div>
              <Label htmlFor="period">Period</Label>
              {USE_REAL && setup.data?.periods.length ? (
                <Select value={period} onValueChange={setPeriod}>
                  <SelectTrigger id="period" className="mt-1"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {setup.data.periods.map((p) => (
                      <SelectItem key={p.id} value={p.periodLabel}>
                        {p.periodLabel} ({p.status})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <Input id="period" type="month" className="mt-1" value={period} onChange={(e) => setPeriod(e.target.value)} />
              )}
            </div>
            <div>
              <Label htmlFor="cutoff">Time cutoff — last day to approve attendance</Label>
              <Input id="cutoff" type="date" className="mt-1" value={effectiveCutoff} min={periodStart} max={periodEnd} onChange={(e) => setCutoff(e.target.value)} aria-describedby="cutoff-help" />
              <p id="cutoff-help" className="mt-1 text-xs text-muted-foreground">Attendance approved on or before this date can be considered for this run. Later approvals move to the next run.</p>
            </div>
            <div>
              <Label htmlFor="paydate">Pay date — planned salary payment day</Label>
              <Input id="paydate" type="date" className="mt-1" value={effectivePayDate} onChange={(e) => setPayDate(e.target.value)} aria-describedby="paydate-help" />
              <p id="paydate-help" className="mt-1 text-xs text-muted-foreground">This is the planned day employees should receive salary. Selecting it does not send money.</p>
            </div>
          </div>

          {dateProblem ? (
            <p role="alert" className="flex gap-2 rounded-md border border-danger/40 bg-danger-soft p-3 text-xs text-danger">
              <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              {dateProblem}
            </p>
          ) : (
            <p className="flex gap-2 text-xs text-muted-foreground">
              <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              In simple terms: the time cutoff is the last day managers can approve attendance for this payroll. Approved after {effectiveCutoff} goes into the next run.
            </p>
          )}
        </div>
      ),
    },
    {
      id: "population",
      title: "Confirm who is in the run",
      purpose: "Nobody is silently left out — every exclusion carries a reason.",
      render: () => (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            {population.length} employees found in the live directory; {included.length} eligible for this pay period and {effectiveExcluded.length} excluded with a reason.
          </p>
          <div className="rounded-lg border bg-surface">
            <div className="border-b bg-surface-muted p-3">
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-6">
                <label className="relative block xl:col-span-2">
                  <span className="sr-only">Search employees</span>
                  <Search className="pointer-events-none absolute left-2.5 top-2.5 size-4 text-muted-foreground" aria-hidden />
                  <Input value={populationSearch} onChange={(event) => setPopulationSearch(event.target.value)} placeholder="Search name, employee no., branch…" className="pl-8" />
                </label>
                <select aria-label="Filter by pay group" value={populationPayGroup} onChange={(event) => setPopulationPayGroup(event.target.value)} className="h-9 rounded-md border border-input bg-background px-3 text-sm">
                  <option value="all">All pay groups</option>
                  {populationOptions.payGroups.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
                <select aria-label="Filter by branch" value={populationBranch} onChange={(event) => setPopulationBranch(event.target.value)} className="h-9 rounded-md border border-input bg-background px-3 text-sm">
                  <option value="all">All branches</option>
                  {populationOptions.branches.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
                <select aria-label="Filter by department" value={populationDepartment} onChange={(event) => setPopulationDepartment(event.target.value)} className="h-9 rounded-md border border-input bg-background px-3 text-sm">
                  <option value="all">All departments</option>
                  {populationOptions.departments.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
                <select aria-label="Filter by eligibility" value={populationEligibility} onChange={(event) => setPopulationEligibility(event.target.value)} className="h-9 rounded-md border border-input bg-background px-3 text-sm">
                  <option value="all">All eligibility</option>
                  <option value="eligible">Eligible for this run</option>
                  <option value="excluded">Excluded with reason</option>
                </select>
              </div>
              <div className="mt-3 flex flex-wrap items-center gap-3">
                <select aria-label="Filter by employee status" value={populationStatus} onChange={(event) => setPopulationStatus(event.target.value)} className="h-9 rounded-md border border-input bg-background px-3 text-sm">
                  <option value="all">All employee statuses</option>
                  {populationOptions.statuses.map((option) => <option key={option} value={option}>{option}</option>)}
                </select>
                <span className="text-xs text-muted-foreground">Showing {filteredPopulation.length} of {population.length} employees. Eligibility is calculated from live payroll profiles and employment dates.</span>
                <Button type="button" variant="ghost" size="sm" onClick={() => { setPopulationSearch(""); setPopulationPayGroup("all"); setPopulationBranch("all"); setPopulationDepartment("all"); setPopulationStatus("all"); setPopulationEligibility("all"); }}>Clear filters</Button>
              </div>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[980px] text-left text-sm">
                <caption className="sr-only">Live payroll population</caption>
                <thead className="border-b bg-background text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2">Employee</th>
                    <th className="px-3 py-2">Pay group</th>
                    <th className="px-3 py-2">Branch</th>
                    <th className="px-3 py-2">Department</th>
                    <th className="px-3 py-2">Status</th>
                    <th className="px-3 py-2">Employment dates</th>
                    <th className="px-3 py-2">Eligibility</th>
                    <th className="px-3 py-2">Reason</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {filteredPopulation.length ? filteredPopulation.map((row) => (
                    <tr key={row.id} className="align-top hover:bg-muted/30">
                      <td className="px-3 py-3"><span className="block font-medium">{row.name}</span><span className="block text-xs text-muted-foreground">{row.employeeNo}</span></td>
                      <td className="px-3 py-3">{row.payGroup}</td>
                      <td className="px-3 py-3">{row.branch}</td>
                      <td className="px-3 py-3">{row.department}</td>
                      <td className="px-3 py-3 capitalize">{row.status.replace("-", " ")}</td>
                      <td className="px-3 py-3 tabular text-xs">{row.startDate} — {row.endDate}</td>
                      <td className="px-3 py-3"><span className={row.in ? "font-medium text-success" : "font-medium text-warning"}>{row.in ? "Included" : "Excluded"}</span></td>
                      <td className="max-w-xs px-3 py-3 text-xs text-muted-foreground">{row.note}</td>
                    </tr>
                  )) : <tr><td colSpan={8} className="px-3 py-8 text-center text-sm text-muted-foreground">No live employees match these filters. Clear the filters or add the missing employee/payroll setup records.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      ),
    },
    {
      id: "readiness",
      title: "Check readiness",
      purpose: "Anything here that is wrong would make the calculation wrong.",
      render: () => (
        <div className="space-y-3">
          <ul className="space-y-2">
            {readiness.map((r) => (
              <ReadinessRow key={r.id} item={r} />
            ))}
          </ul>
          <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
            <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Warnings are calculated from live configuration and persisted records. Resolve them or record
            an explicit approver decision before relying on the calculation.
          </p>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and open the run",
      purpose: "What you are about to create, before it exists.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <dl className="grid gap-3 sm:grid-cols-2">
            {[
              ["Entity", entity.name],
              ["Pay group", payGroup],
              ["Period", period],
              ["Pay date", effectivePayDate],
              ["Employees included", String(included.length)],
              ["Deliberately excluded", String(effectiveExcluded.length)],
            ].map(([k, v]) => (
              <div key={k}>
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>

          <div className="rounded-md border bg-surface-muted p-3">
            <p className="text-sm">
              Indicative gross, based on the last period:{" "}
              <span className="tabular font-medium">{money(estimate, entity.currency)}</span>
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              An estimate to sense-check the population, not a calculation. The real figures come out
              of the calculate stage.
            </p>
          </div>

          <div>
            <Label htmlFor="note">
              Note for the approver
              <span className="ml-1 text-xs font-normal text-muted-foreground">(optional)</span>
            </Label>
            <Textarea
              id="note"
              className="mt-1"
              rows={3}
              value={note}
              placeholder="Anything unusual about this period — a backdated increase, a one-off payment, a late starter."
              onChange={(e) => setNote(e.target.value)}
            />
          </div>

          <p className="flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Opening the run does not calculate anything and pays nobody. It creates the period so
            work can start against it.
          </p>
        </div>
      ),
    },
  ];

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Start a pay run"
        description="Open a pay period, confirm who is in it, and check that the inputs are trustworthy before anything is calculated."
        meta={
          <Button variant="outline" size="sm" asChild>
            <Link to="/hrm/payroll/runs">Back to pay runs</Link>
          </Button>
        }
      />

      <GuidedFlow
        flowId="payroll-run-new"
        steps={steps}
        submitLabel="Open the run"
        onSubmit={async () => {
            if (dateProblem) {
              feedback.blocked("Cannot open this run", dateProblem);
              return;
            }
          if (USE_REAL) {
            if (!setup.data?.periods.length || !chosenPeriod) {
              feedback.blocked(
                "No open period available",
                "Ask an admin to open a pay period for this pay group first.",
              );
              return;
            }
            try {
              const r = await realApi.createPayrollRun({ payPeriodId: chosenPeriod.id, payGroupId: setup.data.groupId });
              setRef(String((r as { id?: string }).id ?? chosenPeriod.id));
              feedback.submitted(
                "Run opened against the selected period.",
                "Next: calculate gross to net. Nothing has been paid.",
              );
            } catch (e) {
              feedback.blocked("Could not open the run", e instanceof Error ? e.message : "Unknown error.");
            }
            return;
          }
          const created = `RUN-${period.replace("-", "-")}-${entity.id.replace("ent-", "").toUpperCase()}-M`;
          setRef(created);
          feedback.submitted(
            `Run opened for ${included.length} employees.`,
            "Next: calculate gross to net. Nothing has been paid.",
          );
        }}
        submitted={
          ref ? (
            <NextSteps
              reference={ref}
              title="Pay run opened"
              steps={[
                "Calculate gross to net for the included employees. The calculation is resumable and shows its working.",
                "Review variances and exceptions — anything moving 2% or more since last period needs an explanation.",
                "Send for approval. Because you opened this run, someone else has to approve it.",
              ]}
              actions={
                <>
                  <Button onClick={() => navigate({ to: "/hrm/payroll/runs" })}>View pay runs</Button>
                  <Button variant="outline" asChild>
                    <Link to="/hrm/payroll">Back to Payroll</Link>
                  </Button>
                </>
              }
            />
          ) : undefined
        }
      />
    </AppShell>
      </AuthGate>
  );
}
