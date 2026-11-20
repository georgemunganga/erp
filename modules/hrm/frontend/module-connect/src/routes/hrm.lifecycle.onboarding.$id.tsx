import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, CheckCircle2, CircleDashed, Clock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";

export const Route = createFileRoute("/hrm/lifecycle/onboarding/$id")({
  head: () => ({
    meta: [
      { title: "Onboarding case — Kuubu HRM" },
      { name: "description", content: "One joiner's statutory pack: what is complete, what is missing and what blocks a clean start." },
      { property: "og:title", content: "Onboarding case — Kuubu HRM" },
      { property: "og:description", content: "One joiner's statutory pack: what is complete, what is missing and what blocks a clean start." },
    ],
  }),
  component: OnboardingDetail,
});

type ItemState = "Done" | "Not started";

const stateMeta: Record<ItemState, { icon: typeof Clock; cls: string }> = {
  Done: { icon: CheckCircle2, cls: "border-success/30 bg-success-soft text-success" },
  "Not started": { icon: CircleDashed, cls: "border-border bg-muted text-muted-foreground" },
};

/** State carries an icon and a word — never colour on its own. */
function StatePill({ state }: { state: ItemState }) {
  const m = stateMeta[state];
  const Icon = m.icon;
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${m.cls}`}>
      <Icon aria-hidden className="size-3.5 shrink-0" />
      {state}
    </span>
  );
}

interface PackItem {
  label: string;
  detail: string;
  state: ItemState;
}

function ItemRow({ item }: { item: PackItem }) {
  return (
    <li className="rounded-md border bg-surface-muted p-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm font-medium">{item.label}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">{item.detail}</p>
        </div>
        <StatePill state={item.state} />
      </div>
    </li>
  );
}

/**
 * M22: the detail case is now the worker record itself. The backend's
 * onboarding plan is a five-item statutory/banking readiness checklist, so
 * this screen renders the real pack with the live worker facts.
 */
function OnboardingDetail() {
  const { id } = Route.useParams();
  const state = useApi(
    async () => {
      const [worker, plan] = await Promise.all([realApi.worker(id), realApi.onboardingPlan(id)]);
      return {
        worker: worker as Record<string, unknown>,
        isOnboarded: Boolean(plan?.isOnboarded),
        done: plan?.tasksCompleted ?? 0,
        total: plan?.tasksTotal ?? 0,
      };
    },
    [id],
  );

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {(s) => {
          if (!s || !s.worker) return <RestrictedState />;
          const w = s.worker;
          const personName = String(w.fullName ?? "");
          const employeeNo = String(w.employeeNo ?? "");
          const jobTitle = String(w.jobTitle ?? "");
          const department = String(w.orgUnitName ?? "");
          const startDate = String(w.startDate ?? "");
          const nrc = String(w.nrc ?? "");
          const tpin = String(w.tpin ?? "");
          const napsa = String(w.napsaNumber ?? "");
          const hasBank = Array.isArray(w.bankDetails) && w.bankDetails.length > 0;
          const bankName = hasBank ? String((w.bankDetails as Array<{ bankName?: unknown }>)[0]?.bankName ?? "") : "";

          const items: PackItem[] = [
            {
              label: "National Registration (NRC)",
              detail: nrc || "No NRC recorded — collect the NRC number from the joiner.",
              state: nrc ? "Done" : "Not started",
            },
            {
              label: "Tax Identification (TPIN)",
              detail: tpin || "No TPIN recorded — needed before the first pay run.",
              state: tpin ? "Done" : "Not started",
            },
            {
              label: "NAPSA number",
              detail: napsa || "No NAPSA number recorded — required for statutory deductions.",
              state: napsa ? "Done" : "Not started",
            },
            {
              label: "Bank account for payroll",
              detail: hasBank ? `Registered with ${bankName || "the bank"} — payout ready` : "No bank account registered — the employee cannot be paid until one is added.",
              state: hasBank ? "Done" : "Not started",
            },
          ];

          const remaining = items.filter((i) => i.state !== "Done");

          return (
            <RecordDetail
              reference={employeeNo}
              title={`Onboarding — ${personName}`}
              subtitle={`${jobTitle} · ${department}${startDate ? ` · starts ${startDate}` : ""}`}
              status={s.isOnboarded ? "Ready" : "In progress"}
              owner={department || "HR operations"}
              nextAction={s.isOnboarded ? "Onboarding complete" : "Complete the statutory pack"}
              primaryAction={
                <Button asChild variant="outline">
                  <Link to="/hrm/employees/$id" params={{ id: String(s.worker?.id ?? "") }}>
                    Open employee record
                  </Link>
                </Button>
              }
              summary={[
                {
                  label: "Live checklist progress",
                  value: `${s.done}/${s.total}${s.isOnboarded ? " · onboarded" : ""}`,
                },
                { label: "Joiner", value: personName },
                { label: "Role", value: jobTitle },
                { label: "Department", value: department },
                { label: "Start date", value: startDate || "Not set" },
                { label: "Employee type", value: String(w.workerType ?? "employee") },
              ]}
            >
              <DetailSection
                title="Statutory pack"
                description="The five readiness items the pay run depends on: assignment, NRC, TPIN, NAPSA number and a bank account. The backend counts the active assignment as the fifth item."
              >
                {remaining.length === 0 ? (
                  <p className="flex items-center gap-2 text-sm text-success">
                    <CheckCircle2 className="size-4" aria-hidden />
                    Every pack item is complete — this joiner is ready to be paid.
                  </p>
                ) : (
                  <ul className="space-y-2">
                    {items.map((t) => (
                      <ItemRow key={t.label} item={t} />
                    ))}
                  </ul>
                )}
                {remaining.length > 0 ? (
                  <p className="mt-3 flex items-start gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
                    <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                    {remaining.length} item{remaining.length === 1 ? "" : "s"} outstanding — {remaining[0].label}
                  </p>
                ) : null}
              </DetailSection>

              <DetailSection title="Related records" description="Where to continue from here.">
                <p>
                  <Link to="/hrm/lifecycle/onboarding" className="text-primary underline underline-offset-2">
                    All onboarding cases
                  </Link>
                </p>
                <p className="mt-2 text-xs text-muted-foreground">
                  Bank details and statutory numbers are edited on the employee record. The pack re-checks
                  automatically each time this screen loads.
                </p>
              </DetailSection>
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
