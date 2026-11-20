import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Check, HeartPulse, Phone, ShieldCheck, TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { api } from "@/mock/service";
import { relationsApi } from "@/mock/relations";
import type { Incident } from "@/mock/relations";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { GuidedFlow, NextSteps } from "@/platform/components/GuidedFlow";
import type { FlowStep } from "@/platform/components/GuidedFlow";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/relations/safety")({
  head: () => ({
    meta: [
      { title: "Health and safety — Kuubu HRM" },
      { name: "description", content: "Report an incident or hazard, and track investigations and corrective actions." },
      { property: "og:title", content: "Health and safety — Kuubu HRM" },
      { property: "og:description", content: "Report an incident or hazard, and track investigations and corrective actions." },
    ],
  }),
  component: SafetyPage,
});

const severityWord = { Minor: "Minor", Moderate: "Moderate", Serious: "Serious" } as const;

function ReportFlow({ onDone }: { onDone: (ref: string) => void }) {
  const [kind, setKind] = useState("Near miss");
  const [what, setWhat] = useState("");
  const [where, setWhere] = useState("Lusaka HQ");
  const [when, setWhen] = useState("2026-07-29");
  const [anyoneHurt, setAnyoneHurt] = useState("no");

  const steps: FlowStep[] = [
    {
      id: "safety-first",
      title: "Is anyone hurt or in danger right now?",
      purpose: "Deal with the situation first. This form is not monitored in real time.",
      render: () => (
        <div className="max-w-xl space-y-4">
          <div className="rounded-lg border border-danger/40 bg-danger-soft p-4">
            <p className="flex items-start gap-2 text-sm font-medium text-danger">
              <Phone className="mt-0.5 size-4 shrink-0" aria-hidden />
              If someone is injured or in immediate danger, stop and act now
            </p>
            <ul className="mt-2 list-inside list-disc space-y-1 text-sm text-foreground">
              <li>Make the area safe if you can do so without putting yourself at risk.</li>
              <li>Call the site first aider or emergency services.</li>
              <li>Tell the shift supervisor.</li>
              <li>Come back and fill this in afterwards — reporting can wait, treatment cannot.</li>
            </ul>
          </div>
          <div>
            <Label htmlFor="hurt">Has anyone been injured?</Label>
            <Select value={anyoneHurt} onValueChange={setAnyoneHurt}>
              <SelectTrigger id="hurt" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="no">No — nobody was hurt</SelectItem>
                <SelectItem value="treated">Yes — first aid given, no further treatment</SelectItem>
                <SelectItem value="medical">Yes — needed medical attention</SelectItem>
              </SelectContent>
            </Select>
            {anyoneHurt === "medical" ? (
              <p className="mt-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-xs text-warning">
                An injury needing medical attention may be externally reportable. Health and safety
                will assess that — you do not need to work it out yourself.
              </p>
            ) : null}
          </div>
        </div>
      ),
    },
    {
      id: "what",
      title: "What happened",
      purpose: "Facts only. Someone else will decide the category and whether it is reportable.",
      render: () => (
        <div className="grid max-w-xl gap-4 sm:grid-cols-2">
          <div>
            <Label htmlFor="kind">Type</Label>
            <Select value={kind} onValueChange={setKind}>
              <SelectTrigger id="kind" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent>
                {["Injury", "Near miss", "Hazard", "Work-related illness"].map((k) => (
                  <SelectItem key={k} value={k}>{k}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="when">When</Label>
            <Input id="when" type="date" className="mt-1" value={when} onChange={(e) => setWhen(e.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="where">Where</Label>
            <Select value={where} onValueChange={setWhere}>
              <SelectTrigger id="where" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent>
                {["Lusaka HQ", "Ndola Plant", "Kitwe Depot", "Chingola Office", "Solwezi Yard", "Livingstone Works"].map((l) => (
                  <SelectItem key={l} value={l}>{l}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="sm:col-span-2">
            <Label htmlFor="what">What happened</Label>
            <Textarea id="what" rows={5} className="mt-1" value={what} onChange={(e) => setWhat(e.target.value)} placeholder="What you saw, in your own words" />
            <p className="mt-1 text-xs text-muted-foreground">
              Do not record anyone's medical details here. Health and safety will record the
              fitness-to-work outcome separately, and only the outcome.
            </p>
          </div>
        </div>
      ),
    },
    {
      id: "review",
      title: "Review and submit",
      purpose: "What happens once you send this.",
      render: () => (
        <div className="max-w-xl space-y-3">
          <dl className="grid gap-3 sm:grid-cols-2">
            {[["Type", kind], ["When", when], ["Where", where], ["Injury", anyoneHurt === "no" ? "Nobody hurt" : anyoneHurt === "treated" ? "First aid given" : "Medical attention needed"]].map(([k, v]) => (
              <div key={k} className="rounded-md border bg-surface-muted px-3 py-2">
                <dt className="text-xs text-muted-foreground">{k}</dt>
                <dd className="text-sm font-medium">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-muted-foreground">
            Reporting a hazard in good faith is always protected. Nobody is penalised for raising a
            concern that turns out to be nothing.
          </p>
        </div>
      ),
    },
  ];

  return (
    <GuidedFlow
      flowId="safety-report"
      steps={steps}
      submitLabel="Submit report"
      onSubmit={async () => {
        const r = await api.submit("incident", { kind, what, where, when });
        onDone(r.id);
      }}
    />
  );
}

function SafetyPage() {
  const state = useMock(() => relationsApi.incidents());
  const [reporting, setReporting] = useState(false);
  const [ref, setRef] = useState<string | null>(null);

  if (ref) {
    return (
      <AuthGate>
      <AppShell>
        <PageHeader eyebrow="Health and safety" title="Report submitted" />
        <NextSteps
          reference={`HS-${ref}`}
          title="Thank you for reporting it"
          steps={[
            "Health and safety will triage this and decide whether it is externally reportable — you do not need to.",
            "If corrective actions are raised, you will see them against this reference.",
            "Reporting in good faith is protected. Nothing about this affects your record.",
          ]}
          actions={<Button onClick={() => { setRef(null); setReporting(false); }}>Back to health and safety</Button>}
        />
      </AppShell>
      </AuthGate>
    );
  }

  if (reporting) {
    return (
      <AppShell>
        <PageHeader
          eyebrow="Health and safety"
          title="Report an incident or concern"
          description="Three short steps. Safety comes first — the form can wait."
          primaryAction={<Button variant="ghost" onClick={() => setReporting(false)}>Cancel</Button>}
        />
        <ReportFlow onDone={setRef} />
      </AppShell>
    );
  }

  return (
    <AppShell>
      <PageHeader
        eyebrow="Relations and safety"
        title="Health and safety"
        description="Incidents, near misses and hazards, with the corrective actions that came out of them."
        primaryAction={<Button onClick={() => setReporting(true)}>Report an incident or concern</Button>}
        meta={
          <span className="inline-flex items-center gap-1.5 rounded-full border border-success/30 bg-success-soft px-2.5 py-0.5 text-xs font-medium text-success">
            <ShieldCheck className="size-3.5" aria-hidden />
            Reporting in good faith is protected
          </span>
        }
      />

      <Async state={state} rows={3}>
        {(rows) => (
          <ul className="space-y-4">
            {rows.map((i: Incident) => (
              <li key={i.id} className="rounded-lg border bg-surface p-5">
                <div className="flex flex-wrap items-start gap-2">
                  <TriangleAlert
                    className={`mt-0.5 size-4 shrink-0 ${i.severity === "Serious" ? "text-danger" : i.severity === "Moderate" ? "text-warning" : "text-muted-foreground"}`}
                    aria-hidden
                  />
                  <span className="min-w-0 flex-1">
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-medium">{i.kind}</span>
                      <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">
                        {severityWord[i.severity]} severity
                      </span>
                      <span className="rounded-full border bg-surface-muted px-2 py-0.5 text-[11px]">
                        {i.status}
                      </span>
                      <span className="font-mono text-xs text-muted-foreground">{i.id}</span>
                    </span>
                    <span className="mt-1 block text-sm">{i.what}</span>
                    <span className="mt-0.5 block text-xs text-muted-foreground">
                      {i.location} · {i.occurred}
                    </span>
                  </span>
                </div>

                <p className="mt-3 text-xs">
                  <span className="font-medium">
                    {i.reportable ? "Externally reportable" : "Not externally reportable"}
                  </span>
                  {i.reportableNote ? <span className="text-muted-foreground"> — {i.reportableNote}</span> : null}
                </p>

                {i.fitnessOutcome ? (
                  <p className="mt-2 flex gap-2 rounded-md border border-info/30 bg-info-soft p-2 text-xs text-info">
                    <HeartPulse className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                    <span>
                      <span className="font-medium">Fitness to work: </span>
                      {i.fitnessOutcome} No diagnosis is recorded here or anywhere in HR.
                    </span>
                  </p>
                ) : null}

                {i.correctiveActions.length ? (
                  <div className="mt-3">
                    <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      Corrective actions
                    </p>
                    <ul className="mt-1 space-y-1 text-sm">
                      {i.correctiveActions.map((a) => (
                        <li key={a.action} className="flex items-start gap-2">
                          {a.done ? (
                            <Check className="mt-0.5 size-3.5 shrink-0 text-success" aria-hidden />
                          ) : (
                            <TriangleAlert className="mt-0.5 size-3.5 shrink-0 text-warning" aria-hidden />
                          )}
                          <span>
                            {a.action}
                            <span className="block text-xs text-muted-foreground">
                              {a.owner} · due {a.due} · {a.done ? "Done" : "Outstanding"}
                            </span>
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}

                <p className="mt-3 text-xs text-muted-foreground">
                  Next: {i.nextAction} · {i.owner} · due {i.dueDate}
                </p>
              </li>
            ))}
          </ul>
        )}
      </Async>
    </AppShell>
  );
}
