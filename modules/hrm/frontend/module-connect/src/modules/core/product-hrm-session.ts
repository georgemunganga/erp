import { parseCoreWorkspaceChoices } from "./core-workspace-context";
import { createOperationalClient, OperationalError } from "./product-operational-api";

export class ProductHrmSessionError extends Error {
  readonly temporary: boolean;
  constructor(readonly status: number) {
    const temporary = status === 429 || status >= 500;
    super(temporary ? "We’re temporarily unable to check workspace access. Your form is kept here while we reconnect."
      : status === 409 ? "The selected workspace changed in another tab. Choose your company in Kuubu again."
      : status === 401 ? "Your ERP session has expired. Continue through Kuubu to reconnect."
      : "Your HR permissions or company access have changed. Review your access in Kuubu.");
    this.temporary = temporary;
  }
}

export const FULL_PRODUCT_HRM = import.meta.env.VITE_ERP_PRODUCT_MODE === "true"
  && import.meta.env.VITE_ERP_FULL_HRM === "true";
type HrmSession = {
  marker: string; csrf: string; principal: string; roles: string[];
  choices: ReturnType<typeof parseCoreWorkspaceChoices>; fullWorkspace: boolean;
};
// No eager initializer: a late-loaded API chunk must not reset an already
// verified session during the bundler's cyclic-module initialization.
let current: HrmSession | null | undefined;
export const PRODUCT_HRM_RECHECK_EVENT = "kuubu-hrm-session-recheck";
export function clearProductHrmSession() { current = null; }
export async function logoutProductHrmSession() {
  const csrf = current?.csrf;
  clearProductHrmSession();
  if (csrf) await createOperationalClient().logout(csrf);
}
export function productHrmHeaders(extra?: Record<string, string>): Record<string, string> {
  if (!current) {
    if (typeof window !== "undefined") queueMicrotask(() => window.dispatchEvent(new Event(PRODUCT_HRM_RECHECK_EVENT)));
    throw new Error("Workspace access is being rechecked. No changes were submitted; try saving again after reconnection.");
  }
  // No bearer, default tenant or cached organisation authority in product mode.
  const narrowing: Record<string, string> = {};
  try {
    const shell = JSON.parse(localStorage.getItem("erp.shell.state.v1") || "null");
    const branch = current.choices.branches.find(row => row.id === shell?.branch || row.name === shell?.branch);
    if (branch) narrowing["X-Shell-Location"] = branch.id;
  } catch { /* Invalid preferences cannot change request authority. */ }
  return { ...extra, ...narrowing, Accept: "application/json", "X-Shell-Entity": current.choices.companies[0].id,
    "X-Kuubu-Session-Context": current.marker,
    "X-Kuubu-Operational-CSRF": current.csrf };
}
export async function loadProductHrmSession(signal: AbortSignal, expected?: string): Promise<HrmSession> {
  try {
  // Observe/refresh continuity before reading company context, rather than
  // competing with our own credential-rotation lock through parallel reads.
  const operational = await createOperationalClient().session(signal);
  const response = await fetch("/api/core/v1/workspace-context", { credentials: "same-origin", cache: "no-store", redirect: "error",
    headers: { Accept: "application/json", ...(expected ? { "X-Kuubu-Session-Context": expected } : {}) },
    signal: AbortSignal.any([signal, AbortSignal.timeout(20000)]) });
  if (!response.ok) throw new ProductHrmSessionError(response.status);
  const data = await response.json();
  const choices = parseCoreWorkspaceChoices(data);
  if (data.contextKind !== "product" || data.selectedModule !== "hrm" || typeof data.hrmWorkspace !== "boolean"
    || choices.companies.length !== 1 || choices.companies[0].id !== data.selectedCompanyId
    || typeof data.principalId !== "string" || !Array.isArray(data.moduleRoles)
    || !data.moduleRoles.every((role: unknown) => typeof role === "string")
    || (data.hrmWorkspace ? !data.moduleRoles.includes("hr_admin") : data.workerDirectory !== true)
    || !/^[0-9a-f]{64}$/.test(data.sessionContext) || expected && expected !== data.sessionContext
    || !operational.csrf)
    throw new Error("This HR workflow requires verified HR administrator and company location access. Review your permissions in Kuubu.");
  const result = { choices, principal: data.principalId as string, roles: data.moduleRoles as string[],
    marker: data.sessionContext as string, csrf: operational.csrf, fullWorkspace: data.hrmWorkspace as boolean };
  if (signal.aborted) throw new Error("Workspace check cancelled.");
  current = result;
  return result;
  } catch (error) {
    if (signal.aborted) throw error;
    clearProductHrmSession();
    if (error instanceof ProductHrmSessionError) throw error;
    if (error instanceof OperationalError) throw new ProductHrmSessionError(error.status);
    if (error instanceof TypeError || error instanceof DOMException) throw new ProductHrmSessionError(503);
    throw error;
  }
}
export type ProductHrmSession = HrmSession;
