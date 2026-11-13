import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
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

export const Route = createFileRoute("/hrm/offboarding/$id")({
  head: () => ({
    meta: [
      { title: "Offboarding detail — Kuubu HRM" },
      { name: "description", content: "Manage exit checklist and conduct exit interview." },
    ],
  }),
  component: OffboardingDetail,
});

interface ChecklistItem {
  id: string;
  title: string;
  description: string;
  owner: string;
  isCompleted: boolean;
  completedBy: string | null;
  completedAt: string | null;
  sortOrder: number;
}

interface ExitInterview {
  id: string;
  workerFullName: string;
  reasonForLeaving: string;
  reasonDetails: string;
  whatWentWell: string;
  whatCouldImprove: string;
  wouldRecommend: string;
  managerFeedback: string;
  hrmNotes: string;
  interviewedBy: string | null;
  interviewedAt: string | null;
  status: string;
}

interface OffboardingRequest {
  id: string;
  workerId: string;
  workerFullName: string;
  workerEmployeeNo: string;
  requestType: string;
  reason: string;
  noticeStartDate: string;
  lastWorkingDay: string;
  status: string;
  approvedBy: string | null;
  approverName: string | null;
  approvedAt: string | null;
  rejectionReason: string | null;
  cancelledReason: string | null;
  isFinalPayProcessed: boolean;
  checklistItemsCompleted: number;
  checklistItemsTotal: number;
  checklistItems: ChecklistItem[];
  exitInterview: ExitInterview | null;
  createdAt: string;
}

function adaptRequest(raw: unknown): OffboardingRequest {
  const r = raw as Record<string, unknown>;
  const items = ((r.checklistItems as unknown[]) ?? []).map((item) => {
    const i = item as Record<string, unknown>;
    return {
      id: String(i.id ?? ""),
      title: String(i.title ?? ""),
      description: String(i.description ?? ""),
      owner: String(i.owner ?? "hr"),
      isCompleted: Boolean(i.isCompleted),
      completedBy: i.completedBy ? String(i.completedBy) : null,
      completedAt: i.completedAt ? String(i.completedAt) : null,
      sortOrder: Number(i.sortOrder ?? 0),
    };
  });
  const ei = r.exitInterview as Record<string, unknown> | null;
  return {
    id: String(r.id ?? ""),
    workerId: String(r.workerId ?? ""),
    workerFullName: String(r.workerFullName ?? ""),
    workerEmployeeNo: String(r.workerEmployeeNo ?? ""),
    requestType: String(r.requestType ?? "resignation"),
    reason: String(r.reason ?? ""),
    noticeStartDate: String(r.noticeStartDate ?? ""),
    lastWorkingDay: String(r.lastWorkingDay ?? ""),
    status: String(r.status ?? "requested"),
    approvedBy: r.approvedBy ? String(r.approvedBy) : null,
    approverName: r.approverName ? String(r.approverName) : null,
    approvedAt: r.approvedAt ? String(r.approvedAt) : null,
    rejectionReason: r.rejectionReason ? String(r.rejectionReason) : null,
    cancelledReason: r.cancelledReason ? String(r.cancelledReason) : null,
    isFinalPayProcessed: Boolean(r.isFinalPayProcessed),
    checklistItemsCompleted: Number(r.checklistItemsCompleted ?? 0),
    checklistItemsTotal: Number(r.checklistItemsTotal ?? 0),
    checklistItems: items,
    exitInterview: ei
      ? {
          id: String(ei.id ?? ""),
          workerFullName: String(ei.workerFullName ?? ""),
          reasonForLeaving: String(ei.reasonForLeaving ?? ""),
          reasonDetails: String(ei.reasonDetails ?? ""),
          whatWentWell: String(ei.whatWentWell ?? ""),
          whatCouldImprove: String(ei.whatCouldImprove ?? ""),
          wouldRecommend: String(ei.wouldRecommend ?? ""),
          managerFeedback: String(ei.managerFeedback ?? ""),
          hrmNotes: String(ei.hrmNotes ?? ""),
          interviewedBy: ei.interviewedBy ? String(ei.interviewedBy) : null,
          interviewedAt: ei.interviewedAt ? String(ei.interviewedAt) : null,
          status: String(ei.status ?? "pending"),
        }
      : null,
    createdAt: String(r.createdAt ?? ""),
  };
}

const typeLabels: Record<string, string> = {
  resignation: "Resignation",
  termination: "Termination",
  retirement: "Retirement",
  redundancy: "Redundancy",
};

const statusLabels: Record<string, string> = {
  requested: "Requested",
  approved: "Approved",
  in_progress: "In progress",
  completed: "Completed",
  cancelled: "Cancelled",
};

const ownerLabels: Record<string, string> = {
  hr: "HR",
  it: "IT",
  manager: "Manager",
  finance: "Finance",
  worker: "Worker",
};

function OffboardingDetail() {
  const { id } = Route.useParams();
  const state = useApi(async () => adaptRequest(await realApi.offboardingRequest(id)), [id]);
  const [addChecklistOpen, setAddChecklistOpen] = useState(false);
  const [exitInterviewOpen, setExitInterviewOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  const [newChecklist, setNewChecklist] = useState({ title: "", description: "", owner: "hr" });
  const [exitForm, setExitForm] = useState({
    reasonForLeaving: "",
    reasonDetails: "",
    whatWentWell: "",
    whatCouldImprove: "",
    wouldRecommend: "",
    managerFeedback: "",
    hrmNotes: "",
    interviewedBy: "",
    status: "pending",
  });
  const [rejectReason, setRejectReason] = useState("");
  const [cancelReason, setCancelReason] = useState("");

  const handleApprove = async () => {
    try {
      await realApi.approveOffboarding(id);
      state.reload();
    } catch (err) {
      console.error("Failed to approve", err);
    }
  };

  const handleReject = async () => {
    try {
      await realApi.rejectOffboarding(id, rejectReason);
      setRejectOpen(false);
      state.reload();
    } catch (err) {
      console.error("Failed to reject", err);
    }
  };

  const handleCancel = async () => {
    try {
      await realApi.cancelOffboarding(id, cancelReason);
      setCancelOpen(false);
      state.reload();
    } catch (err) {
      console.error("Failed to cancel", err);
    }
  };

  const handleFinalPay = async () => {
    try {
      await realApi.markFinalPay(id);
      state.reload();
    } catch (err) {
      console.error("Failed to mark final pay", err);
    }
  };

  const handleCompleteChecklistItem = async (itemId: string) => {
    try {
      await realApi.completeChecklistItem(id, itemId);
      state.reload();
    } catch (err) {
      console.error("Failed to complete checklist item", err);
    }
  };

  const handleAddChecklistItem = async () => {
    try {
      await realApi.addChecklistItem(id, {
        title: newChecklist.title,
        description: newChecklist.description,
        owner: newChecklist.owner,
        sortOrder: 99,
      });
      setAddChecklistOpen(false);
      setNewChecklist({ title: "", description: "", owner: "hr" });
      state.reload();
    } catch (err) {
      console.error("Failed to add checklist item", err);
    }
  };

  const handleExitInterview = async () => {
    try {
      if (state.data?.exitInterview) {
        await realApi.updateExitInterview(id, exitForm);
      } else {
        await realApi.createExitInterview(id, { ...exitForm, workerId: state.data?.workerId ?? "" });
      }
      setExitInterviewOpen(false);
      state.reload();
    } catch (err) {
      console.error("Failed to save exit interview", err);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Offboarding"
          title="Offboarding detail"
          description="Manage the exit process for this employee."
          primaryAction={
            <Link to="/hrm/offboarding">
              <Button variant="outline">Back to list</Button>
            </Link>
          }
        />
        <Async state={state}>
          {(req) => (
            <div className="space-y-6">
              {/* Summary card */}
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <Link to="/hrm/employees" className="hover:underline">
                      {req.workerFullName}
                    </Link>
                    <StatusBadge status={statusLabels[req.status] ?? req.status} />
                  </CardTitle>
                  <CardDescription>
                    {typeLabels[req.requestType] ?? req.requestType} · Employee #{req.workerEmployeeNo}
                  </CardDescription>
                </CardHeader>
                <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                  <div className="grid gap-1">
                    <Label className="text-xs text-muted-foreground">Reason</Label>
                    <p className="text-sm">{req.reason || "—"}</p>
                  </div>
                  <div className="grid gap-1">
                    <Label className="text-xs text-muted-foreground">Notice start</Label>
                    <p className="text-sm">{req.noticeStartDate || "—"}</p>
                  </div>
                  <div className="grid gap-1">
                    <Label className="text-xs text-muted-foreground">Last working day</Label>
                    <p className="text-sm">{req.lastWorkingDay || "—"}</p>
                  </div>
                  <div className="grid gap-1">
                    <Label className="text-xs text-muted-foreground">Created</Label>
                    <p className="text-sm">{req.createdAt}</p>
                  </div>
                  {req.approverName && (
                    <div className="grid gap-1">
                      <Label className="text-xs text-muted-foreground">Approved by</Label>
                      <p className="text-sm">{req.approverName}</p>
                    </div>
                  )}
                  {req.rejectionReason && (
                    <div className="grid gap-1">
                      <Label className="text-xs text-muted-foreground">Rejection reason</Label>
                      <p className="text-sm text-destructive">{req.rejectionReason}</p>
                    </div>
                  )}
                  <div className="grid gap-1">
                    <Label className="text-xs text-muted-foreground">Final pay</Label>
                    <p className="text-sm">{req.isFinalPayProcessed ? "Processed" : "Pending"}</p>
                  </div>
                  <div className="grid gap-1">
                    <Label className="text-xs text-muted-foreground">Checklist progress</Label>
                    <p className="text-sm">
                      {req.checklistItemsCompleted}/{req.checklistItemsTotal} items
                    </p>
                  </div>
                </CardContent>
              </Card>

              {/* Actions */}
              <div className="flex flex-wrap gap-2">
                {req.status === "requested" && (
                  <>
                    <Button onClick={handleApprove} variant="default">Approve</Button>
                    <Button onClick={() => setRejectOpen(true)} variant="destructive">Reject</Button>
                  </>
                )}
                {(req.status === "requested" || req.status === "approved") && (
                  <Button onClick={() => setCancelOpen(true)} variant="outline">Cancel offboarding</Button>
                )}
                {(req.status === "in_progress" || req.status === "completed") && !req.isFinalPayProcessed && (
                  <Button onClick={handleFinalPay} variant="outline">Mark final pay processed</Button>
                )}
              </div>

              <Tabs defaultValue="checklist">
                <TabsList>
                  <TabsTrigger value="checklist">Checklist</TabsTrigger>
                  <TabsTrigger value="interview">Exit interview</TabsTrigger>
                </TabsList>
                <TabsContent value="checklist">
                  <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                      <div>
                        <CardTitle>Exit checklist</CardTitle>
                        <CardDescription>
                          {req.checklistItemsCompleted} of {req.checklistItemsTotal} items completed
                        </CardDescription>
                      </div>
                      <Button variant="outline" onClick={() => setAddChecklistOpen(true)}>
                        Add item
                      </Button>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-3">
                        {req.checklistItems
                          .sort((a, b) => a.sortOrder - b.sortOrder)
                          .map((item) => (
                            <div
                              key={item.id}
                              className="flex items-start gap-3 rounded-lg border p-3"
                            >
                              <Checkbox
                                checked={item.isCompleted}
                                onCheckedChange={() =>
                                  !item.isCompleted && handleCompleteChecklistItem(item.id)
                                }
                                className="mt-0.5"
                              />
                              <div className="flex-1">
                                <p className={`text-sm font-medium ${item.isCompleted ? "line-through text-muted-foreground" : ""}`}>
                                  {item.title}
                                </p>
                                {item.description && (
                                  <p className="text-xs text-muted-foreground">{item.description}</p>
                                )}
                                <p className="mt-1 text-xs text-muted-foreground">
                                  Owner: {ownerLabels[item.owner] ?? item.owner}
                                  {item.completedBy ? ` · Completed by: ${item.completedBy}` : ""}
                                </p>
                              </div>
                            </div>
                          ))}
                      </div>
                    </CardContent>
                  </Card>
                </TabsContent>
                <TabsContent value="interview">
                  <Card>
                    <CardHeader className="flex flex-row items-center justify-between">
                      <div>
                        <CardTitle>Exit interview</CardTitle>
                        <CardDescription>
                          {req.exitInterview
                            ? `Status: ${req.exitInterview.status}`
                            : "Not yet conducted"}
                        </CardDescription>
                      </div>
                      <Button variant="outline" onClick={() => setExitInterviewOpen(true)}>
                        {req.exitInterview ? "Edit interview" : "Start interview"}
                      </Button>
                    </CardHeader>
                    {req.exitInterview && (
                      <CardContent className="space-y-4">
                        <div className="grid gap-1">
                          <Label className="text-xs text-muted-foreground">Reason for leaving</Label>
                          <p className="text-sm">{req.exitInterview.reasonForLeaving || "—"}</p>
                        </div>
                        {req.exitInterview.reasonDetails && (
                          <div className="grid gap-1">
                            <Label className="text-xs text-muted-foreground">Details</Label>
                            <p className="text-sm">{req.exitInterview.reasonDetails}</p>
                          </div>
                        )}
                        <div className="grid gap-1">
                          <Label className="text-xs text-muted-foreground">What went well</Label>
                          <p className="text-sm">{req.exitInterview.whatWentWell || "—"}</p>
                        </div>
                        <div className="grid gap-1">
                          <Label className="text-xs text-muted-foreground">What could improve</Label>
                          <p className="text-sm">{req.exitInterview.whatCouldImprove || "—"}</p>
                        </div>
                        <div className="grid gap-1">
                          <Label className="text-xs text-muted-foreground">Would recommend</Label>
                          <p className="text-sm">{req.exitInterview.wouldRecommend || "—"}</p>
                        </div>
                        {req.exitInterview.managerFeedback && (
                          <div className="grid gap-1">
                            <Label className="text-xs text-muted-foreground">Manager feedback</Label>
                            <p className="text-sm">{req.exitInterview.managerFeedback}</p>
                          </div>
                        )}
                        {req.exitInterview.hrmNotes && (
                          <div className="grid gap-1">
                            <Label className="text-xs text-muted-foreground">HR notes</Label>
                            <p className="text-sm">{req.exitInterview.hrmNotes}</p>
                          </div>
                        )}
                        {req.exitInterview.interviewedBy && (
                          <div className="grid gap-1">
                            <Label className="text-xs text-muted-foreground">Conducted by</Label>
                            <p className="text-sm">
                              {req.exitInterview.interviewedBy}
                              {req.exitInterview.interviewedAt ? ` on ${req.exitInterview.interviewedAt}` : ""}
                            </p>
                          </div>
                        )}
                      </CardContent>
                    )}
                  </Card>
                </TabsContent>
              </Tabs>
            </div>
          )}
        </Async>

        {/* Add checklist item dialog */}
        <Dialog open={addChecklistOpen} onOpenChange={setAddChecklistOpen}>
          <DialogContent className="max-w-md">
            <DialogHeader>
              <DialogTitle>Add checklist item</DialogTitle>
              <DialogDescription>Add a new task to the exit checklist.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <div className="grid gap-2">
                <Label htmlFor="cl-title">Title</Label>
                <Input
                  id="cl-title"
                  placeholder="e.g. Revoke email access"
                  value={newChecklist.title}
                  onChange={(e) => setNewChecklist({ ...newChecklist, title: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="cl-desc">Description</Label>
                <Textarea
                  id="cl-desc"
                  placeholder="Optional details"
                  value={newChecklist.description}
                  onChange={(e) => setNewChecklist({ ...newChecklist, description: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="cl-owner">Owner</Label>
                <Select value={newChecklist.owner} onValueChange={(v) => setNewChecklist({ ...newChecklist, owner: v })}>
                  <SelectTrigger id="cl-owner">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="hr">HR</SelectItem>
                    <SelectItem value="it">IT</SelectItem>
                    <SelectItem value="manager">Manager</SelectItem>
                    <SelectItem value="finance">Finance</SelectItem>
                    <SelectItem value="worker">Worker</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setAddChecklistOpen(false)}>Cancel</Button>
              <Button onClick={handleAddChecklistItem} disabled={!newChecklist.title}>Add item</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Exit interview dialog */}
        <Dialog open={exitInterviewOpen} onOpenChange={setExitInterviewOpen}>
          <DialogContent className="max-w-lg max-h-[80vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{state.data?.exitInterview ? "Edit exit interview" : "New exit interview"}</DialogTitle>
              <DialogDescription>Record the departing employee's feedback.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <div className="grid gap-2">
                <Label htmlFor="ei-reason">Reason for leaving</Label>
                <Input
                  id="ei-reason"
                  placeholder="e.g. Career change, relocation"
                  value={exitForm.reasonForLeaving}
                  onChange={(e) => setExitForm({ ...exitForm, reasonForLeaving: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="ei-details">Reason details</Label>
                <Textarea
                  id="ei-details"
                  value={exitForm.reasonDetails}
                  onChange={(e) => setExitForm({ ...exitForm, reasonDetails: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="ei-well">What went well</Label>
                <Textarea
                  id="ei-well"
                  value={exitForm.whatWentWell}
                  onChange={(e) => setExitForm({ ...exitForm, whatWentWell: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="ei-improve">What could improve</Label>
                <Textarea
                  id="ei-improve"
                  value={exitForm.whatCouldImprove}
                  onChange={(e) => setExitForm({ ...exitForm, whatCouldImprove: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="ei-recommend">Would recommend (to a friend)?</Label>
                <Input
                  id="ei-recommend"
                  placeholder="e.g. Yes, No, Maybe"
                  value={exitForm.wouldRecommend}
                  onChange={(e) => setExitForm({ ...exitForm, wouldRecommend: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="ei-mgr-feedback">Manager feedback</Label>
                <Textarea
                  id="ei-mgr-feedback"
                  value={exitForm.managerFeedback}
                  onChange={(e) => setExitForm({ ...exitForm, managerFeedback: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="ei-hrm-notes">HR notes</Label>
                <Textarea
                  id="ei-hrm-notes"
                  value={exitForm.hrmNotes}
                  onChange={(e) => setExitForm({ ...exitForm, hrmNotes: e.target.value })}
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="grid gap-2">
                  <Label htmlFor="ei-by">Interviewed by</Label>
                  <Input
                    id="ei-by"
                    value={exitForm.interviewedBy}
                    onChange={(e) => setExitForm({ ...exitForm, interviewedBy: e.target.value })}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="ei-status">Status</Label>
                  <Select value={exitForm.status} onValueChange={(v) => setExitForm({ ...exitForm, status: v })}>
                    <SelectTrigger id="ei-status">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="pending">Pending</SelectItem>
                      <SelectItem value="conducted">Conducted</SelectItem>
                      <SelectItem value="saved">Saved</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setExitInterviewOpen(false)}>Cancel</Button>
              <Button onClick={handleExitInterview}>Save interview</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Reject dialog */}
        <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
          <DialogContent className="max-w-sm">
            <DialogHeader>
              <DialogTitle>Reject offboarding request</DialogTitle>
              <DialogDescription>Provide a reason for rejection.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <Input
                placeholder="Reason for rejection"
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
              />
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setRejectOpen(false)}>Cancel</Button>
              <Button variant="destructive" onClick={handleReject} disabled={!rejectReason}>Reject</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Cancel dialog */}
        <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
          <DialogContent className="max-w-sm">
            <DialogHeader>
              <DialogTitle>Cancel offboarding request</DialogTitle>
              <DialogDescription>Provide a reason for cancellation.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <Input
                placeholder="Reason for cancellation"
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
              />
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setCancelOpen(false)}>Cancel</Button>
              <Button variant="destructive" onClick={handleCancel} disabled={!cancelReason}>Cancel offboarding</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </AppShell>
    </AuthGate>
  );
}
