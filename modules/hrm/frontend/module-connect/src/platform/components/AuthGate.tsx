import { useEffect } from "react";
import { Loader2, LogOut, ShieldAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import { canManageCore } from "@/platform/entry-route";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/**
 * Renders children only when the user is authenticated.
 *
 * - Demo mode (VITE_USE_REAL_API=false): always passes through.
 * - Real mode: while the session is being bootstrapped a neutral loading
 *   state renders (this is what makes silent SSO feel seamless); once the
 *   session fails, the user is sent to the ERP-hosted `/sign-in` page
 *   rather than an error screen.
 *
 * Admitted workforce identities are also mirrored into the shell's workspace
 * role so every existing page gate keeps working. Authenticated identities
 * without an HRM workforce role receive an explicit access-denied screen.
 */
export function AuthGate({ children, module = "hrm" }: { children: React.ReactNode; module?: "hrm" | "erp" | "authenticated" }) {
  const { loading, authenticated, authorized, erpAuthorized, user, signOut, resolveRole } = useAuth();
  const admitted = module === "authenticated" ? authenticated : module === "erp" ? erpAuthorized : authorized;
  const { role, setRole } = useApp();

  useEffect(() => {
    if (!USE_REAL || loading || !authenticated || !admitted || !user || module === "authenticated") return;
    const next = resolveRole();
    if (next !== role) setRole(next);
  }, [loading, authenticated, admitted, user, resolveRole, role, setRole, module]);

  useEffect(() => {
    if (!USE_REAL || loading) return;
    if (!authenticated) {
      const returnTo = window.location.pathname + window.location.search + window.location.hash;
      window.location.replace(`/sign-in?returnTo=${encodeURIComponent(returnTo)}`);
    }
  }, [loading, authenticated]);

  if (!USE_REAL) return <>{children}</>;
  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" aria-hidden />
          Checking your session…
        </div>
      </div>
    );
  }
  if (!authenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background px-4">
        <div className="max-w-sm text-center">
          <ShieldAlert className="mx-auto size-8 text-muted-foreground" aria-hidden />
          <p className="mt-4 text-sm text-muted-foreground">Taking you to sign in…</p>
        </div>
      </div>
    );
  }

  if (!admitted) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background px-4">
        <div className="max-w-md text-center" data-testid="erp-access-denied">
          <ShieldAlert className="mx-auto size-9 text-danger" aria-hidden />
          <h1 className="mt-4 text-xl font-semibold text-foreground">{module === "erp" ? "ERP access not assigned" : "HRM access not assigned"}</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            {module === "erp" ? "Your account is valid, but it has no ERP module role. Ask an administrator to assign access to the workspace you need." : "Your account is valid, but it has no HRM role. Ask an administrator to assign the appropriate HRM access."}
          </p>
          <Button className="mt-6" variant="outline" onClick={() => signOut()}>
            Sign out
          </Button>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}

/** Shared settings currently use the HR administrator role supported by the API. */
export function CoreAdminGate({ children }: { children: React.ReactNode }) {
  const { user, authenticated, loading } = useAuth();
  if (!USE_REAL) return <>{children}</>;
  return <AuthGate module="erp">{loading || !authenticated ? null : canManageCore(user?.roles ?? []) ? children : <div className="flex min-h-screen items-center justify-center bg-background px-4">
    <div className="max-w-md rounded-xl border bg-surface p-6 text-center">
      <ShieldAlert className="mx-auto size-8 text-danger" aria-hidden />
      <h1 className="mt-3 text-xl font-semibold">Core settings access required</h1>
      <p className="mt-2 text-sm text-muted-foreground">Your account can sign in, but it does not have the administrator role needed to manage shared settings.</p>
      <Button className="mt-5" variant="outline" onClick={() => window.location.assign("/")}>Choose a module</Button>
    </div>
  </div>}</AuthGate>;
}

/** Small header affordance: who is signed in + sign out. */
export function SignedInBadge() {
  const { user, signOut } = useAuth();
  const label = user?.preferredUsername ?? user?.email ?? "Organisation account";
  return (
    <div className="flex items-center gap-2">
      <span className="hidden truncate text-xs text-muted-foreground sm:inline" title={label}>
        {label}
      </span>
      <Button variant="ghost" size="sm" className="h-8 gap-1.5" onClick={() => signOut()}>
        <LogOut className="size-3.5" aria-hidden />
        Sign out
      </Button>
    </div>
  );
}
