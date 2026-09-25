import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Download,
  Eye,
  FileText,
  KeyRound,
  Link2,
  Printer,
  Unlink,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { adaptWorkerProfile, adaptWorkers, realApi, useApi } from "@/platform/use-api";
import { entities } from "@/mock/data";
import { employeeProfileApi } from "@/mock/employeeprofile";
import { balanceFor } from "@/mock/leavebalance";
import { LeaveBalancePanel } from "@/platform/components/LeaveBalancePanel";
import type { EmployeeProfile } from "@/mock/employeeprofile";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { MaskedValue } from "@/platform/components/Sensitive";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Field,
  FieldGrid,
  SubRecordCard,
  SubRecords,
  YesNo,
} from "@/platform/components/ProfileFields";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";
import { feedback } from "@/platform/feedback";
import { useRoleGate } from "@/platform/app-context";
import type { Role } from "@/mock/types";

// Managers, HR operations, HR administrators and payroll officers can act on
// a person's record (same approver audience as the Approvals surface).
const APPROVER_ROLES: Role[] = ["manager", "hr_ops", "hr_admin", "payroll"];

export const Route = createFileRoute("/hrm/employees/$id")({
  head: () => ({
    meta: [
      { title: "Employee profile — HRM" },
      {
        name: "description",
        content: "Employment record: identity, contract, pay context, history and related records.",
      },
      { property: "og:title", content: "Employee profile — HRM" },
      {
        property: "og:description",
        content: "Employment record: identity, contract, pay context, history and related records.",
      },
    ],
  }),
  component: EmployeePage,
});

type EmployeeRecord = NonNullable<Awaited<ReturnType<typeof api.employee>>>;
type PayslipRecord = {
  id?: string;
  payslipNo?: string;
  periodLabel?: string;
  runId?: string;
  gross?: number;
  deductions?: number;
  net?: number;
  releasedAt?: string | null;
  payDate?: string | null;
};
type PreviewComponent = {
  code: string;
  label: string;
  kind: "Earning" | "Deduction" | "Employer";
  amount: number;
  explanation: string;
};
type PayslipPreview = {
  status: "ready" | "blocked";
  guardrails: string[];
  run?: {
    id: string;
    period: string;
    payGroup: string;
    currency: string;
    status: string;
  };
  line?: {
    id: string;
    gross: number;
    deductions: number;
    employerCost: number;
    net: number;
    components: PreviewComponent[];
    flags: string[];
  };
};

function text(value: unknown) {
  return value === null || value === undefined ? "" : String(value);
}

function escapeHtml(value: unknown) {
  return text(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/\"/g, "&quot;");
}

function mapPayslip(raw: unknown): PayslipRecord {
  const p = raw as Record<string, unknown>;
  return {
    id: text(p.id),
    payslipNo: text(p.payslipNo),
    periodLabel: text(p.periodLabel),
    runId: text(p.runId),
    gross: Number(p.grossPay ?? 0),
    deductions: Number(p.totalDeductions ?? 0),
    net: Number(p.netPay ?? 0),
    releasedAt: p.releasedAt ? text(p.releasedAt) : null,
    payDate: p.payDate ? text(p.payDate) : null,
  };
}

async function latestPayslipFor(workerId: string) {
  const page = await realApi.workerPayslips(workerId);
  const slips = (page.items ?? []).map(mapPayslip).filter((slip) => slip.id);
  return slips[0] ?? null;
}

function payslipLabel(slip: PayslipRecord) {
  return slip.periodLabel || slip.payslipNo || "last payslip";
}

function money(value: number, currency = "ZMW") {
  try {
    return new Intl.NumberFormat("en-ZM", {
      style: "currency",
      currency,
      minimumFractionDigits: 2,
    }).format(value);
  } catch {
    return `${currency} ${value.toFixed(2)}`;
  }
}

function rawText(raw: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const value = raw[key];
    if (value !== null && value !== undefined && String(value).trim()) return String(value);
  }
  return "";
}

function previewRun(raw: unknown) {
  const r = raw as Record<string, unknown>;
  return {
    id: rawText(r, "id"),
    period: rawText(r, "periodLabel", "period", "name"),
    payGroup: rawText(r, "payGroup", "payGroupName") || "Payroll run",
    currency: rawText(r, "currency") || "ZMW",
    status: (rawText(r, "status") || "draft").toLowerCase(),
    sortKey: rawText(
      r,
      "endDate",
      "cutoffDate",
      "postingDate",
      "createdAt",
      "updatedAt",
      "periodLabel",
    ),
  };
}

function previewLine(raw: unknown): NonNullable<PayslipPreview["line"]> {
  const l = raw as Record<string, unknown>;
  const components = ((l.components as Record<string, unknown>[] | undefined) ?? []).map((c) => {
    const componentType = rawText(c, "componentType", "type");
    return {
      code: rawText(c, "componentCode", "code"),
      label: rawText(c, "componentName", "name", "label") || "Payroll component",
      kind:
        componentType === "employer-contribution"
          ? "Employer"
          : componentType === "deduction" || componentType === "tax"
            ? "Deduction"
            : "Earning",
      amount: Number(c.amount ?? 0),
      explanation: rawText(c, "explanation", "basis"),
    } as PreviewComponent;
  });
  return {
    id: rawText(l, "id"),
    gross: Number(l.grossPay ?? l.gross ?? 0),
    deductions: Number(l.totalDeductions ?? l.deductions ?? 0),
    employerCost: Number(l.employerCost ?? 0),
    net: Number(l.netPay ?? l.net ?? 0),
    components,
    flags: l.hasException
      ? [rawText(l, "exceptionReason") || "Payroll exception needs review."]
      : [],
  };
}

async function latestPayslipPreviewFor(workerId: string): Promise<PayslipPreview> {
  // A historical payslip is authoritative. Do not replace it with a fresh
  // simulation that may use a different work calendar or later configuration.
  const releasedSlip = await latestPayslipFor(workerId);
  if (releasedSlip?.runId) {
    const rawLines = await realApi.payrollRunLines(releasedSlip.runId);
    const lines = ((rawLines as { items?: unknown[] }).items ?? []) as Record<string, unknown>[];
    const rawLine = lines.find((line) => rawText(line, "workerId", "employeeId") === workerId);
    if (rawLine) {
      const runs = (await realApi.payrollRuns()).items.map(previewRun);
      const run = runs.find((item) => item.id === releasedSlip.runId);
      return {
        status: "ready",
        guardrails: ["Showing the latest released or corrected payslip. A new simulation is not used for this historical period."],
        run: {
          id: releasedSlip.runId,
          period: releasedSlip.periodLabel || run?.period || "Released pay period",
          payGroup: run?.payGroup ?? "Payroll run",
          currency: run?.currency ?? "ZMW",
          status: "released",
        },
        line: previewLine(rawLine),
      };
    }
  }

  try {
    const rawPreview = (await realApi.workerPayslipPreview(workerId)) as Record<string, unknown>;
    const rawLine = rawPreview.line as Record<string, unknown> | undefined;
    const guardrails = Array.isArray(rawPreview.guardrails)
      ? rawPreview.guardrails.map(String).filter(Boolean)
      : [];
    const line = rawLine ? previewLine(rawLine) : undefined;
    if (line && !line.components.length)
      guardrails.push(
        "The payroll preview was calculated, but no component breakdown was returned by the engine.",
      );
    return {
      status: rawText(rawPreview, "status") === "ready" && guardrails.length === 0 ? "ready" : "blocked",
      guardrails,
      run: {
        id: "current-preview",
        period: rawText(rawPreview, "periodLabel") || "Current pay period",
        payGroup: "",
        currency: rawText(rawPreview, "currency") || "ZMW",
        status: "preview",
      },
      line,
    };
  } catch {
    // Older API deployments did not expose a simulation endpoint. Fall back to
    // the latest calculated line so the screen remains usable during rollout.
  }

  const runs = (await realApi.payrollRuns()).items
    .map(previewRun)
    .filter((run) => run.id)
    .sort((a, b) => b.sortKey.localeCompare(a.sortKey));
  const usableRuns = runs.filter(
    (run) => !["draft", "locked", "cancelled", "void", "reversed"].includes(run.status),
  );
  const searchRuns = usableRuns.length ? usableRuns : runs;
  const guardrails: string[] = [];

  if (!runs.length) {
    return {
      status: "blocked",
      guardrails: [
        "No payroll run exists yet. Create a payroll run, calculate it, then preview the employee's payslip.",
      ],
    };
  }

  for (const run of searchRuns) {
    const rawLines = await realApi.payrollRunLines(run.id);
    const lines = ((rawLines as { items?: unknown[] }).items ?? []) as Record<string, unknown>[];
    const rawLine = lines.find((line) => rawText(line, "workerId", "employeeId") === workerId);
    if (!rawLine) continue;

    const line = previewLine(rawLine);
    if (!line.components.length)
      guardrails.push(
        "The payroll line exists, but no component breakdown was returned by the engine.",
      );
    if (!line.components.some((c) => c.kind === "Earning"))
      guardrails.push("No earning component was calculated for this employee.");
    if (line.gross <= 0)
      guardrails.push(
        "Gross pay is zero. Check basic salary, earning components and salary profile setup.",
      );
    if (line.net <= 0)
      guardrails.push("Net pay is zero or negative. Review deductions before releasing a payslip.");
    if (Math.abs(line.gross - line.deductions - line.net) > 0.05) {
      guardrails.push(
        "Gross minus deductions does not match net pay. Recalculate the run or review payroll engine output.",
      );
    }
    line.flags.forEach((flag) => guardrails.push(flag));

    return {
      status: guardrails.length ? "blocked" : "ready",
      guardrails,
      run,
      line,
    };
  }

  return {
    status: "blocked",
    guardrails: [
      "No calculated payroll line was found for this employee.",
      "Confirm the employee has an active payroll profile, belongs to the selected pay group and branch scope, then calculate the run again.",
    ],
  };
}

function PayslipPreviewDialog({
  employee,
  profile,
  open,
  onOpenChange,
}: {
  employee: EmployeeRecord;
  profile: EmployeeProfile | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const state = useApi(
    () =>
      open && USE_REAL
        ? latestPayslipPreviewFor(employee.id)
        : Promise.resolve({
            status: "blocked",
            guardrails: ["Payslip preview is available in the live HRMS."],
          } as PayslipPreview),
    [open, employee.id],
  );
  const preview = state.data;
  const line = preview?.line;
  const currency = preview?.run?.currency ?? "ZMW";
  const earnings = line?.components.filter((component) => component.kind === "Earning") ?? [];
  const deductions = line?.components.filter((component) => component.kind === "Deduction") ?? [];
  const employer = line?.components.filter((component) => component.kind === "Employer") ?? [];
  const profileWarnings: string[] = [
    !profile?.paymentMethod ? "Payment method is not recorded on the employee profile." : "",
    profile?.paymentMethod === "Bank" && !profile?.bankAccount
      ? "Bank account is not recorded."
      : "",
    profile?.paymentMethod === "Mobile money" && !profile?.mobileMoneyNumber
      ? "Mobile money number is not recorded."
      : "",
  ].filter((item): item is string => Boolean(item));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileText className="size-5 text-primary" aria-hidden />
            Payslip preview for {employee.fullName}
          </DialogTitle>
          <DialogDescription>
            {preview?.run?.status === "preview"
              ? "No released payslip exists for this employee, so this is a current payroll simulation."
              : "Showing the complete latest released or corrected payslip."}
          </DialogDescription>
        </DialogHeader>

        {state.loading ? (
          <div className="rounded-md border bg-surface p-6 text-sm text-muted-foreground">
            Checking payroll output...
          </div>
        ) : state.error ? (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
            {state.error}
          </div>
        ) : (
          <div className="space-y-5">
            <div className="grid gap-3 md:grid-cols-4">
              <div className="rounded-md border bg-surface p-3">
                <div className="text-xs text-muted-foreground">Period</div>
                <div className="mt-1 font-semibold">{preview?.run?.period || "Not calculated"}</div>
              </div>
              <div className="rounded-md border bg-surface p-3">
                <div className="text-xs text-muted-foreground">Gross</div>
                <div className="mt-1 font-semibold">{line ? money(line.gross, currency) : "—"}</div>
              </div>
              <div className="rounded-md border bg-surface p-3">
                <div className="text-xs text-muted-foreground">Deductions</div>
                <div className="mt-1 font-semibold">
                  {line ? money(line.deductions, currency) : "—"}
                </div>
              </div>
              <div className="rounded-md border bg-surface p-3">
                <div className="text-xs text-muted-foreground">Net pay</div>
                <div className="mt-1 font-semibold">{line ? money(line.net, currency) : "—"}</div>
              </div>
            </div>

            {preview?.guardrails.length || profileWarnings.length ? (
              <div className="rounded-md border border-warning/40 bg-warning/10 p-4">
                <div className="flex items-center gap-2 font-semibold text-warning">
                  <AlertTriangle className="size-4" aria-hidden />
                  Guard rails
                </div>
                <ul className="mt-2 list-inside list-disc space-y-1 text-sm">
                  {[...(preview?.guardrails ?? []), ...profileWarnings].map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : (
              <div className="flex items-center gap-2 rounded-md border border-success/30 bg-success/10 p-4 text-sm text-success">
                <CheckCircle2 className="size-4" aria-hidden />
                Payroll line is calculated and ready for approval/release checks.
              </div>
            )}

            {line ? (
              <>
                <div className="grid gap-4 md:grid-cols-3">
                  <ComponentList title="Earnings" items={earnings} currency={currency} />
                  <ComponentList title="Deductions" items={deductions} currency={currency} />
                  <ComponentList title="Employer cost" items={employer} currency={currency} />
                </div>
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border bg-surface p-3 text-sm">
                  <span>
                    Run status: <strong>{preview?.run?.status.replaceAll("-", " ")}</strong> ·{" "}
                    {preview?.run?.payGroup}
                  </span>
                  {preview?.run?.id ? (
                    <Button variant="outline" size="sm" asChild>
                      <Link to="/hrm/payroll/runs/$id" params={{ id: preview.run.id }}>
                        Open payroll run
                      </Link>
                    </Button>
                  ) : null}
                </div>
              </>
            ) : null}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

function ComponentList({
  title,
  items,
  currency,
}: {
  title: string;
  items: PreviewComponent[];
  currency: string;
}) {
  return (
    <div className="rounded-md border bg-surface">
      <div className="border-b px-3 py-2 text-sm font-semibold">{title}</div>
      {items.length ? (
        <div className="divide-y">
          {items.map((item) => (
            <div key={`${item.kind}-${item.code}-${item.label}`} className="p-3 text-sm">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="font-medium">{item.label}</div>
                  <div className="text-xs text-muted-foreground">{item.code || "No code"}</div>
                </div>
                <div className="whitespace-nowrap font-semibold">
                  {money(item.amount, currency)}
                </div>
              </div>
              {item.explanation ? (
                <div className="mt-1 text-xs text-muted-foreground">{item.explanation}</div>
              ) : null}
            </div>
          ))}
        </div>
      ) : (
        <div className="p-3 text-sm text-muted-foreground">No components calculated.</div>
      )}
    </div>
  );
}

type EmployeeReportKind =
  | "master"
  | "profile"
  | "payment"
  | "salary-history"
  | "payroll"
  | "payslips"
  | "benefits"
  | "attendance"
  | "overtime"
  | "leave"
  | "advances"
  | "documents";

type EmployeeReport = {
  title: string;
  description: string;
  columns: string[];
  rows: string[][];
  empty: string;
};

const employeeReports: Array<{ value: EmployeeReportKind; label: string }> = [
  { value: "master", label: "Employee master and profile" },
  { value: "profile", label: "Employee employment and contract" },
  { value: "payment", label: "Employee payment details" },
  { value: "salary-history", label: "Employee salary history" },
  { value: "payroll", label: "Payroll and payment history" },
  { value: "payslips", label: "Payslip report" },
  { value: "benefits", label: "Benefits and claims" },
  { value: "attendance", label: "Attendance report" },
  { value: "overtime", label: "Overtime report" },
  { value: "leave", label: "Leave and leave balances" },
  { value: "advances", label: "Salary advances" },
  { value: "documents", label: "Employee documents" },
];

function date(value: unknown) {
  const raw = text(value);
  return raw ? raw.replace("T", " ").replace("Z", "") : "—";
}

function dataRows(value: unknown): Record<string, unknown>[] {
  if (Array.isArray(value)) return value as Record<string, unknown>[];
  const object = (value ?? {}) as Record<string, unknown>;
  return Array.isArray(object.items) ? (object.items as Record<string, unknown>[]) : [];
}

function downloadReportCsv(employee: EmployeeRecord, report: EmployeeReport) {
  const escape = (value: string) => `"${value.replaceAll('"', '""')}"`;
  const csv = [report.columns, ...report.rows].map((row) => row.map(escape).join(",")).join("\r\n");
  const url = URL.createObjectURL(new Blob([`\ufeff${csv}`], { type: "text/csv;charset=utf-8" }));
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `${employee.employeeNo}-${report.title.toLowerCase().replaceAll(/[^a-z0-9]+/g, "-").replaceAll(/(^-|-$)/g, "")}.csv`;
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

function escapeReportHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

/** Opens the browser's native print dialog, where the user can save a genuine
 * PDF. Keeping this rendering client-side means it exports the exact live
 * report currently displayed, including reports assembled from multiple HRM
 * resources, without a second data-export path to keep in sync. */
function exportReportPdf(employee: EmployeeRecord, report: EmployeeReport) {
  const popup = window.open("", "_blank");
  if (!popup) {
    feedback.blocked("PDF export was blocked", "Allow pop-ups for this site, then try again.");
    return;
  }
  const rows = report.rows.length
    ? report.rows.map((row) => `<tr>${row.map((cell) => `<td>${escapeReportHtml(cell || "—")}</td>`).join("")}</tr>`).join("")
    : `<tr><td colspan="${report.columns.length}">${escapeReportHtml(report.empty)}</td></tr>`;
  popup.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escapeReportHtml(employee.employeeNo)} — ${escapeReportHtml(report.title)}</title><style>
    @page { size: A4; margin: 15mm; } body { color:#172033; font: 11px/1.45 Arial,sans-serif; } h1 { font-size:20px; margin:0 0 4px; } p { color:#56627a; margin:0 0 16px; } table { width:100%; border-collapse:collapse; } th { background:#eef2f8; text-align:left; } th,td { border:1px solid #cbd5e1; padding:7px; vertical-align:top; } @media print { body { -webkit-print-color-adjust:exact; print-color-adjust:exact; } }
  </style></head><body><h1>${escapeReportHtml(report.title)}</h1><p>${escapeReportHtml(employee.fullName)} · ${escapeReportHtml(employee.employeeNo)}<br>${escapeReportHtml(report.description)}</p><table><thead><tr>${report.columns.map((column) => `<th>${escapeReportHtml(column)}</th>`).join("")}</tr></thead><tbody>${rows}</tbody></table></body></html>`);
  popup.document.close();
  popup.focus();
  window.setTimeout(() => popup.print(), 250);
}

function ReportTable({ report }: { report: EmployeeReport }) {
  return (
    <DetailSection title={report.title} description={report.description}>
      {report.rows.length ? (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full min-w-[640px] text-sm">
            <thead className="border-b bg-muted/40 text-left text-xs text-muted-foreground">
              <tr>
                {report.columns.map((column) => <th key={column} className="px-3 py-2 font-medium">{column}</th>)}
              </tr>
            </thead>
            <tbody className="divide-y">
              {report.rows.map((row, index) => (
                <tr key={`${row.join("-")}-${index}`}>
                  {row.map((value, cell) => <td key={`${cell}-${value}`} className="px-3 py-2 align-top">{value || "—"}</td>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{report.empty}</p>
      )}
    </DetailSection>
  );
}

function EmployeeReportsTab({ employee, profile }: { employee: EmployeeRecord; profile: EmployeeProfile }) {
  const [selected, setSelected] = useState<EmployeeReportKind>("master");
  const state = useApi(async (): Promise<EmployeeReport> => {
    const master: EmployeeReport = {
      title: "Employee master and profile",
      description: "Core identity, employment, branch and payment details for this employee.",
      columns: ["Employee number", "Employee", "Department", "Position", "Branch", "Payment method", "Status", "Joined"],
      rows: [[employee.employeeNo, employee.fullName, employee.department, employee.jobTitle, employee.branch, profile.paymentMethod, employee.status, employee.startDate]],
      empty: "No employee master data is available.",
    };
    if (!USE_REAL || selected === "master") return master;

    if (selected === "profile") {
      return {
        title: "Employee employment and contract",
        description: "Employment, position, reporting and contract facts for this employee.",
        columns: ["Employee", "Employment type", "Department", "Position", "Grade", "Reports to", "Start date", "End date", "Status"],
        rows: [[employee.fullName, employee.employmentType, employee.department, employee.jobTitle, employee.grade, profile.reportsTo, employee.startDate, employee.endDate ?? "", employee.status]],
        empty: "No employment profile is available.",
      };
    }

    if (selected === "payment") {
      return {
        title: "Employee payment details",
        description: "Recorded payment method and destination details. Sensitive account numbers stay on the protected Pay and statutory tab.",
        columns: ["Employee", "Payment method", "Account holder", "Bank", "Bank branch", "Mobile money"],
        rows: [[employee.fullName, profile.paymentMethod, profile.accountName ?? "", profile.bankName, profile.bankBranch, profile.mobileMoneyNumber ?? ""]],
        empty: "No payment details are recorded for this employee.",
      };
    }

    if (selected === "salary-history") {
      const profiles = dataRows(await realApi.payrollProfiles({ workerId: employee.id }));
      return {
        title: "Employee salary history",
        description: "Effective payroll profiles and their active pay basis. Use payroll and payslip history to see calculated amounts for each period.",
        columns: ["Effective from", "Effective to", "Pay group", "Pay basis", "Overtime policy", "Status"],
        rows: profiles.map((row) => [text(row.effectiveFrom), text(row.effectiveTo), text(row.payGroupName ?? row.payGroup), text(row.payBasis), text(row.overtimeCategory), text(row.isActive) === "false" ? "Inactive" : "Active"]),
        empty: "No salary profile is recorded for this employee.",
      };
    }

    if (selected === "payroll" || selected === "payslips") {
      const payslips = dataRows(await realApi.workerPayslips(employee.id));
      return {
        title: selected === "payroll" ? "Payroll and payment history" : "Payslip report",
        description: selected === "payroll"
          ? "Each generated payroll result for this employee, including gross pay, deductions, net pay and payment status."
          : "Payslips generated for this employee by payroll period.",
        columns: ["Period", "Gross pay", "Deductions", "Net pay", "Payment date", "Status"],
        rows: payslips.map((row) => [
          text(row.periodLabel) || text(row.payslipNo), money(Number(row.grossPay ?? 0), text(row.currency) || "ZMW"),
          money(Number(row.totalDeductions ?? 0), text(row.currency) || "ZMW"), money(Number(row.netPay ?? 0), text(row.currency) || "ZMW"),
          date(row.payDate ?? row.releasedAt), text(row.status),
        ]),
        empty: selected === "payroll" ? "No payroll payment has been generated for this employee." : "No payslips have been generated for this employee.",
      };
    }

    if (selected === "benefits") {
      const [allowances, claims] = await Promise.all([
        realApi.benefitAllowances({ workerId: employee.id }),
        realApi.benefitClaims({ workerId: employee.id, pageSize: 100 }),
      ]);
      const rows = [
        ...dataRows(allowances).map((row) => ["Allowance", text(row.benefitTypeName), money(Number(row.annualAmount ?? 0)), text(row.year), "Assigned"]),
        ...dataRows(claims).map((row) => ["Claim", text(row.benefitTypeName), money(Number(row.approvedAmount ?? row.amountClaimed ?? 0), text(row.currency) || "ZMW"), date(row.createdAt), text(row.status)]),
      ];
      return {
        title: "Benefits and claims",
        description: "Benefit allowances and employee claims, including their current decision or payment status.",
        columns: ["Record", "Benefit", "Amount", "Year or date", "Status"], rows,
        empty: "No benefit allowances or claims are recorded for this employee.",
      };
    }

    if (selected === "attendance") {
      const records = dataRows(await realApi.attendanceHistory(employee.id));
      return {
        title: "Attendance report", description: "Clock-in/out records, attendance status and recorded working time.",
        columns: ["Date", "Clock in", "Clock out", "Status", "Hours", "Source"],
        rows: records.map((row) => [text(row.workDate), date(row.clockIn), date(row.clockOut), text(row.derivedStatus), text(row.totalHours), text(row.source)]),
        empty: "No attendance records are available for this employee.",
      };
    }

    if (selected === "overtime") {
      const records = dataRows(await realApi.overtime({ workerId: employee.id }));
      return {
        title: "Overtime report", description: "Recorded and approved overtime. Only approved overtime is included in payroll.",
        columns: ["Work date", "Regular hours", "Overtime hours", "Multiplier", "Status", "Payroll"],
        rows: records.map((row) => [text(row.workDate), text(row.regularHours), text(row.overtimeHours), text(row.overtimeMultiplier), text(row.overtimeStatus), text(row.overtimePayrollRunId) ? "Included" : "Not included"]),
        empty: "No overtime records are available for this employee.",
      };
    }

    if (selected === "leave") {
      const [requests, balances] = await Promise.all([
        realApi.leaveRequests({ workerId: employee.id }), realApi.leaveBalances(employee.id),
      ]);
      const rows = [
        ...dataRows(requests).map((row) => ["Leave request", text(row.leaveTypeCode), `${text(row.startDate)} to ${text(row.endDate)}`, text(row.requestedDays), text(row.status)]),
        ...dataRows(balances).map((row) => ["Leave balance", text(row.leaveTypeName ?? row.leaveTypeCode), "", text(row.available), "Available days"]),
      ];
      return {
        title: "Leave and leave balances", description: "Leave requested or taken, together with the current available balance by leave type.",
        columns: ["Record", "Leave type", "Period", "Days", "Status"], rows,
        empty: "No leave requests or balances are available for this employee.",
      };
    }

    if (selected === "documents") {
      const documents = dataRows(await realApi.workerDocuments(employee.id));
      return {
        title: "Employee documents", description: "Documents held against this employee record, including their category and current status.",
        columns: ["Document", "Category", "Issued", "Expires", "Status"],
        rows: documents.map((row) => [text(row.title ?? row.fileName ?? row.name), text(row.category), date(row.issueDate ?? row.createdAt), date(row.expiryDate), text(row.status)]),
        empty: "No employee documents are recorded.",
      };
    }

    const advances = dataRows(await realApi.salaryAdvances({ workerId: employee.id }));
    return {
      title: "Salary advances", description: "Advances issued to this employee, recoveries through payroll and remaining balances.",
      columns: ["Issued", "Amount", "Deduction per payslip", "Recovered", "Remaining", "Status"],
      rows: advances.map((row) => [text(row.issueDate), money(Number(row.amount ?? 0), text(row.currency) || "ZMW"), money(Number(row.installmentAmount ?? 0), text(row.currency) || "ZMW"), money(Number(row.recoveredAmount ?? 0), text(row.currency) || "ZMW"), money(Number(row.remainingAmount ?? 0), text(row.currency) || "ZMW"), text(row.status)]),
      empty: "No salary advances are recorded for this employee.",
    };
  }, [employee.id, employee.employeeNo, employee.fullName, employee.department, employee.jobTitle, employee.branch, employee.employmentType, employee.status, employee.startDate, selected, profile.paymentMethod]);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="w-full max-w-md">
        <label className="text-sm font-medium" htmlFor="employee-report">Report</label>
        <Select value={selected} onValueChange={(value) => setSelected(value as EmployeeReportKind)}>
          <SelectTrigger id="employee-report" className="mt-1.5"><SelectValue /></SelectTrigger>
          <SelectContent>{employeeReports.map((report) => <SelectItem key={report.value} value={report.value}>{report.label}</SelectItem>)}</SelectContent>
        </Select>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            variant="outline"
            disabled={!state.data || state.loading}
            onClick={() => state.data && downloadReportCsv(employee, state.data)}
          >
            <Download className="size-4" aria-hidden /> Download CSV
          </Button>
          <Button
            variant="outline"
            data-testid="export-employee-report-pdf"
            disabled={!state.data || state.loading}
            onClick={() => state.data && exportReportPdf(employee, state.data)}
          >
            <Printer className="size-4" aria-hidden /> Export PDF
          </Button>
        </div>
      </div>
      <Async state={state} rows={5}>{(report) => <ReportTable report={report} />}</Async>
    </div>
  );
}

/**
 * The whole record, grouped so someone can find one fact quickly.
 *
 * Tabs rather than one long page: an HR administrator opening a profile is
 * usually after a single thing — a phone number, a NAPSA number, who to ring
 * in an emergency — and should not have to scroll past everything else.
 */
function ProfileTabs({
  employee: e,
  profile: p,
}: {
  employee: EmployeeRecord;
  profile: EmployeeProfile;
}) {
  const leave = balanceFor(e.id);

  return (
    <Tabs defaultValue="personal" className="w-full">
      <div className="overflow-x-auto">
        <TabsList>
          <TabsTrigger value="personal">Personal</TabsTrigger>
          <TabsTrigger value="contact">Contact and next of kin</TabsTrigger>
          <TabsTrigger value="employment">Employment terms</TabsTrigger>
          <TabsTrigger value="pay">Pay and statutory</TabsTrigger>
          <TabsTrigger value="history">Background</TabsTrigger>
          <TabsTrigger value="reports">Reports</TabsTrigger>
          {p.exit ? <TabsTrigger value="exit">Leaving</TabsTrigger> : null}
        </TabsList>
      </div>

      {/* ---------------------------------------------------------------- */}
      <TabsContent value="personal" className="mt-4 space-y-4">
        <DetailSection
          title="Identity"
          description="As it appears on the NRC. Payroll and the bank both check the legal name."
        >
          <FieldGrid>
            <Field label="Salutation" value={p.salutation} />
            <Field label="Full legal name" value={e.fullName} />
            <Field label="Preferred name" value={e.preferredName} />
            <Field label="Gender" value={p.gender} />
            <Field label="Marital status" value={p.maritalStatus} />
            <Field label="Nationality" value={p.nationality} />
            <Field label="Home town" value={p.homeTown} />
          </FieldGrid>
        </DetailSection>

        <DetailSection
          title="Restricted details"
          description="Masked by default. Revealing a value is a deliberate, recorded action."
        >
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <MaskedValue label="NRC number" value={e.nationalId} />
            <MaskedValue label="Date of birth" value={p.dateOfBirth} />
            {p.passportNo ? <MaskedValue label="Passport number" value={p.passportNo} /> : null}
          </dl>
          <FieldGrid>
            <Field label="Passport expires" value={p.passportExpiry} />
            <Field label="Blood group" value={p.bloodGroup} hint="Held for emergencies only." />
          </FieldGrid>
        </DetailSection>

        {p.workplaceAdjustments || p.dietaryRequirements ? (
          <DetailSection
            title="Support and adjustments"
            description="Recorded so the workplace can be arranged properly, and visible only to those who need it."
          >
            <FieldGrid>
              <Field label="Workplace adjustments" value={p.workplaceAdjustments} wide />
              <Field label="Dietary requirements" value={p.dietaryRequirements} />
            </FieldGrid>
          </DetailSection>
        ) : null}
      </TabsContent>

      <TabsContent value="reports" className="mt-4">
        <EmployeeReportsTab employee={e} profile={p} />
      </TabsContent>

      {/* ---------------------------------------------------------------- */}
      <TabsContent value="contact" className="mt-4 space-y-4">
        <DetailSection title="How to reach them">
          <FieldGrid>
            <Field label="Work email" value={e.email} />
            <Field label="Personal email" value={p.personalEmail} />
            <Field label="Mobile" value={e.phone} />
            <Field label="Alternate phone" value={p.alternatePhone} />
            <Field label="Residential address" value={p.residentialAddress} wide />
            <Field label="Postal address" value={p.postalAddress} wide />
          </FieldGrid>
        </DetailSection>

        <DetailSection
          title="Next of kin"
          description="Who is called first if something happens at work. Kept current at every review."
        >
          <SubRecords
            items={p.emergency}
            empty="No emergency contact recorded. This is the one field that should never be blank."
            render={(c) => (
              <SubRecordCard
                title={
                  <>
                    {c.name}
                    {c.isPrimary ? (
                      <span className="ml-2 rounded-full border border-primary/40 bg-primary-soft px-2 py-0.5 text-[11px] font-normal text-primary">
                        Call first
                      </span>
                    ) : null}
                  </>
                }
                meta={c.phone}
              >
                {c.relationship}
              </SubRecordCard>
            )}
          />
        </DetailSection>
      </TabsContent>

      {/* ---------------------------------------------------------------- */}
      <TabsContent value="employment" className="mt-4 space-y-4">
        <DetailSection title="Contract">
          <FieldGrid>
            <Field label="Employment type" value={e.employmentType} />
            {p.legacyEmploymentType ? <Field label="Previous Frappe employment type" value={p.legacyEmploymentType} /> : null}
            <Field label="Start date" value={e.startDate} />
            <Field label="Probation ends" value={p.probationEndsOn} />
            <Field label="Confirmed on" value={p.confirmedOn} />
            <Field label="Notice period" value={p.noticePeriodDays ? `${p.noticePeriodDays} days` : ""} />
            <Field label="End date" value={e.endDate} />
          </FieldGrid>
        </DetailSection>

        <DetailSection title="Where they sit">
          <FieldGrid>
            <Field label="Job title" value={e.jobTitle} />
            <Field label="Department" value={e.department} />
            <Field label="Grade" value={e.grade} />
            <Field label="Reports to" value={p.reportsTo} />
            <Field label="Legal entity" value={p.legalEntityName || entities.find((x) => x.id === e.entityId)?.name} />
            <Field label="Branch" value={e.branch} />
            <Field label="Work location" value={e.location} />
            <Field
              label="Cost centre"
              value={p.costCentre}
              hint="Where this person's cost lands in the ledger."
            />
          </FieldGrid>
        </DetailSection>

        <DetailSection
          title="Time and attendance"
          description="These decide which days count as worked, and therefore what gets paid."
        >
          <FieldGrid>
            <Field label="Shift pattern" value={p.shiftPattern} />
            <Field label="Holiday calendar" value={p.holidayCalendar} />
            <Field label="Leave policy" value={p.leavePolicy} />
            <Field label="Attendance device ID" value={p.attendanceDeviceId} />
          </FieldGrid>
        </DetailSection>

        <DetailSection
          title="Leave balance"
          description="Derived from the policy, length of service and actual requests — not a stored number."
        >
          {leave ? (
            <LeaveBalancePanel balance={leave} />
          ) : (
            <p className="text-sm text-muted-foreground">No balance could be calculated.</p>
          )}
        </DetailSection>
      </TabsContent>

      {/* ---------------------------------------------------------------- */}
      <TabsContent value="pay" className="mt-4 space-y-4">
        <DetailSection
          title="How they are paid"
          description="Payroll reads these directly. A wrong account number is the most common cause of a failed payment."
        >
          <FieldGrid>
            <Field label="Pay group" value={p.payGroup} />
            <Field label="Payment method" value={p.paymentMethod} />
            <Field label="Account holder" value={p.accountName} />
            <Field label="Bank" value={p.bankName} />
            <Field label="Branch" value={p.bankBranch} />
            <Field label="Mobile money number" value={p.mobileMoneyNumber} />
          </FieldGrid>
          <dl className="mt-4 grid gap-4 sm:grid-cols-2">
            <MaskedValue label="Bank account" value={p.bankAccount} />
          </dl>
        </DetailSection>

        <DetailSection
          title="Statutory registrations"
          description="Zambian registrations. A missing NAPSA or NHIMA number stops the employee being included in a run."
        >
          <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <MaskedValue label="TPIN (ZRA)" value={p.tpin} />
            <MaskedValue label="NAPSA number" value={p.napsaNumber} />
            <MaskedValue label="NHIMA number" value={p.nhimaNumber} />
          </dl>
        </DetailSection>

        <DetailSection
          title="Dependants"
          description="Who is covered by the medical scheme, and who a bereavement or funeral benefit would apply to."
        >
          <SubRecords
            items={p.dependants}
            empty="No dependants recorded."
            render={(d) => (
              <SubRecordCard title={d.name} meta={`Born ${d.dateOfBirth}`}>
                {d.relationship} ·{" "}
                <YesNo
                  value={d.onMedicalScheme}
                  yes="On the medical scheme"
                  no="Not on the scheme"
                />
              </SubRecordCard>
            )}
          />
        </DetailSection>
      </TabsContent>

      {/* ---------------------------------------------------------------- */}
      <TabsContent value="history" className="mt-4 space-y-4">
        <DetailSection
          title="Education"
          description="Verified means the certificate was seen, not just claimed."
        >
          <SubRecords
            items={p.education}
            empty="No qualifications recorded."
            render={(ed) => (
              <SubRecordCard title={ed.qualification} meta={ed.completedYear}>
                {ed.institution} · {ed.field} ·{" "}
                <YesNo value={ed.verified} yes="Verified" no="Not verified" />
              </SubRecordCard>
            )}
          />
        </DetailSection>

        <DetailSection title="Previous employment">
          <SubRecords
            items={p.previousEmployment}
            empty="No prior employment recorded."
            render={(pe) => (
              <SubRecordCard
                title={`${pe.jobTitle} — ${pe.employer}`}
                meta={`${pe.from} to ${pe.to}`}
              >
                Left because: {pe.reasonForLeaving} ·{" "}
                <YesNo
                  value={pe.referenceChecked}
                  yes="Reference checked"
                  no="Reference not checked"
                />
              </SubRecordCard>
            )}
          />
        </DetailSection>
      </TabsContent>

      {/* ---------------------------------------------------------------- */}
      {p.exit ? (
        <TabsContent value="exit" className="mt-4 space-y-4">
          <DetailSection
            title="Leaving"
            description="Recorded before the last working day so final pay and clearance can be prepared."
          >
            <FieldGrid>
              <Field label="Last working day" value={p.exit.lastWorkingDay} />
              <Field label="Notice given on" value={p.exit.noticeGivenOn} />
              <Field label="Reason" value={p.exit.reason} wide />
              <Field
                label="Exit interview"
                value={<YesNo value={p.exit.interviewHeld} yes="Held" no="Not yet held" />}
              />
              <Field
                label="Eligible for rehire"
                value={<YesNo value={p.exit.eligibleForRehire} />}
              />
            </FieldGrid>
            {p.exit.note ? (
              <p className="mt-4 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
                {p.exit.note}
              </p>
            ) : null}
          </DetailSection>

          {leave?.encashment ? (
            <DetailSection
              title="Leave paid out"
              description="Untaken leave is a debt to the employee, so it is settled in the final pay run."
            >
              <LeaveBalancePanel balance={leave} />
            </DetailSection>
          ) : null}
        </TabsContent>
      ) : null}
    </Tabs>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

async function loadLiveProfile(id: string): Promise<EmployeeProfile | null> {
  let raw: Record<string, unknown>;
  try {
    raw = (await realApi.worker(id)) as Record<string, unknown>;
  } catch {
    const page = await realApi.employees();
    const match = page.items.find(
      (item) => String((item as Record<string, unknown>).employeeNo) === id,
    ) as Record<string, unknown> | undefined;
    if (!match) return null;
    raw = (await realApi.worker(String(match.id))) as Record<string, unknown>;
  }
  return adaptWorkerProfile(raw);
}

function EmployeePage() {
  const { id } = Route.useParams();
  const [confirmEnd, setConfirmEnd] = useState(false);
  // M27 P0 UX audit: HR admins can link a worker record to an authentication
  // identity from the profile page — the self-service surfaces (leave,
  // documents, letters, payslips) only work once a link exists.
  const [linkOpen, setLinkOpen] = useState(false);
  const [linkSubject, setLinkSubject] = useState("");
  const [linkBusy, setLinkBusy] = useState(false);
  const [subjectId, setSubjectId] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [payslipBusy, setPayslipBusy] = useState<"print" | null>(null);
  const hrAdmin = useRoleGate()(APPROVER_ROLES);
  useEffect(() => {
    setSubjectId(null);
  }, [id]);
  // Real backend: the employee list keeps the real worker GUID as the row id,
  // so detail/edit links resolve directly. Falls back to mock when off.
  const state = useApi(async () => {
    if (!USE_REAL) return api.employee(id);
    try {
      // Try the GUID directly first, then fall back to a list scan by employee
      // number (hand-typed URLs like /hrm/employees/SMK001).
      const direct = await realApi.worker(id);
      const w = adaptWorkers([direct])[0];
      if (w) {
        const d = direct as Record<string, unknown>;
        setSubjectId(typeof d.subjectId === "string" ? String(d.subjectId) : null);
        return w;
      }
    } catch {
      /* not a GUID — scan the list by employee number instead */
    }
    const workers = adaptWorkers(await realApi.employees());
    return workers.find((w) => w.employeeNo === id) ?? null;
  }, [id]);
  const profileState = useApi(
    () => (USE_REAL ? loadLiveProfile(id) : employeeProfileApi.profile(id)),
    [id],
  );
  const leaveSummary = USE_REAL ? null : balanceFor(id);

  const printLatestPayslip = async (workerId: string) => {
    if (!USE_REAL) {
      feedback.note("Payslip printing is available in the live HRMS.");
      return;
    }
    setPayslipBusy("print");
    try {
      const slip = await latestPayslipFor(workerId);
      if (!slip?.id) {
        feedback.blocked(
          "No payslip is ready to print.",
          "Payroll must release a payslip for this employee before it can be printed.",
        );
        return;
      }
      const blob = await realApi.payslipDownloadBlob(slip.id);
      const url = URL.createObjectURL(blob);
      const frame = document.createElement("iframe");
      frame.style.position = "fixed";
      frame.style.right = "0";
      frame.style.bottom = "0";
      frame.style.width = "0";
      frame.style.height = "0";
      frame.style.border = "0";
      frame.src = url;
      frame.onload = () => {
        frame.contentWindow?.focus();
        frame.contentWindow?.print();
        window.setTimeout(() => {
          URL.revokeObjectURL(url);
          frame.remove();
        }, 60_000);
      };
      document.body.appendChild(frame);
      feedback.note(`Print dialog opened for ${payslipLabel(slip)}.`);
    } catch (error) {
      feedback.blocked(
        "Payslip printing is blocked.",
        error instanceof Error
          ? error.message
          : "Check payroll permissions and release status, then try again.",
      );
    } finally {
      setPayslipBusy(null);
    }
  };

  const printCorrectionStatement = async (workerId: string, employeeNo: string, employeeName: string) => {
    const tab = window.open("", "_blank");
    if (!tab) {
      feedback.blocked("Correction statement is unavailable.", "Your browser blocked the print window. Allow popups for this site and try again.");
      return;
    }
    tab.document.write("<!doctype html><title>Preparing correction statement</title><p style='font-family:Arial;margin:32px'>Preparing correction statement...</p>");
    tab.document.close();
    setPayslipBusy("print");
    try {
      const slip = await latestPayslipFor(workerId);
      if (!slip?.runId) throw new Error("No released payslip is available for a correction statement.");
      const events = await realApi.payrollRunAudit(slip.runId) as Array<Record<string, unknown>>;
      const rows = events.map((event) => {
        try { return JSON.parse(text(event.detailsJson)) as Record<string, unknown>; } catch { return null; }
      }).filter((row): row is Record<string, unknown> => row?.employeeNo === employeeNo && Boolean(row.grossAdjustment ?? row.netAdjustmentPaid));
      if (!rows.length) throw new Error("No paid or pending payroll adjustment is recorded for this employee.");
      const paidRow = rows.find((row) => Number.isFinite(Number(row.actualPaidNetAmount)) && Number(row.actualPaidNetAmount) > 0);
      const currentNet = Number(slip.net ?? 0);
      const adjustment = rows.reduce((sum, row) => sum + Number(row.netAdjustmentPaid ?? row.grossAdjustment ?? 0), 0);
      const paymentRows = paidRow
        ? `<tr><td>Corrected payslip net pay</td><td class="r">${currentNet.toFixed(2)}</td></tr><tr><td>Actual payment already made</td><td class="r">${Number(paidRow.actualPaidNetAmount).toFixed(2)}</td></tr><tr><td>Current reconciliation difference</td><td class="r">${(Number(paidRow.actualPaidNetAmount) - currentNet).toFixed(2)}</td></tr>`
        : `<tr><td>Corrected payslip net pay</td><td class="r">${currentNet.toFixed(2)}</td></tr>${rows.map((row) => `<tr><td>${escapeHtml(row.component ?? "Payroll adjustment")}</td><td class="r">${Number(row.netAdjustmentPaid ?? row.grossAdjustment ?? 0).toFixed(2)}</td></tr>`).join("")}`;
      const displayedTotal = paidRow ? Number(paidRow.actualPaidNetAmount) : currentNet + adjustment;
      const html = `<!doctype html><html><head><title>Newworldcargo HRM payroll correction statement</title><style>@page{margin:16mm}body{font:13px Arial,sans-serif;color:#17212b;margin:0}.brand{background:#012642;color:#fff;padding:22px 24px;border-bottom:6px solid #ffcc04}.brand strong{font-size:22px;display:block}.brand span{font-size:11px;opacity:.88}.document{margin:24px}.document h1{font-size:19px;color:#012642;margin:0 0 4px}.muted{color:#59636d;font-size:11px}table{width:100%;border-collapse:collapse;margin:18px 0}td,th{border:1px solid #cfd6dc;padding:8px;text-align:left}th{background:#e8f0f5;color:#012642}.r{text-align:right}.total th{background:#012642;color:#fff;font-size:14px}.notice{border-left:4px solid #ffcc04;background:#fff9df;padding:9px 12px;font-size:11px}</style></head><body><header class="brand"><strong>Newworldcargo HRM</strong><span>Human Resources &middot; Payroll Correction Statement</span></header><main class="document"><h1>Payroll Correction Statement</h1><p class="muted">This document supplements, and does not replace, the released payslip.</p><table><tr><td>Employee</td><td>${escapeHtml(employeeName)}</td><td>Employee no.</td><td>${escapeHtml(employeeNo)}</td></tr><tr><td>Period</td><td>${escapeHtml(slip.periodLabel)}</td><td>Corrected payslip</td><td>${escapeHtml(slip.payslipNo)}</td></tr></table><table><tr><th>Description</th><th class="r">Amount (ZMW)</th></tr>${paymentRows}<tr class="total"><th>Actual payment total</th><th class="r">${displayedTotal.toFixed(2)}</th></tr></table><p class="notice">Generated from the HRM audit trail on ${new Date().toLocaleString()}. Statutory treatment remains recorded on the corrected payslip.</p></main><script>window.onload=()=>window.print()</script></body></html>`;
      tab.document.write(html); tab.document.close();
    } catch (error) {
      tab.close();
      feedback.blocked("Correction statement is unavailable.", error instanceof Error ? error.message : "Try again.");
    }
    finally { setPayslipBusy(null); }
  };

  // `/employees/$id/edit` is generated as a child of this route.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
      <AppShell>
        <Async state={state} rows={3}>
          {(e) =>
            !e ? (
              <RestrictedState />
            ) : (
              <RecordDetail
                reference={e.employeeNo}
                title={e.fullName}
                subtitle={`${e.jobTitle} · ${e.department}`}
                status={e.status}
                owner={e.managerId ? "Assigned manager" : "HR operations"}
                nextAction={
                  e.status === "Pre-hire"
                    ? `Complete onboarding before ${e.startDate}`
                    : e.status === "Notice period"
                      ? `Clearance and final pay before ${e.endDate}`
                      : e.endDate
                        ? `Confirm contract intention before ${e.endDate}`
                        : "No action required"
                }
                primaryAction={
                  <div className="flex flex-wrap justify-end gap-2">
                    <Button
                      variant="outline"
                      onClick={() => setPreviewOpen(true)}
                      disabled={payslipBusy !== null}
                    >
                      <Eye className="mr-2 size-4" aria-hidden />
                      Preview payslip
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => void printLatestPayslip(e.id)}
                      disabled={payslipBusy !== null}
                    >
                      <Printer className="mr-2 size-4" aria-hidden />
                      {payslipBusy === "print" ? "Checking..." : "Print last payslip"}
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => void printCorrectionStatement(e.id, e.employeeNo, e.fullName)}
                      disabled={payslipBusy !== null}
                    >
                      <Printer className="mr-2 size-4" aria-hidden />
                      Print correction statement
                    </Button>
                    <Button asChild>
                      <Link to="/hrm/employees/$id/edit" params={{ id: e.id }}>
                        Edit details
                      </Link>
                    </Button>
                  </div>
                }
                secondaryActions={
                  <>
                    <Button variant="outline" size="sm" asChild>
                      <Link to="/hrm/leave/new">Request leave</Link>
                    </Button>
                    {!USE_REAL ? (
                      <Button variant="outline" size="sm" onClick={() => setConfirmEnd(true)}>
                        End employment
                      </Button>
                    ) : null}
                  </>
                }
                summary={[
                  { label: "Employee number", value: e.employeeNo },
                  { label: "Employment type", value: e.employmentType },
                  {
                    label: "Legal entity",
                    value: USE_REAL
                      ? "Managed on assignment"
                      : entities.find((x) => x.id === e.entityId)?.name,
                  },
                  { label: "Branch", value: e.branch },
                  { label: "Location", value: e.location },
                  { label: "Start date", value: e.startDate },
                  {
                    label: "End date",
                    value: e.endDate ?? (
                      <span className="text-muted-foreground">Not applicable</span>
                    ),
                  },
                  { label: "Grade", value: e.grade },
                  {
                    label: "Work email",
                    value: e.email ?? <span className="text-muted-foreground">Not recorded</span>,
                  },
                  {
                    label: "Phone",
                    value: e.phone ?? <span className="text-muted-foreground">Not recorded</span>,
                  },
                  {
                    label: "Leave available",
                    value: leaveSummary ? (
                      `${leaveSummary.available} days`
                    ) : (
                      <span className="text-muted-foreground">Not applicable</span>
                    ),
                  },
                  {
                    label: "Future-effective change",
                    value: e.futureEffective ? (
                      `${e.futureEffective.change} from ${e.futureEffective.effectiveFrom}`
                    ) : (
                      <span className="text-muted-foreground">None scheduled</span>
                    ),
                  },
                ]}
                timeline={
                  <StatusTimeline
                    title="Employment history"
                    events={[
                      {
                        id: "e1",
                        at: `${e.startDate}T09:00:00Z`,
                        actor: "HR operations",
                        event: "Hired",
                        after: e.jobTitle,
                      },
                      ...(!USE_REAL
                        ? [
                            {
                              id: "e2",
                              at: "2025-01-01T09:00:00Z",
                              actor: "System",
                              event: "Annual salary review applied",
                              before: "Grade " + e.grade,
                              after: "Grade " + e.grade,
                              evidence: { label: "Review letter", href: "#" },
                            },
                          ]
                        : []),
                      ...(e.futureEffective
                        ? [
                            {
                              id: "e3",
                              at: `${e.futureEffective.effectiveFrom}T09:00:00Z`,
                              actor: "Mutale Kabwe",
                              event: "Scheduled change (future-effective)",
                              after: e.futureEffective.change,
                            },
                          ]
                        : []),
                    ]}
                  />
                }
                related={
                  <>
                    <Link
                      to="/hrm/leave"
                      className="block text-primary underline underline-offset-2"
                    >
                      Leave requests
                    </Link>
                    <Link
                      to="/hrm/attendance"
                      className="block text-primary underline underline-offset-2"
                    >
                      Attendance corrections
                    </Link>
                    <Link
                      to="/hrm/payslips"
                      className="block text-primary underline underline-offset-2"
                    >
                      Payslips
                    </Link>
                  </>
                }
              >
                <Async state={profileState} rows={4}>
                  {(profile) =>
                    profile ? (
                      <ProfileTabs employee={e} profile={profile} />
                    ) : (
                      <DetailSection
                        title="Full profile"
                        description="Only the directory record exists for this person."
                      >
                        <p className="text-sm text-muted-foreground">
                          Personal details, next of kin and statutory registrations have not been
                          captured yet. Payroll cannot pay someone without a bank account and a
                          NAPSA number, so complete the profile before the next run.
                        </p>
                        <Button className="mt-3" asChild>
                          <Link to="/hrm/employees/$id/edit" params={{ id: e.id }}>
                            Complete the profile
                          </Link>
                        </Button>
                      </DetailSection>
                    )
                  }
                </Async>

                {USE_REAL && hrAdmin ? (
                  <DetailSection
                    title="Account linking"
                    description="Self-service works only when the employee record is linked to an identity."
                  >
                    <div className="flex flex-wrap items-center gap-3">
                      {subjectId ? (
                        <span className="flex items-center gap-1.5 rounded border bg-surface px-2 py-1 text-xs font-mono">
                          <Link2 className="size-3.5 text-muted-foreground" aria-hidden />
                          Linked to identity {subjectId.slice(0, 8)}…
                        </span>
                      ) : (
                        <span className="flex items-center gap-1.5 rounded border border-warning bg-warning/10 px-2 py-1 text-xs">
                          <Unlink className="size-3.5" aria-hidden />
                          Not linked — leave, documents, letters and payslips are unavailable for
                          this person.
                        </span>
                      )}
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={linkBusy}
                        onClick={() => {
                          setLinkSubject(subjectId ?? "");
                          setLinkOpen(true);
                        }}
                      >
                        <KeyRound className="size-4" aria-hidden />
                        {subjectId ? "Change account" : "Link account"}
                      </Button>
                    </div>
                  </DetailSection>
                ) : null}

                <PayslipPreviewDialog
                  employee={e}
                  profile={profileState.data}
                  open={previewOpen}
                  onOpenChange={setPreviewOpen}
                />

                <ConfirmDialog
                  open={linkOpen}
                  onOpenChange={setLinkOpen}
                  title={
                    subjectId
                      ? `Change the linked identity for ${e.fullName}?`
                      : `Link an identity to ${e.fullName}?`
                  }
                  consequence="The employee's self-service surfaces (leave, documents, letters, payslips) will attach to this identity. An identity can only be linked to one employee record."
                  detail={
                    <ul className="list-inside list-disc space-y-1">
                      <li>Paste the Keycloak identity id (the token "sub" claim) of the user.</li>
                      <li>
                        If the identity is already linked elsewhere, linking here will transfer it.
                      </li>
                      <li>Unlinking is done by clearing the field in the edit form.</li>
                    </ul>
                  }
                  confirmLabel="Save link"
                  onConfirm={() => {
                    const value = linkSubject.trim();
                    if (!value) {
                      feedback.blocked(
                        "Identity id is empty.",
                        'Paste the identity id (the token "sub" claim) to continue.',
                      );
                      return;
                    }
                    setLinkBusy(true);
                    realApi
                      .updateWorker(e.id, { subjectId: value })
                      .then(() => {
                        setLinkBusy(false);
                        setLinkOpen(false);
                        setSubjectId(value);
                        feedback.submitted(
                          `Identity linked to ${e.fullName}.`,
                          "The account is now connected to this employee record. Self-service surfaces will show their own data on the next visit.",
                        );
                      })
                      .catch((err) => {
                        setLinkBusy(false);
                        const apiErr = err as { message?: string };
                        feedback.blocked(
                          "Link failed.",
                          String(
                            apiErr?.message ??
                              "The identity could not be linked — it may already belong to another employee record or not exist.",
                          ),
                        );
                      });
                  }}
                />
                <ConfirmDialog
                  open={confirmEnd}
                  onOpenChange={setConfirmEnd}
                  title={`End employment for ${e.fullName}?`}
                  consequence="This starts offboarding. It does not delete anything — the record is retained for the statutory period and stays payable for any final settlement."
                  detail={
                    <ul className="list-inside list-disc space-y-1">
                      <li>Access is revoked on the last working day, not today.</li>
                      <li>
                        Final pay, leave settlement and any outstanding advance are calculated
                        first.
                      </li>
                      <li>Assets on loan are listed for return.</li>
                    </ul>
                  }
                  confirmLabel="Start offboarding"
                  onConfirm={() =>
                    feedback.submitted(
                      `Offboarding started for ${e.fullName}.`,
                      "Clearance checklist created. Final pay is calculated in the next run.",
                    )
                  }
                />
              </RecordDetail>
            )
          }
        </Async>
      </AppShell>
    </AuthGate>
  );
}
