import { useState } from "react";
import { ArrowRight, Check, Search, SlidersHorizontal, type LucideIcon } from "lucide-react";
import { Input } from "@/components/ui/input";

export type SettingsDirectoryGroup = {
  title: string;
  description: string;
  icon: LucideIcon;
  items: { label: string; detail: string; to?: string; status?: "connected" | "preview" | "planned" }[];
};

/** Shared settings directory UI. Each module supplies only the records it owns. */
export function SettingsDirectory({ moduleName, description, groups }: { moduleName: string; description: string; groups: SettingsDirectoryGroup[] }) {
  const [query, setQuery] = useState("");
  const term = query.trim().toLowerCase();
  const visible = groups.map((group) => ({
    ...group,
    items: group.items.filter((item) => `${group.title} ${item.label} ${item.detail}`.toLowerCase().includes(term)),
  })).filter((group) => group.items.length);
  const availableCount = groups.flatMap((group) => group.items).filter((item) => item.to && item.status !== "preview" && item.status !== "planned").length;

  return <div className="mx-auto max-w-[1240px] space-y-6">
    <header className="rounded-2xl border bg-surface p-5 shadow-sm sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div><p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-primary"><SlidersHorizontal className="size-4" aria-hidden />{moduleName} configuration</p><h1 className="mt-2 text-2xl font-semibold tracking-tight">{moduleName} settings</h1><p className="mt-2 max-w-2xl text-sm text-muted-foreground">{description}</p></div>
        <span className="inline-flex items-center gap-1.5 rounded-full bg-success-soft px-3 py-1.5 text-xs font-medium text-success"><Check className="size-3.5" aria-hidden />{availableCount} connected settings</span>
      </div>
      <div className="relative mt-5 max-w-xl"><Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden /><Input type="search" aria-label="Search settings" placeholder={`Search ${moduleName} settings`} value={query} onChange={(event) => setQuery(event.target.value)} className="h-11 rounded-lg bg-background pl-10" /></div>
    </header>
    <nav aria-label={`${moduleName} settings groups`} className="flex flex-wrap gap-2">{visible.map((group) => <a key={group.title} href={`#${group.title.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "")}`} className="rounded-full border bg-surface px-3 py-1.5 text-xs font-medium text-muted-foreground hover:border-primary hover:text-primary">{group.title}</a>)}</nav>
    <div className="grid items-start gap-4 md:grid-cols-2">
      {visible.map((group) => <section id={group.title.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "")} key={group.title} className="scroll-mt-24 rounded-xl border bg-surface p-5 shadow-sm">
        <div className="mb-4 flex items-center gap-2 rounded-lg bg-primary-soft px-3 py-2 text-sm font-semibold text-primary"><group.icon className="size-4" aria-hidden />{group.title}</div>
        <p className="min-h-9 text-xs leading-5 text-muted-foreground">{group.description}</p>
        <div className="mt-3 divide-y">
          {group.items.map((item) => item.to ? <a key={`${item.label}:${item.to}`} href={item.to} className="group flex items-center justify-between gap-3 py-3 text-sm hover:text-primary">
            <span className="min-w-0"><span className="block font-medium">{item.label}</span><span className="block text-xs leading-5 text-muted-foreground">{item.detail}</span></span><span className="flex shrink-0 items-center gap-1.5"><span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${item.status === "preview" ? "bg-warning-soft text-warning" : item.status === "planned" ? "bg-surface-muted text-muted-foreground" : "bg-success-soft text-success"}`}>{item.status === "preview" ? "Preview" : item.status === "planned" ? "Planned" : "Connected"}</span><ArrowRight className="size-4 text-muted-foreground group-hover:text-primary" aria-hidden /></span>
          </a> : <div key={item.label} className="flex items-center justify-between gap-3 py-3 text-sm text-muted-foreground"><span className="min-w-0"><span className="block font-medium">{item.label}</span><span className="block text-xs leading-5">{item.detail}</span></span><span className="shrink-0 rounded-full bg-surface-muted px-2 py-0.5 text-[10px] font-semibold">Planned</span></div>)}
        </div>
      </section>)}
    </div>
    {!visible.length && <div className="rounded-xl border bg-surface p-10 text-center text-sm text-muted-foreground">No settings match “{query}”.</div>}
  </div>;
}
