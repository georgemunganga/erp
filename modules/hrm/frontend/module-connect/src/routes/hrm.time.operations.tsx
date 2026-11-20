import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, CalendarDays, CheckCircle2, Clock3, FileClock, Info, ListFilter, LockKeyhole, RefreshCw, Search, TimerReset, Upload, UserRound, WalletCards, XCircle } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { PeriodOvertimeAmounts } from "@/platform/components/PeriodOvertimeAmounts";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/time/operations")({
  head: () => ({ meta: [{ title: "Overtime review — Kuubu HRM" }, { name: "description", content: "Review attendance-derived overtime before payroll." }] }),
  component: OvertimeReviewPage,
});

type QueueTab = "needs-review" | "approved" | "rejected" | "paid" | "all";
type OvertimeStatus = "pending" | "approved" | "rejected" | "paid" | "none";

const STATUS_COPY: Record<OvertimeStatus, string> = {
  pending: "Needs review",
  approved: "Approved for payroll",
  rejected: "Rejected",
  paid: "Paid in payroll",
  none: "Not applicable",
};

const STATUS_CLASS: Record<OvertimeStatus, string> = {
  pending: "border-warning/40 bg-warning-soft text-warning-foreground",
  approved: "border-info/30 bg-info-soft text-info-foreground",
  rejected: "border-danger/30 bg-danger-soft text-danger",
  paid: "border-success/30 bg-success-soft text-success-foreground",
  none: "border-border bg-muted text-muted-foreground",
};

function statusOf(value: unknown): OvertimeStatus {
  const status = String(value ?? "none").toLowerCase() as OvertimeStatus;
  return status in STATUS_COPY ? status : "none";
}

function numberOf(value: unknown) {
  return Number(value ?? 0).toFixed(2);
}

function OvertimeReviewPage() {
  const overtime = useApi(() => realApi.overtime({}), []);
  const rows = (overtime.data ?? []) as Array<Record<string, unknown>>;
  const [tab, setTab] = useState<QueueTab>("needs-review");
  const [search, setSearch] = useState("");
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [optimistic, setOptimistic] = useState<Record<string, OvertimeStatus>>({});
  const [decisionBusy, setDecisionBusy] = useState<string | null>(null);

  const statusFor = (row: Record<string, unknown>) => optimistic[String(row.id)] ?? statusOf(row.overtimeStatus);
  const summary = useMemo(() => {
    const value = { pending: 0, approved: 0, rejected: 0, paid: 0, pendingHours: 0, approvedHours: 0, paidHours: 0 };
    for (const row of rows) {
      const status = optimistic[String(row.id)] ?? statusOf(row.overtimeStatus);
      const overtimeHours = Number(row.overtimeHours ?? 0);
      if (status === "pending") { value.pending += 1; value.pendingHours += overtimeHours; }
      if (status === "approved") { value.approved += 1; value.approvedHours += overtimeHours; }
      if (status === "rejected") value.rejected += 1;
      if (status === "paid") { value.paid += 1; value.paidHours += overtimeHours; }
    }
    return value;
  }, [optimistic, rows]);

  const visibleRows = useMemo(() => {
    const query = search.trim().toLowerCase();
    return rows.filter((row) => {
      const status = optimistic[String(row.id)] ?? statusOf(row.overtimeStatus);
      const inTab = tab === "all" || (tab === "needs-review" && status === "pending") || tab === status;
      const text = [row.workerName, row.workerId, row.workDate, row.source, row.overtimeDecisionReason].map((v) => String(v ?? "").toLowerCase()).join(" ");
      return inTab && (!query || text.includes(query));
    });
  }, [optimistic, rows, search, tab]);

  const workflowMessage = summary.pending > 0
    ? "Review the waiting rows below. Approved hours become eligible for payroll."
    : summary.approved > 0
      ? "Approved hours are ready for the payroll preparer to include in the pay run."
      : "No overtime is waiting for a decision. New attendance imports appear in the review queue.";

  async function decide(id: string, action: "approve" | "reject") {
    const reason = reasons[id]?.trim() ?? "";
    if (action === "reject" && !reason) {
      toast.error("Add a reason to explain why this overtime is being rejected.");
      return;
    }
    setDecisionBusy(id);
    setOptimistic((current) => ({ ...current, [id]: action === "approve" ? "approved" : "rejected" }));
    try {
      await realApi.decideOvertime(id, action, reason || undefined);
      toast.success(action === "approve" ? "Overtime approved for payroll." : "Overtime rejected with reason recorded.");
      setReasons((current) => { const next = { ...current }; delete next[id]; return next; });
      setOptimistic((current) => { const next = { ...current }; delete next[id]; return next; });
      overtime.reload();
    } catch (error) {
      setOptimistic((current) => { const next = { ...current }; delete next[id]; return next; });
      toast.error(error instanceof Error ? error.message : "Overtime decision failed");
    } finally {
      setDecisionBusy(null);
    }
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Time and leave / daily operations"
          title="Overtime review"
          description="Check attendance-derived overtime, make safe decisions, and hand approved hours to payroll."
          meta={<><Badge variant="outline" className="gap-1.5 border-info/30 bg-info-soft text-info-foreground"><LockKeyhole className="size-3" aria-hidden /> Live payroll workflow</Badge><span className="text-xs text-muted-foreground">Your organisation and branch scope is applied automatically.</span></>}
          primaryAction={<Button asChild className="gap-2"><Link to="/hrm/time/attendance/import"><Upload className="size-4" aria-hidden />Import attendance</Link></Button>}
        />

        <div className="space-y-6" data-testid="overtime-review-page">
          <PeriodOvertimeAmounts />
          <section className="space-y-3" aria-labelledby="workflow-title">
            <div className="flex flex-wrap items-end justify-between gap-3"><div><p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">How this works</p><h2 id="workflow-title" className="mt-1 text-lg font-semibold">From clocked time to payroll</h2></div><p className="max-w-xl text-sm text-muted-foreground">{workflowMessage}</p></div>
            <div className="grid gap-2 md:grid-cols-4"><WorkflowStep number="1" title="Import" detail="Bring in clocked attendance." complete={rows.length > 0} /><WorkflowStep number="2" title="Review" detail="Check derived overtime." active={summary.pending > 0} /><WorkflowStep number="3" title="Approve" detail="Make hours payroll-eligible." active={summary.approved > 0} /><WorkflowStep number="4" title="Payroll" detail="Release and link the source row." active={summary.paid > 0} /></div>
          </section>

          <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4" aria-label="Overtime queue summary">
            <SummaryCard title="Needs review" value={summary.pending} detail={`${numberOf(summary.pendingHours)} hours waiting`} icon={TimerReset} tone="warning" active={summary.pending > 0} />
            <SummaryCard title="Approved" value={summary.approved} detail={`${numberOf(summary.approvedHours)} hours eligible`} icon={CheckCircle2} tone="info" />
            <SummaryCard title="Rejected" value={summary.rejected} detail="Excluded from payroll" icon={XCircle} tone="danger" />
            <SummaryCard title="Paid in payroll" value={summary.paid} detail={`${numberOf(summary.paidHours)} historical hours`} icon={WalletCards} tone="success" />
          </section>

          <Card className="overflow-hidden border-border/90 shadow-sm">
            <CardHeader className="border-b bg-surface-muted/50 pb-4"><div className="flex flex-wrap items-start justify-between gap-4"><div><div className="flex items-center gap-2"><CardTitle>Overtime review queue</CardTitle><Badge variant="outline" className="font-normal">{rows.length} records</Badge></div><CardDescription className="mt-1 max-w-2xl">Work through the rows that need your decision. Approve only the hours you are comfortable sending to payroll.</CardDescription></div><Button variant="outline" size="sm" onClick={() => overtime.reload()} disabled={overtime.loading} className="gap-2"><RefreshCw className={`size-4 ${overtime.loading ? "animate-spin" : ""}`} aria-hidden />Refresh queue</Button></div></CardHeader>
            <CardContent className="p-0">
              <div className="space-y-4 border-b p-4"><Tabs value={tab} onValueChange={(value) => setTab(value as QueueTab)}><TabsList className="h-auto w-full justify-start gap-1 overflow-x-auto bg-transparent p-0"><QueueTab value="needs-review" label="Needs review" count={summary.pending} tone="warning" /><QueueTab value="approved" label="Approved" count={summary.approved} tone="info" /><QueueTab value="rejected" label="Rejected" count={summary.rejected} tone="danger" /><QueueTab value="paid" label="Paid" count={summary.paid} tone="success" /><TabsTrigger value="all" className="px-3 py-2">All records</TabsTrigger></TabsList></Tabs><div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_220px]"><div className="relative"><Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden /><Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search employee, date, or import source" className="pl-9" aria-label="Search overtime records" /></div><div className="flex items-center gap-2 rounded-md border bg-card px-3 text-sm text-muted-foreground"><CalendarDays className="size-4 shrink-0" aria-hidden />All available dates</div></div></div>

              {overtime.loading ? <div className="space-y-3 p-6" aria-live="polite">{[1, 2].map((item) => <div key={item} className="h-36 animate-pulse rounded-xl bg-muted" />)}</div> : overtime.error ? <div className="m-4 flex items-start gap-3 rounded-xl border border-danger/30 bg-danger-soft/30 p-4 text-sm" role="alert"><Info className="mt-0.5 size-4 shrink-0 text-danger" aria-hidden /><div><p className="font-medium text-danger">The overtime queue could not be loaded.</p><p className="mt-1 text-muted-foreground">{overtime.error}</p><Button variant="outline" size="sm" className="mt-3" onClick={() => overtime.reload()}>Try again</Button></div></div> : visibleRows.length === 0 ? <EmptyQueue total={rows.length} tab={tab} onViewAll={() => setTab("all")} onImport={() => undefined} /> : <div className="divide-y divide-border">{visibleRows.map((row) => <OvertimeRow key={String(row.id)} row={row} status={statusFor(row)} reason={reasons[String(row.id)] ?? ""} busy={decisionBusy === String(row.id)} onReasonChange={(reason) => setReasons((current) => ({ ...current, [String(row.id)]: reason }))} onDecide={(action) => void decide(String(row.id), action)} />)}</div>}
            </CardContent>
          </Card>

          <section aria-label="Related time workflows" className="grid gap-3 sm:grid-cols-3"><RelatedLink icon={Upload} title="Import attendance" detail="Bring in clocked records using the shared importer." to="/hrm/time/attendance/import" /><RelatedLink icon={FileClock} title="Attendance corrections" detail="Resolve missed or incorrect clocking records." to="/hrm/attendance" /><RelatedLink icon={Clock3} title="Schedules and rosters" detail="Maintain the working pattern used for calculations." to="/hrm/time/schedules" /></section>
        </div>
      </AppShell>
    </AuthGate>
  );
}

function WorkflowStep({ number, title, detail, active = false, complete = false }: { number: string; title: string; detail: string; active?: boolean; complete?: boolean }) {
  return <div className={`flex items-start gap-3 rounded-xl border p-3 ${active ? "border-primary bg-primary-soft/60" : "bg-card"}`}><span className={`flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold ${complete ? "bg-success text-success-foreground" : active ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"}`}>{complete ? <CheckCircle2 className="size-4" aria-hidden /> : number}</span><div><p className="text-sm font-semibold">{title}</p><p className="mt-0.5 text-xs leading-4 text-muted-foreground">{detail}</p></div></div>;
}

function SummaryCard({ title, value, detail, icon: Icon, tone, active = false }: { title: string; value: number; detail: string; icon: typeof TimerReset; tone: "warning" | "info" | "danger" | "success"; active?: boolean }) {
  const styles = { warning: "bg-warning-soft text-warning-foreground", info: "bg-info-soft text-info-foreground", danger: "bg-danger-soft text-danger", success: "bg-success-soft text-success-foreground" };
  return <Card className={`${active ? "border-warning/60 bg-warning-soft/30" : ""} shadow-none`}><CardContent className="flex items-start justify-between gap-3 p-4"><div><p className="text-sm font-medium text-muted-foreground">{title}</p><p className="mt-1 text-2xl font-semibold tabular">{value}</p><p className="mt-1 text-xs text-muted-foreground">{detail}</p></div><span className={`flex size-10 items-center justify-center rounded-xl ${styles[tone]}`}><Icon className="size-5" aria-hidden /></span></CardContent></Card>;
}

function QueueTab({ value, label, count, tone }: { value: QueueTab; label: string; count: number; tone: "warning" | "info" | "danger" | "success" }) {
  const colors = { warning: "data-[state=active]:border-warning/50 data-[state=active]:bg-warning-soft/60", info: "data-[state=active]:border-info/30 data-[state=active]:bg-info-soft/60", danger: "data-[state=active]:border-danger/30 data-[state=active]:bg-danger-soft/60", success: "data-[state=active]:border-success/30 data-[state=active]:bg-success-soft/60" };
  return <TabsTrigger value={value} className={`gap-2 border border-transparent px-3 py-2 ${colors[tone]}`}>{label}<span className="rounded-full bg-muted px-1.5 text-xs tabular">{count}</span></TabsTrigger>;
}

function OvertimeRow({ row, status, reason, busy, onReasonChange, onDecide }: { row: Record<string, unknown>; status: OvertimeStatus; reason: string; busy: boolean; onReasonChange: (reason: string) => void; onDecide: (action: "approve" | "reject") => void }) {
  const pending = status === "pending";
  const id = String(row.id);
  return <article className="space-y-4 p-4 transition-colors hover:bg-surface-muted/35 sm:p-5" data-testid={`overtime-row-${id}`}><div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between"><div className="flex min-w-0 items-start gap-3"><span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-secondary text-secondary-foreground"><UserRound className="size-5" aria-hidden /></span><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><h3 className="font-semibold">{String(row.workerName ?? row.workerId ?? "Worker")}</h3><Badge variant="outline" className={STATUS_CLASS[status]}>{STATUS_COPY[status]}</Badge></div><p className="mt-1 text-sm text-muted-foreground">{String(row.workDate ?? "—")} · {String(row.workerId ?? "No worker reference")}</p><p className="mt-1 text-xs text-muted-foreground">Source: {String(row.source ?? "attendance")}{row.importBatchId ? ` · import ${String(row.importBatchId).slice(0, 8)}` : ""}</p></div></div><div className="flex items-center gap-2 text-sm text-muted-foreground">{status === "paid" ? <><LockKeyhole className="size-4" aria-hidden /> Historical payroll record</> : status === "approved" ? <><CheckCircle2 className="size-4 text-info" aria-hidden /> Eligible for payroll</> : status === "rejected" ? <><XCircle className="size-4 text-danger" aria-hidden /> Excluded from payroll</> : <><Clock3 className="size-4 text-warning-foreground" aria-hidden /> Waiting for review</>}</div></div><div className="grid grid-cols-2 gap-3 rounded-xl bg-surface-muted/70 p-3 sm:grid-cols-4"><Metric label="Scheduled" value={`${numberOf(row.scheduledHours)}h`} /><Metric label="Worked" value={`${numberOf(row.totalHours)}h`} /><Metric label="Regular" value={`${numberOf(row.regularHours)}h`} /><Metric label="Overtime" value={`${numberOf(row.overtimeHours)}h ×${numberOf(row.overtimeMultiplier)}`} emphasis /></div>{row.overtimeDecisionReason ? <div className="rounded-lg border border-border bg-card px-3 py-2 text-sm"><span className="font-medium">Decision note:</span> <span className="text-muted-foreground">{String(row.overtimeDecisionReason)}</span></div> : null}{row.overtimePayrollRunId ? <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground"><WalletCards className="size-3.5" aria-hidden />Payroll run linked: <span className="font-mono">{String(row.overtimePayrollRunId)}</span>{row.overtimePayrollLineId ? <><span>·</span><span className="font-mono">line {String(row.overtimePayrollLineId)}</span></> : null}</div> : null}{pending ? <div className="grid gap-3 rounded-xl border border-warning/40 bg-warning-soft/25 p-3 md:grid-cols-[minmax(0,1fr)_auto_auto] md:items-end"><div><Label htmlFor={`overtime-reason-${id}`} className="text-sm">Decision note <span className="font-normal text-muted-foreground">(required to reject)</span></Label><Input id={`overtime-reason-${id}`} value={reason} onChange={(event) => onReasonChange(event.target.value)} placeholder="e.g. Approved by line manager / missing clock-out" className="mt-1.5 bg-card" /></div><Button onClick={() => onDecide("approve")} disabled={busy} className="gap-2"><CheckCircle2 className="size-4" aria-hidden />{busy ? "Saving…" : "Approve hours"}</Button><Button variant="outline" onClick={() => onDecide("reject")} disabled={busy || !reason.trim()} className="gap-2 border-danger/40 text-danger hover:bg-danger-soft"><XCircle className="size-4" aria-hidden />Reject</Button></div> : null}</article>;
}

function Metric({ label, value, emphasis = false }: { label: string; value: string; emphasis?: boolean }) { return <div><p className="text-xs text-muted-foreground">{label}</p><p className={`mt-1 font-semibold tabular ${emphasis ? "text-primary-foreground" : ""}`}>{value}</p></div>; }

function EmptyQueue({ total, tab, onViewAll }: { total: number; tab: QueueTab; onViewAll: () => void; onImport: () => void }) {
  return <div className="flex flex-col items-center justify-center px-6 py-14 text-center"><span className="flex size-12 items-center justify-center rounded-2xl bg-primary-soft text-primary-foreground">{total === 0 ? <Upload className="size-5" aria-hidden /> : <ListFilter className="size-5" aria-hidden />}</span><h3 className="mt-4 text-base font-semibold">{total === 0 ? "No overtime has been imported yet" : tab === "needs-review" ? "Nothing needs your decision" : `No ${tab} records found`}</h3><p className="mt-1 max-w-md text-sm text-muted-foreground">{total === 0 ? "Use Import attendance to bring in clocked records. The system will derive overtime from the assigned shift." : tab === "needs-review" ? "That is good news. Approved, rejected, and paid history remains available in the tabs above." : "Try another tab or clear the search to see the rest of the queue."}</p>{total > 0 && tab !== "needs-review" ? <Button variant="outline" className="mt-5" onClick={onViewAll}>View all records</Button> : null}</div>;
}

function RelatedLink({ icon: Icon, title, detail, to }: { icon: typeof Upload; title: string; detail: string; to: "/hrm/time/attendance/import" | "/hrm/attendance" | "/hrm/time/schedules" }) { return <Link to={to} className="group rounded-xl border bg-card p-4 transition-colors hover:border-primary/40 hover:bg-primary-soft/20"><div className="flex items-start gap-3"><span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-secondary text-secondary-foreground"><Icon className="size-4" aria-hidden /></span><div className="min-w-0"><p className="font-semibold group-hover:text-primary-foreground">{title}<ArrowRight className="ml-2 inline size-3.5" aria-hidden /></p><p className="mt-1 text-sm leading-5 text-muted-foreground">{detail}</p></div></div></Link>; }
