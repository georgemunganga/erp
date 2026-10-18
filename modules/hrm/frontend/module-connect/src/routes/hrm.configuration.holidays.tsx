import { FormToggle } from "@/components/ui/form-toggle";
import { FormSelect } from "@/components/ui/form-select";
import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, redirect } from '@tanstack/react-router'
import { useMemo, useState } from "react";
import { CalendarDays, Pencil, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/holidays")({
  beforeLoad: () => { throw redirect({ to: "/hrm/workspace/settings/$slug", params: { slug: "holidays" }, replace: true }); },
  head: () => ({ meta: [{ title: "Holiday List — Newworldcargo HRM" }, { name: "description", content: "Live public holiday calendars." }] }),
  component: HolidaysPage,
});

type Holiday = { id: string; calendarId: string; calendarName: string; name: string; holidayDate: string; observedOn?: string | null; isRecurring: boolean; description?: string | null };
type Form = { calendarId: string; name: string; holidayDate: string; observedOn: string; isRecurring: boolean; description: string };
const emptyForm = (): Form => ({ calendarId: "", name: "", holidayDate: "", observedOn: "", isRecurring: false, description: "" });
const list = (value: unknown): Record<string, unknown>[] => Array.isArray(value) ? value as Record<string, unknown>[] : ((value as { items?: unknown[] } | null)?.items ?? []) as Record<string, unknown>[];

export function HolidaysPage() {
  const state = useApi(async () => {
    const calendars = list(await realApi.calendars());
    const details = await Promise.all(calendars.map(async (calendar) => ({ calendar, detail: await realApi.calendar(String(calendar.id)) })));
    return details.flatMap(({ calendar, detail }) => list(detail && (detail as Record<string, unknown>).holidays).map((row) => ({ ...row, calendarId: String(calendar.id), calendarName: String(calendar.name ?? "Calendar") }))) as Holiday[];
  }, []);
  const rows = useMemo(() => (state.data ?? []) as Holiday[], [state.data]);
  const calendars = useApi(realApi.calendars, []);
  const calendarRows = useMemo(() => list(calendars.data), [calendars.data]);
  const [open, setOpen] = useState(false); const [editing, setEditing] = useState<Holiday | null>(null); const [form, setForm] = useState<Form>(emptyForm()); const [busy, setBusy] = useState(false);
  const update = (key: keyof Form, value: string | boolean) => setForm((current) => ({ ...current, [key]: value }));
  const openCreate = () => { setEditing(null); setForm({ ...emptyForm(), calendarId: String(calendarRows[0]?.id ?? "") }); setOpen(true); };
  const openEdit = (row: Holiday) => { setEditing(row); setForm({ calendarId: row.calendarId, name: row.name, holidayDate: row.holidayDate, observedOn: row.observedOn ?? "", isRecurring: row.isRecurring, description: row.description ?? "" }); setOpen(true); };
  const reload = () => { state.reload(); calendars.reload(); };
  const save = async () => {
    if (!form.calendarId || !form.name.trim() || !form.holidayDate) { feedback.blocked("Holiday was not saved.", "Calendar, name, and holiday date are required."); return; }
    setBusy(true); const body = { calendarId: form.calendarId, name: form.name.trim(), holidayDate: form.holidayDate, observedOn: form.observedOn || null, isRecurring: form.isRecurring, description: form.description || null };
    try { if (editing) await realApi.updateHoliday(editing.id, body); else await realApi.createHoliday(body); feedback.success(editing ? "Holiday updated." : "Holiday created."); setOpen(false); reload(); }
    catch (error) { feedback.blocked("Holiday was not saved.", error instanceof Error ? error.message : "The HRM API rejected the change."); }
    finally { setBusy(false); }
  };
  const remove = async (row: Holiday) => { setBusy(true); try { await realApi.deleteHoliday(row.id); feedback.success("Holiday removed."); reload(); } catch (error) { feedback.blocked("Holiday was not removed.", error instanceof Error ? error.message : "The HRM API rejected the change."); } finally { setBusy(false); } };
  return <AppShell><PageHeader eyebrow="Configuration · Working time" title="Holiday List" description="Maintain the public holidays used by calendars, attendance, leave validation, and payroll working-day calculations." meta={<ScopeBadge />} /><div className="mb-4 flex items-center justify-between gap-3"><div className="flex items-center gap-2 text-sm text-muted-foreground"><CalendarDays className="size-4" aria-hidden /> Calendar-specific and recurring dates are stored in PostgreSQL.</div><Button onClick={openCreate}><Plus className="mr-2 size-4" />Add holiday</Button></div><Async state={state} rows={6}>{() => <div className="overflow-x-auto rounded-lg border bg-surface"><RecordTable className="w-full text-left text-sm"><thead className="border-b bg-surface-muted"><tr>{["Holiday", "Date", "Observed", "Calendar", "Recurring", "Action"].map((h) => <th key={h} className="whitespace-nowrap px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{h}</th>)}</tr></thead><tbody className="divide-y">{rows.map((row) => <tr key={row.id}><th className="px-3 py-2 font-medium">{row.name}</th><td className="px-3 py-2">{row.holidayDate}</td><td className="px-3 py-2">{row.observedOn ?? "Same day"}</td><td className="px-3 py-2">{row.calendarName}</td><td className="px-3 py-2">{row.isRecurring ? "Yes" : "No"}</td><td className="px-3 py-2"><span className="flex gap-2"><Button size="sm" variant="outline" onClick={() => openEdit(row)}><Pencil className="mr-1 size-3.5" />Edit</Button><Button size="sm" variant="outline" disabled={busy} onClick={() => remove(row)}><Trash2 className="mr-1 size-3.5" />Remove</Button></span></td></tr>)}{rows.length === 0 ? <tr><td colSpan={6} className="px-3 py-10 text-center text-muted-foreground">No public holidays are configured.</td></tr> : null}</tbody></RecordTable></div>}</Async><p className="mt-4 text-xs text-muted-foreground">Removing a holiday does not rewrite historical attendance or payroll records; it changes future calendar evaluation only.</p><Dialog open={open} onOpenChange={setOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "Edit holiday" : "Add holiday"}</DialogTitle><DialogDescription>This updates the live calendar used by attendance and leave rules. Historical payroll runs remain unchanged.</DialogDescription></DialogHeader><div className="space-y-4"><div className="space-y-1"><Label htmlFor="holiday-calendar">Calendar</Label><FormSelect id="holiday-calendar" className="h-10 w-full rounded-md border bg-background px-3 text-sm" value={form.calendarId} onChange={(e) => update("calendarId", e.target.value)}>{calendarRows.map((row) => <option key={String(row.id)} value={String(row.id)}>{String(row.name ?? row.id)}</option>)}</FormSelect></div><div className="space-y-1"><Label htmlFor="holiday-name">Holiday name</Label><Input id="holiday-name" value={form.name} onChange={(e) => update("name", e.target.value)} /></div><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1"><Label htmlFor="holiday-date">Holiday date</Label><Input id="holiday-date" type="date" value={form.holidayDate} onChange={(e) => update("holidayDate", e.target.value)} /></div><div className="space-y-1"><Label htmlFor="holiday-observed">Observed on (optional)</Label><Input id="holiday-observed" value={form.observedOn} onChange={(e) => update("observedOn", e.target.value)} placeholder="yyyy-MM-dd" /></div></div><label className="flex items-center gap-2 text-sm"><FormToggle type="checkbox" checked={form.isRecurring} onChange={(e) => update("isRecurring", e.target.checked)} />Repeats annually</label><div className="space-y-1"><Label htmlFor="holiday-description">Description (optional)</Label><Input id="holiday-description" value={form.description} onChange={(e) => update("description", e.target.value)} /></div></div><DialogFooter><Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button><Button disabled={busy} onClick={save}>{busy ? "Saving…" : "Save holiday"}</Button></DialogFooter></DialogContent></Dialog></AppShell>;
}
