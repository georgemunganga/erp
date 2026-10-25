import { createFileRoute } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { useMemo, useState } from "react";
import { Edit, Plus, RotateCcw, WalletCards } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
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
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/payroll/salary-advances")({
  head: () => ({
    meta: [
      { title: "Loans and salary advances — Newworldcargo HRM" },
      { name: "description", content: "Record loans and salary advances with prior recoveries and future payroll deductions." },
    ],
  }),
  component: SalaryAdvancesPage,
});

type Row = Record<string, unknown>;
type Form = {
  workerId: string;
  amount: string;
  recordType: string;
  openingRecoveredAmount: string;
  installmentAmount: string;
  currency: string;
  issueDate: string;
  deductionStartDate: string;
  deductFromPayslip: boolean;
  reason: string;
  reference: string;
};

const today = new Date().toISOString().slice(0, 10);

function emptyForm(): Form {
  return {
    workerId: "",
    amount: "",
    recordType: "salary-advance",
    openingRecoveredAmount: "0",
    installmentAmount: "",
    currency: "ZMW",
    issueDate: today,
    deductionStartDate: today,
    deductFromPayslip: true,
    reason: "",
    reference: "",
  };
}

function text(value: unknown) {
  return value === undefined || value === null ? "" : String(value);
}

function money(value: unknown, currency = "ZMW") {
  const amount = Number(value ?? 0);
  return `${currency} ${Number.isFinite(amount) ? amount.toLocaleString(undefined, { maximumFractionDigits: 2 }) : "0"}`;
}

function employeeLabel(row: Row) {
  const name = text(row.fullName ?? row.name ?? row.employeeName) || "Employee";
  return `${name}${row.employeeNo ? ` (${text(row.employeeNo)})` : ""}`;
}

function SalaryAdvancesPage() {
  const [status, setStatus] = useState("active");
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Row | null>(null);
  const [form, setForm] = useState<Form>(emptyForm());
  const [cancelReason, setCancelReason] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  const advances = useApi(
    () => realApi.salaryAdvances({ status: status === "all" ? undefined : status }),
    [status],
  );
  const employees = useApi(() => realApi.employees({ pageSize: 200, status: "active" }), []);
  const rows = ((advances.data ?? []) as Row[]);
  const employeeRows = (employees.data?.items ?? []) as Row[];

  const totals = useMemo(() => {
    const active = rows.filter((row) => text(row.status) === "active");
    return {
      count: rows.length,
      active: active.length,
      remaining: active.reduce((sum, row) => sum + Number(row.remainingAmount ?? 0), 0),
    };
  }, [rows]);

  function startCreate() {
    setEditing(null);
    setForm(emptyForm());
    setShowForm(true);
  }

  function startEdit(row: Row) {
    setEditing(row);
    setForm({
      workerId: text(row.workerId),
      amount: text(row.amount),
      recordType: text(row.recordType) || "salary-advance",
      openingRecoveredAmount: text(row.openingRecoveredAmount) || "0",
      installmentAmount: text(row.installmentAmount),
      currency: text(row.currency) || "ZMW",
      issueDate: text(row.issueDate),
      deductionStartDate: text(row.deductionStartDate) || today,
      deductFromPayslip: Boolean(row.deductFromPayslip),
      reason: text(row.reason),
      reference: text(row.reference),
    });
    setShowForm(true);
  }

  async function save() {
    const amount = Number(form.amount);
    const installment = Number(form.installmentAmount);
    const openingRecovered = Number(form.openingRecoveredAmount);
    if (!form.workerId || !Number.isFinite(amount) || amount <= 0 || !Number.isFinite(installment) || installment <= 0) {
      toast.error("Employee, original amount and deduction amount are required.");
      return;
    }
    if (!editing && (!Number.isFinite(openingRecovered) || openingRecovered < 0 || openingRecovered >= amount || installment > amount - openingRecovered)) {
      toast.error("Opening recovery and deduction must fit within the original amount.");
      return;
    }
    setBusy(true);
    try {
      if (editing) {
        await realApi.updateSalaryAdvance(text(editing.id), {
          installmentAmount: installment,
          deductFromPayslip: form.deductFromPayslip,
          deductionStartDate: form.deductionStartDate,
          reason: form.reason || null,
          reference: form.reference || null,
        });
        toast.success("Record updated");
      } else {
        await realApi.createSalaryAdvance({
          workerId: form.workerId,
          amount,
          recordType: form.recordType,
          openingRecoveredAmount: openingRecovered,
          installmentAmount: installment,
          currency: form.currency || "ZMW",
          issueDate: form.issueDate || null,
          deductionStartDate: form.deductionStartDate,
          deductFromPayslip: form.deductFromPayslip,
          reason: form.reason || null,
          reference: form.reference || null,
        });
        toast.success("Record created");
      }
      setShowForm(false);
      setEditing(null);
      setForm(emptyForm());
      advances.reload();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Salary advance could not be saved");
    } finally {
      setBusy(false);
    }
  }

  async function cancelAdvance(row: Row) {
    const reason = text(cancelReason[text(row.id)]).trim();
    if (!reason) {
      toast.error("Enter a reason before cancelling an advance.");
      return;
    }
    setBusy(true);
    try {
      await realApi.cancelSalaryAdvance(text(row.id), reason);
      toast.success("Salary advance cancelled");
      setCancelReason((current) => ({ ...current, [text(row.id)]: "" }));
      advances.reload();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Salary advance could not be cancelled");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Payroll & benefits"
          title="Loans and salary advances"
          description="Record original amounts, prior repayments and the balance to recover from future payslips."
          meta={<ScopeBadge />}
          primaryAction={
            <Button onClick={startCreate}>
              <Plus className="mr-2 size-4" aria-hidden />
              Add loan or advance
            </Button>
          }
        />

        <div className="space-y-6">
          <section className="grid gap-3 md:grid-cols-3">
            <Card className="shadow-none">
              <CardContent className="p-4">
                <p className="text-xs text-muted-foreground">Records shown</p>
                <p className="mt-1 text-2xl font-semibold">{totals.count}</p>
              </CardContent>
            </Card>
            <Card className="shadow-none">
              <CardContent className="p-4">
                <p className="text-xs text-muted-foreground">Active records</p>
                <p className="mt-1 text-2xl font-semibold">{totals.active}</p>
              </CardContent>
            </Card>
            <Card className="shadow-none">
              <CardContent className="p-4">
                <p className="text-xs text-muted-foreground">Active remaining balance</p>
                <p className="mt-1 text-2xl font-semibold">{money(totals.remaining)}</p>
              </CardContent>
            </Card>
          </section>

          {showForm ? (
            <Card className="shadow-none">
              <CardHeader>
                <CardTitle className="text-base">{editing ? "Edit loan or advance" : "Add loan or advance"}</CardTitle>
                <CardDescription>
                  {editing
                    ? "You can change future deduction settings while this record is active."
                    : "For an existing loan, enter its original amount and repayments made before payroll starts collecting it here."}
                </CardDescription>
              </CardHeader>
              <CardContent className="grid gap-4 md:grid-cols-2">
                <Field label="Employee">
                  <Select
                    value={form.workerId}
                    onValueChange={(workerId) => setForm((current) => ({ ...current, workerId }))}
                    disabled={Boolean(editing)}
                  >
                    <SelectTrigger><SelectValue placeholder="Select employee" /></SelectTrigger>
                    <SelectContent>
                      {employeeRows.map((row) => (
                        <SelectItem key={text(row.id)} value={text(row.id)}>
                          {employeeLabel(row)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="Currency">
                  <Input value={form.currency} onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))} disabled={Boolean(editing)} />
                </Field>
                <Field label="Type">
                  <Select value={form.recordType} onValueChange={(recordType) => setForm((current) => ({ ...current, recordType, issueDate: recordType === "loan" ? "" : current.issueDate || today }))} disabled={Boolean(editing)}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent><SelectItem value="salary-advance">Salary advance</SelectItem><SelectItem value="loan">Loan</SelectItem></SelectContent>
                  </Select>
                </Field>
                <Field label="Original amount">
                  <Input type="number" min="0" step="0.01" value={form.amount} onChange={(event) => setForm((current) => ({ ...current, amount: event.target.value }))} disabled={Boolean(editing)} />
                </Field>
                <Field label="Recovered before tracking here">
                  <Input type="number" min="0" step="0.01" value={form.openingRecoveredAmount} onChange={(event) => setForm((current) => ({ ...current, openingRecoveredAmount: event.target.value }))} disabled={Boolean(editing)} />
                </Field>
                <Field label="Amount deduction per payslip">
                  <Input type="number" min="0" step="0.01" value={form.installmentAmount} onChange={(event) => setForm((current) => ({ ...current, installmentAmount: event.target.value }))} />
                </Field>
                <Field label={form.recordType === "loan" ? "Issue date (if known)" : "Issue date"}>
                  <Input type="date" value={form.issueDate} onChange={(event) => setForm((current) => ({ ...current, issueDate: event.target.value }))} disabled={Boolean(editing)} />
                </Field>
                <Field label="Start deducting from">
                  <Input type="date" value={form.deductionStartDate} onChange={(event) => setForm((current) => ({ ...current, deductionStartDate: event.target.value }))} />
                </Field>
                <Field label="Reference">
                  <Input value={form.reference} onChange={(event) => setForm((current) => ({ ...current, reference: event.target.value }))} />
                </Field>
                <label className="flex items-center gap-3 rounded-lg border p-3 text-sm">
                  <Checkbox
                    checked={form.deductFromPayslip}
                    onCheckedChange={(checked) => setForm((current) => ({ ...current, deductFromPayslip: Boolean(checked) }))}
                  />
                  Deduct this record from payslip
                </label>
                <div className="md:col-span-2">
                  <Label>Reason or notes</Label>
                  <Textarea className="mt-2" value={form.reason} onChange={(event) => setForm((current) => ({ ...current, reason: event.target.value }))} />
                </div>
                <div className="flex gap-2 md:col-span-2">
                  <Button onClick={save} disabled={busy}>{editing ? "Save changes" : "Create record"}</Button>
                  <Button variant="outline" onClick={() => { setShowForm(false); setEditing(null); }} disabled={busy}>Cancel</Button>
                </div>
              </CardContent>
            </Card>
          ) : null}

          <Card className="shadow-none">
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <span>
                <CardTitle className="flex items-center gap-2 text-base">
                  <WalletCards className="size-4" aria-hidden />
                  Loan and advance list
                </CardTitle>
                <CardDescription>Payroll deducts only active records where payslip deduction is enabled.</CardDescription>
              </span>
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger className="w-full sm:w-44"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="active">Active</SelectItem>
                  <SelectItem value="cancelled">Cancelled</SelectItem>
                  <SelectItem value="settled">Settled</SelectItem>
                  <SelectItem value="all">All</SelectItem>
                </SelectContent>
              </Select>
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto rounded-lg border">
                <Table className="min-w-[1050px]">
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Original</TableHead>
                      <TableHead>Recovered</TableHead>
                      <TableHead>Remaining</TableHead>
                      <TableHead>Deduction</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Reference</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {advances.loading ? (
                      <TableRow><TableCell colSpan={9} className="py-10 text-center text-muted-foreground">Loading records...</TableCell></TableRow>
                    ) : rows.length === 0 ? (
                      <TableRow><TableCell colSpan={9} className="py-10 text-center text-muted-foreground">No loans or salary advances found.</TableCell></TableRow>
                    ) : rows.map((row) => {
                      const active = text(row.status) === "active";
                      return (
                        <TableRow key={text(row.id)}>
                          <TableCell>
                            <div className="font-medium">{text(row.workerName)}</div>
                            <div className="text-xs text-muted-foreground">{text(row.employeeNo)}</div>
                          </TableCell>
                          <TableCell>{text(row.recordType) === "loan" ? "Loan" : "Salary advance"}</TableCell>
                          <TableCell>{money(row.amount, text(row.currency) || "ZMW")}</TableCell>
                          <TableCell>{money(row.recoveredAmount, text(row.currency) || "ZMW")}</TableCell>
                          <TableCell>{money(row.remainingAmount, text(row.currency) || "ZMW")}</TableCell>
                          <TableCell>
                            <div>{money(row.installmentAmount, text(row.currency) || "ZMW")}</div>
                            <div className="text-xs text-muted-foreground">{row.deductFromPayslip ? "Payslip deduction on" : "Payslip deduction off"}</div>
                          </TableCell>
                          <TableCell>
                            <Badge variant={active ? "default" : "outline"}>{text(row.status)}</Badge>
                          </TableCell>
                          <TableCell>
                            <div>{text(row.reference) || "-"}</div>
                            {row.issueDate ? <div className="text-xs text-muted-foreground">Issued {text(row.issueDate)}</div> : null}
                            {row.reason ? <div className="max-w-72 text-xs text-muted-foreground" title={text(row.reason)}>{text(row.reason)}</div> : null}
                          </TableCell>
                          <TableCell className="space-y-2 text-right">
                            <Button variant="outline" size="sm" onClick={() => startEdit(row)} disabled={!active || busy}>
                              <Edit className="mr-1 size-3.5" aria-hidden />
                              Edit
                            </Button>
                            {active ? (
                              <div className="flex justify-end gap-2">
                                <Input
                                  className="h-8 w-44 text-xs"
                                  placeholder="Cancel reason"
                                  value={cancelReason[text(row.id)] ?? ""}
                                  onChange={(event) => setCancelReason((current) => ({ ...current, [text(row.id)]: event.target.value }))}
                                />
                                <Button variant="outline" size="sm" onClick={() => cancelAdvance(row)} disabled={busy}>
                                  <RotateCcw className="mr-1 size-3.5" aria-hidden />
                                  Cancel
                                </Button>
                              </div>
                            ) : null}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Label>{label}</Label>
      <div className="mt-2">{children}</div>
    </div>
  );
}
