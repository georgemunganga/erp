import { FormSelect } from "@/components/ui/form-select";
import { useEffect, useState } from "react";
import { hrmApi } from "@/platform/api-client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";

type Period = { id: string; periodLabel: string };
type Row = { workerId: string; employeeNo: string; workerName: string; amount: number | null; note: string };
type Batch = { payPeriodId: string; periodLabel: string; editable: boolean; lockReason: string | null; rows: Row[] };

export function PeriodOvertimeAmounts() {
  const [periods, setPeriods] = useState<Period[]>([]);
  const [periodId, setPeriodId] = useState("");
  const [batch, setBatch] = useState<Batch | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let current = true;
    void hrmApi.get<{ id: string; name: string }[]>("/hrm/payroll/pay-groups").then(async groups => {
      const lists = await Promise.all(groups.map(g => hrmApi.get<Period[]>(`/hrm/payroll/pay-groups/${g.id}/periods`).then(ps => ps.map(p => ({ ...p, periodLabel: `${p.periodLabel} — ${g.name}` })))));
      if (current) setPeriods(lists.flat());
    }).catch(e => { if (current) setError(e.message); });
    return () => { current = false; };
  }, []);
  useEffect(() => {
    let current = true;
    setBatch(null);
    if (!periodId) return;
    setError("");
    void hrmApi.get<Batch>(`/hrm/payroll/periods/${periodId}/overtime-amounts`).then(b => { if (current) setBatch(b); })
      .catch(e => { if (current) setError(e.message); });
    return () => { current = false; };
  }, [periodId]);
  return <Card data-testid="period-overtime-amounts">
    <CardHeader><CardTitle>Overtime amounts by payroll period</CardTitle></CardHeader>
    <CardContent className="space-y-4">
      <p className="text-sm text-muted-foreground">Enter each employee’s total overtime amount for the selected period. Saved amounts replace hours-based overtime for that employee in this period. Leave blank to use approved hours; enter 0 for no overtime payment. Amounts are included when payroll is calculated and go through payroll approval.</p>
      <label className="block space-y-2">Payroll period
        <FormSelect aria-label="Overtime payroll period" className="block w-full rounded-md border bg-background p-2" value={periodId} onChange={e => setPeriodId(e.target.value)}>
          <option value="">Select a payroll period</option>
          {periods.map(p => <option key={p.id} value={p.id}>{p.periodLabel}</option>)}
        </FormSelect>
      </label>
      {error && <p role="alert" className="text-danger">{error}</p>}
      {periodId && !batch && !error && <p>Loading overtime amounts…</p>}
      {batch && <div key={batch.payPeriodId}>
        <p className="mb-3">{batch.periodLabel} · {batch.rows.length} employees · {batch.editable ? "Open for entry" : "Read-only"}</p>
        {batch.lockReason && <p className="mb-3" role="status">{batch.lockReason}</p>}
        <div className="space-y-3">{batch.rows.map(row => <AmountRow key={row.workerId} row={row} batch={batch} />)}</div>
      </div>}
    </CardContent>
  </Card>;
}
function AmountRow({ row, batch }: { row: Row; batch: Batch }) {
  const [amount, setAmount] = useState(row.amount == null ? "" : String(row.amount));
  const [note, setNote] = useState(row.note);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  async function save(event: React.FormEvent) {
    event.preventDefault(); setBusy(true); setMessage("");
    try {
      await hrmApi.put(`/hrm/payroll/periods/${batch.payPeriodId}/overtime-amounts/${row.workerId}`, { amount: amount === "" ? null : Number(amount), note });
      setMessage("Saved for " + batch.periodLabel);
    } catch (e) { setMessage(e instanceof Error ? e.message : "Unable to save overtime."); }
    finally { setBusy(false); }
  }
  return <form onSubmit={save} className="grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_160px_1fr_auto]" data-worker={row.employeeNo}>
    <div><p className="font-medium">{row.workerName}</p><p className="text-xs text-muted-foreground">{row.employeeNo}</p></div>
    <label className="text-sm">Amount<Input aria-label={`Overtime amount for ${row.employeeNo}`} type="number" min="0" max="999999999" step="0.01" value={amount} disabled={!batch.editable || busy} onChange={e => { setAmount(e.target.value); setMessage(""); }} /></label>
    <label className="text-sm">Note<Input aria-label={`Overtime note for ${row.employeeNo}`} maxLength={500} value={note} disabled={!batch.editable || busy} onChange={e => { setNote(e.target.value); setMessage(""); }} /></label>
    <Button type="submit" className="self-end" disabled={!batch.editable || busy}>{busy ? "Saving…" : "Save"}</Button>
    {message && <p role="status" className="text-sm md:col-span-4">{message}</p>}
  </form>;
}
