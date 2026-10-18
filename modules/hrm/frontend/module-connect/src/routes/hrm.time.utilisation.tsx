import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute } from "@tanstack/react-router";
import { ArrowRight, Check, Clock } from "lucide-react";
import { extrasApi, money } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/time/utilisation")({
  head: () => ({
    meta: [
      { title: "Utilisation and hand-off — Newworldcargo HRM" },
      { name: "description", content: "Where time went, and what gets passed to payroll and accounting." },
      { property: "og:title", content: "Utilisation and hand-off — Newworldcargo HRM" },
      { property: "og:description", content: "Where time went, and what gets passed to payroll and accounting." },
    ],
  }),
  component: UtilisationPage,
});

function UtilisationPage() {
  const util = useMock(() => extrasApi.utilisation());
  const handoff = useMock(() => extrasApi.handoff());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Time and leave"
        title="Utilisation and hand-off"
        description="Where time actually went last month, and what flows on from it."
      />

      <section aria-label="Utilisation">
        <h2 className="text-sm font-semibold">Utilisation — July 2026</h2>
        <Async state={util} rows={3}>
          {(rows) => (
            <div className="mt-3 overflow-x-auto rounded-lg border bg-surface">
              <RecordTable className="w-full min-w-[40rem] text-left text-sm">
                <caption className="sr-only">Hours by employee split into billable, non-billable and absence</caption>
                <thead className="border-b bg-surface-muted">
                  <tr>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Employee</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Capacity</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Billable</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Non-billable</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Absence</th>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Split</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {rows.map((r) => {
                    const pct = r.capacity ? Math.round((r.billable / r.capacity) * 100) : 0;
                    return (
                      <tr key={r.employee}>
                        <th scope="row" className="px-3 py-2 font-normal">
                          <span className="block max-w-56 truncate">{r.employee}</span>
                        </th>
                        <td className="tabular px-3 py-2 text-right">{r.capacity}h</td>
                        <td className="tabular px-3 py-2 text-right font-medium">{r.billable}h</td>
                        <td className="tabular px-3 py-2 text-right">{r.nonBillable}h</td>
                        <td className="tabular px-3 py-2 text-right">{r.absence}h</td>
                        <td className="px-3 py-2">
                          <span className="block h-1.5 w-20 overflow-hidden rounded-full bg-muted" role="presentation">
                            <span className="block h-full rounded-full bg-primary" style={{ width: `${pct}%` }} />
                          </span>
                          <span className="mt-0.5 block text-[11px] text-muted-foreground">{pct}% billable</span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </RecordTable>
            </div>
          )}
        </Async>
        <p className="mt-2 text-xs text-muted-foreground">
          Low billable time is not automatically a problem — planning, cover and training are real
          work. Read it alongside the role, not on its own.
        </p>
      </section>

      <section aria-label="Hand-off">
        <h2 className="text-sm font-semibold">Passed to payroll and accounting</h2>
        <Async state={handoff} rows={3}>
          {(rows) => (
            <ul className="mt-3 space-y-2">
              {rows.map((h) => (
                <li key={h.id} className="flex flex-wrap items-center gap-3 rounded-lg border bg-surface p-3">
                  <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">
                    <ArrowRight className="size-3 shrink-0" aria-hidden />
                    {h.destination}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm">{h.what}</span>
                    <span className="block text-xs text-muted-foreground">
                      {h.period}
                      {h.note ? ` · ${h.note}` : ""}
                    </span>
                  </span>
                  <span className="tabular shrink-0 text-sm font-medium">
                    {h.hours !== undefined ? `${h.hours}h` : h.amount !== undefined && h.currency ? money(h.amount, h.currency) : "—"}
                  </span>
                  <span className="shrink-0">
                    {h.state === "Sent" ? (
                      <span className="inline-flex items-center gap-1.5 text-xs text-success">
                        <Check className="size-3.5 shrink-0" aria-hidden />
                        Sent
                      </span>
                    ) : h.state === "Held" ? (
                      <span className="inline-flex items-center gap-1.5 text-xs text-warning">
                        <Clock className="size-3.5 shrink-0" aria-hidden />
                        Held
                      </span>
                    ) : (
                      <span className="text-xs text-muted-foreground">Ready</span>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Async>
      </section>
    </AppShell>
      </AuthGate>
  );
}
