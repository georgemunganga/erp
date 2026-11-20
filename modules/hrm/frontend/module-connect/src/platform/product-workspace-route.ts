/** Routing only. A current licensed company session and server permissions still control every operation. */
export function canRenderProductWorkspace(path: string, fullWorkspace: boolean): boolean {
  return fullWorkspace && (path === "/hrm" || path.startsWith("/hrm/") || path === "/core" || path.startsWith("/core/"));
}
