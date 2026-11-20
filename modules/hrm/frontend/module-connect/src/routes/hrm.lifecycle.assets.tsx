import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import type { Asset } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/lifecycle/assets")({
  head: () => ({
    meta: [
      { title: "Assets and access — Kuubu HRM" },
      { name: "description", content: "What each person holds, and what comes back when they leave." },
      { property: "og:title", content: "Assets and access — Kuubu HRM" },
      { property: "og:description", content: "What each person holds, and what comes back when they leave." },
    ],
  }),
  component: AssetsPage,
});

function AssetsPage() {
  const state = useMock(() => extrasApi.assets());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Assets and access"
        description="What each person holds, and what needs to come back."
        primaryAction={<Button>Assign an item</Button>}
      />
      <Async state={state} rows={4}>
        {(rows) => (
          <ListPage<Asset>
            rows={rows.filter((a) => (view === "due" ? a.state === "Return due" || a.state === "Lost" : true))}
            savedViews={[
              { id: "all", label: "All items" },
              { id: "due", label: "Needs attention" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search item, serial or holder"
            searchFields={(a) => `${a.id} ${a.item} ${a.serial} ${a.holder}`}
            filters={[
              { id: "kind", label: "Type", options: ["Laptop", "Phone", "Access card", "Vehicle", "PPE", "Software"], match: (a, v) => a.kind === v },
              { id: "state", label: "State", options: ["Assigned", "Return due", "Returned", "Lost"], match: (a, v) => a.state === v },
            ]}
            columns={[
              { id: "item", header: "Item", cell: (a) => (
                <span className="block min-w-0 max-w-56">
                  <span className="block truncate font-medium">{a.item}</span>
                  <span className="block truncate text-xs text-muted-foreground">{a.id} · {a.serial}</span>
                </span>
              ) },
              { id: "kind", header: "Type", cell: (a) => a.kind },
              { id: "holder", header: "Held by", cell: (a) => <span className="block max-w-48 truncate">{a.holder}</span> },
              { id: "issued", header: "Issued", cell: (a) => a.issued },
              { id: "condition", header: "Condition", cell: (a) => a.condition },
              { id: "state", header: "State", cell: (a) => <StatusBadge status={a.state === "Return due" ? "Returned" : a.state === "Lost" ? "Rejected" : "Active"} /> },
              { id: "due", header: "Due back", cell: (a) => a.dueBack ?? <span className="text-xs text-muted-foreground">—</span> },
            ]}
            emptyBody="No items match this view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
