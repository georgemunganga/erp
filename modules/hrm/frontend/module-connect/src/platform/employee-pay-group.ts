type Row = Record<string, unknown>;

/** Display only the requested worker's effective payroll-owned assignment. */
export function employeePayGroupLabel(profiles: Row[], groups: Row[], workerId: string, asAt: string): string {
  const active = profiles.filter(p => String(p.workerId ?? "") === workerId
    && p.isActive !== false
    && typeof p.effectiveFrom === "string" && /^\d{4}-\d{2}-\d{2}$/.test(p.effectiveFrom)
    && p.effectiveFrom <= asAt
    && (!p.effectiveTo || (typeof p.effectiveTo === "string" && p.effectiveTo >= asAt)));
  if (!active.length) return "No current salary profile";
  if (active.length > 1) return "Multiple current profiles — review Compensation";
  const group = groups.find(g => String(g.id ?? "") === String(active[0].payGroupId ?? ""));
  return group && typeof group.name === "string" && group.name.trim()
    ? group.name : "Pay group unavailable — review Compensation";
}
