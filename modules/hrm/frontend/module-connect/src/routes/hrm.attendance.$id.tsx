import { createFileRoute } from "@tanstack/react-router";
import { employees } from "@/mock/data";
import { api } from "@/mock/service";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { ApprovalPanel } from "@/platform/components/ApprovalPanel";
import { RecordDetail } from "@/platform/components/RecordDetail";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/attendance/$id")({
  head: () => ({
    meta: [
      { title: "Attendance correction — Kuubu HRM" },
      { name: "description", content: "Compare recorded and claimed time, then approve, return or reject with a reason." },
      { property: "og:title", content: "Attendance correction — Kuubu HRM" },
      { property: "og:description", content: "Compare recorded and claimed time, then approve, return or reject with a reason." },
    ],
  }),
  component: AttendanceDetail,
});

function AttendanceDetail() {
  const { id } = Route.useParams();
  const state = useMock(() => api.attendanceItem(id), [id]);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {(r) => {
          if (!r) return <RestrictedState />;
          const w = employees.find((x) => x.id === r.employeeId);
          return (
            <RecordDetail
              reference={r.id}
              title={`Attendance correction — ${w?.fullName ?? "Unknown employee"}`}
              subtitle={`${r.date} · claimed ${r.claimedIn}–${r.claimedOut}`}
              status={r.status}
              owner={r.owner}
              nextAction={`${r.nextAction} · due ${r.dueDate}`}
              summary={[
                { label: "Employee", value: w?.fullName },
                { label: "Job title", value: w?.jobTitle },
                { label: "Date", value: r.date },
                { label: "Recorded", value: `${r.recordedIn ?? "Not captured"} – ${r.recordedOut ?? "Not captured"}` },
                { label: "Claimed", value: `${r.claimedIn} – ${r.claimedOut}` },
                { label: "Reason", value: r.reason },
              ]}
              timeline={<StatusTimeline title="History" events={r.timeline} />}
            >
              <ApprovalPanel
                decisionSummary={`Approve the claimed hours ${r.claimedIn}–${r.claimedOut} for ${w?.fullName ?? "this employee"} on ${r.date}, replacing the recorded value.`}
                policy={[]}
                conflicts={
                  !r.recordedOut
                    ? [`No clock-out was captured by the system — this correction relies entirely on the employee's account and any attached evidence.`]
                    : []
                }
                evidence={r.timeline.filter((t) => t.evidence).map((t) => t.evidence!)}
                onDecision={() => undefined}
              />
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
