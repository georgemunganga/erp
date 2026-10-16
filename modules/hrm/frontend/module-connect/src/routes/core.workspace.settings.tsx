import { createFileRoute, Outlet, useRouterState } from "@tanstack/react-router";
import { Building2, SlidersHorizontal, Settings2, ShieldCheck, UsersRound } from "lucide-react";
import { CoreShell } from "@/modules/core/CoreShell";
import { coreSettingGroups, settingPath } from "@/modules/core/settings";
import { CoreAdminGate } from "@/platform/components/AuthGate";
import { SettingsDirectory, type SettingsDirectoryGroup } from "@/platform/components/SettingsDirectory";

const icons = [Building2, UsersRound, ShieldCheck, Settings2, SlidersHorizontal];
const groups: SettingsDirectoryGroup[] = coreSettingGroups.map((group, index) => ({
  title: group.title,
  description: group.description,
  icon: icons[index],
  items: group.items.filter((item) => item.owner === "ERP Core").map((item) => ({ label: item.label, detail: item.detail, to: settingPath(item) })),
})).filter((group) => group.items.length);

export const Route = createFileRoute("/core/workspace/settings")({
  head: () => ({ meta: [{ title: "Settings — ERP Core" }] }),
  component: CoreSettingsDirectory,
});

function CoreSettingsDirectory() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  return pathname.startsWith("/core/workspace/settings/")
    ? <Outlet />
    : <CoreAdminGate><CoreShell><SettingsDirectory moduleName="ERP Core" description="Manage company-wide organization, access, branding and shared defaults in one place." groups={groups} /></CoreShell></CoreAdminGate>;
}
