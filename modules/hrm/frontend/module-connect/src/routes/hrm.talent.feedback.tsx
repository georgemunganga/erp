import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Check, Circle, MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/talent/feedback")({
  head: () => ({
    meta: [
      { title: "Feedback and support plans — Kuubu HRM" },
      { name: "description", content: "Day-to-day feedback, and structured support where someone needs it." },
      { property: "og:title", content: "Feedback and support plans — Kuubu HRM" },
      { property: "og:description", content: "Day-to-day feedback, and structured support where someone needs it." },
    ],
  }),
  component: FeedbackPage,
});

function FeedbackPage() {
  const feedback = useMock(() => extrasApi.feedback());
  const pips = useMock(() => extrasApi.pips());
  const [tab, setTab] = useState<"feedback" | "plans">("feedback");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Talent"
        title="Feedback and support plans"
        description="Short notes as you go, so the annual review holds no surprises."
        primaryAction={<Button>Give feedback</Button>}
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Views">
        {([["feedback", "Feedback"], ["plans", "Support plans"]] as const).map(([id, label]) => (
          <button
            key={id}
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${tab === id ? "border-primary bg-primary-soft font-medium text-primary" : "bg-surface text-muted-foreground hover:border-border-strong"}`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "feedback" ? (
        <Async state={feedback} rows={3}>
          {(rows) => (
            <ul className="space-y-3">
              {rows.map((f) => (
                <li key={f.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <MessageSquare className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="text-sm">
                      <span className="font-medium">{f.from}</span> to{" "}
                      <span className="font-medium">{f.to}</span>
                    </span>
                    <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">{f.kind}</span>
                    <span className="text-[11px] text-muted-foreground">{f.when}</span>
                  </div>
                  <p className="mt-2 text-sm text-muted-foreground">{f.note}</p>
                  <p className="mt-1 text-[11px] text-muted-foreground">{f.visibility}</p>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : (
        <Async state={pips} rows={2}>
          {(rows) => (
            <ul className="space-y-4">
              {rows.map((p) => (
                <li key={p.id} className="rounded-lg border bg-surface p-5">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-medium">{p.employee}</span>
                    <StatusBadge status={p.status === "Active" ? "In review" : p.status === "Met" ? "Approved" : "Returned"} />
                    <span className="font-mono text-[11px] text-muted-foreground">{p.id}</span>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Opened {p.opened} · review {p.reviewDate}
                  </p>
                  <p className="mt-2 text-sm">{p.focus}</p>

                  <div className="mt-3 grid gap-3 sm:grid-cols-2">
                    <div>
                      <p className="text-xs font-medium">Support in place</p>
                      <ul className="mt-1 list-inside list-disc space-y-0.5 text-xs text-muted-foreground">
                        {p.support.map((s) => (
                          <li key={s}>{s}</li>
                        ))}
                      </ul>
                    </div>
                    <div>
                      <p className="text-xs font-medium">Milestones</p>
                      <ul className="mt-1 space-y-1">
                        {p.milestones.map((m) => (
                          <li key={m.label} className="flex items-start gap-2 text-xs">
                            {m.done ? (
                              <Check className="mt-0.5 size-3 shrink-0 text-success" aria-label="Met" />
                            ) : (
                              <Circle className="mt-0.5 size-3 shrink-0 text-muted-foreground" aria-label="Open" />
                            )}
                            <span className={m.done ? "text-muted-foreground line-through" : ""}>
                              {m.label} <span className="text-muted-foreground">· {m.due}</span>
                            </span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  </div>

                  <p className="mt-3 text-xs text-muted-foreground">
                    A support plan is help, not a countdown. Meeting it is the expected outcome.
                  </p>
                </li>
              ))}
            </ul>
          )}
        </Async>
      )}
    </AppShell>
      </AuthGate>
  );
}
