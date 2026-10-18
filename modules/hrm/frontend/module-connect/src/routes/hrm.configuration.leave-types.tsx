import { FormToggle } from "@/components/ui/form-toggle";
import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, redirect } from '@tanstack/react-router'
import { useMemo, useState } from "react";
import { Archive, CalendarDays, Plus, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/leave-types")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/$slug", params: { slug: "leave-types" }, replace: true }); },
  head: () => ({ meta: [{ title: "Leave Types — Newworldcargo HRM" }, { name: "description", content: "Live leave entitlement and policy master data." }] }),
  component: LeaveTypesPage,
});

type LeaveType = Record<string, unknown>;
type FormState = { code: string; name: string; category: string; defaultDaysPerYear: string; maxConsecutiveDays: string; minNoticeDays: string; effectiveFrom: string; effectiveTo: string; requiresEvidence: boolean; allowsPartialDays: boolean; carryForwardDays: string; carryForwardExpiryMonths: string; allowNegative: boolean; autoAccrueMonthly: boolean; accrualStartDate: string };

const emptyForm = (): FormState => ({ code: "", name: "", category: "paid", defaultDaysPerYear: "24", maxConsecutiveDays: "999", minNoticeDays: "0", effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: "", requiresEvidence: false, allowsPartialDays: false, carryForwardDays: "0", carryForwardExpiryMonths: "0", allowNegative: false, autoAccrueMonthly: false, accrualStartDate: "" });
const asRows = (data: unknown): LeaveType[] => Array.isArray(data) ? data as LeaveType[] : ((data as { items?: unknown[] } | null)?.items ?? []) as LeaveType[];
const text = (row: LeaveType, key: string, fallback = "—") => row[key] == null || row[key] === "" ? fallback : String(row[key]);

export function LeaveTypesPage() {
  const state = useApi(() => realApi.leaveTypes({ includeInactive: true }), []);
  const rows = useMemo(() => asRows(state.data), [state.data]);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<LeaveType | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [busy, setBusy] = useState(false);

  const update = (key: keyof FormState, value: string | boolean) => setForm((current) => ({ ...current, [key]: value }));
  const openCreate = () => { setEditing(null); setForm(emptyForm()); setOpen(true); };
  const openEdit = (row: LeaveType) => {
    setEditing(row);
    setForm({
      code: text(row, "code", ""), name: text(row, "name", ""), category: text(row, "category", "paid"),
      defaultDaysPerYear: text(row, "defaultDaysPerYear", "24"), maxConsecutiveDays: text(row, "maxConsecutiveDays", "999"), minNoticeDays: text(row, "minNoticeDays", "0"),
      effectiveFrom: text(row, "effectiveFrom", new Date().toISOString().slice(0, 10)), effectiveTo: text(row, "effectiveTo", ""),
      requiresEvidence: Boolean(row.requiresEvidence), allowsPartialDays: Boolean(row.allowsPartialDays), carryForwardDays: text(row, "carryForwardDays", "0"), carryForwardExpiryMonths: text(row, "carryForwardExpiryMonths", "0"), allowNegative: Boolean(row.allowNegative),
      autoAccrueMonthly: Boolean(row.autoAccrueMonthly), accrualStartDate: text(row, "accrualStartDate", ""),
    });
    setOpen(true);
  };
  const save = async () => {
    if (!form.code.trim() || !form.name.trim() || !form.effectiveFrom) { feedback.blocked("Leave Type was not saved.", "Code, name, and effective-from date are required."); return; }
    if (form.autoAccrueMonthly && (!form.accrualStartDate || !form.accrualStartDate.endsWith("-01"))) { feedback.blocked("Leave Type was not saved.", "Monthly accrual needs a start date on the first day of a month."); return; }
    setBusy(true);
    const body = { code: form.code.trim(), name: form.name.trim(), category: form.category, defaultDaysPerYear: Number(form.defaultDaysPerYear) || 0, maxConsecutiveDays: Number(form.maxConsecutiveDays) || 0, minNoticeDays: Number(form.minNoticeDays) || 0, effectiveFrom: form.effectiveFrom, effectiveTo: form.effectiveTo || null, requiresEvidence: form.requiresEvidence, allowsPartialDays: form.allowsPartialDays, carryForwardDays: Number(form.carryForwardDays) || 0, carryForwardExpiryMonths: Number(form.carryForwardExpiryMonths) || 0, allowNegative: form.allowNegative, autoAccrueMonthly: form.autoAccrueMonthly, accrualStartDate: form.accrualStartDate || null, ...(editing ? { isActive: Boolean(editing.isActive) } : {}) };
    try { if (editing) await realApi.updateLeaveType(String(editing.id), body); else await realApi.createLeaveType(body); feedback.success(editing ? "Leave Type updated." : "Leave Type created."); setOpen(false); state.reload(); }
    catch (error) { feedback.blocked("Leave Type was not saved.", error instanceof Error ? error.message : "The HRM API rejected the change."); }
    finally { setBusy(false); }
  };
  const archive = async (row: LeaveType) => {
    setBusy(true);
    try { await realApi.updateLeaveType(String(row.id), { isActive: false }); feedback.success("Leave Type archived."); state.reload(); }
    catch (error) { feedback.blocked("Leave Type was not archived.", error instanceof Error ? error.message : "The HRM API rejected the change."); }
    finally { setBusy(false); }
  };
  const field = (key: keyof FormState, label: string, type = "text") => <div className="space-y-1"><Label htmlFor={`leave-${key}`}>{label}</Label><Input id={`leave-${key}`} type={type} value={String(form[key])} onChange={(e) => update(key, e.target.value)} /></div>;

  return <AppShell>
    <PageHeader eyebrow="Configuration · Leave" title="Leave Types" description="Maintain the live entitlement rules used by leave applications, balances, accruals, and payroll proration." meta={<ScopeBadge />} />
    <div className="mb-4 flex items-center justify-between gap-3"><div className="flex items-center gap-2 text-sm text-muted-foreground"><CalendarDays className="size-4" aria-hidden /> Effective-dated master data; archived types remain visible for history.</div><Button onClick={openCreate}><Plus className="mr-2 size-4" />Add leave type</Button></div>
    <Async state={state} rows={5}>{() => <div className="overflow-x-auto rounded-lg border bg-surface"><RecordTable className="w-full text-left text-sm"><thead className="border-b bg-surface-muted"><tr>{["Code", "Leave type", "Pay treatment", "Days/year", "Monthly accrual", "Notice", "Effective from", "Status", "Action"].map((h) => <th key={h} className="whitespace-nowrap px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{h}</th>)}</tr></thead><tbody className="divide-y">{rows.map((row) => <tr key={String(row.id)}><th className="px-3 py-2 font-mono text-xs">{text(row, "code")}</th><td className="px-3 py-2 font-medium">{text(row, "name")}</td><td className="px-3 py-2">{{paid:"Paid",unpaid:"Unpaid","half-pay":"Half-pay"}[text(row,"category") as "paid"|"unpaid"|"half-pay"] ?? text(row,"category")}</td><td className="px-3 py-2 tabular-nums">{text(row, "defaultDaysPerYear")}</td><td className="px-3 py-2">{Boolean(row.autoAccrueMonthly) ? `On · ${text(row, "accrualStartDate")}` : "Off"}</td><td className="px-3 py-2 tabular-nums">{text(row, "minNoticeDays", "0")} days</td><td className="px-3 py-2">{text(row, "effectiveFrom")}</td><td className="px-3 py-2">{Boolean(row.isActive) ? "Active" : "Archived"}</td><td className="px-3 py-2"><span className="flex gap-2"><Button size="sm" variant="outline" onClick={() => openEdit(row)}><Save className="mr-1 size-3.5" />Edit</Button>{Boolean(row.isActive) ? <Button size="sm" variant="outline" disabled={busy} onClick={() => archive(row)}><Archive className="mr-1 size-3.5" />Archive</Button> : null}</span></td></tr>)}{rows.length === 0 ? <tr><td colSpan={9} className="px-3 py-10 text-center text-muted-foreground">No leave types are configured yet.</td></tr> : null}</tbody></RecordTable></div>}</Async>
    <p className="mt-4 text-xs text-muted-foreground">Only leave types with monthly accrual enabled receive automatic credits. The annual entitlement is divided by 12 and posted from the configured start month. Payroll reads approved leave separately.</p>
    <Dialog open={open} onOpenChange={setOpen}><DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl"><DialogHeader><DialogTitle>{editing ? "Edit leave type" : "Add leave type"}</DialogTitle><DialogDescription>Pay treatment controls how approved leave affects salary in payroll.</DialogDescription></DialogHeader><div className="grid gap-4 sm:grid-cols-2">{field("code", "Code")}{field("name", "Name")}<div className="space-y-1"><Label htmlFor="leave-category">Pay treatment</Label><Select value={form.category} onValueChange={value => update("category",value)}><SelectTrigger id="leave-category"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="paid">Paid — full salary</SelectItem><SelectItem value="unpaid">Unpaid — no salary for leave days</SelectItem><SelectItem value="half-pay">Half-pay — 50% salary for leave days</SelectItem></SelectContent></Select><p className="text-xs text-muted-foreground">Applied to approved leave when payroll is calculated.</p></div>{field("defaultDaysPerYear", "Default days per year", "number")}{field("maxConsecutiveDays", "Maximum consecutive days", "number")}{field("minNoticeDays", "Minimum notice days", "number")}{field("effectiveFrom", "Effective from", "date")}{field("effectiveTo", "Effective to (optional)", "date")}{field("carryForwardDays", "Carry-forward days", "number")}{field("carryForwardExpiryMonths", "Carry-forward expiry months", "number")}</div><div className="grid gap-3 rounded-md border p-3 sm:grid-cols-3"><label className="flex items-center gap-2 text-sm"><FormToggle type="checkbox" checked={form.requiresEvidence} onChange={(e) => update("requiresEvidence", e.target.checked)} />Evidence required</label><label className="flex items-center gap-2 text-sm"><FormToggle type="checkbox" checked={form.allowsPartialDays} onChange={(e) => update("allowsPartialDays", e.target.checked)} />Partial days allowed</label><label className="flex items-center gap-2 text-sm"><FormToggle type="checkbox" checked={form.allowNegative} onChange={(e) => update("allowNegative", e.target.checked)} />Allow negative balance</label></div><div className="space-y-3 rounded-md border p-3"><label className="flex items-center gap-2 text-sm"><FormToggle type="checkbox" checked={form.autoAccrueMonthly} onChange={(e) => update("autoAccrueMonthly", e.target.checked)} />Accrue automatically each month</label>{form.autoAccrueMonthly ? <div className="max-w-xs">{field("accrualStartDate", "Start month (first day)", "date")}</div> : null}<p className="text-xs text-muted-foreground">For 24 days per year, eligible employees receive 2 days each month from this date. Other leave types receive no automatic credit unless enabled separately.</p></div><DialogFooter><Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button><Button disabled={busy} onClick={save}>{busy ? "Saving…" : "Save leave type"}</Button></DialogFooter></DialogContent></Dialog>
  </AppShell>;
}
