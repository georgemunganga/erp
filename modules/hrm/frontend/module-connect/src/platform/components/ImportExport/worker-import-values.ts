/** Translate the employee form labels to the existing import API category.
 * Contract terms are configured separately; unknown values stay unchanged
 * so the server can refuse them in preview.
 */
export function normalizeWorkerImportType(value: string): string {
  const key = value.trim().toLowerCase();
  const labels: Record<string, string> = {
    permanent: "employee", employee: "employee",
    contractor: "contingent", contingent: "contingent",
    intern: "intern", volunteer: "volunteer",
  };
  return Object.hasOwn(labels, key) ? labels[key] : value.trim();
}

/** Only unambiguous formatting is removed. Keep signs and identifier letters
 * so invalid values are reported rather than silently changed into valid ones.
 */
export function cleanWorkerImportValue(fieldKey: string, rawValue: unknown): string {
  const value = String(rawValue ?? "").trim();
  if (fieldKey === "workerType") return normalizeWorkerImportType(value);
  if (["firstName", "middleName", "lastName"].includes(fieldKey))
    return value.replace(/\s+/g, " ");
  if (fieldKey === "phone") return value.replace(/[\s()-]/g, "");
  if (["basicSalary", "costOfLivingAllowance", "overtime.hours", "overtime.multiplier"].includes(fieldKey))
    return /^[+-]?\d{1,3}(,\d{3})+(\.\d+)?$/.test(value) ? value.replaceAll(",", "") : value;
  return value;
}
