import { useMemo } from "react";
import { ArrowRight, CalendarCheck, CircleAlert, Coins, ListChecks } from "lucide-react";
import { Link, createFileRoute } from "@tanstack/react-router";
import { Button } from "@/components/ui/button";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/leave/control")({
  head: () => ({ meta: [{ title: "Leave Control Panel — Kuubu HRM" }, { name: "description", content: "Live leave operations control panel." }] }),
  component: LeaveControlPage,
});

type Row = Record<string, unknown>;
const rows = (value: unknown): Row[] => Array.isArray(value) ? value as Row[] : ((value as { items?: unknown[] } | null)?.items ?? []) as Row[];
const val = (row: Row, key: string, fallback = "—") => row[key] == null || row[key] === "" ? fallback : String(row[key]);

function LeaveControlPage() {
  const state = useApi(async () => {
    const [leave, encashments, history, types] = await Promise.all([
      realApi.leaveRequests({ page: 1, pageSize: 100 }),
      realApi.encashments({ status: "submitted" }),
      realApi.timeOperationsHistory(),
      realApi.leaveTypes({ includeInactive: true }),
    ]);
    return { leave: rows(leave), encashments: rows(encashments), history, types: rows(types) };
  }, []);
  const data = state.data;
  const pending = useMemo(() => data ? data.leave.filter((r) => ["submitted", "in-review", "pending"].includes(String(r.status))) : [], [data]);
  const pendingEncashments = data?.encashments ?? [];
  return <AppShell><PageHeader eyebrow="HR operations · Leave" title="Leave Control Panel" description="One operational view over live leave applications, balances, accruals, encashment, and configuration. Source records remain in their dedicated workflows." meta={<ScopeBadge />} /><Async state={state} rows={4}>{() => data ? <><div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><Metric icon={<ListChecks className="size-4" />} label="Pending applications" value={String(pending.length)} /><Metric icon={<Coins className="size-4" />} label="Encashments awaiting decision" value={String(pendingEncashments.length)} /><Metric icon={<CalendarCheck className="size-4" />} label="Configured leave types" value={String(data.types.filter((r) => r.isActive).length)} /><Metric icon={<CircleAlert className="size-4" />} label="Accrual runs recorded" value={String(rows((data.history as Row).accruals).length)} /></div><div className="mt-6 grid gap-6 xl:grid-cols-2"><section className="rounded-lg border bg-surface p-4"><div className="flex items-start justify-between gap-3"><div><h2 className="text-sm font-semibold">Applications needing attention</h2><p className="mt-1 text-xs text-muted-foreground">Review the original application before deciding.</p></div><Button size="sm" variant="outline" asChild><Link to="/hrm/leave/approvals">Open queue<ArrowRight className="ml-1 size-3.5" /></Link></Button></div><div className="mt-4 divide-y">{pending.slice(0, 8).map((r) => <div key={val(r, "id")} className="flex items-center justify-between gap-3 py-3 text-sm"><span className="min-w-0"><span className="block truncate font-medium">{val(r, "workerName")}</span><span className="block text-xs text-muted-foreground">{val(r, "leaveTypeCode")} · {val(r, "requestedDays")} days · {val(r, "startDate")}</span></span><span className="rounded-full bg-warning-soft px-2 py-1 text-xs text-warning">{val(r, "status")}</span></div>)}{pending.length === 0 ? <p className="py-6 text-sm text-muted-foreground">No pending leave applications.</p> : null}</div></section><section className="rounded-lg border bg-surface p-4"><div className="flex items-start justify-between gap-3"><div><h2 className="text-sm font-semibold">Encashment and accrual operations</h2><p className="mt-1 text-xs text-muted-foreground">Cash conversion and balance operations are controlled separately.</p></div><Button size="sm" variant="outline" asChild><Link to="/hrm/time/toil">Open encashment<ArrowRight className="ml-1 size-3.5" /></Link></Button></div><div className="mt-4 space-y-3 text-sm"><div className="flex justify-between gap-3"><span>Pending encashments</span><strong>{pendingEncashments.length}</strong></div><div className="flex justify-between gap-3"><span>Accrual runs</span><strong>{rows((data.history as Row).accruals).length}</strong></div><div className="flex justify-between gap-3"><span>Manual balance adjustments</span><strong>{rows((data.history as Row).adjustments).length}</strong></div><div className="flex justify-between gap-3"><span>Encashment decisions recorded</span><strong>{rows((data.history as Row).encashments).length}</strong></div></div></section></div><div className="mt-6 flex flex-wrap gap-2"><Button variant="outline" asChild><Link to="/hrm/configuration/leave-types">Leave types</Link></Button><Button variant="outline" asChild><Link to="/hrm/configuration/holidays">Holiday List</Link></Button><Button variant="outline" asChild><Link to="/hrm/leave">Leave applications</Link></Button></div></> : null}</Async></AppShell>;
}
function Metric({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) { return <div className="rounded-lg border bg-surface p-4"><div className="flex items-center gap-2 text-xs text-muted-foreground">{icon}{label}</div><p className="tabular mt-2 text-2xl font-semibold">{value}</p></div>; }
