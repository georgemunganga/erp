import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, CheckCircle2, CircleDashed, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import { blockedTasks, displayName, lifecycleApi, overdueTasks, taskProgress, TODAY } from "@/mock/lifecycle";
import type { ClearanceCategory, ClearanceTask, TaskState } from "@/mock/lifecycle";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/lifecycle/offboarding/$id")({
  head: () => ({
    meta: [
      { title: "Separation case — Kuubu HRM" },
      { name: "description", content: "Clearance checklist, consolidated blockers and final separation sign-off." },
      { property: "og:title", content: "Separation case — Kuubu HRM" },
      { property: "og:description", content: "Clearance checklist, consolidated blockers and final separation sign-off." },
    ],
  }),
  component: OffboardingDetail,
});

const categories: ClearanceCategory[] = [
  "Assets returned",
  "Access revoked",
  "Knowledge handover",
  "Final pay",
  "Outstanding advances",
];

const stateMeta: Record<TaskState, { icon: typeof Clock; cls: string }> = {
  Done: { icon: CheckCircle2, cls: "border-success/30 bg-success-soft text-success" },
  "In progress": { icon: Clock, cls: "border-info/30 bg-info-soft text-info" },
  "Not started": { icon: CircleDashed, cls: "border-border bg-muted text-muted-foreground" },
  Blocked: { icon: AlertTriangle, cls: "border-danger/30 bg-danger-soft text-danger" },
};

/** Task state carries an icon and a word — never colour on its own. */
function TaskStatePill({ state }: { state: TaskState }) {
  const m = stateMeta[state];
  const Icon = m.icon;
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${m.cls}`}>
      <Icon aria-hidden className="size-3.5 shrink-0" />
      {state}
    </span>
  );
}

function ClearanceRow({ task, showCategory }: { task: ClearanceTask; showCategory?: boolean }) {
  const overdue = task.state !== "Done" && task.dueDate < TODAY;
  return (
    <li className="rounded-md border bg-surface-muted p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          {showCategory ? (
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{task.category}</p>
          ) : null}
          <p className="text-sm font-medium">{task.label}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">{task.detail}</p>
        </div>
        <TaskStatePill state={task.state} />
      </div>
      <p className="mt-2 text-xs text-muted-foreground">
        {task.ownerName} · due {task.dueDate}
        {overdue ? <span className="ml-1 font-medium text-danger">· overdue</span> : null}
      </p>
      {task.blocker ? (
        <p className="mt-2 rounded border border-danger/30 bg-danger-soft px-2 py-1 text-xs text-danger">
          Blocked: {task.blocker}
        </p>
      ) : null}
    </li>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

async function submitSeparation(workerId: string): Promise<void> {
  try {
    await realApi.offboardWorker(workerId, {});
    feedback.submitted("Separation signed off.", "The clearance checklist was completed and the final settlement released.");
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    if (message.includes("offboarding-blocked")) {
      feedback.blocked(
        "Offboarding blocked by open clearance items.",
        "Clear the open items below (assets, access, final pay) before the separation can be signed off.",
      );
    } else {
      feedback.note("Separation could not be signed off.", message);
    }
  }
}

function OffboardingDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => lifecycleApi.offboarding(id), [id]);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {(c) => {
          if (!c) return <RestrictedState />;
          const person = displayName(c);
          const progress = taskProgress(c.clearance);
          const blocked = blockedTasks(c.clearance);
          const overdue = overdueTasks(c.clearance).filter((t) => t.state !== "Blocked");
          const unresolved = [...blocked, ...overdue];
          const directoryRecord = employees.find((e) => e.id === c.employeeId);
          const settled = c.status === "Completed" || c.status === "Cancelled";

          return (
            <RecordDetail
              reference={c.id}
              title={`Separation — ${person}`}
              subtitle={`${c.reason} · last working date ${c.lastWorkingDate} · ${c.branch}`}
              status={c.status}
              owner={c.owner}
              nextAction={c.nextAction}
              dueDate={c.dueDate}
              secondaryActions={
                <>
                  {directoryRecord ? (
                    <Button variant="outline" asChild>
                      <Link to="/hrm/employees/$id" params={{ id: directoryRecord.id }}>
                        Open employee record
                      </Link>
                    </Button>
                  ) : null}
                  <Button variant="outline" asChild>
                    <Link to="/hrm/lifecycle/offboarding">All separation cases</Link>
                  </Button>
                </>
              }
              summary={[
                { label: "Employee", value: person },
                { label: "Role", value: c.jobTitle },
                { label: "Organisation", value: `${c.entity} · ${c.branch}` },
                { label: "Separation reason", value: c.reason },
                { label: "Notice", value: `${c.noticePeriod}, given ${c.noticeGivenOn}` },
                { label: "Last working date", value: c.lastWorkingDate },
                { label: "Final pay run", value: c.finalPayRun },
                { label: "Rehire eligibility", value: c.rehireEligible },
                { label: "Clearance progress", value: `${progress.label} · ${blocked.length} blocked · ${overdue.length} overdue` },
              ]}
              timeline={<StatusTimeline title="Case history" events={c.timeline} />}
              related={
                <>
                  <p className="text-sm">{c.reasonDetail}</p>
                  <p>
                    <Link to="/hrm/lifecycle/movements" className="text-primary underline underline-offset-2">
                      Movements affecting this employee
                    </Link>
                  </p>
                  <p>
                    <Link to="/hrm/payslips" className="text-primary underline underline-offset-2">
                      Pay runs and final settlement
                    </Link>
                  </p>
                </>
              }
            >
              <DetailSection
                title="Unresolved blockers"
                description="Everything that must clear before this separation can be signed off, consolidated from the checklist and the policy checks."
              >
                {unresolved.length === 0 && c.conflicts.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    Nothing outstanding. {progress.label} — this case is ready for sign-off.
                  </p>
                ) : (
                  <div className="space-y-4">
                    {unresolved.length ? (
                      <ul className="space-y-2">
                        {unresolved.map((t) => (
                          <ClearanceRow key={`blocker-${t.id}`} task={t} showCategory />
                        ))}
                      </ul>
                    ) : null}
                    {c.conflicts.length ? (
                      <div className="rounded-md border border-warning/40 bg-warning-soft p-3">
                        <h3 className="text-sm font-semibold text-warning">Also in the way</h3>
                        <ul className="mt-1 list-inside list-disc text-sm">
                          {c.conflicts.map((x) => (
                            <li key={x}>{x}</li>
                          ))}
                        </ul>
                      </div>
                    ) : null}
                  </div>
                )}
              </DetailSection>

              <DetailSection
                title="Clearance checklist"
                description="Assets, access, knowledge handover, final pay and outstanding advances. Each item names an owner and a due date."
              >
                <div className="space-y-6">
                  {categories.map((category) => {
                    const tasks = c.clearance.filter((t) => t.category === category);
                    if (tasks.length === 0) return null;
                    const p = taskProgress(tasks);
                    return (
                      <div key={category}>
                        <div className="flex flex-wrap items-baseline justify-between gap-2">
                          <h3 className="text-sm font-semibold">{category}</h3>
                          <p className="text-xs text-muted-foreground">{p.label}</p>
                        </div>
                        <ul className="mt-2 space-y-2">
                          {tasks.map((t) => (
                            <ClearanceRow key={t.id} task={t} />
                          ))}
                        </ul>
                      </div>
                    );
                  })}
                </div>
              </DetailSection>

              <ApprovalPanel
                decisionSummary={
                  <>
                    <p>
                      Sign off the separation of <span className="font-medium">{person}</span> on {c.lastWorkingDate}, releasing the
                      final settlement in the {c.finalPayRun} pay run.
                    </p>
                    <p className="mt-2 text-muted-foreground">
                      {unresolved.length
                        ? `${unresolved.length} clearance ${unresolved.length === 1 ? "item is" : "items are"} still unresolved. Approving now records that you accept them as residual risk.`
                        : "All clearance items are complete."}
                    </p>
                  </>
                }
                policy={c.policy}
                conflicts={c.conflicts}
                evidence={[
                  { label: "Notice letter", href: "#" },
                  { label: "Clearance sheet (mock)", href: "#" },
                ]}
                delegates={["Mutale Kabwe (Manager)", "Thandiwe Banda (HR operations)", "Payroll duty approver"]}
                disabled={settled || !USE_REAL}
                onDecision={() => submitSeparation(c.employeeId ?? c.id)}
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
