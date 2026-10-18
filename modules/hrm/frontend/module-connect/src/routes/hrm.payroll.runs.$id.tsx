import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { Fragment, useEffect, useState } from "react";
import {
  AlertTriangle,
  Ban,
  Check,
  CircleDashed,
  Download,
  Info,
  Lock,
  RotateCcw,
  ShieldAlert,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { isOutstanding, money, payrollRunApi } from "@/mock/payrollrun";
import { CalculationPanel } from "@/platform/components/CalculationPanel";
import type { ControlTotals, PayRun, RunLine, RunStage } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { ConfirmDialog } from "@/platform/components/ConfirmDialog";
import { feedback } from "@/platform/feedback";
import { CalculationExplainer } from "@/platform/components/CalculationExplainer";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { hrmApi } from "@/platform/api-client";
import { realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";
import { useAuth } from "@/platform/auth";

type PayrollRunTab = "overview" | "calculate" | "review" | "approval" | "release";
const payrollRunTabKey = (runId: string) => `hrm.payroll.run.${runId}.active-tab`;

export const Route = createFileRoute("/hrm/payroll/runs/$id")({
  head: () => ({
    meta: [
      { title: "Pay run — Newworldcargo HRM" },
      {
        name: "description",
        content:
          "A pay run stage by stage: population, calculation, variances, approval and controlled release.",
      },
      { property: "og:title", content: "Pay run — Newworldcargo HRM" },
      {
        property: "og:description",
        content:
          "A pay run stage by stage: population, calculation, variances, approval and controlled release.",
      },
    ],
  }),
  component: RunDetail,
});

const stageIcon = {
  done: Check,
  current: CircleDashed,
  blocked: Lock,
  pending: CircleDashed,
} as const;

const stageWord = {
  done: "Complete",
  current: "In progress",
  blocked: "Blocked",
  pending: "Not started",
} as const;

function Stages({ stages }: { stages: RunStage[] }) {
  return (
    <ol className="space-y-0">
      {stages.map((s, i) => {
        const Icon = stageIcon[s.state];
        return (
          <li key={s.id} className="flex gap-3">
            <div className="flex flex-col items-center">
              <span
                className={`grid size-7 shrink-0 place-items-center rounded-full border text-[11px] font-medium ${
                  s.state === "done"
                    ? "border-success bg-success-soft text-success"
                    : s.state === "current"
                      ? "border-primary bg-primary-soft text-primary"
                      : s.state === "blocked"
                        ? "border-warning bg-warning-soft text-warning"
                        : "border-border-strong text-muted-foreground"
                }`}
              >
                {s.state === "done" || s.state === "blocked" ? (
                  <Icon className="size-3.5" aria-hidden />
                ) : (
                  i + 1
                )}
              </span>
              {i < stages.length - 1 ? (
                <span className="min-h-6 w-px flex-1 bg-border" aria-hidden />
              ) : null}
            </div>
            <div className="min-w-0 flex-1 pb-5">
              <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
                {s.label}
                <span
                  className={`rounded-full border px-2 py-0.5 text-[11px] font-normal ${
                    s.state === "done"
                      ? "border-success/30 bg-success-soft text-success"
                      : s.state === "current"
                        ? "border-primary/40 bg-primary-soft text-primary"
                        : s.state === "blocked"
                          ? "border-warning/40 bg-warning-soft text-warning"
                          : "border-border bg-muted text-muted-foreground"
                  }`}
                >
                  {stageWord[s.state]}
                </span>
              </p>
              <p className="mt-0.5 text-xs text-muted-foreground">{s.purpose}</p>
              {s.at ? (
                <p className="mt-1 text-xs text-muted-foreground">
                  {s.by} · {s.at}
                </p>
              ) : null}
              {s.note ? <p className="mt-1 text-xs">{s.note}</p> : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

function Totals({ run }: { run: PayRun }) {
  const rows: { label: string; key: keyof ControlTotals; isMoney: boolean }[] = [
    { label: "Employees paid", key: "headcount", isMoney: false },
    { label: "Gross pay", key: "gross", isMoney: true },
    { label: "Deductions", key: "deductions", isMoney: true },
    { label: "Net pay", key: "net", isMoney: true },
    { label: "Employer cost (not deducted)", key: "employerCost", isMoney: true },
  ];

  return (
    <div className="overflow-x-auto">
      <RecordTable variant="compact" className="w-full min-w-[34rem] text-left text-sm">
        <caption className="sr-only">
          Control totals for this run compared with the previous period
        </caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Control total
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              This period
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Previous
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Change
            </th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((r) => {
            const now = run.totals[r.key];
            const before = run.priorTotals?.[r.key];
            const diff = before === undefined ? null : now - before;
            const pct = before ? (diff! / before) * 100 : null;
            const material = pct !== null && Math.abs(pct) >= 2;
            return (
              <tr key={r.key}>
                <th scope="row" className="px-3 py-2 font-normal">
                  {r.label}
                </th>
                <td className="tabular px-3 py-2 text-right font-medium">
                  {r.isMoney ? money(now, run.currency) : now}
                </td>
                <td className="tabular px-3 py-2 text-right text-muted-foreground">
                  {before === undefined ? "—" : r.isMoney ? money(before, run.currency) : before}
                </td>
                <td className="tabular px-3 py-2 text-right">
                  {diff === null ? (
                    <span className="text-muted-foreground">—</span>
                  ) : diff === 0 ? (
                    <span className="text-muted-foreground">No change</span>
                  ) : (
                    <span className={material ? "font-medium text-warning" : ""}>
                      {diff > 0 ? "+" : "−"}
                      {r.isMoney ? money(Math.abs(diff), run.currency) : Math.abs(diff)}
                      {pct !== null ? ` (${pct > 0 ? "+" : "−"}${Math.abs(pct).toFixed(1)}%)` : ""}
                      {material ? " · material" : ""}
                    </span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </RecordTable>
    </div>
  );
}

/**
 * The three release actions, each gated on the one before it.
 *
 * The rule this encodes: releasing a payslip, paying the money and posting the
 * journal are different decisions with different consequences, so they are
 * different buttons — and a step that is not available says why, rather than
 * being a dead control labelled "Not yet".
 */
type StatutoryWorker = {
  workerId?: string;
  employeeNo?: string;
  fullName?: string;
  hasNrc?: boolean;
  hasTpin?: boolean;
  hasNapsaNumber?: boolean;
  hasNhimaNumber?: boolean;
  ready?: boolean;
};

type CalculationReadiness = {
  ready: boolean;
  includedWorkerCount: number;
  blockingCount: number;
  warningCount: number;
  checks: Array<{ id: string; label: string; state: string; detail: string; count: number }>;
  issues: Array<{
    workerId?: string;
    employeeNo: string;
    workerName: string;
    issue: string;
    severity: string;
  }>;
};

function CalculationReadinessCard({ readiness }: { readiness: CalculationReadiness | null }) {
  if (!readiness) return null;
  const blockers = readiness.issues.filter((issue) => issue.severity === "fail");
  const warnings = readiness.issues.filter((issue) => issue.severity === "warn");
  const tone = readiness.ready
    ? "border-success/30 bg-success-soft text-success"
    : "border-danger/40 bg-danger-soft text-danger";

  return (
    <div className={`rounded-lg border p-4 ${tone}`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="flex items-center gap-2 text-sm font-semibold">
            {readiness.ready ? (
              <Check className="size-4" aria-hidden />
            ) : (
              <ShieldAlert className="size-4" aria-hidden />
            )}
            {readiness.ready ? "Calculation inputs ready" : "Calculation inputs blocked"}
          </p>
          <p className="mt-1 text-xs text-foreground">
            {readiness.includedWorkerCount} worker{readiness.includedWorkerCount === 1 ? "" : "s"}{" "}
            checked. Payroll rules remain configured in setup; this panel verifies the active setup
            for this run.
          </p>
        </div>
        <div className="flex gap-2 text-xs">
          <span className="rounded-full border bg-background px-2.5 py-1 text-foreground">
            {readiness.blockingCount} blocker{readiness.blockingCount === 1 ? "" : "s"}
          </span>
          <span className="rounded-full border bg-background px-2.5 py-1 text-foreground">
            {readiness.warningCount} warning{readiness.warningCount === 1 ? "" : "s"}
          </span>
        </div>
      </div>

      <div className="mt-4 grid gap-2 md:grid-cols-2">
        {readiness.checks.map((check) => (
          <div key={check.id} className="rounded-md border bg-background p-3 text-foreground">
            <p className="flex items-center gap-2 text-sm font-medium">
              {check.state === "pass" ? (
                <Check className="size-3.5 text-success" aria-hidden />
              ) : check.state === "warn" ? (
                <AlertTriangle className="size-3.5 text-warning" aria-hidden />
              ) : (
                <X className="size-3.5 text-danger" aria-hidden />
              )}
              {check.label}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">{check.detail}</p>
          </div>
        ))}
      </div>

      {blockers.length || warnings.length ? (
        <div className="mt-4 rounded-md border bg-background p-3 text-foreground">
          <p className="text-xs font-semibold uppercase text-muted-foreground">Affected workers</p>
          <ul className="mt-2 space-y-1.5 text-xs">
            {[...blockers, ...warnings].slice(0, 8).map((issue) => (
              <li
                key={`${issue.workerId ?? issue.employeeNo}-${issue.issue}`}
                className="flex flex-wrap gap-x-2 gap-y-1"
              >
                <span className="font-medium">
                  {issue.workerName || issue.employeeNo || "Worker"}
                </span>
                {issue.employeeNo ? (
                  <span className="text-muted-foreground">({issue.employeeNo})</span>
                ) : null}
                <span className={issue.severity === "fail" ? "text-danger" : "text-warning"}>
                  {issue.issue}
                </span>
              </li>
            ))}
          </ul>
          {readiness.issues.length > 8 ? (
            <p className="mt-2 text-xs text-muted-foreground">
              {readiness.issues.length - 8} more issue{readiness.issues.length - 8 === 1 ? "" : "s"}{" "}
              not shown.
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

/**
 * M24: the backend refuses to release while any worker in the run is missing an
 * NRC, TPIN, NAPSA or NHIMA number. This card shows exactly who blocks release
 * before the button is even pressed — so the "blocked" message is never a
 * surprise after clicking.
 */
function StatutoryReadinessCard({
  readiness,
}: {
  readiness: { isReady?: boolean; workers?: StatutoryWorker[] } | null;
}) {
  if (!readiness) return null;
  const missing = (readiness.workers ?? []).filter((w) => !w.ready);
  if (!missing.length) return null;
  const checks: Array<{ key: keyof StatutoryWorker; label: string }> = [
    { key: "hasNrc", label: "NRC" },
    { key: "hasTpin", label: "TPIN" },
    { key: "hasNapsaNumber", label: "NAPSA" },
    { key: "hasNhimaNumber", label: "NHIMA" },
  ];
  return (
    <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
      <p className="flex items-start gap-2 text-sm font-medium text-danger">
        <X className="mt-0.5 size-4 shrink-0" aria-hidden />
        Release blocked — {missing.length} worker{missing.length === 1 ? "" : "s"} missing statutory
        identity references
      </p>
      <p className="mt-2 text-xs text-foreground">
        Every worker in this run must carry an NRC, TPIN, NAPSA and NHIMA number before payslips can
        be released. Collect the missing references on the worker record, then re-check.
      </p>
      <ul className="mt-3 space-y-1.5 text-xs">
        {missing.map((w) => (
          <li
            key={w.workerId ?? w.employeeNo ?? Math.random()}
            className="flex flex-wrap gap-x-3 gap-y-0.5"
          >
            <span className="font-medium">
              {w.fullName ?? w.employeeNo ?? "Worker"}
              {w.employeeNo ? (
                <span className="text-muted-foreground"> ({w.employeeNo})</span>
              ) : null}
            </span>
            <span className="flex gap-2 text-warning">
              {checks.map((c) =>
                !w[c.key] ? (
                  <span key={c.key} className="inline-flex items-center gap-1">
                    <X className="size-3" aria-hidden />
                    missing {c.label}
                  </span>
                ) : null,
              )}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function ReleaseActions({
  run,
  readiness,
  onReleased,
}: {
  run: PayRun;
  readiness: { isReady?: boolean } | null;
  onReleased: () => Promise<unknown> | void;
}) {
  const [done, setDone] = useState<string[]>(() => {
    const stages = run.stages.filter((s) => s.state === "done").map((s) => s.id);
    return run.status === "Paid" || run.status === "Closed"
      ? [...new Set([...stages, "s7"])]
      : stages;
  });
  const [confirming, setConfirming] = useState<string | null>(null);
  const [releasing, setReleasing] = useState(false);

  const approved = run.status === "Approved" || run.status === "Paid" || run.status === "Closed";
  // M24: until every worker carries NRC + TPIN + NAPSA + NHIMA, the backend
  // refuses to release — surface that here so the button never fails silently.
  const statutoryBlocked =
    USE_REAL && approved && readiness && !readiness.isReady
      ? "Workers in this run are still missing statutory identity references — see the banner above."
      : null;

  const steps = [
    {
      id: "s7",
      label: "Release payslips",
      detail:
        "Makes payslips visible to employees. Does not move money. A released payslip is never silently overwritten — a correction creates a new linked version.",
      action: "Release payslips",
      requiresText: "",
      destructive: true,
      consequence: `${run.included} employees at ${run.entityName} will be able to see their ${run.period} payslip immediately. No money moves.`,
      blockedBy: approved ? null : "The run must be approved first.",
      toast: `Payslips released to ${run.included} employees.`,
      next: "Payments are still to be released — nobody has been paid yet.",
      api: true,
    },
  ];

  const active = steps.find((s) => s.id === confirming);
  return (
    <>
      <ul className="space-y-3 text-sm">
        {steps.map((s) => {
          const isDone = done.includes(s.id);
          const missing: string | null = null;
          const why = s.blockedBy ?? missing ?? (s.api ? statutoryBlocked : null);

          return (
            <li
              key={s.id}
              className="flex flex-wrap items-start justify-between gap-4 rounded-md border p-3"
            >
              <span className="min-w-0 flex-1">
                <span className="block font-medium">{s.label}</span>
                <span className="block text-xs text-muted-foreground">{s.detail}</span>
                {why && !isDone ? (
                  <span className="mt-1 flex gap-1.5 text-xs text-warning">
                    <Lock className="mt-0.5 size-3 shrink-0" aria-hidden />
                    {why}
                  </span>
                ) : null}
              </span>

              {isDone ? (
                <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-1 text-xs text-success">
                  <Check className="size-3.5 shrink-0" aria-hidden />
                  Released
                </span>
              ) : (
                <Button
                  variant="outline"
                  size="sm"
                  className="shrink-0"
                  disabled={Boolean(why)}
                  onClick={() => setConfirming(s.id)}
                >
                  {releasing && s.id === "s7" ? "Releasing…" : s.action}
                </Button>
              )}
            </li>
          );
        })}
      </ul>

      <ConfirmDialog
        open={active !== undefined}
        onOpenChange={(o) => !o && setConfirming(null)}
        title={active?.action ?? ""}
        consequence={active?.consequence ?? ""}
        detail={
          <span className="block text-xs text-muted-foreground">
            Recorded against {run.id} in the audit trail, with your name and the time.
          </span>
        }
        confirmLabel={active?.action ?? "Confirm"}
        cancelLabel="Not yet"
        destructive={active?.destructive}
        onConfirm={async () => {
          if (!active) return;
          if ("api" in active && active.api) {
            setReleasing(true);
            try {
              await realApi.payrollRunRelease(run.id);
              setDone((d) => [...d, active.id]);
              feedback.submitted(active.toast, active.next);
              await onReleased();
            } catch (e) {
              const msg = e instanceof Error ? e.message : "Unknown error.";
              if (/statutory/i.test(msg)) {
                feedback.blocked(
                  "Release blocked by statutory identity check",
                  "One or more workers are missing an NRC, TPIN, NAPSA or NHIMA number. " +
                    "Add the references on the worker record, then try again.",
                );
              } else {
                feedback.blocked("Release failed", msg);
              }
            } finally {
              setReleasing(false);
              setConfirming(null);
            }
            return;
          }
          setDone((d) => [...d, active.id]);
          setConfirming(null);
          feedback.submitted(active.toast, active.next);
        }}
      />
    </>
  );
}

function PaymentWorkflow({
  run,
  onChanged,
}: {
  run: OperationalPayRun;
  onChanged: () => Promise<unknown>;
}) {
  const { user } = useAuth();
  const [busy, setBusy] = useState(false);
  const [reference, setReference] = useState("");
  const [reportKind, setReportKind] = useState("jv-summary");
  const [reportFormat, setReportFormat] = useState<"csv" | "pdf">("csv");
  const [bankFormat, setBankFormat] = useState<"csv">("csv");
  const readinessState = useApi(async () => {
    const raw = (await realApi.payrollPaymentReadiness(run.id)) as Record<string, unknown>;
    const issues = Array.isArray(raw.issues)
      ? (raw.issues as Record<string, unknown>[]).map((issue) => ({
          workerId: String(issue.workerId ?? ""),
          employeeNo: String(issue.employeeNo ?? ""),
          workerName: String(issue.workerName ?? ""),
          netPay: Number(issue.netPay ?? 0),
          issue: String(issue.issue ?? "Payment readiness issue"),
        }))
      : [];
    return {
      ready: Boolean(raw.ready),
      payableCount: Number(raw.payableCount ?? 0),
      totalNet: Number(raw.totalNet ?? 0),
      missingBankDetailsCount: Number(raw.missingBankDetailsCount ?? issues.length),
      issues,
    };
  }, [run.id, run.paymentStatus, run.backendStatus]);
  const status = run.paymentStatus;
  const readiness = readinessState.data;
  const currentSubjectId = user?.id ? String(user.id) : "";
  const generatedByMe = Boolean(
    currentSubjectId && run.paymentFileGeneratedBySubjectId === currentSubjectId,
  );
  const approvedByMe = Boolean(
    currentSubjectId && run.paymentApprovedBySubjectId === currentSubjectId,
  );
  const isTopAdmin = (user?.roles ?? []).some((role) => role.toLowerCase() === "hr_admin");
  const canGenerate =
    run.backendStatus === "released" && status === "not-created" && readiness?.ready === true;
  const canApprove = status === "generated" && (isTopAdmin || !generatedByMe);
  const canRelease = status === "approved" && (isTopAdmin || (!approvedByMe && !generatedByMe));
  const invoke = async (label: string, action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      feedback.submitted(label, "The action and actor were added to the payroll audit trail.");
      await onChanged();
    } catch (e) {
      feedback.blocked(`${label} failed`, e instanceof Error ? e.message : "Unknown error.");
    } finally {
      setBusy(false);
    }
  };
  const download = async (kind: "payment" | "audit") => {
    const blob =
      kind === "payment"
        ? await realApi.payrollPaymentFile(run.id)
        : await realApi.payrollAuditExport(run.id);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${kind === "payment" ? (run.paymentFileReference ?? `payment-${run.id}`) : `payroll-audit-${run.id}`}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  // M41: accounting-facing reports — the accounts team books the salary from
  // these (JV = debits/credits per transaction; dept = net per department).
  const downloadReport = async (kind: string, format: "csv" | "pdf") => {
    const blob = await hrmApi.getBlob(`/hrm/payroll/runs/${run.id}/reports/${kind}`, { format });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `payroll-report-${kind}-${run.id}.${format}`;
    anchor.click();
    URL.revokeObjectURL(url);
  };
  const paymentSteps = [
    {
      key: "released",
      label: "Payslips released",
      done: ["released", "closed"].includes(run.backendStatus),
      actor: run.releasedBy,
      blocked: !["released", "closed"].includes(run.backendStatus),
    },
    {
      key: "generated",
      label: "Bank file generated",
      done: status !== "not-created",
      actor: run.paymentFileGeneratedBySubjectId,
      blocked: run.backendStatus !== "released" && run.backendStatus !== "closed",
    },
    {
      key: "approved",
      label: "Payment approved",
      done: ["approved", "released", "reconciled"].includes(status),
      actor: run.paymentApprovedBySubjectId,
      blocked: status === "not-created",
    },
    {
      key: "bank-released",
      label: "Released to bank",
      done: ["released", "reconciled"].includes(status),
      actor: run.paymentReleasedBySubjectId,
      blocked: status !== "approved" && !["released", "reconciled"].includes(status),
    },
    {
      key: "reconciled",
      label: "Reconciled and closed",
      done: status === "reconciled",
      actor: run.reconciliationReference,
      blocked: status !== "released" && status !== "reconciled",
    },
  ];
  const closeoutChecks = [
    {
      key: "payslips",
      label: "Payslips released",
      detail: ["released", "closed"].includes(run.backendStatus)
        ? "Employees can see released payslips."
        : "Release payslips before generating a bank file.",
      state: ["released", "closed"].includes(run.backendStatus) ? "done" : "blocked",
    },
    {
      key: "reports",
      label: "Accounting reports available",
      detail: ["released", "closed"].includes(run.backendStatus)
        ? "JV and department payroll reports can be exported."
        : "Reports open once the run is released.",
      state: ["released", "closed"].includes(run.backendStatus) ? "done" : "blocked",
    },
    {
      key: "bank-file",
      label: "Bank file generated",
      detail:
        status === "not-created"
          ? readiness?.ready === false
            ? "Fix worker bank details before generating the file."
            : "Generate the payment file after payslip release."
          : run.paymentFileReference
            ? run.paymentFileReference
            : "Payment file exists for this run.",
      state: status === "not-created" ? "blocked" : "done",
    },
    {
      key: "maker-checker",
      label: "Maker-checker approval",
      detail: ["approved", "released", "reconciled"].includes(status)
        ? "A different authorised user approved the payment file."
        : status === "generated"
          ? "Waiting for a different user to approve."
          : "Approval starts after bank file generation.",
      state: ["approved", "released", "reconciled"].includes(status)
        ? "done"
        : status === "generated"
          ? "current"
          : "blocked",
    },
    {
      key: "bank-release",
      label: "Released to bank",
      detail: ["released", "reconciled"].includes(status)
        ? "Bank instruction release has been recorded."
        : status === "approved"
          ? "Waiting for a separate payroll user to release."
          : "Release starts after payment approval.",
      state: ["released", "reconciled"].includes(status)
        ? "done"
        : status === "approved"
          ? "current"
          : "blocked",
    },
    {
      key: "reconciled",
      label: "Reconciled and closed",
      detail:
        status === "reconciled"
          ? run.reconciliationReference
            ? `Closed with reference ${run.reconciliationReference}.`
            : "Bank acknowledgement matched payroll net."
          : status === "released"
            ? "Enter the bank acknowledgement reference to close this payroll."
            : "Closeout starts after bank release.",
      state: status === "reconciled" ? "done" : status === "released" ? "current" : "blocked",
    },
  ];
  const closeoutDone = closeoutChecks.filter((check) => check.state === "done").length;

  return (
    <div className="mt-4 rounded-lg border p-4" data-testid="payment-workflow">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium">Payment file and reconciliation</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Status:{" "}
            <span className="font-medium text-foreground">{status.replaceAll("-", " ")}</span>
            {run.paymentFileReference ? ` · ${run.paymentFileReference}` : ""}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Payment workflow is maker-checker for payroll officers: generate the bank file, approve
            it, release it to the bank, then reconcile the bank acknowledgement before the run is
            closed. HR admin can override this when they carry final payroll authority.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void download("audit")}>
          Export audit CSV
        </Button>
      </div>
      <div className="mt-4 grid gap-2 sm:grid-cols-5">
        {paymentSteps.map((step) => {
          const Icon = step.done ? Check : step.blocked ? Lock : CircleDashed;
          return (
            <div
              key={step.key}
              className={`rounded-md border p-3 ${
                step.done
                  ? "border-success/30 bg-success-soft"
                  : step.blocked
                    ? "border-border bg-muted/30"
                    : "border-primary/30 bg-primary-soft"
              }`}
            >
              <div className="flex items-center gap-2">
                <Icon
                  className={`size-4 ${
                    step.done
                      ? "text-success"
                      : step.blocked
                        ? "text-muted-foreground"
                        : "text-primary"
                  }`}
                  aria-hidden
                />
                <span className="text-xs font-medium">{step.label}</span>
              </div>
              <p className="mt-1 truncate text-[11px] text-muted-foreground">
                {step.done
                  ? step.actor
                    ? `By ${step.actor}`
                    : "Completed"
                  : step.blocked
                    ? "Waiting for prior step"
                    : "Ready"}
              </p>
            </div>
          );
        })}
      </div>
      <div className="mt-4 rounded-md border bg-surface p-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p className="text-sm font-medium">Payroll closeout readiness</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {closeoutDone} of {closeoutChecks.length} controls complete before payroll is fully
              closed.
            </p>
          </div>
          <span
            className={`rounded-full px-2.5 py-1 text-xs font-medium ${
              closeoutDone === closeoutChecks.length
                ? "bg-success-soft text-success"
                : "bg-primary-soft text-primary"
            }`}
          >
            {closeoutDone === closeoutChecks.length ? "Closed" : "In progress"}
          </span>
        </div>
        <div className="mt-3 grid gap-2 md:grid-cols-2">
          {closeoutChecks.map((check) => {
            const Icon =
              check.state === "done" ? Check : check.state === "current" ? CircleDashed : Lock;
            return (
              <div key={check.key} className="flex gap-2 rounded-md border bg-background p-2.5">
                <Icon
                  className={`mt-0.5 size-4 shrink-0 ${
                    check.state === "done"
                      ? "text-success"
                      : check.state === "current"
                        ? "text-primary"
                        : "text-muted-foreground"
                  }`}
                  aria-hidden
                />
                <div className="min-w-0">
                  <p className="text-xs font-medium">{check.label}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground">{check.detail}</p>
                </div>
              </div>
            );
          })}
        </div>
      </div>
      {run.backendStatus !== "released" && run.backendStatus !== "closed" ? (
        <p className="mt-3 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
          Payment file generation is blocked until payslips are released. Releasing payslips still
          does not pay anyone; it only makes the payslip records visible.
        </p>
      ) : null}
      {status === "not-created" ? (
        <div className="mt-4 rounded-md border bg-surface p-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <p className="text-sm font-medium">Bank readiness</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {readinessState.loading
                  ? "Checking primary bank details for payable workers."
                  : readiness
                    ? `${readiness.payableCount} payable worker${readiness.payableCount === 1 ? "" : "s"} · ${money(readiness.totalNet, run.currency)} net`
                    : "Bank readiness could not be loaded."}
              </p>
            </div>
            {readiness ? (
              <span
                className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                  readiness.ready ? "bg-success-soft text-success" : "bg-warning-soft text-warning"
                }`}
              >
                {readiness.ready
                  ? "Ready"
                  : `${readiness.missingBankDetailsCount} missing bank details`}
              </span>
            ) : null}
          </div>
          {readiness?.issues.length ? (
            <ul className="mt-3 space-y-1 text-xs text-muted-foreground">
              {readiness.issues.slice(0, 5).map((issue) => (
                <li key={`${issue.workerId}-${issue.issue}`} className="flex justify-between gap-3">
                  <span>
                    {issue.employeeNo ? `${issue.employeeNo} · ` : ""}
                    {issue.workerName || "Worker"}
                  </span>
                  <span className="text-warning">{issue.issue}</span>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}

      {/* M41: accounting reports for the accounts team. The run must be
          released first — the backend enforces the same rule server-side. */}
      <div className="mt-4 border-t pt-4">
        <p className="text-sm font-medium">Accounting reports</p>
        <p className="mt-1 text-xs text-muted-foreground">
          JV = debits and credits by transaction; department reports = net pay per department with
          bank details. Reports are only available once the run is released or closed. GL accounts
          marked * need mapping before booking.
        </p>
        <div className="mt-3 grid gap-3 md:grid-cols-[minmax(0,1fr)_180px_auto]">
          <div>
            <Label htmlFor="report-kind" className="text-xs">Report</Label>
            <Select value={reportKind} onValueChange={setReportKind}>
              <SelectTrigger id="report-kind" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="jv-summary">JV summary</SelectItem>
                <SelectItem value="jv-detailed">JV detailed</SelectItem>
                <SelectItem value="dept-summary">Department summary</SelectItem>
                <SelectItem value="dept-detailed">Department detailed</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="report-format" className="text-xs">File type</Label>
            <Select value={reportFormat} onValueChange={(value) => setReportFormat(value as "csv" | "pdf")}>
              <SelectTrigger id="report-format" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="csv">CSV</SelectItem>
                <SelectItem value="pdf">PDF</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <Button
            variant="outline"
            className="self-end"
            disabled={run.backendStatus !== "released" && run.backendStatus !== "closed"}
            onClick={() => void downloadReport(reportKind, reportFormat)}
          >
            <Download className="size-4" aria-hidden /> Download
          </Button>
        </div>
      </div>
      <div className="mt-4 flex flex-wrap gap-2">
        {status === "not-created" ? (
          <Button
            disabled={busy || !canGenerate}
            title={
              run.backendStatus !== "released"
                ? "Payslips must be released before a bank file can be generated."
                : readiness?.ready === false
                  ? "Fix missing primary bank details before generating the bank file."
                  : undefined
            }
            onClick={() =>
              void invoke("Payment file generated", () => realApi.payrollPaymentGenerate(run.id))
            }
          >
            Generate bank file
          </Button>
        ) : null}
        {status !== "not-created" ? (
          <div className="flex flex-wrap items-end gap-2">
            <div>
              <Label htmlFor="bank-format" className="text-xs">Bank file type</Label>
              <Select value={bankFormat} onValueChange={(value) => setBankFormat(value as "csv")}>
                <SelectTrigger id="bank-format" className="mt-1 w-32">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="csv">CSV</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <Button variant="outline" disabled={busy} onClick={() => void download("payment")}>
              <Download className="size-4" aria-hidden /> Download bank file
            </Button>
          </div>
        ) : null}
        {status === "generated" ? (
          <Button
            disabled={busy || !canApprove}
            title={
              generatedByMe && !isTopAdmin
                ? "The person who generated the payment file cannot approve it."
                : undefined
            }
            onClick={() =>
              void invoke("Payment file approved", () =>
                realApi.payrollPaymentApprove(run.id, "Reviewed against payroll control totals"),
              )
            }
          >
            Approve payment file
          </Button>
        ) : null}
        {status === "approved" ? (
          <Button
            disabled={busy || !canRelease}
            title={
              approvedByMe && !isTopAdmin
                ? "The payment approver cannot also release the bank instruction."
                : generatedByMe && !isTopAdmin
                  ? "The person who generated the payment file cannot release it to the bank."
                  : undefined
            }
            onClick={() =>
              void invoke("Payment instruction released", () =>
                realApi.payrollPaymentRelease(run.id),
              )
            }
          >
            Release to bank
          </Button>
        ) : null}
      </div>
      {status === "generated" && generatedByMe && !isTopAdmin ? (
        <p className="mt-3 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
          You generated this payment file. A different payroll or HR approver must approve it.
        </p>
      ) : null}
      {status === "approved" && approvedByMe && !isTopAdmin ? (
        <p className="mt-3 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
          You approved this payment file. A different payroll officer must release it to the bank.
        </p>
      ) : null}
      {status === "approved" && generatedByMe && !isTopAdmin ? (
        <p className="mt-3 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
          You generated this payment file. A different payroll officer must release it to the bank.
        </p>
      ) : null}
      {status === "released" ? (
        <div className="mt-4 flex flex-wrap items-end gap-2">
          <label className="min-w-64 flex-1 text-xs font-medium">
            Bank acknowledgement reference
            <input
              className="mt-1 h-9 w-full rounded-md border bg-background px-3 text-sm"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="e.g. ZANACO-ACK-202608-001"
            />
          </label>
          <Button
            disabled={busy || !reference.trim()}
            onClick={() =>
              void invoke("Payroll reconciled and closed", () =>
                realApi.payrollReconcile(
                  run.id,
                  reference.trim(),
                  run.totals.net,
                  "Bank total matched payroll net",
                ),
              )
            }
          >
            Reconcile and close
          </Button>
        </div>
      ) : null}
      {status === "reconciled" ? (
        <p className="mt-4 text-sm text-success">
          Reconciled and closed
          {run.reconciliationReference ? ` · ${run.reconciliationReference}` : ""}
        </p>
      ) : null}
    </div>
  );
}

/** One employee's line on the run, expandable to show how it was derived. */
function PayLines({
  rows,
  currency,
  payslipsReleased,
}: {
  rows: RunLine[];
  currency: string;
  /** Once released, each line is also visible to the employee as a payslip. */
  payslipsReleased: boolean;
}) {
  const [open, setOpen] = useState<string | null>(null);

  return (
    <div className="overflow-x-auto">
      <RecordTable variant="compact" className="w-full min-w-[38rem] text-left text-sm">
        <caption className="sr-only">
          Every employee in this run with their gross, deductions and net pay
        </caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            <th
              scope="col"
              className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Employee
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Gross
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Deductions
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              Net
            </th>
            <th
              scope="col"
              className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground"
            >
              vs last
            </th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((l) => {
            const shown = open === l.id;
            const variance =
              l.priorNet && l.priorNet > 0 ? ((l.net - l.priorNet) / l.priorNet) * 100 : null;
            const material = variance !== null && Math.abs(variance) >= 2;
            return (
              <Fragment key={l.id}>
                <tr>
                  <th scope="row" className="px-3 py-2 font-normal">
                    <button
                      type="button"
                      onClick={() => setOpen(shown ? null : l.id)}
                      aria-expanded={shown}
                      className="text-left"
                    >
                      <span className="block font-medium underline-offset-2 hover:underline">
                        {l.employee}
                      </span>
                      <span className="block text-xs text-muted-foreground">
                        {l.jobTitle} · {l.grade}
                      </span>
                    </button>
                    {l.flags.map((f) => (
                      <span key={f} className="mt-1 flex gap-1.5 text-xs text-warning">
                        <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
                        {f}
                      </span>
                    ))}
                  </th>
                  <td className="tabular px-3 py-2 text-right">{money(l.gross, currency)}</td>
                  <td className="tabular px-3 py-2 text-right text-muted-foreground">
                    −{money(l.deductions, currency)}
                  </td>
                  <td className="tabular px-3 py-2 text-right font-medium">
                    {money(l.net, currency)}
                  </td>
                  <td className="tabular px-3 py-2 text-right">
                    {variance === null ? (
                      <span className="text-muted-foreground">New</span>
                    ) : Math.abs(variance) < 0.05 ? (
                      <span className="text-muted-foreground">No change</span>
                    ) : (
                      <span
                        className={material ? "font-medium text-warning" : "text-muted-foreground"}
                      >
                        {variance > 0 ? "+" : "−"}
                        {Math.abs(variance).toFixed(1)}%
                      </span>
                    )}
                  </td>
                </tr>
                {shown ? (
                  <tr>
                    <td colSpan={5} className="bg-surface-muted px-3 py-3">
                      <ul className="space-y-1 text-xs">
                        {l.components.map((c) => (
                          <li key={c.code} className="flex flex-wrap justify-between gap-2">
                            <span>
                              <span className="font-medium">{c.label}</span>
                              <span className="text-muted-foreground"> — {c.basis}</span>
                            </span>
                            <span className="tabular shrink-0">
                              {c.kind === "Deduction"
                                ? "−"
                                : c.kind === "Employer"
                                  ? "employer "
                                  : ""}
                              {money(c.amount, currency)}
                            </span>
                          </li>
                        ))}
                      </ul>
                      {payslipsReleased ? (
                        <Link
                          to="/hrm/payslips/$id"
                          params={{ id: `PS-${l.runId.replace("RUN-", "")}-${l.employeeId}` }}
                          className="mt-2 inline-block text-xs text-primary underline underline-offset-2"
                        >
                          Open the payslip {l.employee} sees
                        </Link>
                      ) : (
                        <p className="mt-2 text-xs text-muted-foreground">
                          Payslips are not released yet, so {l.employee} cannot see this.
                        </p>
                      )}
                    </td>
                  </tr>
                ) : null}
              </Fragment>
            );
          })}
        </tbody>
      </RecordTable>
    </div>
  );
}

function CalculationReview({
  run,
  rows,
  readiness,
}: {
  run: PayRun;
  rows: RunLine[];
  readiness: CalculationReadiness | null;
}) {
  if (!rows.length) {
    return (
      <p className="rounded-md border bg-surface-muted p-3 text-sm text-muted-foreground">
        Calculation review opens after the run has pay lines. Lock and calculate the run first.
      </p>
    );
  }

  const blockingLines = rows.filter((line) => line.flags.length > 0);
  const materialVariances = rows
    .map((line) => ({
      line,
      variance:
        line.priorNet && line.priorNet > 0
          ? ((line.net - line.priorNet) / line.priorNet) * 100
          : null,
    }))
    .filter((item) => item.variance !== null && Math.abs(item.variance) >= 2);
  const allComponents = rows.flatMap((line) => line.components);
  const statutoryTotal = allComponents
    .filter((component) => component.source === "Statutory")
    .reduce((sum, component) => sum + component.amount, 0);
  const overtimeTotal = allComponents
    .filter(
      (component) =>
        component.source === "Attendance" ||
        component.code.toLowerCase().includes("overtime") ||
        component.label.toLowerCase().includes("overtime"),
    )
    .reduce((sum, component) => sum + component.amount, 0);
  const employerTotal = allComponents
    .filter((component) => component.kind === "Employer")
    .reduce((sum, component) => sum + component.amount, 0);
  const prorationWarnings = readiness?.checks.find((check) => check.id === "proration")?.count ?? 0;
  const reviewReady =
    readiness?.ready !== false && blockingLines.length === 0 && run.status !== "Draft";

  const reviewCards = [
    {
      label: "Calculated workers",
      value: String(rows.length),
      detail: `${run.totals.headcount || rows.length} included in run totals`,
      tone: "text-foreground",
    },
    {
      label: "Gross to net",
      value: money(run.totals.net, run.currency),
      detail: `${money(run.totals.gross, run.currency)} gross less ${money(run.totals.deductions, run.currency)} deductions`,
      tone: "text-foreground",
    },
    {
      label: "Statutory deductions",
      value: money(statutoryTotal, run.currency),
      detail: "PAYE, NAPSA, NHIMA and configured statutory components from the engine",
      tone: "text-foreground",
    },
    {
      label: "Needs attention",
      value: String(blockingLines.length + materialVariances.length),
      detail: `${blockingLines.length} line exception${blockingLines.length === 1 ? "" : "s"}, ${materialVariances.length} material variance${materialVariances.length === 1 ? "" : "s"}`,
      tone: blockingLines.length || materialVariances.length ? "text-warning" : "text-success",
    },
  ];

  return (
    <div className="space-y-4">
      <div
        className={`rounded-lg border p-4 ${
          reviewReady ? "border-success/30 bg-success-soft" : "border-warning/40 bg-warning-soft"
        }`}
      >
        <p
          className={`flex items-start gap-2 text-sm font-semibold ${reviewReady ? "text-success" : "text-warning"}`}
        >
          {reviewReady ? (
            <Check className="mt-0.5 size-4 shrink-0" aria-hidden />
          ) : (
            <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
          )}
          {reviewReady
            ? "Calculation review ready for approval"
            : "Review required before approval"}
        </p>
        <p className="mt-2 text-xs text-foreground">
          Review the engine output below before approval. This page summarizes results only; salary
          components, tax slabs and statutory rates remain managed in payroll configuration.
        </p>
      </div>

      <div className="grid gap-3 md:grid-cols-4">
        {reviewCards.map((card) => (
          <div key={card.label} className="rounded-lg border bg-background p-3">
            <p className="text-xs font-medium text-muted-foreground">{card.label}</p>
            <p className={`mt-1 text-xl font-semibold tabular ${card.tone}`}>{card.value}</p>
            <p className="mt-1 text-xs text-muted-foreground">{card.detail}</p>
          </div>
        ))}
      </div>

      <div className="grid gap-3 md:grid-cols-3">
        <div className="rounded-lg border bg-background p-3">
          <p className="text-sm font-medium">Overtime and attendance</p>
          <p className="mt-1 text-lg font-semibold tabular">{money(overtimeTotal, run.currency)}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Approved attendance/overtime included in this calculation.
          </p>
        </div>
        <div className="rounded-lg border bg-background p-3">
          <p className="text-sm font-medium">Proration inputs</p>
          <p className="mt-1 text-lg font-semibold tabular">{prorationWarnings}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Approved unpaid leave records overlapping the period.
          </p>
        </div>
        <div className="rounded-lg border bg-background p-3">
          <p className="text-sm font-medium">Employer cost additions</p>
          <p className="mt-1 text-lg font-semibold tabular">{money(employerTotal, run.currency)}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            Employer-side configured contributions not deducted from worker net.
          </p>
        </div>
      </div>

      {blockingLines.length || materialVariances.length ? (
        <div className="rounded-lg border bg-background">
          <div className="border-b bg-surface-muted px-3 py-2">
            <p className="text-sm font-medium">Review exceptions</p>
          </div>
          <ul className="divide-y text-sm">
            {blockingLines.slice(0, 6).map((line) => (
              <li key={`flag-${line.id}`} className="flex flex-wrap gap-x-3 gap-y-1 px-3 py-2">
                <span className="font-medium">{line.employee}</span>
                <span className="text-warning">{line.flags.join(", ")}</span>
              </li>
            ))}
            {materialVariances.slice(0, 6).map(({ line, variance }) => (
              <li key={`variance-${line.id}`} className="flex flex-wrap gap-x-3 gap-y-1 px-3 py-2">
                <span className="font-medium">{line.employee}</span>
                <span className="text-warning">
                  Net changed {variance! > 0 ? "+" : "-"}
                  {Math.abs(variance!).toFixed(1)}% from prior period.
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}

function RunPayslipsSection({
  run,
  onChanged,
}: {
  run: OperationalPayRun;
  onChanged: () => Promise<unknown>;
}) {
  const slipsState = useApi(async () => {
    const raw = await realApi.payrollRunPayslips(run.id);
    return (Array.isArray(raw) ? raw : []) as Array<Record<string, unknown>>;
  }, [run.id]);

  return (
    <DetailSection
      title="Payslips"
      description="One payslip per released pay line. Generate PDFs for the whole run, or preview/download an individual one."
    >
      <Async state={slipsState} rows={4}>
        {(slips) => {
          if (!slips.length) {
            return (
              <div className="flex flex-wrap items-center gap-3">
                <p className="text-sm text-muted-foreground">
                  Run is released but no payslips were found — generate them.
                </p>
              </div>
            );
          }
          return (
            <div className="space-y-3">
              <div className="flex flex-wrap items-center gap-3">
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-2"
                  onClick={async () => {
                    try {
                      await realApi.payrollGenerateAllPayslips(run.id);
                      feedback.submitted(
                        "Payslip PDFs generated",
                        `All ${slips.length} payslip documents are ready. Re-open to see the download links.`,
                      );
                      await slipsState.reload();
                      await onChanged();
                    } catch (e) {
                      feedback.blocked(
                        "Payslip PDF generation failed",
                        e instanceof Error ? e.message : "Unknown error.",
                      );
                    }
                  }}
                >
                  <Download className="size-4" aria-hidden />
                  Generate PDFs for all payslips
                </Button>
                <span className="text-xs text-muted-foreground">
                  Idempotent — already-generated slips are returned as-is.
                </span>
              </div>
              <div className="overflow-x-auto">
                <RecordTable variant="compact" className="w-full text-left text-xs">
                  <thead>
                    <tr className="border-b bg-surface-muted text-muted-foreground">
                      <th className="px-2 py-1.5">Payslip</th>
                      <th className="px-2 py-1.5">Employee</th>
                      <th className="px-2 py-1.5 text-right">Net</th>
                      <th className="px-2 py-1.5">Status</th>
                      <th className="px-2 py-1.5" />
                    </tr>
                  </thead>
                  <tbody>
                    {slips.map((s) => (
                      <tr key={String(s.id ?? "")} className="border-b last:border-0">
                        <td className="px-2 py-1.5 font-mono text-primary">
                          {String(s.payslipNo ?? s.id ?? "")}
                        </td>
                        <td className="px-2 py-1.5">{String(s.employee ?? s.workerName ?? "")}</td>
                        <td className="px-2 py-1.5 text-right tabular">
                          {money(Number(s.netPay ?? 0), run.currency)}
                        </td>
                        <td className="px-2 py-1.5">
                          <span
                            className={
                              String(s.status ?? "") === "released" ||
                              String(s.status ?? "") === "final"
                                ? "text-success"
                                : "text-muted-foreground"
                            }
                          >
                            {String(s.status ?? "draft")}
                          </span>
                        </td>
                        <td className="px-2 py-1.5 text-right">
                          <Link
                            to="/hrm/payslips/$id"
                            params={{ id: String(s.id ?? "") }}
                            className="mr-2 text-primary underline underline-offset-2"
                          >
                            Open
                          </Link>
                          <button
                            type="button"
                            className="text-primary underline underline-offset-2"
                            onClick={async () => {
                              const blob = await realApi.payslipDownloadBlob(String(s.id ?? ""));
                              const url = URL.createObjectURL(blob);
                              const anchor = document.createElement("a");
                              anchor.href = url;
                              anchor.download = `${String(s.payslipNo ?? s.id ?? "payslip")}.pdf`;
                              anchor.click();
                              URL.revokeObjectURL(url);
                            }}
                          >
                            PDF
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </RecordTable>
              </div>
            </div>
          );
        }}
      </Async>
    </DetailSection>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

type OperationalPayRun = PayRun & {
  backendStatus: string;
  paymentStatus: string;
  paymentFileReference?: string;
  paymentFileGeneratedBySubjectId?: string;
  paymentApprovedBySubjectId?: string;
  paymentReleasedBySubjectId?: string;
  reconciliationReference?: string;
};

function adaptRun(raw: unknown, auditRows: unknown[] = []): OperationalPayRun {
  const r = raw as Record<string, unknown>;
  const statusMap: Record<string, string> = {
    draft: "Draft",
    calculated: "Calculated",
    "in-review": "In review",
    approved: "Approved",
    released: "Paid",
    paid: "Paid",
    locked: "Draft",
    closed: "Closed",
    reversed: "Reversed",
  };
  const status = statusMap[String(r.status ?? "")] ?? String(r.status ?? "Draft");
  return {
    id: String(r.id ?? ""),
    period: String(r.periodLabel ?? r.period ?? ""),
    entityId: "",
    entityName: String(r.entityName ?? "Demo Zambia Ltd"),
    payGroup: String(r.payGroup ?? "Monthly ZMW"),
    currency: String(r.currency ?? "ZMW"),
    status: status as PayRun["status"],
    owner: String(r.preparedBy ?? r.owner ?? "Payroll officer"),
    nextAction:
      status === "Calculated"
        ? "Send for review"
        : status === "In review"
          ? "Awaiting top-HR approval"
          : status === "Approved"
            ? "Release payslips"
            : status === "Paid"
              ? "Complete payment workflow"
              : status === "Closed"
                ? "Cycle complete"
                : "Calculate run",
    dueDate: String(r.cutoffDate ?? ""),
    branchId: r.locationId ? String(r.locationId) : undefined,
    preparedBy: String(r.preparedBySubjectId ?? r.preparedBy ?? "Payroll officer"),
    approvedBy: r.approvedBySubjectId ? String(r.approvedBySubjectId) : undefined,
    totals: {
      headcount: Number(r.employeeCount ?? 0),
      gross: Number(r.totalGross ?? 0),
      deductions: Number(r.totalDeductions ?? 0),
      employerCost: Number(r.totalEmployerCost ?? 0),
      net: Number(r.totalNet ?? 0),
    },
    included: Number(r.employeeCount ?? 0),
    excluded: [],
    stages: [],
    timeline: auditRows.map((rawEvent) => {
      const e = rawEvent as Record<string, unknown>;
      return {
        id: String(e.id ?? ""),
        at: String(e.createdAt ?? ""),
        actor: String(e.actorSubjectId ?? "system"),
        event: String(e.action ?? "Payroll action").replaceAll("-", " "),
        reason: e.reason ? String(e.reason) : undefined,
        before: e.fromStatus ? String(e.fromStatus) : undefined,
        after: e.toStatus ? String(e.toStatus) : undefined,
      };
    }),
    paymentStatus: String(r.paymentStatus ?? "not-created"),
    backendStatus: String(r.status ?? "draft"),
    paymentFileReference: r.paymentFileReference ? String(r.paymentFileReference) : undefined,
    paymentFileGeneratedBySubjectId: r.paymentFileGeneratedBySubjectId
      ? String(r.paymentFileGeneratedBySubjectId)
      : undefined,
    paymentApprovedBySubjectId: r.paymentApprovedBySubjectId
      ? String(r.paymentApprovedBySubjectId)
      : undefined,
    paymentReleasedBySubjectId: r.paymentReleasedBySubjectId
      ? String(r.paymentReleasedBySubjectId)
      : undefined,
    reconciliationReference: r.reconciliationReference
      ? String(r.reconciliationReference)
      : undefined,
  };
}

function adaptLines(raw: unknown, runId: string): RunLine[] {
  const envelope = raw as { items?: unknown[] };
  return (envelope.items ?? []).map((item) => {
    const l = item as Record<string, unknown>;
    const status = String(l.exceptionStatus ?? "open");
    const components = (l.components as Record<string, unknown>[] | undefined) ?? [];
    return {
      id: String(l.id ?? ""),
      runId,
      employeeId: String(l.workerId ?? ""),
      employee: String(l.workerName ?? ""),
      jobTitle: "",
      grade: "",
      components: components.map((c) => ({
        code: String(c.componentCode ?? ""),
        label: String(c.componentName ?? ""),
        kind:
          String(c.componentType ?? "earning") === "earning"
            ? "Earning"
            : String(c.componentType ?? "") === "employer-contribution"
              ? "Employer"
              : "Deduction",
        amount: Number(c.amount ?? 0),
        source: c.isStatutory ? "Statutory" : "One-off",
        basis: String(c.explanation ?? ""),
        inputs: [],
        ruleVersion: "engine-v1",
        effectiveFrom: "",
        explanation: String(c.explanation ?? ""),
      })),
      gross: Number(l.grossPay ?? 0),
      deductions: Number(l.totalDeductions ?? 0),
      employerCost: Number(l.employerCost ?? 0),
      net: Number(l.netPay ?? 0),
      flags:
        l.hasException && status === "open"
          ? [String(l.exceptionReason ?? "Payroll exception")]
          : [],
    } as RunLine;
  });
}

function TopAdminRunControls({
  run,
  isTopAdmin,
  onChanged,
}: {
  run: OperationalPayRun;
  isTopAdmin: boolean;
  onChanged: () => Promise<void>;
}) {
  const [mode, setMode] = useState<"cancel" | "reverse" | null>(null);
  const [reason, setReason] = useState("");
  const [working, setWorking] = useState(false);
  if (!USE_REAL || !isTopAdmin) return null;

  const canCancel = ["draft", "locked", "calculated", "in-review", "approved"].includes(
    run.backendStatus,
  );
  const canReverse = ["released", "closed"].includes(run.backendStatus);
  const terminal = run.backendStatus === "reversed";
  const reasonReady = reason.trim().length >= 5;
  const activeTitle = mode === "cancel" ? "Cancel this payroll run" : "Reverse this payroll run";
  const activeDescription =
    mode === "cancel"
      ? "Use this before payslips are released. The run will stay visible, but it will be marked as cancelled so you can create the correct run."
      : "Use this after payslips are released. The system will create a reversal run instead of deleting payroll history.";

  const submit = async () => {
    if (!mode || !reasonReady) return;
    setWorking(true);
    try {
      if (mode === "cancel") {
        await realApi.payrollRunCancel(run.id, reason.trim());
        feedback.submitted(
          "Payroll run voided.",
          "The original run remains in audit history and a replacement run can now be created.",
        );
      } else {
        await realApi.payrollRunReverse(run.id, reason.trim());
        feedback.submitted(
          "Reversal run created.",
          "Review and release the reversal run to complete the payroll correction.",
        );
      }
      setMode(null);
      setReason("");
      await onChanged();
    } catch (e) {
      feedback.blocked(
        mode === "cancel" ? "Void failed" : "Reversal failed",
        e instanceof Error ? e.message : "Unknown error.",
      );
    } finally {
      setWorking(false);
    }
  };

  return (
    <DetailSection
      title="Payroll correction controls"
      description="Fix payroll mistakes safely. The system keeps a record of what changed and who changed it."
    >
      {terminal ? (
        <div className="rounded-lg border bg-surface-muted p-4 text-sm text-muted-foreground">
          Current payroll status: {run.backendStatus.replaceAll("-", " ")}. This run is already
          finished, so no correction button is available here. Check the audit trail to see what
          happened.
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border bg-surface-muted p-4">
          <Button
            type="button"
            variant="outline"
            disabled={!canCancel}
            onClick={() => setMode("cancel")}
          >
            <X className="size-4" aria-hidden />
            Cancel before payslips are released
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={!canReverse}
            onClick={() => setMode("reverse")}
          >
            <RotateCcw className="size-4" aria-hidden />
            Reverse after payslips are released
          </Button>
          <p className="min-w-[16rem] flex-1 text-sm text-muted-foreground">
            Current payroll status: {run.backendStatus.replaceAll("-", " ")}.
            {canCancel
              ? " You can cancel this run because payslips have not been released yet."
              : canReverse
                ? " Payslips have already been released, so use a reversal instead of deleting the run."
                : " No correction action is available for this payroll status."}
          </p>
        </div>
      )}
      <Dialog
        open={mode !== null}
        onOpenChange={(open) => {
          if (!open && !working) {
            setMode(null);
            setReason("");
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{activeTitle}</DialogTitle>
            <DialogDescription>{activeDescription}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="payroll-correction-reason">Why are you doing this?</Label>
            <Textarea
              id="payroll-correction-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Example: wrong pay period was selected, so I need to create the correct run."
              rows={4}
            />
            <p className="text-xs text-muted-foreground">
              This is required. It will show in the payroll history.
            </p>
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              disabled={working}
              onClick={() => setMode(null)}
            >
              Do not change it
            </Button>
            <Button
              type="button"
              variant={mode === "reverse" ? "destructive" : "default"}
              disabled={!reasonReady || working}
              onClick={submit}
            >
              {working ? "Working..." : mode === "cancel" ? "Cancel run" : "Create reversal"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </DetailSection>
  );
}

function RunDetail() {
  const { id } = Route.useParams();
  const { user } = useAuth();
  const state = useApi(async (): Promise<OperationalPayRun | null> => {
    if (!USE_REAL) {
      const mock = await payrollRunApi.run(id);
      return mock
        ? { ...mock, backendStatus: mock.status.toLowerCase(), paymentStatus: "not-created" }
        : null;
    }
    try {
      const [run, audit] = await Promise.all([realApi.payrollRun(id), realApi.payrollRunAudit(id)]);
      return adaptRun(run, audit);
    } catch {
      return null;
    }
  }, [id]);
  // M46: branch names for the branch pill — fetched once per view, best effort.
  const [scopeLocations, setScopeLocations] = useState<{ id: string; name: string }[]>([]);
  useEffect(() => {
    if (!USE_REAL) return;
    let live = true;
    realApi
      .locations()
      .then((raw) => {
        if (!live) return;
        const items = Array.isArray(raw)
          ? raw
          : (((raw as Record<string, unknown>)?.items as unknown[]) ?? []);
        setScopeLocations(
          items.map((l) => ({
            id: String((l as Record<string, unknown>).id ?? ""),
            name: String((l as Record<string, unknown>).name ?? ""),
          })),
        );
      })
      .catch(() => {});
    return () => {
      live = false;
    };
  }, []);
  const mockExceptions = useMock(() => payrollRunApi.exceptionsFor(id), [id]);
  const lines = useApi(async (): Promise<RunLine[]> => {
    if (!USE_REAL) return payrollRunApi.linesFor(id);
    try {
      return adaptLines(await realApi.payrollRunLines(id), id);
    } catch {
      return [];
    }
  }, [id]);
  /** M24: who blocks the release gate — loaded only while the run can still be released. */
  const readiness = useApi(async () => {
    if (!USE_REAL) return null;
    try {
      return await realApi.payrollRunStatutoryReadiness(id);
    } catch {
      return null;
    }
  }, [id]);
  const calculationReadiness = useApi(async (): Promise<CalculationReadiness | null> => {
    if (!USE_REAL) return null;
    try {
      const raw = (await realApi.payrollCalculationReadiness(id)) as Record<string, unknown>;
      const checks = Array.isArray(raw.checks) ? (raw.checks as Record<string, unknown>[]) : [];
      const issues = Array.isArray(raw.issues) ? (raw.issues as Record<string, unknown>[]) : [];
      return {
        ready: Boolean(raw.ready),
        includedWorkerCount: Number(raw.includedWorkerCount ?? 0),
        blockingCount: Number(raw.blockingCount ?? 0),
        warningCount: Number(raw.warningCount ?? 0),
        checks: checks.map((check) => ({
          id: String(check.id ?? ""),
          label: String(check.label ?? "Check"),
          state: String(check.state ?? "warn"),
          detail: String(check.detail ?? ""),
          count: Number(check.count ?? 0),
        })),
        issues: issues.map((issue) => ({
          workerId: issue.workerId ? String(issue.workerId) : undefined,
          employeeNo: String(issue.employeeNo ?? ""),
          workerName: String(issue.workerName ?? ""),
          issue: String(issue.issue ?? "Calculation readiness issue"),
          severity: String(issue.severity ?? "warn"),
        })),
      };
    } catch {
      return null;
    }
  }, [id]);
  const [calculating, setCalculating] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [locking, setLocking] = useState(false);
  const [activeTab, setActiveTab] = useState<PayrollRunTab>(() => {
    if (typeof window === "undefined") return "overview";
    const saved = window.sessionStorage.getItem(payrollRunTabKey(id));
    return saved === "calculate" || saved === "review" || saved === "approval" || saved === "release" ? saved : "overview";
  });
  const selectTab = (tab: PayrollRunTab) => {
    setActiveTab(tab);
    window.sessionStorage.setItem(payrollRunTabKey(id), tab);
  };
  useEffect(() => {
    const saved = window.sessionStorage.getItem(payrollRunTabKey(id));
    setActiveTab(saved === "calculate" || saved === "review" || saved === "approval" || saved === "release" ? saved : "overview");
  }, [id]);

  // `/payroll/runs/$id/edit` is generated as a child of this route.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
      <AppShell>
        <Async state={state} rows={4}>
          {(run) => {
            if (!run) return <RestrictedState />;

            const currentSubjectId = user?.id ? String(user.id) : "";
            const selfApproval = USE_REAL
              ? Boolean(currentSubjectId && run.preparedBy === currentSubjectId)
              : false;
            // An exception that has been resolved, waived or excluded no longer
            // holds the run up — that is the point of dealing with it.
            const liveBlocking = (lines.data ?? []).flatMap((line) =>
              line.flags.map((flag) => ({
                id: `${line.id}-${flag}`,
                severity: "Blocking" as const,
                kind: "Payroll exception",
                affects: line.employee,
                what: flag,
                impact: "This line must be corrected, waived or excluded before approval.",
                recommended:
                  "Open Payroll exceptions or recalculate after fixing the worker input.",
                escalation: "Payroll owner",
                resolvable: true,
              })),
            );
            const blocking = USE_REAL
              ? liveBlocking
              : (mockExceptions.data ?? []).filter(
                  (e) => e.severity === "Blocking" && isOutstanding(e),
                );
            const approvalReady = USE_REAL
              ? run.backendStatus === "calculated" || run.backendStatus === "in-review"
              : run.status === "Calculated" || run.status === "In review";
            const isTopAdmin = (user?.roles ?? []).some(
              (role) => role.toLowerCase() === "hr_admin",
            );
            const canApprove = (!selfApproval || isTopAdmin) && blocking.length === 0;
            const firstLiveExample = lines.data?.find((line) => line.components.length)
              ?.components[0];
            const firstLiveLine = lines.data?.find((line) => line.components.length);
            const canCalculate =
              USE_REAL &&
              !calculating &&
              calculationReadiness.data?.ready !== false &&
              ["draft", "locked", "calculated"].includes(run.backendStatus);
            const calculateLabel =
              run.backendStatus === "calculated"
                ? "Recalculate"
                : run.backendStatus === "draft"
                  ? "Lock and calculate"
                  : "Calculate";
            const canSubmitForReview =
              USE_REAL &&
              !submitting &&
              run.backendStatus === "calculated" &&
              Boolean(run.branchId);
            const canLockInputs =
              USE_REAL &&
              !locking &&
              calculationReadiness.data?.ready !== false &&
              run.backendStatus === "draft";
            const runCalculate = async () => {
              setCalculating(true);
              try {
                if (run.backendStatus === "draft") {
                  await realApi.lockPayrollRun(run.id);
                }
                await realApi.calculatePayrollRun(run.id);
                selectTab("review");
                feedback.submitted(
                  "Calculation complete.",
                  "Review pay lines and variances before sending for review.",
                );
                await state.reload();
                await lines.reload();
                await calculationReadiness.reload();
              } catch (e) {
                feedback.blocked(
                  "Calculation failed",
                  e instanceof Error ? e.message : "Unknown error.",
                );
              } finally {
                setCalculating(false);
              }
            };
            const submitForReview = async () => {
              setSubmitting(true);
              try {
                await realApi.submitPayrollRun(run.id);
                selectTab("approval");
                feedback.submitted(
                  "Branch run sent for review.",
                  "Organisation-wide HR can now review and approve this draft.",
                );
                await state.reload();
              } catch (e) {
                feedback.blocked(
                  "Submit failed",
                  e instanceof Error ? e.message : "Unknown error.",
                );
              } finally {
                setSubmitting(false);
              }
            };
            const lockInputs = async () => {
              setLocking(true);
              try {
                await realApi.lockPayrollRun(run.id);
                feedback.submitted("Payroll inputs locked.", "The run is ready to calculate.");
                await state.reload();
                await calculationReadiness.reload();
              } catch (e) {
                feedback.blocked("Lock failed", e instanceof Error ? e.message : "Unknown error.");
              } finally {
                setLocking(false);
              }
            };

            return (
              <RecordDetail
                reference={run.id}
                title={`${run.period} — ${run.payGroup}`}
                subtitle={`${run.entityName} · ${run.included} employees`}
                status={run.status}
                owner={run.owner}
                nextAction={`${run.nextAction} · due ${run.dueDate}`}
                summary={[
                  { label: "Period", value: run.period },
                  { label: "Entity", value: run.entityName },
                  { label: "Pay group", value: run.payGroup },
                  { label: "Prepared by", value: run.preparedBy },
                  {
                    label: "Approved by",
                    value: run.approvedBy ?? (
                      <span className="text-muted-foreground">Not yet approved</span>
                    ),
                  },
                  { label: "Currency", value: run.currency },
                  run.branchId
                    ? {
                        label: "Branch",
                        value:
                          scopeLocations?.find((l) => l.id === run.branchId)?.name ?? run.branchId,
                      }
                    : { label: "Scope", value: "Organisation-wide" },
                ]}
                timeline={<StatusTimeline title="Audit trail" events={run.timeline} />}
              >
                <Tabs value={activeTab} onValueChange={(value) => selectTab(value as PayrollRunTab)} className="space-y-4">
                  <div className="sticky top-0 z-10 -mx-1 overflow-x-auto bg-background/95 px-1 py-2 backdrop-blur supports-[backdrop-filter]:bg-background/75">
                    <TabsList className="h-auto min-w-max justify-start gap-1">
                      <TabsTrigger value="overview">Overview</TabsTrigger>
                      <TabsTrigger value="calculate">Calculate</TabsTrigger>
                      <TabsTrigger value="review">Review</TabsTrigger>
                      <TabsTrigger value="approval">Approval</TabsTrigger>
                      <TabsTrigger value="release">Release & payment</TabsTrigger>
                    </TabsList>
                  </div>

                  <TabsContent value="overview" className="space-y-4">
                    <TopAdminRunControls
                      run={run}
                      isTopAdmin={isTopAdmin}
                      onChanged={async () => {
                        await state.reload();
                      }}
                    />

                    {run.stages.length ? (
                      <DetailSection
                        title="Stages"
                        description="Payslip release, payment and accounting are separate stages on purpose — releasing a payslip does not move money."
                      >
                        <Stages stages={run.stages} />
                      </DetailSection>
                    ) : null}
                  </TabsContent>

                  <TabsContent value="calculate" className="space-y-4">
                    {USE_REAL ? (
                      <div className="rounded-lg border bg-surface p-4">
                        <div className="flex flex-wrap items-start justify-between gap-4">
                          <div className="min-w-0 flex-1">
                            <p className="text-sm font-semibold">Calculation actions</p>
                            <p className="mt-1 text-sm text-muted-foreground">
                              Current status:{" "}
                              <span className="font-medium text-foreground">
                                {run.backendStatus.replaceAll("-", " ")}
                              </span>
                              . Use this first, then review the calculated lines.
                            </p>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            <Button
                              disabled={!canCalculate}
                              onClick={() => {
                                void runCalculate();
                              }}
                            >
                              {calculating ? "Calculating..." : calculateLabel}
                            </Button>
                            <Button
                              variant="outline"
                              disabled={!canSubmitForReview}
                              onClick={() => {
                                void submitForReview();
                              }}
                            >
                              {submitting ? "Sending..." : "Send for review"}
                            </Button>
                            <Button
                              variant="outline"
                              className="gap-1.5"
                              disabled={!canLockInputs}
                              onClick={() => {
                                void lockInputs();
                              }}
                            >
                              {locking ? "Locking..." : "Lock inputs"}
                            </Button>
                          </div>
                        </div>
                        {calculationReadiness.data?.ready === false ? (
                          <p className="mt-3 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
                            Calculation is blocked until the readiness issues below are fixed.
                          </p>
                        ) : run.backendStatus === "calculated" ? (
                          <p className="mt-3 rounded-md border bg-surface-muted p-3 text-sm text-muted-foreground">
                            This run is already calculated. Use <strong>Recalculate</strong> only
                            after changing inputs or configuration.
                          </p>
                        ) : null}
                      </div>
                    ) : null}

                    <DetailSection
                      title="Calculate"
                      description="Gross to net for every included employee. The payroll engine does the work; this shows what it did, employee by employee."
                    >
                      <CalculationReadinessCard readiness={calculationReadiness.data} />
                      <CalculationPanel
                        runId={run.id}
                        locked={run.status === "Paid" || run.status === "Closed"}
                        liveCalculatedCount={USE_REAL ? (lines.data?.length ?? 0) : undefined}
                        liveTotalCount={USE_REAL ? run.included : undefined}
                        liveStatus={USE_REAL ? run.backendStatus : undefined}
                        lockedReason={
                          run.status === "Closed"
                            ? "This period is closed and locked. A change now needs a correction run."
                            : "Payments have been released, so recalculating would change figures people have already been paid on."
                        }
                      />
                    </DetailSection>
                  </TabsContent>

                  <TabsContent value="review" className="space-y-4">
                    <DetailSection
                      title="Calculation review"
                      description="One checkpoint before approval: control totals, statutory deductions, overtime, proration inputs and employee-level exceptions."
                    >
                      <Async state={lines} rows={3}>
                        {(rows) => (
                          <CalculationReview
                            run={run}
                            rows={rows}
                            readiness={calculationReadiness.data}
                          />
                        )}
                      </Async>
                    </DetailSection>

                    <DetailSection
                      title="Pay lines"
                      description="Every employee in this run. Open a name to see how each figure was derived — no line is a black box."
                      action={
                        <Button variant="outline" size="sm" asChild>
                          <Link to="/hrm/payroll/runs/$id/edit" params={{ id: run.id }}>
                            Edit this run
                          </Link>
                        </Button>
                      }
                    >
                      <Async state={lines} rows={4}>
                        {(rows) =>
                          rows.length ? (
                            <PayLines
                              rows={rows}
                              currency={run.currency}
                              payslipsReleased={
                                run.status === "Paid" ||
                                run.status === "Closed" ||
                                run.stages.some((st) => st.id === "s7" && st.state === "done")
                              }
                            />
                          ) : (
                            <p className="text-sm text-muted-foreground">
                              Nothing has been calculated yet, so this run has no pay lines. They
                              appear once the calculate stage completes.
                            </p>
                          )
                        }
                      </Async>
                    </DetailSection>

                    {/* M34: admin payslip surface — list of payslips for this run with bulk PDF generate */}
                    {USE_REAL &&
                    (run.status === "Paid" ||
                      run.status === "Closed" ||
                      run.backendStatus === "released") ? (
                      <RunPayslipsSection
                        run={run}
                        onChanged={async () => {
                          await state.reload();
                        }}
                      />
                    ) : null}

                    <DetailSection
                      title="Control totals"
                      description="Compared with the previous period. Anything moving 2% or more is flagged as material and needs an explanation before approval."
                    >
                      <Totals run={run} />
                    </DetailSection>

                    {run.excluded.length ? (
                      <DetailSection
                        title="Excluded from this run"
                        description="Exclusion is deliberate and recorded — an employee is never silently left out."
                      >
                        <ul className="space-y-2 text-sm">
                          {run.excluded.map((x) => (
                            <li key={x.employee} className="flex gap-2">
                              <Ban
                                className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                                aria-hidden
                              />
                              <span>
                                <span className="font-medium">{x.employee}</span>
                                <span className="block text-xs text-muted-foreground">
                                  {x.reason}
                                </span>
                              </span>
                            </li>
                          ))}
                        </ul>
                      </DetailSection>
                    ) : null}

                    {firstLiveExample && firstLiveLine ? (
                      <DetailSection
                        title="Worked example — how one figure was derived"
                        description="Every calculated line can be explained. This is the same component used on an employee payslip."
                      >
                        <CalculationExplainer
                          currency={run.currency}
                          caption={`${firstLiveExample.label} for ${firstLiveLine.employee}, ${run.period}.`}
                          lines={[
                            {
                              code: firstLiveExample.code,
                              label: firstLiveExample.label,
                              amount: firstLiveExample.amount,
                              inputs: firstLiveExample.inputs,
                              ruleVersion: firstLiveExample.ruleVersion,
                              effectiveFrom: firstLiveExample.effectiveFrom,
                              explanation: firstLiveExample.explanation || firstLiveExample.basis,
                            },
                          ]}
                        />
                      </DetailSection>
                    ) : null}
                  </TabsContent>

                  <TabsContent value="approval" className="space-y-4">
                    {run.status !== "Closed" && run.status !== "Paid" ? (
                      <DetailSection
                        title="Approval"
                        description={
                          run.branchId
                            ? "Branch payroll drafts flow up for organisation-wide HR approval. Top HR approves; branch HR cannot approve their own draft."
                            : "Segregation of duties is enforced here, not assumed."
                        }
                      >
                        {!approvalReady ? (
                          <div className="rounded-lg border bg-surface-muted p-4">
                            <p className="flex items-start gap-2 text-sm font-medium">
                              <Lock
                                className="mt-0.5 size-4 shrink-0 text-muted-foreground"
                                aria-hidden
                              />
                              Approval opens after calculation
                            </p>
                            <p className="mt-2 text-sm text-muted-foreground">
                              Current backend status is {run.backendStatus.replaceAll("-", " ")}.
                              Lock and calculate the run before sending it for approval.
                            </p>
                          </div>
                        ) : selfApproval && !isTopAdmin ? (
                          <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
                            <p className="flex items-start gap-2 text-sm font-medium text-danger">
                              <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                              You prepared this run, so you cannot approve it
                            </p>
                            <p className="mt-2 text-sm text-foreground">
                              {run.preparedBy} prepared this run. Approval must come from a
                              different authorised person — this is what stops one individual
                              creating and releasing a payment on their own.
                            </p>
                            <Button variant="outline" className="mt-3" disabled>
                              Approve run — not available to you
                            </Button>
                            <p className="mt-2 text-xs text-muted-foreground">
                              Send it to an approver instead. The attempt and the reason it was
                              blocked are both recorded.
                            </p>
                          </div>
                        ) : selfApproval && isTopAdmin ? (
                          <ApprovalPanel
                            decisionSummary={`Emergency top-admin approval for ${run.period}: you prepared this run, but your HR administrator role is allowed to override maker-checker during setup or correction. Net pay ${money(run.totals.net, run.currency)}.`}
                            policy={[
                              {
                                id: "p1",
                                label: "Top-admin override",
                                outcome: "warn",
                                detail:
                                  "You prepared this run. Approval is allowed because your account has the HR administrator role and the action is written to the audit trail.",
                              },
                              {
                                id: "p2",
                                label: "Blocking exceptions",
                                outcome: "pass",
                                detail: "None outstanding.",
                              },
                            ]}
                            conflicts={[]}
                            onDecision={async (decision, reason) => {
                              if (decision === "approve") {
                                try {
                                  await realApi.payrollRunApprove(
                                    run.id,
                                    reason ||
                                      "Top-admin self-approval override during controlled setup",
                                  );
                                  selectTab("release");
                                  feedback.submitted(
                                    `${run.period} approved by top admin.`,
                                    "The override reason was recorded in the payroll audit trail.",
                                  );
                                  await state.reload();
                                  return;
                                } catch (error) {
                                  feedback.blocked(
                                    "Approval failed",
                                    error instanceof Error ? error.message : "Unknown error.",
                                  );
                                  return;
                                }
                              }
                              feedback.note(
                                "Decision not applied.",
                                reason ||
                                  "Approve the run only when the top-admin override is intended.",
                              );
                            }}
                          />
                        ) : blocking.length ? (
                          <div className="rounded-lg border border-warning/40 bg-warning-soft p-4">
                            <p className="flex items-start gap-2 text-sm font-medium text-warning">
                              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
                              {blocking.length} blocking exception{blocking.length === 1 ? "" : "s"}{" "}
                              must be resolved first
                            </p>
                            <Button variant="outline" className="mt-3" asChild>
                              <Link to="/hrm/payroll/exceptions">Review exceptions</Link>
                            </Button>
                          </div>
                        ) : (
                          <ApprovalPanel
                            decisionSummary={`Approve ${run.period} for ${run.included} employees at ${run.entityName}. Net pay ${money(run.totals.net, run.currency)}.`}
                            policy={[
                              {
                                id: "p1",
                                label: "Segregation of duties",
                                outcome: "pass",
                                detail: `Prepared by ${run.preparedBy}; you are a different person.`,
                              },
                              {
                                id: "p2",
                                label: "Blocking exceptions",
                                outcome: "pass",
                                detail: "None outstanding.",
                              },
                              {
                                id: "p3",
                                label: "Material variance explained",
                                outcome: "warn",
                                detail: "One variance above 2% has a recorded explanation.",
                              },
                            ]}
                            conflicts={[]}
                            onDecision={async (decision, reason) => {
                              if (USE_REAL && decision === "approve") {
                                try {
                                  await realApi.payrollRunApprove(run.id);
                                  selectTab("release");
                                  feedback.submitted(
                                    `${run.period} approved for ${run.included} employees.`,
                                    run.branchId
                                      ? "Branch draft merged into the mainstream payroll. Payslips can now be released."
                                      : "Payslips can now be released. Releasing them does not pay anyone.",
                                  );
                                  await state.reload();
                                  return;
                                } catch (e) {
                                  feedback.blocked(
                                    "Approval failed",
                                    e instanceof Error ? e.message : "Unknown error.",
                                  );
                                  return;
                                }
                              }
                              if (decision === "approve") {
                                feedback.submitted(
                                  `${run.period} approved for ${run.included} employees.`,
                                  "Payslips can now be released. Releasing them does not pay anyone.",
                                );
                              } else if (USE_REAL && decision === "reject") {
                                try {
                                  await realApi.payrollRunCancel(
                                    run.id,
                                    reason || "Rejected during payroll approval",
                                  );
                                  feedback.submitted(
                                    `${run.period} rejected and voided.`,
                                    "The preparer will see your reason in the audit trail and can recalculate.",
                                  );
                                  await state.reload();
                                  return;
                                } catch (e) {
                                  feedback.blocked(
                                    "Rejection failed",
                                    e instanceof Error ? e.message : "Unknown error.",
                                  );
                                  return;
                                }
                              } else if (decision === "return" || decision === "reject") {
                                feedback.submitted(
                                  `${run.period} sent back to ${run.preparedBy}.`,
                                  reason ||
                                    "The preparer will see your reason and can recalculate.",
                                );
                              } else {
                                feedback.note(
                                  "Decision delegated.",
                                  reason ||
                                    "The delegate must still be someone other than the preparer.",
                                );
                              }
                            }}
                          />
                        )}
                        {canApprove ? null : null}
                      </DetailSection>
                    ) : (
                      <DetailSection
                        title="Approval"
                        description="This payroll run has already passed approval."
                      >
                        <p className="rounded-md border bg-surface-muted p-4 text-sm text-muted-foreground">
                          Approval is no longer available because the run status is {run.status}.
                        </p>
                      </DetailSection>
                    )}
                  </TabsContent>

                  <TabsContent value="release" className="space-y-4">
                    <DetailSection
                      title="Release"
                      description="Payslip release, payment file, bank approval and reconciliation are separate controls."
                    >
                      <StatutoryReadinessCard readiness={readiness.data} />
                      <div className={readiness.data ? "mt-3" : ""}>
                        <ReleaseActions
                          run={run}
                          readiness={readiness.data}
                          onReleased={async () => {
                            selectTab("release");
                            await readiness.reload();
                            await state.reload();
                          }}
                        />
                        {USE_REAL ? (
                          <PaymentWorkflow
                            run={run}
                            onChanged={async () => {
                              await state.reload();
                            }}
                          />
                        ) : null}
                      </div>
                      <p className="mt-3 flex gap-2 text-xs text-muted-foreground">
                        <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                        {USE_REAL
                          ? "Payslip release is recorded against the run; payment and ledger posting are handled downstream."
                          : "Nothing in this build pays anyone, files anything or posts to a ledger."}
                      </p>
                    </DetailSection>
                  </TabsContent>
                </Tabs>
              </RecordDetail>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
