import { createFileRoute } from "@tanstack/react-router";
import { ArrowRight, Building2, CircleDashed, Settings2 } from "lucide-react";
import { AuthGate } from "@/platform/components/AuthGate";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { CoreShell } from "@/modules/core/CoreShell";
import { coreSettingGroups } from "@/modules/core/settings";

export const Route = createFileRoute("/core/")({
  head: () => ({ meta: [{ title: "ERP Core — Organization and settings" }] }),
  component: CoreHome,
});

function CoreHome() {
  const connected = coreSettingGroups.flatMap((group) => group.items).filter((item) => item.path && item.owner === "ERP Core").length;
  return <AuthGate><CoreShell>
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wider text-primary">ERP Core</p>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight">Organization & Settings</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">One place for company structure and ERP-wide settings. Each business module keeps its own transaction rules.</p>
      </div>
      <div className="flex items-center gap-2 rounded-xl border bg-surface px-4 py-3 text-xs text-muted-foreground"><BrandIdentity showName={false} /><span>{connected} Core editors connected</span></div>
    </div>
    <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
      {coreSettingGroups.map((group) => <section key={group.title} className="rounded-xl border bg-surface p-5 shadow-sm">
        <div className="flex items-center gap-2"><span className="grid size-8 place-items-center rounded-lg bg-primary-soft text-primary">{group.title === "Organization" ? <Building2 className="size-4" /> : <Settings2 className="size-4" />}</span><h2 className="font-semibold">{group.title}</h2></div>
        <p className="mt-2 min-h-10 text-xs leading-5 text-muted-foreground">{group.description}</p>
        <ul className="mt-3 divide-y border-t">
          {group.items.map((item) => <li key={item.label}>
            {item.path ? <a href={item.path} className="group flex items-center justify-between gap-3 py-3 text-sm hover:text-primary">
              <span><span className="block font-medium">{item.label}</span><span className="mt-0.5 block text-xs text-muted-foreground">{item.detail}</span></span><ArrowRight className="size-4 shrink-0 text-muted-foreground group-hover:text-primary" />
            </a> : <div className="flex items-center justify-between gap-3 py-3 text-sm text-muted-foreground">
              <span><span className="block font-medium text-foreground">{item.label}</span><span className="mt-0.5 block text-xs">{item.detail} · {item.owner}</span></span><span className="flex shrink-0 items-center gap-1 text-[11px]"><CircleDashed className="size-3.5" /> Planned</span>
            </div>}
          </li>)}
        </ul>
      </section>)}
    </div>
    <p className="rounded-lg border border-info/30 bg-info-soft px-4 py-3 text-xs text-foreground">Organization, branding and identity editors use the existing live ERP APIs. Financial, template and portal settings remain with their owning modules until their shared contracts are ready.</p>
  </CoreShell></AuthGate>;
}
