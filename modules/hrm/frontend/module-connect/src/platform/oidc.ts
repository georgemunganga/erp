import { notifyAuthContextChanged } from "@/platform/auth-context";
/**
 * Minimal OpenID Connect client for the ERP web shell.
 *
 * Implements just enough of the Authorization Code + PKCE flow to drive the
 * hybrid login experience described in `deployment/auth/README.md`:
 *
 * 1. Silent SSO (`prompt=none`) — when Keycloak already has a session, the
 *    same redirect flow returns with a code instantly and the user never sees
 *    a login screen.
 * 2. ERP-hosted login — when silent SSO fails (`login_required`), the shell
 *    shows its own email/password page and offers a "sign in with your
 *    organisation account" button that starts the interactive redirect flow.
 * 3. Refresh via the refresh token (offline_access); if that fails the
 *    session is cleared and the user is sent back to the login page.
 *
 * No third-party OIDC library is taken on — the shape below is deliberately
 * small, dependency-free and easy to audit. The realm is the
 * `mightyfin-sandbox` realm on the platform Keycloak (26.7).
 */

import { oidcLoginEnabled } from "@/platform/auth-settings";

const AUTHORITY =
  (import.meta.env.VITE_OIDC_AUTHORITY as string | undefined)?.trim() ||
  "https://auth.mightyfinance.co.zm/realms/mightyfin-sandbox";

const CLIENT_ID = (import.meta.env.VITE_OIDC_CLIENT_ID as string | undefined)?.trim() || "erp-web";

const SESSION_KEY = "erp.oidc.session";
const STATE_KEY = "erp.oidc.state";

export interface OidcSession {
  accessToken: string;
  idToken: string;
  refreshToken?: string;
  expiresAt: number; // epoch ms
}

export interface OidcUser {
  subject: string;
  email?: string;
  preferredUsername?: string;
  name?: string;
  roles: string[];
}

export type SilentResult =
  | { ok: true; code?: string }
  | { ok: false; error: "login_required" | "no_session" | "network" | "token_exchange" };

/* ------------------------------------------------------------------ crypto */

function b64url(data: ArrayBufferLike): string {
  const bytes = new Uint8Array(data);
  let bin = "";
  bytes.forEach((b) => (bin += String.fromCharCode(b)));
  return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function randomBytes(n: number): ArrayBuffer {
  return crypto.getRandomValues(new Uint8Array(n)) as unknown as ArrayBuffer;
}

export async function sha256(text: string): Promise<string> {
  const enc = new TextEncoder();
  const hash = await crypto.subtle.digest("SHA-256", enc.encode(text));
  return b64url(hash);
}

/* ------------------------------------------------------------------ utils */

function parseJwtPayload<T = Record<string, unknown>>(token: string): T | null {
  try {
    const payload = token.split(".")[1];
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json) as T;
  } catch {
    return null;
  }
}

/** Decode the id token claims; returns null if the token is malformed. */
export function decodeIdToken(idToken: string): OidcUser | null {
  const claims = parseJwtPayload<{
    sub?: string;
    email?: string;
    preferred_username?: string;
    name?: string;
    realm_access?: { roles?: string[] };
    scope?: string;
  }>(idToken);
  if (!claims?.sub) return null;
  return {
    subject: claims.sub,
    email: claims.email,
    preferredUsername: claims.preferred_username,
    name: claims.name,
    roles: claims.realm_access?.roles ?? [],
  };
}

/**
 * Keycloak only places `realm_access.roles` in the ACCESS token — the id token
 * usually has none. Decode the identity from the id token and enrich roles
 * from the access token so UI role gating (hr_admin/hr_ops) works.
 */
export function decodeSessionUser(session: {
  idToken: string;
  accessToken?: string;
}): OidcUser | null {
  const base = decodeIdToken(session.idToken);
  if (!base) return null;
  if (!session.accessToken) return base;
  const extra = parseJwtPayload<{
    realm_access?: { roles?: string[] };
  }>(session.accessToken);
  return { ...base, roles: extra?.realm_access?.roles ?? base.roles };
}

/* --------------------------------------------------------------- endpoints */

function discovery(): {
  authorizationEndpoint: string;
  tokenEndpoint: string;
  endSessionEndpoint: string;
  jwksUri: string;
} {
  return {
    authorizationEndpoint: `${AUTHORITY}/protocol/openid-connect/auth`,
    tokenEndpoint: `${AUTHORITY}/protocol/openid-connect/token`,
    endSessionEndpoint: `${AUTHORITY}/protocol/openid-connect/logout`,
    jwksUri: `${AUTHORITY}/protocol/openid-connect/certs`,
  };
}

/* --------------------------------------------------------------- storage */

export function getSession(): OidcSession | null {
  if (!oidcLoginEnabled()) return null;
  try {
    const raw = localStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as OidcSession;
    if (!s.accessToken || !s.idToken) return null;
    return s;
  } catch {
    return null;
  }
}

export function saveSession(s: OidcSession): void {
  if (!oidcLoginEnabled()) return;
  localStorage.setItem(SESSION_KEY, JSON.stringify(s));
}

export function clearSession(): void {
  localStorage.removeItem(SESSION_KEY);
  localStorage.removeItem(STATE_KEY);
}

export function isSessionValid(s: OidcSession | null): s is OidcSession {
  return !!s && s.expiresAt > Date.now() + 30_000; // 30s safety margin
}

/* -------------------------------------------------------- PKCE + state */

interface AuthState {
  codeVerifier: string;
  state: string;
  nonce: string;
  redirectUri: string;
  origin: string; // path we were trying to reach before login
}

export function captureAuthState(originPath: string): AuthState {
  const state: AuthState = {
    codeVerifier: b64url(randomBytes(32)),
    state: b64url(randomBytes(16)),
    nonce: b64url(randomBytes(16)),
    redirectUri: `${window.location.origin}${window.location.pathname}`,
    origin: originPath,
  };
  localStorage.setItem(STATE_KEY, JSON.stringify(state));
  return state;
}

export function readAuthState(): AuthState | null {
  try {
    const raw = localStorage.getItem(STATE_KEY);
    return raw ? (JSON.parse(raw) as AuthState) : null;
  } catch {
    return null;
  }
}

export function clearAuthState(): void {
  localStorage.removeItem(STATE_KEY);
}

/** Build the Keycloak authorization URL (silent or interactive). */
export function buildAuthUrl(params: {
  prompt?: "none" | "login" | "consent";
  codeChallenge: string;
  state: string;
  nonce: string;
  redirectUri: string;
  scope?: string;
}): string {
  const { authorizationEndpoint } = discovery();
  const qs = new URLSearchParams({
    client_id: CLIENT_ID,
    response_type: "code",
    redirect_uri: params.redirectUri,
    scope: params.scope ?? "openid email profile offline_access",
    state: params.state,
    nonce: params.nonce,
    code_challenge: params.codeChallenge,
    code_challenge_method: "S256",
  });
  if (params.prompt) qs.set("prompt", params.prompt);
  return `${authorizationEndpoint}?${qs.toString()}`;
}

/* ----------------------------------------------------------------- tokens */

async function exchangeCode(
  code: string,
  codeVerifier: string,
  redirectUri: string,
): Promise<OidcSession> {
  const { tokenEndpoint } = discovery();
  const res = await fetch(tokenEndpoint, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "authorization_code",
      client_id: CLIENT_ID,
      code,
      redirect_uri: redirectUri,
      code_verifier: codeVerifier,
    }).toString(),
  });
  if (!res.ok) {
    const t = await res.text();
    throw new Error(`Token exchange failed (${res.status}): ${t.slice(0, 120)}`);
  }
  const data = (await res.json()) as {
    access_token: string;
    id_token: string;
    refresh_token?: string;
    expires_in?: number;
  };
  return {
    accessToken: data.access_token,
    idToken: data.id_token,
    refreshToken: data.refresh_token,
    expiresAt: Date.now() + (data.expires_in ?? 300) * 1000,
  };
}

/** Exchange the current refresh token for a fresh session. */
async function refreshSession(s: OidcSession): Promise<OidcSession | null> {
  if (!s.refreshToken) return null;
  try {
    const { tokenEndpoint } = discovery();
    const res = await fetch(tokenEndpoint, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "refresh_token",
        client_id: CLIENT_ID,
        refresh_token: s.refreshToken,
      }).toString(),
    });
    if (!res.ok) return null;
    const data = (await res.json()) as {
      access_token: string;
      id_token: string;
      refresh_token?: string;
      expires_in?: number;
    };
    return {
      accessToken: data.access_token,
      idToken: data.id_token,
      refreshToken: data.refresh_token ?? s.refreshToken,
      expiresAt: Date.now() + (data.expires_in ?? 300) * 1000,
    };
  } catch {
    return null;
  }
}

/* ----------------------------------------------------------------- flows */

/**
 * Refresh if the access token is expired or nearly expired. Returns the same
 * session when still valid.
 */
export async function ensureFreshSession(): Promise<OidcSession | null> {
  const s = getSession();
  if (isSessionValid(s)) return s;
  if (!s) return null;
  const fresh = await refreshSession(s);
  if (fresh) {
    saveSession(fresh);
    return fresh;
  }
  clearSession();
  return null;
}

/**
 * Attempt silent SSO: if Keycloak has a session cookie, the redirect comes
 * back with a code; otherwise with `error=login_required`.
 */
export function startSilentSso(originPath: string): void {
  if (!oidcLoginEnabled()) return;
  const st = captureAuthState(originPath);
  void sha256(st.codeVerifier).then((challenge) => {
    window.location.href = buildAuthUrl({
      prompt: "none",
      codeChallenge: challenge,
      state: st.state,
      nonce: st.nonce,
      redirectUri: st.redirectUri,
    });
  });
}

/** Start the interactive (hosted login) redirect flow. */
export function startInteractiveLogin(originPath: string): void {
  if (!oidcLoginEnabled()) return;
  const st = captureAuthState(originPath);
  void sha256(st.codeVerifier).then((challenge) => {
    window.location.href = buildAuthUrl({
      prompt: "login",
      codeChallenge: challenge,
      state: st.state,
      nonce: st.nonce,
      redirectUri: st.redirectUri,
    });
  });
}

/**
 * Handle the redirect back from Keycloak. Parses `code`/`state` (or
 * `error`) from the URL, validates the state against what was stored when the
 * flow started, and exchanges the code for tokens.
 *
 * Returns the origin path to navigate to on success, or null when the flow
 * could not complete (no code, state mismatch, exchange failure).
 */
export async function handleLoginCallback(): Promise<string | null> {
  if (!oidcLoginEnabled()) return null;
  const params = new URLSearchParams(window.location.search);
  const code = params.get("code");
  const state = params.get("state");
  const error = params.get("error");

  if (error) {
    // Silent flow came back with login_required → not a fatal error; the UI
    // decides to render the hosted form. Clear query so reloads stay clean.
    clearAuthState();
    window.history.replaceState(null, "", window.location.pathname);
    return null;
  }
  if (!code || !state) return null;

  const st = readAuthState();
  if (!st || st.state !== state) {
    clearAuthState();
    return null;
  }

  try {
    const session = await exchangeCode(code, st.codeVerifier, st.redirectUri);
    saveSession(session);
    notifyAuthContextChanged();
    const origin = st.origin || "/hrm";
    clearAuthState();
    // Strip the callback params before navigating so the URL stays clean.
    window.history.replaceState(null, "", origin);
    return origin;
  } catch {
    clearAuthState();
    return null;
  }
}

/** Sign out: clear local state and ask Keycloak to end its session. */
export function signOut(nextPath = "/sign-in"): void {
  notifyAuthContextChanged();
  if (!oidcLoginEnabled()) { clearSession(); window.location.href = nextPath; return; }
  const session = getSession();
  clearSession();
  const idTokenHint = session?.idToken;
  const qs = new URLSearchParams({
    id_token_hint: idTokenHint ?? "",
    post_logout_redirect_uri: `${window.location.origin}${nextPath}`,
    client_id: CLIENT_ID,
  });
  // The Keycloak end-session endpoint tolerates a redirect that the client
  // cannot pre-register in sandbox mode; failures here should never throw.
  try {
    window.location.href = `${discovery().endSessionEndpoint}?${qs.toString()}`;
  } catch {
    window.location.href = nextPath;
  }
}
