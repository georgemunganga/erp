export type WorkflowConfigurationType = {
  id: string;
  moduleId: string;
  moduleLabel: string;
  recordType: string;
  recordLabel: string;
  trigger: string;
  triggerLabel: string;
  description: string;
  availabilityNote: string;
  settings: { label: string; path: string }[];
};
const object = (x: unknown): x is Record<string, any> =>
  !!x && typeof x === "object" && !Array.isArray(x);
const identifier = (x: unknown): x is string =>
  typeof x === "string" && /^[a-z][a-z0-9-]{0,59}$/.test(x);
const text = (x: unknown): x is string =>
  typeof x === "string" &&
  x.length > 0 &&
  x.length <= 500 &&
  x === x.trim() &&
  !/[\u0000-\u001f\u007f-\u009f]/.test(x);
const path = (moduleId: string, x: unknown): x is string =>
  typeof x === "string" &&
  x.length <= 250 &&
  x.startsWith(`/${moduleId}/workspace/settings/`) &&
  /^[a-zA-Z0-9/-]+$/.test(x) &&
  !x.includes("//");

export function parseWorkflowConfiguration(value: unknown): WorkflowConfigurationType[] {
  const fail = () => new Error("The workflow settings list could not be verified. Please refresh.");
  if (
    !object(value) ||
    value.schemaVersion !== 1 ||
    !Array.isArray(value.items) ||
    value.items.length > 100
  )
    throw fail();
  const seen = new Set<string>();
  return value.items.map((row: unknown) => {
    if (
      !object(row) ||
      !identifier(row.moduleId) ||
      !identifier(row.recordType) ||
      !identifier(row.trigger) ||
      row.id !== `${row.moduleId}.${row.recordType}.${row.trigger}` ||
      seen.has(row.id) ||
      ![
        row.moduleLabel,
        row.recordLabel,
        row.triggerLabel,
        row.description,
        row.availabilityNote,
      ].every(text) ||
      !Array.isArray(row.settings) ||
      row.settings.length < 1 ||
      row.settings.length > 4 ||
      !row.settings.every(
        (link: unknown) => object(link) && text(link.label) && path(row.moduleId, link.path),
      ) ||
      new Set(row.settings.map((link: any) => link.path)).size !== row.settings.length
    )
      throw fail();
    seen.add(row.id);
    return {
      id: row.id,
      moduleId: row.moduleId,
      moduleLabel: row.moduleLabel,
      recordType: row.recordType,
      recordLabel: row.recordLabel,
      trigger: row.trigger,
      triggerLabel: row.triggerLabel,
      description: row.description,
      availabilityNote: row.availabilityNote,
      settings: row.settings.map((link: any) => ({ label: link.label, path: link.path })),
    };
  });
}
