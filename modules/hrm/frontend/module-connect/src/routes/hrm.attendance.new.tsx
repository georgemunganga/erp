import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { realApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/attendance/new")({
  head: () => ({
    meta: [
      { title: "Raise a correction — Kuubu HRM" },
      { name: "description", content: "Guided attendance correction: the day, the issue, the proposed fix and its impact." },
      { property: "og:title", content: "Raise a correction — Kuubu HRM" },
      { property: "og:description", content: "Guided attendance correction with a preview of the payroll impact before you submit." },
    ],
  }),
  component: NewCorrection,
});

const issues = [
  "Missing punch",
  "Wrong time recorded",
  "Wrong shift assigned",
  "Off-site or field duty",
  "System or reader failure",
  "Other",
] as const;

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
const issueToBackend: Record<string, string> = {
  "Missing punch": "missed-punch",
  "Wrong time recorded": "wrong-time",
  "Wrong shift assigned": "wrong-shift",
  "Off-site or field duty": "off-site",
  "System or reader failure": "system-failure",
};

function NewCorrection() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [date, setDate] = useState("2026-07-28");
  const [issue, setIssue] = useState<string>(issues[0]);
  const [claimedIn, setClaimedIn] = useState("07:00");
  const [claimedOut, setClaimedOut] = useState("16:00");
  const [reason, setReason] = useState("");

  const recordedIn = "06:58";
  const recordedOut = "—";
  const overtimeImpact = useMemo(() => {
    const h = Number(claimedOut.split(":")[0]);
    return h >= 17 ? "Adds 1.0h of approval-required overtime." : "No overtime impact.";
  }, [claimedOut]);

  const steps: FlowStep[] = [
    {
      id: "day",
      title: "Select the day and issue",
      purpose: "The recorded clock data for this day, and what's actually wrong with it.",
      render: () => (
        <div className="max-w-lg space-y-4">
          <div>
            <Label htmlFor="date">Affected day</Label>
            <Input id="date" type="date" className="mt-1" value={date} onChange={(e) => setDate(e.target.value)} />
          </div>
          <div className="grid gap-2 rounded-md border bg-surface-muted px-3 py-2 text-sm">
            <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Recorded (source: badge reader, Gate 3)</span>
            <span className="tabular">{recordedIn} – {recordedOut}</span>
          </div>
          <div>
            <Label htmlFor="issue">What's wrong</Label>
            <Select value={issue} onValueChange={setIssue}>
              <SelectTrigger id="issue" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent>
                {issues.map((i) => <SelectItem key={i} value={i}>{i}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </div>
      ),
    },
    {
      id: "propose",
      title: "Propose the correction",
      purpose: "What actually happened, and why. Evidence speeds up the decision but isn't always required.",
      render: () => (
        <div className="grid max-w-lg gap-4 sm:grid-cols-2">
          <div><Label htmlFor="in">Claimed clock-in</Label><Input id="in" type="time" className="mt-1" value={claimedIn} onChange={(e) => setClaimedIn(e.target.value)} /></div>
          <div><Label htmlFor="out">Claimed clock-out</Label><Input id="out" type="time" className="mt-1" value={claimedOut} onChange={(e) => setClaimedOut(e.target.value)} /></div>
          <div className="sm:col-span-2">
            <Label htmlFor="reason">Reason</Label>
            <Textarea id="reason" className="mt-1" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. badge reader failed at end of shift" />
          </div>
          <div className="rounded-md border border-dashed p-4 text-center text-xs text-muted-foreground sm:col-span-2">
            Attach evidence (mock upload — nothing is stored).
          </div>
        </div>
      ),
    },
    {
      id: "impact",
      title: "Preview impact",
      purpose: "What this correction changes before anyone signs off on it.",
      render: () => (
        <ul className="max-w-lg space-y-2 text-sm">
          <li className="flex gap-2"><CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-hidden /><span>Corrected hours: <span className="font-medium tabular">{claimedIn}–{claimedOut}</span></span></li>
          <li className="flex gap-2"><AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden /><span>{overtimeImpact}</span></li>
          <li className="flex gap-2"><AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden /><span>This date falls inside the current payroll cutoff — approved late may push the change to the next pay run.</span></li>
        </ul>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "Check the facts. Submitting sends this to your manager, then HR operations, for a decision.",
      render: () => (
        <dl className="grid max-w-lg gap-3 sm:grid-cols-2">
          {[["Date", date], ["Issue", issue], ["Claimed in", claimedIn], ["Claimed out", claimedOut], ["Approver", "Mutale Kabwe (Manager)"], ["Reason", reason || "Not given"]].map(([k, v]) => (
            <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
              <dt className="text-xs text-muted-foreground">{k}</dt>
              <dd className="text-sm font-medium">{v}</dd>
            </div>
          ))}
        </dl>
      ),
    },
  ];

  return (
    <AuthGate>
      <AppShell>
      <PageHeader eyebrow="Attendance" title="Raise a correction" description="Four short steps. Review the recorded day before submitting." />
      <GuidedFlow
        flowId="attendance-new"
        steps={steps}
        submitLabel="Submit correction"
        onSubmit={async () => {
          if (USE_REAL) {
            const r = await realApi.createMyCorrection({
              workerId: "00000000-0000-0000-0000-000000000000",
              workDate: date,
              issueType: issueToBackend[issue] ?? "other",
              proposedClockIn: claimedIn || undefined,
              proposedClockOut: claimedOut || undefined,
              reason,
            });
            setRef(String((r as { id?: unknown }).id ?? ""));
            return;
          }
          setRef(`mock-${Date.now().toString(36)}`);
        }}
        submitted={
          ref ? (
            <NextSteps
              reference={`AT-${ref}`}
              title="Correction submitted"
              steps={[
                "Your manager, Mutale Kabwe, reviews it first, then HR operations verifies against source records.",
                "You'll be notified if it's approved, returned for more information, or rejected with a reason.",
                "Approved corrections are included automatically in the next pay run if submitted before cutoff.",
              ]}
              actions={<><Button onClick={() => navigate({ to: "/hrm/attendance" })}>View corrections</Button><Button variant="outline" asChild><Link to="/hrm">Back to Home</Link></Button></>}
            />
          ) : undefined
        }
      />
    </AppShell>
      </AuthGate>
  );
}
