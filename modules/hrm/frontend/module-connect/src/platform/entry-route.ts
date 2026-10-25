/** Keep sign-in returns on this ERP host, and reject protocol-relative URLs. */
export function safeEntryPath(value: string | null | undefined): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.includes("\\") || /[\u0000-\u001f\u007f]/.test(value)) return "/";
  const target = new URL(value, "https://erp.invalid");
  if (target.origin !== "https://erp.invalid" || target.pathname.replace(/\/+$/, "") === "/sign-in") return "/";
  return value;
}

/** ERP Core administration is separate from HRM administration. */
export function canManageCore(roles: string[]): boolean {
  return roles.some((role) => ["erp_admin", "hr_admin"].includes(role.toLowerCase()));
}

/** Structure screens retain their HR operations/admin gate across IdP role casing. */
export function canManageHrmStructure(roles: string[]): boolean {
  return roles.some((role) => ["hr_ops", "hr_admin"].includes(role.toLowerCase()));
}
