import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { EyeOff, Info, Lock, ScaleIcon, ShieldAlert, UserCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { relationsApi } from "@/mock/relations";
import type { RelationsCase } from "@/mock/relations";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { DetailSection, RecordDetail } from "@/platform/components/RecordDetail";
import { MaskedValue } from "@/platform/components/Sensitive";
import { RestrictedState } from "@/platform/components/States";
import { StatusTimeline } from "@/platform/components/StatusTimeline";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/relations/cases/$id")({
  head: () => ({
    meta: [
      { title: "Case — Kuubu HRM" },
      { name: "description", content: "A restricted employee-relations case." },
      { property: "og:title", content: "Case — Kuubu HRM" },
      { property: "og:description", content: "A restricted employee-relations case." },
    ],
  }),
  component: CaseDetail,
});

/**
 * Full case detail is withheld until the handler declares no conflict. This is
 * a real gate, not decoration: a conflicted handler must be removed from the
 * case rather than trusted to look away.
 */
function ConflictGate({
  conflicted,
  onConfirm,
}: {
  conflicted: string[];
  onConfirm: () => void;
}) {
  return (
    <div className="rounded-lg border border-warning/40 bg-warning-soft p-5">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-warning">
        <ScaleIcon className="size-4" aria-hidden />
        Declare any conflict before opening this case
      </h2>
      <p className="mt-2 max-w-2xl text-sm text-foreground">
        Allegations, evidence and findings are hidden until you confirm you have no personal
        interest in the outcome. This protects the people involved and protects any decision that
        follows from being challenged later.
      </p>
      <p className="mt-3 text-sm text-foreground">You must not continue if you:</p>
      <ul className="mt-1 list-inside list-disc space-y-0.5 text-sm text-foreground">
        <li>are the subject of, or the person who raised, this case</li>
        <li>manage or are managed by anyone involved</li>
        <li>witnessed the events, or have already formed a view about them</li>
        <li>have a personal relationship with anyone named</li>
      </ul>

      {conflicted.length ? (
        <p className="mt-3 rounded-md border border-danger/30 bg-danger-soft p-3 text-sm text-danger">
          Already excluded from this case: {conflicted.join(", ")}. They cannot see it, and it does
          not appear in their queue.
        </p>
      ) : null}

      <div className="mt-4 flex flex-wrap gap-2">
        <Button onClick={onConfirm}>I have no conflict — open the case</Button>
        <Button variant="outline">Declare a conflict and hand it back</Button>
      </div>
      <p className="mt-2 text-xs text-muted-foreground">
        Your declaration is recorded against the case with your name and the time.
      </p>
    </div>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

const stageMap: Record<string, string> = {
  "new": "Intake",
  open: "Intake",
  "in-progress": "Investigation",
  investigation: "Investigation",
  hearing: "Hearing",
  "findings-made": "Findings",
  appeal: "Appeal",
  closed: "Closed",
};

async function loadCase(id: string): Promise<RelationsCase | null> {
  const res = await realApi.relationsCases();
  const raw = (res.items as unknown[]).find((c) => String((c as Record<string, unknown>).id ?? "") === id);
  if (!raw) return null;
  const r = raw as Record<string, unknown>;
  return {
    id: String(r.id ?? ""),
    type: (String(r.caseType ?? "Grievance")).replace(/^./, (s) => s.toUpperCase()) as RelationsCase["type"],
    summary: String(r.summary ?? "—"),
    subject: "Subject withheld — conflict check required",
    anonymised: true,
    raisedBy: String(r.raisedBy ?? "Employee"),
    stage: (stageMap[String(r.status ?? "")] ?? String(r.status ?? "Intake")) as RelationsCase["stage"],
    owner: "Employee relations officer",
    nextAction: "Complete conflict-of-interest check",
    dueDate: "—",
    opened: String(r.createdAt ?? "").slice(0, 10) || "—",
    conflicted: [],
    allegations: [],
    evidence: [],
    timeline: [],
  } satisfies RelationsCase;
}

function CaseDetail() {
  const { id } = Route.useParams();
  const state = useApi(async (): Promise<RelationsCase | null> => {
    if (!USE_REAL) return relationsApi.caseItem(id);
    return loadCase(id);
  }, [id]);
  const [declared, setDeclared] = useState(false);

  return (
    <AuthGate>
      <AppShell>
      <Async state={state} rows={3}>
        {(c) => {
          if (!c) return <RestrictedState />;

          return (
            <RecordDetail
              reference={c.id}
              title={c.summary}
              subtitle={`${c.type} · opened ${c.opened}`}
              status={c.stage}
              owner={c.owner}
              nextAction={`${c.nextAction} · due ${c.dueDate}`}
              summary={[
                { label: "Type", value: c.type },
                {
                  label: "Subject",
                  value: (
                    <span className="flex items-center gap-1.5">
                      {c.anonymised ? <EyeOff className="size-3.5 shrink-0 text-muted-foreground" aria-hidden /> : null}
                      {c.subject}
                    </span>
                  ),
                },
                { label: "Raised by", value: c.raisedBy },
                { label: "Stage", value: c.stage },
                { label: "Opened", value: c.opened },
                { label: "Procedural deadline", value: c.dueDate },
              ]}
              timeline={<StatusTimeline title="Case history" events={c.timeline} />}
            >
              <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
                <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
                <span>
                  Nothing here is a finding until the Findings section says so. Record what was
                  alleged and what the evidence shows — do not describe an allegation as though it
                  were established.
                </span>
              </p>

              {!declared ? (
                <ConflictGate conflicted={c.conflicted} onConfirm={() => setDeclared(true)} />
              ) : (
                <>
                  <DetailSection
                    title="Allegations"
                    description="What has been alleged. Neutral language only — these are not findings."
                  >
                    <ul className="list-inside list-disc space-y-1.5 text-sm">
                      {c.allegations.map((a) => (
                        <li key={a}>{a}</li>
                      ))}
                    </ul>
                  </DetailSection>

                  <DetailSection
                    title="Evidence"
                    description="Held separately from allegations and findings. Restricted items stay masked."
                  >
                    <ul className="space-y-2 text-sm">
                      {c.evidence.map((e) => (
                        <li key={e.label} className="flex items-start gap-2 rounded-md border p-3">
                          {e.restricted ? (
                            <Lock className="mt-0.5 size-4 shrink-0 text-danger" aria-hidden />
                          ) : (
                            <UserCheck className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
                          )}
                          <span className="min-w-0">
                            <span className="block font-medium">{e.label}</span>
                            <span className="block text-xs text-muted-foreground">
                              {e.kind}
                              {e.restricted ? " · restricted — witness identity withheld" : ""}
                            </span>
                          </span>
                        </li>
                      ))}
                    </ul>
                    {c.evidence.some((e) => e.restricted) ? (
                      <div className="mt-4 max-w-sm">
                        <MaskedValue
                          label="Witness identity (restricted)"
                          value="Withheld — release requires the investigating officer's approval"
                          hint="Revealing a protected witness identity is recorded and reviewed."
                        />
                      </div>
                    ) : null}
                  </DetailSection>

                  <DetailSection
                    title="Representation and right of reply"
                    description="Procedural fairness is part of the record, not an afterthought."
                  >
                    <ul className="space-y-1.5 text-sm">
                      <li>{c.representation ?? "No representation requested so far."}</li>
                      <li>
                        The employee has the right to respond to every allegation before findings are
                        made, and to see the evidence relied on.
                      </li>
                      <li>
                        If the deadline of {c.dueDate} cannot be met, the employee must be told why
                        and given a revised date. An unexplained delay can invalidate the outcome.
                      </li>
                    </ul>
                  </DetailSection>

                  <DetailSection
                    title="Findings and outcome"
                    description="Separate from allegations on purpose. Empty until a decision is properly made."
                  >
                    {c.findings ? (
                      <div className="space-y-3 text-sm">
                        <div>
                          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Findings</p>
                          <p className="mt-1">{c.findings}</p>
                        </div>
                        <div>
                          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Outcome</p>
                          <p className="mt-1">{c.outcome}</p>
                        </div>
                        <p className="text-xs text-muted-foreground">
                          An appeal may be lodged within 10 working days of the outcome being
                          communicated, and is heard by someone not involved in this case.
                        </p>
                      </div>
                    ) : (
                      <p className="flex gap-2 text-sm text-muted-foreground">
                        <ShieldAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                        No findings yet. This case is at the {c.stage.toLowerCase()} stage — recording
                        an outcome here before that concludes would prejudice it.
                      </p>
                    )}
                  </DetailSection>
                </>
              )}
            </RecordDetail>
          );
        }}
      </Async>
    </AppShell>
      </AuthGate>
  );
}
