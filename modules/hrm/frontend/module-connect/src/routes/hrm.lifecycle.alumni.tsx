import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { extrasApi } from "@/mock/extras";
import type { Alumnus } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/lifecycle/alumni")({
  head: () => ({
    meta: [
      { title: "Alumni and rehire — Kuubu HRM" },
      { name: "description", content: "Former colleagues, whether they can be rehired, and their previous service." },
      { property: "og:title", content: "Alumni and rehire — Kuubu HRM" },
      { property: "og:description", content: "Former colleagues and rehire eligibility." },
    ],
  }),
  component: AlumniPage,
});

function AlumniPage() {
  const state = useMock(() => extrasApi.alumni());
  const [view, setView] = useState("all");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Alumni and rehire"
        description="Good leavers are a hiring pool. Rehire eligibility is set at exit, not guessed at later."
        primaryAction={<Button>Start a rehire</Button>}
      />
      <Async state={state} rows={3}>
        {(rows) => (
          <ListPage<Alumnus>
            rows={rows.filter((a) => (view === "eligible" ? a.rehireEligible !== "No" : true))}
            savedViews={[
              { id: "all", label: "All leavers" },
              { id: "eligible", label: "Rehireable" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search name or role"
            searchFields={(a) => `${a.name} ${a.lastRole}`}
            filters={[
              { id: "reason", label: "Reason", options: ["Resignation", "Contract ended", "Retirement", "Redundancy"], match: (a, v) => a.reason === v },
              { id: "elig", label: "Rehire", options: ["Yes", "With review", "No"], match: (a, v) => a.rehireEligible === v },
            ]}
            columns={[
              { id: "name", header: "Name", cell: (a) => <span className="block max-w-48 truncate font-medium">{a.name}</span> },
              { id: "role", header: "Last role", cell: (a) => <span className="block max-w-48 truncate">{a.lastRole}</span> },
              { id: "left", header: "Left", cell: (a) => a.left },
              { id: "reason", header: "Reason", cell: (a) => a.reason },
              { id: "service", header: "Service", cell: (a) => <span className="tabular">{a.serviceYears} yrs</span> },
              { id: "elig", header: "Rehire", cell: (a) => (
                <span className="block max-w-56">
                  <span className="text-sm">{a.rehireEligible}</span>
                  {a.rehireNote ? <span className="block text-xs text-muted-foreground">{a.rehireNote}</span> : null}
                </span>
              ) },
              { id: "kit", header: "Keep in touch", cell: (a) => (a.keepInTouch ? "Opted in" : "Opted out") },
            ]}
            emptyBody="No former colleagues match this view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
