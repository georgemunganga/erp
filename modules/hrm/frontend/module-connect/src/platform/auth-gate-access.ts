/** An explicit page role list only narrows module admission. It never grants a module role. */
export function admitsPageRoles(moduleAdmitted: boolean, currentRoles: readonly string[], requiredRoles?: readonly string[]): boolean {
  if (!moduleAdmitted) return false;
  if (requiredRoles === undefined) return true;
  const held = new Set(currentRoles.map(role => role.toLowerCase()));
  return requiredRoles.some(role => held.has(role.toLowerCase()));
}
