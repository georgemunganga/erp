import { createFileRoute, redirect } from "@tanstack/react-router";
import { Download, LockKeyhole, RefreshCw, ShieldCheck, ShieldAlert } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/compliance")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/$slug", params: { slug: "compliance" }, replace: true }); },
  head: () => ({
    meta: [
      { title: "Security and compliance — Newworldcargo HRM" },
      {
        name: "description",
        content:
          "Tenant isolation, least privilege, audit, retention, legal holds and control evidence.",
      },
    ],
  }),
  component: ComplianceConfig,
});

const sections = [
  { id: "posture", label: "Security posture" },
  { id: "roles", label: "Enforced role matrix" },
  { id: "audit", label: "Privileged audit" },
  { id: "retention", label: "Retention and holds" },
  { id: "evidence", label: "Control evidence" },
];

export function ComplianceConfig() {
  const [tab, setTab] = useState("posture");
  const dashboard = useApi(() => realApi.securityDashboard());
  const [busy, setBusy] = useState(false);
  const [controlKey, setControlKey] = useState("backup-restore");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [holdReference, setHoldReference] = useState("");
  const [holdScope, setHoldScope] = useState("");
  const [holdReason, setHoldReason] = useState("");

  async function perform(action: () => Promise<unknown>, success: string) {
    setBusy(true);
    try {
      await action();
      feedback.submitted(
        success,
        "The action and its actor are retained in the privileged audit trail.",
      );
      dashboard.reload();
    } catch (error) {
      feedback.blocked(
        "The compliance action was not saved.",
        error instanceof Error ? error.message : "Try again later.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthGate>
      <ConfigPage
        title="Security and compliance"
        description="Review the controls enforced for this HRM tenant and retain evidence for privileged operations."
        sections={sections}
        active={tab}
        onSelect={setTab}
        notice="This is a live, tenant-scoped control surface. Changes, failures and denied privileged attempts are retained as append-only evidence."
      >
        <Async state={dashboard} rows={6}>
          {(data) => (
            <div className="space-y-6" data-testid="security-compliance">
              <div className="grid gap-3 sm:grid-cols-3">
                <Metric label="Tenant" value={data.tenantId} />
                <Metric label="Open control findings" value={String(data.openFindings)} />
                <Metric label="Active legal holds" value={String(data.activeLegalHolds)} />
              </div>

              {tab === "posture" ? (
                <div className="space-y-3">
                  {data.controls.map((control) => (
                    <div
                      key={control.key}
                      className="flex flex-wrap items-start gap-3 rounded-lg border bg-surface p-4"
                    >
                      {control.status === "passed" ? (
                        <ShieldCheck className="mt-0.5 size-5 text-success" />
                      ) : (
                        <ShieldAlert className="mt-0.5 size-5 text-warning" />
                      )}
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-semibold">{control.name}</p>
                        <p className="mt-1 text-xs text-muted-foreground">{control.detail}</p>
                        {control.evidenceReference ? (
                          <p className="mt-2 text-xs">Evidence: {control.evidenceReference}</p>
                        ) : null}
                      </div>
                      <span
                        className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${control.status === "passed" ? "border-success/30 bg-success-soft text-success" : "border-warning/40 bg-warning-soft text-warning"}`}
                      >
                        {control.status}
                      </span>
                    </div>
                  ))}
                </div>
              ) : null}

              {tab === "roles" ? (
                <ConfigTable
                  caption="Backend-enforced least-privilege role matrix"
                  minWidth="56rem"
                  headers={["Capability", "Roles", "Data scope", "Sensitive", "Control"]}
                  rows={data.roleMatrix.map((row) => [
                    <span>
                      <span className="block font-medium">{row.capability}</span>
                      <span className="block text-xs text-muted-foreground">{row.description}</span>
                    </span>,
                    row.roles.join(", "),
                    row.dataScope,
                    row.sensitive ? "Yes" : "No",
                    row.control,
                  ])}
                />
              ) : null}

              {tab === "audit" ? (
                <div className="space-y-4">
                  <div className="flex justify-end gap-2">
                    <Button variant="outline" className="gap-2" onClick={dashboard.reload}>
                      <RefreshCw className="size-4" />
                      Refresh
                    </Button>
                    <Button
                      variant="outline"
                      className="gap-2"
                      onClick={() => realApi.exportSecurityAudit()}
                    >
                      <Download className="size-4" />
                      Export CSV
                    </Button>
                  </div>
                  <ConfigTable
                    caption="Privileged API actions"
                    minWidth="58rem"
                    headers={["When", "Actor", "Action", "Outcome", "Request"]}
                    rows={data.privilegedActions.map((row) => [
                      new Date(row.createdAt).toLocaleString(),
                      <span>
                        <span className="block font-medium">{row.actorSubjectId}</span>
                        <span className="block text-xs text-muted-foreground">
                          {row.actorRoles.join(", ")}
                        </span>
                      </span>,
                      `${row.method} ${row.path}`,
                      `${row.outcome} · ${row.statusCode}`,
                      row.requestId,
                    ])}
                  />
                  <ConfigTable
                    caption="Entity before and after audit"
                    minWidth="54rem"
                    headers={["When", "Entity", "Action", "Actor", "Correlation"]}
                    rows={data.entityAudit.map((row) => [
                      new Date(row.createdAt).toLocaleString(),
                      `${row.entityType} · ${row.entityId}`,
                      row.action,
                      row.actorSubjectId,
                      row.correlationId ?? "—",
                    ])}
                  />
                </div>
              ) : null}

              {tab === "retention" ? (
                <div className="space-y-5">
                  <ConfigTable
                    caption="Retention rules"
                    minWidth="52rem"
                    headers={["Record", "Kept", "Legal basis", "Disposition", "Hold"]}
                    rows={data.retentionRules.map((row) => [
                      row.recordType,
                      `${row.retentionMonths} months`,
                      row.legalBasis,
                      row.disposition,
                      row.legalHoldOverrides ? "Overrides disposal" : "No override",
                    ])}
                  />
                  <section className="rounded-lg border bg-surface p-4">
                    <h2 className="flex items-center gap-2 text-sm font-semibold">
                      <LockKeyhole className="size-4" />
                      Place a legal hold
                    </h2>
                    <div className="mt-3 grid gap-3 md:grid-cols-3">
                      <Input
                        aria-label="Legal hold reference"
                        placeholder="Reference"
                        value={holdReference}
                        onChange={(e) => setHoldReference(e.target.value)}
                      />
                      <Input
                        aria-label="Legal hold scope"
                        placeholder="Scope, for example worker:ID"
                        value={holdScope}
                        onChange={(e) => setHoldScope(e.target.value)}
                      />
                      <Input
                        aria-label="Legal hold reason"
                        placeholder="Reason"
                        value={holdReason}
                        onChange={(e) => setHoldReason(e.target.value)}
                      />
                    </div>
                    <Button
                      className="mt-3"
                      disabled={busy || !holdReference || !holdScope || !holdReason}
                      onClick={() =>
                        perform(
                          () =>
                            realApi.placeLegalHold({
                              reference: holdReference,
                              scope: holdScope,
                              reason: holdReason,
                            }),
                          "Legal hold placed.",
                        )
                      }
                    >
                      Place hold
                    </Button>
                  </section>
                  <ConfigTable
                    caption="Legal holds"
                    minWidth="52rem"
                    headers={["Reference", "Scope", "Reason", "Status", "Action"]}
                    rows={data.legalHolds.map((row) => [
                      row.reference,
                      row.scope,
                      row.reason,
                      row.status,
                      row.status === "active" ? (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={busy}
                          onClick={() =>
                            perform(
                              () =>
                                realApi.releaseLegalHold(
                                  row.id,
                                  "Authorised retention review completed.",
                                ),
                              "Legal hold released.",
                            )
                          }
                        >
                          Release
                        </Button>
                      ) : (
                        "Released"
                      ),
                    ])}
                  />
                </div>
              ) : null}

              {tab === "evidence" ? (
                <div className="space-y-5">
                  <section className="rounded-lg border bg-surface p-4">
                    <h2 className="text-sm font-semibold">Record completed control evidence</h2>
                    <div className="mt-3 grid gap-3 md:grid-cols-[14rem_1fr_auto]">
                      <select
                        aria-label="Compliance control"
                        className="h-10 rounded-md border bg-background px-3 text-sm"
                        value={controlKey}
                        onChange={(e) => setControlKey(e.target.value)}
                      >
                        <option value="backup-restore">Backup and restore</option>
                        <option value="tenant-isolation">Tenant isolation</option>
                        <option value="security-test">Security test</option>
                      </select>
                      <Input
                        aria-label="Evidence reference"
                        placeholder="Report, ticket or rehearsal reference"
                        value={evidenceReference}
                        onChange={(e) => setEvidenceReference(e.target.value)}
                      />
                      <Button
                        disabled={busy || !evidenceReference}
                        onClick={() =>
                          perform(
                            () =>
                              realApi.recordComplianceEvidence({
                                controlKey,
                                status: "passed",
                                evidenceReference,
                                executedAt: new Date().toISOString(),
                                expiresAt: new Date(Date.now() + 90 * 86400000).toISOString(),
                              }),
                            "Control evidence recorded.",
                          )
                        }
                      >
                        Record passed control
                      </Button>
                    </div>
                  </section>
                  <ConfigTable
                    caption="Control evidence history"
                    minWidth="48rem"
                    headers={["Control", "Status", "Reference", "Executed", "Actor"]}
                    rows={data.evidence.map((row) => [
                      row.controlKey,
                      row.status,
                      row.evidenceReference,
                      new Date(row.executedAt).toLocaleString(),
                      row.executedBySubjectId,
                    ])}
                  />
                </div>
              ) : null}
            </div>
          )}
        </Async>
      </ConfigPage>
    </AuthGate>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border bg-surface p-4">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 truncate text-xl font-semibold">{value}</p>
    </div>
  );
}
