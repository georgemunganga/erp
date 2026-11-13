import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Download, Info, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { payrollRunApi } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import {
  CalculationExplainer,
  type CalculationLine,
} from "@/platform/components/CalculationExplainer";
import { PageHeader } from "@/platform/components/PageHeader";
import { RestrictedState } from "@/platform/components/States";
import { useMock } from "@/platform/use-mock";
import type { MockState } from "@/platform/use-mock";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

export const Route = createFileRoute("/hrm/payslips/$id")({
  head: () => ({
    meta: [
      { title: "Payslip — Kuubu HRM" },
      {
        name: "description",
        content:
          "Every line explained: the calculation, the rule version and the difference from last period.",
      },
      { property: "og:title", content: "Payslip — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Every line explained: the calculation, the rule version and the difference from last period.",
      },
    ],
  }),
  component: PayslipDetail,
});

const money = (v: number, c: string) =>
  new Intl.NumberFormat(undefined, { style: "currency", currency: c }).format(v);

/** M24: statutory references snapshotted on the payslip at release time. */
function StatutoryRefs({ slip }: { slip: Record<string, unknown> }) {
  const rows: Array<[string, string]> = [
    ["NRC", String(slip.workerNrc ?? "")],
    ["TPIN", String(slip.workerTpin ?? "")],
    ["NAPSA no.", String(slip.workerNapsaNumber ?? "")],
    ["NHIMA no.", String(slip.workerNhimaNumber ?? "")],
  ];
  const any = rows.some(([, v]) => v.length > 0);
  if (!any) return null;
  return (
    <div className="rounded-lg border bg-surface p-5">
      <h2 className="text-sm font-semibold">Statutory references</h2>
      <p className="mt-1 text-xs text-muted-foreground">
        Taken from the worker record at release time, so the payslip always matches what was filed
        with ZRA, NAPSA and NHIMA for this payment — even if the record changes later.
      </p>
      <dl className="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
        {rows.map(([label, value]) => (
          <div
            key={label}
            className="flex items-baseline justify-between gap-3 border-b border-dashed pb-1"
          >
            <dt className="text-xs text-muted-foreground">{label}</dt>
            <dd className="tabular font-medium">
              {value || <span className="text-muted-foreground">—</span>}
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

function PayslipDetail() {
  const { id } = Route.useParams();
  const mockState = useMock(() => payrollRunApi.payslip(id), [id]);
  const realState = useApi(async () => {
    if (!USE_REAL) return null;
    const raw = (await realApi.myPayslipById(id)) as Record<string, unknown> | null;
    if (!raw) return null;
    // M43: the backend component snapshot is sparse (code, name, type, amount,
    // explanation). CalculationExplainer requires the full CalculationLine
    // shape, so fill the gaps with sensible defaults instead of throwing on
    // `undefined.map`.
    const components = Array.isArray(raw.components)
      ? (raw.components as Record<string, unknown>[]).map((component) => ({
          ...component,
          code: String(component.componentCode ?? component.componentName ?? ""),
          label: String(component.componentName ?? component.componentCode ?? ""),
          amount: Number(component.amount ?? 0),
          explanation: String(component.explanation ?? ""),
          inputs: Array.isArray(component.inputs)
            ? (component.inputs as { label: string; value: string }[])
            : [
                { label: "Amount", value: new Intl.NumberFormat(undefined, { style: "currency", currency: String(raw.currency ?? "ZMW") }).format(Number(component.amount ?? 0)) },
                { label: "Rule", value: String(component.explanation ?? "Fixed amount") },
              ],
          ruleVersion: String(component.ruleVersion ?? "current"),
          effectiveFrom: String(component.effectiveFrom ?? ""),
          priorAmount: component.priorAmount === undefined ? undefined : Number(component.priorAmount),
          name: component.componentName,
          kind:
            component.componentType === "earning"
              ? "Earning"
              : component.componentType === "employer-contribution"
                ? "Employer"
                : "Deduction",
        }))
      : [];
    return {
      ...raw,
      employee: raw.workerName ?? raw.employeeNo ?? raw.payslipNo,
      employeeId: raw.employeeNo,
      period: raw.periodLabel ?? raw.payslipNo,
      entityName: raw.payslipNo,
      payDate: raw.payDate ?? (typeof raw.releasedAt === "string" ? raw.releasedAt.slice(0, 10) : ""),
      gross: raw.grossPay,
      deductions: raw.totalDeductions,
      net: raw.netPay,
      paid: ["paid", "closed", "final"].includes(String(raw.status ?? "").toLowerCase()),
      components,
    };
  }, [id]);
  const [generating, setGenerating] = useState(false);

  // M24: the Async component is typed against MockState, so the real-API state
  // is narrowed to the same envelope (`data | null`, `loading`, `reload`). Both
  // branches carry data — the mock payslip or the raw backend record.
  const state = USE_REAL
    ? (realState as unknown as MockState<Record<string, unknown>>)
    : (mockState as unknown as MockState<Record<string, unknown>>);

  return (
    <AuthGate>
      <AppShell>
        <Async state={state} rows={4}>
          {(slip) => {
            if (!slip) return <RestrictedState />;
            type Kinded = { kind?: string } & Partial<CalculationLine>;
            const components = (slip.components ?? []) as Kinded[];
            const asLines = (xs: Kinded[]): CalculationLine[] => xs as unknown as CalculationLine[];
            const earnings = components.filter((c) => c.kind === "Earning");
            const deductions = components.filter((c) => c.kind === "Deduction");
            const employer = components.filter((c) => c.kind === "Employer");

            return (
              <div className="space-y-6">
                <PageHeader
                  eyebrow="Pay"
                  title={`Payslip — ${String(slip.period ?? "")}`}
                  description={[
                    String(slip.employee ?? ""),
                    String(slip.entityName ?? ""),
                  ].filter(Boolean).join(" · ")}
                  primaryAction={
                    <Button
                      variant="outline"
                      className="gap-2"
                      disabled={generating}
                      onClick={async () => {
                        if (USE_REAL) {
                          setGenerating(true);
                          try {
                            const blob = await realApi.myPayslipDownloadBlob(id);
                            const blobUrl = URL.createObjectURL(blob);
                            const tab = window.open(blobUrl, "_blank", "noopener,noreferrer");
                            if (!tab) URL.revokeObjectURL(blobUrl);
                            setTimeout(() => URL.revokeObjectURL(blobUrl), 120_000);
                            feedback.submitted(
                              "Payslip download ready.",
                              "The generated copy opened in a new browser tab.",
                            );
                          } catch (e) {
                            feedback.blocked(
                              "PDF generation failed",
                              e instanceof Error ? e.message : "Unknown error.",
                            );
                          } finally {
                            setGenerating(false);
                          }
                          return;
                        }
                        feedback.note(
                          "Payslip PDF is not generated in this build.",
                          "The released payslip is the record; the PDF is only a copy of it.",
                        );
                      }}
                    >
                      <Download className="size-4" aria-hidden />
                      {USE_REAL
                        ? generating
                          ? "Preparing…"
                          : "Download PDF"
                        : "Download PDF (mock)"}
                    </Button>
                  }
                  meta={
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
                      <ShieldCheck className="size-3.5" aria-hidden />
                      Released and unchanged since issue
                    </span>
                  }
                />

                <div className="grid gap-4 sm:grid-cols-3">
                  <div className="rounded-lg border bg-surface p-4">
                    <p className="text-xs text-muted-foreground">Gross</p>
                    <p className="tabular mt-1 text-xl font-semibold">
                      {money(Number(slip.gross ?? 0), String(slip.currency ?? "ZMW"))}
                    </p>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <p className="text-xs text-muted-foreground">Net pay</p>
                    <p className="tabular mt-1 text-xl font-semibold text-primary">
                      {money(Number(slip.net ?? 0), String(slip.currency ?? "ZMW"))}
                    </p>
                  </div>
                  <div className="rounded-lg border bg-surface p-4">
                    <p className="text-xs text-muted-foreground">Payment status</p>
                    {String(slip.paid ?? "").toLowerCase() === "true" ? (
                      <>
                        <p className="mt-1 text-sm font-medium text-success">
                          Paid to the bank account on file
                        </p>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          On {String(slip.payDate ?? "")}.
                        </p>
                      </>
                    ) : (
                      <>
                        <p className="mt-1 text-sm font-medium text-warning">
                          Released, not yet paid
                        </p>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          Expected {String(slip.payDate ?? "")}. Seeing a payslip does not mean the
                          money has moved.
                        </p>
                      </>
                    )}
                  </div>
                </div>

                {/* The whole point of deriving a payslip: it cannot disagree with its run. */}
                <p className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
                  <Info className="size-3.5 shrink-0" aria-hidden />
                  <span>
                    These are the figures held by pay run{" "}
                    <Link
                      to="/hrm/payroll/runs/$id"
                      params={{ id: String(slip.runId ?? "") }}
                      className="font-mono underline underline-offset-2"
                    >
                      {String(slip.runId ?? "")}
                    </Link>
                    , not a separate copy of them.
                  </span>
                </p>

                <div>
                  <h2 className="text-sm font-semibold">Earnings</h2>
                  <div className="mt-2">
                    <CalculationExplainer
                      lines={asLines(earnings)}
                      currency={String(slip.currency ?? "ZMW")}
                      caption="Every earning line shows the inputs used and the rule version applied."
                    />
                  </div>
                </div>

                <div>
                  <h2 className="text-sm font-semibold">Deductions</h2>
                  <div className="mt-2">
                    <CalculationExplainer
                      lines={asLines(deductions)}
                      currency={String(slip.currency ?? "ZMW")}
                    />
                  </div>
                </div>

                {employer.length ? (
                  <div>
                    <h2 className="text-sm font-semibold">Employer contributions</h2>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Shown for transparency — paid by the employer on top of pay, never deducted
                      from your net.
                    </p>
                    <div className="mt-2">
                      <CalculationExplainer
                        lines={asLines(employer)}
                        currency={String(slip.currency ?? "ZMW")}
                      />
                    </div>
                  </div>
                ) : null}

                <StatutoryRefs slip={slip} />

                <div className="rounded-lg border bg-surface p-5">
                  <h2 className="text-sm font-semibold">How this adds up</h2>
                  <dl className="mt-3 max-w-md space-y-1.5 text-sm">
                    <div className="flex justify-between gap-4">
                      <dt>Total earnings</dt>
                      <dd className="tabular">
                        {money(Number(slip.gross ?? 0), String(slip.currency ?? "ZMW"))}
                      </dd>
                    </div>
                    <div className="flex justify-between gap-4">
                      <dt>Total deductions</dt>
                      <dd className="tabular text-muted-foreground">
                        −{money(Number(slip.deductions ?? 0), String(slip.currency ?? "ZMW"))}
                      </dd>
                    </div>
                    <div className="flex justify-between gap-4 border-t pt-1.5 font-medium">
                      <dt>Net pay</dt>
                      <dd className="tabular">
                        {money(Number(slip.net ?? 0), String(slip.currency ?? "ZMW"))}
                      </dd>
                    </div>
                    <div className="flex justify-between gap-4 pt-1.5 text-xs text-muted-foreground">
                      <dt>Employer cost on top</dt>
                      <dd className="tabular">
                        {money(Number(slip.employerCost ?? 0), String(slip.currency ?? "ZMW"))}
                      </dd>
                    </div>
                  </dl>
                </div>

                <div className="rounded-lg border bg-surface p-5">
                  <h2 className="text-sm font-semibold">Something doesn't look right?</h2>
                  <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
                    Raise a payroll query and this exact payslip reference is attached
                    automatically. If a correction is approved, a new linked version is created here
                    — this original stays retrievable and unchanged.
                  </p>
                  <Button asChild variant="outline" className="mt-3">
                    <Link to="/hrm/requests/new">Raise a payroll query</Link>
                  </Button>
                </div>
              </div>
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
