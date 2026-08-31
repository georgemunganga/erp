import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { AlertTriangle, ArrowRight, KeyRound, LifeBuoy, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import {
  getSession,
  handleLoginCallback,
  isSessionValid,
  startInteractiveLogin,
  startSilentSso,
} from "@/platform/oidc";

export const Route = createFileRoute("/sign-in")({
  head: () => ({
    meta: [
      { title: "Sign in — Mightyfin ERP" },
      { name: "description", content: "Sign in to the HR workspace." },
      { property: "og:title", content: "Sign in — Mightyfin ERP" },
      { property: "og:description", content: "Sign in to the HR workspace." },
    ],
  }),
  component: SignIn,
});

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";

/**
 * ERP-hosted login page (M12 — hybrid auth).
 *
 * Behaviour on load:
 * 1. If the user landed back from Keycloak with `?code`, the PKCE exchange
 *    runs immediately and, on success, the user is returned to where they
 *    were heading.
 * 2. Otherwise a silent SSO attempt (`prompt=none`) is fired: if Keycloak
 *    already has a session the user is logged in without ever seeing this
 *    page for long; if Keycloak replies `login_required`, the hosted form
 *    stays visible and the "Sign in with your organisation account" button
 *    drives the interactive redirect flow.
 *
 * Credentials are never entered or validated by the ERP — the email field
 * is informational only, and password handling belongs to the IdP's hosted
 * login form. The demo branch (VITE_USE_REAL_API=false) keeps the old mock
 * explorer behaviour so the build stays usable without an IdP.
 */
function SignIn() {
  const navigate = useNavigate();
  const { setRole } = useApp();
  const { authenticated } = useAuth();
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [silenceFailed, setSilenceFailed] = useState(false);
  const callbackInProgress = useRef(false);

  // (1) Handle every redirect back from Keycloak before considering another
  // silent attempt. In particular, `login_required` is the expected response
  // when no SSO cookie exists and must leave the hosted form stable.
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (USE_REAL && (params.has("code") || params.has("error"))) {
      callbackInProgress.current = true;
      void handleLoginCallback().then((origin) => {
        // The callback stores the new OIDC session outside React state. Force
        // one clean document load so AuthProvider restores that session before
        // AuthGate evaluates the protected destination. A client-side navigate
        // leaves AuthProvider's in-memory session null and bounces the user
        // straight back to /sign-in even though token exchange succeeded.
        if (origin) window.location.replace(origin);
        else setSilenceFailed(true);
      });
    }
  }, [navigate]);

  // (2) Auto-login whenever a valid session exists.
  useEffect(() => {
    if (!USE_REAL) return;
    if (authenticated) {
      void navigate({ to: "/hrm", replace: true });
      return;
    }
    if (callbackInProgress.current) return;
    // A callback is already being handled by the effect above. Starting a new
    // authorization request here would replace its PKCE state and loop.
    const params = new URLSearchParams(window.location.search);
    if (params.has("code") || params.has("error")) return;
    // Only fire the silent round-trip once, and only if no attempt already
    // came back with `login_required` on this visit.
    if (silenceFailed) return;
    const session = getSession();
    if (isSessionValid(session)) return;
    startSilentSso(window.location.pathname === "/sign-in" ? "/hrm" : window.location.pathname);
  }, [authenticated, navigate, silenceFailed]);

  const enterWithOrganisation = () => {
    setBusy(true);
    startInteractiveLogin("/hrm");
  };

  const continueDemo = () => {
    setRole("hr_admin");
    void navigate({ to: "/" });
  };

  /* ---------------------------------------------------------------- demo */

  if (!USE_REAL) {
    return (
      <div className="grid min-h-screen lg:grid-cols-2">
        <div className="hidden flex-col justify-between bg-rail p-10 text-rail-foreground lg:flex">
          <div
            className="flex h-24 w-36 shrink-0 items-start justify-start"
            data-testid="signin-brand-logo-container"
          >
            <img
              src="/mightyfin-logo-light.png"
              alt="Mightyfin ERP"
              data-testid="signin-brand-logo"
              className="block max-h-full max-w-full object-contain object-left"
            />
          </div>
          <div className="max-w-md">
            <h1 className="text-2xl font-semibold">Human resources</h1>
            <p className="mt-3 text-sm text-rail-muted">
              One place for your profile, leave, attendance, pay and requests.
            </p>
          </div>
          <p className="text-xs text-rail-muted">Demonstration build — no real accounts.</p>
        </div>
        <main className="flex items-center justify-center px-4 py-12 sm:px-8">
          <div className="w-full max-w-sm">
            <h2 className="text-xl font-semibold">Sign in</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              Demo mode — choose a role to explore the app as that kind of user.
            </p>
            <Button className="mt-6 w-full" onClick={continueDemo}>
              Enter the workspace
            </Button>
          </div>
        </main>
      </div>
    );
  }

  /* -------------------------------------------------------------- real */

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand / context panel */}
      <div className="hidden flex-col justify-between bg-rail p-10 text-rail-foreground lg:flex">
        <div
          className="flex h-24 w-36 shrink-0 items-start justify-start"
          data-testid="signin-brand-logo-container"
        >
          <img
            src="/mightyfin-logo-light.png"
            alt="Mightyfin ERP"
            data-testid="signin-brand-logo"
            className="block max-h-full max-w-full object-contain object-left"
          />
        </div>
        <div className="max-w-md">
          <h1 className="text-2xl font-semibold">Human resources</h1>
          <p className="mt-3 text-sm text-rail-muted">
            One place for your profile, leave, attendance, pay and requests — and for the people who
            administer them.
          </p>
          <ul className="mt-6 space-y-2 text-sm text-rail-muted">
            <li className="flex gap-2">
              <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
              Access is scoped to your role, entity and branch.
            </li>
            <li className="flex gap-2">
              <KeyRound className="mt-0.5 size-4 shrink-0" aria-hidden />
              Credentials stay with your organisation's identity provider.
            </li>
          </ul>
        </div>
        <p className="text-xs text-rail-muted">
          Secure sign-in via the platform identity provider.
        </p>
      </div>

      {/* Sign-in panel */}
      <main className="flex items-center justify-center px-4 py-12 sm:px-8">
        <div className="w-full max-w-sm">
          <div className="lg:hidden">
            <div className="flex items-center gap-2">
              <img src="/mightyfin-mark.png" alt="" className="size-5" aria-hidden />
              <span className="font-semibold">Mightyfin ERP</span>
            </div>
          </div>

          <h2 className="mt-6 text-xl font-semibold lg:mt-0">Sign in</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Use your organisation account. If you are already signed in with the platform identity
            provider you will be taken straight in.
          </p>

          <Button className="mt-6 w-full" onClick={enterWithOrganisation} disabled={busy}>
            {busy ? (
              "Checking your session\u2026"
            ) : (
              <>
                Continue with organisation account
                <ArrowRight className="size-4" aria-hidden />
              </>
            )}
          </Button>

          <div className="my-6 flex items-center gap-3">
            <span className="h-px flex-1 bg-border" />
            <span className="text-xs text-muted-foreground">Your work email</span>
            <span className="h-px flex-1 bg-border" />
          </div>

          <div className="space-y-4">
            <div>
              <Label htmlFor="email">Work email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                className="mt-1"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@mightyfinance.co.zm"
              />
              <p className="mt-1 text-xs text-muted-foreground">
                For reference only — your organisation's identity provider verifies who you are.
              </p>
            </div>

            <div className="rounded-lg border border-warning/40 bg-warning-soft p-3">
              <p className="flex gap-2 text-xs text-warning">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                <span>
                  No password is requested or stored here. Pressing the button above opens your
                  organisation's secure sign-in page, where MFA and password policies still apply.
                </span>
              </p>
            </div>
          </div>

          <p className="mt-6 flex items-center justify-center gap-1.5 text-xs text-muted-foreground">
            <LifeBuoy className="size-3.5" aria-hidden />
            Need to report something confidentially?{" "}
            <a href="/speak-up" className="text-primary underline underline-offset-2">
              Speak up without signing in
            </a>
          </p>
        </div>
      </main>
    </div>
  );
}
