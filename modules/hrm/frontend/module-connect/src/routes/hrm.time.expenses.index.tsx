import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, ArrowRight, Wallet } from "lucide-react";
import { Button } from "@/components/ui/button";
import { claimTotal, expensesApi, money } from "@/mock/expenses";
import type { Claim } from "@/mock/expenses";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/time/expenses/")({
  head: () => ({
    meta: [
      { title: "Expenses — Kuubu HRM" },
      { name: "description", content: "Expense claims and outstanding advances, reconciled so you always know who owes whom." },
      { property: "og:title", content: "Expenses — Kuubu HRM" },
      { property: "og:description", content: "Expense claims and outstanding advances, reconciled so you always know who owes whom." },
    ],
  }),
  component: ExpensesPage,
});

function ExpensesPage() {
  const claims = useMock(() => expensesApi.claims());
  const advances = useMock(() => expensesApi.advances());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Time and leave"
        title="Expenses"
        description="An advance is money you already have. A claim settles against it — so the result can be money owed to you, or money you owe back."
        primaryAction={
          <Button asChild>
            <Link to="/hrm/time/expenses/new">New expense claim</Link>
          </Button>
        }
      />

      <Async state={advances} rows={2}>
        {(rows) => {
          const outstanding = rows.filter((a) => a.status === "Paid" || a.status === "Partly retired");
          if (!outstanding.length) return null;
          return (
            <section aria-label="Outstanding advances" className="rounded-lg border border-warning/40 bg-warning-soft p-5">
              <h2 className="flex items-center gap-2 text-sm font-semibold text-warning">
                <Wallet className="size-4" aria-hidden />
                Outstanding advances
              </h2>
              <p className="mt-1 text-sm text-foreground">
                Money already paid to you that has not yet been accounted for with receipts. A new
                advance will not normally be approved while one is outstanding.
              </p>
              <ul className="mt-3 space-y-2">
                {outstanding.map((a) => (
                  <li key={a.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-md border bg-surface p-3 text-sm">
                    <span className="font-mono text-xs text-muted-foreground">{a.id}</span>
                    <span className="min-w-0 flex-1 truncate">{a.reason}</span>
                    <span className="tabular font-medium">{money(a.amount - a.retired, a.currency)} outstanding</span>
                    <span className="text-xs text-muted-foreground">retire by {a.dueDate}</span>
                  </li>
                ))}
              </ul>
            </section>
          );
        }}
      </Async>

      <Async state={claims} rows={3}>
        {(rows) => (
          <ListPage<Claim>
            rows={rows}
            searchPlaceholder="Search reference or purpose"
            searchFields={(c) => `${c.id} ${c.purpose} ${c.employee}`}
            filters={[
              { id: "status", label: "Status", options: ["Draft", "Submitted", "In review", "Approved", "Paid", "Returned"], match: (c, v) => c.status === v },
            ]}
            columns={[
              { id: "ref", header: "Reference", cell: (c) => <span className="font-mono text-xs">{c.id}</span> },
              { id: "purpose", header: "Purpose", cell: (c) => <span className="block max-w-72 truncate">{c.purpose}</span> },
              { id: "lines", header: "Lines", cell: (c) => <span className="tabular">{c.lines.length}</span> },
              { id: "total", header: "Claimed", cell: (c) => <span className="tabular">{money(claimTotal(c), c.currency)}</span> },
              {
                id: "settle",
                header: "Settles to",
                cell: (c) => {
                  const net = claimTotal(c) - (c.advanceAmount ?? 0);
                  if (!c.advanceAmount) return <span className="tabular text-xs">{money(claimTotal(c), c.currency)} to you</span>;
                  return net >= 0 ? (
                    <span className="tabular text-xs">{money(net, c.currency)} to you</span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 text-xs text-warning">
                      <ArrowRight className="size-3.5 shrink-0" aria-hidden />
                      {money(Math.abs(net), c.currency)} back from you
                    </span>
                  );
                },
              },
              { id: "status", header: "Status", cell: (c) => <StatusBadge status={c.status} /> },
              { id: "next", header: "Next action", cell: (c) => <span className="block max-w-56 truncate text-xs">{c.nextAction} · due {c.dueDate}</span> },
              { id: "advance", header: "Against advance", defaultVisible: false, cell: (c) => <span className="font-mono text-xs">{c.advanceId ?? "—"}</span> },
            ]}
            emptyBody="No expense claims yet."
          />
        )}
      </Async>

      <p className="flex gap-2 text-xs text-muted-foreground">
        <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
        Nothing in this build is actually reimbursed or recovered. Amounts shown are illustrative.
      </p>
    </AppShell>
      </AuthGate>
  );
}
