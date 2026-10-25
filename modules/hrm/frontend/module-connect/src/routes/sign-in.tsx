import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useRef, useState } from "react";
import { AlertTriangle, ArrowRight, KeyRound, LifeBuoy, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useApp } from "@/platform/app-context";
import { useAuth } from "@/platform/auth";
import { safeEntryPath } from "@/platform/entry-route";
import { ApiError, hrmApi } from "@/platform/api-client";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { WorkEmailInput, completeWorkEmail, isValidWorkEmail } from "@/platform/components/WorkEmailInput";
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
      { title: "Sign in — ERP workspace" },
      { name: "description", content: "Sign in to your ERP workspace." },
      { property: "og:title", content: "Sign in — ERP workspace" },
      { property: "og:description", content: "Sign in to your ERP workspace." },
      { name: "referrer", content: "no-referrer" },
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
 * Organisation credentials stay in the IdP. The secondary local-account form
 * is only for users created in the shared identity service because no shared
 * directory identity exists yet. The demo branch keeps the mock explorer.
 */
export function SignIn() {
  const navigate = useNavigate();
  const { setRole } = useApp();
  const { authenticated, signInLocal, authSettings, loading } = useAuth();
  const organisationLogin = authSettings.oidcEnabled;
  const localLogin = authSettings.localEnabled;
  const { branding } = useBranding();
  const [email, setEmail] = useState("");
  const [emailDomain, setEmailDomain] = useState<string | null>(null);
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [localBusy, setLocalBusy] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  const [showReset, setShowReset] = useState(false);
  const [resetBusy, setResetBusy] = useState(false);
  const [resetRequested, setResetRequested] = useState(false);
  const [resetError, setResetError] = useState<string | null>(null);
  const [setupPassword, setSetupPassword] = useState("");
  const [setupPasswordConfirmation, setSetupPasswordConfirmation] = useState("");
  const [setupBusy, setSetupBusy] = useState(false);
  const [setupComplete, setSetupComplete] = useState(false);
  const [silenceFailed, setSilenceFailed] = useState(false);
  const callbackInProgress = useRef(false);
  const credentialToken = typeof window === "undefined"
    ? ""
    : new URLSearchParams(window.location.search).get("token") ?? "";
  const returnTo = typeof window === "undefined"
    ? "/"
    : safeEntryPath(new URLSearchParams(window.location.search).get("returnTo"));
  const selectedEmailDomain = emailDomain || branding?.companyDomain?.trim() || "custom";
  const emailDomainChoices = [branding?.companyDomain, "gmail.com", "outlook.com", "yahoo.com"]
    .filter((value): value is string => Boolean(value));

  const updateEmail = (value: string) => {
    setEmail(value);
    if (value.includes("@")) setEmailDomain("custom");
  };

  const chooseEmailDomain = (domain: string) => {
    // Radix form synchronization can emit an empty value while branding loads.
    // It is not a user selection and must not hide the domain picker.
    if (!domain) return;
    setEmailDomain(domain);
    if (domain === "custom") {
      setEmail((current) => current && !current.includes("@") ? `${current}@` : current);
    } else {
      setEmail((current) => current.includes("@") ? current.split("@")[0] : current);
    }
  };

  // (1) Handle every redirect back from Keycloak before considering another
  // silent attempt. In particular, `login_required` is the expected response
  // when no SSO cookie exists and must leave the hosted form stable.
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (USE_REAL && organisationLogin && (params.has("code") || params.has("error"))) {
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
  }, [navigate, organisationLogin]);

  // (2) Auto-login whenever a valid session exists.
  useEffect(() => {
    if (!USE_REAL || loading || !authSettings.ready) return;
    if (credentialToken) return;
    if (authenticated) {
      window.location.replace(returnTo);
      return;
    }
    if (!organisationLogin || callbackInProgress.current) return;
    // A callback is already being handled by the effect above. Starting a new
    // authorization request here would replace its PKCE state and loop.
    const params = new URLSearchParams(window.location.search);
    if (params.has("code") || params.has("error")) return;
    // Only fire the silent round-trip once, and only if no attempt already
    // came back with `login_required` on this visit.
    if (silenceFailed) return;
    const session = getSession();
    if (isSessionValid(session)) return;
    startSilentSso(returnTo);
  }, [authenticated, authSettings.ready, credentialToken, loading, organisationLogin, returnTo, silenceFailed]);

  const enterWithOrganisation = () => {
    setBusy(true);
    startInteractiveLogin(returnTo);
  };

  const enterWithLocalAccount = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLocalBusy(true);
    setLocalError(null);
    try {
      const fullEmail = completeWorkEmail(email, selectedEmailDomain === "custom" ? null : selectedEmailDomain);
      if (!isValidWorkEmail(fullEmail)) throw new Error("Enter a valid email address.");
      await signInLocal(fullEmail, password);
      window.location.assign(returnTo);
    } catch (error) {
      setLocalError(error instanceof ApiError ? error.message : error instanceof Error ? error.message : "Local sign-in failed.");
    } finally {
      setLocalBusy(false);
    }
  };

  const requestPasswordReset = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setResetError(null);
    const fullEmail = completeWorkEmail(email, selectedEmailDomain === "custom" ? null : selectedEmailDomain);
    if (!isValidWorkEmail(fullEmail)) {
      setResetError("Enter a valid email address.");
      return;
    }
    setResetBusy(true);
    try {
      await hrmApi.auth.requestPasswordReset(fullEmail);
      setResetRequested(true);
    } catch (error) {
      setResetError(error instanceof ApiError && error.status === 429
        ? "Too many requests. Please wait and try again."
        : "We couldn't process this request right now. Please try again later.");
    } finally {
      setResetBusy(false);
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
            <h1 className="text-2xl font-semibold">ERP workspace</h1>
            <p className="mt-3 text-sm text-rail-muted">
              One sign-in for your assigned workspaces.
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
          <h1 className="text-2xl font-semibold">ERP workspace{branding?.companyName ? ` at ${branding.companyName}` : ""}</h1>
          <p className="mt-3 text-sm text-rail-muted">
            Sign in once to reach the workspaces and settings assigned to your account.
          </p>
          <ul className="mt-6 space-y-2 text-sm text-rail-muted">
            <li className="flex gap-2">
              <ShieldCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
              Access is scoped to your role, entity and branch.
            </li>
            <li className="flex gap-2">
              <KeyRound className="mt-0.5 size-4 shrink-0" aria-hidden />
              Your account access is managed by your organization.
            </li>
          </ul>
        </div>
        <p className="text-xs text-rail-muted">
          Secure {branding?.companyName || "ERP workspace"} sign-in.
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
                Complete your ERP account setup. This one-time link expires after 24 hours.
              </p>
              {setupComplete ? (
                <div className="mt-6 space-y-4">
                  <p className="rounded-lg border border-primary/30 bg-primary/5 p-3 text-sm text-primary" role="status">
                    Your password is ready. You can now sign in with your ERP account.
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
          <h2 className="mt-6 text-xl font-semibold lg:mt-0">{showReset ? "Reset your password" : loginHeadingFor(branding)}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {showReset ? "Enter your account email and we’ll send you a link to set a new password." : branding?.loginDescription || (organisationLogin ? "Use your organisation account or local account." : "Use your local account.")}
          </p>

          {!showReset && organisationLogin ? <Button className="mt-6 w-full" onClick={enterWithOrganisation} disabled={busy}>
            {busy ? (
              "Checking your session\u2026"
            ) : (
              <>
                Continue with organisation account
                <ArrowRight className="size-4" aria-hidden />
              </>
            )}
          </Button> : null}

          {!showReset && organisationLogin && localLogin ? <div className="my-6 flex items-center gap-3">
            <span className="h-px flex-1 bg-border" />
            <span className="text-xs text-muted-foreground">Local account</span>
            <span className="h-px flex-1 bg-border" />
          </div> : null}

          {showReset && localLogin ? <div className="mt-6 space-y-4">
            {resetRequested ? <p className="rounded-lg border border-primary/30 bg-primary/5 p-3 text-sm text-primary" role="status">
              If this email has an active local account, we’ll send a reset link. Check your inbox and spam folder.
            </p> : <form className="space-y-4" onSubmit={requestPasswordReset}>
              <div>
                <Label htmlFor="reset-email">Email address</Label>
                <div className="mt-1"><WorkEmailInput id="reset-email" autoComplete="email" value={email} onChange={updateEmail} domain={branding?.companyDomain} domainChoices={emailDomainChoices} selectedDomain={selectedEmailDomain} onDomainChange={chooseEmailDomain} required /></div>
              </div>
              {resetError ? <p className="text-sm text-destructive" role="alert">{resetError}</p> : null}
              <Button type="submit" className="w-full" disabled={resetBusy}>{resetBusy ? "Sending link…" : "Email reset link"}</Button>
            </form>}
            <Button type="button" variant="ghost" className="w-full" onClick={() => { setShowReset(false); setResetError(null); setResetRequested(false); }}>Back to sign in</Button>
          </div> : null}

          {!showReset && localLogin ? <form className="space-y-4" onSubmit={enterWithLocalAccount}>
            <div>
              <Label htmlFor="email">Email address</Label>
              <div className="mt-1"><WorkEmailInput
                id="email"
                autoComplete="username"
                value={email}
                onChange={updateEmail}
                domain={branding?.companyDomain}
                domainChoices={emailDomainChoices}
                selectedDomain={selectedEmailDomain}
                onDomainChange={chooseEmailDomain}
                placeholder={branding?.companyDomain ? undefined : emailPlaceholderFor(branding)}
              /></div>
              <p className="mt-1 text-xs text-muted-foreground">Select the @ domain to choose Gmail, Outlook, Yahoo, or another email address.</p>
              {organisationLogin ? <p className="mt-1 text-xs text-muted-foreground">Use this when your organization gave you a local account.</p> : null}
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

            <div className="text-right"><button type="button" className="text-sm font-medium text-primary underline underline-offset-2" onClick={() => { setShowReset(true); setLocalError(null); }}>Forgot password?</button></div>

            {localError ? <p className="text-sm text-destructive" role="alert">{localError}</p> : null}

            <Button type="submit" variant={organisationLogin ? "outline" : "default"} className="w-full" disabled={localBusy}>
              {localBusy ? "Signing in…" : "Sign in"}
            </Button>

            {organisationLogin ? <div className="rounded-lg border border-warning/40 bg-warning-soft p-3">
              <p className="flex gap-2 text-xs text-warning">
                <AlertTriangle className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                <span>
                  Prefer the organisation account above whenever one exists. Local accounts are
                  managed in the current identity service and should be migrated to the shared identity directory later.
                </span>
              </p>
            </div> : null}
          </form> : null}
          {USE_REAL && !authSettings.ready ? <p className="mt-6 text-sm text-muted-foreground" role="status">Loading sign-in options…</p> : null}
          {USE_REAL && authSettings.ready && !organisationLogin && !localLogin ? <div className="mt-6" role="alert"><p className="text-sm text-destructive">Sign-in is temporarily unavailable. Please reload or contact support.</p><Button className="mt-3" variant="outline" onClick={() => window.location.reload()}>Reload</Button></div> : null}
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
