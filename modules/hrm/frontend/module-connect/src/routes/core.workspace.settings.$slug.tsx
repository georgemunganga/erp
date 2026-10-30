import { createFileRoute, Outlet, useRouterState } from "@tanstack/react-router";
import { ArrowLeft, CircleDashed } from "lucide-react";
import { AuthGate, CoreAdminGate } from "@/platform/components/AuthGate";
import { CoreShell } from "@/modules/core/CoreShell";
import { findCoreSetting } from "@/modules/core/settings";
import { CurrencyPrecisionsPage } from "@/modules/core/CurrencyPrecisionsPage";
import { BrandingPage } from "./hrm.configuration.branding";
import { OrganisationConfig } from "./hrm.configuration.organisation";
import { UsersConfiguration } from "./hrm.configuration.users";
import { RolesConfig } from "./hrm.configuration.roles";
import { BranchAccessPage } from "./hrm.configuration.branch-access";

export const Route = createFileRoute("/core/workspace/settings/$slug")({
  head: ({ params }) => ({ meta: [{ title: `${(params.slug === "currencies" ? "Currencies" : findCoreSetting(params.slug)?.label ?? params.slug)} — ERP Core` }] }),
  component: CoreSettingPage,
});

function CoreSettingPage() {
  const { slug } = Route.useParams();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  if (slug === "currencies") return <AuthGate module="authenticated"><CoreShell currencyContext><CurrencyPrecisionsPage /></CoreShell></AuthGate>;
  if (slug === "branding") return <BrandingPage />;
  if (slug === "organization") return <OrganisationConfig />;
  if (slug === "users") return pathname.startsWith("/core/workspace/settings/users/") ? <Outlet /> : <UsersConfiguration />;
  if (slug === "roles") return <RolesConfig />;
  if (slug === "branch-access") return <CoreAdminGate><CoreShell><BranchAccessPage /></CoreShell></CoreAdminGate>;

  const setting = findCoreSetting(slug);
  return <CoreAdminGate><CoreShell>
    <a href="/core/workspace/settings" className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-primary"><ArrowLeft className="size-4" /> All settings</a>
    {setting ? <div className="mx-auto max-w-3xl space-y-5">
      <div><p className="text-xs font-semibold uppercase tracking-wider text-primary">{setting.group}</p><h1 className="mt-1 text-2xl font-semibold tracking-tight">{setting.label}</h1><p className="mt-2 text-sm text-muted-foreground">{setting.detail}</p></div>
      <section className="rounded-xl border bg-surface p-6 shadow-sm">
        <div className="flex items-center gap-3"><span className="grid size-10 place-items-center rounded-lg bg-primary-soft text-primary"><CircleDashed className="size-5" /></span><h2 className="font-semibold">Editor coming soon</h2></div>
        <p className="mt-4 text-sm leading-6 text-muted-foreground">This setting is listed here so it is easy to find. Its editor has not been connected yet, so changes cannot be saved from this page.</p>
      </section>
    </div> : <section className="rounded-xl border bg-surface p-6"><h1 className="text-xl font-semibold">Setting not found</h1><p className="mt-2 text-sm text-muted-foreground">Choose a setting from the directory.</p></section>}
  </CoreShell></CoreAdminGate>;
}
