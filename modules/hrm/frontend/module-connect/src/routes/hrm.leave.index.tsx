import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import {
  AlertCircle,
  Ban,
  CalendarPlus,
  CalendarRange,
  CheckCircle2,
  CircleArrowLeft,
  CircleDot,
  RotateCcw,
  XCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { ScopeBadge } from "@/platform/components/ScopeBadge";
import {
  hrmApi,
  type MyLeave,
  type SelfLeaveRequest,
} from "@/platform/api-client";
import { useApi } from "@/platform/use-api";
import { api } from "@/mock/service";
import type { LeaveRequest } from "@/mock/types";

export const Route = createFileRoute("/hrm/leave/")({
  head: () => ({
    meta: [
      { title: "My leave — Kuubu HRM" },
      {
        name: "description",
        content:
          "Your leave balances, open requests and what happens next — one self-service page.",
      },
      { property: "og:title", content: "My leave — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "Your leave balances, open requests and what happens next — one self-service page.",
      },
    ],
  }),
  component: MyLeavePage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const statusMeta: Record<
  string,
  { label: string; tone: string; icon: typeof CircleDot; actionable: boolean }
> = {
  submitted: { label: "Submitted", tone: "text-info", icon: CircleDot, actionable: true },
  "in-review": { label: "In review", tone: "text-primary", icon: CircleArrowLeft, actionable: true },
  returned: { label: "Returned", tone: "text-warning", icon: RotateCcw, actionable: true },
  approved: { label: "Approved", tone: "text-success", icon: CheckCircle2, actionable: false },
  rejected: { label: "Rejected", tone: "text-destructive", icon: XCircle, actionable: false },
  cancelled: { label: "Cancelled", tone: "text-muted-foreground", icon: Ban, actionable: false },
};

function statusOf(status: string) {
  return statusMeta[status] ?? { label: status, tone: "text-muted-foreground", icon: CircleDot, actionable: false };
}

function toMockRows(): LeaveRequest[] {
  return [
    {
      id: "LV-0004",
      employeeId: "w-1001",
      type: "Annual",
      from: "2026-09-07",
      to: "2026-09-11",
      days: 5,
      status: "Submitted",
      nextAction: "Awaiting manager approval",
      dueDate: "2026-09-07",
      owner: "You",
      submittedAt: "2026-08-12",
      policy: [],
      timeline: [],
      conflicts: [],
    },
  ];
}

function balanceCard(b: {
  leaveTypeCode: string;
  leaveTypeName: string;
  accrued: number;
  taken: number;
  reserved: number;
  available: number;
}) {
  const pct = b.accrued > 0 ? Math.min(100, Math.round((b.reserved / b.accrued) * 100)) : 0;
  return (
    <div key={b.leaveTypeCode} className="rounded-md border bg-surface-muted px-4 py-3">
      <p className="text-sm font-medium">{b.leaveTypeName || b.leaveTypeCode}</p>
      <div className="mt-2 h-2 w-full overflow-hidden rounded-full bg-muted">
        <div
          className="h-full rounded-full bg-primary transition-all"
          style={{ width: `${pct}%` }}
        />
      </div>
      <dl className="mt-2 grid grid-cols-3 gap-2 text-xs text-muted-foreground">
        <div>
          <dt className="uppercase tracking-wide">Available</dt>
          <dd className="text-base font-semibold tabular text-foreground">{b.available}</dd>
        </div>
        <div>
          <dt className="uppercase tracking-wide">Reserved</dt>
          <dd className="text-base font-semibold tabular text-foreground">{b.reserved}</dd>
        </div>
        <div>
          <dt className="uppercase tracking-wide">Taken</dt>
          <dd className="text-base font-semibold tabular text-foreground">{b.taken}</dd>
        </div>
      </dl>
    </div>
  );
}

function MyLeavePage() {
  const [cancelId, setCancelId] = useState<string | null>(null);
  const [cancelState, setCancelState] = useState<"idle" | "busy" | "done" | "error">("idle");
  const [cancelError, setCancelError] = useState<string | null>(null);

  const state = useApi<MyLeave>(
    async () =>
      USE_REAL
        ? hrmApi.myLeave()
        : Promise.resolve({ linked: true, workerId: "w-mock", workerName: "Mock Employee", balances: [], requests: [] } satisfies MyLeave),
    [],
  );

  if (!USE_REAL) {
    return (
      <AuthGate>
        <AppShell>
          <PageHeader
            eyebrow="Leave"
            title="My leave"
            description="Your leave balances, open requests and what happens next."
            meta={<ScopeBadge />}
            primaryAction={
              <Button asChild>
                <Link to="/hrm/leave/new">
                  <CalendarPlus className="mr-1 size-4" aria-hidden />
                  Request leave
                </Link>
              </Button>
            }
          />
          <div className="mt-6">
            <Async
              state={{
                data: { requests: toMockRows().map((r) => ({ id: r.id, leaveTypeCode: r.type, startDate: r.from, endDate: r.to, requestedDays: r.days, status: "submitted", rejectionReason: null, crossesCutoff: false, createdAt: r.submittedAt })) as SelfLeaveRequest[], balances: [], linked: true, workerId: "w-mock", workerName: "Mock Employee" },
                loading: false,
                degraded: null,
                error: null,
                reload: () => undefined,
              }}
            >
              {(inbox) => (
                <div className="rounded-md border bg-surface-muted px-4 py-3 text-sm">
                  <p className="font-medium">Mock preview — {inbox.requests.length} request</p>
                  <ul className="mt-2 space-y-1">
                    {inbox.requests.map((r) => (
                      <li key={r.id}>
                        {r.leaveTypeCode} · {r.startDate} → {r.endDate} · {r.status}
                      </li>
                    ))}
                  </ul>
                  <p className="mt-3 text-xs text-muted-foreground">
                    This view is stubbed in mock mode; balances and requests come from the real API in live mode.
                  </p>
                </div>
              )}
            </Async>
          </div>
        </AppShell>
      </AuthGate>
    );
  }

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Leave"
          title="My leave"
          description="Your balances, open requests and what happens next — one self-service page."
          meta={<ScopeBadge />}
          primaryAction={
            <Button asChild>
              <Link to="/hrm/leave/new">
                <CalendarPlus className="mr-1 size-4" aria-hidden />
                Request leave
              </Link>
            </Button>
          }
        />
        <Async<MyLeave> state={state}>
          {(inbox) => (
            <div className="space-y-6">
              {!inbox.linked && (
                <Card className="border-warning">
                  <CardContent className="flex gap-3 py-4">
                    <AlertCircle className="mt-0.5 size-5 shrink-0 text-warning" aria-hidden />
                    <div>
                      <p className="font-medium">No employee record is linked to your account.</p>
                      <p className="mt-1 text-sm text-muted-foreground">
                        Leave requests and balances are tracked against your employee record. Ask HR
                        to link your account, then come back here.
                      </p>
                    </div>
                  </CardContent>
                </Card>
              )}
              {inbox.linked && inbox.workerName && (
                <p className="text-sm text-muted-foreground">
                  Signed in as {inbox.workerName}
                  {inbox.employeeNo ? ` (${inbox.employeeNo})` : ""}
                </p>
              )}

              <section aria-labelledby="balances-title">
                <h2 id="balances-title" className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                  Leave balances
                </h2>
                {inbox.balances.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No leave types are configured yet. Balances will appear here once payroll sets up
                    leave types for your tenant.
                  </p>
                ) : (
                  <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                    {inbox.balances.map(balanceCard)}
                  </div>
                )}
              </section>

              <section aria-labelledby="requests-title">
                <h2 id="requests-title" className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted-foreground">
                  My requests
                </h2>
                <Async
                  state={{
                    data: inbox.requests,
                    loading: false,
                    degraded: null,
                    error: null,
                    reload: () => undefined,
                  }}
                >
                  {(rows) =>
                    rows.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        Nothing yet. When you request leave, it will appear here.
                      </p>
                    ) : (
                      <ul className="space-y-2">
                        {rows.map((r) => (
                          <RequestRow
                            key={r.id}
                            request={r}
                            cancelling={cancelId === r.id}
                            onCancel={async () => {
                              setCancelId(r.id);
                              setCancelState("busy");
                              setCancelError(null);
                              try {
                                await hrmApi.cancelLeave(r.id);
                                setCancelState("done");
                              } catch (e) {
                                setCancelState("error");
                                setCancelError(e instanceof Error ? e.message : "Cancel failed");
                              } finally {
                                // refresh list after a short delay so the API settles
                                setTimeout(() => window.location.reload(), 900);
                              }
                            }}
                            cancelState={cancelId === r.id ? cancelState : "idle"}
                            cancelError={cancelId === r.id ? cancelError : null}
                          />
                        ))}
                      </ul>
                    )
                  }
                </Async>
              </section>
            </div>
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}

function RequestRow({
  request,
  cancelling,
  onCancel,
  cancelState,
  cancelError,
}: {
  request: SelfLeaveRequest;
  cancelling: boolean;
  onCancel: () => void;
  cancelState: "idle" | "busy" | "done" | "error";
  cancelError: string | null;
}) {
  const meta = statusOf(request.status);
  const Icon = meta.icon;
  const canCancel = meta.actionable && cancelState === "idle";
  return (
    <li className="flex items-start justify-between gap-4 rounded-md border bg-surface-muted p-4">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">
            {request.leaveTypeCode} leave
          </span>
          <span className={`inline-flex items-center gap-1 text-xs ${meta.tone}`}>
            <Icon className="size-3.5" aria-hidden />
            {meta.label}
          </span>
          {request.crossesCutoff && (
            <span className="inline-flex items-center gap-1 rounded-sm border border-warning/50 bg-warning/10 px-1.5 py-0.5 text-xs text-warning">
              <CalendarRange className="size-3.5" aria-hidden />
              crosses payroll cutoff
            </span>
          )}
          <span className="text-xs text-muted-foreground">
            {request.startDate} → {request.endDate} · {request.requestedDays} day
            {request.requestedDays === 1 ? "" : "s"}
          </span>
        </div>
        {request.rejectionReason && (
          <p className="mt-1.5 text-sm text-muted-foreground">
            Note from reviewer: <span className="italic">{request.rejectionReason}</span>
          </p>
        )}
        {cancelState === "done" && (
          <p className="mt-1.5 inline-flex items-center gap-1 text-sm text-success">
            <CheckCircle2 className="size-4" aria-hidden /> Request cancelled — the reserved balance
            was released.
          </p>
        )}
        {cancelState === "error" && cancelError && (
          <p className="mt-1.5 text-sm text-destructive">{cancelError}</p>
        )}
        <p className="mt-1 text-xs text-muted-foreground">
          Requested {new Date(request.createdAt).toLocaleString()} ·{" "}
          <Link
            to="/hrm/leave/$id"
            params={{ id: request.id }}
            className="text-primary underline underline-offset-2"
          >
            open details
          </Link>
        </p>
      </div>
      <div className="shrink-0 pt-1">
        {canCancel ? (
          <Button
            size="sm"
            variant="outline"
            className="border-destructive/40 text-destructive hover:bg-destructive/10"
            onClick={onCancel}
          >
            <Ban className="mr-1 size-3.5" aria-hidden />
            Cancel
          </Button>
        ) : cancelState === "busy" ? (
          <Button size="sm" variant="outline" disabled>
            <RotateCcw className="mr-1 size-3.5 animate-spin" aria-hidden />
            Cancelling…
          </Button>
        ) : null}
      </div>
    </li>
  );
}
