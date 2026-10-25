/** Presentation metadata only. The server remains the grant and authorization authority. */
export interface RolePermissionOption {
  key: string;
  group: string;
  label: string;
  detail?: string;
}

export interface ProcurementRoleCatalog {
  schemaVersion: 1;
  procurement: RolePermissionOption[];
}

export function readProcurementRoleCatalog(value: unknown): ProcurementRoleCatalog {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("The permission catalog is unavailable.");
  const source = value as Record<string, unknown>;
  if (source.schemaVersion !== 1 || !Array.isArray(source.procurement) || source.procurement.length === 0)
    throw new Error("The permission catalog is unavailable.");
  const seen = new Set<string>();
  const procurement = source.procurement.map((item) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) throw new Error("The permission catalog is invalid.");
    const row = item as Record<string, unknown>;
    const key = row.key, label = row.label, group = row.group;
    if (typeof key !== "string" || !/^procurement-[a-z0-9-]+$/.test(key) || seen.has(key)
      || typeof label !== "string" || !label.trim() || label.length > 120
      || typeof group !== "string" || !group.trim() || group.length > 80)
      throw new Error("The permission catalog is invalid.");
    seen.add(key);
    return { key, label: label.trim(), group: group.trim() };
  });
  return { schemaVersion: 1, procurement };
}

export function permissionPatch(original: { roleName: string; category: string; active: boolean; permissions: string[] },
  draft: { roleName: string; category: string; active: boolean; permissions: string[] }):
  { roleName?: string; category?: string; active?: boolean; permissions?: string[] } {
  const patch: { roleName?: string; category?: string; active?: boolean; permissions?: string[] } = {};
  if (draft.roleName !== original.roleName) patch.roleName = draft.roleName;
  if (draft.active !== original.active) patch.active = draft.active;
  if (draft.category !== original.category) patch.category = draft.category;
  const changed = draft.permissions.length !== original.permissions.length
    || draft.permissions.some((key) => !original.permissions.includes(key));
  if (changed || patch.category) patch.permissions = [...draft.permissions];
  return patch;
}
