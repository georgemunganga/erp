import { RecordTable } from "@/platform/components/RecordTable";
/**
 * M48: the top-HR payroll approval queue.
 *
 * Branch payroll runs prepared by branch HR are drafts until organisation-wide
 * HR approve them. This screen is the single place where those in-review runs
 * surface, each with its control totals, the branch it belongs to, who prepared
 * it, and how long it has been waiting since submission — so the approver can
 * sanity-check the numbers before opening the run itself.
 *
 * Confinement is enforced twice: the backend refuses branch-confined users
 * with 403, and this page shows an explanation screen instead of the queue.
 */
import { createFileRoute, Link } from "@tanstack/react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { money } from "@/mock/payrollrun";
import type { PayQueueItem } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/payroll/queue")({
  head: () => ({
    meta: [
      { title: "Payroll approval queue — Newworldcargo HRM" },
      {
        name: "description",
        content: "Branch payroll runs awaiting organisation-wide HR approval, with control totals and submission stamps.",
      },
    ],
  }),
  component: PayrollQueue,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function backendStatusToLabel(status: string): string {
  return status === "in-review" ? "In review" : status === "calculated" ? "Awaiting submission" : status;
}

function timeAgo(iso?: string): string {
  if (!iso) return "—";
  const diffMs = Date.now() - new Date(iso).getTime();
  if (isNaN(diffMs)) return "—";
  const mins = Math.round(diffMs / 60_000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

function adaptQueueRows(rows: unknown[]): PayQueueItem[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    return {
      runId: String(r.runId ?? ""),
      status: String(r.status ?? "in-review"),
      periodLabel: String(r.periodLabel ?? ""),
      branchId: r.branchId ? String(r.branchId) : undefined,
      branchName: r.branchName ? String(r.branchName) : undefined,
      entityId: String(r.entityId ?? ""),
      employeeCount: Number(r.employeeCount ?? 0),
      totalGross: Number(r.totalGross ?? 0),
      totalNet: Number(r.totalNet ?? 0),
      totalDeductions: Number(r.totalDeductions ?? 0),
      totalEmployerCost: Number(r.totalEmployerCost ?? 0),
      exceptionCount: Number(r.exceptionCount ?? 0),
      preparedBySubjectId: r.preparedBySubjectId ? String(r.preparedBySubjectId) : undefined,
      submittedAt: r.submittedAt ? String(r.submittedAt) : undefined,
      createdAt: String(r.createdAt ?? ""),
    } satisfies PayQueueItem;
  });
}

function summaryOf(rows: PayQueueItem[]) {
  return {
    runs: rows.length,
    employees: rows.reduce((s, r) => s + r.employeeCount, 0),
    gross: rows.reduce((s, r) => s + r.totalGross, 0),
    net: rows.reduce((s, r) => s + r.totalNet, 0),
    deductions: rows.reduce((s, r) => s + r.totalDeductions, 0),
    employerCost: rows.reduce((s, r) => s + r.totalEmployerCost, 0),
    exceptions: rows.reduce((s, r) => s + r.exceptionCount, 0),
  };
}

/** M44/45: reads the resolved scope to decide whether this viewer is confined. */
function useShellScope() {
  return useApi(
    () => realApi.shell(),
    [],
  );
}

function ConfinedScreen() {
  return (
    <Card className="mx-auto max-w-2xl border-dashed">
      <CardHeader>
        <CardTitle>This queue is for organisation-wide HR</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm text-muted-foreground">
        <p>
          Your account is assigned to a branch, so every branch payroll run you
          prepare flows up to organisation-wide HR for approval — and you
          cannot open this queue yourself.
        </p>
        <p>
          While a run waits here, your branch's work is visible under{" "}
          <Link
            to="/hrm/payroll/runs"
            className="text-primary underline-offset-2 hover:underline"
          >
            Pay runs
          </Link>{" "}
          marked <em>In review</em>. Approve, return or reject decisions
          belong to top HR, not the branch that prepared the figures — that is
          the segregation of duties the workflow enforces.
        </p>
      </CardContent>
    </Card>
  );
}

function PayrollQueue() {
  const queueState = useApi(async (): Promise<PayQueueItem[]> => {
    const rows = await realApi.payrollQueue();
    return adaptQueueRows(rows);
  }, []);
  const shell = useShellScope();
  const confined = shell.data?.confined === true;

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Payroll"
          title="Approval queue"
          description="Branch payroll runs waiting for organisation-wide HR to approve. Each row carries the control totals computed for that branch — check the numbers before opening the run."
        />

        {confined ? (
          <ConfinedScreen />
        ) : (
          <Async state={queueState} rows={6}>
            {(rows) => {
              const summary = summaryOf(rows);
              const statCard = (label: string, value: string, tone?: string) => (
                <Card key={label}>
                  <CardHeader className="pb-1 pt-4">
                    <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      {label}
                    </span>
                  </CardHeader>
                  <CardContent className="pb-4">
                    <span className={`text-2xl font-semibold tabular ${tone ?? ""}`}>
                      {value}
                    </span>
                  </CardContent>
                </Card>
              );
              return (
                <div className="space-y-6">
                  <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
                    {statCard("Runs waiting", String(summary.runs))}
                    {statCard("Employees covered", String(summary.employees))}
                    {statCard("Total gross", money(summary.gross, "ZMW"), "font-medium text-foreground")}
                    {statCard("Total net", money(summary.net, "ZMW"), "font-medium text-foreground")}
                    {statCard("Deductions", money(summary.deductions, "ZMW"))}
                    {statCard("Employer cost", money(summary.employerCost, "ZMW"))}
                  </div>

                  {summary.exceptions > 0 && (
                    <div className="rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                      {summary.exceptions} unresolved payroll exception{summary.exceptions === 1 ? "" : "s"} across
                      these runs — each one needs a decision before approval.
                    </div>
                  )}

                  {rows.length === 0 ? (
                    <Card className="border-dashed">
                      <CardContent className="py-16 text-center">
                        <p className="text-lg font-medium">Nothing is waiting for review</p>
                        <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
                          No branch payroll runs are in review or awaiting submission. When branch HR
                          calculate a run and send it up, it will appear here with its control totals.
                        </p>
                        <Link
                          to="/hrm/payroll/runs"
                          className="mt-4 inline-block text-sm text-primary underline-offset-2 hover:underline"
                        >
                          View all pay runs
                        </Link>
                      </CardContent>
                    </Card>
                  ) : (
                    <Card>
                      <CardContent className="p-0">
                        <div className="overflow-x-auto">
                        <RecordTable className="min-w-[900px] caption-bottom text-sm">
                          <thead>
                            <tr className="border-b bg-muted/40 text-xs uppercase tracking-wide text-muted-foreground">
                              <th className="h-10 px-4 text-left">Branch</th>
                              <th className="h-10 px-4 text-left">Period</th>
                              <th className="h-10 px-4 text-left">Status</th>
                              <th className="h-10 px-4 text-left">Prepared</th>
                              <th className="h-10 px-4 text-left">Submitted</th>
                              <th className="h-10 px-4 text-right">Employees</th>
                              <th className="h-10 px-4 text-right">Gross</th>
                              <th className="h-10 px-4 text-right">Deductions</th>
                              <th className="h-10 px-4 text-right">Net</th>
                              <th className="h-10 px-4 text-right">Employer cost</th>
                              <th className="h-10 px-4 text-right">Exceptions</th>
                              <th className="h-10 px-4 text-right">Action</th>
                            </tr>
                          </thead>
                          <tbody>
                            {rows.map((r) => (
                              <tr key={r.runId} className="border-b transition-colors last:border-0 hover:bg-muted/40">
                                <td className="px-4 py-3 font-medium">{r.branchName ?? "Branch"}</td>
                                <td className="px-4 py-3">{r.periodLabel || "—"}</td>
                                <td className="px-4 py-3">
                                  <StatusBadge status={backendStatusToLabel(r.status)} />
                                </td>
                                <td className="max-w-40 truncate px-4 py-3 text-muted-foreground">
                                  {r.preparedBySubjectId ?? "—"}
                                </td>
                                <td className="px-4 py-3 text-muted-foreground">
                                  {r.submittedAt ? timeAgo(r.submittedAt) : "Not yet"}
                                </td>
                                <td className="px-4 py-3 text-right tabular">{r.employeeCount}</td>
                                <td className="px-4 py-3 text-right tabular">
                                  {money(r.totalGross, "ZMW")}
                                </td>
                                <td className="px-4 py-3 text-right tabular">
                                  {money(r.totalDeductions, "ZMW")}
                                </td>
                                <td className="px-4 py-3 text-right tabular font-medium">
                                  {money(r.totalNet, "ZMW")}
                                </td>
                                <td className="px-4 py-3 text-right tabular text-muted-foreground">
                                  {money(r.totalEmployerCost, "ZMW")}
                                </td>
                                <td
                                  className={`px-4 py-3 text-right tabular ${
                                    r.exceptionCount > 0 ? "font-medium text-amber-600" : "text-muted-foreground"
                                  }`}
                                >
                                  {r.exceptionCount > 0 ? r.exceptionCount : "—"}
                                </td>
                                <td className="px-4 py-3 text-right">
                                  <Link
                                    to="/hrm/payroll/runs/$id"
                                    params={{ id: r.runId }}
                                    className="text-sm text-primary underline-offset-2 hover:underline"
                                  >
                                    Review
                                  </Link>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </RecordTable>
                        </div>
                      </CardContent>
                    </Card>
                  )}
                </div>
              );
            }}
          </Async>
        )}
      </AppShell>
    </AuthGate>
  );
}
