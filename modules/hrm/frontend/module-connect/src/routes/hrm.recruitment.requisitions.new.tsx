import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, CheckCircle2, Info } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { entities } from "@/mock/data";
import {
  demoEntityTree,
  flattenEntityTree,
  treePathLabel,
  treeToSelectOptions,
  type OrgTreeNode,
} from "@/platform/orgTree";
import { approversFor, checkEstablishment, grades, money, recruitmentApi } from "@/mock/recruitment";
import type { RequisitionReason } from "@/mock/recruitment";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { realApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";
export const Route = createFileRoute("/hrm/recruitment/requisitions/new")({
  head: () => ({
    meta: [
      { title: "Raise a requisition — Kuubu HRM" },
      {
        name: "description",
        content:
          "Guided requisition: replacement or new position, role details, establishment and budget check, approvers, then review and submit.",
      },
      { property: "og:title", content: "Raise a requisition — Kuubu HRM" },
      {
        property: "og:description",
        content: "Guided requisition with an establishment check before anyone spends time on approval.",
      },
    ],
  }),
  component: NewRequisition,
});

const departments = ["Operations", "Manufacturing", "Logistics", "Finance", "People"];
const employmentTypes = ["Permanent", "Fixed term", "Contractor", "Intern", "Part time"];

function NewRequisition() {
  const navigate = useNavigate();
  const [ref, setRef] = useState<string | null>(null);
  const [unitMap, setUnitMap] = useState<Record<string, string>>({});
  if (USE_REAL && Object.keys(unitMap).length === 0) {
    realApi.orgUnits().then((units) => {
      const map: Record<string, string> = {};
      for (const u of (units ?? []) as Record<string, unknown>[]) {
        map[String(u.name ?? "").toLowerCase()] = String(u.id ?? "");
      }
      setUnitMap(map);
    }).catch(() => undefined);
  }

  const [reason, setReason] = useState<RequisitionReason>("Replacement");
  const [replacementFor, setReplacementFor] = useState("");
  const [jobTitle, setJobTitle] = useState("Maintenance Technician");
  const [grade, setGrade] = useState("G5");
  const [entityId, setEntityId] = useState("ent-zm1");
  const [branch, setBranch] = useState("Lusaka HQ");
  const [department, setDepartment] = useState("Operations");

  // Tree-driven placement options: legal entity › branch/department paths.
  const [placementTree, setPlacementTree] = useState<OrgTreeNode[] | null>(null);
  if (USE_REAL && !placementTree) {
    realApi.entityTree().then((t) => setPlacementTree((t ?? []) as OrgTreeNode[])).catch(() => undefined);
  }
  const placementUnits = flattenEntityTree(placementTree ?? demoEntityTree);
  const placementOptions = treeToSelectOptions(placementTree ?? demoEntityTree);
  const choosePlacement = (value: string) => {
    const match = placementUnits.find(
      (p) => (p.unitType === "entity" ? `entity:${p.entityId}` : p.unitId) === value,
    );
    if (match) {
      if (match.unitType === "entity") {
        setEntityId(match.entityId);
        setBranch(match.path[1] ?? "");
        setDepartment(match.path[1] ?? "Operations");
      } else {
        setEntityId(match.entityId);
        setBranch(match.path[1] ?? "");
        setDepartment(match.unitName);
      }
    }
  };
  const [employmentType, setEmploymentType] = useState("Permanent");
  const [headcount, setHeadcount] = useState(1);
  const [targetStart, setTargetStart] = useState("2026-10-01");
  const [justification, setJustification] = useState("");
  const [budgetSource, setBudgetSource] = useState("Departmental opex — like-for-like replacement");
  const [annualCost, setAnnualCost] = useState(52000);

  const entity = entities.find((e) => e.id === entityId) ?? entities[0];
  const currency = entity.country === "Zambia" ? "ZMW" : "ZMW";
  const establishment = checkEstablishment({ department, branch, headcount });
  const approvers = approversFor({ reason, within: establishment.within, entityId });

  const chooseEntity = (id: string) => {
    setEntityId(id);
    const next = entities.find((e) => e.id === id);
    if (next && !next.branches.includes(branch)) setBranch(next.branches[0]);
  };

  const steps: FlowStep[] = [
    {
      id: "purpose",
      title: "Why is this post needed?",
      purpose:
        "A replacement follows the post that already exists. A new position changes the establishment, so it needs a Finance Director decision.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <RadioGroup
            value={reason}
            onValueChange={(v) => setReason(v as RequisitionReason)}
            aria-label="Reason for the requisition"
          >
            {(
              [
                {
                  value: "Replacement",
                  title: "Replacement for an existing post",
                  body: "Someone has left, retired or moved on and the approved post is now vacant. No change to the establishment.",
                },
                {
                  value: "New position",
                  title: "New position",
                  body: "The post does not exist yet. You will need a business case and a Finance Director decision, whatever the budget says.",
                },
              ] as const
            ).map((o) => (
              <label
                key={o.value}
                htmlFor={`reason-${o.value}`}
                className={`flex cursor-pointer gap-3 rounded-md border p-3 transition-colors ${
                  reason === o.value ? "border-primary bg-primary-soft" : "hover:border-border-strong"
                }`}
              >
                <RadioGroupItem id={`reason-${o.value}`} value={o.value} className="mt-1" />
                <span>
                  <span className="block text-sm font-medium">{o.title}</span>
                  <span className="block text-sm text-muted-foreground">{o.body}</span>
                </span>
              </label>
            ))}
          </RadioGroup>

          {reason === "Replacement" ? (
            <div>
              <Label htmlFor="replacement-for">Who is being replaced, and when do they leave?</Label>
              <Input
                id="replacement-for"
                className="mt-1"
                value={replacementFor}
                onChange={(e) => setReplacementFor(e.target.value)}
                placeholder="For example: Bart Hendriks — resigned, last day 21 August 2026"
              />
              <p className="mt-1 text-xs text-muted-foreground">
                This is checked against the leavers list so the establishment stays accurate.
              </p>
            </div>
          ) : null}
        </div>
      ),
    },
    {
      id: "role",
      title: "Role details",
      purpose: "The facts that decide the grade, the approvers and where the post sits in the organisation.",
      render: () => (
        <div className="grid max-w-2xl gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Label htmlFor="job-title">Job title</Label>
            <Input id="job-title" className="mt-1" value={jobTitle} onChange={(e) => setJobTitle(e.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="placement">Placement — entity › branch › department</Label>
            <Select value={placementOptions.find((o) => o.value === `entity:${entityId}` || o.value === (placementUnits.find((p) => p.unitName === department && p.unitType !== "entity")?.unitId ?? ""))?.value ?? `entity:${entityId}`} onValueChange={choosePlacement}>
              <SelectTrigger id="placement" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {placementOptions.map((o) => (
                  <SelectItem
                    key={o.value}
                    value={o.value}
                    className={o.value.startsWith("entity:") ? "font-semibold text-primary" : undefined}
                  >
                    {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="mt-1 text-xs text-muted-foreground">
              The post will be created under the selected branch or department within the organisation
              tree.
            </p>
          </div>
          <div>
            <Label htmlFor="grade">Grade</Label>
            <Select value={grade} onValueChange={setGrade}>
              <SelectTrigger id="grade" className="mt-1">
                <SelectValue />
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
          <div>
            <Label htmlFor="employment-type">Employment type</Label>
            <Select value={employmentType} onValueChange={setEmploymentType}>
              <SelectTrigger id="employment-type" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {employmentTypes.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="headcount">Headcount</Label>
            <Input
              id="headcount"
              type="number"
              min={1}
              max={20}
              className="mt-1"
              value={headcount}
              onChange={(e) => setHeadcount(Math.max(1, Number(e.target.value) || 1))}
              aria-describedby="headcount-help"
            />
            <p id="headcount-help" className="mt-1 text-xs text-muted-foreground">
              How many people you need in this same role, at this same grade and branch.
            </p>
          </div>
          <div>
            <Label htmlFor="target-start">Target start date</Label>
            <Input
              id="target-start"
              type="date"
              className="mt-1"
              value={targetStart}
              onChange={(e) => setTargetStart(e.target.value)}
            />
          </div>
        </div>
      ),
    },
    {
      id: "justification",
      title: "Justification and budget",
      purpose: "The establishment check runs here, before an approver spends time on the request.",
      render: () => (
        <div className="max-w-2xl space-y-5">
          <div
            className={`rounded-md border p-4 ${
              establishment.within ? "border-success/40 bg-success-soft" : "border-warning/40 bg-warning-soft"
            }`}
          >
            <h3 className="flex items-center gap-2 text-sm font-semibold">
              {establishment.within ? (
                <CheckCircle2 className="size-4 text-success" aria-hidden />
              ) : (
                <AlertTriangle className="size-4 text-warning" aria-hidden />
              )}
              Establishment check — {establishment.within ? "within establishment" : "over establishment"}
            </h3>
            <p className="mt-1 text-sm">{establishment.detail}</p>
            <dl className="mt-3 grid gap-3 sm:grid-cols-4">
              {[
                ["Approved posts", establishment.approvedPosts],
                ["Filled", establishment.filledPosts],
                ["Vacant", establishment.vacantPosts],
                ["Requested", establishment.requested],
              ].map(([k, v]) => (
                <div key={String(k)} className="rounded-md border bg-surface px-3 py-2">
                  <dt className="text-xs text-muted-foreground">{k}</dt>
                  <dd className="tabular text-sm font-medium">{v}</dd>
                </div>
              ))}
            </dl>
          </div>

          <div>
            <Label htmlFor="justification">Business case</Label>
            <Textarea
              id="justification"
              className="mt-1"
              rows={4}
              value={justification}
              onChange={(e) => setJustification(e.target.value)}
              placeholder={
                reason === "Replacement"
                  ? "What stops working if this post stays empty? Name the service, the cover arrangement and its cost."
                  : "What changed to create this post? Give the evidence of demand and the payback."
              }
              aria-describedby="justification-help"
            />
            <p id="justification-help" className="mt-1 text-xs text-muted-foreground">
              Approvers read this first. Requisitions returned for more information are almost always missing evidence
              here.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="budget-source">Budget source</Label>
              <Input
                id="budget-source"
                className="mt-1"
                value={budgetSource}
                onChange={(e) => setBudgetSource(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="annual-cost">Annual cost per post ({currency})</Label>
              <Input
                id="annual-cost"
                type="number"
                min={0}
                step={100}
                className="mt-1"
                value={annualCost}
                onChange={(e) => setAnnualCost(Number(e.target.value) || 0)}
              />
            </div>
          </div>

          <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>
              Total annual cost for {headcount} {headcount === 1 ? "post" : "posts"}:{" "}
              <span className="font-medium">{money(annualCost * headcount, currency)}</span>. Employer on-costs are added
              by Finance during approval.
            </span>
          </p>
        </div>
      ),
    },
    {
      id: "approvers",
      title: "Who will decide",
      purpose: "The approval route is set by the reason, the entity and the establishment result. You cannot change it.",
      render: () => (
        <div className="max-w-2xl space-y-4">
          <ol className="space-y-2">
            {approvers.map((a) => (
              <li key={a.step} className="flex gap-3 rounded-md border bg-surface-muted p-3">
                <span
                  aria-hidden
                  className="mt-0.5 grid size-6 shrink-0 place-items-center rounded-full border border-border-strong text-xs"
                >
                  {a.step}
                </span>
                <span className="min-w-0">
                  <span className="block text-sm font-medium">{a.name}</span>
                  <span className="block text-sm text-muted-foreground">{a.role}</span>
                  <span className="block text-xs text-muted-foreground">Service standard: {a.sla}</span>
                </span>
              </li>
            ))}
          </ol>
          {!establishment.within || reason === "New position" ? (
            <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm">
              <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
              <span>
                A Finance Director step has been added because this requisition is{" "}
                {reason === "New position" ? "a new position" : "over establishment"}. Expect five extra working days.
              </span>
            </p>
          ) : (
            <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
              <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
              <span>
                Three steps, so a decision is normally reached within seven working days of submission.
              </span>
            </p>
          )}
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "Check the facts. Submitting sends this to the first approver and starts the service standard clock.",
      render: () => (
        <div className="max-w-2xl space-y-4">
          <dl className="grid gap-3 sm:grid-cols-2">
            {(
              [
                ["Reason", reason === "Replacement" ? `Replacement — ${replacementFor || "leaver not named"}` : "New position"],
                ["Job title", jobTitle],
                ["Grade", grade],
                ["Employment type", employmentType],
                ["Legal entity", entity.name],
                ["Branch", branch],
                ["Department", department],
                ["Headcount", String(headcount)],
                ["Target start date", targetStart],
                ["Establishment", establishment.within ? "Within establishment" : "Over establishment"],
                ["Budget source", budgetSource],
                ["Total annual cost", money(annualCost * headcount, currency)],
                ["First approver", approvers[0]?.name ?? "Not set"],
                ["Approval steps", String(approvers.length)],
              ] as const
            ).map(([k, v]) => (
              <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>
          {justification.trim().length < 20 ? (
            <p className="flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm">
              <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
              <span>
                The business case is short. You can still submit, but requisitions without evidence are usually returned.
              </span>
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
        eyebrow="Recruitment"
        title="Raise a requisition"
        description="Five short steps. The establishment check runs before you submit, so you know the approval route in advance. Your draft saves as you go."
      />
      <GuidedFlow
        flowId="requisition-new"
        steps={steps}
        submitLabel="Submit requisition"
        onSubmit={async () => {
          if (USE_REAL) {
            const orgUnitId =
              unitMap[department.toLowerCase()] ??
              placementUnits.find((p) => p.unitName === department && p.unitType !== "entity")?.unitId ??
              null;
            const result = await realApi.createRequisition({
              jobTitle: jobTitle.trim(),
              reason: reason === "Replacement" ? "replacement" : "new",
              headcount,
              grade: grade.trim() || null,
              orgUnitId,
              locationId: null,
              hiringManagerName: null,
              budgetAnnual: annualCost > 0 ? annualCost * headcount : null,
              currency,
              businessCase: justification.trim() || `${reason} requisition for ${department}`,
              approverName: approvers[0]?.name ?? null,
            });
            const created = result as Record<string, unknown>;
            setRef(String(created.requisitionNo ?? created.id ?? "draft"));
            feedback.submitted(
              "Requisition submitted",
              `The requisition for ${jobTitle.trim()} is saved as a draft. Open it from the requisitions list to submit for approval.`,
            );
            navigate({ to: "/hrm/recruitment/requisitions" }).catch(() => undefined);
            return;
          }
          const r = await recruitmentApi.submitRequisition({
            reason,
            replacementFor,
            jobTitle,
            grade,
            entityId,
            branch,
            department,
            employmentType,
            headcount,
            targetStart,
            justification,
            budgetSource,
            annualCost,
          });
          setRef(r.id);
        }}
        submitted={
          ref ? (
            <NextSteps
              reference={ref}
              title="Requisition submitted"
              steps={[
                `${approvers[0]?.name ?? "Your approver"} has ${approvers[0]?.sla ?? "two working days"} to make the first decision.`,
                `There ${approvers.length === 1 ? "is" : "are"} ${approvers.length} approval ${approvers.length === 1 ? "step" : "steps"} in total. You'll be told at each one, and if it's returned you'll get the reason and what to change.`,
                establishment.within
                  ? "Once approved, Talent Acquisition creates the vacancy and advertises internally for ten working days before going external."
                  : "Because this is over establishment, the Finance Director decides last. If approved, the establishment is amended before the vacancy is created.",
                "Nothing is advertised and no candidate data is collected until the requisition is approved.",
              ]}
              actions={
                <>
                  <Button onClick={() => navigate({ to: "/hrm/recruitment/requisitions" })}>View requisitions</Button>
                  <Button variant="outline" asChild>
                    <Link to="/hrm/recruitment/vacancies">Go to vacancies</Link>
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
