import { RecordTable } from "@/platform/components/RecordTable";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { Play, Plus, Share2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { extrasApi } from "@/mock/extras";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { Async } from "@/platform/components/Async";
import { PageHeader } from "@/platform/components/PageHeader";
import { useMock } from "@/platform/use-mock";

export const Route = createFileRoute("/hrm/reports/builder")({
  head: () => ({
    meta: [
      { title: "Report builder — Newworldcargo HRM" },
      { name: "description", content: "Build a report by picking a base, fields and filters. It only ever returns what you could already see." },
      { property: "og:title", content: "Report builder — Newworldcargo HRM" },
      { property: "og:description", content: "Build a report by picking a base, fields and filters." },
    ],
  }),
  component: BuilderPage,
});

const bases: Record<string, string[]> = {
  Employees: ["Name", "Employee number", "Entity", "Branch", "Department", "Employment type", "Status", "Start date", "End date", "Manager", "Grade"],
  Leave: ["Reference", "Employee", "Type", "From", "To", "Days", "Status", "Approver"],
  Attendance: ["Reference", "Employee", "Branch", "Date", "Recorded", "Claimed", "Status"],
  Positions: ["Position", "Grade", "Entity", "Branch", "Incumbent", "Status", "Licence", "Expires"],
};

function BuilderPage() {
  const saved = useMock(() => extrasApi.savedReports());
  const [base, setBase] = useState("Employees");
  const [fields, setFields] = useState<string[]>(["Name", "Entity", "Employment type", "End date"]);

  const available = bases[base] ?? [];

  return (
    <AuthGate>
      <AppShell>
      <PageHeader
        eyebrow="Reports"
        title="Report builder"
        description="Pick a base, add fields, add filters. A report never shows more than you could already see."
        primaryAction={
          <Button className="gap-2">
            <Play className="size-4" aria-hidden />
            Run
          </Button>
        }
      />

      <section aria-label="Build" className="grid gap-4 lg:grid-cols-[16rem_minmax(0,1fr)]">
        <div className="space-y-4 rounded-lg border bg-surface p-4">
          <div>
            <Label htmlFor="base">Start from</Label>
            <Select
              value={base}
              onValueChange={(v) => {
                setBase(v);
                setFields((bases[v] ?? []).slice(0, 4));
              }}
            >
              <SelectTrigger id="base" className="mt-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {Object.keys(bases).map((b) => (
                  <SelectItem key={b} value={b}>
                    {b}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div>
            <p className="text-sm font-medium">Add a field</p>
            <ul className="mt-2 space-y-1">
              {available
                .filter((f) => !fields.includes(f))
                .map((f) => (
                  <li key={f}>
                    <button
                      type="button"
                      onClick={() => setFields((s) => [...s, f])}
                      className="flex w-full items-center gap-1.5 rounded-md px-2 py-1 text-left text-xs text-muted-foreground transition-colors hover:bg-surface-muted hover:text-foreground"
                    >
                      <Plus className="size-3 shrink-0" aria-hidden />
                      {f}
                    </button>
                  </li>
                ))}
              {available.filter((f) => !fields.includes(f)).length === 0 ? (
                <li className="px-2 text-xs text-muted-foreground">All fields added.</li>
              ) : null}
            </ul>
          </div>
        </div>

        <div className="space-y-4">
          <div className="rounded-lg border bg-surface p-4">
            <p className="text-sm font-medium">Columns</p>
            {fields.length ? (
              <ul className="mt-2 flex flex-wrap gap-2">
                {fields.map((f) => (
                  <li key={f}>
                    <button
                      type="button"
                      onClick={() => setFields((s) => s.filter((x) => x !== f))}
                      className="inline-flex items-center gap-1.5 rounded-full border bg-surface-muted px-2.5 py-1 text-xs transition-colors hover:border-danger/40 hover:text-danger"
                      aria-label={`Remove ${f}`}
                    >
                      {f}
                      <X className="size-3 shrink-0" aria-hidden />
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-2 text-xs text-muted-foreground">
                No columns yet. Add at least one from the left.
              </p>
            )}
          </div>

          <div className="rounded-lg border bg-surface p-4">
            <p className="text-sm font-medium">Preview</p>
            <div className="mt-2 overflow-x-auto">
              <RecordTable variant="compact" className="w-full text-left text-xs">
                <caption className="sr-only">Preview of the report columns</caption>
                <thead className="border-b bg-surface-muted">
                  <tr>
                    {fields.map((f) => (
                      <th key={f} scope="col" className="whitespace-nowrap px-2 py-1.5 font-semibold uppercase tracking-wide text-muted-foreground">
                        {f}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  <tr className="border-b">
                    {fields.map((f) => (
                      <td key={f} className="px-2 py-1.5 text-muted-foreground">
                        —
                      </td>
                    ))}
                  </tr>
                </tbody>
              </RecordTable>
            </div>
            <p className="mt-2 text-xs text-muted-foreground">
              Run to fill this in. Results are limited to your entity and branch access.
            </p>
          </div>
        </div>
      </section>

      <section aria-label="Saved reports">
        <h2 className="text-sm font-semibold">Saved reports</h2>
        <Async state={saved} rows={3}>
          {(rows) => (
            <ul className="mt-3 space-y-2">
              {rows.map((r) => (
                <li key={r.id} className="flex flex-wrap items-center gap-3 rounded-lg border bg-surface p-3">
                  <span className="min-w-0 flex-1">
                    <span className="block text-sm font-medium">{r.name}</span>
                    <span className="block text-xs text-muted-foreground">
                      {r.base} · {r.fields.length} columns · {r.filters.length} filters · last run {r.lastRun}
                    </span>
                  </span>
                  <span className="inline-flex shrink-0 items-center gap-1.5 text-xs text-muted-foreground">
                    <Share2 className="size-3.5 shrink-0" aria-hidden />
                    {r.shared}
                  </span>
                  <Button size="sm" variant="outline" className="shrink-0">
                    Open
                  </Button>
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
