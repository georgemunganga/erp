import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import type { CertificateState, Course, Enrolment } from "@/mock/talent";
import { certificateState, daysUntil, talentApi } from "@/mock/talent";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/talent/learning")({
  head: () => ({
    meta: [
      { title: "Learning — Kuubu HRM" },
      {
        name: "description",
        content:
          "The course catalogue and my enrolments, with certificate expiry, recertification dates and what a lapsed certificate stops me doing.",
      },
      { property: "og:title", content: "Learning — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "The course catalogue and my enrolments, with certificate expiry, recertification dates and what a lapsed certificate stops me doing.",
      },
    ],
  }),
  component: LearningPage,
});

interface LearningRow {
  id: string;
  kind: "My enrolment" | "Catalogue";
  title: string;
  provider: string;
  mode: string;
  durationHours: number;
  mandatory: boolean;
  cpdPoints: number;
  category: string;
  status: string;
  progress: number | null;
  certificateRef?: string;
  certificateExpiry?: string;
  recertificationDue?: string;
  certificate: CertificateState;
  owner: string;
  nextAction: string;
  dueDate: string;
  fitnessImpact?: string;
  nextCohort: string;
}

function buildRows(courses: Course[], enrolments: Enrolment[]): LearningRow[] {
  const byId = new Map(courses.map((c) => [c.id, c]));
  const enrolled = new Set(enrolments.map((e) => e.courseId));

  const mine: LearningRow[] = enrolments.flatMap((e) => {
    const c = byId.get(e.courseId);
    if (!c) return [];
    return [
      {
        id: e.id,
        kind: "My enrolment",
        title: c.title,
        provider: c.provider,
        mode: c.mode,
        durationHours: c.durationHours,
        mandatory: c.mandatory,
        cpdPoints: c.cpdPoints,
        category: c.category,
        status: e.status,
        progress: e.progress,
        certificateRef: e.certificateRef,
        certificateExpiry: e.certificateExpiry,
        recertificationDue: e.recertificationDue,
        certificate: certificateState(e.certificateExpiry),
        owner: e.owner,
        nextAction: e.nextAction,
        dueDate: e.dueDate,
        fitnessImpact: e.fitnessImpact,
        nextCohort: c.nextCohort,
      },
    ];
  });

  const catalogue: LearningRow[] = courses.map((c) => ({
    id: `cat-${c.id}`,
    kind: "Catalogue",
    title: c.title,
    provider: c.provider,
    mode: c.mode,
    durationHours: c.durationHours,
    mandatory: c.mandatory,
    cpdPoints: c.cpdPoints,
    category: c.category,
    status: enrolled.has(c.id) ? "Already enrolled" : "Available",
    progress: null,
    certificate: "No certificate",
    owner: c.provider,
    nextAction: enrolled.has(c.id)
      ? "No action — see 'My enrolments'"
      : c.mandatory
        ? "Enrol — this course is required for your role"
        : "Enrol if it supports a goal in your cycle",
    dueDate: c.nextCohort,
    nextCohort: c.nextCohort,
  }));

  return [...mine, ...catalogue];
}

function CertificateAlert({ rows }: { rows: LearningRow[] }) {
  const atRisk = rows.filter(
    (r) => r.kind === "My enrolment" && (r.certificate === "Expiring" || r.certificate === "Expired"),
  );
  if (!atRisk.length) return null;

  return (
    <section
      aria-label="Certificates needing attention"
      className="rounded-lg border border-warning/40 bg-warning-soft p-4"
    >
      <h2 className="flex items-center gap-2 text-sm font-semibold text-warning">
        <AlertTriangle className="size-4 shrink-0" aria-hidden />
        Action required — {atRisk.length} certificates affect what you are fit to do
      </h2>
      <ul className="mt-3 space-y-3">
        {atRisk.map((r) => {
          const days = r.certificateExpiry ? daysUntil(r.certificateExpiry) : 0;
          return (
            <li key={r.id} className="rounded-md border bg-surface p-3">
              <p className="text-sm font-medium">
                {r.title} — {r.certificate === "Expired" ? "expired" : "expiring"}
              </p>
              <p className="mt-0.5 text-xs text-muted-foreground">
                Certificate {r.certificateRef} · expires {r.certificateExpiry} ·{" "}
                {r.certificate === "Expired"
                  ? `${Math.abs(days)} days ago`
                  : `in ${days} days`}{" "}
                · recertification due {r.recertificationDue}
              </p>
              {r.fitnessImpact ? <p className="mt-2 text-sm">{r.fitnessImpact}</p> : null}
              <p className="mt-2 text-xs text-muted-foreground">
                Next action: {r.nextAction} · due {r.dueDate} · owner {r.owner}
              </p>
            </li>
          );
        })}
      </ul>
      <p className="mt-3 text-xs text-warning">
        Certificate lapses are shared with scheduling: an expired licence removes you from work that requires it
        until it is renewed.
      </p>
    </section>
  );
}

function LearningPage() {
  const state = useMock(() => talentApi.learning());
  const [view, setView] = useState("mine");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Talent · Learning"
        title="Learning and certificates"
        description="Everything you are enrolled on, everything you could enrol on, and every certificate whose expiry changes what you are authorised to do."
        primaryAction={<Button>Enrol on a course</Button>}
      />
      <Async state={state}>
        {({ courses, enrolments }) => {
          const all = buildRows(courses, enrolments);
          const rows = all.filter((r) =>
            view === "mine"
              ? r.kind === "My enrolment"
              : view === "catalogue"
                ? r.kind === "Catalogue"
                : view === "mandatory"
                  ? r.mandatory
                  : view === "certificates"
                    ? r.certificate === "Expiring" || r.certificate === "Expired"
                    : true,
          );

          return (
            <div className="space-y-4">
              <CertificateAlert rows={all} />

              <ListPage<LearningRow>
                rows={rows}
                savedViews={[
                  { id: "mine", label: "My enrolments" },
                  { id: "catalogue", label: "Course catalogue" },
                  { id: "mandatory", label: "Mandatory and licences" },
                  { id: "certificates", label: "Certificates needing renewal" },
                  { id: "all", label: "Everything" },
                ]}
                activeView={view}
                onViewChange={setView}
                searchPlaceholder="Search course, provider or category"
                searchFields={(r) => `${r.id} ${r.title} ${r.provider} ${r.category} ${r.mode}`}
                emptyBody="No courses or enrolments match this view. Try 'Everything', or clear the filters."
                filters={[
                  {
                    id: "category",
                    label: "Category",
                    options: ["Health and safety", "Licences", "Technical", "Leadership", "Compliance"],
                    match: (r, v) => r.category === v,
                  },
                  {
                    id: "mode",
                    label: "Delivery",
                    options: ["E-learning", "Classroom", "Virtual classroom", "On-the-job", "Blended"],
                    match: (r, v) => r.mode === v,
                  },
                  {
                    id: "mandatory",
                    label: "Required",
                    options: ["Mandatory", "Optional"],
                    match: (r, v) => (v === "Mandatory" ? r.mandatory : !r.mandatory),
                  },
                ]}
                bulkActions={[{ label: "Add to my learning plan", onSelect: () => undefined }]}
                columns={[
                  {
                    id: "course",
                    header: "Course",
                    cell: (r) => (
                      <div className="max-w-80 space-y-0.5">
                        <p className="font-medium">{r.title}</p>
                        <p className="text-xs text-muted-foreground">
                          {r.provider} · {r.mode} · {r.durationHours} hours · {r.cpdPoints} CPD points
                        </p>
                        <p className="text-xs text-muted-foreground">
                          {r.mandatory ? "Mandatory for your role" : "Optional"} · {r.category}
                        </p>
                      </div>
                    ),
                  },
                  {
                    id: "kind",
                    header: "Record",
                    cell: (r) => (
                      <span className="inline-flex rounded-full border bg-surface-muted px-2 py-0.5 text-xs">
                        {r.kind}
                      </span>
                    ),
                  },
                  { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
                  {
                    id: "progress",
                    header: "Progress",
                    cell: (r) =>
                      r.progress === null ? (
                        <span className="text-xs text-muted-foreground">
                          Not enrolled · next cohort {r.nextCohort}
                        </span>
                      ) : (
                        <div className="w-36 space-y-1">
                          <Progress
                            value={r.progress}
                            aria-label={`${r.title}: ${r.progress}% complete`}
                            className="h-2"
                          />
                          <p className="tabular text-xs">{r.progress}% complete</p>
                        </div>
                      ),
                  },
                  {
                    id: "certificate",
                    header: "Certificate",
                    cell: (r) =>
                      r.certificate === "No certificate" ? (
                        <span className="text-xs text-muted-foreground">No certificate held</span>
                      ) : (
                        <div className="max-w-48 space-y-0.5 text-xs">
                          <p className="font-medium">{r.certificate}</p>
                          <p className="text-muted-foreground">Expires {r.certificateExpiry}</p>
                          <p className="text-muted-foreground">Recertify by {r.recertificationDue}</p>
                        </div>
                      ),
                  },
                  {
                    id: "owner",
                    header: "Owner",
                    cell: (r) => <span className="block max-w-40 truncate text-xs">{r.owner}</span>,
                  },
                  {
                    id: "next",
                    header: "Next action",
                    cell: (r) => (
                      <span className="block max-w-56 text-xs">
                        {r.nextAction} · due {r.dueDate}
                      </span>
                    ),
                  },
                  { id: "provider", header: "Provider", defaultVisible: false, cell: (r) => r.provider },
                  { id: "category", header: "Category", defaultVisible: false, cell: (r) => r.category },
                  { id: "cpd", header: "CPD points", defaultVisible: false, cell: (r) => r.cpdPoints },
                  {
                    id: "fitness",
                    header: "Fitness to work",
                    defaultVisible: false,
                    cell: (r) => r.fitnessImpact ?? "No effect on fitness to work",
                  },
                ]}
              />

              <p className="text-xs text-muted-foreground">
                Recertification dates feed the fitness-to-work check used by scheduling. Expiry is shown as a word
                as well as a date — see the{" "}
                <Link to="/hrm/talent/goals" className="text-primary underline underline-offset-2">
                  goals
                </Link>{" "}
                that depend on a licence staying in date.
              </p>
            </div>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
