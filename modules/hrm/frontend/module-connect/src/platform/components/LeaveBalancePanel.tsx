import { RecordTable } from "./RecordTable";
import { AlertTriangle, Info } from "lucide-react";
import type { LeaveBalance } from "@/mock/leavebalance";

/**
 * A leave balance with its working shown.
 *
 * The rule this encodes: a balance is an argument, not an assertion. Anyone
 * looking at it should be able to see how it was reached and disagree with a
 * specific line, rather than with a single unexplained number.
 */
export function LeaveBalancePanel({
  balance: b,
  currency = "ZMW",
}: {
  balance: LeaveBalance;
  currency?: string;
}) {
  if (b.annualEntitlement === null) {
    return (
      <div className="space-y-2">
        <p className="text-sm font-medium">{b.policy}</p>
        {b.notes.map((n) => (
          <p key={n} className="flex gap-2 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
            {n}
          </p>
        ))}
      </div>
    );
  }

  const rows: { label: string; value: number; muted?: boolean }[] = [
    { label: "Brought forward from 2025", value: b.broughtForward },
    { label: `Accrued over ${b.monthsAccrued} months`, value: b.accrued },
    { label: "Already taken", value: -b.taken, muted: true },
    { label: "Approved and still to come", value: -b.booked, muted: true },
  ];

  const money = (v: number) =>
    new Intl.NumberFormat(undefined, { style: "currency", currency }).format(v);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-x-6 gap-y-2">
        <div>
          <p className="text-xs uppercase tracking-wide text-muted-foreground">Available now</p>
          <p className="tabular text-2xl font-semibold">{b.available} days</p>
        </div>
        {b.requested > 0 ? (
          <div>
            <p className="text-xs uppercase tracking-wide text-muted-foreground">
              If everything outstanding is approved
            </p>
            <p className={`tabular text-lg font-medium ${b.projected < 0 ? "text-danger" : ""}`}>
              {b.projected} days
            </p>
          </div>
        ) : null}
        <p className="text-xs text-muted-foreground">
          {b.policy} · as at {b.asAt}
        </p>
      </div>

      <div className="overflow-x-auto">
        <RecordTable className="w-full min-w-[22rem] text-left text-sm">
          <caption className="sr-only">How the leave balance was calculated</caption>
          <tbody className="divide-y">
            {rows.map((r) => (
              <tr key={r.label}>
                <th scope="row" className="py-1.5 pr-4 font-normal">
                  {r.label}
                </th>
                <td
                  className={`tabular py-1.5 text-right ${r.muted ? "text-muted-foreground" : ""}`}
                >
                  {r.value > 0 ? "+" : r.value < 0 ? "−" : ""}
                  {Math.abs(r.value)}
                </td>
              </tr>
            ))}
            <tr className="border-t-2">
              <th scope="row" className="py-1.5 pr-4 font-medium">
                Available
              </th>
              <td className="tabular py-1.5 text-right font-medium">{b.available}</td>
            </tr>
            {b.requested > 0 ? (
              <tr>
                <th scope="row" className="py-1.5 pr-4 font-normal text-muted-foreground">
                  Requested, not yet decided
                </th>
                <td className="tabular py-1.5 text-right text-muted-foreground">−{b.requested}</td>
              </tr>
            ) : null}
          </tbody>
        </RecordTable>
      </div>

      {b.entries.length ? (
        <details className="rounded-md border bg-surface-muted p-3">
          <summary className="cursor-pointer text-xs font-medium">
            Every movement ({b.entries.length})
          </summary>
          <ul className="mt-2 space-y-1.5">
            {b.entries.map((en) => (
              <li key={en.id} className="flex flex-wrap items-baseline justify-between gap-2 text-xs">
                <span className="min-w-0">
                  <span className="font-medium">{en.kind}</span>
                  <span className="text-muted-foreground"> — {en.detail}</span>
                </span>
                <span className="tabular shrink-0">
                  {en.days > 0 ? "+" : "−"}
                  {Math.abs(en.days)} days
                </span>
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      {b.encashment ? (
        <div className="rounded-md border border-info/30 bg-info-soft p-3">
          <p className="text-xs font-medium text-info">Paid out on leaving</p>
          <dl className="mt-1.5 space-y-1 text-xs">
            <div className="flex justify-between gap-4">
              <dt>Days at {b.encashment.lastWorkingDay}</dt>
              <dd className="tabular font-medium">{b.encashment.days}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt>Daily rate</dt>
              <dd className="tabular">{money(b.encashment.dailyRate)}</dd>
            </div>
            <div className="flex justify-between gap-4 border-t border-info/20 pt-1 font-medium">
              <dt>Estimated encashment</dt>
              <dd className="tabular">{money(b.encashment.value)}</dd>
            </div>
          </dl>
          <p className="mt-1.5 text-xs text-info">
            {b.encashment.basis}. This is an estimate — the figure that is paid comes from{" "}
            {b.encashment.paidIn}, calculated on actual pay rather than the grade midpoint.
          </p>
        </div>
      ) : null}

      {b.notes.map((n) => (
        <p
          key={n}
          className={`flex gap-2 text-xs ${
            n.startsWith("Approving everything") ? "text-warning" : "text-muted-foreground"
          }`}
        >
          {n.startsWith("Approving everything") ? (
            <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          ) : (
            <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          )}
          {n}
        </p>
      ))}
    </div>
  );
}
