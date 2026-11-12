import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import { AlertTriangle, Check, FileKey, Lock, Plus, ShieldCheck, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { privacyApi } from "@/mock/privacy";
import type { ProcessingPurpose } from "@/mock/privacy";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { realApi, useApi } from "@/platform/use-api";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/people/privacy")({
  head: () => ({
    meta: [
      { title: "Privacy and data retention — Kuubu HRM" },
      {
        name: "description",
        content:
          "What personal data is held, why, how long each record type is kept, and how to exercise your rights over it.",
      },
      { property: "og:title", content: "Privacy and data retention — Kuubu HRM" },
      {
        property: "og:description",
        content:
          "What personal data is held, why, how long each record type is kept, and how to exercise your rights over it.",
      },
    ],
  }),
  component: PrivacyPage,
});

const consentLabel: Record<ProcessingPurpose["consent"], string> = {
  granted: "Consent given",
  withdrawn: "Consent withdrawn",
  "not-required": "Consent not required",
};

function PurposeCard({ p }: { p: ProcessingPurpose }) {
  const [confirming, setConfirming] = useState(false);
  const [consent, setConsent] = useState(p.consent);

  return (
    <li className="rounded-lg border bg-surface p-5">
      <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
        <div className="min-w-0">
          <h3 className="text-sm font-semibold">{p.purpose}</h3>
          <p className="mt-1 text-sm text-muted-foreground">{p.dataHeld}</p>

          <dl className="mt-3 grid gap-x-6 gap-y-2 text-xs sm:grid-cols-2">
            <div>
              <dt className="text-muted-foreground">Why we can hold it</dt>
              <dd className="font-medium">{p.lawfulBasis}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Kept for</dt>
              <dd className="font-medium">{p.retention}</dd>
            </div>
            {p.crossBorder ? (
              <div className="sm:col-span-2">
                <dt className="text-muted-foreground">Where it is processed</dt>
                <dd className="font-medium">{p.crossBorder}</dd>
              </div>
            ) : null}
          </dl>
        </div>

        <div className="flex shrink-0 flex-col items-start gap-2 sm:items-end">
          <span
            className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${
              consent === "granted"
                ? "border-success/30 bg-success-soft text-success"
                : consent === "withdrawn"
                  ? "border-warning/40 bg-warning-soft text-warning"
                  : "border-border bg-muted text-muted-foreground"
            }`}
          >
            {consent === "granted" ? (
              <Check className="size-3.5" aria-hidden />
            ) : consent === "withdrawn" ? (
              <AlertTriangle className="size-3.5" aria-hidden />
            ) : (
              <Lock className="size-3.5" aria-hidden />
            )}
            {consentLabel[consent]}
          </span>

          {p.withdrawable && consent === "granted" ? (
            <Button variant="outline" size="sm" onClick={() => setConfirming(true)}>
              Withdraw consent
            </Button>
          ) : null}

          {p.withdrawable && consent === "withdrawn" ? (
            <Button variant="ghost" size="sm" onClick={() => setConsent("granted")}>
              Give consent again
            </Button>
          ) : null}

          {!p.withdrawable && p.notRequiredReason ? (
            <p className="max-w-56 text-left text-[11px] text-muted-foreground sm:text-right">
              {p.notRequiredReason}
            </p>
          ) : null}
        </div>
      </div>

      <AlertDialog open={confirming} onOpenChange={setConfirming}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Withdraw consent for {p.purpose.toLowerCase()}?</AlertDialogTitle>
            <AlertDialogDescription>
              {p.consequenceOfWithdrawal} This is recorded and takes effect immediately. You can give
              consent again at any time.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep consent</AlertDialogCancel>
            <AlertDialogAction onClick={() => setConsent("withdrawn")}>
              Withdraw consent
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </li>
  );
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

interface RetentionRow {
  id: string;
  recordType: string;
  description: string;
  retentionMonths: number;
  active: boolean;
}

function retentionYears(months: number): string {
  if (months < 12) return `${months} month${months === 1 ? "" : "s"}`;
  const y = months / 12;
  return y % 1 === 0 ? `${y} year${y === 1 ? "" : "s"}` : `${y.toFixed(1)} years`;
}

function PrivacyPage() {
  const purposes = useMock(() => privacyApi.purposes());
  const requests = useMock(() => privacyApi.requests());
  const [tick, setTick] = useState(0);
  const [createOpen, setCreateOpen] = useState(false);
  const retentionState = useApi(
    async () => {
      const rules = await realApi.retentionRules();
      return ((rules ?? []) as Record<string, unknown>[]).map((r) => ({
        id: String(r.id ?? ""),
        recordType: String(r.recordType ?? ""),
        description: String(r.description ?? ""),
        retentionMonths: Number(r.retentionMonths ?? 84),
        active: Boolean(r.active ?? true),
      }));
    },
    [tick, createOpen],
  );

  return (
    <AuthGate>
      <AppShell>
        <PageHeader
          eyebrow="People"
          title="Privacy and data retention"
          description={
            USE_REAL
              ? "How long each category of employee record is retained, who can see what, and how to exercise data rights. Retention rules apply to this tenant only."
              : "Everything held about you, grouped by why it is held rather than by where it is stored."
          }
          primaryAction={
            <Button asChild>
              <Link to="/hrm/requests/new">Raise a data request</Link>
            </Button>
          }
          meta={
            <span className="inline-flex items-center gap-1.5 rounded-full border border-info/30 bg-info-soft px-2.5 py-0.5 text-xs font-medium text-info">
              <ShieldCheck className="size-3.5" aria-hidden />
              You can access, correct or ask us to erase your data
            </span>
          }
        />

        {USE_REAL ? (
          <section aria-label="Data retention rules" className="space-y-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-sm font-semibold">Data retention rules</h2>
                <p className="mt-1 text-xs text-muted-foreground">
                  How long each record type is kept before it is purged. Records under a legal hold
                  are never purged, no matter the rule.
                </p>
              </div>
              <Button onClick={() => setCreateOpen(true)}>
                <Plus className="size-4" aria-hidden />
                Add rule
              </Button>
            </div>
            <Async state={retentionState}>
              {(rows: RetentionRow[]) => (
                <ul className="divide-y rounded-lg border bg-surface">
                  {rows.map((rule) => (
                    <li key={rule.id} className="flex flex-wrap items-center gap-4 p-4">
                      <div className="flex size-9 shrink-0 items-center justify-center rounded-md border bg-surface-muted">
                        <FileKey className="size-4 text-muted-foreground" aria-hidden />
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-medium capitalize">{rule.recordType}</span>
                          <StatusBadge status={rule.active ? "active" : "inactive"} />
                        </div>
                        {rule.description ? (
                          <p className="mt-0.5 text-xs text-muted-foreground">{rule.description}</p>
                        ) : null}
                      </div>
                      <span className="text-sm font-semibold">
                        {retentionYears(rule.retentionMonths)}
                      </span>
                      <div className="flex items-center gap-4">
                        <div className="flex items-center gap-2">
                          <Switch
                            checked={rule.active}
                            onCheckedChange={async (next) => {
                              try {
                                await realApi.updateRetentionRule(rule.id, { active: next });
                                feedback.submitted(
                                  next ? "Rule enabled" : "Rule disabled",
                                  `The ${rule.recordType} retention rule is now ${
                                    next ? "active" : "switched off"
                                  }.`,
                                );
                                setTick((t) => t + 1);
                              } catch (err) {
                                feedback.blocked(
                                  "Could not change the rule",
                                  err instanceof Error ? err.message : "Unknown error",
                                );
                              }
                            }}
                            aria-label={`Toggle ${rule.recordType}`}
                          />
                        </div>
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={async () => {
                            try {
                              await realApi.deleteRetentionRule(rule.id);
                              feedback.removed(
                                "Rule deleted",
                                () => setTick((t) => t + 1),
                              );
                            } catch (err) {
                              feedback.blocked(
                                "Could not delete the rule",
                                err instanceof Error ? err.message : "Unknown error",
                              );
                            }
                          }}
                          aria-label={`Delete ${rule.recordType} rule`}
                        >
                          <Trash2 className="size-4 text-destructive" aria-hidden />
                        </Button>
                      </div>
                    </li>
                  ))}
                  {rows.length === 0 ? (
                    <li className="p-8 text-center text-sm text-muted-foreground">
                      No retention rules yet. Add one so the system knows how long each record type
                      should be kept.
                    </li>
                  ) : null}
                </ul>
              )}
            </Async>
            <CreateRetentionDialog
              open={createOpen}
              onOpenChange={setCreateOpen}
              onSaved={() => setTick((t) => t + 1)}
            />
          </section>
        ) : null}

        <section aria-label="What we hold and why" className={USE_REAL ? "pt-6" : undefined}>
          <h2 className="text-sm font-semibold">What we hold, and why</h2>
          <Async state={purposes} rows={4}>
            {(rows) => (
              <ul className="mt-3 space-y-3">
                {rows.map((p) => (
                  <PurposeCard key={p.id} p={p} />
                ))}
              </ul>
            )}
          </Async>
        </section>

        <section aria-label="Your requests" className="pt-6">
          <h2 className="text-sm font-semibold">Your data requests</h2>
          <p className="mt-1 text-xs text-muted-foreground">
            Each request has a statutory response deadline. We tell you if something cannot be
            actioned, and why.
          </p>
          <Async state={requests} rows={3}>
            {(rows) => (
              <ul className="mt-3 divide-y rounded-lg border bg-surface">
                {rows.map((r) => (
                  <li key={r.id} className="p-4">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs text-muted-foreground">{r.id}</span>
                      <span className="text-sm font-medium">{r.type}</span>
                      <StatusBadge status={r.status} />
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">{r.scope}</p>
                    <p className="mt-2 text-xs">
                      <span className="text-muted-foreground">Next: </span>
                      {r.nextAction}
                      <span className="text-muted-foreground"> · due {r.dueDate}</span>
                      <span className="text-muted-foreground"> · statutory deadline {r.statutoryDeadline}</span>
                    </p>
                    {r.legalHold ? (
                      <p className="mt-2 flex gap-2 rounded-md border border-warning/40 bg-warning-soft p-2 text-xs text-warning">
                        <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                        <span>{r.legalHold}</span>
                      </p>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}
          </Async>
        </section>
      </AppShell>
    </AuthGate>
  );
}

function CreateRetentionDialog({
  open,
  onOpenChange,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [saving, setSaving] = useState(false);
  const [recordType, setRecordType] = useState("");
  const [months, setMonths] = useState("84");
  const [description, setDescription] = useState("");

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Add retention rule</DialogTitle>
          <DialogDescription>
            Decide how long a category of records is kept. Examples: contract, payslip, attendance,
            leave, case, document, letter.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="grid gap-2">
            <Label htmlFor="retention-type">Record type</Label>
            <Input
              id="retention-type"
              placeholder="e.g. payslip"
              value={recordType}
              onChange={(e) => setRecordType(e.target.value)}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="retention-months">Retention (months)</Label>
            <Input
              id="retention-months"
              type="number"
              min={1}
              max={600}
              value={months}
              onChange={(e) => setMonths(e.target.value)}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="retention-desc">Description (optional)</Label>
            <Input
              id="retention-desc"
              placeholder="e.g. ZRA requires payslips to be kept for 5 years"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="ghost" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={async () => {
              if (!recordType.trim()) {
                feedback.blocked("Record type is required", "Name the category of records the rule covers.");
                return;
              }
              const retentionMonths = Math.max(1, Math.min(600, Number(months) || 84));
              setSaving(true);
              try {
                await realApi.createRetentionRule({
                  recordType: recordType.trim().toLowerCase(),
                  retentionMonths,
                  description: description.trim() || null,
                });
                feedback.submitted(
                  "Retention rule added",
                  `${recordType.trim()} records will now be kept for ${retentionYears(retentionMonths)}.`,
                );
                setRecordType("");
                setMonths("84");
                setDescription("");
                onSaved();
                onOpenChange(false);
              } catch (err) {
                feedback.blocked(
                  "Could not add the rule",
                  err instanceof Error ? err.message : "Unknown error",
                );
              } finally {
                setSaving(false);
              }
            }}
            disabled={saving}
          >
            {saving ? "Saving…" : "Add rule"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
