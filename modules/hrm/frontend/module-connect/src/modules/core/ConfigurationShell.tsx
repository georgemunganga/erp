import { useRouterState } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { AppShell } from "@/platform/components/AppShell";
import { CoreAdminGate } from "@/platform/components/AuthGate";
import { CoreShell } from "./CoreShell";

/** Reuses the existing live editor while its backend route is migrated to ERP Core. */
export function ConfigurationShell({ children }: { children: ReactNode }) {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  return pathname.startsWith("/core") ? <CoreAdminGate><CoreShell>{children}</CoreShell></CoreAdminGate> : <AppShell>{children}</AppShell>;
}
