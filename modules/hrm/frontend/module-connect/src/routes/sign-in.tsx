import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { AlertTriangle, ArrowRight, KeyRound, LifeBuoy, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import { ApiError, hrmApi } from "@/platform/api-client";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { useBranding } from "@/platform/branding";
import { emailPlaceholderFor, loginHeadingFor } from "@/platform/branding-copy";
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
      { title: "Sign in — HR workspace" },
      { name: "description", content: "Sign in to the HR workspace." },
      { property: "og:title", content: "Sign in — HR workspace" },
      { property: "og:description", content: "Sign in to the HR workspace." },
    ],
  }),
  component: SignIn,
});

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";
const ORGANISATION_LOGIN = ["oidc", "hybrid"].includes(
  (import.meta.env.VITE_HRM_AUTH_MODE as string | undefined)?.trim().toLowerCase() ?? "local",
);

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
 * Organisation credentials stay in the IdP. The secondary local-account form
 * is only for users HR explicitly created inside this HRMS because no shared
 * directory identity exists yet. The demo branch keeps the mock explorer.
 */
function SignIn() {
  const navigate = useNavigate();
  const { setRole } = useApp();
  const { authenticated, signInLocal } = useAuth();
  const { branding } = useBranding();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [localBusy, setLocalBusy] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [setupPassword, setSetupPassword] = useState("");
  const [setupPasswordConfirmation, setSetupPasswordConfirmation] = useState("");
  const [setupBusy, setSetupBusy] = useState(false);
  const [setupComplete, setSetupComplete] = useState(false);
  const [silenceFailed, setSilenceFailed] = useState(false);
  const callbackInProgress = useRef(false);
  const credentialToken = typeof window === "undefined"
    ? ""
    : new URLSearchParams(window.location.search).get("token") ?? "";

  // (1) Handle every redirect back from Keycloak before considering another
  // silent attempt. In particular, `login_required` is the expected response
  // when no SSO cookie exists and must leave the hosted form stable.
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (USE_REAL && ORGANISATION_LOGIN && (params.has("code") || params.has("error"))) {
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
    if (!USE_REAL || !ORGANISATION_LOGIN) return;
    if (credentialToken) return;
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
  }, [authenticated, credentialToken, navigate, silenceFailed]);

  const enterWithOrganisation = () => {
    setBusy(true);
    startInteractiveLogin("/hrm");
  };

  const enterWithLocalAccount = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLocalBusy(true);
    setLocalError(null);
    try {
      await signInLocal(email, password);
      window.location.assign("/hrm");
    } catch (error) {
      setLocalError(error instanceof ApiError ? error.message : "Local HRMS sign-in failed.");
    } finally {
      setLocalBusy(false);
    }
  };

  const completePasswordSetup = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLocalError(null);
    if (setupPassword !== setupPasswordConfirmation) {
      setLocalError("The passwords do not match.");
      return;
    }
    if (setupPassword.length < 12) {
      setLocalError("Use at least 12 characters for your password.");
      return;
    }
    setSetupBusy(true);
    try {
      await hrmApi.auth.setPassword(credentialToken, setupPassword);
      window.history.replaceState({}, "", "/sign-in");
      setSetupComplete(true);
      setSetupPassword("");
      setSetupPasswordConfirmation("");
    } catch (error) {
      setLocalError(error instanceof ApiError ? error.message : "Unable to set the account password.");
    } finally {
      setSetupBusy(false);
    }
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
            className="flex h-24 max-w-xs shrink-0 items-center gap-3"
            data-testid="signin-brand-logo-container"
          >
            <BrandIdentity onDark logoClassName="max-h-20 max-w-36 object-contain" nameClassName="text-xl font-semibold" />
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
          className="flex h-24 max-w-xs shrink-0 items-center gap-3"
          data-testid="signin-brand-logo-container"
        >
          <BrandIdentity onDark logoClassName="max-h-20 max-w-36 object-contain" nameClassName="text-xl font-semibold" />
        </div>
        <div className="max-w-md">
          <h1 className="text-2xl font-semibold">Human resources{branding?.companyName ? ` at ${branding.companyName}` : ""}</h1>
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
              Your account access is protected and managed by HR.
            </li>
          </ul>
        </div>
        <p className="text-xs text-rail-muted">
          Secure {branding?.companyName || "HR workspace"} sign-in.
        </p>
      </div>

      {/* Sign-in panel */}
      <main className="flex items-center justify-center px-4 py-12 sm:px-8">
        <div className="w-full max-w-sm">
          <div className="lg:hidden">
            <div className="flex items-center gap-2">
              <BrandIdentity logoClassName="h-8 w-auto max-w-[96px] object-contain" />
            </div>
          </div>

          {credentialToken ? (
            <>
              <h2 className="mt-6 text-xl font-semibold lg:mt-0">Set your account password</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                Complete your local HRMS account setup. This one-time link expires after 24 hours.
              </p>
              {setupComplete ? (
                <div className="mt-6 space-y-4">
                  <p className="rounded-lg border border-primary/30 bg-primary/5 p-3 text-sm text-primary" role="status">
                    Your password is ready. You can now sign in with your HRMS local account.
                  </p>
                  <Button className="w-full" onClick={() => window.location.assign("/sign-in")}>Continue to sign in</Button>
                </div>
              ) : (
                <form className="mt-6 space-y-4" onSubmit={completePasswordSetup}>
                  <div>
                    <Label htmlFor="setup-password">New password</Label>
                    <Input id="setup-password" type="password" autoComplete="new-password" className="mt-1" value={setupPassword} onChange={(event) => setSetupPassword(event.target.value)} minLength={12} required />
                    <p className="mt-1 text-xs text-muted-foreground">Use at least 12 characters.</p>
                  </div>
                  <div>
                    <Label htmlFor="setup-password-confirmation">Confirm new password</Label>
                    <Input id="setup-password-confirmation" type="password" autoComplete="new-password" className="mt-1" value={setupPasswordConfirmation} onChange={(event) => setSetupPasswordConfirmation(event.target.value)} minLength={12} required />
                  </div>
                  {localError ? <p className="text-sm text-destructive" role="alert">{localError}</p> : null}
                  <Button type="submit" className="w-full" disabled={setupBusy}>{setupBusy ? "Setting password…" : "Set password"}</Button>
                </form>
              )}
            </>
          ) : (
            <>
          <h2 className="mt-6 text-xl font-semibold lg:mt-0">{loginHeadingFor(branding)}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {branding?.loginDescription || (ORGANISATION_LOGIN ? "Use your organisation account or HRMS local account." : "Use your HRMS local account.")}
          </p>

          {ORGANISATION_LOGIN ? <Button className="mt-6 w-full" onClick={enterWithOrganisation} disabled={busy}>
            {busy ? (
              "Checking your session\u2026"
            ) : (
              <>
                Continue with organisation account
                <ArrowRight className="size-4" aria-hidden />
              </>
            )}
          </Button> : null}

          {ORGANISATION_LOGIN ? <div className="my-6 flex items-center gap-3">
            <span className="h-px flex-1 bg-border" />
            <span className="text-xs text-muted-foreground">HRMS local account</span>
            <span className="h-px flex-1 bg-border" />
          </div> : null}

          <form className="space-y-4" onSubmit={enterWithLocalAccount}>
            <div>
              <Label htmlFor="email">Work email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                className="mt-1"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder={emailPlaceholderFor(branding)}
              />
              {ORGANISATION_LOGIN ? <p className="mt-1 text-xs text-muted-foreground">Use this when HR created an HRMS-local account.</p> : null}
            </div>

            <div>
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                className="mt-1"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder={branding?.passwordPlaceholder || "Enter your password"}
                required
              />
            </div>

            {localError ? <p className="text-sm text-destructive" role="alert">{localError}</p> : null}

            <Button type="submit" variant={ORGANISATION_LOGIN ? "outline" : "default"} className="w-full" disabled={localBusy}>
              {localBusy ? "Signing in…" : "Sign in with HRMS local account"}
            </Button>

            {ORGANISATION_LOGIN ? <div className="rounded-lg border border-warning/40 bg-warning-soft p-3">
              <p className="flex gap-2 text-xs text-warning">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                <span>
                  Prefer the organisation account above whenever one exists. Local accounts are
                  isolated to this HRMS and should be migrated to the shared identity directory later.
                </span>
              </p>
            </div> : null}
          </form>
          {branding?.supportEmail ? <p className="mt-4 text-center text-xs text-muted-foreground">
            Need account help? <a className="text-primary underline underline-offset-2" href={`mailto:${branding.supportEmail}`}>{branding.supportEmail}</a>
          </p> : null}
            </>
          )}

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
