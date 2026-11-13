import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Copy, ReceiptText, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { claims, claimTotal, money } from "@/mock/expenses";
import type { ExpenseLine } from "@/mock/expenses";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/time/expenses/new")({
  head: () => ({
    meta: [
      { title: "New expense claim — Kuubu HRM" },
      { name: "description", content: "Add lines, see policy findings beside each one, and reconcile against any advance before submitting." },
      { property: "og:title", content: "New expense claim — Kuubu HRM" },
      { property: "og:description", content: "Add lines, see policy findings beside each one, and reconcile against any advance before submitting." },
    ],
  }),
  component: NewClaim,
});

/** The draft claim is prefilled from the in-flight Livingstone trip. */
const draft = claims[0];

function LineCard({ line, index }: { line: ExpenseLine; index: number }) {
  const shown = line.converted ? line.converted.amount : line.amount;
  return (
    <li className="rounded-lg border bg-surface p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <span className="min-w-0">
          <span className="block text-sm font-medium">
            {index + 1}. {line.category} — {line.merchant}
          </span>
          <span className="block text-xs text-muted-foreground">
            {line.date} · {line.purpose} · {line.costCentre}
          </span>
        </span>
        <span className="shrink-0 text-right">
          <span className="tabular block text-sm font-semibold">{money(shown, draft.currency)}</span>
          {line.converted ? (
            <span className="block text-[11px] text-muted-foreground">
              {money(line.amount, line.currency)} at {line.converted.rate} on {line.converted.rateDate}
            </span>
          ) : null}
        </span>
      </div>

      <p className="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">
        <ReceiptText className="size-3.5 shrink-0" aria-hidden />
        {line.receipt}
        {line.missingReason ? ` — ${line.missingReason}` : ""}
      </p>

      {line.possibleDuplicateOf ? (
        <p className="mt-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
          <Copy className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>
            Possible duplicate of line {draft.lines.findIndex((l) => l.id === line.possibleDuplicateOf) + 1}. Remove
            it if you entered the same receipt twice, or add a note explaining why there are two.
          </span>
        </p>
      ) : null}

      {line.warnings?.filter((w) => !line.possibleDuplicateOf || !w.startsWith("Same date")).map((w) => (
        <p key={w} className="mt-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
          <TriangleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>{w}</span>
        </p>
      ))}
    </li>
  );
}

function NewClaim() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [kind, setKind] = useState("advance");

  const total = claimTotal(draft);
  const advance = draft.advanceAmount ?? 0;
  const net = total - advance;

  const steps: FlowStep[] = [
    {
      id: "purpose",
      title: "What is this claim for?",
      purpose: "Retiring an advance works differently to a standalone claim — it has to balance.",
      render: () => (
        <div className="max-w-md">
          <Label htmlFor="kind">Claim type</Label>
          <Select value={kind} onValueChange={setKind}>
            <SelectTrigger id="kind" className="mt-1"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="advance">Retire an advance — ADV-2026-0044, K900.00</SelectItem>
              <SelectItem value="trip">Trip expenses with no advance</SelectItem>
              <SelectItem value="standalone">Standalone expense</SelectItem>
            </SelectContent>
          </Select>
          {kind === "advance" ? (
            <p className="mt-3 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
              You were paid K900.00 on 4 August for the Livingstone trip. This claim accounts for how it
              was spent. Anything unspent must be returned.
            </p>
          ) : null}
        </div>
      ),
    },
    {
      id: "lines",
      title: "Expense lines",
      purpose: "Policy findings appear beside the line they affect, not in a list at the bottom.",
      render: () => (
        <div className="space-y-3">
          <ul className="space-y-3">
            {draft.lines.map((l, i) => (
              <LineCard key={l.id} line={l} index={i} />
            ))}
          </ul>
          <Button
            variant="outline"
            size="sm"
            onClick={() =>
              feedback.note(
                "Add each expense as its own line.",
                "Each line is checked against the policy separately.",
              )
            }
          >
            Add another line
          </Button>
        </div>
      ),
    },
    {
      id: "evidence",
      title: "Evidence",
      purpose: "Receipts, or a declaration where one genuinely does not exist.",
      optional: true,
      render: () => (
        <div className="max-w-xl space-y-3">
          <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
            Drop receipts here (mock upload — nothing is stored).
          </div>
          <p className="rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
            One line has a missing-receipt declaration. Those lines are approved one level higher
            than usual, and repeated declarations are reviewed.
          </p>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "The arithmetic is shown in full so there is no surprise at settlement.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <dl className="divide-y rounded-lg border bg-surface">
            <div className="flex items-center justify-between px-4 py-2.5">
              <dt className="text-sm">Total claimed ({draft.lines.length} lines)</dt>
              <dd className="tabular text-sm font-medium">{money(total, draft.currency)}</dd>
            </div>
            <div className="flex items-center justify-between px-4 py-2.5">
              <dt className="text-sm">Less advance already paid to you</dt>
              <dd className="tabular text-sm font-medium">− {money(advance, draft.currency)}</dd>
            </div>
            <div className="flex items-center justify-between bg-surface-muted px-4 py-3">
              <dt className="text-sm font-semibold">
                {net >= 0 ? "Due to you" : "You need to return"}
              </dt>
              <dd className={`tabular text-base font-semibold ${net >= 0 ? "" : "text-warning"}`}>
                {money(Math.abs(net), draft.currency)}
              </dd>
            </div>
          </dl>

          {net < 0 ? (
            <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-warning">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>
                You spent less than the advance. {money(Math.abs(net), draft.currency)} must be
                returned — it can be deducted from your next payslip or repaid directly. This is not
                a penalty; it is simply money that was advanced and not used.
              </span>
            </p>
          ) : null}

          <p className="text-xs text-muted-foreground">
            Goes to Mutale Kabwe (Manager). The over-cap accommodation and the missing receipt each
            need a reason recorded at approval.
          </p>
        </div>
      ),
    },
  ];

  if (ref) {
    return (
      <AuthGate>
      <AppShell>
        <PageHeader eyebrow="Expenses" title="Claim submitted" />
        <NextSteps
          reference={`EXP-${ref}`}
          title="Claim submitted"
          steps={[
            "Mutale Kabwe reviews it, including the two lines that carry a policy finding.",
            net >= 0
              ? `If approved, ${money(Math.abs(net), draft.currency)} is reimbursed with your next salary payment.`
              : `Once approved, ${money(Math.abs(net), draft.currency)} is recovered from your next salary payment.`,
            "The advance is marked retired only when the claim is approved in full.",
          ]}
          actions={
            <>
              <Button onClick={() => navigate({ to: "/hrm/time/expenses" })}>View my claims</Button>
              <Button variant="outline" asChild>
                <Link to="/hrm/time/travel">Back to travel</Link>
              </Button>
            </>
          }
        />
      </AppShell>
      </AuthGate>
    );
  }

  return (
    <AppShell>
      <PageHeader
        eyebrow="Expenses"
        title="New expense claim"
        description="Four short steps. Your draft saves as you go."
        primaryAction={
          <Button variant="ghost" asChild>
            <Link to="/hrm/time/expenses">Cancel</Link>
          </Button>
        }
      />
      <GuidedFlow
        flowId="expense-new"
        steps={steps}
        submitLabel="Submit claim"
        onSubmit={async () => {
          const r = await api.submit("expense", { kind, total, net });
          setRef(r.id);
        }}
      />
    </AppShell>
  );
}
