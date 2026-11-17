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
