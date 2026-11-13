import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Info, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { relationsApi } from "@/mock/relations";
import type { Declaration } from "@/mock/relations";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/relations/ethics")({
  head: () => ({
    meta: [
      { title: "Ethics and declarations — Kuubu HRM" },
      { name: "description", content: "Conflicts of interest, outside employment, gifts and related-party declarations." },
      { property: "og:title", content: "Ethics and declarations — Kuubu HRM" },
      { property: "og:description", content: "Conflicts of interest, outside employment, gifts and related-party declarations." },
    ],
  }),
  component: EthicsPage,
});

function EthicsPage() {
  const declarations = useMock(() => relationsApi.declarations());
  const campaigns = useMock(() => relationsApi.campaigns());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Relations and safety"
        title="Ethics and declarations"
        description="Declaring an interest is not an admission of wrongdoing. It is what allows the interest to be managed openly."
        primaryAction={<Button>Make a declaration</Button>}
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
            <ShieldCheck className="size-3.5" aria-hidden />
            Declaring early protects you as well as the organisation
          </span>
        }
      />

      <section aria-label="Declaration campaigns">
        <h2 className="text-sm font-semibold">Campaigns</h2>
        <Async state={campaigns} rows={2}>
          {(rows) => (
            <ul className="mt-3 grid gap-3 sm:grid-cols-2">
              {rows.map((c) => {
                const pct = Math.round((c.completed / c.total) * 100);
                const outstanding = c.total - c.completed;
                return (
                  <li key={c.id} className="rounded-lg border bg-surface p-4">
                    <p className="text-sm font-medium">{c.name}</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">{c.population}</p>
                    <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-muted" role="presentation">
                      <div className="h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
                    </div>
                    <p className="mt-1.5 text-xs">
                      <span className="font-medium">
                        {c.completed} of {c.total} complete ({pct}%)
                      </span>
                      {outstanding > 0 ? (
                        <span className="text-muted-foreground"> · {outstanding} outstanding · due {c.due}</span>
                      ) : (
                        <span className="text-muted-foreground"> · complete</span>
                      )}
                    </p>
                  </li>
                );
              })}
            </ul>
          )}
        </Async>
      </section>

      <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
        <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>
          "Accepted with mitigation" means the interest is real and allowed to stand, with specific
          controls attached. The mitigation is part of the record — a declaration without one is not
          a decision.
        </span>
      </p>

      <Async state={declarations} rows={3}>
        {(rows) => (
          <ListPage<Declaration>
            rows={rows.filter((d) =>
              view === "open" ? d.status === "Submitted" || d.status === "Under review" : true,
            )}
            savedViews={[
              { id: "all", label: "All declarations" },
              { id: "open", label: "Awaiting review" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee or subject"
            searchFields={(d) => `${d.id} ${d.employee} ${d.type} ${d.what}`}
            filters={[
              {
                id: "type",
                label: "Type",
                options: ["Conflict of interest", "Outside employment", "Gift or hospitality", "Related party"],
                match: (d, v) => d.type === v,
              },
              {
                id: "status",
                label: "Status",
                options: ["Submitted", "Under review", "Accepted with mitigation", "Accepted", "Refused"],
                match: (d, v) => d.status === v,
              },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (d) => <span className="font-mono text-xs">{d.id}</span> },
              { id: "employee", header: "Employee", cell: (d) => <span className="block max-w-56 truncate">{d.employee}</span> },
              { id: "type", header: "Type", cell: (d) => d.type },
              { id: "what", header: "Declared interest", cell: (d) => <span className="block max-w-72 truncate text-xs">{d.what}</span> },
              { id: "status", header: "Status", cell: (d) => <span className="text-xs">{d.status}</span> },
              {
                id: "mitigation",
                header: "Mitigation",
                cell: (d) =>
                  d.mitigation ? (
                    <span className="block max-w-64 truncate text-xs">{d.mitigation}</span>
                  ) : (
                    <span className="text-xs text-muted-foreground">None required</span>
                  ),
              },
              { id: "declared", header: "Declared", cell: (d) => d.declared },
              {
                id: "expires",
                header: "Renew by",
                defaultVisible: false,
                cell: (d) => (d.expires ? d.expires : <span className="text-xs text-muted-foreground">No renewal</span>),
              },
              { id: "reviewer", header: "Reviewer", defaultVisible: false, cell: (d) => d.reviewer },
            ]}
            emptyBody="No declarations match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
