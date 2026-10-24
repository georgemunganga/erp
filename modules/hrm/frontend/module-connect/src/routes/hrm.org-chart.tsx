import { createFileRoute } from "@tanstack/react-router";
import { Building2, Users } from "lucide-react";
import { realApi, useApi, type OrgChartNode } from "@/platform/use-api";
import { useAuth } from "@/platform/auth";
import { canManageHrmStructure } from "@/platform/entry-route";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/org-chart")({
  head: () => ({
    meta: [
      { title: "Organization chart — Newworldcargo HRM" },
      { name: "description", content: "Visual organization chart of legal entities, branches, departments and teams with headcount and unit leads." },
    ],
  }),
  component: OrgChartPage,
});

interface OrgChart {
  asAt: string;
  roots: OrgChartNode[];
}

/** Small icon per unit type so the chart reads at a glance. */
function unitTypeIcon(unitType: string) {
  if (unitType === "entity") return "🏛";
  if (unitType === "branch") return "🏢";
  if (unitType === "department") return "🏬";
  return "👥";
}

function UnitCard({ node }: { node: OrgChartNode }) {
  return (
    <div className="w-56 shrink-0 rounded-lg border bg-card p-3 shadow-sm">
      <div className="flex items-start justify-between gap-2">
        <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <span>{unitTypeIcon(node.unitType)}</span>
          {node.unitType}
        </p>
        <StatusBadge status={node.status} />
      </div>
      <p className="mt-1 truncate text-sm font-semibold text-foreground">{node.name}</p>
      {node.code ? (
        <p className="truncate text-xs text-muted-foreground">{node.code}</p>
      ) : null}
      <div className="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">
        <Users className="size-3.5" />
        <span>{node.headcount} worker{node.headcount === 1 ? "" : "s"} active</span>
      </div>
      {node.managerName ? (
        <div className="mt-2 rounded-md bg-muted/60 p-2">
          <p className="truncate text-xs font-medium text-foreground">{node.managerName}</p>
          {node.managerJobTitle ? (
            <p className="truncate text-[11px] text-muted-foreground">{node.managerJobTitle}</p>
          ) : null}
        </div>
      ) : (
        <p className="mt-2 text-[11px] italic text-muted-foreground">No unit lead set</p>
      )}
      {node.legalEntityName && node.unitType !== "entity" ? (
        <p className="mt-2 flex items-center gap-1 text-[11px] text-muted-foreground">
          <Building2 className="size-3" />
          <span className="truncate">{node.legalEntityName}</span>
        </p>
      ) : null}
    </div>
  );
}

function UnitRow({ nodes }: { nodes: OrgChartNode[] }) {
  if (nodes.length === 0) return null;
  return (
    <div className="relative flex flex-wrap items-start justify-center gap-4">
      {nodes.map((n) => (
        <div key={n.id} className="flex flex-col items-center">
          <UnitCard node={n} />
          <UnitRow nodes={n.children} />
        </div>
      ))}
    </div>
  );
}

function OrgChartPage() {
  const { user } = useAuth();
  const permitted = import.meta.env.VITE_USE_REAL_API !== "true" || canManageHrmStructure(user?.roles ?? []);
  return <AppShell><AuthGate>{permitted ? <OrgChartContent /> : <p role="alert">Organisation chart access is not assigned.</p>}</AuthGate></AppShell>;
}

function OrgChartContent() {
  const state = useApi<OrgChart>(() => realApi.orgChart(), []);

  return (
    <>
        <PageHeader
          eyebrow="Organisation"
          title="Organization chart"
          description="How the organisation is structured: legal entities, branches, departments and teams, with active headcount and unit leads."
        />
        <Async state={state}>
          {(chart) => (
            <div>
              {chart.roots.length === 0 ? (
                <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
                  No active organizational units yet. Create legal entities and units under
                  Configuration › Organisation structure, and the chart will appear here.
                </div>
              ) : (
                <div className="mt-6 space-y-10 overflow-x-auto pb-10">
                  {chart.roots.map((root) => (
                    <div key={root.id} className="flex min-w-max justify-center px-4">
                      <div className="flex flex-col items-center">
                        <UnitCard node={root} />
                        <UnitRow nodes={root.children} />
                      </div>
                    </div>
                  ))}
                </div>
              )}
              {chart.asAt ? (
                <p className="mt-2 text-xs text-muted-foreground">
                  Reflects current active assignments as at {new Date(chart.asAt).toLocaleString()}
                </p>
              ) : null}
            </div>
          )}
        </Async>
    </>
  );
}
