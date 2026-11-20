import { createFileRoute, Link, Outlet, useChildMatches } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { employees } from "@/mock/data";
import type { Review } from "@/mock/talent";
import { ME, ratingLabel, talentApi } from "@/mock/talent";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/talent/reviews")({
  head: () => ({
    meta: [
      { title: "Performance reviews — Kuubu HRM" },
      {
        name: "description",
        content:
          "Review cycles with stage, owner, next action and due date. Ratings stay on the record itself and are never printed in a list.",
      },
      { property: "og:title", content: "Performance reviews — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Review cycles with stage, owner, next action and due date. Ratings stay on the record itself and are never printed in a list.",
      },
    ],
  }),
  component: ReviewsPage,
});

const name = (id: string) => employees.find((e) => e.id === id)?.fullName ?? "Unknown employee";

/**
 * Ratings are sensitive. A list view prints a rating only when it is the signed-in
 * employee's own; everyone else's is summarised, and the value stays on the record.
 */
function ratingCell(r: Review) {
  if (r.employeeId === ME) {
    return r.overallScore === null ? (
      <span className="text-xs text-muted-foreground">Not yet rated</span>
    ) : (
      <span className="text-xs">{ratingLabel(r.overallScore)}</span>
    );
  }
  if (r.stage === "Self-assessment" || r.stage === "Manager review") {
    return <span className="text-xs text-muted-foreground">No rating proposed yet</span>;
  }
  return <span className="text-xs text-muted-foreground">Recorded — open the record to view</span>;
}

function ReviewsPage() {
  const state = useMock(() => talentApi.reviews());
  const [view, setView] = useState("mine");
  // `/talent/reviews/$id` is generated as a child of this route, so hand the
  // screen over to the record whenever one is open.
  const childMatches = useChildMatches();
  if (childMatches.length > 0) return <Outlet />;

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Talent · Performance"
        title="Performance reviews"
        description="Every cycle shows where it has reached, who holds it, what happens next and when it is due. Ratings are not printed in list views — open a record you are authorised to see."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/talent/reviews/$id" params={{ id: "rv-2041" }}>
              Open my mid-year review
            </Link>
          </Button>
        }
      />
      <Async state={state}>
        {(all) => {
          const rows = all.filter((r) =>
            view === "mine"
              ? r.employeeId === ME
              : view === "team"
                ? r.scope === "team"
                : view === "open"
                  ? r.stage !== "Closed"
                  : true,
          );

          return (
            <ListPage<Review>
              rows={rows}
              savedViews={[
                { id: "mine", label: "My reviews" },
                { id: "team", label: "My team's reviews" },
                { id: "open", label: "Still open" },
                { id: "all", label: "All reviews" },
              ]}
              activeView={view}
              onViewChange={setView}
              searchPlaceholder="Search reference, cycle or employee"
              searchFields={(r) => `${r.id} ${r.cycle} ${name(r.employeeId)} ${r.stage}`}
              emptyBody="No review records match this view. Try 'All reviews', or clear the filters."
              filters={[
                {
                  id: "stage",
                  label: "Stage",
                  options: [
                    "Self-assessment",
                    "Manager review",
                    "Calibration",
                    "Acknowledgement",
                    "Closed",
                  ],
                  match: (r, v) => r.stage === v,
                },
                {
                  id: "cycle",
                  label: "Cycle",
                  options: ["Mid-year 2026", "Year-end 2025"],
                  match: (r, v) => r.cycle === v,
                },
              ]}
              bulkActions={[{ label: "Send a reminder", onSelect: () => undefined }]}
              columns={[
                {
                  id: "ref",
                  header: "Reference",
                  cell: (r) => (
                    <Link
                      to="/hrm/talent/reviews/$id"
                      params={{ id: r.id }}
                      className="font-mono text-xs text-primary underline underline-offset-2"
                    >
                      {r.id}
                    </Link>
                  ),
                },
                { id: "cycle", header: "Cycle", cell: (r) => <span className="text-sm">{r.cycle}</span> },
                {
                  id: "employee",
                  header: "Employee",
                  cell: (r) => <span className="block max-w-52 truncate">{name(r.employeeId)}</span>,
                },
                {
                  id: "stage",
                  header: "Stage",
                  cell: (r) => (
                    <span className="inline-flex rounded-full border bg-surface-muted px-2 py-0.5 text-xs">
                      {r.stage}
                    </span>
                  ),
                },
                { id: "status", header: "Status", cell: (r) => <StatusBadge status={r.status} /> },
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
                {
                  id: "reviewer",
                  header: "Reviewer",
                  defaultVisible: false,
                  cell: (r) => name(r.reviewerId),
                },
                {
                  id: "rating",
                  header: "Overall rating",
                  defaultVisible: false,
                  cell: ratingCell,
                },
              ]}
            />
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
