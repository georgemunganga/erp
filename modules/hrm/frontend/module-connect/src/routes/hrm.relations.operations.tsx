import { FormSelect } from "@/components/ui/form-select";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/relations/operations")({
  component: RelationsOperations,
});
type Row = Record<string, unknown>;

function RelationsOperations() {
  const cases = useApi(async () => (await realApi.relationsCases()).items as Row[], []);
  const [caseId, setCaseId] = useState("");
  const [caseType, setCaseType] = useState("grievance");
  const [severity, setSeverity] = useState("medium");
  const [summary, setSummary] = useState("");
  const [description, setDescription] = useState("");
  const [detail, setDetail] = useState<Row | null>(null);
  const [ownerSubjectId, setOwnerSubjectId] = useState("");
  const [actionTitle, setActionTitle] = useState("");
  const [findings, setFindings] = useState("");
  const [outcome, setOutcome] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState("");

  const run = async (label: string, operation: () => Promise<unknown>, refreshDetail = false) => {
    setBusy(label);
    try {
      const result = (await operation()) as Row;
      if (label === "Create case" && result.id) setCaseId(String(result.id));
      if (refreshDetail && caseId) setDetail(await realApi.relationsCase(caseId));
      cases.reload();
      toast.success(`${label} completed`);
      return result;
    } catch (error) {
      toast.error(error instanceof Error ? error.message : `${label} failed`, {
        duration: Infinity,
      });
      return null;
    } finally {
      setBusy("");
    }
  };

  const declare = async () => {
    const result = await run("Conflict declaration", () =>
      realApi.declareRelationsAccess(caseId, {
        decision: "no-conflict",
        notes: "No known conflict",
      }),
    );
    if (result) setDetail(await realApi.relationsCase(caseId));
  };
  const transition = async (status: string) => {
    const result = await run(
      `Move case to ${status}`,
      () =>
        realApi.transitionRelationsCase(caseId, {
          status,
          findings: findings || undefined,
          outcome: outcome || undefined,
        }),
      false,
    );
    if (result) setDetail(result);
  };

  const selected = detail?.case as Row | undefined;
  const actions = (detail?.actions as Row[] | undefined) ?? [];
  const evidence = (detail?.evidence as Row[] | undefined) ?? [];
  const history = (detail?.history as Row[] | undefined) ?? [];

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="Relations and safety"
          title="Case operations"
          description="Restricted employee-relations investigations with declared access, evidence custody, actions, findings and a complete audit history."
        />
        <div className="grid gap-6 xl:grid-cols-2" data-testid="relations-operations">
          <Card>
            <CardHeader>
              <CardTitle>Open a restricted case</CardTitle>
              <CardDescription>Case detail never appears in the triage queue.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <Label htmlFor="case-type">Type</Label>
                  <FormSelect
                    id="case-type"
                    className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                    value={caseType}
                    onChange={(e) => setCaseType(e.target.value)}
                  >
                    <option value="grievance">Grievance</option>
                    <option value="misconduct">Misconduct</option>
                    <option value="harassment">Harassment</option>
                    <option value="investigation">Investigation</option>
                  </FormSelect>
                </div>
                <div>
                  <Label htmlFor="case-severity">Severity</Label>
                  <FormSelect
                    id="case-severity"
                    className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                    value={severity}
                    onChange={(e) => setSeverity(e.target.value)}
                  >
                    <option>low</option>
                    <option>medium</option>
                    <option>high</option>
                    <option>critical</option>
                  </FormSelect>
                </div>
              </div>
              <div>
                <Label htmlFor="case-summary">Neutral summary</Label>
                <Input
                  id="case-summary"
                  value={summary}
                  onChange={(e) => setSummary(e.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="case-description">Allegation or concern</Label>
                <Textarea
                  id="case-description"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                />
              </div>
              <Button
                disabled={!summary || !description || Boolean(busy)}
                onClick={() =>
                  run("Create case", () =>
                    realApi.createCase({
                      caseType,
                      category: caseType,
                      severity,
                      summary,
                      description,
                      confidentiality: "restricted",
                    }),
                  )
                }
              >
                Create restricted case
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Access and assignment</CardTitle>
              <CardDescription>
                A conflict declaration is persisted before sensitive detail is returned.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <FormSelect
                aria-label="Restricted case"
                className="h-9 w-full rounded-md border bg-transparent px-3 text-sm"
                value={caseId}
                onChange={(e) => {
                  setCaseId(e.target.value);
                  setDetail(null);
                }}
              >
                <option value="">Select case</option>
                {(cases.data ?? []).map((c) => (
                  <option key={String(c.id)} value={String(c.id)}>
                    {String(c.reference ?? c.id)} · {String(c.caseType)} · {String(c.status)}
                  </option>
                ))}
              </FormSelect>
              <div className="flex gap-2">
                <Input
                  aria-label="Investigator subject ID"
                  placeholder="Keycloak subject ID"
                  value={ownerSubjectId}
                  onChange={(e) => setOwnerSubjectId(e.target.value)}
                />
                <Button
                  variant="outline"
                  disabled={!caseId || !ownerSubjectId}
                  onClick={() =>
                    run("Assign investigator", () =>
                      realApi.assignRelationsCase(caseId, { ownerSubjectId }),
                    )
                  }
                >
                  Assign
                </Button>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button disabled={!caseId} onClick={declare}>
                  I have no conflict — open case
                </Button>
                <Button
                  variant="outline"
                  disabled={!caseId}
                  onClick={() =>
                    run("Conflict declaration", () =>
                      realApi.declareRelationsAccess(caseId, {
                        decision: "conflict",
                        notes: "Conflict declared in case operations",
                      }),
                    )
                  }
                >
                  Declare conflict
                </Button>
              </div>
              {selected ? (
                <div className="rounded-md border p-3 text-sm">
                  <div className="flex justify-between">
                    <strong>{String(selected.reference)}</strong>
                    <StatusBadge status={String(selected.status)} />
                  </div>
                  <p className="mt-2">{String(detail?.description)}</p>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Sensitive details remain sealed until the declaration succeeds.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Investigation record</CardTitle>
              <CardDescription>
                Actions and evidence remain separate from allegations and findings.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex gap-2">
                <Input
                  aria-label="Investigation action"
                  placeholder="Interview witness"
                  value={actionTitle}
                  onChange={(e) => setActionTitle(e.target.value)}
                />
                <Button
                  disabled={!detail || !actionTitle}
                  onClick={() =>
                    run(
                      "Add investigation action",
                      () =>
                        realApi.createRelationsAction(caseId, {
                          actionType: "investigation",
                          title: actionTitle,
                        }),
                      true,
                    )
                  }
                >
                  Add action
                </Button>
              </div>
              <ul className="space-y-2">
                {actions.map((action) => (
                  <li
                    key={String(action.id)}
                    className="flex items-center justify-between rounded-md border p-2 text-sm"
                  >
                    <span>{String(action.title)}</span>
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={action.status === "completed"}
                      onClick={() =>
                        run(
                          "Complete investigation action",
                          () =>
                            realApi.updateRelationsAction(caseId, String(action.id), {
                              status: "completed",
                            }),
                          true,
                        )
                      }
                    >
                      {action.status === "completed" ? "Complete" : "Mark complete"}
                    </Button>
                  </li>
                ))}
              </ul>
              <div>
                <Label htmlFor="case-evidence">Restricted evidence</Label>
                <Input
                  id="case-evidence"
                  type="file"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                />
              </div>
              <Button
                variant="outline"
                disabled={!detail || !file}
                onClick={() =>
                  file &&
                  run(
                    "Upload evidence",
                    () => realApi.uploadRelationsEvidence(caseId, file, file.name, "document"),
                    true,
                  )
                }
              >
                Upload evidence
              </Button>
              {evidence.map((item) => (
                <div
                  key={String(item.id)}
                  className="flex items-center justify-between rounded-md border p-2 text-sm"
                >
                  <span>
                    {String(item.title)} · {String(item.classification)}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() =>
                      run("Download evidence", () =>
                        realApi.downloadRelationsEvidence(String(item.id), String(item.fileName)),
                      )
                    }
                  >
                    Download
                  </Button>
                </div>
              ))}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Findings and closure</CardTitle>
              <CardDescription>
                Resolution requires findings, an outcome, and no open actions.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" disabled={!detail} onClick={() => transition("triage")}>
                  Begin triage
                </Button>
                <Button
                  variant="outline"
                  disabled={!detail}
                  onClick={() => transition("investigating")}
                >
                  Begin investigation
                </Button>
              </div>
              <div>
                <Label htmlFor="findings">Findings</Label>
                <Textarea
                  id="findings"
                  value={findings}
                  onChange={(e) => setFindings(e.target.value)}
                />
              </div>
              <div>
                <Label htmlFor="outcome">Outcome</Label>
                <Textarea
                  id="outcome"
                  value={outcome}
                  onChange={(e) => setOutcome(e.target.value)}
                />
              </div>
              <div className="flex gap-2">
                <Button
                  disabled={!detail || !findings || !outcome}
                  onClick={() => transition("resolved")}
                >
                  Resolve case
                </Button>
                <Button
                  variant="outline"
                  disabled={!detail || selected?.status !== "resolved"}
                  onClick={() => transition("closed")}
                >
                  Close case
                </Button>
              </div>
              <p className="text-xs text-muted-foreground">
                {history.length} immutable audit event{history.length === 1 ? "" : "s"} recorded.
              </p>
              <ul
                className="max-h-40 space-y-1 overflow-auto text-xs"
                data-testid="relations-audit"
              >
                {history.map((event) => (
                  <li key={String(event.id)} className="rounded border p-2">
                    {String(event.action)} · {String(event.actorSubjectId ?? "system")}
                    {event.toStatus ? ` · ${String(event.toStatus)}` : ""}
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        </div>
      </AppShell>
    </AuthGate>
  );
}
