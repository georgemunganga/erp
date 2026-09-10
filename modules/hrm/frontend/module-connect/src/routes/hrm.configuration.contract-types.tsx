import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { Archive, FileText, Pencil, Plus } from "lucide-react";
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

export const Route = createFileRoute("/hrm/configuration/contract-types")({
  head: () => ({ meta: [{ title: "Contract Types - Newworldcargo HRM" }] }),
  component: ContractTypesPage,
});

type ContractType = Record<string, unknown>;
type FormState = { code: string; name: string; probationDays: string; noticeDays: string };
const emptyForm = (): FormState => ({ code: "", name: "", probationDays: "0", noticeDays: "30" });
const rowsOf = (data: unknown): ContractType[] => Array.isArray(data) ? data as ContractType[] : ((data as { items?: unknown[] } | null)?.items ?? []) as ContractType[];
const value = (row: ContractType, key: string, fallback = "") => row[key] == null ? fallback : String(row[key]);

function ContractTypesPage() {
  const state = useApi(() => realApi.contractTypes({ includeInactive: true }), []);
  const rows = useMemo(() => rowsOf(state.data), [state.data]);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ContractType | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [busy, setBusy] = useState(false);

  const openCreate = () => { setEditing(null); setForm(emptyForm()); setOpen(true); };
  const openEdit = (row: ContractType) => {
    setEditing(row);
    setForm({ code: value(row, "code"), name: value(row, "name"), probationDays: value(row, "probationDays", "0"), noticeDays: value(row, "noticeDays", "30") });
    setOpen(true);
  };
  const save = async () => {
    if (!form.code.trim() || !form.name.trim()) {
      feedback.blocked("Contract type was not saved.", "Code and name are required.");
      return;
    }
    setBusy(true);
    try {
      const body = { code: form.code.trim().toLowerCase(), name: form.name.trim(), probationDays: Math.max(0, Number(form.probationDays) || 0), noticeDays: Math.max(0, Number(form.noticeDays) || 0) };
      if (editing) await realApi.updateContractType(value(editing, "id"), body);
      else await realApi.createContractType(body);
      feedback.success(editing ? "Contract type updated." : "Contract type created.");
      setOpen(false);
      state.reload();
    } catch (error) {
      feedback.blocked("Contract type was not saved.", error instanceof Error ? error.message : "The HRM API rejected the change.");
    } finally { setBusy(false); }
  };
  const archive = async (row: ContractType) => {
    setBusy(true);
    try {
      await realApi.updateContractType(value(row, "id"), { isActive: false });
      feedback.success("Contract type archived. Existing employee records keep their history.");
      state.reload();
    } catch (error) {
      feedback.blocked("Contract type was not archived.", error instanceof Error ? error.message : "The HRM API rejected the change.");
    } finally { setBusy(false); }
  };
  const set = (key: keyof FormState, next: string) => setForm((current) => ({ ...current, [key]: next }));

  return <AppShell>
    <PageHeader eyebrow="Configuration · Employment" title="Contract Types" description="Manage the employment terms available when creating or updating an employee assignment." meta={<ScopeBadge />} />
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3"><p className="flex items-center gap-2 text-sm text-muted-foreground"><FileText className="size-4" /> Archived types remain on historical employee assignments but cannot be selected for new changes.</p><Button onClick={openCreate}><Plus className="mr-2 size-4" />Add contract type</Button></div>
    <Async state={state} rows={5}>{() => <div className="overflow-x-auto rounded-lg border bg-surface"><table className="w-full text-left text-sm"><thead className="border-b bg-surface-muted"><tr>{["Code", "Contract type", "Probation", "Notice", "Status", "Actions"].map((heading) => <th key={heading} className="whitespace-nowrap px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{heading}</th>)}</tr></thead><tbody className="divide-y">{rows.map((row) => <tr key={value(row, "id")}><td className="px-3 py-2 font-mono text-xs">{value(row, "code")}</td><th className="px-3 py-2 font-medium">{value(row, "name")}</th><td className="px-3 py-2 tabular-nums">{value(row, "probationDays", "0")} days</td><td className="px-3 py-2 tabular-nums">{value(row, "noticeDays", "0")} days</td><td className="px-3 py-2">{row.isActive === false ? "Archived" : "Active"}</td><td className="px-3 py-2"><div className="flex gap-2"><Button size="sm" variant="outline" onClick={() => openEdit(row)}><Pencil className="mr-1 size-3.5" />Edit</Button>{row.isActive !== false ? <Button size="sm" variant="outline" disabled={busy} onClick={() => archive(row)}><Archive className="mr-1 size-3.5" />Archive</Button> : null}</div></td></tr>)}{rows.length === 0 ? <tr><td colSpan={6} className="px-3 py-10 text-center text-muted-foreground">No contract types have been configured yet.</td></tr> : null}</tbody></table></div>}</Async>
    <Dialog open={open} onOpenChange={setOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "Edit contract type" : "Add contract type"}</DialogTitle><DialogDescription>These values are used when HR creates or changes employee assignments.</DialogDescription></DialogHeader><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-1"><Label htmlFor="contract-code">Code</Label><Input id="contract-code" disabled={!!editing} value={form.code} onChange={(e) => set("code", e.target.value)} placeholder="fixed-term" /></div><div className="space-y-1"><Label htmlFor="contract-name">Name</Label><Input id="contract-name" value={form.name} onChange={(e) => set("name", e.target.value)} placeholder="Fixed term" /></div><div className="space-y-1"><Label htmlFor="contract-probation">Probation (days)</Label><Input id="contract-probation" type="number" min="0" value={form.probationDays} onChange={(e) => set("probationDays", e.target.value)} /></div><div className="space-y-1"><Label htmlFor="contract-notice">Notice period (days)</Label><Input id="contract-notice" type="number" min="0" value={form.noticeDays} onChange={(e) => set("noticeDays", e.target.value)} /></div></div><DialogFooter><Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button><Button disabled={busy} onClick={save}>{busy ? "Saving..." : "Save contract type"}</Button></DialogFooter></DialogContent></Dialog>
  </AppShell>;
}
