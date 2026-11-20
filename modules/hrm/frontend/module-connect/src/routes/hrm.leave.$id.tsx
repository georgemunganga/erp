import { createFileRoute } from "@tanstack/react-router";
import { employees } from "@/mock/data";
import { balanceFor } from "@/mock/leavebalance";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/leave/$id")({
  head: () => ({
    meta: [
      { title: "Leave decision — Kuubu HRM" },
      { name: "description", content: "Approve, return, reject or delegate a leave request with full policy context." },
      { property: "og:title", content: "Leave decision — Kuubu HRM" },
      { property: "og:description", content: "Approve, return, reject or delegate a leave request with full policy context." },
    ],
  }),
  component: LeaveDetail,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const mockStatus: Record<string, string> = {
  submitted: "Submitted",
  approved: "Approved",
  rejected: "Rejected",
  cancelled: "Cancelled",
  in_review: "In review",
};

function LeaveDetail() {
  const { id } = Route.useParams();

  // Real mode: load the list and pick the row matching this id.
  interface LeaveRow {
    id: string;
    workerId: string;
    workerName: string;
    leaveTypeCode: string;
    startDate: string;
    endDate: string;
    requestedDays: number;
    status: string;
    balanceReserved: boolean;
    crossesCutoff: boolean;
    createdAt: string;
  }

  const state = useApi<LeaveRow | null>(
    () =>
      realApi
        .leaveRequests({ page: 1, pageSize: 200 })
        .then((page) => {
          const raw = page.items.find((r) => String((r as { id?: unknown }).id) === id);
          return (raw as LeaveRow | null) ?? null;
        }),
    [id],
  );

  // Mock mode fallback (kept for green-UI development) — mapped into the
  // same LeaveRow shape so both branches satisfy the Async generic.
  const mockState = useMock(
    () =>
      api.leaveRequest(id).then((raw) => {
        if (!raw) return null as LeaveRow | null;
        const x = raw as unknown as Record<string, unknown>;
        return {
          id: String(x.id ?? ""),
          workerId: String(x.employeeId ?? ""),
          workerName: String(x.owner ?? ""),
          leaveTypeCode: String(x.type ?? ""),
          startDate: String(x.from ?? ""),
          endDate: String(x.to ?? ""),
          requestedDays: Number(x.days ?? 0),
          status: String(x.status ?? ""),
          balanceReserved: false,
          crossesCutoff: false,
          createdAt: "",
        } as LeaveRow | null;
      }),
    [id],
  );

  return (
    <AuthGate>
      <AppShell>
      <Async<LeaveRow | null> state={USE_REAL ? state : mockState} rows={3}>
        {(r) => {
          if (!r) return <RestrictedState />;
          const row = r as unknown as Record<string, unknown>;
          const backend = USE_REAL;
          const requestId = String(row.id ?? "");
          const employeeId = String(row.workerId ?? "");
          const type = String(row.leaveTypeCode ?? "");
          const from = String(row.startDate ?? "");
          const to = String(row.endDate ?? "");
          const days = Number(row.requestedDays ?? 0);
          const status = mockStatus[String(row.status ?? "")] ?? String(row.status ?? "");

          // Mock-row fields when not running against the real backend.
          const w = !backend ? employees.find((x) => x.id === String(row.employeeId ?? "")) : undefined;
          const bal = !backend && type === "Annual" ? balanceFor(String(row.employeeId ?? "")) : null;
          const m = r as unknown as {
            employeeId?: string;
            type?: string;
            from?: string;
            to?: string;
            days?: number;
            status: string;
            owner?: string;
            nextAction?: string;
            dueDate?: string;
            reason?: string;
            policy?: { label: string; outcome: string; detail?: string }[];
            conflicts?: string[];
            timeline?: { id?: unknown; at?: unknown; actor?: unknown; event?: unknown; detail?: unknown }[];
          };

          return (
            <RecordDetail
              reference={requestId}
              title={`${type} leave — ${w?.fullName ?? (backend ? String(row.workerName ?? "Unknown employee") : "Unknown employee")}`}
              subtitle={`${from} → ${to} · ${days} days`}
              status={m.status ?? status}
              owner={String(row.workerName ?? m.owner ?? "")}
              nextAction={`${m.nextAction ?? "Awaiting decision"}${m.dueDate ? ` · due ${m.dueDate}` : ""}`}
              summary={[
                { label: "Employee", value: w?.fullName ?? String(row.workerName ?? "—") },
                { label: "Job title", value: w?.jobTitle ?? "—" },
                { label: "Leave type", value: type },
                { label: "Days", value: days },
                {
                  label: "Balance if approved",
                  value: bal
                    ? `${Math.round((bal.available - days) * 10) / 10} days, from ${bal.available} now`
                    : backend
                      ? "Checked against live balance at decision time"
                      : "Not applicable",
                },
                { label: "Reason", value: String(row.reason ?? m.reason ?? "Not given") },
              ]}
              timeline={
                <StatusTimeline
                  title="History"
                  events={(m.timeline ?? []).map((t, i) => ({
                    id: String(t.id ?? `ev-${i}`),
                    at: String(t.at ?? ""),
                    actor: String(t.actor ?? ""),
                    event: String(t.event ?? ""),
                    detail: String(t.detail ?? ""),
                  }))}
                />
              }
            >
              <ApprovalPanel
                decisionSummary={`Decide ${days} days of ${type.toLowerCase()} leave for ${w?.fullName ?? String(row.workerName ?? "this employee")}.`}
                policy={
                  (Array.isArray(m.policy) ? m.policy.map((p) => ({
                    id: String(p.label),
                    label: String(p.label),
                    outcome: (String(p.outcome ?? "pass") as "pass" | "warn" | "fail"),
                    detail: String(p.detail ?? ""),
                  })) : []) as unknown as import("@/mock/types").PolicyResult[]
                }
                conflicts={m.conflicts ?? []}
                onDecision={
                  backend
                    ? async (d, reason) => {
                        await realApi.decideLeaveRequest(requestId, d, reason || undefined);
                        state.reload();
                      }
                    : (() => undefined)
                }
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
