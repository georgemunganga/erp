import { parseCoreWorkspaceChoices } from "./core-workspace-context";
import { createOperationalClient } from "./product-operational-api";

export const FULL_PRODUCT_HRM = import.meta.env.VITE_ERP_PRODUCT_MODE === "true"
  && import.meta.env.VITE_ERP_FULL_HRM === "true";
type HrmSession = {
  marker: string; csrf: string; principal: string; roles: string[];
  choices: ReturnType<typeof parseCoreWorkspaceChoices>; fullWorkspace: boolean;
};
let current: HrmSession | null = null;
export function clearProductHrmSession() { current = null; }
export async function logoutProductHrmSession() {
  const csrf = current?.csrf;
  clearProductHrmSession();
  if (csrf) await createOperationalClient().logout(csrf);
}
export function productHrmHeaders(extra?: Record<string, string>): Record<string, string> {
  if (!current) throw new Error("Choose your HR workspace from Kuubu to continue.");
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
  const [response, operational] = await Promise.all([
    fetch("/api/core/v1/workspace-context", { credentials: "same-origin", cache: "no-store", redirect: "error",
      headers: { Accept: "application/json", ...(expected ? { "X-Kuubu-Session-Context": expected } : {}) }, signal }),
    createOperationalClient().session(signal),
  ]);
  if (!response.ok) throw new Error("Your HR workspace access could not be confirmed. Return to Kuubu to choose your company.");
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
}
export type ProductHrmSession = HrmSession;
