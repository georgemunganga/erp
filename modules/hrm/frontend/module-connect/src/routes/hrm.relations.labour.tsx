import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Check, Radio, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/relations/labour")({
  head: () => ({
    meta: [
      { title: "Agreements and roll call — Kuubu HRM" },
      { name: "description", content: "Collective agreements and their terms, plus emergency roll call." },
      { property: "og:title", content: "Agreements and roll call — Kuubu HRM" },
      { property: "og:description", content: "Collective agreements and their terms, plus emergency roll call." },
    ],
  }),
  component: LabourPage,
});

function LabourPage() {
  const agreements = useMock(() => extrasApi.agreements());
  const rollCalls = useMock(() => extrasApi.rollCalls());
  const [tab, setTab] = useState<"agreements" | "rollcall">("agreements");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Relations and safety"
        title="Agreements and roll call"
        description="Collective terms that override the default policy, and a way to account for everyone quickly."
        primaryAction={
          tab === "rollcall" ? <Button>Start a roll call</Button> : <Button>Add an agreement</Button>
        }
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Views">
        {([
          ["agreements", "Agreements"],
          ["rollcall", "Roll call"],
        ] as const).map(([id, label]) => (
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

      {tab === "agreements" ? (
        <Async state={agreements} rows={2}>
          {(rows) => (
            <ul className="space-y-4">
              {rows.map((a) => (
                <li key={a.id} className="rounded-lg border bg-surface p-5">
                  <div className="flex flex-wrap items-center gap-2">
                    <Users className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="text-sm font-medium">{a.union}</span>
                    <StatusBadge status={a.status === "Active" ? "Active" : a.status === "In negotiation" ? "In review" : "Cancelled"} />
                    <span className="font-mono text-[11px] text-muted-foreground">{a.id}</span>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {a.covers} · {a.members} members · {a.from} to {a.to}
                  </p>
                  <ul className="mt-3 space-y-1.5">
                    {a.keyTerms.map((t) => (
                      <li key={t} className="flex items-start gap-2 text-sm">
                        <Check className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                        {t}
                      </li>
                    ))}
                  </ul>
                  {a.status === "Active" ? (
                    <p className="mt-3 text-xs text-muted-foreground">
                      Where a term here is better than the standard policy, this one applies.
                    </p>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : (
        <Async state={rollCalls} rows={2}>
          {(rows) => (
            <ul className="space-y-3">
              {rows.map((r) => (
                <li key={r.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <Radio className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="text-sm font-medium">{r.event}</span>
                    <StatusBadge status={r.status === "Active" ? "In review" : "Approved"} />
                    <span className="font-mono text-[11px] text-muted-foreground">{r.id}</span>
                  </div>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {r.branch} · started {r.started}
                  </p>
                  <div className="mt-3 flex flex-wrap gap-4 text-sm">
                    <span>
                      <span className="tabular block font-semibold">{r.expected}</span>
                      <span className="block text-xs text-muted-foreground">Expected on site</span>
                    </span>
                    <span>
                      <span className="tabular block font-semibold text-success">{r.safe}</span>
                      <span className="block text-xs text-muted-foreground">Confirmed safe</span>
                    </span>
                    <span>
                      <span className={`tabular block font-semibold ${r.noResponse ? "text-warning" : ""}`}>{r.noResponse}</span>
                      <span className="block text-xs text-muted-foreground">No response</span>
                    </span>
                  </div>
                  {r.outstanding.length ? (
                    <ul className="mt-2 space-y-1">
                      {r.outstanding.map((o) => (
                        <li key={o} className="text-xs text-muted-foreground">{o}</li>
                      ))}
                    </ul>
                  ) : null}
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
