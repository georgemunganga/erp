import { createFileRoute } from "@tanstack/react-router";
import { ArrowRight, Clock, Loader2, LogOut } from "lucide-react";
import { modules } from "@/platform/modules";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { AuthGate } from "@/platform/components/AuthGate";
import { Button } from "@/components/ui/button";
import { canAccessProcurement, hasHrmStaffRole, useAuth } from "@/platform/auth";
import { canManageCore } from "@/platform/entry-route";
import { SignIn } from "./sign-in";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "ERP workspace" },
      { name: "description", content: "Sign in to your ERP workspace." },
      { property: "og:title", content: "ERP workspace" },
      { property: "og:description", content: "Sign in to your ERP workspace." },
    ],
  }),
  component: Entrance,
});

function Entrance() {
  const { loading, authenticated, erpAuthorized, coreAccessLoading, coreActions, user, signOut } = useAuth();
  const real = import.meta.env.VITE_USE_REAL_API === "true";
  const administrator = canManageCore(user?.roles ?? []);
  const roles = user?.roles ?? [];

  if (real && (loading || authenticated && !administrator && coreAccessLoading)) return <div className="flex min-h-screen items-center justify-center gap-2 text-sm text-muted-foreground"><Loader2 className="size-4 animate-spin" /> Checking your session…</div>;
  if (real && !authenticated) return <SignIn />;
  if (real && !erpAuthorized) return <AuthGate module="erp"><span /></AuthGate>;

  return (
    <div className="min-h-screen bg-background">
      <header className="border-b border-primary/40 bg-primary text-primary-foreground shadow-sm">
        <div className="mx-auto flex h-14 max-w-5xl items-center justify-between gap-2 px-4">
          <BrandIdentity onDark />
          {real ? <Button variant="ghost" size="sm" className="shrink-0 text-primary-foreground hover:bg-white/10 hover:text-primary-foreground" onClick={() => signOut()}>
            <LogOut className="size-4" aria-hidden />
            Switch account
          </Button> : null}
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-12">
        <h1 className="text-2xl font-semibold text-foreground">Choose a module</h1>
        <p className="mt-1 text-sm text-muted-foreground">Choose the workspace you want to manage.</p>

        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {modules.map((m) =>
            m.available && (m.id === "core" ? administrator || coreActions.includes("core-currency-precision-read") : m.id === "hrm" ? hasHrmStaffRole(roles) : m.id === "procurement" ? canAccessProcurement(roles) : true) ? (
              <a
                key={m.id}
                href={m.to}
                className="group flex flex-col gap-2 rounded-lg border bg-surface p-5 transition-colors hover:border-primary hover:bg-primary-soft"
              >
                <div className="flex items-start justify-between gap-3">
                  <span className="text-base font-semibold text-foreground">{m.label}</span>
                  <ArrowRight
                    className="mt-0.5 size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-primary"
                    aria-hidden
                  />
                </div>
                <p className="text-sm text-muted-foreground">{m.description}</p>
              </a>
            ) : (
              <div
                key={m.id}
                className="flex cursor-not-allowed flex-col gap-2 rounded-lg border p-5 opacity-60"
                aria-disabled="true"
                title={`${m.label} — ${m.available ? "access not assigned" : "not enabled"}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <span className="text-base font-semibold text-foreground">{m.label}</span>
                  <span className="inline-flex shrink-0 items-center gap-1 rounded-full border border-info/30 bg-info-soft px-2 py-0.5 text-[10px] font-medium text-info">
                    <Clock className="size-3" aria-hidden />
                    {m.available ? "Access not assigned" : "Not enabled"}
                  </span>
                </div>
                <p className="text-sm text-muted-foreground">{m.description}</p>
              </div>
            ),
          )}
        </div>
      </main>
    </div>
  );
}
