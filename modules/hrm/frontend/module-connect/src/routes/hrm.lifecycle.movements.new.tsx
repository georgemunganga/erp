import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, ArrowRight, CalendarClock } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { employees } from "@/mock/data";
import { currentAssignment, lifecycleApi, movementsFor, TODAY } from "@/mock/lifecycle";
import type { MovementType } from "@/mock/lifecycle";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";

export const Route = createFileRoute("/hrm/lifecycle/movements/new")({
  head: () => ({
    meta: [
      { title: "Raise a movement — Kuubu HRM" },
      { name: "description", content: "Guided movement: purpose, current assignment, proposed change with an effective date, impact summary, review and submit." },
      { property: "og:title", content: "Raise a movement — Kuubu HRM" },
      { property: "og:description", content: "Submitting records a pending future change — nothing in the employment history is overwritten." },
    ],
  }),
  component: NewMovement,
});

const movementTypes: MovementType[] = ["Promotion", "Transfer", "Secondment", "Manager change"];

const branches = [
  "Lusaka HQ",
  "Ndola Plant",
  "Kitwe Depot",
  "Livingstone Works",
  "Chingola Office",
  "Solwezi Yard",
];

const grades = ["G2", "G4", "G5", "G6", "G7", "G8", "G9"];

function Baseline({ items }: { items: { label: string; value: string }[] }) {
  return (
    <dl className="grid max-w-3xl gap-3 sm:grid-cols-2">
      {items.map((i) => (
        <div key={i.label} className="rounded-md border bg-surface-muted px-3 py-2">
          <dt className="text-xs text-muted-foreground">{i.label}</dt>
          <dd className="text-sm font-medium">{i.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function NewMovement() {
  const navigate = useNavigate();
  const [reference, setReference] = useState<string | null>(null);
  const [attempted, setAttempted] = useState(false);

  const [employeeId, setEmployeeId] = useState("w-1001");
  const [type, setType] = useState<MovementType>("Promotion");
  const [reason, setReason] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState("");
  const [effectiveTo, setEffectiveTo] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [grade, setGrade] = useState("");
  const [branch, setBranch] = useState("");
  const [costCentre, setCostCentre] = useState("");
  const [managerId, setManagerId] = useState("");

  const employee = employees.find((e) => e.id === employeeId);
  const current = currentAssignment(employeeId);
  const history = movementsFor(employeeId);
  const dateMissing = effectiveFrom.trim() === "";

  const proposed = {
    jobTitle: jobTitle || current?.jobTitle || "",
    grade: grade || current?.grade || "",
    branch: branch || current?.branch || "",
    costCentre: costCentre || current?.costCentre || "",
    manager: managerId ? (employees.find((e) => e.id === managerId)?.fullName ?? "") : (current?.manager ?? ""),
  };

  /** The one fact that changes, by movement type — used in the impact and review steps. */
  const changeLine =
    type === "Promotion"
      ? `${current?.grade ?? "—"} · ${current?.jobTitle ?? "—"} → ${proposed.grade} · ${proposed.jobTitle}`
      : type === "Manager change"
        ? `${current?.manager ?? "—"} → ${proposed.manager}`
        : `${current?.branch ?? "—"} · ${current?.costCentre ?? "—"} → ${proposed.branch} · ${proposed.costCentre}`;

  const impacts: { area: string; summary: string; detail: string }[] = [
    {
      area: "Reporting line",
      summary: type === "Manager change" ? `Moves to ${proposed.manager}` : type === "Secondment" ? "Dotted line to the host branch" : "Unchanged",
      detail:
        type === "Manager change"
          ? "Approvals raised before the effective date stay with the current manager. Anything raised afterwards routes to the new one."
          : type === "Secondment"
            ? "The home manager stays accountable for the employment record; day-to-day direction comes from the host branch."
            : `Continues to report to ${current?.manager ?? "the current manager"}.`,
    },
    {
      area: "Payroll",
      summary:
        type === "Promotion"
          ? `Grade ${current?.grade ?? "—"} → ${proposed.grade}`
          : type === "Transfer"
            ? `Cost centre ${current?.costCentre ?? "—"} → ${proposed.costCentre}`
            : type === "Secondment"
              ? "Paid from the home entity, recharged to the host"
              : "No change",
      detail: dateMissing
        ? "Set an effective date to see which pay run this lands in."
        : effectiveFrom.endsWith("-01")
          ? `Effective ${effectiveFrom} — the first of the month, so no pro-rating is needed.`
          : `Effective ${effectiveFrom} — mid-period, so the pay run pro-rates across the change.`,
    },
    {
      area: "Access",
      summary:
        type === "Manager change"
          ? "Approval routing changes"
          : type === "Promotion"
            ? "Approval limits may increase"
            : "Site access changes",
      detail:
        type === "Manager change"
          ? "Leave, attendance and expense approvals re-point to the new manager on the effective date."
          : type === "Promotion"
            ? "Any change to approval limits is granted by the HR administrator after the movement is applied — not automatically."
            : `${proposed.branch} access is added on the effective date. ${type === "Secondment" ? "It expires automatically on the return date." : "Access to the previous site closes after a two-week handover."}`,
    },
    {
      area: "Position",
      summary: type === "Manager change" ? "Unchanged" : type === "Secondment" ? "Home position held open" : "New position required",
      detail:
        type === "Manager change"
          ? `${current?.positionId ?? "The position"} stays in place; only the reporting relationship changes.`
          : type === "Secondment"
            ? `${current?.positionId ?? "The home position"} is reserved for the return date and cannot be filled permanently.`
            : `HR operations confirms the receiving position is on the approved establishment before this can be approved.`,
    },
  ];

  const steps: FlowStep[] = [
    {
      id: "purpose",
      title: "Purpose",
      purpose: "Who is moving, what kind of change it is, and why. The type decides which checks and approvers apply.",
      render: () => (
        <div className="grid max-w-2xl gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="mov-employee">Employee</Label>
            <Select value={employeeId} onValueChange={setEmployeeId}>
              <SelectTrigger id="mov-employee" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {employees.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.fullName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="mov-type">Movement type</Label>
            <Select value={type} onValueChange={(v) => setType(v as MovementType)}>
              <SelectTrigger id="mov-type" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {movementTypes.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="mov-reason">Reason for the change</Label>
            <Textarea
              id="mov-reason"
              className="mt-1"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="What has changed in the work that justifies this movement?"
            />
            <p className="mt-1 text-xs text-muted-foreground">
              The reason is stored on the record and shown to whoever decides.
            </p>
          </div>
        </div>
      ),
    },
    {
      id: "current",
      title: "Current assignment",
      purpose: "The baseline this change is proposed against. Read-only — it is the employment record as it stands today.",
      render: () =>
        current ? (
          <div className="space-y-4">
            <Baseline
              items={[
                { label: "Employee", value: employee?.fullName ?? "—" },
                { label: "Employee number", value: employee?.employeeNo ?? "—" },
                { label: "Job title", value: current.jobTitle },
                { label: "Grade", value: current.grade },
                { label: "Department", value: current.department },
                { label: "Organisation", value: `${current.entity} · ${current.branch}` },
                { label: "Manager", value: current.manager },
                { label: "Position", value: `${current.positionId} · ${current.costCentre}` },
              ]}
            />
            <p className="text-xs text-muted-foreground">
              Baseline as at {TODAY}.{" "}
              {history.length === 1
                ? "One movement record already exists for this employee; it is not changed by this request."
                : history.length > 1
                  ? `${history.length} movement records already exist for this employee; none of them is changed by this request.`
                  : "No previous movements recorded for this employee."}
            </p>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">Choose an employee in step 1 to see the baseline.</p>
        ),
    },
    {
      id: "proposed",
      title: "Proposed change",
      purpose: "Only the facts that change. The effective date is required — it decides when this takes effect and which pay run it lands in.",
      render: () => (
        <div className="grid max-w-2xl gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="mov-effective">
              Effective from <span className="text-danger">(required)</span>
            </Label>
            <Input
              id="mov-effective"
              type="date"
              className="mt-1"
              value={effectiveFrom}
              onChange={(e) => setEffectiveFrom(e.target.value)}
              aria-describedby="mov-effective-help"
              aria-invalid={attempted && dateMissing}
            />
            <p id="mov-effective-help" className="mt-1 text-xs text-muted-foreground">
              The change is held as a pending future change until this date.
            </p>
            {attempted && dateMissing ? (
              <p className="mt-1 text-xs font-medium text-danger">An effective date is required before you can submit.</p>
            ) : null}
          </div>

          {type === "Secondment" ? (
            <div>
              <Label htmlFor="mov-return">Return date</Label>
              <Input
                id="mov-return"
                type="date"
                className="mt-1"
                value={effectiveTo}
                onChange={(e) => setEffectiveTo(e.target.value)}
              />
              <p className="mt-1 text-xs text-muted-foreground">Host access expires automatically on this date.</p>
            </div>
          ) : null}

          {type === "Promotion" ? (
            <>
              <div>
                <Label htmlFor="mov-title">Proposed job title</Label>
                <Input
                  id="mov-title"
                  className="mt-1"
                  value={jobTitle}
                  onChange={(e) => setJobTitle(e.target.value)}
                  placeholder={current?.jobTitle ?? ""}
                />
              </div>
              <div>
                <Label htmlFor="mov-grade">Proposed grade</Label>
                <Select value={grade} onValueChange={setGrade}>
                  <SelectTrigger id="mov-grade" className="mt-1">
                    <SelectValue placeholder={current?.grade ?? "Choose a grade"} />
                  </SelectTrigger>
                  <SelectContent>
                    {grades.map((g) => (
                      <SelectItem key={g} value={g}>
                        {g}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </>
          ) : null}

          {type === "Transfer" || type === "Secondment" ? (
            <>
              <div>
                <Label htmlFor="mov-branch">Proposed branch</Label>
                <Select value={branch} onValueChange={setBranch}>
                  <SelectTrigger id="mov-branch" className="mt-1">
                    <SelectValue placeholder={current?.branch ?? "Choose a branch"} />
                  </SelectTrigger>
                  <SelectContent>
                    {branches.map((b) => (
                      <SelectItem key={b} value={b}>
                        {b}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label htmlFor="mov-cost">Proposed cost centre</Label>
                <Input
                  id="mov-cost"
                  className="mt-1"
                  value={costCentre}
                  onChange={(e) => setCostCentre(e.target.value)}
                  placeholder={current?.costCentre ?? ""}
                />
              </div>
            </>
          ) : null}

          {type === "Manager change" ? (
            <div className="sm:col-span-2">
              <Label htmlFor="mov-manager">Proposed manager</Label>
              <Select value={managerId} onValueChange={setManagerId}>
                <SelectTrigger id="mov-manager" className="mt-1">
                  <SelectValue placeholder={current?.manager ?? "Choose a manager"} />
                </SelectTrigger>
                <SelectContent>
                  {employees
                    .filter((e) => e.id !== employeeId)
                    .map((e) => (
                      <SelectItem key={e.id} value={e.id}>
                        {e.fullName} — {e.jobTitle}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
          ) : null}
        </div>
      ),
    },
    {
      id: "impact",
      title: "Impact summary",
      purpose: "What this movement changes beyond the job record, so nothing is discovered after the effective date.",
      render: () => (
        <div className="space-y-3">
          <p className="inline-flex items-start gap-2 rounded-md border bg-surface-muted px-3 py-2 text-sm">
            <ArrowRight className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
            <span>{changeLine}</span>
          </p>
          <ul className="grid gap-3 sm:grid-cols-2">
            {impacts.map((i) => (
              <li key={i.area} className="rounded-md border bg-surface p-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{i.area}</p>
                <p className="mt-1 text-sm font-medium">{i.summary}</p>
                <p className="mt-1 text-xs text-muted-foreground">{i.detail}</p>
              </li>
            ))}
          </ul>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "Check the facts. Submitting records a pending future change — nothing in the employment history is overwritten.",
      render: () => (
        <div className="space-y-4">
          <dl className="grid max-w-3xl gap-3 sm:grid-cols-2">
            {[
              ["Employee", employee?.fullName ?? "—"],
              ["Movement type", type],
              ["Change", changeLine],
              ["Effective from", effectiveFrom || "Not set — required"],
              ["Return date", type === "Secondment" ? effectiveTo || "Open ended" : "Not applicable"],
              ["Reason", reason || "Not given"],
            ].map(([k, v]) => (
              <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="inline-flex items-start gap-2 rounded-md border border-info/30 bg-info-soft px-3 py-2 text-sm text-info">
            <CalendarClock className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>
              This is recorded as a pending future change. The current assignment stays in force until{" "}
              {effectiveFrom || "the effective date"}, and the previous record is kept in full.
            </span>
          </p>
          {attempted && dateMissing ? (
            <p className="inline-flex items-start gap-2 rounded-md border border-danger/30 bg-danger-soft px-3 py-2 text-sm text-danger">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>Go back to step 3 and set an effective date. Nothing has been submitted.</span>
            </p>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Lifecycle"
        title="Raise a movement"
        description="Five steps. The current assignment is read-only — you are proposing a dated change on top of it, not editing history."
      />
      <GuidedFlow
        flowId="movement-new"
        steps={steps}
        submitLabel="Submit movement"
        onSubmit={async () => {
          setAttempted(true);
          if (dateMissing) return;
          const r = await lifecycleApi.submitMovement({ employeeId, type, reason, effectiveFrom, effectiveTo, proposed });
          setReference(r.id);
        }}
        submitted={
          reference ? (
            <NextSteps
              reference={reference}
              title="Movement submitted as a pending future change"
              steps={[
                `HR operations checks the receiving position and the effective date of ${effectiveFrom}. The decision is due within five working days.`,
                "Once approved, the change is held against the record and applied automatically on the effective date — the current assignment stays in force until then.",
                "Payroll is notified so the change lands in the right pay run; a mid-period date is pro-rated.",
                "The previous assignment stays in the employment history in full. Nothing is overwritten.",
              ]}
              actions={
                <>
                  <Button onClick={() => navigate({ to: "/hrm/lifecycle/movements" })}>View all movements</Button>
                  <Button variant="outline" asChild>
                    <Link to="/hrm/lifecycle">Back to Lifecycle</Link>
                  </Button>
                </>
              }
            />
          ) : undefined
        }
      />
    </AppShell>
      </AuthGate>
  );
}
