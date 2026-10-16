/** Keep sign-in returns on this ERP host, and reject protocol-relative URLs. */
export function safeEntryPath(value: string | null | undefined): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.includes("\\")) return "/";
  return value;
}

/** The current shared administration role is backed by the HRM identity service. */
export function canManageCore(roles: string[]): boolean {
  return roles.some((role) => role.toLowerCase() === "hr_admin");
}
