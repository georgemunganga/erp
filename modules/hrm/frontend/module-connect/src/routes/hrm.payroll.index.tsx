import { createFileRoute, Link } from "@tanstack/react-router";
import { CheckCircle2, Clock } from "lucide-react";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/payroll/")({
  head: () => ({
    meta: [
      { title: "Payroll — Kuubu HRM" },
      { name: "description", content: "Payroll administration: readiness, runs, exceptions, payments and statutory output." },
      { property: "og:title", content: "Payroll — Kuubu HRM" },
      { property: "og:description", content: "Payroll administration: readiness, runs, exceptions, payments and statutory output." },
    ],
  }),
  component: PayrollHome,
});

const areas: { label: string; status: "available" | "next"; detail: string; to?: string }[] = [
  { label: "Setup", status: "available", detail: "Pay groups, salary components, tax and contribution rules — shared with Configuration." },
  { label: "Inputs", status: "next", detail: "Time, attendance, leave and one-time earnings/deductions feeding this period's run." },
  { label: "Salary advances", status: "available", to: "/hrm/payroll/salary-advances", detail: "Record advances and recover them through payslip deductions." },
  { label: "Runs", status: "available", to: "/hrm/payroll/runs", detail: "Create, calculate, validate and approve a payroll run with maker-checker sign-off." },
  { label: "Exceptions", status: "available", to: "/hrm/payroll/exceptions", detail: "Missing data, negative net pay, variances and duplicate detection before release." },
  { label: "Payments", status: "next", detail: "Bank files, mobile money and third-party remittances, released as a controlled stage." },
  { label: "Statutory", status: "next", detail: "Tax, contribution and year-end filings with authoritative source and effective dates." },
];

function PayrollHome() {
  const config = useApi(
    async () => {
      const [components, payGroups, rules] = await Promise.all([
        realApi.payrollComponents(),
        realApi.payrollPayGroups(),
        realApi.payrollContributionRules(),
      ]);
      const groupIds = payGroups as unknown as { id?: string }[];
      const periods = groupIds.length && groupIds[0]?.id ? await realApi.payrollPayGroupPeriods(groupIds[0].id) : [];
      const allPeriods = periods as unknown as { periodLabel?: string; cutoffDate?: string; status?: string }[];
      const current = allPeriods.find((p) => p.status === "open") ?? allPeriods[allPeriods.length - 1] ?? null;
      return { components, payGroups, rules, current };
    },
    [],
  );

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Payroll"
        title="Payroll"
        description="This is the administrative workspace — running payroll, not just viewing a payslip. Restricted to Payroll and HR admin roles."
        meta={<ScopeBadge />}
      />

      <Async state={config}>
        {(c) => (
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="rounded-lg border bg-surface p-4">
              <p className="text-xs text-muted-foreground">Current period</p>
              <p className="mt-1 text-lg font-semibold">{c.current?.periodLabel ? `${c.current.periodLabel} (${c.current.status ?? "closed"})` : "No open period"}</p>
              <p className="mt-1 text-xs text-muted-foreground">Cutoff: {c.current?.cutoffDate ?? "see period details"}</p>
            </div>
            <div className="rounded-lg border bg-surface p-4">
              <p className="text-xs text-muted-foreground">Pay setup</p>
              <p className="mt-1 text-lg font-semibold">{(c.components as unknown[]).length} components · {(c.rules as unknown[]).length} rules</p>
              <p className="mt-1 text-xs text-muted-foreground">{(c.payGroups as unknown[]).length} pay group{(c.payGroups as unknown[]).length === 1 ? "" : "s"}</p>
            </div>
            <div className="rounded-lg border bg-surface p-4">
              <p className="text-xs text-muted-foreground">Readiness</p>
              <p className="mt-1 text-lg font-semibold text-warning">Check before running</p>
              <p className="mt-1 text-xs text-muted-foreground">Missing data, variances and exceptions surface on the run itself.</p>
            </div>
          </div>
        )}
      </Async>

      <div>
        <h2 className="mt-6 text-sm font-semibold">Payroll areas</h2>
        <p className="mt-1 text-xs text-muted-foreground">
          "Setup" is shared with Configuration, not duplicated. The rest — running an actual pay
          period end to end — is now available under Runs and Exceptions.
        </p>
        <ul className="mt-3 grid gap-3 sm:grid-cols-2">
          {areas.map((a) => (
            <li key={a.label} className="flex items-start gap-3 rounded-lg border bg-surface p-4">
              {a.status === "available" ? (
                <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
              ) : (
                <Clock className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
              )}
              <span className="min-w-0">
                {a.to ? (
                  <Link to={a.to} className="block text-sm font-medium text-primary underline-offset-2 hover:underline">
                    {a.label}
                  </Link>
                ) : (
                  <span className="block text-sm font-medium">{a.label}</span>
                )}
                <span className="block text-xs text-muted-foreground">{a.detail}</span>
              </span>
            </li>
          ))}
        </ul>
      </div>
    </AppShell>
      </AuthGate>
  );
}
