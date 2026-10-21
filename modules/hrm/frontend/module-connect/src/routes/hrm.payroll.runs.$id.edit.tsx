import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Ban, Info, Lock, Plus, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { money } from "@/mock/payrollrun";
import type { RunLine } from "@/mock/payrollrun";
import { isEditableSource } from "@/mock/payrollrun";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { EditPage } from "@/platform/components/EditPage";
import type { EditSection } from "@/platform/components/EditPage";
import { RestrictedState } from "@/platform/components/States";
import { feedback } from "@/platform/feedback";
import { useAuth } from "@/platform/auth";
import { adaptPayrollLines, realApi, useApi } from "@/platform/use-api";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/payroll/runs/$id/edit")({
  head: () => ({
    meta: [
      { title: "Edit pay run — Newworldcargo HRM" },
      {
        name: "description",
        content:
          "Edit a pay run: period and dates, population, adjustments to individual pay lines, and the note the approver sees.",
      },
      { property: "og:title", content: "Edit pay run — Newworldcargo HRM" },
      {
        property: "og:description",
        content: "Edit period, population, pay-line adjustments and approver notes on a pay run.",
      },
    ],
  }),
  component: EditRun,
});

/** A display-only row of the live run — every figure comes from the backend. */
interface DisplayLine {
  id: string;
  employeeId: string;
  employee: string;
  components: NonNullable<RunLine["components"]>;
  gross: number;
  deductions: number;
  employerCost: number;
  net: number;
  flags: RunLine["flags"];
  isExcluded: boolean;
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/** Maps a backend PayrollRunDto into the small display shape this page needs. */
function adaptRunDisplay(raw: unknown): {
  id: string;
  period: string;
  payGroup: string;
  entityName: string;
  status: string;
  currency: string;
  dueDate: string;
  payPeriodId: string;
  payGroupId: string;
  approvalNote: string;
  excluded: { employee: string; reason: string }[];
} {
  const r = (raw ?? {}) as Record<string, unknown>;
  const excluded = Array.isArray(r.excluded) ? (r.excluded as Array<Record<string, unknown>>) : [];
  return {
    id: String(r.id ?? ""),
    period: String(r.periodLabel ?? ""),
    payGroup: String(r.payGroup ?? String(r.payGroupName ?? "")),
    entityName: String(r.entityName ?? String(r.legalEntityName ?? "")),
    status: String(r.status ?? "draft"),
    currency: String(r.currency ?? "ZMW"),
    dueDate: r.dueDate ? String(r.dueDate) : "",
    payPeriodId: String(r.payPeriodId ?? ""),
    payGroupId: String(r.payGroupId ?? ""),
    approvalNote: String(r.approvalNote ?? ""),
    excluded: excluded.map((x) => ({
      employee: String(x.employee ?? x.workerName ?? ""),
      reason: String(x.reason ?? x.exceptionReason ?? ""),
    })),
  };
}

type OptionRow = Record<string, unknown>;

const asArray = (raw: unknown): unknown[] =>
  Array.isArray(raw)
    ? raw
    : raw && typeof raw === "object" && "items" in raw
      ? ((raw as { items?: unknown[] }).items ?? [])
      : [];

const text = (value: unknown) => (value == null ? "" : String(value));

function DraftRunFields({
  values,
  setValue,
}: {
  values: Record<string, string>;
  setValue: (name: string, value: string) => void;
}) {
  const groups = useApi(realApi.payrollPayGroups, []);
  const periods = useApi(
    async () => (values.payGroupId ? realApi.payrollPayGroupPeriods(values.payGroupId) : []),
    [values.payGroupId],
  );
  const groupRows = asArray(groups.data) as OptionRow[];
  const periodRows = asArray(periods.data) as OptionRow[];

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <div>
        <Label htmlFor="payGroupId">Pay group</Label>
        <Select
          value={values.payGroupId || undefined}
          onValueChange={(value) => {
            setValue("payGroupId", value);
            setValue("payPeriodId", "");
          }}
        >
          <SelectTrigger id="payGroupId" className="mt-1">
            <SelectValue placeholder="Select pay group" />
          </SelectTrigger>
          <SelectContent>
            {groupRows.map((row) => (
              <SelectItem key={text(row.id)} value={text(row.id)}>
                {text(row.name || row.code || "Pay group")}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div>
        <Label htmlFor="payPeriodId">Pay period</Label>
        <Select
          value={values.payPeriodId || undefined}
          onValueChange={(value) => setValue("payPeriodId", value)}
        >
          <SelectTrigger id="payPeriodId" className="mt-1">
            <SelectValue
              placeholder={periods.loading ? "Loading periods..." : "Select pay period"}
            />
          </SelectTrigger>
          <SelectContent>
            {periodRows.map((row) => (
              <SelectItem key={text(row.id)} value={text(row.id)}>
                {text(row.periodLabel)} ({text(row.status)})
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {values.payGroupId && !periods.loading && periodRows.length === 0 ? (
          <p className="mt-1 text-xs text-warning">No periods are configured for this pay group.</p>
        ) : null}
      </div>

      <p className="flex gap-1.5 text-xs text-muted-foreground sm:col-span-2">
        <Info className="mt-0.5 size-3 shrink-0" aria-hidden />
        Draft runs can change pay group and period. After calculation starts, use line corrections
        instead.
      </p>
    </div>
  );
}

/* -------------------------------------------------------------------------- */

/** One employee's pay line, expanded so its components can be reviewed. */
function LineRow({
  line,
  currency,
  adjustment,
  onAdjust,
  locked,
}: {
  line: DisplayLine;
  currency: string;
  adjustment: { label: string; amount: string } | undefined;
  onAdjust: (v: { label: string; amount: string } | undefined) => void;
  locked: boolean;
}) {
  const [open, setOpen] = useState(false);
  const adjAmount = Number(adjustment?.amount ?? 0) || 0;
  const newNet = line.net + adjAmount;

  return (
    <li className="rounded-lg border bg-surface">
      <div className="flex flex-wrap items-start gap-3 p-3">
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
          className="min-w-0 flex-1 text-left"
        >
          <span className="block text-sm font-medium">{line.employee}</span>
          <span className="block text-xs text-muted-foreground">
            {line.employeeId} · {open ? "Hide" : "Show"} the {line.components.length} components
          </span>
        </button>

        <div className="shrink-0 text-right">
          <span className="tabular block text-sm font-medium">{money(newNet, currency)}</span>
          <span className="block text-xs text-muted-foreground">
            net{adjAmount ? ` (was ${money(line.net, currency)})` : ""}
          </span>
        </div>
      </div>

      {line.flags.length ? (
        <ul className="space-y-1 px-3 pb-2">
          {line.flags.map((f) => (
            <li key={f} className="flex gap-1.5 text-xs text-warning">
              <AlertTriangle className="mt-0.5 size-3 shrink-0" aria-hidden />
              {f}
            </li>
          ))}
        </ul>
      ) : null}

      {open ? (
        <div className="border-t px-3 py-3">
          <RecordTable variant="compact" className="w-full text-left text-xs">
            <caption className="sr-only">Pay components for {line.employee}</caption>
            <thead>
              <tr className="text-muted-foreground">
                <th scope="col" className="pb-1 font-medium">
                  Component
                </th>
                <th scope="col" className="pb-1 font-medium">
                  How it is calculated
                </th>
                <th scope="col" className="pb-1 text-right font-medium">
                  Amount
                </th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {line.components.map((c) => (
                <tr key={c.code}>
                  <th scope="row" className="py-1.5 font-normal">
                    <span className="font-medium">{c.label}</span>
                    {c.source === "Statutory" ? (
                      <span className="ml-1.5 inline-flex items-center gap-1 text-[11px] text-muted-foreground">
                        <Lock className="size-3 shrink-0" aria-hidden />
                        Statutory
                      </span>
                    ) : null}
                  </th>
                  <td className="py-1.5 text-muted-foreground">{c.basis}</td>
                  <td
                    className={`tabular py-1.5 text-right ${
                      c.kind === "Deduction" ? "text-muted-foreground" : ""
                    }`}
                  >
                    {c.kind === "Deduction" ? "−" : ""}
                    {money(c.amount, currency)}
                  </td>
                </tr>
              ))}
            </tbody>
          </RecordTable>

          <p className="mt-2 flex gap-1.5 text-xs text-muted-foreground">
            <Info className="mt-0.5 size-3 shrink-0" aria-hidden />A statutory line is recalculated
            from the country pack, never typed in. To change one, change the pack or the input it
            reads.
          </p>
        </div>
      ) : null}

      <div className="border-t px-3 py-3">
        {adjustment ? (
          <div className="flex flex-wrap items-end gap-2">
            <div className="min-w-0 flex-1">
              <label htmlFor={`adj-label-${line.employeeId}`} className="text-xs font-medium">
                One-off adjustment
              </label>
              <Input
                id={`adj-label-${line.employeeId}`}
                className="mt-1 h-8 text-sm"
                value={adjustment.label}
                placeholder="Why this employee is paid something extra or less"
                onChange={(e) => onAdjust({ ...adjustment, label: e.target.value })}
              />
            </div>
            <div className="w-32">
              <label htmlFor={`adj-amount-${line.employeeId}`} className="text-xs font-medium">
                Amount ({currency})
              </label>
              <Input
                id={`adj-amount-${line.employeeId}`}
                type="number"
                step="0.01"
                className="mt-1 h-8 text-sm"
                value={adjustment.amount}
                onChange={(e) => onAdjust({ ...adjustment, amount: e.target.value })}
              />
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="h-8 gap-1.5 text-xs"
              onClick={() => onAdjust(undefined)}
            >
              <Trash2 className="size-3.5" aria-hidden />
              Remove
            </Button>
          </div>
        ) : (
          <Button
            variant="outline"
            size="sm"
            className="h-8 gap-1.5 text-xs"
            disabled={locked}
            onClick={() => onAdjust({ label: "", amount: "" })}
          >
            <Plus className="size-3.5" aria-hidden />
            Add a one-off adjustment
          </Button>
        )}
        {locked ? (
          <p className="mt-1.5 flex gap-1.5 text-xs text-warning">
            <Lock className="mt-0.5 size-3 shrink-0" aria-hidden />
            This run is not in the calculated stage, so pay lines cannot be changed here.
          </p>
        ) : null}
      </div>
    </li>
  );
}

/* -------------------------------------------------------------------------- */

const lockedStatuses = new Set(["approved", "released", "paid", "closed", "reversed", "in-review"]);

function EditRun() {
  const { id } = Route.useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const runState = useApi(
    async () => adaptRunDisplay(USE_REAL ? await realApi.payrollRun(id) : null),
    [id, USE_REAL],
  );
  const linesState = useApi(
    async () =>
      USE_REAL ? adaptPayrollLines(await realApi.payrollRunLines(id), id) : ([] as RunLine[]),
    [id, USE_REAL],
  );

  const [adjustments, setAdjustments] = useState<Record<string, { label: string; amount: string }>>(
    {},
  );
  const [saving, setSaving] = useState(false);
  const [requestingCorrection, setRequestingCorrection] = useState(false);
  const [correctionRequestId, setCorrectionRequestId] = useState<string | null>(null);

  return (
    <AuthGate>
      <AppShell>
        <Async state={runState} rows={4}>
          {(run) => {
            if (!run) return <RestrictedState />;

            const isDraft = run.status === "draft";
            const canCorrectLines = run.status === "calculated";
            const roles = new Set((user?.roles ?? []).map((role) => role.toLowerCase()));
            const canManagePostedRun =
              roles.has("hr_admin") || roles.has("payroll") || roles.has("payroll_admin");
            const canUseLineCorrections = canCorrectLines && canManagePostedRun;
            const locked = !canUseLineCorrections || lockedStatuses.has(run.status);
            const lines: RunLine[] = USE_REAL ? (linesState.data ?? []) : [];
            const pageDescription = isDraft
              ? `${run.entityName}. Change the pay group or pay period before calculation.`
              : canUseLineCorrections
                ? `${run.entityName}. This run has already been calculated. Users with payroll correction access can add one-off employee corrections here.`
                : canCorrectLines
                  ? `${run.entityName}. This run has already been calculated, so changes now need payroll admin approval.`
                  : `${run.entityName}. This run is ${run.status}, so it is shown for review only.`;
            const requestAdminCorrection = async () => {
              if (requestingCorrection) return;
              setRequestingCorrection(true);
              try {
                const created = (await realApi.createExperienceRequest({
                  category: "payroll",
                  subject: `Payroll correction needed for ${run.period || run.id}`,
                  body: [
                    `Payroll run: ${run.id}`,
                    `Status: ${run.status}`,
                    `Pay group: ${run.payGroup || "Not shown"}`,
                    `Entity: ${run.entityName || "Not shown"}`,
                    "",
                    "Requested action:",
                    "Please review this posted payroll run and make the required admin correction, cancellation, or reversal.",
                  ].join("\n"),
                  confidentiality: "confidential",
                })) as { id?: unknown };
                const requestId = typeof created?.id === "string" ? created.id : null;
                setCorrectionRequestId(requestId);
                feedback.submitted(
                  "Payroll correction request sent",
                  "HR/admin can review it from HR requests and approvals.",
                );
              } catch (error) {
                feedback.blocked(
                  "Could not send the request",
                  error instanceof Error
                    ? error.message
                    : "The HR request could not be created. Try again from HR requests.",
                );
              } finally {
                setRequestingCorrection(false);
              }
            };

            const sections: EditSection[] = [
              ...(run.status === "draft"
                ? [
                    {
                      id: "draft",
                      title: "Draft setup",
                      description:
                        "Choose the payroll period and pay group before calculation starts.",
                      render: ({ values, setValue }) => (
                        <DraftRunFields values={values} setValue={setValue} />
                      ),
                    } satisfies EditSection,
                  ]
                : []),
              {
                id: "lines",
                title: canCorrectLines ? "Employee corrections" : "Pay lines",
                description: USE_REAL
                  ? canUseLineCorrections
                    ? "Add a one-off correction to an employee line when the calculated result needs a payroll adjustment."
                    : canCorrectLines
                      ? "This run is already calculated. If you do not have payroll correction access, send a request for HR/admin approval."
                      : "One line per employee, read from the calculated run. Open a line to see how its figures were derived."
                  : "Pay lines are shown from the demo data until onboarding of the run editor is complete.",
                render: () =>
                  USE_REAL ? (
                    <div className="space-y-3">
                      {canUseLineCorrections ? (
                        <Alert>
                          <Info className="size-4" aria-hidden />
                          <AlertTitle>Calculated run</AlertTitle>
                          <AlertDescription>
                            Pay group and pay period are locked now. Add corrections below only when
                            one employee needs a clear adjustment before approval.
                          </AlertDescription>
                        </Alert>
                      ) : canCorrectLines ? (
                        <Alert className="border-warning/40 bg-warning-soft">
                          <Lock className="size-4" aria-hidden />
                          <AlertTitle>Admin approval is needed</AlertTitle>
                          <AlertDescription>
                            This run is no longer a draft. You can review the lines, but changes
                            need a payroll admin or an approved correction request.
                            <div className="mt-3 flex flex-wrap gap-2">
                              <Button
                                type="button"
                                size="sm"
                                onClick={() => {
                                  void requestAdminCorrection();
                                }}
                                disabled={requestingCorrection || Boolean(correctionRequestId)}
                              >
                                {requestingCorrection
                                  ? "Sending request..."
                                  : correctionRequestId
                                    ? "Request sent"
                                    : "Request payroll admin correction"}
                              </Button>
                              {correctionRequestId ? (
                                <Button variant="outline" size="sm" asChild>
                                  <Link to="/hrm/requests/$id" params={{ id: correctionRequestId }}>
                                    Open request
                                  </Link>
                                </Button>
                              ) : null}
                            </div>
                          </AlertDescription>
                        </Alert>
                      ) : locked ? (
                        <Alert className="border-warning/40 bg-warning-soft">
                          <Lock className="size-4" aria-hidden />
                          <AlertTitle>This run cannot be edited here</AlertTitle>
                          <AlertDescription>
                            Current status: {run.status}. Use the correct payroll action from the
                            run page, or cancel/reverse it if a top admin needs to fix a mistake.
                            {!isDraft ? (
                              <div className="mt-3 flex flex-wrap gap-2">
                                <Button
                                  type="button"
                                  size="sm"
                                  onClick={() => {
                                    void requestAdminCorrection();
                                  }}
                                  disabled={requestingCorrection || Boolean(correctionRequestId)}
                                >
                                  {requestingCorrection
                                    ? "Sending request..."
                                    : correctionRequestId
                                      ? "Request sent"
                                      : "Request payroll admin correction"}
                                </Button>
                                {correctionRequestId ? (
                                  <Button variant="outline" size="sm" asChild>
                                    <Link
                                      to="/hrm/requests/$id"
                                      params={{ id: correctionRequestId }}
                                    >
                                      Open request
                                    </Link>
                                  </Button>
                                ) : null}
                              </div>
                            ) : null}
                          </AlertDescription>
                        </Alert>
                      ) : null}

                      <ul className="space-y-2">
                        {lines.map((l) => (
                          <LineRow
                            key={l.id}
                            line={{
                              id: l.id,
                              employeeId: l.employeeId,
                              employee: l.employee,
                              components: l.components,
                              gross: l.gross,
                              deductions: l.deductions,
                              employerCost: l.employerCost,
                              net: l.net,
                              flags: l.flags,
                              isExcluded: false,
                            }}
                            currency={run.currency}
                            locked={locked}
                            adjustment={adjustments[l.employeeId]}
                            onAdjust={(v) =>
                              setAdjustments((s) => {
                                const next = { ...s };
                                if (v) next[l.employeeId] = v;
                                else delete next[l.employeeId];
                                return next;
                              })
                            }
                          />
                        ))}
                      </ul>

                      {lines.length === 0 && !linesState.loading ? (
                        <p className="rounded-md border bg-surface-muted p-3 text-xs text-muted-foreground">
                          No pay lines yet — run the calculation first to populate the run.
                        </p>
                      ) : null}
                    </div>
                  ) : (
                    <p className="rounded-md border border-info/40 bg-info-soft p-3 text-xs text-info">
                      Demonstration data — nothing is saved. The editor is wired to the live run
                      when the backend is reachable.
                    </p>
                  ),
              },
              {
                id: "population",
                title: "Population",
                description: USE_REAL
                  ? "The run population is rebuilt from the pay group each time the run is recalculated. Excluding someone here is a record of intent for the next calculation, kept as a note for the approver."
                  : "Taking someone out of a run is a decision with a reason, not a deletion.",
                render: () => (
                  <div className="space-y-3">
                    <ul className="space-y-2">
                      {(USE_REAL ? lines : []).map((l) => (
                        <li
                          key={l.employeeId}
                          className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"
                        >
                          <span className="min-w-0 flex-1">
                            <span className="block text-sm font-medium">{l.employee}</span>
                            <span className="block text-xs text-muted-foreground">
                              {l.employeeId} · included in the calculated run
                            </span>
                          </span>
                          <Button variant="outline" size="sm" disabled>
                            <Ban className="size-3.5" aria-hidden />
                            Exclusion endpoint pending
                          </Button>
                        </li>
                      ))}
                    </ul>

                    {run.excluded.length ? (
                      <div className="rounded-md border bg-surface-muted p-3">
                        <p className="text-xs font-medium">Already excluded, with reasons</p>
                        <ul className="mt-1.5 space-y-1">
                          {run.excluded.map((x) => (
                            <li key={x.employee} className="flex gap-1.5 text-xs">
                              <Ban
                                className="mt-0.5 size-3 shrink-0 text-muted-foreground"
                                aria-hidden
                              />
                              <span>
                                <span className="font-medium">{x.employee}</span> — {x.reason}
                              </span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ) : null}

                    {USE_REAL ? (
                      <p className="flex gap-1.5 text-xs text-muted-foreground">
                        <Info className="mt-0.5 size-3 shrink-0" aria-hidden />
                        Payroll exclusion needs a dedicated backend action so it can be audited
                        separately. For now, remove a worker from the source payroll profile before
                        recalculating.
                      </p>
                    ) : null}
                  </div>
                ),
              },
              {
                id: "notes",
                title: "Notes for the approver",
                description:
                  "Whoever approves this run reads this first. Explain anything that moved.",
                fields: [
                  {
                    name: "approverNote",
                    label: "Note",
                    type: "textarea",
                    hint: "A material variance without an explanation will come straight back to you.",
                  },
                ],
              },
            ];

            return (
              <EditPage
                title={run.period ? `${run.period} — ${run.payGroup}` : `Run ${run.id}`}
                reference={run.id}
                description={pageDescription}
                sections={sections}
                initial={{
                  payGroupId: run.payGroupId,
                  payPeriodId: run.payPeriodId,
                  payGroup: run.payGroup,
                  cutoff: "",
                  payDate: "",
                  dueDate: run.dueDate,
                  approverNote: run.approvalNote,
                }}
                extraChanges={[
                  ...Object.entries(adjustments)
                    .filter(([, a]) => (Number(a.amount) || 0) !== 0 || a.label.trim())
                    .map(([key, a]) => {
                      if (key.startsWith("excl-")) {
                        const workerId = key.replace("excl-", "");
                        const who = lines.find((l) => l.employeeId === workerId);
                        return {
                          id: key,
                          label: "Marked for exclusion on next calculation",
                          detail: who?.employee ?? workerId,
                        };
                      }
                      const who = lines.find((l) => l.employeeId === key);
                      return {
                        id: `adj-${key}`,
                        label: `Adjustment for ${who?.employee ?? key}`,
                        detail: `${money(Number(a.amount) || 0, run.currency)}${
                          a.label.trim() ? ` — ${a.label.trim()}` : " — no reason given yet"
                        }`,
                      };
                    }),
                ]}
                saveLabel={isDraft ? "Save draft run" : "Save corrections"}
                footerNote={
                  isDraft
                    ? "Draft runs can change pay group, period and approver note. Calculate after saving."
                    : locked
                      ? "Only users with payroll correction access can change a posted run here. Others should send an HR/admin request."
                      : USE_REAL
                        ? "Corrections are saved on the calculated payroll line and recorded in payroll history."
                        : "Demonstration build — nothing is saved."
                }
                onCancel={() => navigate({ to: "/hrm/payroll/runs/$id", params: { id } })}
                onSave={async (_values, _changed) => {
                  if (!USE_REAL) {
                    feedback.saved(`${run.id} demo update — nothing was persisted.`);
                    navigate({ to: "/hrm/payroll/runs/$id", params: { id } });
                    return;
                  }
                  const corrections = Object.entries(adjustments)
                    .filter(([k]) => !k.startsWith("excl-"))
                    .map(([workerId, a]) => ({ workerId, ...a }));

                  const unexplained = corrections.filter((c) => !c.label.trim());
                  if (unexplained.length) {
                    feedback.blocked(
                      "Every adjustment needs a reason",
                      `${unexplained.length} one-off adjustment${
                        unexplained.length === 1 ? " has" : "s have"
                      } no explanation. An approver cannot sign off a figure nobody can account for.`,
                    );
                    return;
                  }

                  if (saving) return;
                  setSaving(true);
                  try {
                    if (run.status === "draft") {
                      if (!_values.payGroupId || !_values.payPeriodId) {
                        feedback.blocked(
                          "Payroll run cannot be saved",
                          "Choose a pay group and pay period before saving.",
                        );
                        return;
                      }
                      await realApi.updatePayrollRun(id, {
                        payGroupId: _values.payGroupId,
                        payPeriodId: _values.payPeriodId,
                        approvalNote: _values.approverNote || null,
                      });
                      feedback.saved("Draft payroll run updated.");
                      navigate({ to: "/hrm/payroll/runs/$id", params: { id } });
                      return;
                    }

                    let saved = 0;
                    let failed = 0;
                    for (const c of corrections) {
                      const line = lines.find((l) => l.employeeId === c.workerId);
                      if (!line) {
                        failed += 1;
                        continue;
                      }
                      // First non-statutory earning component takes the adjustment.
                      const target =
                        line.components.find(
                          (comp) => comp.source !== "Statutory" && comp.kind === "Earning",
                        ) ??
                        line.components.find((comp) => comp.source !== "Statutory") ??
                        line.components[0];
                      if (!target) {
                        failed += 1;
                        continue;
                      }
                      const adjustedAmount = target.amount + (Number(c.amount) || 0);
                      await realApi.payrollCorrection(
                        id,
                        line.id,
                        target.code,
                        adjustedAmount,
                        c.label.trim(),
                      );
                      saved += 1;
                    }
                    if (saved + failed === 0) {
                      feedback.note("No changes to save.");
                    } else if (failed) {
                      feedback.saved(
                        `${saved} correction${saved === 1 ? "" : "s"} saved; ${failed} could not be applied.`,
                        () => feedback.note("Adjustments reverted."),
                      );
                    } else {
                      feedback.saved(
                        `${saved} change${saved === 1 ? "" : "s"} saved — recalculate the run to pick them up.`,
                        () => feedback.note("Changes reverted."),
                      );
                    }
                    navigate({ to: "/hrm/payroll/runs/$id", params: { id } });
                  } catch (e) {
                    feedback.blocked(
                      "Could not save the run",
                      e instanceof Error ? e.message : "An unexpected error occurred while saving.",
                    );
                  } finally {
                    setSaving(false);
                  }
                }}
              />
            );
          }}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
