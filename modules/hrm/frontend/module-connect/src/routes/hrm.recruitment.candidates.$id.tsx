import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, Check, ShieldCheck } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { entities } from "@/mock/data";
import { money, pipelineStages, recruitmentApi } from "@/mock/recruitment";
import type { Candidate, CandidateSource, Scorecard } from "@/mock/recruitment";
import type { RequestStatus } from "@/mock/types";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/recruitment/candidates/$id")({
  head: () => ({
    meta: [
      { title: "Candidate — Kuubu HRM" },
      {
        name: "description",
        content:
          "Selection pipeline, interview scorecards, reference and background checks, retention basis and the offer decision.",
      },
      { property: "og:title", content: "Candidate — Kuubu HRM" },
      {
        property: "og:description",
        content: "Pipeline, scorecards, checks, retention basis and the offer decision for one candidate.",
      },
    ],
  }),
  component: CandidateDetail,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const entityName = (id: string) => entities.find((e) => e.id === id)?.name ?? "Demo Logistics Zambia Ltd";

const stageMap: Record<string, string> = {
  applied: "Applied",
  screening: "Screening",
  shortlisted: "Shortlisted",
  interview: "Interview",
  interviewing: "Interview",
  interviewed: "Interview",
  offer: "Offer",
  offered: "Offer",
  preboarding: "Offer",
  hired: "Hired",
  rejected: "Rejected",
  withdrawn: "Withdrawn",
};

/** Build a candidate context from the backend (no single context endpoint yet). */
async function loadCandidateContext(id: string): Promise<{
  candidate: Candidate | null;
  vacancy: { id: string; jobTitle: string; branch: string; entityId: string } | null;
  requisition: { id: string; grade: string; jobTitle: string } | null;
  peers: { id: string; fullName: string; stage: string }[];
}> {
  const vacancies = await realApi.recruitmentVacancies();
  for (const raw of vacancies.items) {
    const v = raw as Record<string, unknown>;
    const vacancyId = String(v.id ?? "");
    try {
      const res = await realApi.vacancyCandidates(vacancyId);
      const match = (res.items as unknown[]).find((c) => String((c as Record<string, unknown>).id ?? "") === id);
      if (!match) continue;
      const r = match as Record<string, unknown>;
      const stage = (stageMap[String(r.stage ?? "")] ?? String(r.stage ?? "Applied")) as Candidate["stage"];
      const appliedOn = String(r.createdAt ?? "").slice(0, 10);
      const candidate: Candidate = {
        id: String(r.id ?? ""),
        reference: String(r.reference ?? r.id ?? ""),
        fullName: String(r.fullName ?? ""),
        vacancyId,
        appliedOn: appliedOn || "—",
        stage,
        source: (String(r.source ?? "") as CandidateSource) || "Careers portal",
        sourceDetail: String(r.sourceDetail ?? ""),
        status: "In progress" as RequestStatus,
        owner: String(r.owner ?? "Talent acquisition"),
        nextAction: "Screen application",
        dueDate: "—",
        location: String(r.location ?? "Lusaka, Zambia"),
        rightToWork: String(r.rightToWork ?? "—"),
        noticePeriod: String(r.noticePeriod ?? "—"),
        currentRole: String(r.currentRole ?? ""),
        salaryExpectation: String(r.salaryExpectation ?? ""),
        consent: { lawfulBasis: "Consent", obtainedOn: appliedOn, retainUntil: "—", state: "Consent current", note: "" },
        scorecards: [],
        checks: [],
        policy: [],
        conflicts: [],
        timeline: [],
      } satisfies Candidate;
      const vacancy = {
        id: vacancyId,
        jobTitle: String(v.jobTitle ?? ""),
        branch: String(v.orgUnitName ?? v.location ?? "—"),
        entityId: String((v as Record<string, unknown>).legalEntityId ?? "ent-zm1"),
      };
      const peers = (res.items as unknown[])
        .filter((c) => String((c as Record<string, unknown>).id ?? "") !== id)
        .map((p) => ({ id: String((p as Record<string, unknown>).id ?? ""), fullName: String((p as Record<string, unknown>).fullName ?? ""), stage: String((p as Record<string, unknown>).stage ?? "") }));
      return { candidate, vacancy, requisition: null, peers };
    } catch {
      continue;
    }
  }
  return { candidate: null, vacancy: null, requisition: null, peers: [] };
}

/** Ratings are shown as a number out of five and as a bar, never as colour alone. */
function Rating({ value, label }: { value: number; label: string }) {
  return (
    <span className="inline-flex items-center gap-2">
      <span className="tabular text-xs font-medium">
        {value} of 5
        <span className="sr-only"> for {label}</span>
      </span>
      <span aria-hidden className="flex gap-0.5">
        {[1, 2, 3, 4, 5].map((n) => (
          <span
            key={n}
            className={`h-1.5 w-4 rounded-full ${n <= value ? "bg-primary" : "bg-muted"}`}
          />
        ))}
      </span>
    </span>
  );
}

function ScorecardCard({ s }: { s: Scorecard }) {
  return (
    <article className="rounded-md border bg-surface-muted p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <h3 className="text-sm font-semibold">{s.stage}</h3>
          <p className="text-xs text-muted-foreground">
            {s.interviewer} · {s.interviewerRole} · held {s.heldOn}
          </p>
        </div>
        <div className="flex flex-col items-end gap-1">
          <Rating value={s.overall} label="overall" />
          <span className="rounded-full border bg-surface px-2 py-0.5 text-xs font-medium">{s.recommendation}</span>
        </div>
      </div>
      <ul className="mt-3 space-y-2 border-t pt-3">
        {s.criteria.map((c) => (
          <li key={c.label} className="text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium">{c.label}</span>
              <Rating value={c.rating} label={c.label} />
            </div>
            <p className="text-xs text-muted-foreground">{c.note}</p>
          </li>
        ))}
      </ul>
      <p className="mt-3 border-t pt-3 text-sm text-muted-foreground">{s.comment}</p>
    </article>
  );
}

function Pipeline({ candidate }: { candidate: Candidate }) {
  const current = pipelineStages.indexOf(candidate.stage);
  const exited = current === -1;
  const exitFrom = [...candidate.timeline].reverse().find((e) => e.after === candidate.stage)?.before;

  return (
    <div>
      <ol className="flex flex-wrap gap-2" aria-label="Selection pipeline">
        {pipelineStages.map((stage, i) => {
          const done = !exited && i < current;
          const isCurrent = !exited && i === current;
          return (
            <li
              key={stage}
              aria-current={isCurrent ? "step" : undefined}
              className={`flex items-center gap-2 rounded-full border px-3 py-1 text-xs ${
                isCurrent
                  ? "border-primary bg-primary-soft font-medium text-primary"
                  : done
                    ? "border-success/40 bg-success-soft text-success"
                    : "bg-surface text-muted-foreground"
              }`}
            >
              <span
                aria-hidden
                className="grid size-4 shrink-0 place-items-center rounded-full border text-[10px]"
              >
                {done ? <Check className="size-2.5" /> : i + 1}
              </span>
              {stage}
              <span className="sr-only">
                {isCurrent ? " — current stage" : done ? " — completed" : " — not reached"}
              </span>
            </li>
          );
        })}
      </ol>
      {exited ? (
        <p className="mt-3 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm">
          <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
          <span>
            This candidate is <span className="font-medium">{candidate.stage.toLowerCase()}</span>
            {exitFrom ? ` and left the process at the ${exitFrom.toLowerCase()} stage` : ""}. The reason is recorded in
            the history below and was shared with the candidate.
          </span>
        </p>
      ) : null}
    </div>
  );
}

function CandidateDetail() {
  const { id } = Route.useParams();
  const [advancing, setAdvancing] = useState<string | null>(null);
  const state = useApi(async () => {
    if (!USE_REAL) return recruitmentApi.candidateContext(id);
    return loadCandidateContext(id);
  }, [id]);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {({ candidate: c, vacancy, requisition, peers }) => {
          if (!c) return <RestrictedState />;

          const offerDecidable = c.stage === "Offer" && Boolean(c.offer);
          const decisionSummary = c.offer
            ? offerDecidable
              ? `Authorise an offer to ${c.fullName} at grade ${c.offer.grade}, ${money(c.offer.baseSalary, c.offer.currency)} a year, starting ${c.offer.proposedStart}. The offer lapses on ${c.offer.expiresOn}.`
              : `The offer to ${c.fullName} at ${money(c.offer.baseSalary, c.offer.currency)} has already been decided — the candidate is ${c.stage.toLowerCase()}. This panel is read-only.`
            : `There is no offer to decide yet. ${c.fullName} is at the ${c.stage.toLowerCase()} stage; an offer can only be prepared once the interviews and scorecards are complete.`;

          return (
            <RecordDetail
              reference={c.reference}
              title={c.fullName}
              subtitle={`${vacancy ? `${vacancy.jobTitle} — ${vacancy.branch}` : "Vacancy withdrawn"} · applied ${c.appliedOn} · ${c.source}`}
              status={c.status}
              owner={c.owner}
              nextAction={c.nextAction}
              dueDate={c.dueDate}
              secondaryActions={
                <>
                  <Button variant="outline" size="sm" asChild>
                    <Link to="/hrm/recruitment/candidates">Back to candidates</Link>
                  </Button>
                  <Button variant="outline" size="sm" asChild>
                    <Link to="/hrm/recruitment/vacancies">View the vacancy</Link>
                  </Button>
                </>
              }
              summary={[
                { label: "Selection stage", value: c.stage },
                { label: "Source", value: c.sourceDetail },
                { label: "Vacancy", value: vacancy ? `${vacancy.id} — ${vacancy.jobTitle}` : "Withdrawn" },
                { label: "Requisition", value: requisition ? `${requisition.id} · ${requisition.grade}` : "Not linked" },
                { label: "Legal entity", value: vacancy ? entityName(vacancy.entityId) : "Not linked" },
                { label: "Current role", value: c.currentRole },
                { label: "Location", value: c.location },
                { label: "Notice period", value: c.noticePeriod },
                { label: "Salary expectation", value: c.salaryExpectation },
                { label: "Right to work", value: c.rightToWork },
                { label: "Scorecards recorded", value: `${c.scorecards.length}` },
                { label: "Retention", value: `Retain until ${c.consent.retainUntil}` },
              ]}
              timeline={<StatusTimeline title="Selection history" events={c.timeline} />}
              related={
                <>
                  <Link to="/hrm/recruitment/vacancies" className="block text-primary underline underline-offset-2">
                    {vacancy ? `${vacancy.id} — ${vacancy.jobTitle}` : "Vacancies"}
                  </Link>
                  <Link to="/hrm/recruitment/requisitions" className="block text-primary underline underline-offset-2">
                    {requisition ? `${requisition.id} — ${requisition.jobTitle}` : "Requisitions"}
                  </Link>
                  {peers.length ? (
                    <p className="pt-2 text-xs text-muted-foreground">
                      {peers.length} other {peers.length === 1 ? "candidate is" : "candidates are"} in the process for
                      this vacancy.
                    </p>
                  ) : null}
                  {peers.slice(0, 3).map((p) => (
                    <Link
                      key={p.id}
                      to="/hrm/recruitment/candidates/$id"
                      params={{ id: p.id }}
                      className="block text-primary underline underline-offset-2"
                    >
                      {p.fullName} — {p.stage}
                    </Link>
                  ))}
                </>
              }
            >
              <DetailSection
                title="Selection pipeline"
                description="Where this candidate has reached. Moving a stage always records who moved it and why."
              >
                <Pipeline candidate={c} />
              </DetailSection>

              <DetailSection
                title="Interview scorecards"
                description="Each interviewer scores independently before the panel discusses. Scores are shown as a number out of five, not as a colour."
              >
                {c.scorecards.length ? (
                  <div className="space-y-4">
                    {c.scorecards.map((s) => (
                      <ScorecardCard key={s.id} s={s} />
                    ))}
                  </div>
                ) : (
                  <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                    No scorecards yet. At least two independent scorecards are required before a shortlist or offer
                    decision.
                  </p>
                )}
              </DetailSection>

              <DetailSection
                title="References and background checks"
                description="References are only taken up with the candidate's agreement. An offer may be made conditional on a check that is still running."
              >
                <ul className="space-y-3">
                  {c.checks.map((b) => (
                    <li key={b.id} className="flex flex-wrap items-start justify-between gap-3 border-b pb-3 last:border-0 last:pb-0">
                      <div className="min-w-0">
                        <p className="text-sm font-medium">{b.label}</p>
                        <p className="text-xs text-muted-foreground">
                          {b.provider} · updated {b.updatedOn}
                        </p>
                        <p className="mt-1 max-w-xl text-sm text-muted-foreground">{b.note}</p>
                      </div>
                      <StatusBadge status={b.outcome} />
                    </li>
                  ))}
                </ul>
              </DetailSection>

              {c.offer ? (
                <DetailSection
                  title="Proposed offer"
                  description="What the candidate would be offered, and how it compares with the approved requisition and the grade range."
                >
                  <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {[
                      ["Grade", c.offer.grade],
                      ["Base salary", money(c.offer.baseSalary, c.offer.currency)],
                      ["Approved requisition cost", money(c.offer.approvedBudget, c.offer.currency)],
                      ["Contract", c.offer.contractType],
                      ["Proposed start", c.offer.proposedStart],
                      ["Probation", `${c.offer.probationMonths} months`],
                      ["Allowances", c.offer.allowances],
                      ["Offer lapses", c.offer.expiresOn],
                    ].map(([k, v]) => (
                      <div key={String(k)} className="min-w-0">
                        <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{k}</dt>
                        <dd className="mt-1 text-sm">{v}</dd>
                      </div>
                    ))}
                  </dl>
                  <p className="mt-4 rounded-md border bg-surface-muted p-3 text-sm text-muted-foreground">
                    {c.offer.comparatorNote}
                  </p>
                </DetailSection>
              ) : null}

              <DetailSection
                title="Data protection and retention"
                description="Why the organisation is allowed to hold this candidate's data, and when it disappears."
              >
                <div className="flex flex-wrap items-start gap-3 rounded-md border border-info/30 bg-info-soft p-4 text-sm text-info">
                  <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
                  <div className="min-w-0 space-y-1">
                    <p>
                      <span className="font-medium">Lawful basis:</span> {c.consent.lawfulBasis}
                    </p>
                    <p>
                      <span className="font-medium">Obtained:</span> {c.consent.obtainedOn} ·{" "}
                      <span className="font-medium">Retain until:</span> {c.consent.retainUntil} ·{" "}
                      <span className="font-medium">State:</span> {c.consent.state}
                    </p>
                    <p>{c.consent.note}</p>
                  </div>
                </div>
              </DetailSection>

              <ApprovalPanel
                decisionSummary={decisionSummary}
                policy={c.policy}
                conflicts={c.conflicts}
                evidence={c.scorecards.map((s) => ({ label: `Scorecard — ${s.stage} (${s.interviewer})`, href: "#" }))}
                delegates={["Sanne Verhoeven (Operations Director)", "Thandiwe Banda (HR operations)"]}
                disabled={!offerDecidable || advancing !== null}
                onDecision={async (decision, reason, _delegate) => {
                  if (!USE_REAL) return;
                  const targetStage = decision === "approve" ? "hired" : decision === "return" ? "screening" : "rejected";
                  setAdvancing(targetStage);
                  try {
                    await realApi.advanceCandidate(c.id, { stage: targetStage, notes: reason });
                    feedback.submitted(
                      decision === "approve" ? `Candidate advanced to ${targetStage}.` : `Candidate moved to ${targetStage}.`,
                      reason || "The decision and its reason are recorded against the candidate.",
                    );
                    await state.reload();
                  } catch (e) {
                    feedback.blocked("Could not advance the candidate", e instanceof Error ? e.message : "Unknown error.");
                  } finally {
                    setAdvancing(null);
                  }
                }}
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
