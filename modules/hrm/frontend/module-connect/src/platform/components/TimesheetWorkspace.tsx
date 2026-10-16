import { useEffect, type ReactNode } from "react";
import { useRouterState, useNavigate } from "@tanstack/react-router";
import { isTimesheetOnly, useAuth } from "@/platform/auth";

const USE_REAL = (import.meta.env.VITE_USE_REAL_API as string | undefined) === "true";
const paths = new Set(["/hrm/time/timesheets", "/hrm/time/attendance/import", "/hrm/time/attendance/bulk", "/sign-in"]);

export const canUseTimesheetPath = (path: string) => paths.has(path.replace(/\/$/, ""));

/** Block unrelated pages before they mount or request data. */
export function TimesheetBoundary({ children }: { children: ReactNode }) {
  const { loading, user } = useAuth();
  const pathname = useRouterState({ select: (state) => state.matches.at(-1)?.pathname ?? state.location.pathname });
  const navigate = useNavigate();
  const denied = USE_REAL && isTimesheetOnly(user?.roles ?? []) && !canUseTimesheetPath(pathname);
  useEffect(() => {
    if (denied) void navigate({ to: "/hrm/time/timesheets", replace: true });
  }, [denied, navigate]);
  if ((USE_REAL && loading && pathname !== "/sign-in") || denied) return null;
  return <>{children}</>;
}
