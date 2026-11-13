import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { entities } from "@/mock/data";
import {
  demoEntityTree,
  flattenEntityTree,
  treePathLabel,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { recruitmentApi } from "@/mock/recruitment";
import type { Vacancy } from "@/mock/recruitment";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/recruitment/vacancies")({
  head: () => ({
    meta: [
      { title: "Vacancies — Kuubu HRM" },
      {
        name: "description",
        content:
          "Live and closed postings created from approved requisitions, with applicant numbers, days open, closing date and the next action.",
      },
      { property: "og:title", content: "Vacancies — Kuubu HRM" },
      {
        property: "og:description",
        content: "Postings from approved requisitions, with applicants, days open, closing date and next action.",
      },
    ],
  }),
  component: VacanciesList,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const entityName = (id: string) => entities.find((e) => e.id === id)?.name ?? "Demo Logistics Zambia Ltd";

/**
 * The backend vacancy record carries far fewer fields than the design mock
 * (id, jobTitle, grade, status, orgUnitName, createdAt). Everything else on
 * the row — applicants, days open, channels, next action — is derived or
 * defaulted until those surfaces exist in the backend.
 */
function adaptVacancy(raw: unknown): Vacancy {
  const r = raw as Record<string, unknown>;
  const status = String(r.status ?? "draft");
  const created = String(r.createdAt ?? "").slice(0, 10);
  const now = new Date().toISOString().slice(0, 10);
  const daysOpen = created && status !== "closed" && status !== "cancelled" ? Math.max(0, Math.floor((new Date(now).getTime() - new Date(created).getTime()) / 86_400_000)) : 0;
    return {
    id: String(r.id ?? ""),
    requisitionId: r.requisitionId ? String(r.requisitionId) : "—",
    jobTitle: String(r.jobTitle ?? ""),
    department: String(r.orgUnitName ?? "—"),
    branch: String(r.location ?? r.locationName ?? "—"),
    grade: String(r.grade ?? "—"),
    entityId: String(r.legalEntityId ?? "ent-zm1"),
    postingStatus: (status === "published" || status === "open") ? "External" : status === "closed" || status === "cancelled" ? "Closed" : "Draft",
    channels: (r.channels as string[]) ?? [],
    applicants: Number(r.candidateCount ?? 0),
    shortlisted: Number(r.candidateCount ?? 0) > 0 ? Math.floor(Number(r.candidateCount ?? 0) / 3) : 0,
    interviewsBooked: 0,
    daysOpen,
    openedOn: created || "—",
    closingDate: r.closingDate ? String(r.closingDate).slice(0, 10) : "—",
    nextAction: status === "closed" || status === "cancelled" ? "Vacancy closed" : "Post vacancy",
    dueDate: r.closingDate ? String(r.closingDate).slice(0, 10) : "—",
    owner: String(r.owner ?? "Talent acquisition"),
  } satisfies Vacancy;
}

function VacanciesList() {
  const state = useApi(async (): Promise<Vacancy[]> => {
    if (!USE_REAL) return recruitmentApi.vacancies();
    const res = await realApi.recruitmentVacancies();
    return (res.items as unknown[]).map(adaptVacancy);
  }, []);
  const treeState = useApi<OrgTreeNode[]>(async () => {
    if (USE_REAL) return (await realApi.entityTree()) as OrgTreeNode[];
    return demoEntityTree;
  }, []);
  const entityTreeOptions = treeToSelectOptions(treeState.data ?? []).map((o) => ({
    ...o,
    entity: o.value.startsWith("entity:"),
  }));
  const entityUnits = flattenEntityTree(treeState.data ?? []);
  const deptTreeOptions = entityUnits.map((e) => ({ value: e.unitName, label: treePathLabel(e.path) }));
  const [view, setView] = useState("live");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Recruitment"
        title="Vacancies"
        description="A vacancy exists only where a requisition has been approved. Postings run internally first, then externally, and every row carries the owner, the next action and the date it is due."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/recruitment/requisitions/new">Raise a requisition</Link>
          </Button>
        }
      />
      <Async state={state}>
        {(rows) => (
          <ListPage<Vacancy>
            rows={rows.filter((v) =>
              view === "live"
                ? v.postingStatus === "Internal" || v.postingStatus === "External"
                : view === "draft"
                  ? v.postingStatus === "Draft"
                  : view === "closing"
                    ? v.postingStatus !== "Closed" && v.closingDate <= "2026-08-09"
                    : view === "closed"
                      ? v.postingStatus === "Closed"
                      : true,
            )}
            savedViews={[
              { id: "live", label: "Advertised now" },
              { id: "closing", label: "Closing within two weeks" },
              { id: "draft", label: "Not yet posted" },
              { id: "closed", label: "Closed" },
              { id: "all", label: "All vacancies" },
            ]}
            activeView={view}
            onViewChange={setView}
            searchPlaceholder="Search vacancy, job title or requisition"
            searchFields={(v) => `${v.id} ${v.jobTitle} ${v.requisitionId} ${v.department}`}
            filters={[
              {
                id: "posting",
                label: "Posting",
                options: ["Draft", "Internal", "External", "Closed"],
                match: (v, value) => v.postingStatus === value,
              },
              {
                id: "entity",
                label: "Entity & branch",
                options: treeToSelectOptions(treeState.data ?? []).map((o) => o.value),
                treeOptions: entityTreeOptions,
                match: (v, value) =>
                  value.startsWith("entity:")
                    ? v.entityId === value.slice(7)
                    : v.department === value,
              },
              {
                id: "applicants",
                label: "Applicants",
                options: ["None yet", "1 to 20", "More than 20"],
                match: (v, value) =>
                  value === "None yet" ? v.applicants === 0 : value === "1 to 20" ? v.applicants > 0 && v.applicants <= 20 : v.applicants > 20,
              },
            ]}
            bulkActions={[{ label: "Export selection", onSelect: () => undefined }]}
            emptyBody="No vacancies match this view. Vacancies appear here once a requisition has been approved."
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (v) => (
                  <span className="block font-mono text-xs">
                    {v.id}
                    <span className="block text-muted-foreground">from {v.requisitionId}</span>
                  </span>
                ),
              },
              {
                id: "job",
                header: "Job title",
                cell: (v) => (
                  <span className="block max-w-56 truncate font-medium">
                    {v.jobTitle}
                    <span className="block truncate text-xs font-normal text-muted-foreground">
                      {v.branch} · {v.grade}
                    </span>
                  </span>
                ),
              },
              { id: "posting", header: "Posting", cell: (v) => <StatusBadge status={v.postingStatus} /> },
              {
                id: "applicants",
                header: "Applicants",
                cell: (v) => (
                  <span className="block text-xs">
                    <span className="tabular font-medium">{v.applicants}</span> applied
                    <span className="block text-muted-foreground">
                      <span className="tabular">{v.shortlisted}</span> shortlisted ·{" "}
                      <span className="tabular">{v.interviewsBooked}</span> interviews booked
                    </span>
                  </span>
                ),
              },
              {
                id: "open",
                header: "Days open",
                cell: (v) => (
                  <span className="block text-xs">
                    <span className="tabular font-medium">{v.daysOpen}</span> days
                    <span className="block text-muted-foreground">Opened {v.openedOn}</span>
                  </span>
                ),
              },
              {
                id: "closing",
                header: "Closing date",
                cell: (v) => (
                  <span className="block text-xs">
                    {v.closingDate}
                    {v.postingStatus === "Closed" ? (
                      <span className="block text-muted-foreground">Closed to applications</span>
                    ) : null}
                  </span>
                ),
              },
              {
                id: "next",
                header: "Next action",
                cell: (v) => (
                  <span className="block max-w-64 text-xs">
                    {v.nextAction}
                    <span className="block text-muted-foreground">Due {v.dueDate}</span>
                  </span>
                ),
              },
              {
                id: "owner",
                header: "Owner",
                defaultVisible: false,
                cell: (v) => <span className="block max-w-56 truncate text-xs">{v.owner}</span>,
              },
              {
                id: "entity",
                header: "Legal entity",
                defaultVisible: false,
                cell: (v) => <span className="block max-w-56 truncate text-xs">{entityName(v.entityId)}</span>,
              },
              {
                id: "department",
                header: "Department",
                defaultVisible: false,
                cell: (v) => <span className="text-xs">{v.department}</span>,
              },
              { id: "grade", header: "Grade", defaultVisible: false, cell: (v) => <span className="text-xs">{v.grade}</span> },
            ]}
          />
        )}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
