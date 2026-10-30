import { createFileRoute, Navigate } from "@tanstack/react-router";
import { AuthGate } from "@/platform/components/AuthGate";
import { useAuth } from "@/platform/auth";
import { canManageCore } from "@/platform/entry-route";

export const Route = createFileRoute("/core/")({
  head: () => ({ meta: [{ title: "ERP Core" }] }),
  component: CoreEntry,
});

function CoreEntry() {
  const { user, coreActions, coreAccessLoading } = useAuth();
  if (import.meta.env.VITE_USE_REAL_API === "true" && coreAccessLoading)
    return <AuthGate module="authenticated"><p role="status" className="p-6">Checking Core access…</p></AuthGate>;
  return <AuthGate module="authenticated">{!canManageCore(user?.roles ?? []) && coreActions.includes("core-currency-precision-read")
    ? <Navigate to="/core/workspace/settings/$slug" params={{ slug: "currencies" }} replace />
    : <Navigate to="/core/workspace/settings" replace />}</AuthGate>;
}
