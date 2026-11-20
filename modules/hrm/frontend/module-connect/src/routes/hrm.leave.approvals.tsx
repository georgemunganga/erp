/**
 * M17 — HR leave approvals inbox (employer side).
 *
 * Company-wide view of every leave request: filter by status, open a request
 * for a full decision (approve / return / reject) and act on the whole list.
 * Scopes the list to what needs decisions and shows a pending/returned summary
 * so HR can see at a glance how much is waiting on them.
 *
 * Roles: hr_ops, hr_admin, manager (enforced by the nav link and by the
 * backend on every decision call).
 */
import { createFileRoute, Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import {
  AlertTriangle,
  CalendarClock,
  CalendarDays,
  CalendarPlus,
  CheckCircle2,
  RotateCcw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ListPage } from "@/platform/components/ListPage";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/leave/approvals")({
  head: () => ({
    meta: [
      { title: "Leave approvals — Kuubu HRM" },
      { name: "description", content: "Company-wide leave requests waiting on an HR decision." },
      { property: "og:title", content: "Leave approvals — Kuubu HRM" },
      { property: "og:description", content: "Company-wide leave requests waiting on an HR decision." },
    ],
  }),
  component: LeaveApprovals,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/** Backend status values (submitted, in-review, returned) are actionable; everything else is closed. */
const actionables = new Set(["submitted", "in-review", "returned"]);

const statusLabel: Record<string, string> = {
  submitted: "Submitted",
  "in-review": "In review",
  returned: "Returned",
  approved: "Approved",
  rejected: "Rejected",
  cancelled: "Cancelled",
};

function labelOf(status: string) {
  return statusLabel[status] ?? status;
}

function rowFrom(item: Record<string, unknown>) {
  return {
    id: String(item.id ?? ""),
    workerId: String(item.workerId ?? ""),
    workerName: String(item.workerName ?? "—"),
    leaveTypeCode: String(item.leaveTypeCode ?? ""),
    startDate: String(item.startDate ?? ""),
    endDate: String(item.endDate ?? ""),
    requestedDays: Number(item.requestedDays ?? 0),
    status: String(item.status ?? ""),
    balanceReserved: Boolean(item.balanceReserved),
    crossesCutoff: Boolean(item.crossesCutoff),
    createdAt: String(item.createdAt ?? ""),
  };
}

function LeaveApprovals() {
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [decideRow, setDecideRow] = useState<ReturnType<typeof rowFrom> | null>(null);
  const [decisionError, setDecisionError] = useState<string | null>(null);

  const state = useApi(
    () =>
      USE_REAL
        ? realApi.leaveRequests(statusFilter ? { status: statusFilter } : undefined).then((p) => p.items.map(rowFrom))
        : Promise.resolve([] as ReturnType<typeof rowFrom>[]),
    [statusFilter],
  );

  const summary = useMemo(() => {
    if (!USE_REAL) return { pending: 0, returned: 0 };
    const items = state.data ?? [];
    return {
      pending: items.filter((r) => actionables.has(r.status)).length,
      returned: items.filter((r) => r.status === "returned").length,
    };
  }, [state.data]);

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Leave"
          title="Leave approvals"
          description="Company-wide leave requests waiting on an HR decision."
          meta={<ScopeBadge />}
          primaryAction={
            <Button asChild>
              <Link to="/hrm/leave">
                <CalendarClock className="mr-1 size-4" aria-hidden />
                My leave
              </Link>
            </Button>
          }
        />
        <div className="mt-4 grid gap-3 sm:grid-cols-3">
          <SummaryCard
            label="Awaiting decision"
            value={summary.pending}
            hint="Submitted or in review"
            icon={CalendarDays}
          />
          <SummaryCard
            label="Returned to requester"
            value={summary.returned}
            hint="Sent back for changes"
            icon={RotateCcw}
          />
          <SummaryCard
            label="This pay cycle"
            value={summary.pending + summary.returned}
            hint="Total needing action"
            icon={AlertTriangle}
          />
        </div>

        <div className="mt-6 flex flex-wrap items-center gap-2">
          {(
            [
              ["", "All statuses"],
              ["submitted", "Submitted"],
              ["in-review", "In review"],
              ["returned", "Returned"],
            ] as const
          ).map(([value, label]) => (
            <Button
              key={value || "all"}
              size="sm"
              variant={statusFilter === value ? "default" : "outline"}
              onClick={() => setStatusFilter(value)}
            >
              {label}
            </Button>
          ))}
        </div>

        <Async state={state}>
          {(rows) => (
            <ListPage
              rows={rows}
              searchPlaceholder="Search employee or leave type"
              searchFields={(r) => `${r.workerName} ${r.leaveTypeCode} ${r.id}`}
              filters={[
                {
                  id: "status",
                  label: "Status",
                  options: ["Submitted", "In review", "Returned", "Approved", "Rejected", "Cancelled"],
                  match: (r, v) => labelOf(r.status) === v,
                },
              ]}
              columns={[
                { id: "employee", header: "Employee", cell: (r) => <span className="block max-w-56 truncate">{r.workerName}</span> },
                { id: "type", header: "Leave type", cell: (r) => r.leaveTypeCode },
                { id: "dates", header: "Dates", cell: (r) => <span className="text-xs">{r.startDate} → {r.endDate}</span> },
                { id: "days", header: "Days", cell: (r) => r.requestedDays },
                { id: "status", header: "Status", cell: (r) => <StatusBadge status={labelOf(r.status)} /> },
                { id: "cutoff", header: "", cell: (r) => (r.crossesCutoff ? <span className="inline-flex items-center gap-1 text-xs text-warning"><AlertTriangle className="size-3.5" aria-hidden />cutoff</span> : null) },
                { id: "actions", header: "Actions", cell: (r) => (
                  <Button
                    size="sm"
                    variant={actionables.has(r.status) ? "outline" : "ghost"}
                    disabled={!actionables.has(r.status)}
                    onClick={() => { setDecideRow(r); setDecisionError(null); }}
                  >
                    <CheckCircle2 className="mr-1 size-3.5" aria-hidden />
                    Decide
                  </Button>
                ) },
              ]}
              emptyBody="No leave requests found — employees' requests will appear here."
            />
          )}
        </Async>

        {decideRow && (
          <DecisionDialog
            row={decideRow}
            error={decisionError}
            onClose={() => setDecideRow(null)}
            onDecide={async (action, reason) => {
              try {
                await realApi.decideLeaveRequest(decideRow.id, action, reason || undefined);
                setDecideRow(null);
                state.reload();
              } catch (e) {
                setDecisionError(e instanceof Error ? e.message : "Decision failed — check the reason and try again.");
              }
            }}
          />
        )}
      </AppShell>
    </AuthGate>
  );
}

function SummaryCard({ label, value, hint, icon: Icon }: { label: string; value: number; hint: string; icon: typeof CalendarDays }) {
  return (
    <Card>
      <CardContent className="flex items-center gap-3 py-4">
        <Icon className="size-8 shrink-0 text-muted-foreground" aria-hidden />
        <div>
          <p className="text-2xl font-semibold tabular">{value}</p>
          <p className="text-sm font-medium">{label}</p>
          <p className="text-xs text-muted-foreground">{hint}</p>
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * M17 decision dialog — HR decides a leave request in one step.
 * Approve commits the balance (already reserved), return or reject send it
 * back with an optional reason so the employee knows what to fix.
 */
function DecisionDialog({
  row,
  error,
  onClose,
  onDecide,
}: {
  row: ReturnType<typeof rowFrom>;
  error: string | null;
  onClose: () => void;
  onDecide: (action: string, reason: string) => void;
}) {
  const [action, setAction] = useState<"approve" | "return" | "reject">("approve");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const needsReason = action === "return" || action === "reject";

  async function submit() {
    setBusy(true);
    try {
      await onDecide(action, reason);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Decide leave request</DialogTitle>
          <DialogDescription>
            {row.workerName} — {row.leaveTypeCode} leave, {row.startDate} → {row.endDate} · {row.requestedDays} day
            {row.requestedDays === 1 ? "" : "s"}
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-3 gap-2">
          {(["approve", "return", "reject"] as const).map((a) => (
            <Button
              key={a}
              type="button"
              variant={action === a ? "default" : "outline"}
              className={a === "approve" ? "bg-success hover:bg-success/90 text-success-foreground" : a === "reject" ? "border-destructive/40 text-destructive hover:bg-destructive/10" : ""}
              onClick={() => setAction(a)}
            >
              {a === "approve" ? "Approve" : a === "return" ? "Return" : "Reject"}
            </Button>
          ))}
        </div>

        {needsReason && (
          <Textarea
            placeholder={action === "return" ? "What should the employee change and resubmit?" : "Why is this request being rejected?"}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
          />
        )}

        {row.crossesCutoff && (
          <p className="flex items-center gap-2 text-xs text-warning">
            <AlertTriangle className="size-3.5" aria-hidden />
            This request crosses the payroll cutoff — decide before the next pay run.
          </p>
        )}

        {error && <p className="text-sm text-destructive">{error}</p>}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button
            onClick={submit}
            disabled={busy || (needsReason && !reason.trim())}
            className={action === "approve" ? "bg-success hover:bg-success/90 text-success-foreground" : action === "reject" ? "bg-destructive hover:bg-destructive/90" : ""}
          >
            <CalendarPlus className="mr-1 size-4" aria-hidden />
            {action === "approve" ? "Approve" : action === "return" ? "Return to employee" : "Reject"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
