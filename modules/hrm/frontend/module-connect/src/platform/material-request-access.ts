/** Display permission only; the existing Procurement APIs remain authoritative. */
export function hasProcurementModuleGrant(value: unknown, principalId: string, companyId: string): boolean {
  if (!value || typeof value !== "object" || !principalId || !companyId) return false;
  const row = value as Record<string, unknown>;
  const permissions = row.permissions;
  return row.principalId === principalId && row.legalEntityId === companyId
    && Array.isArray(permissions) && permissions.every(action => typeof action === "string")
    && permissions.includes("procurement-access");
}

export function mayCreateMaterialRequest(value: unknown, principalId: string, companyId: string): boolean {
  if (!hasProcurementModuleGrant(value, principalId, companyId)) return false;
  const permissions = (value as { permissions: string[] }).permissions;
  return ["procurement-request-read", "procurement-request-create"].every(action => permissions.includes(action));
}
