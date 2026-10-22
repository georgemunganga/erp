/** Public invalidation only. No account IDs, credentials, tokens, or authorization claims. */
export const AUTH_CONTEXT_KEY = "erp.auth-context.v1";
export const AUTH_CONTEXT_EVENT = "erp:auth-context-changed";
export function notifyAuthContextChanged(): void {
  // Cross-tab storage is best effort; same-tab notification also works without storage.
  try { localStorage.setItem(AUTH_CONTEXT_KEY, crypto.randomUUID()); } catch { /* No identity fallback. */ }
  window.dispatchEvent(new Event(AUTH_CONTEXT_EVENT));
}
