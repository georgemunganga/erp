import { createFileRoute } from "@tanstack/react-router";
import { Plane } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi, money } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/lifecycle/mobility")({
  head: () => ({
    meta: [
      { title: "Assignments — Kuubu HRM" },
      { name: "description", content: "People working away from their home entity: permits, allowances and which payroll pays them." },
      { property: "og:title", content: "Assignments — Kuubu HRM" },
      { property: "og:description", content: "People working away from their home entity." },
    ],
  }),
  component: MobilityPage,
});

function MobilityPage() {
  const state = useMock(() => extrasApi.assignments());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Assignments"
        description="Working away from the home entity. The two things that go wrong are permits and which payroll pays."
        primaryAction={<Button>New assignment</Button>}
      />
      <Async state={state} rows={2}>
        {(rows) => (
          <ul className="space-y-4">
            {rows.map((a) => (
              <li key={a.id} className="rounded-lg border bg-surface p-5">
                <div className="flex flex-wrap items-center gap-2">
                  <Plane className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                  <span className="text-sm font-medium">{a.employee}</span>
                  <StatusBadge status={a.status === "Active" ? "Active" : a.status === "Approved" ? "Approved" : "Submitted"} />
                  <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">{a.type}</span>
                  <span className="font-mono text-[11px] text-muted-foreground">{a.id}</span>
                </div>

                <p className="mt-2 text-sm">
                  {a.homeEntity} → {a.hostEntity}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {a.from}{a.to ? ` to ${a.to}` : " — open ended"}
                </p>

                <div className="mt-3 grid gap-3 sm:grid-cols-2">
                  <div className="rounded-md border bg-surface-muted p-3">
                    <p className="text-xs font-medium">Permit</p>
                    <p className="mt-0.5 text-sm">{a.permit}</p>
                    {a.permitNote ? <p className="mt-0.5 text-xs text-muted-foreground">{a.permitNote}</p> : null}
                  </div>
                  <div className="rounded-md border bg-surface-muted p-3">
                    <p className="text-xs font-medium">Payroll</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">{a.payrollNote}</p>
                  </div>
                </div>

                {a.allowances.length ? (
                  <ul className="mt-3 flex flex-wrap gap-2">
                    {a.allowances.map((al) => (
                      <li key={al.label} className="rounded-full border bg-surface-muted px-2.5 py-1 text-xs">
                        {al.label} <span className="tabular font-medium">{money(al.amount, al.currency)}</span>
                      </li>
                    ))}
                  </ul>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
