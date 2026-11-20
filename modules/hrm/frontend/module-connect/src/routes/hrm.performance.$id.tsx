import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/performance/$id")({
  head: () => ({
    meta: [
      { title: "Performance cycle — Kuubu HRM" },
      { name: "description", content: "View goals, assessments and reports for a performance review cycle." },
    ],
  }),
  component: CycleDetail,
});

interface Cycle {
  id: string;
  name: string;
  periodType: string;
  startDate: string;
  endDate: string;
  status: string;
  goalCount: number;
  assessmentCount: number;
}

interface Goal {
  id: string;
  cycleId: string;
  workerId?: string;
  workerName?: string;
  category: string;
  title: string;
  description?: string;
  weight: number;
  measurementType: string;
  targetValue?: string;
  actualValue?: string;
  sortOrder: number;
}

interface Assessment {
  id: string;
  cycleId: string;
  workerId: string;
  workerName?: string;
  workerEmployeeNo?: string;
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

function adaptCycle(raw: Record<string, unknown>): Cycle {
  return {
    id: String(raw.id ?? ""),
    name: String(raw.name ?? ""),
    periodType: String(raw.periodType ?? ""),
    startDate: String(raw.startDate ?? ""),
    endDate: String(raw.endDate ?? ""),
    status: String(raw.status ?? "draft"),
    goalCount: Number(raw.goalCount ?? 0),
    assessmentCount: Number(raw.assessmentCount ?? 0),
  };
}

function adaptGoals(rows: unknown[]): Goal[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    return {
      id: String(r.id ?? ""),
      cycleId: String(r.cycleId ?? ""),
      workerId: r.workerId ? String(r.workerId) : undefined,
      workerName: r.workerName ? String(r.workerName) : undefined,
      category: String(r.category ?? "business"),
      title: String(r.title ?? ""),
      description: r.description ? String(r.description) : undefined,
      weight: Number(r.weight ?? 100),
      measurementType: String(r.measurementType ?? "qualitative"),
      targetValue: r.targetValue ? String(r.targetValue) : undefined,
      actualValue: r.actualValue ? String(r.actualValue) : undefined,
      sortOrder: Number(r.sortOrder ?? 0),
    };
  });
}

function adaptAssessments(rows: unknown[]): Assessment[] {
  return rows.map((raw) => {
    const r = raw as Record<string, unknown>;
    return {
      id: String(r.id ?? ""),
      cycleId: String(r.cycleId ?? ""),
      workerId: String(r.workerId ?? ""),
      workerName: r.workerName ? String(r.workerName) : undefined,
      workerEmployeeNo: r.workerEmployeeNo ? String(r.workerEmployeeNo) : undefined,
      status: String(r.status ?? "not_started"),
      selfRating: r.selfRating ? String(r.selfRating) : undefined,
      selfComments: r.selfComments ? String(r.selfComments) : undefined,
      selfSubmittedAt: r.selfSubmittedAt ? String(r.selfSubmittedAt) : undefined,
      managerRating: r.managerRating ? String(r.managerRating) : undefined,
      managerComments: r.managerComments ? String(r.managerComments) : undefined,
      managerSubmittedAt: r.managerSubmittedAt ? String(r.managerSubmittedAt) : undefined,
      managerName: r.managerName ? String(r.managerName) : undefined,
      finalRating: r.finalRating ? String(r.finalRating) : undefined,
      finalComments: r.finalComments ? String(r.finalComments) : undefined,
      finalizedAt: r.finalizedAt ? String(r.finalizedAt) : undefined,
    };
  });
}

const statusLabels: Record<string, string> = {
  draft: "Draft",
  active: "Active",
  assessments_open: "Assessments open",
  assessments_due: "Assessments due",
  completed: "Completed",
  closed: "Closed",
};

const categoryLabels: Record<string, string> = {
  business: "Business",
  development: "Development",
  behavioural: "Behavioural",
};

const ratingLabels: Record<string, string> = {
  exceptional: "Exceptional",
  exceeds: "Exceeds",
  meets: "Meets",
  developing: "Developing",
  unsatisfactory: "Unsatisfactory",
};

const assessmentStatusLabels: Record<string, string> = {
  not_started: "Not started",
  self_assessment: "Self done",
  manager_assessment: "Manager done",
  finalized: "Finalized",
};

const categoryTones: Record<string, string> = {
  business: "text-primary",
  development: "text-info",
  behavioural: "text-warning",
};

function CycleDetail() {
  const { id } = Route.useParams();
  const [tab, setTab] = useState("goals");
  const [goalDialogOpen, setGoalDialogOpen] = useState(false);
  const [editingAssessment, setEditingAssessment] = useState<Assessment | null>(null);
  const [goalForm, setGoalForm] = useState({
    workerId: "",
    category: "business",
    title: "",
    description: "",
    weight: "100",
    measurementType: "qualitative",
    targetValue: "",
    sortOrder: "0",
  });
  const [managerForm, setManagerForm] = useState({
    rating: "",
    comments: "",
  });

  const cycleState = useApi(
    async () => {
      const raw = await realApi.performanceCycle(id);
      return adaptCycle(raw);
    },
    [id],
  );

  const goalsState = useApi(
    async () => {
      const rows = await realApi.performanceGoals(id);
      return adaptGoals(rows);
    },
    [id],
  );

  const assessmentsState = useApi(
    async () => {
      const rows = await realApi.performanceAssessments(id);
      return adaptAssessments(rows);
    },
    [id],
  );

  const reportState = useApi(
    async () => {
      const raw = await realApi.performanceCycleReport(id);
      return raw as Record<string, unknown>;
    },
    [id],
  );

  const handleCreateGoal = async () => {
    try {
      await realApi.createPerformanceGoal(id, {
        workerId: goalForm.workerId || undefined,
        category: goalForm.category,
        title: goalForm.title,
        description: goalForm.description || undefined,
        weight: Number(goalForm.weight),
        measurementType: goalForm.measurementType,
        targetValue: goalForm.targetValue || undefined,
        sortOrder: Number(goalForm.sortOrder),
      });
      setGoalDialogOpen(false);
      setGoalForm({ workerId: "", category: "business", title: "", description: "", weight: "100", measurementType: "qualitative", targetValue: "", sortOrder: "0" });
      goalsState.reload();
    } catch (err) {
      console.error("Failed to create goal", err);
    }
  };

  const handleSeedAssessments = async () => {
    try {
      await realApi.seedPerformanceAssessments(id);
      assessmentsState.reload();
    } catch (err) {
      console.error("Failed to seed assessments", err);
    }
  };

  const handleManagerSubmit = async () => {
    if (!editingAssessment) return;
    try {
      await realApi.submitManagerAssessment(editingAssessment.id, {
        rating: managerForm.rating,
        comments: managerForm.comments,
      });
      setEditingAssessment(null);
      assessmentsState.reload();
    } catch (err) {
      console.error("Failed to submit manager assessment", err);
    }
  };

  const handleFinalize = async (assessmentId: string) => {
    try {
      await realApi.finalizeAssessment(assessmentId, {});
      assessmentsState.reload();
    } catch (err) {
      console.error("Failed to finalize", err);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <Async state={cycleState}>
          {(cycle) => (
            <>
              <PageHeader
                eyebrow="Performance"
                title={cycle.name}
                description={`${cycle.periodType} review · ${cycle.startDate} → ${cycle.endDate}`}
                meta={
                  <StatusBadge status={statusLabels[cycle.status] ?? cycle.status} />
                }
                primaryAction={
                  <div className="flex items-center gap-2">
                    {cycle.status !== "closed" && cycle.status !== "completed" && cycle.status !== "assessments_open" && (
                      <Button
                        variant="outline"
                        onClick={async () => {
                          await realApi.updatePerformanceCycle(id, { status: "assessments_open" });
                          cycleState.reload();
                        }}
                      >
                        Open assessments
                      </Button>
                    )}
                    {cycle.status !== "closed" && (
                      <Button
                        variant="destructive"
                        onClick={async () => {
                          await realApi.closePerformanceCycle(id);
                          cycleState.reload();
                        }}
                      >
                        Close cycle
                      </Button>
                    )}
                  </div>
                }
              />
              <Tabs value={tab} onValueChange={setTab} className="mt-4">
                <TabsList>
                  <TabsTrigger value="goals">Goals ({cycle.goalCount})</TabsTrigger>
                  <TabsTrigger value="assessments">Assessments ({cycle.assessmentCount})</TabsTrigger>
                  <TabsTrigger value="report">Report</TabsTrigger>
                </TabsList>
                <TabsContent value="goals" className="mt-4">
                  <div className="mb-4 flex justify-end">
                    <Button onClick={() => setGoalDialogOpen(true)}>Add goal</Button>
                  </div>
                  <Async state={goalsState}>
                    {(goals) => (
                      <div className="grid gap-3">
                        {goals.length === 0 && (
                          <p className="py-8 text-center text-sm text-muted-foreground">
                            No goals yet. Add goals for workers in this cycle.
                          </p>
                        )}
                        {goals.map((goal) => (
                          <div key={goal.id} className="rounded-lg border bg-card p-4">
                            <div className="flex items-start justify-between">
                              <div>
                                <h3 className="font-medium">{goal.title}</h3>
                                {goal.description && (
                                  <p className="mt-1 text-sm text-muted-foreground">{goal.description}</p>
                                )}
                                {goal.workerName && (
                                  <p className="mt-1 text-xs text-muted-foreground">Assigned to: {goal.workerName}</p>
                                )}
                              </div>
                              <div className="flex items-center gap-2">
                                <span className={`text-xs font-medium ${categoryTones[goal.category] ?? "text-muted-foreground"}`}>
                                  {categoryLabels[goal.category] ?? goal.category}
                                </span>
                                <span className="text-xs text-muted-foreground">Weight: {goal.weight}%</span>
                              </div>
                            </div>
                            <div className="mt-2 grid grid-cols-2 gap-4 text-xs text-muted-foreground">
                              <div><span className="font-medium text-foreground">Target:</span> {goal.targetValue ?? "—"}</div>
                              <div><span className="font-medium text-foreground">Actual:</span> {goal.actualValue ?? "—"}</div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </Async>
                </TabsContent>
                <TabsContent value="assessments" className="mt-4">
                  <div className="mb-4 flex items-center gap-2">
                    <Button onClick={handleSeedAssessments} variant="outline">
                      Create assessments for all workers
                    </Button>
                  </div>
                  <Async state={assessmentsState}>
                    {(assessments) => (
                      <div className="grid gap-3">
                        {assessments.length === 0 && (
                          <p className="py-8 text-center text-sm text-muted-foreground">
                            No assessments yet. Click &quot;Create assessments for all workers&quot; to generate rows for every active employee.
                          </p>
                        )}
                        {assessments.map((a) => (
                          <div key={a.id} className="rounded-lg border bg-card p-4">
                            <div className="flex items-start justify-between">
                              <div>
                                <h3 className="font-medium">{a.workerName ?? a.workerEmployeeNo ?? "Unknown"}</h3>
                                {a.workerEmployeeNo && <p className="text-xs text-muted-foreground">{a.workerEmployeeNo}</p>}
                              </div>
                              <StatusBadge status={assessmentStatusLabels[a.status] ?? a.status} />
                            </div>
                            <div className="mt-3 grid gap-2 text-sm">
                              {a.selfRating && (
                                <div className="flex items-center gap-2">
                                  <span className="w-24 text-xs font-medium text-muted-foreground">Self</span>
                                  <StatusBadge status={ratingLabels[a.selfRating] ?? a.selfRating} />
                                </div>
                              )}
                              {a.managerRating && (
                                <div className="flex items-center gap-2">
                                  <span className="w-24 text-xs font-medium text-muted-foreground">Manager</span>
                                  <StatusBadge status={ratingLabels[a.managerRating] ?? a.managerRating} />
                                </div>
                              )}
                              {a.finalRating && (
                                <div className="flex items-center gap-2">
                                  <span className="w-24 text-xs font-medium text-muted-foreground">Final</span>
                                  <StatusBadge status={ratingLabels[a.finalRating] ?? a.finalRating} />
                                </div>
                              )}
                            </div>
                            <div className="mt-3 flex gap-2">
                              {a.status !== "finalized" && (
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => {
                                    setEditingAssessment(a);
                                    setManagerForm({ rating: "", comments: "" });
                                  }}
                                >
                                  Manager review
                                </Button>
                              )}
                              {a.status === "manager_assessment" && (
                                <Button size="sm" onClick={() => handleFinalize(a.id)}>
                                  Finalize
                                </Button>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </Async>
                </TabsContent>
                <TabsContent value="report" className="mt-4">
                  <Async state={reportState}>
                    {(report) => (
                      <div className="rounded-lg border bg-card p-6">
                        <h3 className="mb-4 font-medium">Rating distribution</h3>
                        <div className="grid gap-2 text-sm">
                          {Object.entries(report).map(([key, value]) => (
                            <div key={key} className="flex items-center justify-between border-b pb-2">
                              <span className="text-muted-foreground">{ratingLabels[key] ?? key}</span>
                              <span className="font-medium tabular-nums">{String(value)}</span>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </Async>
                </TabsContent>
              </Tabs>
              {/* Goal creation dialog */}
              <Dialog open={goalDialogOpen} onOpenChange={setGoalDialogOpen}>
                <DialogContent className="max-w-lg">
                  <DialogHeader>
                    <DialogTitle>Add goal</DialogTitle>
                    <DialogDescription>Define a goal for a worker in this review cycle.</DialogDescription>
                  </DialogHeader>
                  <div className="grid gap-4 py-2">
                    <div className="grid gap-2">
                      <Label htmlFor="goal-title">Title</Label>
                      <Input
                        id="goal-title"
                        placeholder="e.g. Increase sales by 15%"
                        value={goalForm.title}
                        onChange={(e) => setGoalForm({ ...goalForm, title: e.target.value })}
                      />
                    </div>
                    <div className="grid gap-2">
                      <Label htmlFor="goal-desc">Description</Label>
                      <Textarea
                        id="goal-desc"
                        placeholder="Optional description"
                        value={goalForm.description}
                        onChange={(e) => setGoalForm({ ...goalForm, description: e.target.value })}
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="grid gap-2">
                        <Label>Category</Label>
                        <Select value={goalForm.category} onValueChange={(v) => setGoalForm({ ...goalForm, category: v })}>
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            <SelectItem value="business">Business</SelectItem>
                            <SelectItem value="development">Development</SelectItem>
                            <SelectItem value="behavioural">Behavioural</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="grid gap-2">
                        <Label htmlFor="goal-weight">Weight (%)</Label>
                        <Input
                          id="goal-weight"
                          type="number"
                          value={goalForm.weight}
                          onChange={(e) => setGoalForm({ ...goalForm, weight: e.target.value })}
                        />
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="grid gap-2">
                        <Label>Measurement</Label>
                        <Select value={goalForm.measurementType} onValueChange={(v) => setGoalForm({ ...goalForm, measurementType: v })}>
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            <SelectItem value="qualitative">Qualitative</SelectItem>
                            <SelectItem value="quantitative">Quantitative</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="grid gap-2">
                        <Label htmlFor="goal-target">Target value</Label>
                        <Input
                          id="goal-target"
                          placeholder="e.g. 15%"
                          value={goalForm.targetValue}
                          onChange={(e) => setGoalForm({ ...goalForm, targetValue: e.target.value })}
                        />
                      </div>
                    </div>
                  </div>
                  <DialogFooter>
                    <Button variant="outline" onClick={() => setGoalDialogOpen(false)}>Cancel</Button>
                    <Button onClick={handleCreateGoal} disabled={!goalForm.title}>Add goal</Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
              {/* Manager assessment dialog */}
              <Dialog open={!!editingAssessment} onOpenChange={() => setEditingAssessment(null)}>
                <DialogContent className="max-w-lg">
                  <DialogHeader>
                    <DialogTitle>Manager assessment</DialogTitle>
                    <DialogDescription>{editingAssessment?.workerName ?? editingAssessment?.workerEmployeeNo ?? ""}</DialogDescription>
                  </DialogHeader>
                  {editingAssessment?.selfRating && (
                    <div className="rounded-md bg-muted p-3 text-sm">
                      <span className="font-medium">Self rating:</span>{" "}
                      {ratingLabels[editingAssessment.selfRating] ?? editingAssessment.selfRating}
                    </div>
                  )}
                  <div className="grid gap-4 py-2">
                    <div className="grid gap-2">
                      <Label>Rating</Label>
                      <Select value={managerForm.rating} onValueChange={(v) => setManagerForm({ ...managerForm, rating: v })}>
                        <SelectTrigger><SelectValue placeholder="Select rating" /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="exceptional">Exceptional</SelectItem>
                          <SelectItem value="exceeds">Exceeds expectations</SelectItem>
                          <SelectItem value="meets">Meets expectations</SelectItem>
                          <SelectItem value="developing">Developing</SelectItem>
                          <SelectItem value="unsatisfactory">Unsatisfactory</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid gap-2">
                      <Label htmlFor="mgr-comments">Comments</Label>
                      <Textarea
                        id="mgr-comments"
                        placeholder="Provide feedback and justification..."
                        value={managerForm.comments}
                        onChange={(e) => setManagerForm({ ...managerForm, comments: e.target.value })}
                        rows={4}
                      />
                    </div>
                  </div>
                  <DialogFooter>
                    <Button variant="outline" onClick={() => setEditingAssessment(null)}>Cancel</Button>
                    <Button onClick={handleManagerSubmit} disabled={!managerForm.rating}>Submit</Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
            </>
          )}
        </Async>
      </AppShell>
    </AuthGate>
  );
}
