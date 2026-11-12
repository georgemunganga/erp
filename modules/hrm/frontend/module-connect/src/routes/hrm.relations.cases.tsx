import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { EyeOff, ShieldAlert } from "lucide-react";
import { relationsApi } from "@/mock/relations";
import type { RelationsCase } from "@/mock/relations";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/relations/cases")({
  head: () => ({
    meta: [
      { title: "Employee relations cases — Kuubu HRM" },
      { name: "description", content: "A restricted case queue. The list shows only enough to triage — never the allegation itself." },
      { property: "og:title", content: "Employee relations cases — Kuubu HRM" },
      { property: "og:description", content: "A restricted case queue. The list shows only enough to triage — never the allegation itself." },
    ],
  }),
  component: CasesList,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const typeMap: Record<string, string> = {
  grievance: "Grievance",
  misconduct: "Misconduct allegation",
  harassment: "Bullying or harassment",
  bullying: "Bullying or harassment",
  discrimination: "Discrimination",
  dispute: "Workplace dispute",
};

const stageMap: Record<string, string> = {
  "new": "Intake",
  open: "Intake",
  "in-progress": "Investigation",
  investigation: "Investigation",
  hearing: "Hearing",
  "findings-made": "Findings",
  appeal: "Appeal",
  closed: "Closed",
};

/**
 * The backend case record is deliberately triage-level (id, caseType, category,
 * severity, summary, status, createdAt, subjectWorkerId) — subject names are
 * not returned so the design promise of "never the allegation / anonymised
 * subjects" is honoured at the API layer.
 */
function adaptCase(raw: unknown): RelationsCase {
  const r = raw as Record<string, unknown>;
  const stage = (stageMap[String(r.status ?? "")] ?? String(r.status ?? "Intake")) as RelationsCase["stage"];
  const created = String(r.createdAt ?? "").slice(0, 10);
  return {
    id: String(r.id ?? ""),
    type: (typeMap[String(r.caseType ?? "")] ?? String(r.caseType ?? "Grievance")) as RelationsCase["type"],
    summary: String(r.summary ?? "—"),
    subject: "Subject withheld — conflict check required",
    anonymised: true,
    raisedBy: String(r.raisedBy ?? "Employee"),
    stage,
    owner: "Employee relations officer",
    nextAction: stage === "Closed" ? "File findings" : "Complete conflict-of-interest check",
    dueDate: "—",
    opened: created || "—",
    conflicted: [],
    allegations: [],
    evidence: [],
    timeline: [],
  } satisfies RelationsCase;
}

function CasesList() {
  const state = useApi(async (): Promise<RelationsCase[]> => {
    if (!USE_REAL) return relationsApi.cases();
    const res = await realApi.relationsCases();
    return (res.items as unknown[]).map(adaptCase);
  }, []);
  const [view, setView] = useState("open");
  // `/relations/cases/$id` is generated as a child of this route.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Relations and safety"
        title="Employee relations cases"
        description="Grievances, allegations and disputes. Access is restricted and every view is logged."
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-danger/30 bg-danger-soft px-2.5 py-0.5 text-xs font-medium text-danger">
            <ShieldAlert className="size-3.5" aria-hidden />
            Restricted — your access to each case is recorded
          </span>
        }
      />

      <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
        <EyeOff className="mt-0.5 size-4 shrink-0" aria-hidden />
        <span>
          This list deliberately shows only enough to triage. The allegation, evidence and any
          findings sit behind a conflict-of-interest declaration on the case itself. Subjects of
          serious allegations are anonymised until findings are made.
        </span>
      </p>

      <Async state={state} rows={3}>
        {(rows) => (
          <ListPage<RelationsCase>
            rows={rows.filter((c) => (view === "open" ? c.stage !== "Closed" : view === "closed" ? c.stage === "Closed" : true))}
            savedViews={[
              { id: "open", label: "Open cases" },
              { id: "closed", label: "Closed" },
              { id: "all", label: "All" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search reference or type"
            searchFields={(c) => `${c.id} ${c.type} ${c.summary}`}
            filters={[
              {
                id: "type",
                label: "Type",
                options: ["Grievance", "Misconduct allegation", "Bullying or harassment", "Discrimination", "Workplace dispute"],
                match: (c, v) => c.type === v,
              },
              {
                id: "stage",
                label: "Stage",
                options: ["Intake", "Conflict check", "Investigation", "Hearing", "Findings", "Appeal", "Closed"],
                match: (c, v) => c.stage === v,
              },
            ]}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (c) => (
                  <Link to="/hrm/relations/cases/$id" params={{ id: c.id }} className="font-mono text-xs text-primary underline underline-offset-2">
                    {c.id}
                  </Link>
                ),
              },
              { id: "type", header: "Type", cell: (c) => c.type },
              {
                id: "subject",
                header: "Subject",
                cell: (c) => (
                  <span className="flex min-w-0 max-w-56 items-center gap-1.5">
                    {c.anonymised ? <EyeOff className="size-3.5 shrink-0 text-muted-foreground" aria-label="Anonymised" /> : null}
                    <span className="truncate">{c.subject}</span>
                  </span>
                ),
              },
              { id: "stage", header: "Stage", cell: (c) => c.stage },
              { id: "opened", header: "Opened", cell: (c) => c.opened },
              { id: "next", header: "Next action", cell: (c) => <span className="block max-w-56 truncate text-xs">{c.nextAction} · due {c.dueDate}</span> },
              { id: "owner", header: "Owner", defaultVisible: false, cell: (c) => c.owner },
            ]}
            emptyBody="No cases match the current view."
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
