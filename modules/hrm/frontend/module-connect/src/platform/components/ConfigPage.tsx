import { Link } from "@tanstack/react-router";
import { ChevronLeft, Info } from "lucide-react";
import type { ReactNode } from "react";
import { AppShell } from "./AppShell";
import { PageHeader } from "./PageHeader";
import { RecordTable } from "./RecordTable";

/**
 * Shared shell for a configuration screen: back link to the single hub, a
 * tabbed section switcher, and a consistent "nothing here is saved" notice.
 * Keeps every config screen reading the same way.
 */
export function ConfigPage({
  title,
  description,
  sections,
  active,
  onSelect,
  children,
  notice = "Nothing on this screen is saved in this build. Changing a value here would normally be recorded with who changed it, when, and what it was before.",
}: {
  title: string;
  description: string;
  sections: { id: string; label: string }[];
  active: string;
  onSelect: (id: string) => void;
  children: ReactNode;
  notice?: string;
}) {
  return (
    <AppShell>
      <Link
        to="/hrm/workspace/settings"
        className="inline-flex items-center gap-1.5 text-xs text-muted-foreground transition-colors hover:text-primary"
      >
        <ChevronLeft className="size-3.5" aria-hidden />
        All configuration
      </Link>

      <PageHeader eyebrow="Configuration" title={title} description={description} />

      <div className="flex flex-wrap gap-2" role="tablist" aria-label={`${title} sections`}>
        {sections.map((s) => (
          <button
            key={s.id}
            role="tab"
            aria-selected={active === s.id}
            onClick={() => onSelect(s.id)}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              active === s.id
                ? "border-primary bg-primary-soft font-medium text-primary"
                : "bg-surface text-muted-foreground hover:border-border-strong"
            }`}
          >
            {s.label}
          </button>
        ))}
      </div>

      {children}

      <p className="flex gap-2 text-xs text-muted-foreground">
        <Info className="mt-0.5 size-3.5 shrink-0" aria-hidden />
        {notice}
      </p>
    </AppShell>
  );
}

/** Plain table wrapper so every config list looks and behaves the same. */
export function ConfigTable({
  caption,
  headers,
  rows,
  minWidth = "34rem",
}: {
  caption: string;
  headers: string[];
  rows: ReactNode[][];
  minWidth?: string;
}) {
  return (
    <div className="overflow-x-auto rounded-lg border bg-surface">
      <RecordTable style={{ minWidth }}>
        <caption className="sr-only">{caption}</caption>
        <thead className="border-b bg-surface-muted">
          <tr>
            {headers.map((h) => (
              <th
                key={h}
                scope="col"
                className="whitespace-nowrap px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground"
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y">
          {rows.map((r, i) => (
            <tr key={i}>
              {r.map((cell, j) =>
                j === 0 ? (
                  <th key={j} scope="row" className="px-3 py-2 text-left font-normal">
                    {cell}
                  </th>
                ) : (
                  <td key={j} className="px-3 py-2 align-top">
                    {cell}
                  </td>
                ),
              )}
            </tr>
          ))}
        </tbody>
      </RecordTable>
    </div>
  );
}
