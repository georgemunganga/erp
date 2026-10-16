import { createFileRoute, Outlet, useRouterState } from "@tanstack/react-router";
import { BriefcaseBusiness, FileCheck2, ShieldCheck, Workflow } from "lucide-react";
import { configurationGroups } from "@/modules/hrm/nav";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { SettingsDirectory, type SettingsDirectoryGroup } from "@/platform/components/SettingsDirectory";

const icons = [BriefcaseBusiness, Workflow, ShieldCheck, FileCheck2];
const groups: SettingsDirectoryGroup[] = configurationGroups.map((group, index) => ({
  title: group.label,
  description: group.description,
  icon: icons[index],
  items: group.items,
}));

export const Route = createFileRoute("/hrm/workspace/settings")({
  head: () => ({ meta: [{ title: "Settings — HRM" }] }),
  component: HrmSettingsDirectory,
});

function HrmSettingsDirectory() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  return pathname.startsWith("/hrm/workspace/settings/")
    ? <Outlet />
    : <AuthGate><AppShell><SettingsDirectory moduleName="HRM" description="Set up employment rules, leave, payroll, approvals and HRM integrations." groups={groups} /></AppShell></AuthGate>;
}
