import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { Position } from "@/mock/structure";
import { entityName, formatMoney, positionIncumbentName, structureApi } from "@/mock/structure";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/people/positions/$id")({
  head: () => ({
    meta: [
      { title: "Position — Newworldcargo HRM" },
      {
        name: "description",
        content:
          "Position record: requirements, pay band, incumbency history, mandatory licences and establishment funding.",
      },
      { property: "og:title", content: "Position — Newworldcargo HRM" },
      {
        property: "og:description",
        content:
          "Position record: requirements, pay band, incumbency history, mandatory licences and establishment funding.",
      },
    ],
  }),
  component: PositionPage,
});

function nextActionFor(p: Position) {
  if (p.licence && p.licence.status === "Expired") {
    return `The mandatory ${p.licence.name} has expired. Record a fitness-to-work decision before the incumbent returns to the position.`;
  }
  if (p.licence && p.licence.status === "Expiring soon" && p.licence.holderExpiry) {
    return `Book refresher training and complete a fitness-to-work review before ${p.licence.holderExpiry}, when the mandatory ${p.licence.name} expires.`;
  }
  if (p.licence && p.licence.status === "No holder") {
    return "Appoint a certified holder, or extend the external cover arrangement before it ends.";
  }
  if (p.status === "Vacant" && p.critical) {
    return "Escalate this critical vacancy at the next establishment review and confirm the interim cover arrangement.";
  }
  if (p.status === "Vacant") {
    return p.funded && p.withinEstablishment
      ? "Progress the requisition with recruitment."
      : "Secure funding and establishment approval before advertising.";
  }
  if (p.status === "Frozen") {
    return "No recruitment permitted. The freeze is reconsidered at the next establishment review.";
  }
  if (p.status === "Closed") {
    return "No action required. The position is closed and retained for audit history only.";
  }
  return "No action required. Confirm the position at the next establishment review.";
}

function dueDateFor(p: Position) {
  if (p.licence?.holderExpiry) return p.licence.holderExpiry;
  return undefined;
}

function YesNo({ value, yes, no }: { value: boolean; yes: string; no: string }) {
  return value ? <span>{yes}</span> : <span className="text-warning">{no}</span>;
}

function PositionPage() {
  const { id } = Route.useParams();
  const state = useMock(() => structureApi.position(id), [id]);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {(p) =>
          !p ? (
            <RestrictedState />
          ) : (
            <RecordDetail
              reference={p.positionNo}
              title={p.jobTitle}
              subtitle={`${p.department} · ${p.team} · ${p.branch}, ${entityName(p.entityId)}`}
              status={p.status}
              owner={p.establishment.approvedBy}
              nextAction={nextActionFor(p)}
              dueDate={dueDateFor(p)}
              primaryAction={<Button
                  onClick={() =>
                    feedback.submitted(
                      "Position change started.",
                      "Changing the grade or cost centre affects pay, so it goes for approval.",
                    )
                  }
                >
                  Start a position change
                </Button>}
              secondaryActions={
                <>
                  {p.incumbentId ? (
                    <Button variant="outline" size="sm" asChild>
                      <Link to="/hrm/employees/$id" params={{ id: p.incumbentId }}>
                        Open incumbent profile
                      </Link>
                    </Button>
                  ) : null}
                  <Button variant="outline" size="sm" asChild>
                    <Link to="/hrm/people/org">View in organisation structure</Link>
                  </Button>
                  <Button variant="outline" size="sm" asChild>
                    <Link to="/hrm/people/positions">Back to positions</Link>
                  </Button>
                </>
              }
              summary={[
                { label: "Position number", value: p.positionNo },
                { label: "Job family", value: p.jobFamily },
                { label: "Grade", value: p.grade },
                { label: "Legal entity", value: entityName(p.entityId) },
                { label: "Branch", value: p.branch },
                { label: "Department", value: `${p.department} — ${p.team}` },
                { label: "FTE", value: p.fte.toFixed(1) },
                {
                  label: "Incumbent",
                  value: p.incumbentId ? (
                    <Link
                      to="/hrm/employees/$id"
                      params={{ id: p.incumbentId }}
                      className="text-primary underline underline-offset-2"
                    >
                      {positionIncumbentName(p)}
                    </Link>
                  ) : (
                    <span className="text-muted-foreground">No incumbent</span>
                  ),
                },
                {
                  label: "Incumbent since",
                  value: p.incumbentFrom ?? (
                    <span className="text-muted-foreground">Not applicable</span>
                  ),
                },
                { label: "Reports to", value: p.reportsTo },
                {
                  label: "Critical position",
                  value: p.critical ? (
                    <span className="inline-flex items-center gap-1 text-warning">
                      <AlertTriangle aria-hidden className="size-3.5" />
                      Yes — cover must be maintained
                    </span>
                  ) : (
                    "No"
                  ),
                },
                {
                  label: "Within establishment",
                  value: (
                    <YesNo value={p.withinEstablishment} yes="Yes" no="No — off establishment" />
                  ),
                },
                {
                  label: "Funded",
                  value: <YesNo value={p.funded} yes="Yes" no="No — funding withdrawn" />,
                },
                { label: "Effective from", value: p.effectiveFrom },
                {
                  label: "Mandatory licence",
                  value: p.licence ? `${p.licence.name} (${p.licence.status})` : "None required",
                },
              ]}
              timeline={<StatusTimeline title="Incumbency history" events={p.incumbency} />}
              related={
                <>
                  <Link
                    to="/hrm/people/positions"
                    className="block text-primary underline underline-offset-2"
                  >
                    All positions
                  </Link>
                  <Link
                    to="/hrm/people/org"
                    className="block text-primary underline underline-offset-2"
                  >
                    Organisation structure
                  </Link>
                  <Link
                    to="/hrm/people/positions"
                    className="block text-primary underline underline-offset-2"
                  >
                    All positions and grades
                  </Link>
                  {p.incumbentId ? (
                    <Link
                      to="/hrm/employees/$id"
                      params={{ id: p.incumbentId }}
                      className="block text-primary underline underline-offset-2"
                    >
                      Incumbent employment record
                    </Link>
                  ) : null}
                </>
              }
            >
              {p.vacancyReason ? (
                <DetailSection
                  title={
                    p.status === "Closed" ? "Why this position was closed" : "Vacancy and cover"
                  }
                  description="Recorded so the gap between the plan and the people is explainable, not just visible."
                >
                  <p className="text-sm text-foreground">{p.vacancyReason}</p>
                </DetailSection>
              ) : null}

              {p.incumbentNote ? (
                <DetailSection
                  title="Incumbency note"
                  description="Anything about this incumbency that changes what happens next."
                >
                  <p className="text-sm text-foreground">{p.incumbentNote}</p>
                </DetailSection>
              ) : null}

              <DetailSection
                title="Required qualifications and competencies"
                description="What a person must hold and demonstrate to occupy this position. Assessed at appointment and at every position change."
              >
                <div className="grid gap-6 lg:grid-cols-2">
                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      Qualifications
                    </h3>
                    <ul className="mt-2 list-disc space-y-1.5 pl-5 text-sm">
                      {p.qualifications.map((q) => (
                        <li key={q}>{q}</li>
                      ))}
                    </ul>
                  </div>
                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      Competencies
                    </h3>
                    <dl className="mt-2 space-y-3">
                      {p.competencies.map((c) => (
                        <div key={c.name}>
                          <dt className="text-sm font-medium">
                            {c.name}
                            <span className="ml-2 rounded-full border bg-surface-muted px-2 py-0.5 text-[11px] font-normal text-muted-foreground">
                              {c.level}
                            </span>
                          </dt>
                          <dd className="mt-0.5 text-sm text-muted-foreground">{c.note}</dd>
                        </div>
                      ))}
                    </dl>
                  </div>
                </div>
              </DetailSection>

              <DetailSection
                title={`Pay band for grade ${p.grade}`}
                description="The band belongs to the grade, not to the person. An offer outside the band needs a documented exception."
              >
                <dl className="grid gap-4 sm:grid-cols-3">
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Band minimum
                    </dt>
                    <dd className="mt-1 text-sm font-medium">
                      {formatMoney(p.salaryBand.min, p.salaryBand.currency)}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Mid-point
                    </dt>
                    <dd className="mt-1 text-sm font-medium">
                      {formatMoney(p.salaryBand.mid, p.salaryBand.currency)}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Band maximum
                    </dt>
                    <dd className="mt-1 text-sm font-medium">
                      {formatMoney(p.salaryBand.max, p.salaryBand.currency)}
                    </dd>
                  </div>
                </dl>
                <p className="mt-3 text-xs text-muted-foreground">
                  {p.salaryBand.source} · last reviewed {p.salaryBand.reviewedOn} · annual,
                  full-time equivalent in {p.salaryBand.currency}.
                </p>
              </DetailSection>

              {p.licence ? (
                <DetailSection
                  title="Mandatory licence and fitness to work"
                  description="The position cannot lawfully be occupied without this certification. Expiry changes the employee's fitness to work even though nothing about the position has changed."
                >
                  <div
                    className={
                      p.licence.status === "Valid"
                        ? "rounded-md border bg-surface-muted p-4"
                        : "rounded-md border border-warning/40 bg-warning-soft p-4"
                    }
                  >
                    <p className="flex flex-wrap items-center gap-2 text-sm font-medium">
                      {p.licence.status === "Valid" ? null : (
                        <AlertTriangle aria-hidden className="size-4 text-warning" />
                      )}
                      <span>{p.licence.name}</span>
                      <span className="rounded-full border bg-surface px-2 py-0.5 text-[11px] font-normal">
                        {p.licence.status}
                      </span>
                    </p>
                    <dl className="mt-3 grid gap-4 sm:grid-cols-3">
                      <div>
                        <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Issuing authority
                        </dt>
                        <dd className="mt-1 text-sm">{p.licence.authority}</dd>
                      </div>
                      <div>
                        <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Renewal cycle
                        </dt>
                        <dd className="mt-1 text-sm">Every {p.licence.renewalMonths} months</dd>
                      </div>
                      <div>
                        <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          Holder certificate expires
                        </dt>
                        <dd className="mt-1 text-sm">
                          {p.licence.holderExpiry ?? (
                            <span className="text-muted-foreground">No certified holder</span>
                          )}
                        </dd>
                      </div>
                    </dl>
                    <p className="mt-3 text-sm">{p.licence.fitnessImpact}</p>
                  </div>
                </DetailSection>
              ) : null}

              <DetailSection
                title="Establishment and funding"
                description="Budgeted against actual for the current plan year. A position can be on the establishment without being funded, and can cost money without being on it."
              >
                <div className="overflow-x-auto rounded-lg border">
                  <RecordTable className="w-full min-w-[34rem] text-left text-sm">
                    <caption className="sr-only">
                      Budgeted against actual establishment figures for {p.positionNo}
                    </caption>
                    <thead className="border-b bg-surface-muted">
                      <tr>
                        <th
                          scope="col"
                          className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                        >
                          Measure
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                        >
                          Budgeted
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                        >
                          Actual
                        </th>
                        <th
                          scope="col"
                          className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
                        >
                          Variance
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      <tr>
                        <th scope="row" className="px-3 py-2 font-normal">
                          Full-time equivalent
                        </th>
                        <td className="px-3 py-2">{p.establishment.budgetedFte.toFixed(1)}</td>
                        <td className="px-3 py-2">{p.establishment.actualFte.toFixed(1)}</td>
                        <td className="px-3 py-2">
                          {(p.establishment.actualFte - p.establishment.budgetedFte).toFixed(1)}
                        </td>
                      </tr>
                      <tr>
                        <th scope="row" className="px-3 py-2 font-normal">
                          Annual cost
                        </th>
                        <td className="px-3 py-2">
                          {formatMoney(
                            p.establishment.budgetedAnnualCost,
                            p.establishment.currency,
                          )}
                        </td>
                        <td className="px-3 py-2">
                          {formatMoney(p.establishment.actualAnnualCost, p.establishment.currency)}
                        </td>
                        <td className="px-3 py-2">
                          {formatMoney(
                            p.establishment.actualAnnualCost - p.establishment.budgetedAnnualCost,
                            p.establishment.currency,
                          )}
                        </td>
                      </tr>
                    </tbody>
                  </RecordTable>
                </div>
                <dl className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Establishment reference
                    </dt>
                    <dd className="mt-1 text-sm">{p.establishment.reference}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Cost centre
                    </dt>
                    <dd className="mt-1 text-sm">{p.establishment.costCentre}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Funding source
                    </dt>
                    <dd className="mt-1 text-sm">{p.establishment.fundingSource}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Approved
                    </dt>
                    <dd className="mt-1 text-sm">
                      {p.establishment.approvedBy} · {p.establishment.approvedOn}
                    </dd>
                  </div>
                </dl>
                <p className="mt-3 text-sm text-muted-foreground">{p.establishment.note}</p>
              </DetailSection>
            </RecordDetail>
          )
        }
      </Async>
    </AppShell>
      </AuthGate>
  );
}
