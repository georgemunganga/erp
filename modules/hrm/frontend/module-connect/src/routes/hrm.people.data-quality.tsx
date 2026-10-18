import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import {
  ArrowLeftRight,
  Check,
  Info,
  OctagonAlert,
  RotateCcw,
  TriangleAlert,
  Undo2,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { dataQualityApi } from "@/mock/dataquality";
import type { BulkJob, DuplicateCandidate, QualityRule } from "@/mock/dataquality";
import { realApi, useApi } from "@/platform/use-api";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { StatusBadge } from "@/platform/components/StatusBadge";
import { useMock } from "@/platform/use-mock";
import { feedback } from "@/platform/feedback";

export const Route = createFileRoute("/hrm/people/data-quality")({
  head: () => ({
    meta: [
      { title: "Data quality — Newworldcargo HRM" },
      { name: "description", content: "Quality rules with real consequences, reversible merges, previewed bulk changes and import reconciliation." },
      { property: "og:title", content: "Data quality — Newworldcargo HRM" },
      { property: "og:description", content: "Quality rules with real consequences, reversible merges, previewed bulk changes and import reconciliation." },
    ],
  }),
  component: DataQualityPage,
});

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

/** Maps the backend `/hrm/dq/checks` envelope to the UI `QualityRule` shape. */
function adaptDqChecks(raw: unknown[]): QualityRule[] {
  const grouped = new Map<string, { failing: number; failingDetail: string[] }>();
  for (const r of raw as Array<{ rule?: string; severity?: string; workerId?: string; detail?: string }>) {
    const rule = r.rule ?? "unknown";
    const entry = grouped.get(rule) ?? { failing: 0, failingDetail: [] };
    entry.failing += 1;
    if (r.workerId && r.detail) entry.failingDetail.push(`${r.workerId.slice(0, 8)}… — ${r.detail}`);
    grouped.set(rule, entry);
  }
  if (grouped.size === 0) {
    return [
      {
        id: "dq-completeness",
        rule: "Record completeness",
        severity: "Advisory",
        scope: "All workers",
        owner: "HR operations",
        passing: 0,
        failing: 0,
        consequence: "No failing records found against the real backend.",
      },
    ];
  }
  return Array.from(grouped.entries()).map(([rule, g]) => ({
    id: `dq-${rule}`,
    rule: `Record ${rule}`,
    severity: "Warning" as const,
    scope: "All workers",
    owner: "HR operations",
    passing: 0,
    failing: g.failing,
    consequence: g.failingDetail.slice(0, 2).join("; "),
  }));
}

const sevMeta = {
  Blocking: { icon: OctagonAlert, cls: "text-danger" },
  Warning: { icon: TriangleAlert, cls: "text-warning" },
  Advisory: { icon: Info, cls: "text-info" },
} as const;

function RuleRow({ r }: { r: QualityRule }) {
  const { icon: Icon, cls } = sevMeta[r.severity];
  const total = r.passing + r.failing;
  const pct = total ? Math.round((r.passing / total) * 100) : 100;
  return (
    <li className="rounded-lg border bg-surface p-4">
      <div className="flex flex-wrap items-start gap-2">
        <Icon className={`mt-0.5 size-4 shrink-0 ${cls}`} aria-hidden />
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-medium">{r.rule}</span>
            <span className="font-mono text-[11px] text-muted-foreground">{r.id}</span>
            <span className={`text-[11px] font-medium ${cls}`}>{r.severity}</span>
          </span>
          <span className="mt-0.5 block text-xs text-muted-foreground">{r.scope} · owner {r.owner}</span>
        </span>
        <span className="shrink-0 text-right">
          <span className="tabular block text-sm font-semibold">
            {r.failing === 0 ? "All passing" : `${r.failing} failing`}
          </span>
          <span className="block text-[11px] text-muted-foreground">
            {r.passing} of {total} ({pct}%)
          </span>
        </span>
      </div>
      <p className="mt-2 flex gap-2 rounded-md border bg-surface-muted p-2 text-xs">
        <span className="font-medium">If it fails:</span>
        <span>{r.consequence}</span>
      </p>
    </li>
  );
}

function DuplicateCard({ d }: { d: DuplicateCandidate }) {
  const [decided, setDecided] = useState<string | null>(null);
  return (
    <li className="rounded-lg border bg-surface p-5">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-xs text-muted-foreground">{d.id}</span>
        <StatusBadge status={d.status === "Open" ? "In review" : d.status === "Merged" ? "Approved" : "Cancelled"} />
        <span className="text-xs text-muted-foreground">
          match confidence {(d.score * 100).toFixed(0)}%
        </span>
        <span className="text-xs font-medium">{d.recommendation}</span>
      </div>

      <div className="mt-3 grid gap-3 sm:grid-cols-[1fr_auto_1fr] sm:items-center">
        <div className="rounded-md border bg-surface-muted p-3">
          <p className="text-sm font-medium">{d.a.name}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">{d.a.detail}</p>
        </div>
        <ArrowLeftRight className="mx-auto size-4 shrink-0 text-muted-foreground" aria-hidden />
        <div className="rounded-md border bg-surface-muted p-3">
          <p className="text-sm font-medium">{d.b.name}</p>
          <p className="mt-0.5 text-xs text-muted-foreground">{d.b.detail}</p>
        </div>
      </div>

      <dl className="mt-3 grid gap-3 text-xs sm:grid-cols-2">
        <div>
          <dt className="font-medium text-muted-foreground">Matched on</dt>
          <dd className="mt-0.5">{d.matchedOn.join(", ")}</dd>
        </div>
        <div>
          <dt className="font-medium text-muted-foreground">Differs on</dt>
          <dd className="mt-0.5">{d.differsOn.join(", ")}</dd>
        </div>
      </dl>

      {d.status === "Open" && !decided ? (
        <div className="mt-4 flex flex-wrap gap-2">
          <Button size="sm" onClick={() => setDecided("merged")}>
            Merge — same person
          </Button>
          <Button size="sm" variant="outline" onClick={() => setDecided("different")}>
            Keep separate — different people
          </Button>
        </div>
      ) : null}

      {decided ? (
        <p className="mt-3 flex gap-2 rounded-md border border-success/30 bg-success-soft p-2 text-xs text-success">
          <Check className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>
            {decided === "merged"
              ? "Recorded as a merge. Both original records are retained, so this is reversible for 90 days."
              : "Recorded as different people. This pair will not be raised again."}{" "}
            Local to this demonstration — nothing is written.
          </span>
        </p>
      ) : null}

      {d.status === "Merged" ? (
        <div className="mt-3 flex flex-wrap items-center gap-3 rounded-md border border-info/30 bg-info-soft p-3">
          <p className="min-w-0 flex-1 text-xs text-info">
            Merged {d.mergedOn}. Both originals were kept, so this can be undone until{" "}
            {d.reversibleUntil}. After that the merge is permanent.
          </p>
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            onClick={() =>
              feedback.note(
                "Merge reversal requested.",
                "The two records are separated again and both keep their history.",
              )
            }
          >
            <Undo2 className="size-3.5" aria-hidden />
            Reverse merge
          </Button>
        </div>
      ) : null}
    </li>
  );
}

function BulkJobCard({ j }: { j: BulkJob }) {
  return (
    <li className="rounded-lg border bg-surface p-5">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-xs text-muted-foreground">{j.id}</span>
        <StatusBadge
          status={
            j.status === "Applied"
              ? "Approved"
              : j.status === "Rejected"
                ? "Rejected"
                : j.status === "Rolled back"
                  ? "Cancelled"
                  : "In review"
          }
        />
        <span className="text-xs text-muted-foreground">{j.status}</span>
      </div>
      <p className="mt-1 text-sm font-medium">{j.what}</p>
      <p className="mt-0.5 text-xs text-muted-foreground">
        {j.scope} · requested by {j.requestedBy}
      </p>

      <div className="mt-3 grid gap-2 sm:grid-cols-3">
        <div className="rounded-md border bg-surface-muted p-2.5">
          <p className="text-[11px] text-muted-foreground">Would change</p>
          <p className="tabular text-lg font-semibold">{j.dryRun.willChange}</p>
        </div>
        <div className="rounded-md border bg-surface-muted p-2.5">
          <p className="text-[11px] text-muted-foreground">Already correct</p>
          <p className="tabular text-lg font-semibold">{j.dryRun.noChange}</p>
        </div>
        <div className={`rounded-md border p-2.5 ${j.dryRun.wouldFail ? "border-danger/40 bg-danger-soft" : "bg-surface-muted"}`}>
          <p className="text-[11px] text-muted-foreground">Would fail</p>
          <p className={`tabular text-lg font-semibold ${j.dryRun.wouldFail ? "text-danger" : ""}`}>
            {j.dryRun.wouldFail}
          </p>
        </div>
      </div>

      {j.dryRun.failReason ? (
        <p className="mt-2 flex gap-2 rounded-md border border-danger/40 bg-danger-soft p-2 text-xs text-danger">
          <OctagonAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
          <span>{j.dryRun.failReason}</span>
        </p>
      ) : null}

      <div className="mt-3 overflow-x-auto">
        <RecordTable className="w-full min-w-[34rem] text-left text-xs">
          <caption className="sr-only">Sample of records this change would affect</caption>
          <thead className="border-b bg-surface-muted">
            <tr>
              <th scope="col" className="px-2 py-1.5 font-semibold uppercase tracking-wide text-muted-foreground">Employee</th>
              <th scope="col" className="px-2 py-1.5 font-semibold uppercase tracking-wide text-muted-foreground">Field</th>
              <th scope="col" className="px-2 py-1.5 font-semibold uppercase tracking-wide text-muted-foreground">Before</th>
              <th scope="col" className="px-2 py-1.5 font-semibold uppercase tracking-wide text-muted-foreground">After</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {j.sample.map((s) => (
              <tr key={s.employee + s.field}>
                <th scope="row" className="px-2 py-1.5 font-normal">
                  <span className="flex items-center gap-1.5">
                    {s.ok ? (
                      <Check className="size-3 shrink-0 text-success" aria-label="Would succeed" />
                    ) : (
                      <X className="size-3 shrink-0 text-danger" aria-label="Would fail" />
                    )}
                    {s.employee}
                  </span>
                </th>
                <td className="px-2 py-1.5">{s.field}</td>
                <td className="px-2 py-1.5 text-muted-foreground">{s.before}</td>
                <td className="px-2 py-1.5">
                  {s.after}
                  {s.note ? <span className="block text-[11px] text-muted-foreground">{s.note}</span> : null}
                </td>
              </tr>
            ))}
          </tbody>
        </RecordTable>
      </div>

      {j.status === "Dry run" ? (
        <div className="mt-3 flex flex-wrap gap-2">
          <Button
            size="sm"
            onClick={() =>
              feedback.submitted(
                "Corrections sent for approval.",
                "A second person checks them before anything changes on the employee record.",
              )
            }
          >
            Send for approval
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={() => feedback.removed("Proposed corrections discarded.")}
          >
            Discard
          </Button>
        </div>
      ) : null}

      {j.status === "Applied" ? (
        <div className="mt-3 flex flex-wrap items-center gap-3 rounded-md border border-info/30 bg-info-soft p-3">
          <p className="min-w-0 flex-1 text-xs text-info">
            Applied {j.appliedOn}. Reversible until {j.reversibleUntil} — the previous values are
            retained until then.
          </p>
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            onClick={() =>
              feedback.note(
                "Roll-back requested.",
                "The batch is reversed as one, so records cannot be left half-corrected.",
              )
            }
          >
            <RotateCcw className="size-3.5" aria-hidden />
            Roll back
          </Button>
        </div>
      ) : null}
    </li>
  );
}

function DataQualityPage() {
  const mockRules = useMock(() => dataQualityApi.rules());
  const realRules = useApi(() => realApi.dqChecks().then((raw) => adaptDqChecks(raw as unknown[])));
  const rules = USE_REAL ? realRules : mockRules;
  const duplicates = useMock(() => dataQualityApi.duplicates());
  const jobs = useMock(() => dataQualityApi.bulkJobs());
  const imports = useMock(() => dataQualityApi.imports());
  const [tab, setTab] = useState<"rules" | "duplicates" | "bulk" | "imports">("rules");

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="People"
        title="Data quality and stewardship"
        description="Every rule states what actually breaks if it fails. Every bulk change is previewed before it commits and stays reversible after."
      />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Data quality views">
        {([
          ["rules", "Quality rules"],
          ["duplicates", "Possible duplicates"],
          ["bulk", "Bulk changes"],
          ["imports", "Import reconciliation"],
        ] as const).map(([id, label]) => (
          <button
            key={id}
            role="tab"
            aria-selected={tab === id}
            onClick={() => setTab(id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              tab === id
                ? "border-primary bg-primary-soft font-medium text-primary"
                : "bg-surface text-muted-foreground hover:border-border-strong"
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "rules" ? (
        <Async state={rules} rows={4}>
          {(rows) => (
            <ul className="space-y-3">
              {rows.map((r) => (
                <RuleRow key={r.id} r={r} />
              ))}
            </ul>
          )}
        </Async>
      ) : null}

      {tab === "duplicates" ? (
        <>
          <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>
              Merging two people who are not the same person is only a recoverable mistake if both
              originals were kept. They are — a merge stays reversible for 90 days.
              {USE_REAL
                ? " Duplicates are shown from the demo mock — the backend exposes only quality checks so far."
                : ""}
            </span>
          </p>
          <Async state={duplicates} rows={3}>
            {(rows) => (
              <ul className="space-y-4">
                {rows.map((d) => (
                  <DuplicateCard key={d.id} d={d} />
                ))}
              </ul>
            )}
          </Async>
        </>
      ) : null}

      {tab === "bulk" ? (
        <>
          <p className="flex gap-2 rounded-md border border-info/30 bg-info-soft p-3 text-sm text-info">
            <Info className="mt-0.5 size-4 shrink-0" aria-hidden />
            <span>
              A bulk change is never applied straight from a filter. It runs as a dry run first,
              showing what would change, what is already correct, and what would fail — with a
              sample you can actually read.
            </span>
          </p>
          <Async state={jobs} rows={3}>
            {(rows) => (
              <ul className="space-y-4">
                {rows.map((j) => (
                  <BulkJobCard key={j.id} j={j} />
                ))}
              </ul>
            )}
          </Async>
        </>
      ) : null}

      {tab === "imports" ? (
        <Async state={imports} rows={2}>
          {(rows) => (
            <ul className="space-y-4">
              {rows.map((i) => (
                <li key={i.id} className="rounded-lg border bg-surface p-5">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-mono text-xs text-muted-foreground">{i.id}</span>
                    <StatusBadge status={i.status === "Reconciled" ? "Approved" : "In review"} />
                    <span className="text-xs text-muted-foreground">{i.status}</span>
                  </div>
                  <p className="mt-1 text-sm font-medium">{i.source}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    Received {i.received} · {i.rows} rows · {i.accepted} accepted · {i.rejected} held
                  </p>

                  {i.exceptions.length ? (
                    <ul className="mt-3 space-y-2">
                      {i.exceptions.map((e) => (
                        <li key={e.row} className="rounded-md border border-warning/40 bg-warning-soft p-2.5 text-xs">
                          <p className="font-medium text-warning">Row {e.row}: {e.problem}</p>
                          <p className="mt-0.5 text-foreground">{e.action}</p>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p className="mt-3 flex gap-2 text-xs text-success">
                      <Check className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                      Every row reconciled. Nothing held for review.
                    </p>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Async>
      ) : null}
    </AppShell>
      </AuthGate>
  );
}
