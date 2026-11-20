import { createFileRoute } from "@tanstack/react-router";
import { Check, Circle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/lifecycle/journeys")({
  head: () => ({
    meta: [
      { title: "Journeys — Kuubu HRM" },
      { name: "description", content: "Guided support for the moments that matter: promotion, returning to work, becoming a parent." },
      { property: "og:title", content: "Journeys — Kuubu HRM" },
      { property: "og:description", content: "Guided support for the moments that matter." },
    ],
  }),
  component: JourneysPage,
});

function JourneysPage() {
  const state = useMock(() => extrasApi.journeys());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Journeys"
        description="A short checklist for the moments that matter, so nobody has to work out the steps themselves."
        primaryAction={<Button>Start a journey</Button>}
      />
      <Async state={state} rows={3}>
        {(rows) => (
          <ul className="grid gap-4 sm:grid-cols-2">
            {rows.map((j) => {
              const done = j.steps.filter((s) => s.done).length;
              return (
                <li key={j.id} className="rounded-lg border bg-surface p-5">
                  <p className="text-sm font-medium">{j.name}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {j.employee} · {j.trigger}
                  </p>
                  <p className="mt-3 text-xs font-medium">
                    {done} of {j.steps.length} done · due {j.due}
                  </p>
                  <ul className="mt-2 space-y-1.5">
                    {j.steps.map((s) => (
                      <li key={s.label} className="flex items-start gap-2 text-sm">
                        {s.done ? (
                          <Check className="mt-0.5 size-3.5 shrink-0 text-success" aria-label="Done" />
                        ) : (
                          <Circle className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" aria-label="To do" />
                        )}
                        <span className={s.done ? "text-muted-foreground line-through" : ""}>
                          {s.label}
                          <span className="ml-1.5 text-xs text-muted-foreground">{s.owner}</span>
                        </span>
                      </li>
                    ))}
                  </ul>
                </li>
              );
            })}
          </ul>
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
