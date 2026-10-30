/**
 * Authentication provider for the ERP web shell.
 *
 * Drives the hybrid login experience agreed in the auth design document
 * (`deployment/auth/README.md`):
 *
 * - **Hybrid mode (production, VITE_USE_REAL_API=true).** On every app load
 *   the shell checks for a locally-stored Keycloak session and refreshes it
 *   silently. Routes that require a user are gated: without a session the
 *   user is sent to the ERP-hosted `/sign-in` page, which first tries silent
 *   SSO (`prompt=none`) and otherwise shows the email/password form that
 *   hands off to the Keycloak hosted login via PKCE.
 * - **Demo mode (local dev with VITE_USE_REAL_API=false).** The gate is
 *   skipped so the mock app remains explorable without an IdP; the shell
 *   continues to use the mock role picker in AppProvider.
 *
 * Identity is always owned by the IdP — the shell never stores or validates
 * credentials itself.
 */
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { Role } from "@/mock/types";
import { hrmApi, type LinkedWorker, type LocalAuthUser } from "@/platform/api-client";
import { parseCoreWorkspaceChoices, type CoreWorkspaceChoices } from "@/modules/core/core-workspace-context";
import {
  clearSession,
  decodeSessionUser,
  ensureFreshSession,
  getSession,
  isSessionValid,
  signOut as oidcSignOut,
  type OidcSession,
  type OidcUser,
} from "@/platform/oidc";

import { INITIAL_AUTH_SETTINGS, loadAuthSettings, type AuthSettings } from "@/platform/auth-settings";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";
const EMPTY_CORE_ACTIONS: CoreWorkspaceChoices["coreActions"] = [];

interface AuthState {
  authSettings: AuthSettings;
  /** Real OIDC session when the hybrid flow is active; null in demo mode. */
  session: OidcSession | null;
  user: OidcUser | null;
  loading: boolean;
  /** True when the user is allowed past the sign-in gate. */
  authenticated: boolean;
  /** True when the identity has an HRM role or shared ERP administration. */
  authorized: boolean;
  /** True when the identity is assigned to any ERP module. */
  erpAuthorized: boolean;
  /** Map Keycloak realm roles to the shell's demo workspace role. */
  resolveRole: () => Role;
  signInInteractive: () => void;
  signInLocal: (email: string, password: string) => Promise<void>;
  signOut: () => void;
  /**
   * M14 identity link: the HRM worker record bound to the caller's Keycloak
   * subject (resolved via `GET /hrm/me`). Null when signed in but not yet
   * linked to a worker, or in demo mode. The shell shows this real identity
   * (name, employee number, photo) wherever a user line appears.
   */
  worker: LinkedWorker | null;
  /** True while the initial `/hrm/me` resolution is in flight. */
  resolvingWorker: boolean;
  coreWorkspace: CoreWorkspaceChoices | null;
  coreAccessLoading: boolean;
  coreActions: CoreWorkspaceChoices["coreActions"];
}

const Ctx = createContext<AuthState | null>(null);

/**
 * Realm roles the ERP understands. A shared IdP identity carrying only roles
 * for another product is authenticated but is not admitted to HRM.
 */
export const HRM_STAFF_ROLES = [
  "employee",
  "manager",
  "hr_ops",
  "payroll",
  "finance_approver",
  "hr_admin",
  "investigator",
  "timesheet_operator",
] as const;

export function hasHrmStaffRole(roles: string[]): boolean {
  const set = new Set(roles.map((role) => role.toLowerCase()));
  return HRM_STAFF_ROLES.some((role) => set.has(role));
}

const PROCUREMENT_ROLES = ["procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor"];

export function canAccessProcurement(roles: string[]): boolean {
  const set = new Set(roles.map((role) => role.toLowerCase()));
  return ["erp_admin", "employee", "manager", ...PROCUREMENT_ROLES].some((role) => set.has(role));
}

export function hasErpModuleRole(roles: string[]): boolean {
  const set = new Set(roles.map((role) => role.toLowerCase()));
  return set.has("erp_admin") || hasHrmStaffRole(roles) || PROCUREMENT_ROLES.some((role) => set.has(role));
}

export function isTimesheetOnly(roles: string[]): boolean {
  const set = new Set(roles.map((role) => role.toLowerCase()));
  return set.has("timesheet_operator") && !HRM_STAFF_ROLES.some((role) => role !== "timesheet_operator" && set.has(role));
}

function mapRolesToDemoRole(roles: string[]): Role {
  const set = new Set(roles);
  // Users can hold several operational roles. Resolve the most privileged
  // workspace first so an HR administrator who also processes payroll keeps
  // access to Configuration and other administrator-only navigation.
  if (set.has("hr_admin") || set.has("investigator")) return "hr_admin";
  if (set.has("hr_ops")) return "hr_ops";
  if (set.has("payroll") || set.has("finance_approver")) return "payroll";
  if (set.has("manager")) return "manager";
  return "employee";
}

/** Public paths that never need a session (speak-up must stay anonymous). */
const PUBLIC_PATHS = new Set(["/sign-in", "/speak-up"]);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [authSettings, setAuthSettings] = useState(INITIAL_AUTH_SETTINGS);
  const [session, setSession] = useState<OidcSession | null>(null);
  const [localUser, setLocalUser] = useState<LocalAuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [worker, setWorker] = useState<LinkedWorker | null>(null);
  const [resolvingWorker, setResolvingWorker] = useState(false);
  const [origin, setOrigin] = useState<string>("/hrm");
  const [coreContext, setCoreContext] = useState<{ actor: string; choices: CoreWorkspaceChoices | null } | null>(null);

  // 1. Restore / refresh the Keycloak session on boot (hybrid mode only).
  useEffect(() => {
    if (!USE_REAL) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      const settings = await loadAuthSettings();
      if (!settings.oidcEnabled) clearSession();
      const fresh = settings.oidcEnabled ? await ensureFreshSession() : null;
      let restoredLocal: LocalAuthUser | null = null;
      if (!fresh && settings.localEnabled) {
        try {
          const local = await hrmApi.auth.me();
          restoredLocal = local.authenticated ? local.user : null;
        } catch {
          restoredLocal = null;
        }
      }
      if (!cancelled) {
        setAuthSettings(settings);
        setSession(fresh);
        setLocalUser(restoredLocal);
        setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // 2. Resolve the signed-in identity to an HRM worker record (M14 link).
  //    Runs after the session is up and never blocks the gate: a real user
  //    who isn't linked yet simply sees their IdP name until HR links them.
  useEffect(() => {
    if (!USE_REAL || (!isSessionValid(session) && !localUser)) return;
    const roles = session ? decodeSessionUser(session)?.roles ?? [] : localUser?.roles ?? [];
    if (isTimesheetOnly(roles)) return;
    let cancelled = false;
    setResolvingWorker(true);
    (async () => {
      try {
        const profile = await hrmApi.myProfile();
        if (!cancelled && profile.linked) setWorker(profile.worker);
      } catch {
        // Backend temporarily unreachable or session revoked mid-flight —
        // stay signed in (the session is still valid) and retry later.
      } finally {
        if (!cancelled) setResolvingWorker(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [session, localUser]);

  // Remember where the user was heading so a login round-trip can return them
  // to the same place (e.g. a deep link like /hrm/leave/abc).
  useEffect(() => {
    if (!loading && !isSessionValid(session) && !localUser) {
      const path = window.location.pathname;
      setOrigin(PUBLIC_PATHS.has(path) ? "/hrm" : path);
    }
  }, [loading, session, localUser]);

  const user = useMemo<OidcUser | null>(() => {
    if (session) return decodeSessionUser(session);
    if (!localUser) return null;
    return {
      subject: localUser.id,
      email: localUser.email,
      preferredUsername: localUser.email,
      name: localUser.displayName,
      roles: localUser.roles,
    };
  }, [session, localUser]);

  const authenticated = !USE_REAL || isSessionValid(session) || Boolean(localUser);
  useEffect(() => {
    if (!USE_REAL || !authenticated || !user?.subject) return;
    let cancelled = false;
    const actor = user.subject;
    hrmApi.get<unknown>("/core/v1/workspace-context")
      .then(parseCoreWorkspaceChoices).catch(() => null)
      .then(choices => { if (!cancelled) setCoreContext({ actor, choices }); });
    return () => { cancelled = true; };
  }, [authenticated, user?.subject]);
  const coreWorkspace = authenticated && coreContext && coreContext.actor === user?.subject ? coreContext.choices : null;
  const coreAccessLoading = USE_REAL && authenticated && !!user?.subject && coreContext?.actor !== user.subject;
  const coreActions = coreWorkspace?.coreActions ?? EMPTY_CORE_ACTIONS;
  const authorized = !USE_REAL || (authenticated && hasHrmStaffRole(user?.roles ?? []));
  const erpAuthorized = !USE_REAL || (authenticated && (hasErpModuleRole(user?.roles ?? [])
    || coreActions.includes("core-currency-precision-read")));

  const signInInteractive = useCallback(() => {
    const path = PUBLIC_PATHS.has(window.location.pathname) ? "/hrm" : window.location.pathname;
    // Imported lazily so demo builds can stay tree-shaken away from unused
    // network code paths; startInteractiveLogin lives next to the flow.
    void import("@/platform/oidc").then((m) => m.startInteractiveLogin(path));
  }, []);

  const signInLocal = useCallback(async (email: string, password: string) => {
    if (!authSettings.localEnabled) throw new Error("Local sign-in is not enabled.");
    const result = await hrmApi.auth.login(email, password);
    setLocalUser(result.user);
    setSession(null);
  }, [authSettings.localEnabled]);

  const signOut = useCallback(() => {
    clearSession();
    setSession(null);
    if (localUser) {
      setLocalUser(null);
      void hrmApi.auth.logout().finally(() => window.location.assign("/sign-in"));
      return;
    }
    if (USE_REAL) {
      oidcSignOut("/sign-in");
    }
  }, [localUser]);

  const resolveRole = useCallback((): Role => {
    if (!USE_REAL) return "employee";
    return mapRolesToDemoRole(user?.roles ?? []);
  }, [user]);

  const value = useMemo<AuthState>(
    () => ({
      authSettings,
      session,
      user,
      loading,
      authenticated,
      authorized,
      erpAuthorized,
      resolveRole,
      signInInteractive,
      signInLocal,
      signOut,
      worker,
      resolvingWorker,
      coreWorkspace,
      coreAccessLoading,
      coreActions,
    }),
    [
      authSettings,
      session,
      user,
      loading,
      authenticated,
      authorized,
      erpAuthorized,
      resolveRole,
      signInInteractive,
      signInLocal,
      signOut,
      worker,
      resolvingWorker,
      coreWorkspace,
      coreAccessLoading,
      coreActions,
    ],
  );

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
