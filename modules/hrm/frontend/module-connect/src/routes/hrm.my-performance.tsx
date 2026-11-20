import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/my-performance")({
  head: () => ({
    meta: [
      { title: "My performance — Kuubu HRM" },
      { name: "description", content: "Complete your self-assessment and review your performance goals." },
    ],
  }),
  component: MyPerformance,
});

interface MyAssessment {
  id: string;
  cycleId: string;
  cycleName: string;
  cycleStatus: string;
  status: string;
  selfRating?: string;
  selfComments?: string;
  selfSubmittedAt?: string;
  managerRating?: string;
  managerComments?: string;
  managerSubmittedAt?: string;
  managerName?: string;
  finalRating?: string;
  finalComments?: string;
  finalizedAt?: string;
}

const ratingLabels: Record<string, string> = {
  exceptional: "Exceptional",
  exceeds: "Exceeds",
  meets: "Meets",
  developing: "Developing",
  unsatisfactory: "Unsatisfactory",
};

const statusLabels: Record<string, string> = {
  not_started: "Not started",
  self_assessment: "Self done",
  manager_assessment: "Manager done",
  finalized: "Finalized",
};

function MyPerformance() {
  const [submittingFor, setSubmittingFor] = useState<string | null>(null);
  const [selfForm, setSelfForm] = useState({ rating: "", comments: "" });

  const state = useApi(
    async () => {
      const rows = await realApi.myPerformance();
      return (rows as Record<string, unknown>[]).map((raw) => ({
        id: String(raw.id ?? ""),
        cycleId: String(raw.cycleId ?? ""),
        cycleName: String(raw.cycleName ?? ""),
        cycleStatus: String(raw.cycleStatus ?? ""),
        status: String(raw.status ?? "not_started"),
        selfRating: raw.selfRating ? String(raw.selfRating) : undefined,
        selfComments: raw.selfComments ? String(raw.selfComments) : undefined,
        selfSubmittedAt: raw.selfSubmittedAt ? String(raw.selfSubmittedAt) : undefined,
        managerRating: raw.managerRating ? String(raw.managerRating) : undefined,
        managerComments: raw.managerComments ? String(raw.managerComments) : undefined,
        managerSubmittedAt: raw.managerSubmittedAt ? String(raw.managerSubmittedAt) : undefined,
        managerName: raw.managerName ? String(raw.managerName) : undefined,
        finalRating: raw.finalRating ? String(raw.finalRating) : undefined,
        finalComments: raw.finalComments ? String(raw.finalComments) : undefined,
        finalizedAt: raw.finalizedAt ? String(raw.finalizedAt) : undefined,
      })) as MyAssessment[];
    },
    [],
  );

  const handleSubmitSelf = async (assessmentId: string) => {
    try {
      await realApi.submitSelfAssessment(assessmentId, {
        rating: selfForm.rating,
        comments: selfForm.comments,
      });
      setSubmittingFor(null);
      setSelfForm({ rating: "", comments: "" });
      state.reload();
    } catch (err) {
      console.error("Failed to submit self-assessment", err);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Self-service"
          title="My performance"
          description="Review your goals, submit your self-assessment, and see feedback from your manager."
        />
        <Async state={state}>
          {(assessments) => (
            <div className="grid gap-4">
              {assessments.length === 0 && (
                <div className="rounded-lg border bg-card p-8 text-center text-sm text-muted-foreground">
                  No performance cycles are currently active for you.
                </div>
              )}
              {assessments.map((a) => (
                <div key={a.id} className="rounded-lg border bg-card p-5">
                  <div className="flex items-start justify-between">
                    <div>
                      <h3 className="font-medium">{a.cycleName}</h3>
                      <p className="text-xs text-muted-foreground">{a.cycleStatus}</p>
                    </div>
                    <StatusBadge status={statusLabels[a.status] ?? a.status} />
                  </div>

                  {/* Self rating */}
                  {a.selfRating && (
                    <div className="mt-3 rounded-md bg-muted p-3 text-sm">
                      <div className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">Your self-assessment</div>
                      <StatusBadge status={ratingLabels[a.selfRating] ?? a.selfRating} />
                      {a.selfComments && <p className="mt-1 text-xs text-muted-foreground">{a.selfComments}</p>}
                    </div>
                  )}

                  {/* Manager rating */}
                  {a.managerRating && (
                    <div className="mt-3 rounded-md bg-muted p-3 text-sm">
                      <div className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        Manager feedback {a.managerName ? `(${a.managerName})` : ""}
                      </div>
                      <StatusBadge status={ratingLabels[a.managerRating] ?? a.managerRating} />
                      {a.managerComments && <p className="mt-1 text-xs text-muted-foreground">{a.managerComments}</p>}
                    </div>
                  )}

                  {/* Final rating */}
                  {a.finalRating && (
                    <div className="mt-3 rounded-md border border-border bg-accent p-3 text-sm">
                      <div className="mb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">Final rating</div>
                      <StatusBadge status={ratingLabels[a.finalRating] ?? a.finalRating} />
                      {a.finalComments && <p className="mt-1 text-xs text-muted-foreground">{a.finalComments}</p>}
                    </div>
                  )}

                  {/* Submit self-assessment */}
                  {a.status === "not_started" && (
                    <div className="mt-4">
                      {submittingFor === a.id ? (
                        <div className="grid gap-3">
                          <Select value={selfForm.rating} onValueChange={(v) => setSelfForm({ ...selfForm, rating: v })}>
                            <SelectTrigger><SelectValue placeholder="Select your self-rating" /></SelectTrigger>
                            <SelectContent>
                              <SelectItem value="exceptional">Exceptional</SelectItem>
                              <SelectItem value="exceeds">Exceeds expectations</SelectItem>
                              <SelectItem value="meets">Meets expectations</SelectItem>
                              <SelectItem value="developing">Developing</SelectItem>
                              <SelectItem value="unsatisfactory">Unsatisfactory</SelectItem>
                            </SelectContent>
                          </Select>
                          <Textarea
                            placeholder="Provide your self-assessment comments..."
                            value={selfForm.comments}
                            onChange={(e) => setSelfForm({ ...selfForm, comments: e.target.value })}
                            rows={4}
                          />
                          <div className="flex gap-2">
                            <Button variant="outline" onClick={() => setSubmittingFor(null)}>Cancel</Button>
                            <Button onClick={() => handleSubmitSelf(a.id)} disabled={!selfForm.rating}>Submit</Button>
                          </div>
                        </div>
                      ) : (
                        <Button variant="outline" onClick={() => setSubmittingFor(a.id)}>
                          Complete self-assessment
                        </Button>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
