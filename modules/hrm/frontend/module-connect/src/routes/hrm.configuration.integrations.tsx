import { createFileRoute, redirect } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { ArrowRightLeft, Download, RefreshCw, RotateCcw, Send, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/integrations")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/integrations", replace: true }); },
  head: () => ({
    meta: [
      { title: "Integration operations — Newworldcargo HRM" },
      {
        name: "description",
        content: "Finance, payment, statutory, storage and workforce identity hand-offs.",
      },
    ],
  }),
  component: IntegrationOperations,
});

type Row = Record<string, unknown>;

export function IntegrationOperations() {
  const dashboard = useApi(() => realApi.integrationDashboard());
  const runs = useApi(() => realApi.payrollRuns());
  const groups = useApi(() => realApi.payrollPayGroups());
  const [runId, setRunId] = useState("");
  const [groupId, setGroupId] = useState("");
  const [periodId, setPeriodId] = useState("");
  const [scheme, setScheme] = useState("zra");
  const [busy, setBusy] = useState("");
  const [reconcileId, setReconcileId] = useState("");
  const [reference, setReference] = useState("");
  const [outcome, setOutcome] = useState("matched");
  const periods = useApi(
    () => (groupId ? realApi.payrollPayGroupPeriods(groupId) : Promise.resolve([])),
    [groupId],
  );
  const runRows = useMemo(() => (runs.data?.items ?? []) as Row[], [runs.data]);
  const groupRows = useMemo(() => (groups.data ?? []) as Row[], [groups.data]);
  const periodRows = useMemo(() => (periods.data ?? []) as Row[], [periods.data]);

  async function perform(label: string, action: () => Promise<unknown>) {
    setBusy(label);
    try {
      await action();
      feedback.submitted(
        "Integration hand-off prepared.",
        "The operation is traceable and safe to replay with the same idempotency key.",
      );
      dashboard.reload();
    } catch (error) {
      feedback.blocked(
        "The hand-off was not created.",
        error instanceof Error ? error.message : "Try again later.",
      );
    } finally {
      setBusy("");
    }
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Configuration · Integrations"
          title="Integration operations"
          description="Prepare, trace and reconcile every HRM hand-off without giving an external system direct access to payroll records."
          primaryAction={
            <Button variant="outline" className="gap-2" onClick={dashboard.reload}>
              <RefreshCw className="size-4" />
              Refresh
            </Button>
          }
        />
        <Async state={dashboard} rows={6}>
          {(data) => (
            <div className="space-y-6" data-testid="integration-operations">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {[
                  ["Ready", data.ready],
                  ["Delivered", data.delivered],
                  ["Failed", data.failed],
                  ["Reconciled", data.reconciled],
                ].map(([label, value]) => (
                  <div key={String(label)} className="rounded-lg border bg-surface p-4">
                    <p className="text-xs text-muted-foreground">{label}</p>
                    <p className="mt-1 text-2xl font-semibold tabular-nums">{value}</p>
                  </div>
                ))}
              </div>

              <section className="rounded-lg border bg-surface p-5">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <h2 className="flex items-center gap-2 text-sm font-semibold">
                      <Send className="size-4" />
                      Prepare a hand-off
                    </h2>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Only released and approved source records pass the backend control gates.
                    </p>
                  </div>
                </div>
                <div className="mt-4 grid gap-4 lg:grid-cols-2">
                  <div className="rounded-md border p-4">
                    <label className="text-xs font-medium" htmlFor="integration-run">
                      Released payroll run
                    </label>
                    <select
                      id="integration-run"
                      value={runId}
                      onChange={(e) => setRunId(e.target.value)}
                      className="mt-2 h-10 w-full rounded-md border bg-background px-3 text-sm"
                    >
                      <option value="">Select a payroll run</option>
                      {runRows.map((run) => (
                        <option key={String(run.id)} value={String(run.id)}>
                          {String(run.periodLabel ?? run.id)} · {String(run.status)}
                        </option>
                      ))}
                    </select>
                    <div className="mt-3 flex flex-wrap gap-2">
                      <Button
                        size="sm"
                        disabled={!runId || !!busy}
                        onClick={() =>
                          perform("finance", () => realApi.createFinancePosting(runId))
                        }
                      >
                        Finance journal
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={!runId || !!busy}
                        onClick={() =>
                          perform("payments", () => realApi.createPaymentHandoff(runId))
                        }
                      >
                        Bank payment batch
                      </Button>
                    </div>
                  </div>
                  <div className="rounded-md border p-4">
                    <p className="text-xs font-medium">Statutory return hand-off</p>
                    <div className="mt-2 grid gap-2 sm:grid-cols-3">
                      <select
                        aria-label="Pay group"
                        value={groupId}
                        onChange={(e) => {
                          setGroupId(e.target.value);
                          setPeriodId("");
                        }}
                        className="h-10 rounded-md border bg-background px-3 text-sm"
                      >
                        <option value="">Pay group</option>
                        {groupRows.map((group) => (
                          <option key={String(group.id)} value={String(group.id)}>
                            {String(group.name ?? group.code)}
                          </option>
                        ))}
                      </select>
                      <select
                        aria-label="Pay period"
                        value={periodId}
                        onChange={(e) => setPeriodId(e.target.value)}
                        className="h-10 rounded-md border bg-background px-3 text-sm"
                      >
                        <option value="">Period</option>
                        {periodRows.map((period) => (
                          <option key={String(period.id)} value={String(period.id)}>
                            {String(period.periodLabel)}
                          </option>
                        ))}
                      </select>
                      <select
                        aria-label="Statutory scheme"
                        value={scheme}
                        onChange={(e) => setScheme(e.target.value)}
                        className="h-10 rounded-md border bg-background px-3 text-sm"
                      >
                        <option value="zra">ZRA PAYE</option>
                        <option value="napsa">NAPSA</option>
                        <option value="nhima">NHIMA</option>
                      </select>
                    </div>
                    <Button
                      size="sm"
                      className="mt-3"
                      disabled={!periodId || !!busy}
                      onClick={() =>
                        perform("statutory", () => realApi.createStatutoryHandoff(scheme, periodId))
                      }
                    >
                      Prepare return
                    </Button>
                  </div>
                </div>
                <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-md border p-4">
                  <div>
                    <p className="text-xs font-medium">Workforce identity</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {data.linkedWorkers} of {data.activeWorkers} active workers linked ·{" "}
                      {data.unlinkedWorkers} need attention
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={!!busy}
                      onClick={() =>
                        perform("identity-delta", () => realApi.createIdentitySync("delta"))
                      }
                    >
                      Delta sync
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={!!busy}
                      onClick={() =>
                        perform("identity-full", () => realApi.createIdentitySync("full"))
                      }
                    >
                      Full sync
                    </Button>
                  </div>
                </div>
              </section>

              <section>
                <h2 className="flex items-center gap-2 text-sm font-semibold">
                  <ArrowRightLeft className="size-4" />
                  Integration contracts
                </h2>
                <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                  {data.contracts.map((contract) => (
                    <article key={contract.key} className="rounded-lg border bg-surface p-4">
                      <div className="flex items-center justify-between gap-3">
                        <h3 className="text-sm font-medium">{contract.name}</h3>
                        <span className="rounded-full border px-2 py-0.5 text-[11px]">
                          v{contract.contractVersion}
                        </span>
                      </div>
                      <p className="mt-2 text-xs text-muted-foreground">
                        {contract.direction} · {contract.transport}
                      </p>
                      <dl className="mt-3 space-y-2 text-xs">
                        <div>
                          <dt className="font-medium">Operational owner</dt>
                          <dd className="text-muted-foreground">{contract.owner}</dd>
                        </div>
                        <div>
                          <dt className="font-medium">Retry</dt>
                          <dd className="text-muted-foreground">{contract.retryStrategy}</dd>
                        </div>
                        <div>
                          <dt className="font-medium">Reconciliation</dt>
                          <dd className="text-muted-foreground">
                            {contract.reconciliationProcess}
                          </dd>
                        </div>
                      </dl>
                      {contract.detail ? (
                        <p className="mt-3 rounded bg-surface-muted px-2 py-1.5 text-xs">
                          {contract.detail}
                        </p>
                      ) : null}
                    </article>
                  ))}
                </div>
              </section>

              <section className="rounded-lg border bg-surface p-5">
                <h2 className="flex items-center gap-2 text-sm font-semibold">
                  <ShieldCheck className="size-4" />
                  Operation history
                </h2>
                <div className="mt-3 overflow-x-auto">
                  <table className="w-full min-w-[62rem] text-left text-xs">
                    <thead>
                      <tr className="border-b text-muted-foreground">
                        {[
                          "Created",
                          "Integration",
                          "Source",
                          "Status",
                          "Attempts",
                          "External reference",
                          "Actions",
                        ].map((h) => (
                          <th key={h} className="px-2 py-2 font-medium">
                            {h}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {data.operations.map((operation) => (
                        <tr key={operation.id} className="border-b last:border-0">
                          <td className="px-2 py-3 tabular-nums">
                            {new Date(operation.createdAt).toLocaleString()}
                          </td>
                          <td className="px-2 py-3">
                            <span className="font-medium">{operation.integrationKey}</span>
                            <br />
                            <span className="text-muted-foreground">{operation.operationType}</span>
                          </td>
                          <td className="px-2 py-3">{operation.sourceReference ?? "—"}</td>
                          <td
                            className={`px-2 py-3 font-medium ${operation.status === "failed" || operation.status === "rejected" ? "text-danger" : ""}`}
                          >
                            {operation.status}
                          </td>
                          <td className="px-2 py-3 tabular-nums">{operation.attemptCount}</td>
                          <td className="px-2 py-3">{operation.externalReference ?? "—"}</td>
                          <td className="px-2 py-3">
                            <div className="flex gap-1">
                              <Button
                                aria-label="Download payload"
                                size="sm"
                                variant="ghost"
                                onClick={() =>
                                  realApi.downloadIntegration(
                                    operation.id,
                                    `${operation.integrationKey}-${operation.publicId}`,
                                  )
                                }
                              >
                                <Download className="size-3.5" />
                              </Button>
                              {operation.status === "failed" || operation.status === "rejected" ? (
                                <Button
                                  aria-label="Retry operation"
                                  size="sm"
                                  variant="ghost"
                                  onClick={() =>
                                    perform("retry", () => realApi.retryIntegration(operation.id))
                                  }
                                >
                                  <RotateCcw className="size-3.5" />
                                </Button>
                              ) : null}
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => {
                                  setReconcileId(operation.id);
                                  setReference(operation.externalReference ?? "");
                                }}
                              >
                                Reconcile
                              </Button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {data.operations.length === 0 ? (
                  <p className="py-8 text-center text-sm text-muted-foreground">
                    No integration hand-offs have been prepared yet.
                  </p>
                ) : null}
              </section>

              {reconcileId ? (
                <section
                  className="rounded-lg border border-primary/30 bg-surface p-5"
                  data-testid="integration-reconcile"
                >
                  <h2 className="text-sm font-semibold">Record external outcome</h2>
                  <div className="mt-3 grid gap-3 sm:grid-cols-[10rem_1fr_auto]">
                    <select
                      aria-label="Outcome"
                      value={outcome}
                      onChange={(e) => setOutcome(e.target.value)}
                      className="h-10 rounded-md border bg-background px-3 text-sm"
                    >
                      <option value="matched">Matched</option>
                      <option value="accepted">Accepted</option>
                      <option value="failed">Failed</option>
                      <option value="rejected">Rejected</option>
                    </select>
                    <input
                      aria-label="External reference"
                      value={reference}
                      onChange={(e) => setReference(e.target.value)}
                      placeholder="External receipt or batch reference"
                      className="h-10 rounded-md border bg-background px-3 text-sm"
                    />
                    <Button
                      disabled={!reference.trim() || !!busy}
                      onClick={() =>
                        perform("reconcile", async () => {
                          await realApi.reconcileIntegration(reconcileId, outcome, reference);
                          setReconcileId("");
                          setReference("");
                        })
                      }
                    >
                      Save outcome
                    </Button>
                  </div>
                </section>
              ) : null}
            </div>
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
