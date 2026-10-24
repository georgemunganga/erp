import { FormSelect } from "@/components/ui/form-select";
import { PeriodBenefitAssignments, benefitPayrollPeriods } from "@/platform/components/PeriodBenefitAssignments";
import { useAuth } from "@/platform/auth";
import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState, type FormEvent } from "react";
import { ChevronLeft, ChevronRight, Edit, Plus, RefreshCw, Search, Trash2, UsersRound } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/benefits")({ component: Benefits });

type Row = Record<string, unknown>;
type Mode = "list" | "assign" | "bulk" | "types" | "claims";

const ASSIGNMENT_PAGE_SIZE = 25;

const STATUS_LABEL: Record<string, string> = {
  submitted: "Submitted",
  returned: "Returned",
  approved: "Approved",
  rejected: "Rejected",
  paid: "Paid",
};

const STATUS_CLASS: Record<string, string> = {
  submitted: "rounded bg-amber-100 px-1.5 py-0.5 text-xs font-medium text-amber-700",
  returned: "rounded bg-sky-100 px-1.5 py-0.5 text-xs font-medium text-sky-700",
  approved: "rounded bg-emerald-100 px-1.5 py-0.5 text-xs font-medium text-emerald-700",
  rejected: "rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-700",
  paid: "rounded bg-slate-700 px-1.5 py-0.5 text-xs font-medium text-white",
};

function emptyType(): Row {
  return {
    code: "",
    name: "",
    description: "",
    annualCap: 0,
    requiresEvidence: false,
    includeInPayroll: false,
    isTaxable: false,
    isActive: true,
  };
}

function text(value: unknown) {
  return value === undefined || value === null ? "" : String(value);
}

function money(value: unknown) {
  const amount = Number(value ?? 0);
  return Number.isFinite(amount)
    ? amount.toLocaleString(undefined, { maximumFractionDigits: 2 })
    : "0";
}

function isPayrollBenefit(type: Row | undefined) {
  return Boolean(type?.includeInPayroll);
}

function monthlyAmountFromAnnual(annualAmount: string) {
  if (!annualAmount) return "";
  const annual = Number(annualAmount);
  return Number.isFinite(annual) ? String(Math.round((annual / 12) * 100) / 100) : annualAmount;
}

function annualAmountFromMonthly(monthlyAmount: string) {
  if (!monthlyAmount) return "";
  const monthly = Number(monthlyAmount);
  return Number.isFinite(monthly) ? String(Math.round(monthly * 12 * 100) / 100) : monthlyAmount;
}

function employeeLabel(row: Row) {
  const name = text(row.fullName ?? row.name ?? row.employeeName) || "Employee";
  return `${name}${row.employeeNo ? ` (${text(row.employeeNo)})` : ""}`;
}

function EditClaimDialog({ row, onClose, onSaved }: { row: Row; onClose: () => void; onSaved: () => void }) {
  const [amount, setAmount] = useState(text(row.amountClaimed));
  const [currency, setCurrency] = useState(text(row.currency || "ZMW"));
  const [note, setNote] = useState(text(row.note));
  const [evidence, setEvidence] = useState(Boolean(row.evidenceAttached));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function save(event: FormEvent) {
    event.preventDefault();
    if (!/^\d+(\.\d{1,2})?$/.test(amount) || Number(amount) <= 0) { setError("Enter a positive amount with at most two decimal places."); return; }
    setBusy(true); setError("");
    try {
      await realApi.updateBenefitClaim(text(row.id), { amountClaimed: Number(amount), currency, note, evidenceAttached: evidence });
      toast.success("Claim updated. Recalculate payroll if this is a payroll claim.");
      onSaved(); onClose();
    } catch (cause) { setError(cause instanceof Error ? cause.message : "Could not update claim."); }
    finally { setBusy(false); }
  }
  return <Dialog open onOpenChange={open => { if (!open && !busy) onClose(); }}><DialogContent className="sm:max-w-lg"><DialogHeader><DialogTitle>Edit claim</DialogTitle><DialogDescription>{text(row.workerName)} · {text(row.benefitTypeName)}{row.payPeriodId ? " · payroll needs recalculation after saving" : ""}</DialogDescription></DialogHeader>
    <form onSubmit={save} className="space-y-4">
      <div><Label htmlFor="edit-claim-amount">Amount claimed</Label><Input id="edit-claim-amount" type="number" min="0.01" step="0.01" required value={amount} disabled={busy} onChange={event => setAmount(event.target.value)} /></div>
      <div><Label htmlFor="edit-claim-currency">Currency</Label><Select value={currency} onValueChange={setCurrency} disabled={busy}><SelectTrigger id="edit-claim-currency"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="ZMW">ZMW</SelectItem><SelectItem value="USD">USD</SelectItem></SelectContent></Select></div>
      <div><Label htmlFor="edit-claim-note">Note</Label><Input id="edit-claim-note" maxLength={500} value={note} disabled={busy} onChange={event => setNote(event.target.value)} /></div>
      <div className="flex items-center gap-2"><Checkbox id="edit-claim-evidence" checked={evidence} disabled={busy} onCheckedChange={checked => setEvidence(Boolean(checked))} /><Label htmlFor="edit-claim-evidence">Evidence attached</Label></div>
      {error && <p role="alert" className="text-sm text-danger">{error}</p>}
      <div className="flex justify-end gap-2"><Button type="button" variant="outline" disabled={busy} onClick={onClose}>Cancel</Button><Button type="submit" disabled={busy}>{busy ? "Saving…" : "Save changes"}</Button></div>
    </form>
  </DialogContent></Dialog>;
}

function Benefits() {
  const [mode, setMode] = useState<Mode>("list");
  const { user } = useAuth();
  const canAssignPayroll = user?.roles.some(r => ["hr_ops", "hr_admin", "payroll"].includes(r));
  const [claimMode, setClaimMode] = useState<"once" | "recurring" | "reimbursement">("reimbursement");
  const [statusFilter, setStatusFilter] = useState("");
  const [busy, setBusy] = useState(false);
  const [assignmentYearFilter, setAssignmentYearFilter] = useState(() => String(new Date().getFullYear()));
  const [assignmentSearch, setAssignmentSearch] = useState("");
  const [assignmentPayrollFilter, setAssignmentPayrollFilter] = useState("all");
  const [assignmentBenefitFilter, setAssignmentBenefitFilter] = useState("all");
  const [assignmentPage, setAssignmentPage] = useState(1);

  const types = useApi(realApi.benefitTypes, []);
  const allowances = useApi(realApi.benefitAllowances, []);
  const employees = useApi(() => realApi.employees({ pageSize: 200 }), []);
  const orgUnits = useApi(realApi.orgUnits, []);
  const locations = useApi(realApi.locations, []);
  const claims = useApi(
    () => realApi.benefitClaims({ status: statusFilter || undefined, pageSize: 50 }),
    [statusFilter],
  );

  const [typeForm, setTypeForm] = useState<Row>(emptyType());
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null);
  const [deletingType, setDeletingType] = useState<Row | null>(null);
  const [deletingAllowance, setDeletingAllowance] = useState<Row | null>(null);
  const [editingClaim, setEditingClaim] = useState<Row | null>(null);
  const [deletingClaim, setDeletingClaim] = useState<Row | null>(null);

  const [allowanceWorker, setAllowanceWorker] = useState("");
  const [allowanceType, setAllowanceType] = useState("");
  const [allowanceYear, setAllowanceYear] = useState(String(new Date().getFullYear()));
  const [allowanceAmount, setAllowanceAmount] = useState("");

  const [bulkType, setBulkType] = useState("");
  const [bulkYear, setBulkYear] = useState(String(new Date().getFullYear()));
  const [bulkAmount, setBulkAmount] = useState("");
  const [bulkOrgUnit, setBulkOrgUnit] = useState("");
  const [bulkLocation, setBulkLocation] = useState("");
  const [bulkGrade, setBulkGrade] = useState("");
  const [bulkStatus, setBulkStatus] = useState("active");
  const [selectedWorkers, setSelectedWorkers] = useState<Record<string, boolean>>({});
  const bulkWorkers = useApi(
    () =>
      realApi.employees({
        pageSize: 200,
        status: bulkStatus || undefined,
        orgUnitId: bulkOrgUnit || undefined,
        locationId: bulkLocation || undefined,
        grade: bulkGrade || undefined,
      }),
    [bulkStatus, bulkOrgUnit, bulkLocation, bulkGrade],
  );

  const payrollPeriods = useApi(benefitPayrollPeriods, []);
  const [claimPeriod, setClaimPeriod] = useState("");
  const [claimWorker, setClaimWorker] = useState("");
  const [claimType, setClaimType] = useState("");
  const [claimAmount, setClaimAmount] = useState("");
  const [claimCurrency, setClaimCurrency] = useState("ZMW");
  const [claimNote, setClaimNote] = useState("");
  const [claimEvidence, setClaimEvidence] = useState(false);
  const [approveAmount, setApproveAmount] = useState<Record<string, string>>({});
  const [decisionReason, setDecisionReason] = useState<Record<string, string>>({});

  const activeTypes = useMemo(
    () => ((types.data ?? []) as Row[]).filter((row) => Boolean(row.isActive)),
    [types.data],
  );
  const workerRows = (employees.data?.items ?? []) as Row[];
  const bulkWorkerRows = (bulkWorkers.data?.items ?? []) as Row[];
  const employeeOptions = workerRows.map((row) => ({
    value: text(row.id),
    label: employeeLabel(row),
  }));
  const grades = Array.from(
    new Set(workerRows.map((row) => text(row.grade)).filter(Boolean)),
  ).sort();
  const selectedType = activeTypes.find((row) => text(row.code) === allowanceType);
  const selectedBulkType = activeTypes.find((row) => text(row.id) === bulkType);
  const selectedClaimType = activeTypes.find((row) => text(row.code) === claimType);
  const typeByCode = useMemo(
    () => new Map(((types.data ?? []) as Row[]).map((row) => [text(row.code).toLowerCase(), row])),
    [types.data],
  );
  const assignmentYears = useMemo(
    () => Array.from(new Set(((allowances.data ?? []) as Row[]).map((row) => text(row.year)).filter(Boolean)))
      .sort((a, b) => Number(b) - Number(a)),
    [allowances.data],
  );
  const visibleAllowanceRows = useMemo(() => {
    const search = assignmentSearch.trim().toLowerCase();
    return ((allowances.data ?? []) as Row[]).filter((row) => {
      const benefitType = typeByCode.get(text(row.benefitTypeCode).toLowerCase());
      const isPayroll = Boolean(benefitType?.includeInPayroll);
      const matchesYear = assignmentYearFilter === "all" || text(row.year) === assignmentYearFilter;
      const matchesPayroll = assignmentPayrollFilter === "all"
        || (assignmentPayrollFilter === "payslip" ? isPayroll : !isPayroll);
      const matchesBenefit = assignmentBenefitFilter === "all"
        || text(row.benefitTypeCode).toLowerCase() === assignmentBenefitFilter;
      const searchText = [
        row.workerName,
        row.employeeNo,
        row.benefitTypeName,
        row.benefitTypeCode,
        row.year,
      ].map(text).join(" ").toLowerCase();
      return matchesYear && matchesPayroll && matchesBenefit && (!search || searchText.includes(search));
    });
  }, [allowances.data, assignmentYearFilter, assignmentSearch, assignmentPayrollFilter, assignmentBenefitFilter, typeByCode]);
  const assignmentTotalPages = Math.max(1, Math.ceil(visibleAllowanceRows.length / ASSIGNMENT_PAGE_SIZE));
  const currentAssignmentPage = Math.min(assignmentPage, assignmentTotalPages);
  const assignmentPageStart = visibleAllowanceRows.length === 0 ? 0 : (currentAssignmentPage - 1) * ASSIGNMENT_PAGE_SIZE + 1;
  const assignmentPageEnd = Math.min(currentAssignmentPage * ASSIGNMENT_PAGE_SIZE, visibleAllowanceRows.length);
  const paginatedAllowanceRows = visibleAllowanceRows.slice(
    (currentAssignmentPage - 1) * ASSIGNMENT_PAGE_SIZE,
    currentAssignmentPage * ASSIGNMENT_PAGE_SIZE,
  );

  const updateAssignmentFilters = (update: () => void) => {
    update();
    setAssignmentPage(1);
  };

  const run = async <T,>(name: string, operation: () => Promise<T>, onSuccess?: () => void) => {
    setBusy(true);
    try {
      await operation();
      toast.success(`${name} completed`);
      onSuccess?.();
      types.reload();
      allowances.reload();
      claims.reload();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : `${name} failed`);
    } finally {
      setBusy(false);
    }
  };

  const amountOverCap = (amount: string, type: Row | undefined) =>
    Boolean(type) && Number(amount || 0) > Number(type?.annualCap ?? 0);

  const typeOptions = (rows: Row[]) =>
    rows.map((row) => (
      <SelectItem key={text(row.code)} value={text(row.code)}>
        {text(row.name)} · cap {money(row.annualCap)}
        {row.includeInPayroll ? " · payslip" : " · claim"}
      </SelectItem>
    ));

  const submitType = async () => {
    const annualCap = Number(typeForm.annualCap) || 0;
    if (!typeForm.code || !typeForm.name || annualCap <= 0) {
      toast.error("Code, name and a positive annual cap are required.");
      return;
    }
    const duplicate = ((types.data ?? []) as Row[]).find((row) =>
      text(row.id) !== editingTypeId &&
      (text(row.code).trim().toLowerCase() === text(typeForm.code).trim().toLowerCase() ||
       text(row.name).trim().toLowerCase() === text(typeForm.name).trim().toLowerCase()));
    if (duplicate) {
      toast.error("A benefit type with this code or name already exists.");
      return;
    }
    if (editingTypeId) {
      await run(
        "Benefit type updated",
        () =>
          realApi.updateBenefitType(editingTypeId, {
            code: typeForm.code,
            name: typeForm.name,
            description: typeForm.description || null,
            annualCap,
            requiresEvidence: Boolean(typeForm.requiresEvidence),
            includeInPayroll: Boolean(typeForm.includeInPayroll),
            isTaxable: Boolean(typeForm.isTaxable),
            isActive: Boolean(typeForm.isActive),
          }),
        () => {
          setEditingTypeId(null);
          setTypeForm(emptyType());
        },
      );
    } else {
      await run(
        "Benefit type created",
        () =>
          realApi.createBenefitType({
            code: typeForm.code,
            name: typeForm.name,
            description: typeForm.description || null,
            annualCap,
            requiresEvidence: Boolean(typeForm.requiresEvidence),
            includeInPayroll: Boolean(typeForm.includeInPayroll),
            isTaxable: Boolean(typeForm.isTaxable),
          }),
        () => setTypeForm(emptyType()),
      );
    }
  };

  const deleteType = async () => {
    if (!deletingType) return;
    const name = text(deletingType.name);
    await run("Benefit type deleted", () => realApi.deleteBenefitType(text(deletingType.id)), () => {
      if (editingTypeId === text(deletingType.id)) {
        setEditingTypeId(null);
        setTypeForm(emptyType());
      }
      setDeletingType(null);
    });
  };

  const startEditAllowance = (row: Row) => {
    setAllowanceWorker(text(row.workerId));
    setAllowanceType(text(row.benefitTypeCode));
    setAllowanceYear(text(row.year) || String(new Date().getFullYear()));
    setAllowanceAmount(text(row.annualAmount));
    setMode("assign");
  };

  const deleteAllowance = async () => {
    if (!deletingAllowance) return;
    await run("Benefit assignment deleted", () =>
      realApi.deleteBenefitAllowance(text(deletingAllowance.id)), () => setDeletingAllowance(null));
  };

  const deleteClaim = async () => {
    if (!deletingClaim) return;
    await run("Claim deleted", () => realApi.deleteBenefitClaim(text(deletingClaim.id)), () => setDeletingClaim(null));
  };

  const submitAllowance = async () => {
    if (!allowanceWorker || !allowanceType || !allowanceAmount) {
      toast.error("Employee, benefit type and amount are required.");
      return;
    }
    if (amountOverCap(allowanceAmount, selectedType)) {
      toast.error(
        `Amount cannot exceed the ${text(selectedType?.name)} cap of ${money(selectedType?.annualCap)}.`,
      );
      return;
    }
    await run(
      "Allowance saved",
      () =>
        realApi.setBenefitAllowance({
          workerId: allowanceWorker,
          benefitTypeCode: allowanceType,
          annualAmount: Number(allowanceAmount) || 0,
          year: Number(allowanceYear) || new Date().getFullYear(),
        }),
      () => {
        setAllowanceWorker("");
        setAllowanceAmount("");
        if (mode !== "claims") setMode("list");
      },
    );
  };

  const applyBulk = async () => {
    const ids = Object.entries(selectedWorkers)
      .filter(([, selected]) => selected)
      .map(([id]) => id);
    if (!selectedBulkType) {
      toast.error("Select the benefit type to assign.");
      return;
    }
    if (!bulkAmount || Number(bulkAmount) <= 0) {
      toast.error("Enter a positive annual amount.");
      return;
    }
    if (!ids.length) {
      toast.error("Select at least one employee.");
      return;
    }
    if (amountOverCap(bulkAmount, selectedBulkType)) {
      toast.error(
        `Amount cannot exceed the ${text(selectedBulkType?.name)} cap of ${money(selectedBulkType?.annualCap)}.`,
      );
      return;
    }
    await run(
      `${ids.length} allowance${ids.length === 1 ? "" : "s"} assigned`,
      async () => {
        for (const workerId of ids) {
          await realApi.setBenefitAllowance({
            workerId,
            benefitTypeCode: text(selectedBulkType.code),
            annualAmount: Number(bulkAmount) || 0,
            year: Number(bulkYear) || new Date().getFullYear(),
          });
        }
      },
      () => {
        setSelectedWorkers({});
        setBulkAmount("");
        setMode("list");
      },
    );
  };

  const submitClaim = async () => {
    if (selectedClaimType?.includeInPayroll && !claimPeriod) {
      toast.error("Select the payroll month for this claim.");
      return;
    }
    if (!claimWorker || !claimType || !claimAmount) {
      toast.error("Employee, benefit type and amount are required.");
      return;
    }
    const evidenceAttached = Boolean(claimEvidence);
    await run(
      "Claim submitted",
      () =>
        realApi.createBenefitClaim({
          workerId: claimWorker,
          benefitTypeCode: claimType,
          amountClaimed: Number(claimAmount) || 0,
          currency: claimCurrency || "ZMW",
          note: claimNote || null,
          evidenceAttached,
          ...(selectedClaimType?.includeInPayroll ? { payPeriodId: claimPeriod } : {}),
        }),
      () => {
        setClaimAmount("");
        setClaimNote("");
        setClaimEvidence(false);
      },
    );
  };

  const decide = async (id: string, action: string) => {
    const raw = approveAmount[id];
    await run(`Claim ${action}`, () =>
      realApi.decideBenefitClaim(id, {
        action,
        reason: decisionReason[id] || undefined,
        ...(action === "approve" && raw ? { approvedAmount: Number(raw) } : {}),
      }),
    );
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave"
          title="Benefits"
          description="Benefit allowances and claims. The list stays simple; setup, assignment and bulk tools open only when needed."
          meta={<ScopeBadge />}
        />

        <div className="mb-4 flex flex-wrap gap-2">
          <Button variant={mode === "list" ? "default" : "outline"} onClick={() => setMode("list")}>
            Assignment list
          </Button>
          <Button
            variant={mode === "assign" ? "default" : "outline"}
            onClick={() => setMode("assign")}
          >
            <Plus className="size-4" aria-hidden /> Assign one
          </Button>
          <Button variant={mode === "bulk" ? "default" : "outline"} onClick={() => setMode("bulk")}>
            <UsersRound className="size-4" aria-hidden /> Bulk assign
          </Button>
          <Button
            variant={mode === "types" ? "default" : "outline"}
            onClick={() => setMode("types")}
          >
            Benefit types
          </Button>
          <Button
            variant={mode === "claims" ? "default" : "outline"}
            onClick={() => setMode("claims")}
          >
            Claims
          </Button>
        </div>

        {mode === "claims" && canAssignPayroll && <div className="mb-4 space-y-3 rounded-lg border bg-card p-4">
          <h2 className="font-semibold">Claims and payroll assignments</h2>
          <p className="text-sm text-muted-foreground">Choose how this benefit should be paid. Payroll assignments are earnings added to the employee’s payslip.</p>
          <div className="flex flex-wrap gap-2">
            <Button variant={claimMode === "once" ? "default" : "outline"} onClick={() => setClaimMode("once")}>Once-off payroll</Button>
            <Button variant={claimMode === "recurring" ? "default" : "outline"} onClick={() => { setClaimMode("recurring"); if (!selectedType?.includeInPayroll) setAllowanceType(""); }}>Recurring payroll</Button>
            <Button variant={claimMode === "reimbursement" ? "default" : "outline"} onClick={() => setClaimMode("reimbursement")}>Reimbursement claim</Button>
          </div>
        </div>}
        {canAssignPayroll && <div hidden={mode !== "claims" || claimMode !== "once"}><PeriodBenefitAssignments types={activeTypes.map(t => ({ id: text(t.id), name: text(t.name), includeInPayroll: Boolean(t.includeInPayroll), isActive: Boolean(t.isActive) }))} /></div>}

        {mode === "list" ? (
          <Card>
            <CardHeader>
              <CardTitle>Allowance assignments</CardTitle>
              <CardDescription>
                The current year is shown by default. One employee can have one allowance per benefit
                type per year; choose all years only when reviewing payroll history.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="mb-4 grid gap-3 lg:grid-cols-[minmax(0,1fr)_11rem_12rem_12rem_auto]">
                <div>
                  <Label htmlFor="assignment-search">Search assignments</Label>
                  <div className="relative mt-1">
                    <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
                    <Input
                      id="assignment-search"
                      value={assignmentSearch}
                      onChange={(event) => updateAssignmentFilters(() => setAssignmentSearch(event.target.value))}
                      placeholder="Employee, number or benefit"
                      className="pl-9"
                    />
                  </div>
                </div>
                <div>
                  <Label htmlFor="assignment-year-filter">Benefit year</Label>
                  <Select value={assignmentYearFilter} onValueChange={(value) => updateAssignmentFilters(() => setAssignmentYearFilter(value))}>
                    <SelectTrigger id="assignment-year-filter" className="mt-1"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All years</SelectItem>
                      {assignmentYears.map((year) => <SelectItem key={year} value={year}>{year}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="assignment-benefit-filter">Benefit type</Label>
                  <Select value={assignmentBenefitFilter} onValueChange={(value) => updateAssignmentFilters(() => setAssignmentBenefitFilter(value))}>
                    <SelectTrigger id="assignment-benefit-filter" className="mt-1"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All benefits</SelectItem>
                      {((types.data ?? []) as Row[]).map((type) => (
                        <SelectItem key={text(type.code)} value={text(type.code).toLowerCase()}>{text(type.name)}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="assignment-payroll-filter">Payroll use</Label>
                  <Select value={assignmentPayrollFilter} onValueChange={(value) => updateAssignmentFilters(() => setAssignmentPayrollFilter(value))}>
                    <SelectTrigger id="assignment-payroll-filter" className="mt-1"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All uses</SelectItem>
                      <SelectItem value="payslip">Added to payslip</SelectItem>
                      <SelectItem value="claim">Claim only</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex items-end">
                  <Button type="button" variant="outline" onClick={() => allowances.reload()} disabled={allowances.loading}>
                    <RefreshCw className="size-4" aria-hidden />
                    Refresh
                  </Button>
                </div>
              </div>
              <div className="mb-4 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                <span>
                  {visibleAllowanceRows.length === 0 ? "No" : `Showing ${assignmentPageStart}-${assignmentPageEnd} of ${visibleAllowanceRows.length}`} assignment{visibleAllowanceRows.length === 1 ? "" : "s"}
                </span>
                {(assignmentSearch || assignmentYearFilter !== String(new Date().getFullYear()) || assignmentBenefitFilter !== "all" || assignmentPayrollFilter !== "all") ? (
                  <Button type="button" variant="ghost" size="sm" onClick={() => {
                    setAssignmentSearch("");
                    setAssignmentYearFilter(String(new Date().getFullYear()));
                    setAssignmentBenefitFilter("all");
                    setAssignmentPayrollFilter("all");
                    setAssignmentPage(1);
                  }}>
                    Clear filters
                  </Button>
                ) : null}
              </div>
              {allowances.loading ? (
                <p className="text-sm text-muted-foreground">Loading assignments...</p>
              ) : null}
              {allowances.error ? (
                <p className="text-sm text-destructive">{allowances.error}</p>
              ) : null}
              {allowances.data && !visibleAllowanceRows.length ? (
                <p className="text-sm text-muted-foreground">
                  No allowance assignments for {assignmentYearFilter === "all" ? "the selected history" : assignmentYearFilter}.
                </p>
              ) : null}
              {allowances.data && visibleAllowanceRows.length ? (
                <div className="overflow-x-auto">
                <Table className="min-w-[900px]">
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Benefit</TableHead>
                      <TableHead>Payroll use</TableHead>
                      <TableHead>Year</TableHead>
                      <TableHead className="text-right">Annual amount</TableHead>
                      <TableHead className="text-right">Monthly payslip</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {paginatedAllowanceRows.map((row) => {
                      const benefitType = typeByCode.get(text(row.benefitTypeCode).toLowerCase());
                      return (
                        <TableRow key={text(row.id)}>
                          <TableCell>
                            <div className="font-medium">{text(row.workerName)}</div>
                            <div className="text-xs text-muted-foreground">
                              {text(row.employeeNo)}
                            </div>
                          </TableCell>
                          <TableCell>
                            <div>{text(row.benefitTypeName)}</div>
                            <div className="text-xs text-muted-foreground">
                              {text(row.benefitTypeCode)}
                            </div>
                          </TableCell>
                          <TableCell>
                            <span className="rounded bg-muted px-1.5 py-0.5 text-xs font-medium">
                              {benefitType?.includeInPayroll ? "Added to payslip" : "Claim only"}
                            </span>
                          </TableCell>
                          <TableCell>{text(row.year)}</TableCell>
                          <TableCell className="text-right tabular-nums">
                            {money(row.annualAmount)}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            {benefitType?.includeInPayroll
                              ? money(Number(row.annualAmount ?? 0) / 12)
                              : "-"}
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-2">
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => startEditAllowance(row)}
                              >
                                <Edit className="size-4" aria-hidden /> Edit
                              </Button>
                              <Button
                                size="sm"
                                variant="destructive"
                                onClick={() => setDeletingAllowance(row)}
                              >
                                <Trash2 className="size-4" aria-hidden /> Delete
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
                </div>
              ) : null}
              {visibleAllowanceRows.length > ASSIGNMENT_PAGE_SIZE ? (
                <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-card px-3 py-2 text-sm">
                  <span className="text-muted-foreground">Page {currentAssignmentPage} of {assignmentTotalPages}</span>
                <div className="flex items-center gap-2">
                    <Button type="button" variant="outline" size="sm" disabled={currentAssignmentPage <= 1} onClick={() => setAssignmentPage((page) => Math.max(1, page - 1))}>
                      <ChevronLeft className="mr-1 size-4" aria-hidden /> Previous
                    </Button>
                    <Button type="button" variant="outline" size="sm" disabled={currentAssignmentPage >= assignmentTotalPages} onClick={() => setAssignmentPage((page) => Math.min(assignmentTotalPages, page + 1))}>
                      Next <ChevronRight className="ml-1 size-4" aria-hidden />
                    </Button>
                  </div>
                </div>
              ) : null}
            </CardContent>
          </Card>
        ) : null}

        {(mode === "assign" || (mode === "claims" && canAssignPayroll && claimMode === "recurring")) ? (
          <Card>
            <CardHeader>
              <CardTitle>{mode === "claims" ? "Recurring payroll assignment" : "Assign allowance"}</CardTitle>
              <CardDescription>
                Enter the annual allowance or monthly payslip amount for the selected year. Recurring payroll
                assignments apply throughout that year (annual amount divided by 12). For a single month, use Once-off payroll in Claims.
              </CardDescription>
            </CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="md:col-span-2">
                <Label>Employee</Label>
                <Select value={allowanceWorker || undefined} onValueChange={setAllowanceWorker}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select employee..." />
                  </SelectTrigger>
                  <SelectContent className="max-h-64">
                    {employeeOptions.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Benefit type</Label>
                <Select value={allowanceType || undefined} onValueChange={setAllowanceType}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select type..." />
                  </SelectTrigger>
                  <SelectContent>{typeOptions(mode === "claims" ? activeTypes.filter(t => Boolean(t.includeInPayroll)) : activeTypes)}</SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="allowance-year">Year</Label>
                <Input
                  id="allowance-year"
                  type="number"
                  value={allowanceYear}
                  onChange={(e) => setAllowanceYear(e.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="allowance-amount">Annual amount</Label>
                <Input
                  id="allowance-amount"
                  type="number"
                  min="0"
                  step="0.01"
                  value={allowanceAmount}
                  onChange={(e) => setAllowanceAmount(e.target.value)}
                />
                {selectedType ? (
                  <p className="mt-1 text-xs text-muted-foreground">
                    Annual cap: {money(selectedType.annualCap)}
                  </p>
                ) : null}
                {amountOverCap(allowanceAmount, selectedType) ? (
                  <p className="mt-1 text-xs text-destructive">
                    Amount is above the configured cap.
                  </p>
                ) : null}
              </div>
              {isPayrollBenefit(selectedType) ? (
                <div>
                  <Label htmlFor="allowance-monthly-preview">Monthly payslip amount</Label>
                  <Input
                    id="allowance-monthly-preview"
                    type="number"
                    min="0"
                    step="0.01"
                    value={monthlyAmountFromAnnual(allowanceAmount)}
                    onChange={(e) => setAllowanceAmount(annualAmountFromMonthly(e.target.value))}
                    aria-describedby="allowance-monthly-help"
                  />
                  <p id="allowance-monthly-help" className="mt-1 text-xs text-muted-foreground">
                    Edit either amount. Monthly x 12 updates the annual amount; annual / 12 updates this monthly amount.
                  </p>
                </div>
              ) : null}
              <div className="flex items-end justify-end gap-2">
                <Button variant="outline" onClick={() => setMode("list")}>
                  Cancel
                </Button>
                <Button
                  onClick={submitAllowance}
                  disabled={busy || amountOverCap(allowanceAmount, selectedType)}
                >
                  Save allowance
                </Button>
              </div>
            </CardContent>
          </Card>
        ) : null}

        {mode === "bulk" ? (
          <Card>
            <CardHeader>
              <CardTitle>Bulk assign allowances</CardTitle>
              <CardDescription>
                Filter employees by department, branch, grade or status, select the matching people,
                then assign one allowance to all selected employees.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 md:grid-cols-4">
                <div>
                  <Label>Department</Label>
                  <Select
                    value={bulkOrgUnit || "all"}
                    onValueChange={(v) => setBulkOrgUnit(v === "all" ? "" : v)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All departments</SelectItem>
                      {((orgUnits.data ?? []) as Row[]).map((row) => (
                        <SelectItem key={text(row.id)} value={text(row.id)}>
                          {text(row.name)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>Branch</Label>
                  <Select
                    value={bulkLocation || "all"}
                    onValueChange={(v) => setBulkLocation(v === "all" ? "" : v)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All branches</SelectItem>
                      {((locations.data ?? []) as Row[]).map((row) => (
                        <SelectItem key={text(row.id)} value={text(row.id)}>
                          {text(row.name)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>Pay grade</Label>
                  <Select
                    value={bulkGrade || "all"}
                    onValueChange={(v) => setBulkGrade(v === "all" ? "" : v)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All grades</SelectItem>
                      {grades.map((grade) => (
                        <SelectItem key={grade} value={grade}>
                          {grade}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>Status</Label>
                  <Select
                    value={bulkStatus || "all"}
                    onValueChange={(v) => setBulkStatus(v === "all" ? "" : v)}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All statuses</SelectItem>
                      <SelectItem value="active">Active</SelectItem>
                      <SelectItem value="pre-hire">Pre-hire</SelectItem>
                      <SelectItem value="on-leave">On leave</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <Label>Benefit type</Label>
                  <Select value={bulkType || undefined} onValueChange={setBulkType}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select type..." />
                    </SelectTrigger>
                    <SelectContent>
                      {activeTypes.map((row) => (
                        <SelectItem key={text(row.id)} value={text(row.id)}>
                          {text(row.name)} · cap {money(row.annualCap)}
                          {row.includeInPayroll ? " · payslip" : " · claim"}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="bulk-year">Year</Label>
                  <Input
                    id="bulk-year"
                    type="number"
                    value={bulkYear}
                    onChange={(e) => setBulkYear(e.target.value)}
                  />
                </div>
                <div>
                <Label htmlFor="bulk-amount">Annual amount</Label>
                <Input
                    id="bulk-amount"
                    type="number"
                    min="0"
                    step="0.01"
                  value={bulkAmount}
                  onChange={(e) => setBulkAmount(e.target.value)}
                />
                {selectedBulkType ? (
                  <p className="mt-1 text-xs text-muted-foreground">
                    Annual cap: {money(selectedBulkType.annualCap)}
                  </p>
                  ) : null}
                  {amountOverCap(bulkAmount, selectedBulkType) ? (
                    <p className="mt-1 text-xs text-destructive">
                      Amount is above the configured cap.
                    </p>
                  ) : null}
                </div>
                {isPayrollBenefit(selectedBulkType) ? (
                  <div>
                    <Label htmlFor="bulk-monthly-preview">Monthly payslip amount</Label>
                    <Input
                      id="bulk-monthly-preview"
                      type="number"
                      min="0"
                      step="0.01"
                      value={monthlyAmountFromAnnual(bulkAmount)}
                      onChange={(e) => setBulkAmount(annualAmountFromMonthly(e.target.value))}
                      aria-describedby="bulk-monthly-help"
                    />
                    <p id="bulk-monthly-help" className="mt-1 text-xs text-muted-foreground">
                      Edit either amount. Monthly x 12 updates the annual amount for every selected employee.
                    </p>
                  </div>
                ) : null}
              </div>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm text-muted-foreground">
                  {bulkWorkerRows.length} employee{bulkWorkerRows.length === 1 ? "" : "s"} listed.
                </p>
                <div className="flex gap-2">
                  <Button
                    variant="outline"
                    onClick={() =>
                      setSelectedWorkers(
                        Object.fromEntries(bulkWorkerRows.map((row) => [text(row.id), true])),
                      )
                    }
                  >
                    Select all listed
                  </Button>
                  <Button variant="outline" onClick={() => setSelectedWorkers({})}>
                    Clear
                  </Button>
                  <Button
                    onClick={applyBulk}
                    disabled={busy || !selectedBulkType || amountOverCap(bulkAmount, selectedBulkType)}
                  >
                    Apply to selected
                  </Button>
                </div>
              </div>
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-12">Use</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Department</TableHead>
                      <TableHead>Branch</TableHead>
                      <TableHead>Grade</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {bulkWorkerRows.map((row) => {
                      const id = text(row.id);
                      return (
                        <TableRow key={id}>
                          <TableCell>
                            <Checkbox
                              checked={Boolean(selectedWorkers[id])}
                              onCheckedChange={(checked) =>
                                setSelectedWorkers((s) => ({ ...s, [id]: Boolean(checked) }))
                              }
                            />
                          </TableCell>
                          <TableCell>{employeeLabel(row)}</TableCell>
                          <TableCell>{text(row.orgUnitName) || "-"}</TableCell>
                          <TableCell>{text(row.locationName) || "-"}</TableCell>
                          <TableCell>{text(row.grade) || "-"}</TableCell>
                        </TableRow>
                      );
                    })}
                    {!bulkWorkerRows.length ? (
                      <TableRow>
                        <TableCell
                          colSpan={5}
                          className="py-8 text-center text-sm text-muted-foreground"
                        >
                          No employees match these filters.
                        </TableCell>
                      </TableRow>
                    ) : null}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        ) : null}

        {mode === "types" ? (
          <Card>
            <CardHeader>
              <CardTitle>Benefit types</CardTitle>
              <CardDescription>
                Configure claimable benefits and annual caps. These are configuration records.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <Label htmlFor="type-code">Code</Label>
                  <Input
                    id="type-code"
                    value={text(typeForm.code)}
                    disabled={editingTypeId !== null}
                    onChange={(e) => setTypeForm({ ...typeForm, code: e.target.value })}
                  />
                </div>
                <div>
                  <Label htmlFor="type-name">Name</Label>
                  <Input
                    id="type-name"
                    value={text(typeForm.name)}
                    onChange={(e) => setTypeForm({ ...typeForm, name: e.target.value })}
                  />
                </div>
                <div>
                  <Label htmlFor="type-cap">Annual cap</Label>
                  <Input
                    id="type-cap"
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={text(typeForm.annualCap)}
                    onChange={(e) =>
                      setTypeForm({ ...typeForm, annualCap: Number(e.target.value) })
                    }
                  />
                </div>
                <div className="flex items-end gap-2 pb-2">
                  <Checkbox
                    id="type-evidence"
                    checked={Boolean(typeForm.requiresEvidence)}
                    onCheckedChange={(checked) =>
                      setTypeForm({ ...typeForm, requiresEvidence: Boolean(checked) })
                    }
                  />
                  <Label htmlFor="type-evidence">Requires evidence</Label>
                </div>
                <div className="flex items-end gap-2 pb-2">
                  <Checkbox
                    id="type-payroll"
                    checked={Boolean(typeForm.includeInPayroll)}
                    onCheckedChange={(checked) =>
                      setTypeForm({ ...typeForm, includeInPayroll: Boolean(checked) })
                    }
                  />
                  <Label htmlFor="type-payroll">Add to payslip</Label>
                </div>
                <div className="flex items-center gap-2">
                  <Checkbox
                    id="type-taxable"
                    checked={Boolean(typeForm.isTaxable)}
                    disabled={!Boolean(typeForm.includeInPayroll)}
                    onCheckedChange={(checked) =>
                      setTypeForm({ ...typeForm, isTaxable: Boolean(checked) })
                    }
                  />
                  <Label htmlFor="type-taxable">Taxable for PAYE</Label>
                </div>
                <div className="md:col-span-2">
                  <Label htmlFor="type-description">Description</Label>
                  <Textarea
                    id="type-description"
                    rows={2}
                    value={text(typeForm.description)}
                    onChange={(e) => setTypeForm({ ...typeForm, description: e.target.value })}
                  />
                </div>
              </div>
              <div className="flex gap-2">
                {editingTypeId ? (
                  <Button
                    variant="outline"
                    onClick={() => {
                      setEditingTypeId(null);
                      setTypeForm(emptyType());
                    }}
                  >
                    Cancel edit
                  </Button>
                ) : null}
                <Button onClick={submitType} disabled={busy}>
                  {editingTypeId ? "Save type" : "Create type"}
                </Button>
              </div>
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Name</TableHead>
                      <TableHead>Code</TableHead>
                      <TableHead className="text-right">Cap</TableHead>
                      <TableHead>Evidence</TableHead>
                      <TableHead>Payroll use</TableHead>
                      <TableHead>PAYE</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {((types.data ?? []) as Row[]).map((row) => (
                      <TableRow key={text(row.id)}>
                        <TableCell>{text(row.name)}</TableCell>
                        <TableCell>{text(row.code)}</TableCell>
                        <TableCell className="text-right tabular-nums">
                          {money(row.annualCap)}
                        </TableCell>
                        <TableCell>{row.requiresEvidence ? "Required" : "No"}</TableCell>
                        <TableCell>
                          {row.includeInPayroll ? "Added to payslip" : "Claim only"}
                        </TableCell>
                        <TableCell>{row.includeInPayroll ? (row.isTaxable ? "Taxable" : "Exempt") : "—"}</TableCell>
                        <TableCell>{row.isActive ? "Active" : "Inactive"}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                setEditingTypeId(text(row.id));
                                setTypeForm(row);
                              }}
                            >
                              <Edit className="size-4" aria-hidden /> Edit
                            </Button>
                            <Button
                              size="icon"
                              variant="ghost"
                              aria-label={`Delete ${text(row.name)}`}
                              onClick={() => setDeletingType(row)}
                            >
                              <Trash2 className="size-4 text-destructive" aria-hidden />
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        ) : null}

        {mode === "claims" && (!canAssignPayroll || claimMode === "reimbursement") ? (
          <Card>
            <CardHeader>
              <CardTitle>Claims</CardTitle>
              <CardDescription>
                Submit and decide claims against the configured allowance caps.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <Label>Employee</Label>
                  <Select value={claimWorker || undefined} onValueChange={setClaimWorker}>
                    <SelectTrigger aria-label="Claim employee">
                      <SelectValue placeholder="Select employee..." />
                    </SelectTrigger>
                    <SelectContent className="max-h-64">
                      {employeeOptions.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>Benefit type</Label>
                  <Select value={claimType || undefined} onValueChange={setClaimType}>
                    <SelectTrigger aria-label="Claim benefit type">
                      <SelectValue placeholder="Select type..." />
                    </SelectTrigger>
                    <SelectContent>{activeTypes.map(t => <SelectItem key={text(t.code)} value={text(t.code)}>{text(t.name)} · claim</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                {Boolean(selectedClaimType?.includeInPayroll) && <div>
                  <Label htmlFor="claim-payroll-period">Payroll month</Label>
                  <FormSelect id="claim-payroll-period" className="block h-10 w-full rounded-md border bg-background px-2" value={claimPeriod} onChange={e => setClaimPeriod(e.target.value)} disabled={busy || payrollPeriods.loading}>
                    <option value="">Select payroll month...</option>
                    {(payrollPeriods.data ?? []).map(p => <option key={p.id} value={p.id}>{p.periodLabel}</option>)}
                  </FormSelect>
                  <p className="mt-1 text-xs text-muted-foreground">Added directly to this month's payroll. Tax follows the benefit settings.</p>
                  {(payrollPeriods.error || payrollPeriods.degraded) && <p role="alert" className="text-sm text-danger">Payroll months could not be loaded. Refresh to retry.</p>}
                </div>}
                <div>
                  <Label htmlFor="claim-amount">Amount claimed</Label>
                  <Input
                    id="claim-amount"
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={claimAmount}
                    onChange={(e) => setClaimAmount(e.target.value)}
                  />
                </div>
                <div>
                  <Label htmlFor="claim-currency">Currency</Label>
                  <Select value={claimCurrency} onValueChange={setClaimCurrency}>
                    <SelectTrigger id="claim-currency">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ZMW">ZMW</SelectItem>
                      <SelectItem value="USD">USD</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label htmlFor="claim-note">Note</Label>
                  <Input
                    id="claim-note"
                    value={claimNote}
                    onChange={(e) => setClaimNote(e.target.value)}
                  />
                </div>
                <div className="flex items-end gap-2 pb-2">
                  <Checkbox
                    id="claim-evidence"
                    checked={claimEvidence}
                    onCheckedChange={(checked) => setClaimEvidence(Boolean(checked))}
                  />
                  <Label htmlFor="claim-evidence">Evidence attached</Label>
                </div>
                <Button className="md:col-span-3" onClick={submitClaim} disabled={busy || (Boolean(selectedClaimType?.includeInPayroll) && !canAssignPayroll)}>
                  Submit claim
                </Button>

              </div>
              <div className="flex justify-end">
                <Select
                  value={statusFilter || "all"}
                  onValueChange={(value) => setStatusFilter(value === "all" ? "" : value)}
                >
                  <SelectTrigger className="w-44">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All statuses</SelectItem>
                    {Object.entries(STATUS_LABEL).map(([key, label]) => (
                      <SelectItem key={key} value={key}>
                        {label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Benefit</TableHead>
                      <TableHead className="text-right">Claimed</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Decision</TableHead>
                      <TableHead>Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {(claims.data?.items ?? []).map((row) => (
                      <TableRow key={text(row.id)}>
                        <TableCell>
                          {text(row.workerName)}
                          <div className="text-xs text-muted-foreground">
                            {text(row.employeeNo)}
                          </div>
                        </TableCell>
                        <TableCell>{text(row.benefitTypeName)}{Boolean(row.payPeriodId) && <div className="text-xs text-muted-foreground">{payrollPeriods.data?.find(p => p.id === row.payPeriodId)?.periodLabel ?? "Payroll assignment"}</div>}</TableCell>
                        <TableCell className="text-right">
                          {money(row.amountClaimed)} {text(row.currency || "ZMW")}
                        </TableCell>
                        <TableCell>
                          <span className={STATUS_CLASS[text(row.status)] ?? ""}>
                            {row.payPeriodId ? "Assigned to payroll" : STATUS_LABEL[text(row.status)] ?? text(row.status)}
                          </span>
                        </TableCell>
                        <TableCell>
                          {(row.status === "submitted" || row.status === "returned") && <><Input
                            className="mb-1 h-8 min-w-36"
                            placeholder="Reason"
                            value={decisionReason[text(row.id)] ?? ""}
                            onChange={(e) =>
                              setDecisionReason((s) => ({ ...s, [text(row.id)]: e.target.value }))
                            }
                          />
                          <Input
                            className="h-8 min-w-36"
                            placeholder="Approved amount"
                            value={approveAmount[text(row.id)] ?? ""}
                            onChange={(e) =>
                              setApproveAmount((s) => ({ ...s, [text(row.id)]: e.target.value }))
                            }
                          /></>}
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap gap-1">
                          {row.status === "submitted" || row.status === "returned" ? (
                            <div className="flex flex-wrap gap-1">
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => decide(text(row.id), "approve")}
                                disabled={busy}
                              >
                                Approve
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => decide(text(row.id), "reject")}
                                disabled={busy}
                              >
                                Reject
                              </Button>
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => decide(text(row.id), "return")}
                                disabled={busy}
                              >
                                Return
                              </Button>
                            </div>
                          ) : row.status === "approved" && !row.payPeriodId ? (
                            <Button
                              size="sm"
                              onClick={() =>
                                run("Claim paid", () => realApi.payBenefitClaim(text(row.id)))
                              }
                              disabled={busy}
                            >
                              Mark paid
                            </Button>
                          ) : null}
                          {canAssignPayroll && (row.payPeriodId || ["submitted", "returned", "rejected"].includes(text(row.status))) && <>
                            <Button size="sm" variant="outline" aria-label={`Edit claim for ${text(row.workerName)} ${money(row.amountClaimed)}`} onClick={() => setEditingClaim(row)} disabled={busy}><Edit className="mr-1 size-4" />Edit</Button>
                            <Button size="sm" variant="outline" aria-label={`Delete claim for ${text(row.workerName)} ${money(row.amountClaimed)}`} onClick={() => setDeletingClaim(row)} disabled={busy}><Trash2 className="mr-1 size-4" />Delete</Button>
                          </>}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                    {!claims.data?.items?.length ? (
                      <TableRow>
                        <TableCell
                          colSpan={6}
                          className="py-8 text-center text-sm text-muted-foreground"
                        >
                          No claims found.
                        </TableCell>
                      </TableRow>
                    ) : null}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        ) : null}

        <ConfirmDialog
          open={Boolean(deletingType)}
          onOpenChange={(open) => { if (!open) setDeletingType(null); }}
          title="Delete benefit type"
          consequence={`Delete ${text(deletingType?.name)}. This is only available when it has no employee allowances or claims.`}
          confirmLabel="Delete benefit type"
          destructive
          onConfirm={() => void deleteType()}
        />
        {editingClaim && <EditClaimDialog key={text(editingClaim.id)} row={editingClaim} onClose={() => setEditingClaim(null)} onSaved={() => claims.reload()} />}
        <ConfirmDialog open={Boolean(deletingClaim)} onOpenChange={open => { if (!open) setDeletingClaim(null); }} title="Delete claim" consequence={`Remove ${money(deletingClaim?.amountClaimed)} ${text(deletingClaim?.currency)} for ${text(deletingClaim?.workerName)} from ${text(deletingClaim?.benefitTypeName)}?`} detail={deletingClaim?.payPeriodId ? "The assigned payroll amount will decrease. Recalculate payroll before approval." : "The claim will be removed from the claims list."} confirmLabel="Delete claim" destructive onConfirm={() => void deleteClaim()} />
        <ConfirmDialog
          open={Boolean(deletingAllowance)}
          onOpenChange={(open) => { if (!open) setDeletingAllowance(null); }}
          title="Delete benefit assignment"
          consequence={`Remove ${text(deletingAllowance?.benefitTypeName)} for ${text(deletingAllowance?.workerName)} in ${text(deletingAllowance?.year)}. This cannot be undone.`}
          detail="Assignments with benefit claims for the same employee and year cannot be deleted. Edit the amount instead."
          confirmLabel="Delete assignment"
          destructive
          onConfirm={() => void deleteAllowance()}
        />
      </AppShell>
    </AuthGate>
  );
}
