import { FormToggle } from "@/components/ui/form-toggle";
import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, redirect } from "@tanstack/react-router";
import { BadgeDollarSign, CalendarClock, Info, Pencil, Plus, ShieldCheck, Unplug } from "lucide-react";
import { useEffect, useState } from "react";
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
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { feedback } from "@/platform/feedback";

const description =
  "Pay groups, ZRA PAYE tax bands, NAPSA and NHIMA contribution rules, and the standard salary components a run can post to. Only HR operations and HR administration can change these values; everyone else reads them.";

export const Route = createFileRoute("/hrm/configuration/payroll")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/$slug", params: { slug: "payroll" }, replace: true }); },
  head: () => ({
    meta: [
      { title: "Payroll setup — Newworldcargo HRM" },
      { name: "description", content: description },
      { property: "og:title", content: "Payroll setup — Newworldcargo HRM" },
      { property: "og:description", content: description },
    ],
  }),
  component: PayrollSetup,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

type Raw = Record<string, unknown>;
const s = (v: unknown) => (v === undefined || v === null ? "" : String(v));
const n = (v: unknown) => {
  const x = Number(v);
  return Number.isFinite(x) ? x : NaN;
};

/* ------------------------------------------------------------------ */

const sections = [
  { id: "groups", label: "Pay groups" },
  { id: "structures", label: "Structures" },
  { id: "slabs", label: "ZRA PAYE slabs" },
  { id: "rules", label: "Contribution rules" },
  { id: "components", label: "Salary components" },
] as const;
type SectionId = (typeof sections)[number]["id"];

/* ---------- loading helpers ---------- */

async function loadPayrollSetup() {
  if (!USE_REAL) return { groups: [], structures: [], slabs: [], rules: [], components: [] };
  const taxYear = String(new Date().getFullYear());
  const [groups, structures, slabs, rules, components] = await Promise.all([
    realApi.payGroupsFull(),
    realApi.payrollStructures(),
    realApi.payrollTaxSlabs(taxYear),
    realApi.payrollContributionRules(),
    realApi.payrollComponents(),
  ]);
  return {
    groups: Array.isArray(groups) ? (groups as Raw[]) : [],
    structures: Array.isArray(structures) ? (structures as Raw[]) : [],
    slabs: Array.isArray(slabs) ? (slabs as Raw[]) : [],
    rules: Array.isArray(rules) ? (rules as Raw[]) : [],
    components: Array.isArray(components) ? (components as Raw[]) : [],
  };
}

/* ---------- edit dialogs ---------- */

function PayGroupDialog({
  group,
  taxYear,
  open,
  onOpenChange,
  onSaved,
}: {
  group: Raw | null;
  taxYear: string;
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [frequency, setFrequency] = useState("monthly");
  const [currency, setCurrency] = useState("ZMW");
  const [dayOfMonth, setDayOfMonth] = useState("28");
  const [cutoff, setCutoff] = useState("3");
  const [isDefault, setIsDefault] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Re-seed when the dialog target changes.
  if (group && String(group.id ?? "") !== name) {
    // (no-op branch kept readable; state sync happens in onOpenChange handlers below)
  }
  if (!open && group) {
    setName(String(group.name ?? ""));
    setFrequency(String(group.frequency ?? "monthly"));
    setCurrency(String(group.currency ?? "ZMW"));
    setDayOfMonth(String(group.calendarDayOfMonth ?? 28));
    setCutoff(String(group.inputCutoffDaysBeforePayday ?? 3));
    setIsDefault(Boolean(group.isDefault));
  }

  if (!group) return null;
  const id = String(group.id ?? "");

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        if (o) {
          setName(String(group.name ?? ""));
          setFrequency(String(group.frequency ?? "monthly"));
          setCurrency(String(group.currency ?? "ZMW"));
          setDayOfMonth(String(group.calendarDayOfMonth ?? 28));
          setCutoff(String(group.inputCutoffDaysBeforePayday ?? 3));
          setIsDefault(Boolean(group.isDefault));
        }
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Edit pay group</DialogTitle>
          <DialogDescription>
            The calendar a pay run follows. Changing the payday moves every future run for this
            group — employees already on an open period keep theirs until it closes.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            setBusy(true);
            try {
              await realApi.updatePayGroup(id, {
                name: name.trim() || undefined,
                frequency,
                currency,
                calendarDayOfMonth: Math.min(28, Math.max(1, Number(dayOfMonth) || 1)),
                inputCutoffDaysBeforePayday: Math.max(0, Number(cutoff) || 0),
                isDefault,
              });
              feedback.saved(`${name.trim() || String(group.code ?? "Pay group")} saved.`);
              onSaved();
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
            }
          }}
        >
          <div className="min-w-0">
            <Label htmlFor="pg-name">Group name</Label>
            <Input id="pg-name" value={name} onChange={(e) => setName(e.target.value)} className="mt-1.5" required />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="min-w-0">
              <Label htmlFor="pg-freq">Frequency</Label>
              <Select value={frequency} onValueChange={setFrequency}>
                <SelectTrigger id="pg-freq" className="mt-1.5 w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="monthly">Monthly</SelectItem>
                  <SelectItem value="biweekly">Bi-weekly</SelectItem>
                  <SelectItem value="weekly">Weekly</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-0">
              <Label htmlFor="pg-curr">Currency</Label>
              <Select value={currency} onValueChange={setCurrency}>
                <SelectTrigger id="pg-curr" className="mt-1.5 w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ZMW">ZMW — Zambian kwacha</SelectItem>
                  <SelectItem value="USD">USD</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-0">
              <Label htmlFor="pg-day">Payday — day of month</Label>
              <Input
                id="pg-day"
                type="number"
                min={1}
                max={28}
                value={dayOfMonth}
                onChange={(e) => setDayOfMonth(e.target.value)}
                className="mt-1.5"
                aria-describedby="pg-day-hint"
              />
              <p id="pg-day-hint" className="mt-1 text-xs text-muted-foreground">
                Capped at 28 so every month carries this payday, February included.
              </p>
            </div>
            <div className="min-w-0">
              <Label htmlFor="pg-cutoff">Input cutoff (days before payday)</Label>
              <Input
                id="pg-cutoff"
                type="number"
                min={0}
                max={31}
                value={cutoff}
                onChange={(e) => setCutoff(e.target.value)}
                className="mt-1.5"
              />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <Switch checked={isDefault} onCheckedChange={setIsDefault} aria-label="Default pay group" />
            <span>Default pay group for new employees</span>
          </label>
          {error ? (
            <p className="rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
              {error}
            </p>
          ) : null}
          <div className="flex items-center justify-between gap-2 pt-2">
            <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
              {String(group.code ?? "")} · in force for {taxYear}
            </span>
            <div className="flex gap-2">
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy ? "Saving…" : "Save pay group"}
              </Button>
            </div>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function TaxSlabDialog({
  slab,
  open,
  onOpenChange,
  onSaved,
}: {
  slab: Raw | null;
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSaved: () => void;
}) {
  const [rate, setRate] = useState("0");
  const [maxAmount, setMaxAmount] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (!slab) return null;
  const id = String(slab.id ?? "");
  const minAmount = n(slab.minAmount);
  const rateValid = Number(rate) >= 0 && Number(rate) <= 100;

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        if (o) {
          setRate(String(slab.rate ?? "0"));
          setMaxAmount(slab.maxAmount === undefined || slab.maxAmount === null ? "" : String(slab.maxAmount));
        }
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Edit PAYE band</DialogTitle>
          <DialogDescription>
            The ZRA PAYE bracket from {Number.isFinite(minAmount) ? minAmount.toLocaleString("en-GB") : "—"} kwacha
            per month. The rate must stay inside the tax-year scale; a wrong band here changes what
            every open and future run calculates.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            setBusy(true);
            try {
              await realApi.updateTaxSlab(id, {
                rate: Number(rate),
                maxAmount: maxAmount.trim() === "" ? null : Number(maxAmount),
              });
              feedback.saved(
                `Band from ${Number.isFinite(minAmount) ? minAmount.toLocaleString("en-GB") : "—"} set to ${rate}%.`,
              );
              onSaved();
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
            }
          }}
        >
          <div className="min-w-0">
            <Label htmlFor="slab-rate">Rate (%)</Label>
            <Input
              id="slab-rate"
              type="number"
              min={0}
              max={100}
              step="0.01"
              value={rate}
              onChange={(e) => setRate(e.target.value)}
              className="mt-1.5"
              required
              aria-invalid={rateValid ? undefined : true}
            />
            {!rateValid ? (
              <p className="mt-1 text-xs text-warning">Rate must sit between 0 and 100 percent.</p>
            ) : null}
          </div>
          <div className="min-w-0">
            <Label htmlFor="slab-max">Top of band (K/month), blank = up to anything</Label>
            <Input
              id="slab-max"
              type="number"
              min={0}
              step="0.01"
              value={maxAmount}
              onChange={(e) => setMaxAmount(e.target.value)}
              className="mt-1.5"
              aria-describedby="slab-max-hint"
            />
            <p id="slab-max-hint" className="mt-1 text-xs text-muted-foreground">
              {slab.maxAmount === undefined || slab.maxAmount === null
                ? "This is the top band — nothing earns above it, so leave it empty."
                : "Payable earnings inside this range are taxed at the band rate, anything above falls into the next band."}
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
            <Button type="submit" disabled={busy || !rateValid}>
              {busy ? "Saving…" : "Save band"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ContributionRuleDialog({
  rule,
  open,
  onOpenChange,
  onSaved,
}: {
  rule: Raw | null;
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSaved: () => void;
}) {
  const [rate, setRate] = useState("5");
  const [ceiling, setCeiling] = useState("");
  const [floor, setFloor] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (!rule) return null;
  const id = String(rule.id ?? "");
  const rateValid = Number(rate) >= 0 && Number(rate) <= 100;

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        if (o) {
          setRate(String(rule.rate ?? ""));
          setCeiling(rule.ceiling === undefined || rule.ceiling === null ? "" : String(rule.ceiling));
          setFloor(rule.floor === undefined || rule.floor === null ? "" : String(rule.floor));
        }
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Edit {String(rule.name ?? rule.code ?? "contribution rule")}</DialogTitle>
          <DialogDescription>
            The {String(rule.payer ?? "statutory")} share of {String(rule.code ?? "")}, computed on
            the tied component each run posts to. The ceiling is what the pension authority caps
            contributions on — when the minimum wage moves, this usually moves with it.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            setBusy(true);
            try {
              await realApi.updateContributionRule(id, {
                rate: Number(rate),
                ceiling: ceiling.trim() === "" ? null : Number(ceiling),
                floor: floor.trim() === "" ? null : Number(floor),
              });
              feedback.saved(`${String(rule.name ?? rule.code ?? "")} saved.`);
              onSaved();
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
            }
          }}
        >
          <div className="min-w-0">
            <Label htmlFor="rule-rate">Rate (%)</Label>
            <Input
              id="rule-rate"
              type="number"
              min={0}
              max={100}
              step="0.01"
              value={rate}
              onChange={(e) => setRate(e.target.value)}
              className="mt-1.5"
              required
              aria-invalid={rateValid ? undefined : true}
            />
            {!rateValid ? (
              <p className="mt-1 text-xs text-warning">Rate must sit between 0 and 100 percent.</p>
            ) : null}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="min-w-0">
              <Label htmlFor="rule-ceiling">Ceiling (K/month)</Label>
              <Input
                id="rule-ceiling"
                type="number"
                min={0}
                step="0.01"
                value={ceiling}
                onChange={(e) => setCeiling(e.target.value)}
                className="mt-1.5"
                aria-describedby="rule-ceiling-hint"
              />
              <p id="rule-ceiling-hint" className="mt-1 text-xs text-muted-foreground">
                Contributions stop growing above this basic-pay level.
              </p>
            </div>
            <div className="min-w-0">
              <Label htmlFor="rule-floor">Floor (K/month)</Label>
              <Input
                id="rule-floor"
                type="number"
                min={0}
                step="0.01"
                value={floor}
                onChange={(e) => setFloor(e.target.value)}
                className="mt-1.5"
              />
            </div>
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
            <Button type="submit" disabled={busy || !rateValid}>
              {busy ? "Saving…" : "Save rule"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ComponentDialog({
  comp,
  components,
  open,
  onOpenChange,
  onSaved,
}: {
  comp: Raw | null;
  components: Raw[];
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [componentType, setComponentType] = useState("earning");
  const [calculationBasis, setCalculationBasis] = useState("fixed");
  const [basisComponentCode, setBasisComponentCode] = useState("");
  const [rate, setRate] = useState("");
  const [fixedAmount, setFixedAmount] = useState("");
  const [ceiling, setCeiling] = useState("");
  const [isTaxable, setIsTaxable] = useState(false);
  const [isArchived, setIsArchived] = useState(false);
  const [priority, setPriority] = useState("20");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The dialog is controlled by the parent. Opening it by selecting a row does
  // not call Radix's onOpenChange(true), so initialise from the selected record
  // here rather than relying on that callback. It also clears a prior edit when
  // the user switches straight to adding a new component.
  useEffect(() => {
    if (!open || !comp) return;
    setCode(String(comp.code ?? ""));
    setName(String(comp.name ?? comp.code ?? ""));
    setComponentType(String(comp.componentType ?? "earning"));
    setCalculationBasis(String(comp.calculationBasis ?? "fixed"));
    setBasisComponentCode(String(comp.basisComponentCode ?? ""));
    setRate(comp.rate === undefined || comp.rate === null ? "" : String(comp.rate));
    setFixedAmount(
      comp.fixedAmount === undefined || comp.fixedAmount === null ? "" : String(comp.fixedAmount),
    );
    setCeiling(comp.ceiling === undefined || comp.ceiling === null ? "" : String(comp.ceiling));
    setIsTaxable(Boolean(comp.isTaxable));
    setIsArchived(!Boolean(comp.isActive));
    setPriority(String(comp.priority ?? 20));
    setError(null);
  }, [comp, open]);

  if (!comp) return null;
  const id = String(comp.id ?? "");
  const creating = id === "";
  const isStatutory = Boolean(comp.isStatutory);

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
      }}
    >
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{creating ? "Add salary component" : "Edit salary component"}</DialogTitle>
          <DialogDescription>
            {creating
              ? "Create a reusable earning or deduction. Percentage components calculate automatically from Basic Salary or Gross Pay."
              : isStatutory
              ? "This component is statutory. You can edit its component setup, but PAYE slabs and NAPSA/NHIMA contribution rules still control the legal calculation amounts."
              : "A standard component a run can post to. Archiving keeps every past run correct but hides it from new ones."}
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            if (!name.trim()) {
              setError("Component name is required.");
              return;
            }
            if (creating && !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(code.trim())) {
              setError("Code must use lowercase letters, numbers and single hyphens, for example housing-allowance.");
              return;
            }
            if (calculationBasis === "percent-of" && (!basisComponentCode || !rate || Number(rate) <= 0)) {
              setError("Select a basis component and enter a percentage greater than zero.");
              return;
            }
            setBusy(true);
            try {
              const body = {
                ...(creating ? { code: code.trim(), componentType, priority: Number(priority) } : {}),
                name: name.trim(),
                calculationBasis,
                basisComponentCode: calculationBasis === "percent-of" ? basisComponentCode : undefined,
                rate: rate.trim() === "" ? null : Number(rate),
                fixedAmount: fixedAmount.trim() === "" ? null : Number(fixedAmount),
                ceiling: ceiling.trim() === "" ? null : Number(ceiling),
                isTaxable,
                ...(!creating ? { isArchived } : {}),
              };
              if (creating) await realApi.createSalaryComponent(body);
              else await realApi.updateSalaryComponent(id, body);
              feedback.saved(
                creating
                  ? `${name.trim()} created.`
                  : isArchived
                  ? `${String(comp.name ?? comp.code ?? "Component")} archived for new runs.`
                  : `${String(comp.name ?? comp.code ?? "Component")} saved.`,
              );
              onSaved();
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
            }
          }}
        >
          {creating ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="min-w-0">
                <Label htmlFor="comp-code">Code</Label>
                <Input
                  id="comp-code"
                  value={code}
                  onChange={(e) => setCode(e.target.value.toLowerCase().replace(/\s+/g, "-"))}
                  className="mt-1.5"
                  placeholder="housing-allowance"
                  required
                />
              </div>
              <div className="min-w-0">
                <Label htmlFor="comp-type">Type</Label>
                <Select value={componentType} onValueChange={setComponentType}>
                  <SelectTrigger id="comp-type" className="mt-1.5"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="earning">Earning</SelectItem>
                    <SelectItem value="deduction">Deduction</SelectItem>
                    <SelectItem value="employer-contribution">Employer contribution</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
          ) : null}
          <div className="min-w-0">
            <Label htmlFor="comp-name">Name</Label>
            <Input
              id="comp-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="mt-1.5"
              required
            />
            {!creating ? <p className="mt-1 text-xs text-muted-foreground">Code: <span className="font-mono">{String(comp.code ?? "")}</span></p> : null}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="min-w-0">
              <Label htmlFor="comp-basis">Calculation basis</Label>
              <Select value={calculationBasis} onValueChange={setCalculationBasis}>
                <SelectTrigger id="comp-basis" className="mt-1.5">
                  <SelectValue placeholder="Select basis" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="fixed">Fixed amount</SelectItem>
                  <SelectItem value="percent-of">Percent of component</SelectItem>
                  {!creating || isStatutory ? <SelectItem value="slab">Tax slab</SelectItem> : null}
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-0">
              <Label htmlFor="comp-basis-code">Basis component</Label>
              <Select value={basisComponentCode || "none"} onValueChange={(v) => setBasisComponentCode(v === "none" ? "" : v)} disabled={calculationBasis !== "percent-of"}>
                <SelectTrigger id="comp-basis-code" className="mt-1.5"><SelectValue placeholder="Select component" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Select component</SelectItem>
                  {components.filter((item) => Boolean(item.isActive) && String(item.code) !== code).map((item) => (
                    <SelectItem key={String(item.id)} value={String(item.code)}>{String(item.name ?? item.code)}</SelectItem>
                  ))}
                  <SelectItem value="gross">Gross pay</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            {calculationBasis === "percent-of" ? <div className="min-w-0">
              <Label htmlFor="comp-rate">Rate (%)</Label>
              <Input
                id="comp-rate"
                type="number"
                min={0}
                step="0.01"
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                className="mt-1.5"
                aria-describedby="comp-rate-hint"
              />
              <p id="comp-rate-hint" className="mt-1 text-xs text-muted-foreground">
                For percent-of calculations.
              </p>
            </div> : null}
            {calculationBasis === "fixed" ? <div className="min-w-0">
              <Label htmlFor="comp-fixed">Fixed amount (K)</Label>
              <Input
                id="comp-fixed"
                type="number"
                min={0}
                step="0.01"
                value={fixedAmount}
                onChange={(e) => setFixedAmount(e.target.value)}
                className="mt-1.5"
              />
            </div> : null}
            <div className="min-w-0">
              <Label htmlFor="comp-ceiling">Ceiling (K)</Label>
              <Input
                id="comp-ceiling"
                type="number"
                min={0}
                step="0.01"
                value={ceiling}
                onChange={(e) => setCeiling(e.target.value)}
                className="mt-1.5"
              />
            </div>
          </div>
          {creating ? (
            <div className="min-w-0">
              <Label htmlFor="comp-priority">Evaluation order</Label>
              <Input id="comp-priority" type="number" min={1} max={1000} value={priority} onChange={(e) => setPriority(e.target.value)} className="mt-1.5" required />
              <p className="mt-1 text-xs text-muted-foreground">Use 20 for Housing Allowance so Basic Salary (10) is calculated first.</p>
            </div>
          ) : null}
          <label className="flex items-center gap-2 text-sm">
            <Switch checked={isTaxable} onCheckedChange={setIsTaxable} aria-label="Taxable component" />
            <span>Taxable — PAYE applies to this component</span>
          </label>
          {!creating ? <label className="flex items-center gap-2 text-sm">
            <Switch checked={isArchived} onCheckedChange={setIsArchived} aria-label="Archive component" />
            <span>Archive — keep history, hide from new runs</span>
          </label> : null}
          {isStatutory ? (
            <p className="flex items-start gap-2 rounded-md border border-info/30 bg-info-soft px-3 py-2 text-xs text-info">
              <ShieldCheck className="mt-0.5 size-3.5 shrink-0" aria-hidden />
              For statutory components, contribution rules and tax slabs override the legal amount
              calculation during payroll. Keep those rules aligned after changing component setup.
            </p>
          ) : null}
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
              {busy ? "Saving…" : creating ? "Add component" : "Save component"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function StructureDialog({
  structure,
  components,
  open,
  onOpenChange,
  onSaved,
}: {
  structure: Raw | null;
  components: Raw[];
  open: boolean;
  onOpenChange: (o: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [items, setItems] = useState<Raw[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  if (!structure) return null;
  const id = String(structure.id ?? "");
  const isDefault = String(structure.code ?? "").toUpperCase() === "ZMW-STANDARD";
  const activeComponents = components.filter((c) => Boolean(c.isActive) && !c.isArchived);

  const upsertItem = (componentId: string) => {
    setItems((prev) => {
      if (prev.find((i) => String(i.componentId) === componentId)) return prev;
      return [
        ...prev,
        { componentId, defaultAmount: "0", isOptional: false },
      ];
    });
  };
  const removeItem = (componentId: string) =>
    setItems((prev) => prev.filter((i) => String(i.componentId) !== componentId));
  const setItemAmount = (componentId: string, value: string) =>
    setItems((prev) =>
      prev.map((i) => (String(i.componentId) === componentId ? { ...i, defaultAmount: value } : i)),
    );
  const setItemOptional = (componentId: string, value: boolean) =>
    setItems((prev) =>
      prev.map((i) => (String(i.componentId) === componentId ? { ...i, isOptional: value } : i)),
    );

  return (
    <Dialog
      open={open}
      onOpenChange={(o) => {
        onOpenChange(o);
        if (o) {
          setName(String(structure.name ?? ""));
          setItems(((structure.items as Raw[] | undefined) ?? []).map((i) => ({
            componentId: String(i.componentId ?? ""),
            defaultAmount: String(i.defaultAmount ?? "0"),
            isOptional: Boolean(i.isOptional),
          })));
        }
      }}
    >
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{isDefault ? "ZMW-STANDARD — mandatory structure" : "Edit structure"}</DialogTitle>
          <DialogDescription>
            A structure decides which components an employee carries and their starting amounts. A run
            posts to every component on the employee's structure; components are the ones from the
            Salary components screen, minus the archived ones.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={async (e) => {
            e.preventDefault();
            setError(null);
            if (!name.trim()) {
              setError("Give the structure a name.");
              return;
            }
            const payload = items.map((i) => ({
              componentId: String(i.componentId),
              defaultAmount: Number(i.defaultAmount) || 0,
              isOptional: Boolean(i.isOptional),
            }));
            if (payload.length !== new Set(items.map((i) => String(i.componentId))).size) {
              setError("The same component cannot appear twice.");
              return;
            }
            setBusy(true);
            try {
              await realApi.updateStructure(id, { name: name.trim(), items: payload });
              feedback.saved(`${name.trim()} saved with ${payload.length} component${payload.length === 1 ? "" : "s"}.`);
              onSaved();
              onOpenChange(false);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Server rejected the change.");
            } finally {
              setBusy(false);
            }
          }}
        >
          <div className="min-w-0">
            <Label htmlFor="st-name">Structure name</Label>
            <Input
              id="st-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="mt-1.5"
              required
              disabled={isDefault}
            />
          </div>
          <div className="space-y-2">
            <Label>Components on this structure</Label>
            <div className="max-h-64 space-y-2 overflow-y-auto rounded-md border p-3">
              {activeComponents.map((comp) => {
                const item = items.find((i) => String(i.componentId) === String(comp.id));
                return (
                  <div key={String(comp.id)} className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={!!item}
                      onChange={(e) => (e.target.checked ? upsertItem(String(comp.id)) : removeItem(String(comp.id)))}
                      aria-label={`Include ${String(comp.name ?? comp.code)}`}
                      className="size-4"
                    />
                    <span className="min-w-0 flex-1 truncate text-sm">{String(comp.name ?? comp.code)}</span>
                    {item ? (
                      <>
                        <Input
                          type="number"
                          min={0}
                          step="0.01"
                          value={String(item.defaultAmount)}
                          onChange={(e) => setItemAmount(String(comp.id), e.target.value)}
                          className="w-28"
                          aria-label={`Default amount for ${String(comp.name ?? comp.code)}`}
                        />
                        <label className="flex shrink-0 items-center gap-1.5 text-xs text-muted-foreground">
                          <FormToggle
                            type="checkbox"
                            checked={Boolean(item.isOptional)}
                            onChange={(e) => setItemOptional(String(comp.id), e.target.checked)}
                            aria-label={`Optional — ${String(comp.name ?? comp.code)}`}
                            className="size-3.5"
                          />
                          optional
                        </label>
                      </>
                    ) : null}
                  </div>
                );
              })}
              {!activeComponents.length ? (
                <p className="py-4 text-center text-xs text-muted-foreground">No active components available.</p>
              ) : null}
            </div>
            <p className="text-xs text-muted-foreground">
              Checked components are posted to the employee with their default amount; unchecking
              removes the component from this structure entirely.
            </p>
          </div>
          {error ? (
            <p className="rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
              {error}
            </p>
          ) : null}
          <div className="flex items-center justify-between gap-2 pt-2">
            <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
              {String(structure.code ?? "")}{isDefault ? " · this structure cannot be deactivated" : ""}
            </span>
            <div className="flex gap-2">
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy ? "Saving…" : "Save structure"}
              </Button>
            </div>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/* ---------- tables ---------- */

function StructureTable({
  rows,
  canAct,
  onEdit,
}: {
  rows: Raw[];
  canAct: boolean;
  onEdit: (g: Raw) => void;
}) {
  if (!rows.length) {
    return (
      <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
        No salary structures yet — HR picks components here that every employee assigned to the
        structure will carry into a run.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border bg-surface">
      <RecordTable className="w-full min-w-[44rem] text-left text-sm">
        <caption className="sr-only">Salary structures defining which components employees carry</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            {["Code", "Name", "Components", "Status", "Action"].map((h) => (
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
          {rows.map((st) => (
            <tr key={String(st.id)} className="hover:bg-surface-muted">
              <td className="px-3 py-3 font-mono text-xs text-muted-foreground">{String(st.code ?? "")}</td>
              <td className="px-3 py-3">{String(st.name ?? "")}</td>
              <td className="px-3 py-3">
                {Number(((st.items as unknown[]) ?? []).length)}{
                  (st.items as Raw[] | undefined)?.some((i) => Boolean(i.isOptional))
                    ? " · some optional"
                    : ""
                }
              </td>
              <td className="px-3 py-3">
                <StatusBadge status={Boolean(st.isActive) ? "active" : "inactive"} />
                <span className="ml-1.5 text-xs text-muted-foreground">{Boolean(st.isActive) ? "In use" : "Retired"}</span>
              </td>
              <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button variant="ghost" size="sm" className="h-8" onClick={() => onEdit(st)}>
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> structure {String(st.code ?? "")}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </RecordTable>
    </div>
  );
}

function PayGroupTable({
  rows,
  canAct,
  onEdit,
}: {
  rows: Raw[];
  canAct: boolean;
  onEdit: (g: Raw) => void;
}) {
  if (!rows.length) {
    return (
      <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
        No pay groups yet — the first one becomes the default for new employees.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border bg-surface">
      <RecordTable className="w-full min-w-[44rem] text-left text-sm">
        <caption className="sr-only">Pay groups the company runs payroll on</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            {["Group", "Frequency", "Currency", "Payday", "Input cutoff", "Default", "Status", "Action"].map((h) => (
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
          {rows.map((g) => (
            <tr key={String(g.id)} className="align-top hover:bg-surface-muted">
              <th scope="row" className="px-3 py-3 text-left font-medium">
                <span className="block">{String(g.name ?? g.code)}</span>
                <span className="block font-mono text-xs font-normal text-muted-foreground">
                  {String(g.code ?? "")}
                </span>
              </th>
              <td className="px-3 py-3">{String(g.frequency ?? "")}</td>
              <td className="px-3 py-3 font-mono">{String(g.currency ?? "")}</td>
              <td className="px-3 py-3">
                {n(g.calendarDayOfMonth)
                  ? `day ${Math.round(n(g.calendarDayOfMonth))} of each month`
                  : String(g.frequency ?? "") !== "monthly"
                    ? `every ${String(g.frequency ?? "")}`
                    : "—"}
              </td>
              <td className="px-3 py-3">{String(g.inputCutoffDaysBeforePayday ?? "—")} days</td>
              <td className="px-3 py-3">{Boolean(g.isDefault) ? "Yes" : "No"}</td>
              <td className="px-3 py-3">
                <StatusBadge status={String(g.status ?? (Boolean(g.isArchived) ? "archived" : "active"))} />
              </td>
              <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button variant="ghost" size="sm" className="h-8" onClick={() => onEdit(g)}>
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> {String(g.name ?? g.code)}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </RecordTable>
    </div>
  );
}

function SlabTable({
  rows,
  canAct,
  onEdit,
}: {
  rows: Raw[];
  canAct: boolean;
  onEdit: (g: Raw) => void;
}) {
  if (!rows.length) {
    return (
      <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
        No PAYE bands recorded for this tax year yet.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border bg-surface">
      <RecordTable className="w-full min-w-[40rem] text-left text-sm">
        <caption className="sr-only">ZRA PAYE bands for the current tax year</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            {["Band", "From (K/month)", "To (K/month)", "Rate", "Action"].map((h) => (
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
          {rows.map((slab) => (
            <tr key={String(slab.id)} className="hover:bg-surface-muted">
              <td className="px-3 py-3 font-mono text-xs text-muted-foreground">
                band {Number(slab.sequence)}
              </td>
              <td className="px-3 py-3 font-mono">{Number(slab.minAmount).toLocaleString("en-GB")}</td>
              <td className="px-3 py-3 font-mono">
                {slab.maxAmount === undefined || slab.maxAmount === null ? "—" : Number(slab.maxAmount).toLocaleString("en-GB")}
              </td>
              <td className="px-3 py-3 font-medium">{Number(slab.rate).toLocaleString("en-GB")}%</td>
              <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button variant="ghost" size="sm" className="h-8" onClick={() => onEdit(slab)}>
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> band from {Number(slab.minAmount).toLocaleString("en-GB")}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </RecordTable>
    </div>
  );
}

function RuleTable({
  rows,
  canAct,
  onEdit,
}: {
  rows: Raw[];
  canAct: boolean;
  onEdit: (g: Raw) => void;
}) {
  if (!rows.length) {
    return (
      <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
        No contribution rules recorded yet.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border bg-surface">
      <RecordTable className="w-full min-w-[44rem] text-left text-sm">
        <caption className="sr-only">Statutory contribution rules (NAPSA / NHIMA)</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            {["Rule", "Payer", "Tied component", "Rate", "Ceiling (K/month)", "Floor (K/month)", "Action"].map((h) => (
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
          {rows.map((r) => (
            <tr key={String(r.id)} className="hover:bg-surface-muted">
              <th scope="row" className="px-3 py-3 text-left font-medium">
                <span className="block">{String(r.name ?? r.code)}</span>
                <span className="block font-mono text-xs font-normal text-muted-foreground">
                  {String(r.code ?? "")}
                </span>
              </th>
              <td className="px-3 py-3">{String(r.payer ?? "")}</td>
              <td className="px-3 py-3 font-mono text-xs">{String(r.tiedComponentCode ?? "basic")}</td>
              <td className="px-3 py-3 font-medium">{Number(r.rate).toLocaleString("en-GB")}%</td>
              <td className="px-3 py-3 font-mono">
                {r.ceiling === undefined || r.ceiling === null ? "—" : Number(r.ceiling).toLocaleString("en-GB")}
              </td>
              <td className="px-3 py-3 font-mono">
                {r.floor === undefined || r.floor === null ? "—" : Number(r.floor).toLocaleString("en-GB")}
              </td>
              <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button variant="ghost" size="sm" className="h-8" onClick={() => onEdit(r)}>
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> {String(r.name ?? r.code)}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </RecordTable>
    </div>
  );
}

const typeLabels: Record<string, string> = {
  earning: "Earning",
  deduction: "Deduction",
  tax: "Tax",
  employer_contribution: "Employer contribution",
  allowance: "Allowance",
};

function ComponentTable({
  rows,
  canAct,
  onEdit,
}: {
  rows: Raw[];
  canAct: boolean;
  onEdit: (g: Raw) => void;
}) {
  if (!rows.length) {
    return (
      <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
        No salary components recorded yet.
      </p>
    );
  }
  return (
    <div className="overflow-x-auto rounded-lg border bg-surface">
      <RecordTable className="w-full min-w-[48rem] text-left text-sm">
        <caption className="sr-only">Standard salary components</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            {["Component", "Type", "Basis", "Rate / fixed", "Ceiling", "Taxable", "Status", "Action"].map((h) => (
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
          {rows.map((c) => (
            <tr key={String(c.id)} className="hover:bg-surface-muted">
              <th scope="row" className="px-3 py-3 text-left font-medium">
                <span className="block">
                  {String(c.name ?? c.code)}
                  {Boolean(c.isStatutory) ? (
                    <span className="ml-1.5 inline-flex items-center gap-1 rounded-full border border-primary/30 bg-primary-soft px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-primary">
                      <ShieldCheck className="size-3" aria-hidden /> statutory
                    </span>
                  ) : null}
                </span>
                <span className="block font-mono text-xs font-normal text-muted-foreground">
                  {String(c.code ?? "")}
                </span>
              </th>
              <td className="px-3 py-3">{typeLabels[String(c.componentType ?? "")] ?? String(c.componentType ?? "")}</td>
              <td className="px-3 py-3 text-xs">{String(c.calculationBasis ?? "—")}</td>
              <td className="px-3 py-3 font-mono">
                {c.rate === undefined || c.rate === null ? "—" : `${Number(c.rate).toLocaleString("en-GB")}%`}
                {c.rate !== undefined && c.rate !== null && c.fixedAmount !== undefined && c.fixedAmount !== null ? " / " : ""}
                {c.fixedAmount === undefined || c.fixedAmount === null ? "" : `K ${Number(c.fixedAmount).toLocaleString("en-GB")}`}
              </td>
              <td className="px-3 py-3 font-mono">
                {c.ceiling === undefined || c.ceiling === null ? "—" : `K ${Number(c.ceiling).toLocaleString("en-GB")}`}
              </td>
              <td className="px-3 py-3">{Boolean(c.isTaxable) ? "Yes" : "No"}</td>
              <td className="px-3 py-3">
                <StatusBadge status={Boolean(c.isActive) ? "active" : "archived"} />
              </td>
              <td className="px-3 py-3 text-right">
                {canAct ? (
                  <Button variant="ghost" size="sm" className="h-8" onClick={() => onEdit(c)}>
                    <Pencil className="size-3.5" aria-hidden />
                    Edit
                    <span className="sr-only"> {String(c.name ?? c.code)}</span>
                  </Button>
                ) : (
                  <span className="text-xs text-muted-foreground">Read-only</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </RecordTable>
    </div>
  );
}

/* ---------- page ---------- */

export function PayrollSetup() {
  const state = useApi(loadPayrollSetup);
  const userRoles = new Set(useAuth().user?.roles ?? []);
  const canAct = userRoles.has("hr_admin") || userRoles.has("hr_ops");
  const [section, setSection] = useState<SectionId>("groups");
  const [editingGroup, setEditingGroup] = useState<Raw | null>(null);
  const [editingStructure, setEditingStructure] = useState<Raw | null>(null);
  const [editingSlab, setEditingSlab] = useState<Raw | null>(null);
  const [editingRule, setEditingRule] = useState<Raw | null>(null);
  const [editingComp, setEditingComp] = useState<Raw | null>(null);
  const [addingComp, setAddingComp] = useState(false);
  const taxYear = String(new Date().getFullYear());

  return (
    <AppShell>
      <PageHeader
        eyebrow="Configuration"
        title="Payroll setup"
        description={description}
        meta={
          <span className="rounded-full border bg-surface-muted px-2.5 py-0.5 text-xs text-muted-foreground">
            {taxYear} tax year · Zambian payroll pack
          </span>
        }
      />

      {!USE_REAL ? (
        <p className="flex items-start gap-2 rounded-lg border border-warning/40 bg-warning-soft px-3 py-2 text-sm text-warning">
          <Unplug className="mt-0.5 size-4 shrink-0" aria-hidden />
          Live API is off for this build. The real surfaces exist on the backend — this page shows
          the intended layout without connecting.
        </p>
      ) : null}

      <div className="mt-4 flex flex-wrap gap-2" role="tablist" aria-label="Payroll setup sections">
        {sections.map((sec) => (
          <button
            key={sec.id}
            role="tab"
            aria-selected={section === sec.id}
            onClick={() => setSection(sec.id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              section === sec.id
                ? "border-primary bg-primary-soft font-medium text-primary"
                : "bg-surface text-muted-foreground hover:border-border-strong"
            }`}
          >
            {sec.label}
          </button>
        ))}
      </div>

      <div className="mt-6">
        <Async state={state} rows={5}>
          {(data) => (
            <>
              {section === "structures" ? (
                <section aria-label="Salary structures" className="space-y-4">
                  <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
                    <p className="flex items-start gap-2 font-medium">
                      <BadgeDollarSign className="mt-0.5 size-4 shrink-0" aria-hidden />
                      Which components an employee carries into a run
                    </p>
                    <p className="mt-1.5 pl-6">
                      Every employee is assigned a structure, and a run posts to every component on
                      it. The ZMW-STANDARD structure is the company-wide default and can never be
                      switched off; other structures can be retired once nobody is assigned to
                      them. Archived components never appear as candidates.
                    </p>
                  </div>
                  <StructureTable
                    rows={data.structures}
                    canAct={canAct}
                    onEdit={(st) => setEditingStructure(st)}
                  />
                </section>
              ) : null}

              {section === "groups" ? (
                <section aria-label="Pay groups" className="space-y-4">
                  <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
                    <p className="flex items-start gap-2 font-medium">
                      <CalendarClock className="mt-0.5 size-4 shrink-0" aria-hidden />
                      One default pay group drives the calendar for new employees
                    </p>
                    <p className="mt-1.5 pl-6">
                      A pay group decides how often people are paid, in which currency, and on what
                      day. Exactly one group stays flagged as the default; picking a new one clears
                      the old flag automatically.
                    </p>
                  </div>
                  <PayGroupTable rows={data.groups} canAct={canAct} onEdit={(g) => setEditingGroup(g)} />
                </section>
              ) : null}

              {section === "slabs" ? (
                <section aria-label="ZRA PAYE slabs" className="space-y-4">
                  <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
                    <p className="flex items-start gap-2 font-medium">
                      <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
                      The {taxYear} ZRA PAYE scale
                    </p>
                    <p className="mt-1.5 pl-6">
                      Monthly taxable pay inside a band is taxed at that band's rate; the top band
                      has no ceiling. When ZRA publishes a new Finance Act scale, the top-of-band
                      figures are what usually move.
                    </p>
                  </div>
                  <SlabTable rows={data.slabs} canAct={canAct} onEdit={(slab) => setEditingSlab(slab)} />
                </section>
              ) : null}

              {section === "rules" ? (
                <section aria-label="Contribution rules" className="space-y-4">
                  <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
                    <p className="flex items-start gap-2 font-medium">
                      <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
                      NAPSA and NHIMA deductions and employer shares
                    </p>
                    <p className="mt-1.5 pl-6">
                      Each rule names a rate tied to the basic-pay component. A ceiling is a maximum
                      monthly contribution; a floor is a minimum monthly contribution. For NHIMA
                      employee, use floor K50 and leave ceiling blank.
                    </p>
                  </div>
                  <RuleTable rows={data.rules} canAct={canAct} onEdit={(r) => setEditingRule(r)} />
                </section>
              ) : null}

              {section === "components" ? (
                <section aria-label="Salary components" className="space-y-4">
                  <div className="rounded-lg border border-info/30 bg-info-soft p-4 text-sm text-info">
                    <p className="flex items-start gap-2 font-medium">
                      <BadgeDollarSign className="mt-0.5 size-4 shrink-0" aria-hidden />
                      What a pay run can post to
                    </p>
                    <p className="mt-1.5 pl-6">
                      Earnings, deductions and taxes are all components. The statutory ones come
                      from ZRA and the pension and health authorities — their rates live on the
                      slab and rule screens, so they are read-only here. Archive a standard
                      component to keep every past run correct while hiding it from new ones.
                    </p>
                  </div>
                  {canAct ? (
                    <div className="flex justify-end">
                      <Button onClick={() => setAddingComp(true)}>
                        <Plus className="size-4" aria-hidden />
                        Add salary component
                      </Button>
                    </div>
                  ) : null}
                  <ComponentTable
                    rows={data.components}
                    canAct={canAct}
                    onEdit={(c) => setEditingComp(c)}
                  />
                </section>
              ) : null}
            </>
          )}
        </Async>
      </div>

      <PayGroupDialog
        group={editingGroup}
        taxYear={taxYear}
        open={editingGroup !== null}
        onOpenChange={(o) => !o && setEditingGroup(null)}
        onSaved={state.reload}
      />
      <StructureDialog
        structure={editingStructure}
        components={state.data ? state.data.components : []}
        open={editingStructure !== null}
        onOpenChange={(o) => !o && setEditingStructure(null)}
        onSaved={state.reload}
      />
      <TaxSlabDialog
        slab={editingSlab}
        open={editingSlab !== null}
        onOpenChange={(o) => !o && setEditingSlab(null)}
        onSaved={state.reload}
      />
      <ContributionRuleDialog
        rule={editingRule}
        open={editingRule !== null}
        onOpenChange={(o) => !o && setEditingRule(null)}
        onSaved={state.reload}
      />
      <ComponentDialog
        comp={addingComp ? {} : editingComp}
        components={state.data ? state.data.components : []}
        open={addingComp || editingComp !== null}
        onOpenChange={(o) => {
          if (!o) {
            setAddingComp(false);
            setEditingComp(null);
          }
        }}
        onSaved={state.reload}
      />
    </AppShell>
  );
}
