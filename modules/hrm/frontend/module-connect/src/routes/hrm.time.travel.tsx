import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link } from "@tanstack/react-router";
import { Info, MapPin, ShieldAlert, Wallet } from "lucide-react";
import { Button } from "@/components/ui/button";
import { expensesApi, money } from "@/mock/expenses";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/time/travel")({
  head: () => ({
    meta: [
      { title: "Travel — Newworldcargo HRM" },
      { name: "description", content: "Travel authorisation, per diem rates, advances and their retirement." },
      { property: "og:title", content: "Travel — Newworldcargo HRM" },
      { property: "og:description", content: "Travel authorisation, per diem rates, advances and their retirement." },
    ],
  }),
  component: TravelPage,
});

function TravelPage() {
  const trips = useMock(() => expensesApi.trips());
  const advances = useMock(() => expensesApi.advances());
  const rates = useMock(() => expensesApi.perDiem());

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Time and leave"
        title="Travel"
        description="Authorisation before you go, an advance if you need one, and a clear obligation to account for it afterwards."
        primaryAction={<Button
            onClick={() =>
              feedback.submitted(
                "Travel authorisation requested.",
                "Travel is approved before it is booked, because an unapproved trip cannot be claimed back.",
              )
            }
          >
            Request travel authorisation
          </Button>}
      />

      <section aria-label="Trips">
        <h2 className="text-sm font-semibold">Travel authorisations</h2>
        <Async state={trips} rows={3}>
          {(rows) => (
            <ul className="mt-3 space-y-4">
              {rows.map((t) => (
                <li key={t.id} className="rounded-lg border bg-surface p-5">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <span className="min-w-0">
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-xs text-muted-foreground">{t.id}</span>
                        <StatusBadge status={t.status} />
                        {t.riskLevel === "Elevated" ? (
                          <span className="inline-flex items-center gap-1.5 rounded-full border border-warning/40 bg-warning-soft px-2 py-0.5 text-[11px] font-medium text-warning">
                            <ShieldAlert className="size-3 shrink-0" aria-hidden />
                            Elevated risk
                          </span>
                        ) : null}
                      </span>
                      <span className="mt-1 block text-sm font-medium">{t.purpose}</span>
                      <span className="mt-0.5 flex items-center gap-1.5 text-xs text-muted-foreground">
                        <MapPin className="size-3.5 shrink-0" aria-hidden />
                        {t.destination} · duty station {t.dutyStation} · {t.from} to {t.to}
                      </span>
                    </span>
                    <span className="tabular shrink-0 text-sm font-semibold">
                      {money(t.estimatedCost, t.currency)}
                    </span>
                  </div>

                  <dl className="mt-3 grid gap-x-6 gap-y-2 text-xs sm:grid-cols-2">
                    <div>
                      <dt className="text-muted-foreground">Transport</dt>
                      <dd>{t.transport}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Accommodation</dt>
                      <dd>{t.accommodation}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Cover while away</dt>
                      <dd>{t.coverArrangement}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Visa and documents</dt>
                      <dd>{t.visaRequired ? "Visa required — check validity" : (t.visaNote ?? "No visa required")}</dd>
                    </div>
                  </dl>

                  {t.riskNote ? (
                    <p className="mt-3 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
                      <ShieldAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                      <span>{t.riskNote}</span>
                    </p>
                  ) : null}

                  <p className="mt-3 text-xs text-muted-foreground">
                    Next: {t.nextAction} · {t.owner} · due {t.dueDate}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </Async>
      </section>

      <section aria-label="Advances">
        <h2 className="text-sm font-semibold">Advances and retirement</h2>
        <p className="mt-1 text-xs text-muted-foreground">
          An advance is not an allowance. It must be accounted for with receipts, and anything
          unspent is returned.
        </p>
        <Async state={advances} rows={2}>
          {(rows) => (
            <ul className="mt-3 space-y-3">
              {rows.map((a) => {
                const outstanding = a.amount - a.retired;
                return (
                  <li key={a.id} className="rounded-lg border bg-surface p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <span className="min-w-0">
                        <span className="flex items-center gap-2">
                          <Wallet className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
                          <span className="font-mono text-xs text-muted-foreground">{a.id}</span>
                          <StatusBadge status={a.status} />
                        </span>
                        <span className="mt-1 block text-sm">{a.reason}</span>
                      </span>
                      <span className="shrink-0 text-right">
                        <span className="tabular block text-sm font-semibold">{money(a.amount, a.currency)}</span>
                        <span className="block text-[11px] text-muted-foreground">
                          {a.retired > 0 ? `${money(a.retired, a.currency)} accounted for` : "Nothing accounted for yet"}
                        </span>
                      </span>
                    </div>

                    {outstanding > 0 ? (
                      <div className="mt-3 flex flex-wrap items-center gap-3">
                        <span className="tabular text-sm font-medium text-warning">
                          {money(outstanding, a.currency)} still to account for
                        </span>
                        <Button variant="outline" size="sm" asChild>
                          <Link to="/hrm/time/expenses/new">Retire this advance</Link>
                        </Button>
                      </div>
                    ) : null}

                    <p className="mt-2 text-xs text-muted-foreground">
                      Next: {a.nextAction} · due {a.dueDate}
                    </p>
                  </li>
                );
              })}
            </ul>
          )}
        </Async>
      </section>

      <section aria-label="Per diem rates" className="rounded-lg border bg-surface p-5">
        <h2 className="text-sm font-semibold">Per diem and accommodation caps</h2>
        <p className="mt-1 flex gap-2 text-xs text-muted-foreground">
          <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          These are configuration, not fixed product behaviour. Each organisation sets its own rates
          and effective dates in Configuration.
        </p>
        <Async state={rates} rows={2}>
          {(rows) => (
            <div className="mt-3 overflow-x-auto">
              <RecordTable className="w-full min-w-[36rem] text-left text-sm">
                <caption className="sr-only">Per diem and accommodation caps by destination</caption>
                <thead className="border-b bg-surface-muted">
                  <tr>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Destination</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Meals</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Incidentals</th>
                    <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wide text-muted-foreground">Accommodation cap</th>
                    <th scope="col" className="px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Effective from</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {rows.map((r) => (
                    <tr key={r.destination}>
                      <th scope="row" className="px-3 py-2 font-normal">{r.destination}</th>
                      <td className="tabular px-3 py-2 text-right">{money(r.meals, r.currency)}</td>
                      <td className="tabular px-3 py-2 text-right">{money(r.incidentals, r.currency)}</td>
                      <td className="tabular px-3 py-2 text-right">{money(r.accommodationCap, r.currency)}</td>
                      <td className="px-3 py-2 text-xs text-muted-foreground">{r.effectiveFrom}</td>
                    </tr>
                  ))}
                </tbody>
              </RecordTable>
            </div>
          )}
        </Async>
      </section>
    </AppShell>
      </AuthGate>
  );
}
