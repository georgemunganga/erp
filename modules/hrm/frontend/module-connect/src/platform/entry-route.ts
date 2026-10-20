/** Keep sign-in returns on this ERP host, and reject protocol-relative URLs. */
export function safeEntryPath(value: string | null | undefined): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.includes("\\")) return "/";
  return value;
}

/** ERP Core administration is separate from HRM administration. */
export function canManageCore(roles: string[]): boolean {
  return roles.some((role) => ["erp_admin", "hr_admin"].includes(role.toLowerCase()));
}

/** Shared organization configuration retains the existing HR operations editor. */
export function canManageOrganisation(roles: string[]): boolean {
  return canManageCore(roles) || roles.some((role) => role.toLowerCase() === "hr_ops");
}

/** Counts reuse the workforce read contract only for its existing HRM roles. */
export function canReadOrganisationHeadcounts(roles: string[]): boolean {
  return roles.some((role) => ["hr_ops", "hr_admin", "payroll", "manager"].includes(role.toLowerCase()));
}

/** A Core-only administrator never probes the employee-details endpoint for optional counts. */
export async function optionalOrganisationHeadcounts<T>(roles: string[], load: () => Promise<T>): Promise<T | null> {
  return canReadOrganisationHeadcounts(roles) ? load() : null;
}
