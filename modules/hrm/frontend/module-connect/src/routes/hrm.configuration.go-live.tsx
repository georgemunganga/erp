import { createFileRoute, redirect } from "@tanstack/react-router";
import { AlertTriangle, CheckCircle2, ClipboardCheck, RefreshCw, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/go-live")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/go-live", replace: true }); },
  head: () => ({
    meta: [
      { title: "Production readiness — Newworldcargo HRM" },
      { name: "description", content: "Controlled go-live gates, evidence and formal acceptance." },
    ],
  }),
  component: GoLiveConfiguration,
});

const evidenceOptions = [
  ["backup-restore", "Backup and restore rehearsal"],
  ["security-test", "Security acceptance test"],
  ["migration-rehearsal", "Production migration rehearsal"],
  ["performance-test", "Performance acceptance test"],
  ["monitoring-alerts", "Monitoring and alert validation"],
  ["incident-runbook", "Incident runbook walkthrough"],
  ["rollback-rehearsal", "Rollback rehearsal"],
  ["uat-hr", "HR user acceptance testing"],
  ["uat-payroll", "Payroll user acceptance testing"],
  ["training-hr", "HR administrator training"],
  ["training-payroll", "Payroll operator training"],
] as const;

const sections = [
  { id: "decision", label: "Release decision" },
  { id: "evidence", label: "Evidence" },
  { id: "signoff", label: "Formal sign-off" },
  { id: "runbooks", label: "Runbooks and training" },
];

export function GoLiveConfiguration() {
  const [tab, setTab] = useState("decision");
  const dashboard = useApi(() => realApi.goLiveReadiness());
  const [busy, setBusy] = useState(false);
  const [controlKey, setControlKey] = useState("migration-rehearsal");
  const [evidenceReference, setEvidenceReference] = useState("");
  const [notes, setNotes] = useState("");

  async function perform(action: () => Promise<unknown>, message: string) {
    setBusy(true);
    try {
      await action();
      feedback.submitted(
        message,
        "The actor and decision are retained in the append-only acceptance trail.",
      );
      dashboard.reload();
    } catch (error) {
      feedback.blocked(
        "The readiness action was not saved.",
        error instanceof Error ? error.message : "Try again later.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthGate>
      <ConfigPage
        title="Production readiness"
        description="A fail-closed release workspace for rehearsals, operational evidence and formal HR, payroll, finance, technical and tenant acceptance."
        sections={sections}
        active={tab}
        onSelect={setTab}
        notice="Approval is computed from live tenant data and append-only evidence. This screen never manufactures UAT, training, rehearsal or owner acceptance."
      >
        <Async state={dashboard} rows={8}>
          {(data) => (
            <div className="space-y-6" data-testid="go-live-readiness">
              <section
                className={`rounded-lg border p-5 ${data.canGoLive ? "border-success/40 bg-success-soft" : "border-warning/40 bg-warning-soft"}`}
              >
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div className="flex gap-3">
                    {data.canGoLive ? (
                      <ShieldCheck className="size-6 text-success" />
                    ) : (
                      <AlertTriangle className="size-6 text-warning" />
                    )}
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                        Current release decision
                      </p>
                      <h2
                        className="mt-1 text-xl font-semibold capitalize"
                        data-testid="release-decision"
                      >
                        {data.decision.replaceAll("-", " ")}
                      </h2>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {data.passedGates} of {data.totalGates} readiness gates passed.
                      </p>
                    </div>
                  </div>
                  <Button variant="outline" className="gap-2" onClick={dashboard.reload}>
                    <RefreshCw className="size-4" /> Refresh evidence
                  </Button>
                </div>
              </section>

              {tab === "decision" ? (
                <div className="space-y-4">
                  {data.blockers.length ? (
                    <section className="rounded-lg border bg-surface p-4">
                      <h2 className="text-sm font-semibold">Release blockers</h2>
                      <ul className="mt-3 grid gap-2 text-sm sm:grid-cols-2">
                        {data.blockers.map((blocker) => (
                          <li key={blocker} className="flex gap-2">
                            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" />
                            {blocker}
                          </li>
                        ))}
                      </ul>
                    </section>
                  ) : null}
                  <div className="grid gap-3 md:grid-cols-2">
                    {data.gates.map((gate) => (
                      <Gate key={gate.key} gate={gate} />
                    ))}
                  </div>
                </div>
              ) : null}

              {tab === "evidence" ? (
                <div className="space-y-5">
                  <section className="rounded-lg border bg-surface p-4">
                    <h2 className="text-sm font-semibold">Record verified evidence</h2>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Use a durable report, ticket, run ID or signed checklist reference. Do not
                      record a pass until the control was actually exercised.
                    </p>
                    <div className="mt-4 grid gap-3 md:grid-cols-2">
                      <select
                        aria-label="Evidence control"
                        className="h-10 rounded-md border bg-background px-3 text-sm"
                        value={controlKey}
                        onChange={(event) => setControlKey(event.target.value)}
                      >
                        {evidenceOptions.map(([key, label]) => (
                          <option key={key} value={key}>
                            {label}
                          </option>
                        ))}
                      </select>
                      <Input
                        aria-label="Evidence reference"
                        placeholder="Report, ticket or run reference"
                        value={evidenceReference}
                        onChange={(event) => setEvidenceReference(event.target.value)}
                      />
                      <Input
                        aria-label="Evidence notes"
                        placeholder="Scope, result and exceptions"
                        value={notes}
                        onChange={(event) => setNotes(event.target.value)}
                      />
                    </div>
                    <Button
                      className="mt-3"
                      disabled={busy || !evidenceReference.trim()}
                      onClick={() =>
                        perform(
                          () =>
                            realApi.recordGoLiveEvidence({
                              controlKey,
                              status: "passed",
                              evidenceReference: evidenceReference.trim(),
                              notes: notes.trim() || null,
                              executedAt: new Date().toISOString(),
                              expiresAt: null,
                            }),
                          "Readiness evidence recorded.",
                        )
                      }
                    >
                      Record passed evidence
                    </Button>
                  </section>
                  <ConfigTable
                    caption="Readiness evidence state"
                    headers={["Control", "State", "Evidence", "Verified"]}
                    rows={data.gates
                      .filter((gate) => gate.category === "evidence")
                      .map((gate) => [
                        gate.name,
                        gate.status,
                        gate.evidenceReference ?? "Not recorded",
                        gate.verifiedAt ? new Date(gate.verifiedAt).toLocaleString() : "—",
                      ])}
                  />
                </div>
              ) : null}

              {tab === "signoff" ? (
                <div className="space-y-3">
                  <p className="text-sm text-muted-foreground">
                    Each acceptance is enforced against the caller’s workforce role. A shared IdP
                    identity with only an external-platform tenant role cannot enter HRM or sign
                    here.
                  </p>
                  {data.signoffs.map((signoff) => (
                    <section
                      key={signoff.roleKey}
                      className="flex flex-wrap items-center gap-3 rounded-lg border bg-surface p-4"
                    >
                      <ClipboardCheck className="size-5 text-primary" />
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-semibold">{signoff.roleName}</p>
                        <p className="text-xs text-muted-foreground">
                          {signoff.decision === "pending"
                            ? "No decision recorded"
                            : `${signoff.decision} by ${signoff.actorSubjectId}`}
                        </p>
                      </div>
                      <span className="rounded-full border px-2.5 py-0.5 text-xs capitalize">
                        {signoff.decision}
                      </span>
                      <Button
                        size="sm"
                        disabled={busy}
                        onClick={() =>
                          perform(
                            () =>
                              realApi.recordGoLiveSignoff(
                                signoff.roleKey,
                                "approved",
                                "M36 acceptance criteria reviewed.",
                              ),
                            `${signoff.roleName} approval recorded.`,
                          )
                        }
                      >
                        Approve
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={busy}
                        onClick={() =>
                          perform(
                            () =>
                              realApi.recordGoLiveSignoff(
                                signoff.roleKey,
                                "withdrawn",
                                "Acceptance withdrawn pending review.",
                              ),
                            `${signoff.roleName} acceptance withdrawn.`,
                          )
                        }
                      >
                        Withdraw
                      </Button>
                    </section>
                  ))}
                </div>
              ) : null}

              {tab === "runbooks" ? (
                <div className="grid gap-4 md:grid-cols-2">
                  {[
                    [
                      "Migration and release rehearsal",
                      "Apply migrations in an isolated restore, compare schema and counts, execute smoke and performance gates, then retain the run reference.",
                    ],
                    [
                      "Incident response",
                      "Confirm ownership, severity, communications path, containment, evidence capture and recovery decision before launch.",
                    ],
                    [
                      "HR and payroll UAT",
                      "Exercise worker maintenance, leave, payroll preparation, segregation, release, reconciliation, reports and self-service using named test cases.",
                    ],
                    [
                      "Rollback",
                      "Retain immutable API/web image tags and a compatible database backup; rehearse the exact restoration decision and commands before approval.",
                    ],
                  ].map(([title, detail]) => (
                    <section key={title} className="rounded-lg border bg-surface p-4">
                      <CheckCircle2 className="size-5 text-primary" />
                      <h2 className="mt-3 text-sm font-semibold">{title}</h2>
                      <p className="mt-1 text-sm text-muted-foreground">{detail}</p>
                    </section>
                  ))}
                </div>
              ) : null}
            </div>
          )}
        </Async>
      </ConfigPage>
    </AuthGate>
  );
}

function Gate({
  gate,
}: {
  gate: { name: string; status: string; detail: string; evidenceReference?: string | null };
}) {
  const passed = gate.status === "passed";
  return (
    <section className="rounded-lg border bg-surface p-4">
      <div className="flex items-start gap-3">
        {passed ? (
          <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-success" />
        ) : (
          <AlertTriangle className="mt-0.5 size-5 shrink-0 text-warning" />
        )}
        <div>
          <h2 className="text-sm font-semibold">{gate.name}</h2>
          <p className="mt-1 text-xs text-muted-foreground">{gate.detail}</p>
          {gate.evidenceReference ? (
            <p className="mt-2 text-xs">Evidence: {gate.evidenceReference}</p>
          ) : null}
        </div>
      </div>
    </section>
  );
}
