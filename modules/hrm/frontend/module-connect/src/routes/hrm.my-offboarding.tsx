import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";

export const Route = createFileRoute("/hrm/my-offboarding")({
  head: () => ({
    meta: [
      { title: "My resignation — Kuubu HRM" },
      { name: "description", content: "Submit a resignation request and track its status." },
    ],
  }),
  component: MyOffboarding,
});

interface MyOffboardingState {
  workerFullName: string;
  workerEmployeeNo: string;
  request: Record<string, unknown> | null;
}

function adaptMyOffboarding(raw: unknown): MyOffboardingState {
  const r = raw as Record<string, unknown>;
  return {
    workerFullName: String(r.workerFullName ?? ""),
    workerEmployeeNo: String(r.workerEmployeeNo ?? ""),
    request: (r.request as Record<string, unknown>) ?? null,
  };
}

const statusLabels: Record<string, string> = {
  requested: "Requested",
  approved: "Approved",
  in_progress: "In progress",
  completed: "Completed",
  cancelled: "Cancelled",
};

function MyOffboarding() {
  const state = useApi(
    async () => adaptMyOffboarding(await realApi.myOffboarding()),
    [],
  );
  const [submitOpen, setSubmitOpen] = useState(false);
  const [form, setForm] = useState({
    requestType: "resignation",
    reason: "",
    noticeStartDate: "",
    lastWorkingDay: "",
  });

  const handleSubmit = async () => {
    try {
      await realApi.submitResignation({
        requestType: form.requestType,
        reason: form.reason,
        noticeStartDate: form.noticeStartDate,
        lastWorkingDay: form.lastWorkingDay,
      });
      setSubmitOpen(false);
      state.reload();
    } catch (err) {
      console.error("Failed to submit resignation", err);
    }
  };

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Self-service"
          title="My resignation"
          description="Submit a resignation request or check the status of an existing one."
        />
        <Async state={state}>
          {(data) => (
            <div className="max-w-2xl">
              <Card>
                <CardHeader>
                  <CardTitle>
                    {data.workerFullName} · {data.workerEmployeeNo}
                  </CardTitle>
                  <CardDescription>
                    {data.request
                      ? "You have an active offboarding request."
                      : "No resignation request submitted yet."}
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  {data.request ? (
                    <div className="space-y-4">
                      <div className="flex items-center gap-2">
                        <StatusBadge
                          status={
                            statusLabels[String(data.request.status ?? "requested")] ??
                            String(data.request.status ?? "requested")
                          }
                        />
                      </div>
                      <div className="grid gap-2 sm:grid-cols-2">
                        <div>
                          <Label className="text-xs text-muted-foreground">Reason</Label>
                          <p className="text-sm">{String(data.request.reason ?? "—")}</p>
                        </div>
                        <div>
                          <Label className="text-xs text-muted-foreground">Last working day</Label>
                          <p className="text-sm">{String(data.request.lastWorkingDay ?? "—")}</p>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <Button onClick={() => setSubmitOpen(true)}>Submit resignation</Button>
                  )}
                </CardContent>
              </Card>
            </div>
          )}
        </Async>
        <Dialog open={submitOpen} onOpenChange={setSubmitOpen}>
          <DialogContent className="max-w-md">
            <DialogHeader>
              <DialogTitle>Submit resignation</DialogTitle>
              <DialogDescription>
                Your request will be sent to HR for review. Once approved, the offboarding
                checklist will begin automatically.
              </DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-2">
              <div className="grid gap-2">
                <Label htmlFor="my-resignation-reason">Reason</Label>
                <Textarea
                  id="my-resignation-reason"
                  placeholder="Please share the reason for your resignation"
                  value={form.reason}
                  onChange={(e) => setForm({ ...form, reason: e.target.value })}
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div className="grid gap-2">
                  <Label htmlFor="my-notice-start">Notice start date</Label>
                  <Input
                    id="my-notice-start"
                    type="date"
                    value={form.noticeStartDate}
                    onChange={(e) => setForm({ ...form, noticeStartDate: e.target.value })}
                  />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="my-last-day">Last working day</Label>
                  <Input
                    id="my-last-day"
                    type="date"
                    value={form.lastWorkingDay}
                    onChange={(e) => setForm({ ...form, lastWorkingDay: e.target.value })}
                  />
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setSubmitOpen(false)}>
                Cancel
              </Button>
              <Button onClick={handleSubmit} disabled={!form.reason}>
                Submit request
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </AppShell>
    </AuthGate>
  );
}
