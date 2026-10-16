import {
  Banknote,
  BarChart3,
  Briefcase,
  Clock4,
  Home,
  MessagesSquare,
  Settings,
  Shield,
  ShieldCheck,
  Sparkles,
  UserCog,
  Users,
} from "lucide-react";
import type { ModuleDefinition } from "@/platform/nav";

/**
 * HRM module navigation, following the information-architecture design:
 * grouped by user goal rather than by catalogue entity, with exactly ONE
 * Configuration entry point so admin/setup never leaks into operational areas.
 *
 * Rail sections are capped at ~12 (spatially learned, not re-scanned); any
 * single expanded list is capped at ~8-9 items before it needs sub-grouping.
 */
export const hrmModule: ModuleDefinition = {
  id: "hrm",
  name: "Human resources",
  shortName: "HRM",
  sections: [
    { id: "home", label: "Home", icon: Home, to: "/hrm" },
    { id: "self-service", label: "My HR", icon: UserCog, to: "/hrm/self-service" },
    {
      id: "people",
      label: "People",
      icon: Users,
      items: [
        { label: "Employees", to: "/hrm/employees" },
        { label: "My profile", to: "/hrm/my-profile" },
        { label: "Positions", to: "/hrm/people/positions", roles: ["hr_ops", "hr_admin", "manager"] },
        { label: "Organisation structure", to: "/hrm/people/org", roles: ["hr_ops", "hr_admin", "manager"] },
        { label: "Organization chart", to: "/hrm/org-chart", roles: ["hr_ops", "hr_admin"] },
        { label: "Reporting lines", to: "/hrm/reporting", roles: ["hr_ops", "hr_admin"] },
        { label: "My documents", to: "/hrm/my-documents" },
        { label: "Employee documents", to: "/hrm/people/documents", roles: ["hr_ops", "hr_admin"] },
        { label: "Privacy and consent", to: "/hrm/people/privacy" },
        { label: "Master data operations", to: "/hrm/people/master-data", roles: ["hr_ops", "hr_admin"] },
        { label: "Data quality", to: "/hrm/people/data-quality", roles: ["hr_ops", "hr_admin"] },
      ],
      groups: [
        {
          label: "Employee lifecycle",
          items: [
            { label: "Onboarding", to: "/hrm/lifecycle/onboarding" },
            { label: "Movements and transfers", to: "/hrm/lifecycle/movements" },
            { label: "Offboarding", to: "/hrm/lifecycle/offboarding" },
            { label: "Assets and access", to: "/hrm/lifecycle/assets" },
            { label: "Journeys", to: "/hrm/lifecycle/journeys" },
            { label: "Assignments", to: "/hrm/lifecycle/mobility" },
            { label: "Alumni and rehire", to: "/hrm/lifecycle/alumni" },
          ],
        },
        {
          label: "Employee experience",
          items: [
            { label: "HR requests", to: "/hrm/requests" },
            { label: "Raise a request", to: "/hrm/requests/new" },
            { label: "Letters", to: "/hrm/experience/letters" },
            { label: "Knowledge", to: "/hrm/experience/knowledge" },
            { label: "Engagement", to: "/hrm/experience/announcements" },
            { label: "Speak up", to: "/hrm/speak-up" },
          ],
        },
        {
          label: "Relations and safety",
          items: [
            { label: "Case operations", to: "/hrm/relations/operations", roles: ["hr_ops", "hr_admin"] },
            { label: "Protected disclosures", to: "/hrm/relations/protected-disclosures", roles: ["hr_ops", "hr_admin"] },
            { label: "Cases", to: "/hrm/relations/cases", roles: ["hr_ops", "hr_admin"] },
            { label: "Warning register", to: "/hrm/relations/discipline", roles: ["hr_ops", "hr_admin"] },
            { label: "Health and safety", to: "/hrm/relations/safety", roles: ["hr_ops", "hr_admin"] },
            { label: "Ethics and declarations", to: "/hrm/relations/ethics", roles: ["hr_ops", "hr_admin"] },
            { label: "Agreements and roll call", to: "/hrm/relations/labour", roles: ["hr_ops", "hr_admin"] },
          ],
        },
      ],
    },
    {
      id: "time",
      label: "Time & attendance",
      icon: Clock4,
      groups: [
        {
          label: "Attendance",
          items: [
            { label: "Timesheets", to: "/hrm/time/timesheets" },
            { label: "Clock in and out", to: "/hrm/attendance/clock" },
            { label: "Corrections", to: "/hrm/attendance" },
            { label: "Raise a correction", to: "/hrm/attendance/new" },
            { label: "Schedules and rosters", to: "/hrm/time/schedules", roles: ["hr_ops", "hr_admin", "manager"] },
            { label: "Add bulk attendance", to: "/hrm/time/attendance/bulk", roles: ["hr_ops", "hr_admin"] },
            { label: "Import attendance", to: "/hrm/time/attendance/import", roles: ["hr_ops", "hr_admin"] },
          ],
        },
        {
          label: "Overtime and leave",
          items: [
            { label: "Overtime review", to: "/hrm/time/operations", roles: ["hr_ops", "hr_admin", "manager"] },
            { label: "My leave", to: "/hrm/leave" },
            { label: "Request leave", to: "/hrm/leave/new" },
            { label: "Recorded leave", to: "/hrm/leave/recorded", roles: ["hr_ops", "hr_admin"] },
            { label: "Leave approvals", to: "/hrm/leave/approvals", roles: ["hr_ops", "hr_admin", "manager"] },
            { label: "Leave control panel", to: "/hrm/leave/control", roles: ["hr_ops", "hr_admin", "manager", "payroll"] },
            { label: "Leave allocation", to: "/hrm/leave/allocations", roles: ["hr_ops", "hr_admin"] },
            { label: "Leave periods & accruals", to: "/hrm/workspace/settings/leave-periods", roles: ["hr_ops", "hr_admin"] },
            { label: "Leave encashment", to: "/hrm/time/toil", roles: ["hr_ops", "hr_admin", "payroll"] },
          ],
        },
      ],
    },
    {
      id: "recruitment",
      label: "Recruitment",
      icon: Briefcase,
      roles: ["hr_ops", "hr_admin", "manager"],
      items: [
        { label: "Hiring operations", to: "/hrm/recruitment/operations" },
        { label: "Requisitions", to: "/hrm/recruitment/requisitions" },
        { label: "Vacancies", to: "/hrm/recruitment/vacancies" },
        { label: "Candidates", to: "/hrm/recruitment/candidates" },
        { label: "Offers and referrals", to: "/hrm/recruitment/offers" },
      ],
    },
    {
      id: "payroll",
      label: "Payroll & benefits",
      icon: Banknote,
      items: [
        { label: "My payslips", to: "/hrm/payslips" },
        { label: "Compensation", to: "/hrm/pay/compensation", roles: ["hr_ops", "hr_admin", "payroll"] },
        { label: "Benefits", to: "/hrm/benefits", roles: ["hr_ops", "hr_admin", "payroll"] },
        { label: "Salary advances", to: "/hrm/payroll/salary-advances", roles: ["hr_ops", "hr_admin", "payroll"] },
        { label: "Payroll administration", to: "/hrm/payroll", roles: ["payroll", "hr_admin"] },
        { label: "Pay runs", to: "/hrm/payroll/runs", roles: ["payroll", "hr_admin"] },
        { label: "Approval queue", to: "/hrm/payroll/queue", roles: ["payroll", "hr_admin"] },
        { label: "Payroll exceptions", to: "/hrm/payroll/exceptions", roles: ["payroll", "hr_admin"] },
        { label: "Payroll setup", to: "/hrm/workspace/settings/payroll", roles: ["hr_ops", "hr_admin"] },
      ],
    },
    {
      id: "performance",
      label: "Performance",
      icon: Sparkles,
      items: [
        { label: "Performance cycles", to: "/hrm/performance" },
        { label: "Goals", to: "/hrm/talent/goals" },
        { label: "Performance reviews", to: "/hrm/talent/reviews" },
        { label: "Learning", to: "/hrm/talent/learning" },
        { label: "Feedback and plans", to: "/hrm/talent/feedback" },
        { label: "Succession and skills", to: "/hrm/talent/succession" },
      ],
    },
    {
      id: "approvals",
      label: "Approvals",
      icon: ShieldCheck,
      to: "/hrm/approvals",
      roles: ["manager", "hr_ops", "hr_admin", "payroll"],
    },
    {
      id: "reports",
      label: "Reports & exports",
      icon: BarChart3,
      roles: ["manager", "hr_ops", "hr_admin", "payroll"],
      items: [
        { label: "Analytics", to: "/hrm/analytics", roles: ["hr_ops", "hr_admin"] },
        { label: "Statutory filings", to: "/hrm/reports" },
        { label: "Report builder", to: "/hrm/reports/builder" },
        { label: "Import and export", to: "/hrm/data/import-export" },
      ],
    },
    {
      id: "configuration",
      label: "Configuration",
      icon: Settings,
      to: "/hrm/workspace/settings",
      roles: ["hr_admin"],
    },
  ]
};

/** One row per editor. Preview and planned entries must not imply live policy. */
export const configurationGroups: {
  label: string;
  description: string;
  items: { label: string; detail: string; to?: string; status?: "connected" | "preview" | "planned" }[];
}[] = [
  {
    label: "Employment & pay",
    description: "Employee records, calendars, leave and payroll rules.",
    items: [
      { label: "Jobs & designations", detail: "Create and maintain job titles", to: "/hrm/workspace/settings/business" },
      { label: "Public holidays", detail: "Dates used by working calendars", to: "/hrm/workspace/settings/holidays" },
      { label: "Leave types", detail: "Entitlements and carry-forward", to: "/hrm/workspace/settings/leave-types" },
      { label: "Leave periods & accruals", detail: "Review or run monthly accruals", to: "/hrm/workspace/settings/leave-periods" },
      { label: "Contract types", detail: "Employment terms and notice periods", to: "/hrm/workspace/settings/contract-types" },
      { label: "Payroll setup", detail: "Pay groups, components and statutory rules", to: "/hrm/workspace/settings/payroll" },
      { label: "Grades & pay ranges", detail: "Employee grade master and approved ranges" },
      { label: "Country packs", detail: "Effective-dated local employment rules" },
    ],
  },
  {
    label: "Workflows",
    description: "How HRM requests, approvals and forms should work.",
    items: [
      { label: "Process design", detail: "Leave policies, shifts, approvals, forms and automation; preview only", to: "/hrm/workspace/settings/process", status: "preview" },
    ],
  },
  {
    label: "Privacy & compliance",
    description: "Sensitive information, retention and evidence.",
    items: [
      { label: "Security & compliance", detail: "Audit, legal holds and control evidence", to: "/hrm/workspace/settings/compliance" },
    ],
  },
  {
    label: "Operations & integrations",
    description: "Move HRM data and monitor hand-offs.",
    items: [
      { label: "Import & export", detail: "CSV and Excel data movement", to: "/hrm/data/import-export" },
      { label: "Notification delivery", detail: "Review and retry HRM notifications", to: "/hrm/workspace/settings/technical" },
      { label: "Payroll interface", detail: "Finance, banking and statutory hand-offs", to: "/hrm/workspace/settings/integrations" },
      { label: "Production readiness", detail: "Go-live evidence and sign-off", to: "/hrm/workspace/settings/go-live" },
    ],
  },
];
