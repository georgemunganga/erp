import { createFileRoute, Link } from "@tanstack/react-router";
import { Info } from "lucide-react";
import { derivePayslips, type DerivedPayslip } from "@/mock/payrollrun";
import type { MockState } from "@/platform/use-mock";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { useApi, realApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/**
 * M25: the employee sees only their own payslips, keyed on the OIDC subject.
 * Backend: GET /hrm/me/payslips. The admin list (`/hrm/payslips/{workerId}`)
 * stays on the mock shape for now — this page is the self-service surface.
 */
function adaptPayslip(raw: unknown): DerivedPayslip | null {
  const p = raw as Record<string, unknown>;
  const text = (v: unknown) => (v === undefined || v === null ? "" : String(v));
  const id = text(p.id);
  if (!id) return null;
  const num = (v: unknown) => (typeof v === "number" ? v : Number(v) || 0);
  const status = text(p.status).toLowerCase();
  // The backend DTO is the authoritative record — the detail page
  // (`/hrm/payslips/$id`) re-fetches the same slip, so this row only needs the
  // fields the list columns and the click-through URL consume.
  return {
    id,
    runId: text(p.runId),
    employeeId: text(p.employeeNo),
    employee: text(p.workerName || p.employeeNo || p.payslipNo || id),
    period: text(p.periodLabel || p.payslipNo || "Released payslip"),
    entityName: text(p.payslipNo || id),
    currency: text(p.currency || "ZMW"),
    payDate: text(p.payDate || (text(p.releasedAt) ? text(p.releasedAt).slice(0, 10) : "")),
    gross: num(p.grossPay),
    deductions: num(p.totalDeductions),
    net: num(p.netPay),
    employerCost: 0,
    components: [],
    paid: status === "paid" || status === "closed" || status === "final",
  } satisfies DerivedPayslip;
}

export const Route = createFileRoute("/hrm/payslips/")({
  head: () => ({
    meta: [
      { title: "Payslips — Kuubu HRM" },
      { name: "description", content: "Your pay history, each with a full explanation of how it was calculated." },
      { property: "og:title", content: "Payslips — Kuubu HRM" },
      { property: "og:description", content: "Your pay history, each with a full explanation of how it was calculated." },
    ],
  }),
  component: PayslipsList,
});

const money = (v: number, c: string) => new Intl.NumberFormat(undefined, { style: "currency", currency: c }).format(v);

function PayslipsList() {
  // M25: real backend first — the signed-in worker's own payslips. Falls back
  // to the mock envelope when the real backend is off (`VITE_USE_REAL_API`).
  const realState = useApi<DerivedPayslip[]>(
    async () =>
      ((await realApi.myPayslips()) as { items: unknown[] }).items
        .map(adaptPayslip)
        .filter((p: DerivedPayslip | null): p is DerivedPayslip => p !== null),
    [],
  );
  const mockState = useMock<DerivedPayslip[]>(() => Promise.resolve(derivePayslips()));
  // M25: Async expects the mock-shaped envelope; both states share
  // `{ data: T | null, loading, degraded, error, reload }` so the cast is safe.
  const state = USE_REAL
    ? (realState as unknown as MockState<DerivedPayslip[]>)
    : (mockState as unknown as MockState<DerivedPayslip[]>);

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Pay"
        title="Payslips"
        description="Your own payslips — released payslips stay retrievable exactly as issued, and a correction creates a new linked version, never a silent overwrite."
      />

      <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-xs text-info">
        <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
        A payslip exists only once its pay run has released payslips. It is the same record the run
        holds, so the two can never show different figures.
      </p>

      <Async state={state}>
        {(rows) => (
          <ListPage<DerivedPayslip>
            rows={rows as unknown as DerivedPayslip[]}
            searchPlaceholder="Search period, reference or pay date"
            searchFields={(p) => `${p.id} ${p.employee} ${p.period} ${p.payDate}`}
            columns={[
              {
                id: "ref",
                header: "Reference",
                cell: (p) => (
                  <Link
                    to="/hrm/payslips/$id"
                    params={{ id: p.id }}
                    className="font-mono text-xs text-primary underline underline-offset-2"
                  >
                    {p.id}
                  </Link>
                ),
              },
              { id: "period", header: "Period", cell: (p) => p.period || "—" },
              { id: "gross", header: "Gross", cell: (p) => <span className="tabular">{money(p.gross, p.currency)}</span> },
              { id: "net", header: "Net", cell: (p) => <span className="tabular font-medium">{money(p.net, p.currency)}</span> },
              {
                id: "paid",
                header: "Payment",
                cell: (p) =>
                  p.paid ? (
                    <span className="text-xs text-success">Paid</span>
                  ) : (
                    <span className="text-xs text-warning">Not yet paid</span>
                  ),
              },
            ]}
            emptyBody="No payslips match the current view."
          />
        )}
      </Async>
      </AppShell>
    </AuthGate>
  );
}
