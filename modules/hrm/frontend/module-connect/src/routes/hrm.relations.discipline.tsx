import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { CircleSlash, EyeOff, Info } from "lucide-react";
import { hasLapsed, relationsApi } from "@/mock/relations";
import type { Warning } from "@/mock/relations";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/relations/discipline")({
  head: () => ({
    meta: [
      { title: "Warning register — Kuubu HRM" },
      { name: "description", content: "Disciplinary warnings, when they lapse, and why a lapsed warning cannot be relied on." },
      { property: "og:title", content: "Warning register — Kuubu HRM" },
      { property: "og:description", content: "Disciplinary warnings, when they lapse, and why a lapsed warning cannot be relied on." },
    ],
  }),
  component: DisciplinePage,
});

function DisciplinePage() {
  const state = useMock(() => relationsApi.warnings());
  const [view, setView] = useState("active");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Relations and safety"
        title="Warning register"
        description="Every warning has a life. Once it lapses it stays on the record for audit, but it must not influence a later decision."
      />

      <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
        <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>
          A lapsed warning is not a live warning. Counting an old warning against someone after it
          has expired is a common and expensive mistake, so lapsed entries are shown struck through
          rather than hidden or deleted.
        </span>
      </p>

      <Async state={state} rows={3}>
        {(rows) => (
          <ListPage<Warning>
            rows={rows.filter((w) =>
              view === "active" ? !hasLapsed(w.expires) : view === "lapsed" ? hasLapsed(w.expires) : true,
            )}
            savedViews={[
              { id: "active", label: "Active warnings" },
              { id: "lapsed", label: "Lapsed" },
              { id: "all", label: "All" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference, employee or reason"
            searchFields={(w) => `${w.id} ${w.employee} ${w.level} ${w.reason}`}
            filters={[
              {
                id: "level",
                label: "Level",
                options: ["Verbal caution", "Written warning", "Final written warning", "Suspension"],
                match: (w, v) => w.level === v,
              },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (w) => <span className="font-mono text-xs">{w.id}</span> },
              {
                id: "employee",
                header: "Employee",
                cell: (w) => (
                  <span className="flex min-w-0 max-w-56 items-center gap-1.5">
                    {w.anonymised ? <EyeOff className="size-3.5 shrink-0 text-muted-foreground" aria-label="Anonymised" /> : null}
                    <span className="truncate">{w.employee}</span>
                  </span>
                ),
              },
              { id: "level", header: "Level", cell: (w) => w.level },
              { id: "reason", header: "Reason", cell: (w) => <span className="block max-w-64 truncate text-xs">{w.reason}</span> },
              { id: "issued", header: "Issued", cell: (w) => w.issued },
              {
                id: "expires",
                header: "Status",
                cell: (w) =>
                  hasLapsed(w.expires) ? (
                    <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                      <CircleSlash className="size-3.5 shrink-0" aria-hidden />
                      <s>Lapsed {w.expires}</s>
                    </span>
                  ) : (
                    <span className="text-xs">Active until {w.expires}</span>
                  ),
              },
              { id: "appeal", header: "Appeal", cell: (w) => <span className="text-xs">{w.appeal}</span> },
              {
                id: "case",
                header: "Linked case",
                defaultVisible: false,
                cell: (w) =>
                  w.caseId ? (
                    <Link to="/hrm/relations/cases/$id" params={{ id: w.caseId }} className="font-mono text-xs text-primary underline underline-offset-2">
                      {w.caseId}
                    </Link>
                  ) : (
                    <span className="text-xs text-muted-foreground">None</span>
                  ),
              },
              { id: "by", header: "Issued by", defaultVisible: false, cell: (w) => <span className="text-xs">{w.issuedBy}</span> },
            ]}
            emptyBody="No warnings match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
