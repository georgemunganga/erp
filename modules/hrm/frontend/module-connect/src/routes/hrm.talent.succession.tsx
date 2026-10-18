import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { TriangleAlert, UserPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/talent/succession")({
  head: () => ({
    meta: [
      { title: "Succession and skills — Newworldcargo HRM" },
      { name: "description", content: "Which roles would hurt to lose, who could step up, and where the skills gaps are." },
      { property: "og:title", content: "Succession and skills — Newworldcargo HRM" },
      { property: "og:description", content: "Which roles would hurt to lose, and where the skills gaps are." },
    ],
  }),
  component: SuccessionPage,
});

const riskCls = { High: "text-danger", Medium: "text-warning", Low: "text-muted-foreground" } as const;

function SuccessionPage() {
  const roles = useMock(() => extrasApi.criticalRoles());
  const skills = useMock(() => extrasApi.skills());
  const opps = useMock(() => extrasApi.opportunities());
  const [tab, setTab] = useState<"roles" | "skills" | "opps">("roles");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Talent"
        title="Succession and skills"
        description="Which roles would hurt to lose, and who could step up."
        primaryAction={<Button>Add a critical role</Button>}
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Views">
        {([
          ["roles", "Critical roles"],
          ["skills", "Skills gaps"],
          ["opps", "Opportunities"],
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

      {tab === "roles" ? (
        <Async state={roles} rows={3}>
          {(rows) => (
            <ul className="space-y-3">
              {rows.map((r) => (
                <li key={r.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-medium">{r.role}</span>
                    <span className={`inline-flex items-center gap-1 text-xs font-medium ${riskCls[r.risk]}`}>
                      <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
                      {r.risk} risk
                    </span>
                  </div>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {r.incumbent === "Vacant" ? "Currently vacant" : r.incumbent}
                  </p>
                  <p className="mt-2 text-sm text-muted-foreground">{r.reason}</p>
                  <div className="mt-2">
                    <p className="text-xs font-medium">Possible successors</p>
                    {r.successors.length ? (
                      <ul className="mt-1 flex flex-wrap gap-2">
                        {r.successors.map((s) => (
                          <li key={s.name} className="rounded-full border bg-surface-muted px-2.5 py-1 text-xs">
                            {s.name} <span className="text-muted-foreground">· {s.readiness}</span>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="mt-1 text-xs text-warning">Nobody identified yet.</p>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "skills" ? (
        <Async state={skills} rows={3}>
          {(rows) => (
            <div className="overflow-x-auto rounded-lg border bg-surface">
              <RecordTable className="w-full min-w-[30rem] text-left text-sm">
                <caption className="sr-only">Skills held against skills needed</caption>
                <thead className="border-b bg-surface-muted">
                  <tr>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Skill</th>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Category</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Held</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Needed</th>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Gap</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {rows.map((s) => {
                    const gap = s.needed - s.held;
                    return (
                      <tr key={s.name}>
                        <th scope="row" className="px-3 py-2 font-normal">{s.name}</th>
                        <td className="px-3 py-2 text-xs text-muted-foreground">{s.category}</td>
                        <td className="tabular px-3 py-2 text-right">{s.held}</td>
                        <td className="tabular px-3 py-2 text-right">{s.needed}</td>
                        <td className="px-3 py-2">
                          {gap > 0 ? (
                            <span className={`inline-flex items-center gap-1.5 text-xs ${s.scarce ? "text-danger" : "text-warning"}`}>
                              <TriangleAlert className="size-3.5 shrink-0" aria-hidden />
                              Short by {gap}
                              {s.scarce ? " · scarce" : ""}
                            </span>
                          ) : (
                            <span className="text-xs text-muted-foreground">Covered</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </RecordTable>
            </div>
          )}
        </Async>
      ) : null}

      {tab === "opps" ? (
        <Async state={opps} rows={3}>
          {(rows) => (
            <ul className="grid gap-3 sm:grid-cols-2">
              {rows.map((o) => (
                <li key={o.id} className="rounded-lg border bg-surface p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <UserPlus className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">{o.kind}</span>
                    <span className="text-[11px] text-muted-foreground">closes {o.closes}</span>
                  </div>
                  <p className="mt-2 text-sm font-medium">{o.title}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {o.branch} · {o.commitment}
                  </p>
                  <ul className="mt-2 flex flex-wrap gap-1.5">
                    {o.skills.map((s) => (
                      <li key={s} className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">
                        {s}
                      </li>
                    ))}
                  </ul>
                  <Button size="sm" variant="outline" className="mt-3">
                    Express interest
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}
    </AppShell>
      </AuthGate>
  );
}
