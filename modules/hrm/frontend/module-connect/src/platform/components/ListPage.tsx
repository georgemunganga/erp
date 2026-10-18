import { RecordTable } from "./RecordTable";
import { ArrowDown, ArrowUp, ArrowUpDown, Columns3, Filter, Search, X } from "lucide-react";
import { useMemo, useState } from "react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { NoResultsState } from "./States";

export interface ColumnDef<T> {
  id: string;
  header: string;
  /** Columns beyond the first 7 are hidden by default and offered in the column picker. */
  defaultVisible?: boolean;
  cell: (row: T) => ReactNode;
  /** Enable local sorting where the page supplies an appropriate value. */
  sortValue?: (row: T) => string | number;
}

export interface FilterDef<T> {
  id: string;
  label: string;
  options: string[];
  match: (row: T, value: string) => boolean;
  /** Optional hierarchical option list (entity › branch › department). Renders
   *  a tree with › separators and entity group labels instead of flat options. */
  treeOptions?: { value: string; label: string; entity?: boolean }[];
}

export interface SavedView {
  id: string;
  label: string;
}

export function ListPage<T extends { id: string }>({
  rows,
  columns,
  filters = [],
  savedViews = [],
  activeView,
  onViewChange,
  searchPlaceholder = "Search",
  searchFields,
  searchValue,
  onSearchChange,
  bulkActions = [],
  rowHref,
  onRowOpen,
  rowLabel,
  toolbarActions,
  footer,
  emptyBody = "No records match the current view.",
}: {
  rows: T[];
  columns: ColumnDef<T>[];
  filters?: FilterDef<T>[];
  savedViews?: SavedView[];
  activeView?: string;
  onViewChange?: (id: string) => void;
  searchPlaceholder?: string;
  searchFields: (row: T) => string;
  /** When supplied, the caller searches the full dataset on the server. */
  searchValue?: string;
  onSearchChange?: (value: string) => void;
  bulkActions?: { label: string; onSelect: (ids: string[]) => void }[];
  rowHref?: (row: T) => ReactNode;
  onRowOpen?: (row: T) => void;
  rowLabel?: (row: T) => string;
  toolbarActions?: ReactNode;
  footer?: ReactNode;
  emptyBody?: string;
}) {
  const [query, setQuery] = useState("");
  const [filterValues, setFilterValues] = useState<Record<string, string>>({});
  const [selected, setSelected] = useState<string[]>([]);
  const [sort, setSort] = useState<{ id: string; direction: "asc" | "desc" } | null>(null);
  const [visible, setVisible] = useState<string[]>(
    columns.filter((c, i) => c.defaultVisible === true || (c.defaultVisible !== false && i < 7)).map((c) => c.id),
  );

  const filtered = useMemo(() => {
    const result = rows.filter((r) => {
      if (!onSearchChange && query && !searchFields(r).toLowerCase().includes(query.toLowerCase())) return false;
      return filters.every((f) => {
        const v = filterValues[f.id];
        return !v || v === "all" || f.match(r, v);
      });
    });
    const column = columns.find((item) => item.id === sort?.id);
    if (column?.sortValue && sort) {
      const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: "base" });
      result.sort((a, b) => {
        const first = column.sortValue!(a);
        const second = column.sortValue!(b);
        const comparison = typeof first === "number" && typeof second === "number"
          ? first - second
          : collator.compare(String(first ?? ""), String(second ?? ""));
        return sort.direction === "asc" ? comparison : -comparison;
      });
    }
    return result;
  }, [rows, query, filterValues, filters, searchFields, onSearchChange, columns, sort]);

  const shown = columns.filter((c) => visible.includes(c.id));
  const allSelected = filtered.length > 0 && filtered.every((row) => selected.includes(row.id));
  const activeFilters = Object.entries(filterValues).filter(([, v]) => v && v !== "all");
  const toggleSort = (id: string) => setSort((current) => current?.id === id
    ? { id, direction: current.direction === "asc" ? "desc" : "asc" }
    : { id, direction: "asc" });
  const isInteractive = (target: EventTarget | null) =>
    target instanceof Element && Boolean(target.closest("a, button, input, select, textarea, [role='button'], [role='checkbox']"));

  return (
    <section className="overflow-hidden rounded-xl border border-border bg-surface shadow-sm" data-testid="erp-record-list">
      {savedViews.length ? (
        <div className="flex flex-wrap items-center gap-1 border-b border-border px-4 pt-2" role="tablist" aria-label="Saved views">
          {savedViews.map((v) => (
            <button
              key={v.id}
              role="tab"
              aria-selected={activeView === v.id}
              onClick={() => onViewChange?.(v.id)}
              className={`border-b-2 px-3 py-3 text-sm font-medium transition-colors ${
                activeView === v.id
                  ? "border-primary text-primary"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              }`}
            >
              {v.label}
            </button>
          ))}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-2 border-b border-border px-4 py-3">
        <div className="relative min-w-44 flex-1 sm:max-w-sm">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden />
          <Input
            value={searchValue ?? query}
            onChange={(e) => onSearchChange ? onSearchChange(e.target.value) : setQuery(e.target.value)}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
            className="h-9 rounded-lg pl-9"
          />
        </div>
        {filters.map((f) => (
          <Select
            key={f.id}
            value={filterValues[f.id] ?? "all"}
            onValueChange={(v) => setFilterValues((s) => ({ ...s, [f.id]: v }))}
          >
            <SelectTrigger className="h-9 w-auto min-w-32 rounded-lg" aria-label={f.label}>
              <Filter className="size-3.5 text-muted-foreground" aria-hidden />
              <SelectValue placeholder={f.label} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{f.label}: all</SelectItem>
              {f.treeOptions
                ? f.treeOptions.map((o) => (
                    <SelectItem
                      key={o.value}
                      value={o.value}
                      className={o.entity ? "font-semibold text-primary" : undefined}
                    >
                      {o.label}
                    </SelectItem>
                  ))
                : f.options.map((o) => (
                    <SelectItem key={o} value={o}>
                      {o}
                    </SelectItem>
                  ))}
            </SelectContent>
          </Select>
        ))}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" size="sm" className="h-9 rounded-lg">
              <Columns3 className="size-4" aria-hidden />
              Columns
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>Visible columns</DropdownMenuLabel>
            {columns.map((c) => (
              <DropdownMenuCheckboxItem
                key={c.id}
                checked={visible.includes(c.id)}
                disabled={visible.length === 1 && visible.includes(c.id)}
                onCheckedChange={(on) => setVisible((s) => on ? [...s, c.id] : s.filter((x) => x !== c.id))}
              >
                {c.header}
              </DropdownMenuCheckboxItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        {toolbarActions}
      </div>

      {activeFilters.length ? (
        <div className="flex flex-wrap items-center gap-2 border-b border-border px-4 py-2 text-xs">
          {activeFilters.map(([k, v]) => (
            <button
              key={k}
              onClick={() => setFilterValues((s) => ({ ...s, [k]: "all" }))}
              className="inline-flex items-center gap-1 rounded-full border bg-background px-2.5 py-1 text-muted-foreground hover:text-foreground"
            >
              {filters.find((f) => f.id === k)?.label}: {v}
              <X className="size-3" aria-hidden />
              <span className="sr-only">Remove filter</span>
            </button>
          ))}
        </div>
      ) : null}

      {selected.length > 0 && bulkActions.length ? (
        <div className="flex flex-wrap items-center gap-2 border-b border-primary/20 bg-primary-soft px-4 py-2">
          <span className="text-sm font-medium text-primary">{selected.length} selected</span>
          {bulkActions.map((a) => (
            <Button key={a.label} size="sm" variant="outline" onClick={() => a.onSelect(selected)}>
              {a.label}
            </Button>
          ))}
          <Button size="sm" variant="ghost" onClick={() => setSelected([])}>
            Clear
          </Button>
        </div>
      ) : null}

      {filtered.length === 0 ? (
        <div className="p-5"><NoResultsState title="No matching records" body={emptyBody} /></div>
      ) : (
        <div className="overflow-x-auto">
          <RecordTable className="w-full min-w-[42rem] border-collapse text-left text-sm">
            <thead className="border-b border-border bg-surface-muted/60">
              <tr>
                <th scope="col" className="w-11 px-4 py-3">
                  <Checkbox
                    aria-label="Select all visible rows"
                    checked={allSelected}
                    onCheckedChange={(on) => setSelected((current) => on
                      ? [...new Set([...current, ...filtered.map((row) => row.id)])]
                      : current.filter((id) => !filtered.some((row) => row.id === id)))}
                  />
                </th>
                {shown.map((c) => (
                  <th key={c.id} scope="col"
                    aria-sort={sort?.id === c.id ? sort.direction === "asc" ? "ascending" : "descending" : undefined}
                    className="whitespace-nowrap px-4 py-3 text-xs font-semibold text-muted-foreground">
                    {c.sortValue ? (
                      <button type="button" onClick={() => toggleSort(c.id)} className="inline-flex items-center gap-1 hover:text-foreground">
                        {c.header}
                        {sort?.id === c.id ? sort.direction === "asc" ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" /> : <ArrowUpDown className="size-3 opacity-50" />}
                      </button>
                    ) : c.header}
                  </th>
                ))}
                {rowHref ? <th scope="col" className="w-20 px-4 py-3 text-right text-xs font-semibold text-muted-foreground">View</th> : null}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {filtered.map((r) => (
                <tr key={r.id}
                  tabIndex={onRowOpen ? 0 : undefined}
                  aria-label={onRowOpen ? rowLabel?.(r) ?? `Open record ${r.id}` : undefined}
                  onClick={onRowOpen ? (event) => { if (!isInteractive(event.target)) onRowOpen(r); } : undefined}
                  onKeyDown={onRowOpen ? (event) => {
                    if ((event.key === "Enter" || event.key === " ") && event.target === event.currentTarget) {
                      event.preventDefault();
                      onRowOpen(r);
                    }
                  } : undefined}
                  className={`transition-colors hover:bg-primary-soft/45 ${onRowOpen ? "cursor-pointer focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary" : ""} ${selected.includes(r.id) ? "bg-primary-soft/35" : ""}`}>
                  <td className="px-4 py-3 align-middle">
                    <Checkbox
                      aria-label={`Select ${rowLabel?.(r) ?? r.id}`}
                      checked={selected.includes(r.id)}
                      onCheckedChange={(on) =>
                        setSelected((s) => (on ? [...new Set([...s, r.id])] : s.filter((x) => x !== r.id)))
                      }
                    />
                  </td>
                  {shown.map((c) => (
                    <td key={c.id} className="px-4 py-3 align-middle text-foreground">
                      {c.cell(r)}
                    </td>
                  ))}
                  {rowHref ? <td className="px-4 py-3 text-right text-sm font-medium text-primary">
                    {typeof rowHref(r) === "string" ? <a href={String(rowHref(r))} className="hover:underline">Open</a> : rowHref(r)}
                  </td> : null}
                </tr>
              ))}
            </tbody>
          </RecordTable>
        </div>
      )}
      <footer className="flex flex-wrap items-center justify-between gap-2 border-t border-border px-4 py-3 text-xs text-muted-foreground">
        <span aria-live="polite">{filtered.length === rows.length ? `${rows.length} records` : `${filtered.length} of ${rows.length} records`}</span>
        {footer}
      </footer>
    </section>
  );
}
