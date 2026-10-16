/**
 * Release scope — the single switch that decides what ships now.
 *
 * This release covers Setup, People, Payroll and Configuration, plus
 * everything payroll actually depends on: a pay run is only as good as the
 * hours, absence and claims that feed it, and the approvals that make those
 * figures final.
 *
 * Everything else is built but deliberately held back and shown as
 * "Coming soon" rather than deleted. Re-enabling an area is a one-line change.
 */

/** Nav sections included in this release, with why they are in. */
export const ENABLED_SECTIONS = new Set([
  "home",
  "people", // the employee record payroll pays
  "time", // hours, absence, timesheets and claims — all payroll inputs
  "payroll",
  "approvals", // an unapproved correction silently drops out of the run
  "lifecycle", // M22 onboarding — statutory readiness feeds the payroll record
  "experience", // M22 HR requests — the admin inbox lands on approvals
  "reports", // M23 statutory filings — PAYE returns and remittances
  "self-service", // M31 employee-owned profile, pay, leave, documents and notifications
  "recruitment", // M29 candidate-to-worker workflow is operational
  "relations", // M30 restricted case and protected-disclosure operations
  "performance", // M36 performance cycles, goals and assessments
  "offboarding", // M37 exit requests, checklists and exit interviews
  "configuration",
  "organization", // M39 org chart + reporting lines (nested under People)
  "setup", // M49 first-time setup wizard — admin-only at runtime via the nav roles guard
]);

/** Route prefixes included in this release. */
const ENABLED_PREFIXES = [
  "/hrm/employees",
  "/hrm/people",
  "/hrm/attendance", // hours feeding the run
  "/hrm/leave", // paid and unpaid absence
  "/hrm/time", // timesheets, TOIL, utilisation, travel and expenses
  "/hrm/benefits", // M41 Gap 6b flexible benefit claims — real backend live
  "/hrm/payslips",
  "/hrm/pay",
  "/hrm/payroll",
  "/hrm/approvals",
  "/hrm/requests",
  "/hrm/self-service",
  "/hrm/my-profile",
  "/hrm/my-preferences", // M35 employee notification preferences
  "/hrm/performance", // M36 performance management
  "/hrm/my-performance", // M36 self-service performance
  "/hrm/offboarding", // M37 offboarding & exit management
  "/hrm/my-offboarding", // M37 self-service resignation
  "/hrm/org-chart", // M39 organization chart
  "/hrm/reporting", // M39 reporting lines
  "/hrm/analytics", // M40 HR analytics dashboard
  "/hrm/my-documents",
  "/hrm/experience/letters",
  "/hrm/lifecycle/onboarding",
  "/hrm/reports", // M23 statutory filings
  "/hrm/recruitment", // M29 vacancy, candidate, offer and preboarding operations
  "/hrm/relations", // M30 employee-relations and investigator workspaces
  "/hrm/configuration",
  "/hrm/workspace/settings",
  "/hrm/data", // shared Import/Export hub
  "/hrm/setup",
  "/hrm/help",
  "/sign-in",
];

/** Always reachable regardless of scope. */
const ALWAYS = ["/hrm", "/hrm/help", "/sign-in", "/speak-up"];

const PRODUCTION = import.meta.env.VITE_USE_REAL_API === "true";
const DEMO_ONLY_PREFIXES = [
  "/hrm/configuration/branch-access", // M45 branch confinement admin
  "/hrm/configuration/process",
  "/hrm/reports/builder",
  "/hrm/experience/knowledge",
  "/hrm/experience/announcements",
  "/hrm/lifecycle/movements",

  "/hrm/lifecycle/assets",
  "/hrm/lifecycle/journeys",
  "/hrm/lifecycle/mobility",
  "/hrm/lifecycle/alumni",
  "/hrm/relations/discipline",
  "/hrm/relations/safety",
  "/hrm/relations/ethics",
  "/hrm/relations/labour",
  "/hrm/time/utilisation",
  "/hrm/time/travel",
  "/hrm/time/expenses",
];

export function isSectionEnabled(id: string) {
  return ENABLED_SECTIONS.has(id);
}

export function isPathEnabled(pathname: string) {
  if (PRODUCTION && /^\/hrm\/payroll\/runs\/[^/]+\/edit$/.test(pathname)) return false;
  if (PRODUCTION && DEMO_ONLY_PREFIXES.some((p) => pathname === p || pathname.startsWith(p + "/")))
    return false;
  if (ALWAYS.includes(pathname)) return true;
  return ENABLED_PREFIXES.some((p) => pathname === p || pathname.startsWith(p + "/"));
}

/** Shown on the Coming soon screen so the roadmap is explicit, not a dead end. */
export const COMING_SOON_AREAS = [
  "Lifecycle — onboarding, movements, offboarding, assets",
  "Talent — goals, reviews, learning, succession",
  "Employee experience — requests, letters, knowledge, engagement",
];
