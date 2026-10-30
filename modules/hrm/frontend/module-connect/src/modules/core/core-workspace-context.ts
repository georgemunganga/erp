/** The Core selector is purpose-scoped and does not require HRM organization administration. */
export interface CoreWorkspaceChoices {
  companies: { id: string; code: string; name: string; currency: string; isDefault: boolean }[];
  branches: { id: string; name: string; legalEntityId: string }[];
  coreActions: ("core-currency-precision-read" | "core-currency-precision-configure")[];
}
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const object = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Core company choices could not be read.");
  return value as Record<string, unknown>;
};
export function parseCoreWorkspaceChoices(value: unknown): CoreWorkspaceChoices {
  const root = object(value);
  if (root.schemaVersion !== 1 || typeof root.tenantId !== "string" || !root.tenantId ||
    !Array.isArray(root.companies) || !Array.isArray(root.branches))
    throw new Error("Core company choices could not be read.");
  const companies = root.companies.map((entry) => {
    const row = object(entry);
    if (typeof row.id !== "string" || !guid.test(row.id) || typeof row.code !== "string" || !row.code ||
      typeof row.registeredName !== "string" || !row.registeredName || typeof row.currency !== "string" ||
      !/^[A-Z]{3}$/.test(row.currency) || typeof row.isDefault !== "boolean")
      throw new Error("A Core company choice was incomplete.");
    return { id: row.id, code: row.code, name: row.registeredName, currency: row.currency, isDefault: row.isDefault };
  });
  const allowed = new Set(companies.map((company) => company.id));
  const branches = root.branches.map((entry) => {
    const row = object(entry);
    if (typeof row.id !== "string" || !guid.test(row.id) || typeof row.legalEntityId !== "string" ||
      !allowed.has(row.legalEntityId) || typeof row.name !== "string" || !row.name)
      throw new Error("A Core branch choice was incomplete.");
    return { id: row.id, name: row.name, legalEntityId: row.legalEntityId };
  });
  const actions = root.coreActions ?? [];
  if (!Array.isArray(actions) || !actions.every(action => action === "core-currency-precision-read"
    || action === "core-currency-precision-configure"))
    throw new Error("Core permissions could not be read.");
  return { companies, branches, coreActions: [...new Set(actions)] };
}
export function selectedCoreCompanyId(choices: CoreWorkspaceChoices, currentId: string): string {
  if (choices.companies.some((company) => company.id === currentId)) return currentId;
  return choices.companies.find((company) => company.isDefault)?.id ?? choices.companies[0]?.id ?? "";
}
