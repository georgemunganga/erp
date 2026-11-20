import { createFileRoute, Link } from "@tanstack/react-router";
import { CheckCircle2, MessageSquareWarning, Paperclip } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { employees } from "@/mock/data";
import type { CompetencyRating, Review } from "@/mock/talent";
import { ME, ratingLabel, ratingScale, talentApi, TODAY } from "@/mock/talent";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/talent/reviews/$id")({
  head: () => ({
    meta: [
      { title: "Performance review — Kuubu HRM" },
      {
        name: "description",
        content:
          "Self-assessment and manager rating side by side, the written summary, the evidence behind it, and how to acknowledge or challenge the outcome.",
      },
      { property: "og:title", content: "Performance review — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Self-assessment and manager rating side by side, the written summary, the evidence behind it, and how to acknowledge or challenge the outcome.",
      },
    ],
  }),
  component: ReviewDetail,
});

const name = (id: string) => employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

/** A rating is always a number AND a word. The dot scale is decorative only. */
function Rating({ score, by }: { score: number | null; by: string }) {
  return (
    <div>
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{by}</p>
      <p className="mt-1 text-sm font-medium text-foreground">{ratingLabel(score)}</p>
      <p aria-hidden className="mt-1 flex gap-1">
        {[1, 2, 3, 4, 5].map((n) => (
          <span
            key={n}
            className={`size-2 rounded-full border ${
              score !== null && n <= score ? "border-primary bg-primary" : "border-border bg-transparent"
            }`}
          />
        ))}
      </p>
    </div>
  );
}

function CompetencyRow({ c }: { c: CompetencyRating }) {
  const gap =
    c.selfScore !== null && c.managerScore !== null && c.selfScore !== c.managerScore
      ? c.selfScore > c.managerScore
        ? "Your rating is higher than your manager's — the reasoning is set out alongside."
        : "Your manager rated this higher than you did."
      : null;

  return (
    <li className="rounded-lg border bg-surface-muted p-4">
      <h3 className="text-sm font-semibold text-foreground">{c.competency}</h3>
      <p className="mt-0.5 text-xs text-muted-foreground">{c.descriptor}</p>
      <div className="mt-3 grid gap-4 sm:grid-cols-2">
        <div className="rounded-md border bg-surface p-3">
          <Rating score={c.selfScore} by="Self-assessment" />
          <p className="mt-2 text-sm text-muted-foreground">{c.selfComment || "No comment recorded."}</p>
        </div>
        <div className="rounded-md border bg-surface p-3">
          <Rating score={c.managerScore} by="Manager review" />
          <p className="mt-2 text-sm text-muted-foreground">{c.managerComment || "No comment recorded."}</p>
        </div>
      </div>
      {gap ? (
        <p className="mt-3 rounded-md border border-warning/40 bg-warning-soft px-3 py-2 text-xs text-warning">
          Difference of view: {gap}
        </p>
      ) : null}
    </li>
  );
}

function AppealPanel({ review }: { review: Review }) {
  const [open, setOpen] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  return (
    <DetailSection
      title="If you disagree with this review"
      description="Disagreeing is a normal part of the process. It does not delay your pay and it is not recorded as a complaint about your manager."
    >
      <div className="rounded-lg border border-info/30 bg-info-soft p-4">
        <p className="flex items-start gap-2 text-sm text-info">
          <MessageSquareWarning className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            {review.appeal.open
              ? `You can request reconsideration until ${review.appeal.deadline}. It goes to ${review.appeal.routeTo}.`
              : `The reconsideration window for this review closed on ${review.appeal.deadline}.`}
          </span>
        </p>
      </div>

      <ol className="mt-4 space-y-2 text-sm text-muted-foreground">
        {review.appeal.howItWorks.map((step, i) => (
          <li key={step} className="flex gap-2">
            <span className="tabular font-medium text-foreground">{i + 1}.</span>
            <span>{step}</span>
          </li>
        ))}
      </ol>

      {review.appeal.outcome ? (
        <p className="mt-4 rounded-md border bg-surface-muted px-3 py-2 text-sm">
          <span className="font-medium">Previous reconsideration</span> — raised {review.appeal.raisedOn}.{" "}
          {review.appeal.outcome}
        </p>
      ) : null}

      {review.appeal.open ? (
        <div className="mt-4">
          {submitted ? (
            <p className="flex items-start gap-2 rounded-md border border-success/30 bg-success-soft px-3 py-2 text-sm text-success">
              <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>
                Request for reconsideration submitted. HR Operations will acknowledge within 2 working days and
                will tell you who is looking at it. Nothing about your rating changes while it is open.
              </span>
            </p>
          ) : open ? (
            <div className="space-y-3">
              <div>
                <Label htmlFor="appeal-reason">
                  What do you disagree with, and why? Name the competency if it is a specific one.
                </Label>
                <Textarea
                  id="appeal-reason"
                  className="mt-2"
                  rows={4}
                  placeholder="For example: I disagree with the rating for Data and systems (CMMS), because the weekly schedule has been produced from the system since April."
                />
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" onClick={() => setSubmitted(true)}>
                  Submit the request for reconsideration
                </Button>
                <Button variant="ghost" onClick={() => setOpen(false)}>
                  Cancel
                </Button>
              </div>
            </div>
          ) : (
            <Button variant="outline" onClick={() => setOpen(true)}>
              Disagree — request reconsideration
            </Button>
          )}
        </div>
      ) : null}
    </DetailSection>
  );
}

function ReviewBody({ review }: { review: Review }) {
  const [acknowledged, setAcknowledged] = useState(Boolean(review.acknowledgedOn));
  const employee = employees.find((e) => e.id === review.employeeId);
  const canAcknowledge = review.stage === "Acknowledgement" && review.employeeId === ME && !acknowledged;
  const acknowledgedOn = review.acknowledgedOn ?? (acknowledged ? TODAY : undefined);

  return (
    <RecordDetail
      reference={review.id}
      title={`${review.cycle} review — ${name(review.employeeId)}`}
      subtitle={`${employee?.jobTitle ?? "Role not recorded"} · reviewer ${name(review.reviewerId)} · stage: ${review.stage}`}
      status={review.status}
      owner={acknowledged && review.stage === "Acknowledgement" ? "Closed — no owner" : review.owner}
      nextAction={
        acknowledged
          ? `No action — acknowledged on ${acknowledgedOn}`
          : `${review.nextAction} · due ${review.dueDate}`
      }
      dueDate={review.dueDate}
      primaryAction={
        canAcknowledge ? (
          <Button onClick={() => setAcknowledged(true)}>Acknowledge this review</Button>
        ) : undefined
      }
      secondaryActions={
        <>
          <Button variant="outline" size="sm">
            Add evidence
          </Button>
          <Button variant="outline" size="sm">
            Download a copy
          </Button>
        </>
      }
      summary={[
        { label: "Cycle", value: review.cycle },
        { label: "Stage", value: review.stage },
        { label: "Employee", value: name(review.employeeId) },
        { label: "Reviewer", value: name(review.reviewerId) },
        {
          label: "Overall rating — manager",
          value: ratingLabel(review.overallScore),
        },
        {
          label: "Overall rating — self",
          value: ratingLabel(review.selfOverallScore),
        },
        {
          label: "Acknowledgement",
          value: acknowledgedOn ? `Acknowledged on ${acknowledgedOn}` : `Due by ${review.dueDate}`,
        },
        {
          label: "Reconsideration window",
          value: review.appeal.open ? `Open until ${review.appeal.deadline}` : `Closed on ${review.appeal.deadline}`,
        },
      ]}
      timeline={
        review.timeline.length ? (
          <StatusTimeline title="History" events={review.timeline} />
        ) : (
          <p className="text-sm text-muted-foreground">Nothing has happened on this review yet.</p>
        )
      }
      related={
        <>
          <Link to="/hrm/talent/goals" className="block text-primary underline underline-offset-2">
            Goals for this cycle
          </Link>
          <Link to="/hrm/talent/learning" className="block text-primary underline underline-offset-2">
            Learning and certificates
          </Link>
          <Link
            to="/hrm/employees/$id"
            params={{ id: review.employeeId }}
            className="block text-primary underline underline-offset-2"
          >
            Employment record
          </Link>
        </>
      }
    >
      {acknowledged ? (
        <p className="flex items-start gap-2 rounded-lg border border-success/30 bg-success-soft px-4 py-3 text-sm text-success">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            Acknowledged on {acknowledgedOn}. Acknowledging records that you have read the review — it does not
            mean you agree with it, and the reconsideration route below stays open until{" "}
            {review.appeal.deadline}.
          </span>
        </p>
      ) : null}

      <DetailSection
        title="Written summary"
        description="The self-assessment and the manager's summary are kept separate — neither overwrites the other."
      >
        <div className="grid gap-4 lg:grid-cols-2">
          <article className="rounded-lg border bg-surface-muted p-4">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Self-assessment · {name(review.employeeId)}
            </h3>
            <p className="mt-2 text-sm">{review.selfSummary || "No self-assessment has been submitted yet."}</p>
          </article>
          <article className="rounded-lg border bg-surface-muted p-4">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Manager review · {name(review.reviewerId)}
            </h3>
            <p className="mt-2 text-sm">{review.managerSummary || "The manager review has not been written yet."}</p>
          </article>
        </div>
      </DetailSection>

      <DetailSection
        title="Competency ratings"
        description={`Self-assessment and manager rating side by side. The scale is 1 ${ratingScale[1]} · 2 ${ratingScale[2]} · 3 ${ratingScale[3]} · 4 ${ratingScale[4]} · 5 ${ratingScale[5]}. Every rating is shown as a number and a word.`}
      >
        {review.competencies.length ? (
          <ul className="space-y-4">
            {review.competencies.map((c) => (
              <CompetencyRow key={c.id} c={c} />
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">
            No competency ratings have been recorded on this review yet.
          </p>
        )}
      </DetailSection>

      <DetailSection
        title="Evidence"
        description="What the ratings were based on. Anything cited in the summary should appear here."
      >
        {review.evidence.length ? (
          <ul className="space-y-2">
            {review.evidence.map((e) => (
              <li key={e.id}>
                <a
                  href={e.href}
                  className="inline-flex items-center gap-2 text-sm font-medium text-primary underline underline-offset-2"
                >
                  <Paperclip className="size-3.5 shrink-0" aria-hidden />
                  {e.label}
                </a>
                <span className="ml-1 text-xs text-muted-foreground">
                  · {e.source}, added {e.addedOn}
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">No evidence has been attached to this review yet.</p>
        )}
      </DetailSection>

      <AppealPanel review={review} />
    </RecordDetail>
  );
}

function ReviewDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => talentApi.review(id), [id]);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {(review) => {
          if (!review) return <RestrictedState />;
          if (!review.visibleToMe) {
            return (
              <RestrictedState
                title="This review is not yours to open"
                body="Performance ratings are only visible to the employee, their reviewer and the calibration panel. Managers can see stage, owner and due date in the list, but not another employee's rating. Ask HR Operations if you believe you should have access."
              />
            );
          }
          return <ReviewBody review={review} />;
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
