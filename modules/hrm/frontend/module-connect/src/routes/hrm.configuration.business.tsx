import { createFileRoute, redirect } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { Archive, BriefcaseBusiness, Plus, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EditDrawer } from "@/platform/components/EditDrawer";
import { feedback } from "@/platform/feedback";
import { Async } from "@/platform/components/Async";
import { ConfigPage, ConfigTable } from "@/platform/components/ConfigPage";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/business")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/$slug", params: { slug: "business" }, replace: true }); },
  head: () => ({
    meta: [
      { title: "Business setup — Newworldcargo HRM" },
      { name: "description", content: "Live designations and business master data." },
      { property: "og:title", content: "Business setup — Newworldcargo HRM" },
      { property: "og:description", content: "Live designations and business master data." },
    ],
  }),
  component: BusinessConfig,
});

const SECTIONS = [
  { id: "designations", label: "Designations" },
  { id: "grades", label: "Employee grades" },
  { id: "calendar", label: "Calendars and holidays" },
  { id: "packs", label: "Country packs" },
  { id: "lang", label: "Language" },
];

type Job = {
  id: string;
  code: string;
  title: string;
  orgUnitName: string | null;
  grade: string | null;
  status: string;
};

function asJob(value: unknown): Job {
  const row = value as Record<string, unknown>;
  return {
    id: String(row.id ?? ""),
    code: String(row.code ?? ""),
    title: String(row.title ?? ""),
    orgUnitName: row.orgUnitName ? String(row.orgUnitName) : null,
    grade: row.grade ? String(row.grade) : null,
    status: String(row.status ?? "active"),
  };
}

export function BusinessConfig() {
  const [tab, setTab] = useState("designations");
  const [editing, setEditing] = useState<Job | null>(null);
  const [creating, setCreating] = useState(false);
  const state = useApi(() => realApi.jobs({ includeInactive: true }));
  const jobs = useMemo(() => (state.data ?? []).map(asJob), [state.data]);

  async function save(values: Record<string, string>, changed: string[]) {
    try {
      if (creating) {
        await realApi.createJob({
          code: values.code.trim(),
          title: values.title.trim(),
          grade: values.grade.trim() || null,
        });
        feedback.saved(`${values.title.trim()} designation created.`);
      } else if (editing) {
        await realApi.updateJob(editing.id, {
          ...(changed.includes("title") ? { title: values.title.trim() } : {}),
          ...(changed.includes("grade") ? { grade: values.grade.trim() || null } : {}),
        });
        feedback.saved(`${values.title.trim()} designation updated.`);
      }
      state.reload();
    } catch (error) {
      feedback.blocked("Designation was not saved.", error instanceof Error ? error.message : "The HRM API rejected the change.");
    }
  }

  async function closeDesignation(job: Job) {
    try {
      await realApi.closeJob(job.id);
      feedback.saved(`${job.title} was archived from new selections.`);
      state.reload();
    } catch (error) {
      feedback.blocked("Designation was not archived.", error instanceof Error ? error.message : "The HRM API rejected the change.");
    }
  }

  return (
    <ConfigPage
      title="Business setup"
      description="Maintain live business master data used by employee assignments and payroll. Changes are stored in PostgreSQL and inactive records remain available for history."
      sections={SECTIONS}
      active={tab}
      onSelect={setTab}
      notice="Designations on this tab are loaded from and saved to the live HRM API. Other tabs remain explicitly unavailable until their persistence contracts are delivered."
    >
      {tab === "designations" ? (
        <Async state={state} rows={5}>
          {() => (
            <>
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 className="text-sm font-semibold">Designations</h2>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Designations are the live Jobs catalogue used by employee assignments. They are not project activities.
                  </p>
                </div>
                <Button className="gap-2" onClick={() => setCreating(true)}>
                  <Plus className="size-4" aria-hidden />
                  Add designation
                </Button>
              </div>
              <ConfigTable
                caption={`${jobs.length} designation${jobs.length === 1 ? "" : "s"} in the live catalogue`}
                headers={["Code", "Designation", "Department", "Grade", "Status", "Action"]}
                rows={jobs.map((job) => [
                  <span className="font-mono text-xs">{job.code}</span>,
                  <span className="font-medium">{job.title}</span>,
                  job.orgUnitName ?? "—",
                  job.grade ?? "—",
                  <span className={job.status === "active" ? "text-success" : "text-muted-foreground"}>{job.status}</span>,
                  <span className="flex flex-wrap gap-1">
                    <Button variant="ghost" size="sm" className="h-7 gap-1.5 px-2 text-xs" onClick={() => setEditing(job)}>
                      <BriefcaseBusiness className="size-3.5" aria-hidden />
                      Edit
                    </Button>
                    {job.status === "active" ? (
                      <Button variant="ghost" size="sm" className="h-7 gap-1.5 px-2 text-xs text-danger" onClick={() => void closeDesignation(job)}>
                        <Archive className="size-3.5" aria-hidden />
                        Archive
                      </Button>
                    ) : null}
                  </span>,
                ])}
              />
            </>
          )}
        </Async>
      ) : (
        <ExplicitlyUnavailable title={SECTIONS.find((section) => section.id === tab)?.label ?? "Business setting"} />
      )}

      <EditDrawer
        open={creating || editing !== null}
        onOpenChange={(open) => {
          if (!open) {
            setCreating(false);
            setEditing(null);
          }
        }}
        title={creating ? "Add designation" : `Edit ${editing?.title ?? "designation"}`}
        description="This writes to the live Jobs catalogue. Archiving is used instead of deleting a designation so employee and payroll history stays explainable."
        initial={{
          code: creating ? "" : editing?.code ?? "",
          title: creating ? "" : editing?.title ?? "",
          grade: creating ? "" : editing?.grade ?? "",
        }}
        fields={[
          { name: "code", label: "Designation code", required: true, hint: creating ? "A stable tenant-scoped code, for example OPS-OFFICER." : "The code is immutable after creation." },
          { name: "title", label: "Designation", required: true },
          { name: "grade", label: "Employee grade", hint: "Optional until the live Employee Grade master is delivered." },
        ]}
        saveLabel={creating ? "Create designation" : "Save designation"}
        onSave={(values, changed) => void save(values, changed)}
        footerNote={
          <span className="flex items-start gap-2">
            <TriangleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            Employee Grade remains a separate master-data slice and is not silently populated from mock data.
          </span>
        }
      />
    </ConfigPage>
  );
}

function ExplicitlyUnavailable({ title }: { title: string }) {
  return (
    <div className="rounded-xl border border-warning/30 bg-warning-soft p-5">
      <div className="flex items-start gap-3">
        <TriangleAlert className="mt-0.5 size-5 shrink-0 text-warning" aria-hidden />
        <div>
          <h2 className="text-sm font-semibold">{title} is not live yet</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            This tab is intentionally disabled until its PostgreSQL contract and permissions are implemented. No demo records are shown in production.
          </p>
        </div>
      </div>
    </div>
  );
}
