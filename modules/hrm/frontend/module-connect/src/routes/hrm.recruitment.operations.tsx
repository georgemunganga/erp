import { FormSelect } from "@/components/ui/form-select";
import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/recruitment/operations")({
  component: RecruitmentOperations,
});

type Row = Record<string, unknown>;
type Org = { id: string; name: string };

function flattenOrg(value: unknown): Org[] {
  if (!value) return [];
  if (Array.isArray(value)) return value.flatMap(flattenOrg);
  if (typeof value !== "object") return [];
  const row = value as Row;
  const own = row.id ? [{ id: String(row.id), name: String(row.name ?? row.code ?? row.id) }] : [];
  return [...own, ...flattenOrg(row.children), ...flattenOrg(row.items)];
}

function RecruitmentOperations() {
  const state = useApi(async () => {
    const [vacancies, offers, preboarding, orgTree] = await Promise.all([
      realApi.recruitmentVacancies(),
      realApi.recruitmentOffers(),
      realApi.preboardingCases(),
      realApi.orgTree(),
    ]);
    return {
      vacancies: vacancies.items as Row[],
      offers: offers.items as Row[],
      preboarding: preboarding.items as Row[],
      orgs: flattenOrg(orgTree),
    };
  }, []);
  const [busy, setBusy] = useState("");
  const [jobTitle, setJobTitle] = useState("");
  const [orgUnitId, setOrgUnitId] = useState("");
  const [candidateName, setCandidateName] = useState("");
  const [candidateEmail, setCandidateEmail] = useState("");
  const [vacancyId, setVacancyId] = useState("");
  const [candidateId, setCandidateId] = useState("");
  const [interviewId, setInterviewId] = useState("");
  const [salary, setSalary] = useState("120000");
  const [offerId, setOfferId] = useState("");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [file, setFile] = useState<File | null>(null);

  const run = async (label: string, operation: () => Promise<unknown>) => {
    setBusy(label);
    try {
      const result = await operation();
      const row = (result ?? {}) as Row;
      if (row.id && label === "Create candidate") setCandidateId(String(row.id));
      if (row.id && label === "Schedule interview") setInterviewId(String(row.id));
      if (row.id && label === "Create offer") setOfferId(String(row.id));
      toast.success(`${label} completed`);
      state.reload();
      return row;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : `${label} failed`, {
        duration: Infinity,
      });
      return null;
    } finally {
      setBusy("");
    }
  };

  const vacancies = state.data?.vacancies ?? [];
  const offers = state.data?.offers ?? [];
  const cases = state.data?.preboarding ?? [];
  const orgs = state.data?.orgs ?? [];
  const selectedCase = cases.find((x) => String(x.candidateId) === candidateId) ?? cases[0];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Recruitment"
          title="Hiring operations"
          description="Run a traceable candidate journey from a published vacancy through interview, approved offer, preboarding and worker activation."
        />
        <div className="grid gap-6 xl:grid-cols-2" data-testid="recruitment-operations">
          <Card>
            <CardHeader>
              <CardTitle>1. Vacancy and application</CardTitle>
              <CardDescription>Draft, publish, then accept an application.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div>
                <Label htmlFor="org-unit">Org unit</Label>
                <FormSelect
                  id="org-unit"
                  aria-label="Org unit"
                  className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                  value={orgUnitId}
                  onChange={(e) => setOrgUnitId(e.target.value)}
                >
                  <option value="">Select org unit</option>
                  {orgs.map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.name}
                    </option>
                  ))}
                </FormSelect>
              </div>
              <div>
                <Label htmlFor="job-title">Job title</Label>
                <Input
                  id="job-title"
                  value={jobTitle}
                  onChange={(e) => setJobTitle(e.target.value)}
                />
              </div>
              <Button
                disabled={!orgUnitId || !jobTitle || Boolean(busy)}
                onClick={async () => {
                  const row = await run("Create vacancy", () =>
                    realApi.createVacancy({ orgUnitId, jobTitle, grade: "", status: "draft" }),
                  );
                  if (row?.id) setVacancyId(String(row.id));
                }}
              >
                Create draft
              </Button>
              <div className="grid gap-2 sm:grid-cols-2">
                <FormSelect
                  aria-label="Vacancy"
                  className="h-9 rounded-md border bg-transparent px-3 text-sm"
                  value={vacancyId}
                  onChange={(e) => setVacancyId(e.target.value)}
                >
                  <option value="">Select vacancy</option>
                  {vacancies.map((v) => (
                    <option key={String(v.id)} value={String(v.id)}>
                      {String(v.jobTitle)} · {String(v.status)}
                    </option>
                  ))}
                </FormSelect>
                <Button
                  variant="outline"
                  disabled={!vacancyId || Boolean(busy)}
                  onClick={() => run("Publish vacancy", () => realApi.publishVacancy(vacancyId))}
                >
                  Publish
                </Button>
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                <Input
                  aria-label="Candidate name"
                  placeholder="Candidate name"
                  value={candidateName}
                  onChange={(e) => setCandidateName(e.target.value)}
                />
                <Input
                  aria-label="Candidate email"
                  placeholder="Email"
                  value={candidateEmail}
                  onChange={(e) => setCandidateEmail(e.target.value)}
                />
              </div>
              <Button
                disabled={!vacancyId || !candidateName || Boolean(busy)}
                onClick={() =>
                  run("Create candidate", () =>
                    realApi.createCandidate({
                      vacancyId,
                      fullName: candidateName,
                      email: candidateEmail,
                      source: "HR operations",
                    }),
                  )
                }
              >
                Record application
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>2. Selection and interview</CardTitle>
              <CardDescription>
                Every movement and decision is retained in candidate history.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <Input
                aria-label="Candidate ID"
                placeholder="Candidate ID"
                value={candidateId}
                onChange={(e) => setCandidateId(e.target.value)}
              />
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  onClick={() =>
                    run("Start screening", () =>
                      realApi.advanceCandidate(candidateId, {
                        stage: "screening",
                        notes: "Application accepted for screening",
                      }),
                    )
                  }
                >
                  Screen
                </Button>
                <Button
                  variant="outline"
                  onClick={() =>
                    run("Shortlist", () =>
                      realApi.advanceCandidate(candidateId, {
                        stage: "shortlisted",
                        notes: "Meets selection criteria",
                      }),
                    )
                  }
                >
                  Shortlist
                </Button>
                <Button
                  onClick={() =>
                    run("Schedule interview", () =>
                      realApi.createInterview(candidateId, {
                        scheduledAt: new Date(Date.now() + 86_400_000).toISOString(),
                        interviewType: "panel",
                        interviewerName: "Hiring panel",
                      }),
                    )
                  }
                >
                  Schedule interview
                </Button>
              </div>
              <Input
                aria-label="Interview ID"
                placeholder="Interview ID"
                value={interviewId}
                onChange={(e) => setInterviewId(e.target.value)}
              />
              <div className="flex gap-2">
                <Button
                  disabled={!interviewId}
                  onClick={() =>
                    run("Record interview decision", () =>
                      realApi.decideInterview(interviewId, {
                        overallScore: 4,
                        recommendation: "hire",
                        notes: "Panel recommends appointment",
                      }),
                    )
                  }
                >
                  Record decision
                </Button>
                <Button
                  variant="outline"
                  onClick={() =>
                    run("Move to offer", () =>
                      realApi.advanceCandidate(candidateId, {
                        stage: "offered",
                        notes: "Selection decision approved",
                      }),
                    )
                  }
                >
                  Move to offer
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>3. Offer and documents</CardTitle>
              <CardDescription>
                Approval is separate from issue, and responses are timestamped.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid gap-2 sm:grid-cols-2">
                <Input
                  aria-label="Annual base salary"
                  type="number"
                  value={salary}
                  onChange={(e) => setSalary(e.target.value)}
                />
                <Input
                  aria-label="Start date"
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                />
              </div>
              <Button
                onClick={() =>
                  run("Create offer", () =>
                    realApi.createOffer({
                      candidateId,
                      baseSalary: Number(salary),
                      startDate,
                      expiresOn: new Date(Date.now() + 604_800_000).toISOString().slice(0, 10),
                      contractType: "permanent",
                    }),
                  )
                }
              >
                Create offer
              </Button>
              <Input
                aria-label="Offer ID"
                placeholder="Offer ID"
                value={offerId}
                onChange={(e) => setOfferId(e.target.value)}
              />
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  onClick={() => run("Approve offer", () => realApi.approveOffer(offerId))}
                >
                  Approve
                </Button>
                <Button
                  variant="outline"
                  onClick={() => run("Issue offer", () => realApi.issueOffer(offerId))}
                >
                  Issue
                </Button>
                <Button
                  onClick={() =>
                    run("Accept offer", () => realApi.acceptOffer(offerId, { startDate }))
                  }
                >
                  Accept and preboard
                </Button>
              </div>
              <div>
                <Label htmlFor="candidate-document">Candidate document</Label>
                <Input
                  id="candidate-document"
                  type="file"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                />
              </div>
              <Button
                variant="outline"
                disabled={!file || !candidateId}
                onClick={() =>
                  file &&
                  run("Upload candidate document", () =>
                    realApi.uploadCandidateDocument(candidateId, file, "identity", file.name),
                  )
                }
              >
                Upload document
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>4. Preboarding and activation</CardTitle>
              <CardDescription>
                Activation is blocked until every required task is complete.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {selectedCase ? (
                <>
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="font-medium">{String(selectedCase.candidateName)}</p>
                      <p className="text-xs text-muted-foreground">
                        {String(selectedCase.completedTasks)}/{String(selectedCase.totalTasks)}{" "}
                        tasks complete · employee {String(selectedCase.employeeNo)}
                      </p>
                    </div>
                    <StatusBadge status={String(selectedCase.status)} />
                  </div>
                  <ul className="space-y-2">
                    {((selectedCase.tasks as Row[]) ?? []).map((task) => (
                      <li
                        key={String(task.id)}
                        className="flex items-center justify-between rounded-md border p-2 text-sm"
                      >
                        <span>
                          {String(task.title)}
                          {task.required ? " *" : ""}
                        </span>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={task.status === "completed"}
                          onClick={() =>
                            run("Complete preboarding task", () =>
                              realApi.updatePreboardingTask(
                                String(selectedCase.id),
                                String(task.id),
                                { status: "completed", notes: "Completed in hiring operations" },
                              ),
                            )
                          }
                        >
                          {task.status === "completed" ? "Complete" : "Mark complete"}
                        </Button>
                      </li>
                    ))}
                  </ul>
                  <Button
                    onClick={() =>
                      run("Activate worker", () =>
                        realApi.activatePreboarding(String(selectedCase.id)),
                      )
                    }
                  >
                    Activate worker
                  </Button>
                </>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Accept an issued offer to create the preboarding checklist.
                </p>
              )}
            </CardContent>
          </Card>
        </div>
        <div className="mt-6 grid gap-4 md:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>Offers</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {offers.map((offer) => (
                <div key={String(offer.id)} className="flex justify-between border-b py-2 text-sm">
                  <Link
                    className="text-primary underline"
                    to="/hrm/recruitment/candidates/$id"
                    params={{ id: String(offer.candidateId) }}
                  >
                    {String(offer.candidateName ?? offer.candidateId)}
                  </Link>
                  <StatusBadge status={String(offer.status)} />
                </div>
              ))}
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Preboarding queue</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm">
                {cases.length} candidate{cases.length === 1 ? "" : "s"} in onboarding workflow.
              </p>
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}
