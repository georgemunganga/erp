/**
 * Drop-in data hook that mirrors `useMock`'s shape (`data`, `loading`,
 * `degraded`, `error`, `reload`) but reads from the real ASP.NET backend when
 * `VITE_USE_REAL_API=true` is set. Otherwise it delegates to the provided
 * mock reader, keeping the entire UI green on mock data by default.
 *
 * When the real backend is unreachable or returns 5xx the hook surfaces a
 * `degraded` banner instead of a hard failure, so screens stay usable with
 * fallback data.
 */
import { useCallback, useEffect, useState } from "react";
import { ApiError, hrmApi, type CompanyBranding, type CompanyBrandingUpdate } from "@/platform/api-client";
import type { EducationRecord } from "@/mock/employeeprofile";

export interface ApiState<T> {
  data: T | null;
  loading: boolean;
  degraded: string | null;
  error: string | null;
  reload: () => void;
}

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function downloadUrl(url: string, fileName: string) {
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function useApi<T>(fn: () => Promise<T>, deps: unknown[] = []): ApiState<T> {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [degraded, setDegraded] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);

  useEffect(() => {
    if (!USE_REAL) {
      // Real backend off — fall back to the mock reader untouched.
      let live = true;
      setLoading(true);
      setDegraded(null);
      setError(null);
      fn()
        .then((d) => live && setData(d))
        .catch((e) => {
          if (!live) return;
          setError(e instanceof Error ? e.message : "Unknown error");
        })
        .finally(() => live && setLoading(false));
      return () => {
        live = false;
      };
    }
    let live = true;
    setLoading(true);
    setDegraded(null);
    setError(null);
    fn()
      .then(async (d) => {
        if (!live) return;
        setData(d);
      })
      .catch((e) => {
        if (!live) return;
        if (e instanceof ApiError && e.status >= 500) {
          setDegraded("hrm-api");
        } else {
          setError(e instanceof Error ? e.message : "Unknown error");
        }
      })
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, nonce, USE_REAL]);

  const reload = useCallback(() => setNonce((n) => n + 1), []);
  return { data, loading, degraded, error, reload };
}

/**
 * Adapts a backend WorkerDto list into the mock `Employee` shape so existing
 * column definitions keep working when the real backend is switched on.
 */
export function adaptWorkers(backend: unknown): Array<import("@/mock/types").Employee> {
  const rows = Array.isArray(backend)
    ? backend
    : backend && typeof backend === "object" && "items" in backend
      ? Array.from((backend as { items?: unknown[] }).items ?? [])
      : [];
  return rows.map((raw) => {
    const w = raw as Record<string, unknown>;
    const toText = (v: unknown) => (v === undefined || v === null ? "" : String(v));
    const status = toText(w.status).toLowerCase();
    const workerType = toText(w.workerType).toLowerCase();
    return {
      id: toText(w.id),
      employeeNo: toText(w.employeeNo),
      fullName: toText(w.fullName),
      preferredName: w.preferredName ? toText(w.preferredName) : undefined,
      jobTitle: toText(w.jobTitle),
      department: toText(w.orgUnitName),
      entityId: "",
      branch: toText(w.locationName),
      managerId: w.managerId ? toText(w.managerId) : undefined,
      employmentType: (workerType === "contractor" ? "Contractor" : workerType === "intern" ? "Intern" : "Permanent") as never,
      status: (status === "pre-hire" ? "Pre-hire" : status === "on-leave" ? "On leave" : status === "notice" ? "Notice period" : status === "terminated" || status === "archived" ? "Terminated" : "Active") as never,
      startDate: toText(w.startDate),
      endDate: w.endDate ? toText(w.endDate) : undefined,
      email: w.email ? toText(w.email) : undefined,
      phone: w.phone ? toText(w.phone) : undefined,
      location: toText(w.locationName),
      grade: toText(w.grade),
      nationalId: toText(w.nrc),
      bankAccount: Array.isArray(w.bankDetails)
        ? (((w.bankDetails as Array<{ accountNumber?: unknown }>)[0]?.accountNumber ??
            "") as string)
        : "",
    };
  });
}

/**
 * Adapts backend PayrollRunLineDto items ({ items }) into the UI RunLine
 * shape used by the pay-run screens. Derived display fields the backend does
 * not expose (job title, grade, prior net) stay blank rather than fabricated.
 */
export function adaptPayrollLines(
  raw: unknown,
  runId: string,
): Array<import("@/mock/payrollrun").RunLine> {
  const envelope = raw as { items?: unknown[] };
  return (envelope.items ?? []).map((item) => {
    const l = item as Record<string, unknown>;
    const status = String(l.exceptionStatus ?? "open");
    const components = (l.components as Record<string, unknown>[] | undefined) ?? [];
    return {
      id: String(l.id ?? ""),
      runId,
      employeeId: String(l.workerId ?? ""),
      employee: String(l.workerName ?? ""),
      jobTitle: "",
      grade: "",
      components: components.map((c) => ({
        code: String(c.componentCode ?? ""),
        label: String(c.componentName ?? ""),
        kind:
          String(c.componentType ?? "earning") === "earning"
            ? "Earning"
            : String(c.componentType ?? "") === "employer-contribution"
              ? "Employer"
              : "Deduction",
        amount: Number(c.amount ?? 0),
        source: c.isStatutory ? "Statutory" : "One-off",
        basis: String(c.explanation ?? ""),
        inputs: [],
        ruleVersion: "engine-v1",
        effectiveFrom: "",
        explanation: String(c.explanation ?? ""),
      })),
      gross: Number(l.grossPay ?? 0),
      deductions: Number(l.totalDeductions ?? 0),
      employerCost: Number(l.employerCost ?? 0),
      net: Number(l.netPay ?? 0),
      flags:
        l.hasException && status === "open"
          ? [String(l.exceptionReason ?? "Payroll exception")]
          : [],
    } as import("@/mock/payrollrun").RunLine;
  });
}

/**
 * Adapts the backend OrgUnitTreeDto (recursive children) into the UI OrgUnit
 * list the structure screens consume. Headcount, incoming, leaver, position
 * and vacancy figures are not exposed by the admin API yet, so they stay at
 * zero rather than being fabricated; the UI renders "—" for those cells.
 */
export function adaptOrgUnits(raw: unknown): import("@/mock/structure").OrgUnit[] {
  const items = raw as Array<Record<string, unknown>> | undefined;
  if (!Array.isArray(items)) return [];
  const out: import("@/mock/structure").OrgUnit[] = [];
  const walk = (node: Record<string, unknown>, parentId: string | undefined) => {
    const children = (node.children as Array<Record<string, unknown>> | undefined) ?? [];
    const unitType = String(node.unitType ?? "").toLowerCase();
    const kind: "Entity" | "Branch" | "Department" | "Team" =
      unitType === "entity"
        ? "Entity"
        : unitType === "branch"
          ? "Branch"
          : unitType === "department"
            ? "Department"
            : "Team";
    out.push({
      id: String(node.id ?? ""),
      parentId,
      kind,
      name: String(node.name ?? ""),
      code: String(node.code ?? ""),
      entityId: String(node.legalEntityId ?? parentId ?? ""),
      location: "",
      lead: { name: node.managerName ? String(node.managerName) : "", title: "Unit lead" },
      headcount: 0,
      incoming: 0,
      leavers: 0,
      positions: 0,
      vacancies: 0,
      note: node.status && String(node.status) !== "Active" ? String(node.status) : undefined,
    });
    for (const child of children) walk(child, String(node.id ?? ""));
  };
  for (const root of items) walk(root, undefined);
  return out;
}

/** Maps only fields the live WorkerDto actually owns; unavailable profile fields remain blank. */
function paymentMethodLabel(value: string) {
  const normalized = value.toLowerCase().replace(/_/g, "-").trim();
  if (normalized === "bank") return "Bank transfer";
  if (normalized === "mobile-money") return "Mobile money";
  if (normalized === "cash") return "Cash";
  if (normalized === "accounts-payable") return "Paid through accounts payable, not payroll";
  return value;
}

export function adaptWorkerProfile(rawValue: unknown): import("@/mock/employeeprofile").EmployeeProfile {
  const raw = (rawValue ?? {}) as Record<string, unknown>;
  const emergency = Array.isArray(raw.emergencyContacts) ? raw.emergencyContacts as Record<string, unknown>[] : [];
  const banks = Array.isArray(raw.bankDetails) ? raw.bankDetails as Record<string, unknown>[] : [];
  const bank = banks.find((item) => Boolean(item.isPrimary)) ?? banks[0];
  const text = (value: unknown) => value == null ? "" : String(value);
  return {
    employeeId: text(raw.id), salutation: "", gender: "", dateOfBirth: text(raw.dateOfBirth),
    maritalStatus: "", nationality: text(raw.nationality), passportNo: text(raw.passportNo),
    residentialAddress: "", emergency: emergency.map((item) => ({
      id: text(item.id), name: text(item.fullName), relationship: text(item.relationship),
      phone: text(item.phone), isPrimary: Boolean(item.isPrimary),
    })),
    noticePeriodDays: 0, reportsTo: text(raw.managerName), costCentre: "", payGroup: "",
    shiftPattern: "", holidayCalendar: "", leavePolicy: "", attendanceDeviceId: "",
    paymentMethod: paymentMethodLabel(text(bank?.paymentMethod)), bankDetailId: text(bank?.id),
    bankName: text(bank?.bankName),
    bankBranch: text(bank?.branchCode), bankAccount: text(bank?.accountNumber),
    accountName: text(bank?.accountName), mobileMoneyNumber: text(bank?.mobileMoneyNumber),
    tpin: text(raw.tpin), napsaNumber: text(raw.napsaNumber), nhimaNumber: text(raw.nhimaNumber),
    education: (Array.isArray(raw.education)
      ? (raw.education as Record<string, unknown>[]).map((item) => ({
          id: text(item.id),
          qualification: text(item.qualification ?? item.degree ?? ""),
          institution: text(item.institution ?? item.school ?? ""),
          field: text(item.field ?? ""),
          completedYear: text(item.completedYear ?? item.endDate ?? item.to ?? ""),
          verified: Boolean(item.verified),
        }))
      : []) as EducationRecord[],
    previousEmployment: Array.isArray(raw.externalWorkHistory)
      ? (raw.externalWorkHistory as Record<string, unknown>[]).map((item) => ({
          id: text(item.id), employer: text(item.company), jobTitle: text(item.role ?? ""),
          from: text(item.startDate), to: text(item.endDate), reasonForLeaving: "",
          referenceChecked: false,
        }))
      : [],
    dependants: [],
  };
}

/** Shortcut readers for the flagship backend surfaces used by pages. */
export const realApi = {
  /** The backend returns `Paged<T>` — { items, totalCount, page, pageSize }. */
  employees: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[]; totalCount: number }>("/hrm/workers", {
      page: 1,
      pageSize: 200,
      ...params,
    }),
  worker: (id: string) => hrmApi.get<unknown>(`/hrm/workers/${id}`),
  dqChecks: () => hrmApi.get<unknown>("/hrm/dq/checks"),
  workerDocuments: (workerId: string) => hrmApi.get<unknown>(`/hrm/documents/worker/${workerId}`),
  reports: (params: Record<string, unknown>) => hrmApi.get<unknown>("/hrm/reports", params),
  managementReports: (params: Record<string, unknown>) =>
    hrmApi.get<unknown>("/hrm/reports/management", params),
  downloadManagementReport: async (
    reportType: string,
    params: Record<string, unknown>,
    format: "csv" | "xlsx" | "pdf" = "csv",
  ) => {
    const blob = await hrmApi.getBlob(
      `/hrm/reports/management/export/${reportType}`,
      { ...params, format },
      {
        Accept:
          format === "pdf"
            ? "application/pdf"
            : format === "xlsx"
              ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              : "text/csv",
      },
    );
    downloadUrl(URL.createObjectURL(blob), `${reportType}.${format}`);
  },
  /** Create a worker and return the created WorkerDto. */
  createWorker: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/workers", body),
  /** Patch-update a worker (fields sent as-is, backend accepts partial). */
  updateWorker: (id: string, body: Record<string, unknown>) =>
    hrmApi.put<Record<string, unknown>>(`/hrm/workers/${id}`, body),
  /** Soft-archive a worker. */
  archiveWorker: (id: string) => hrmApi.post<unknown>(`/hrm/workers/${id}/archive`, null),
  masterDataBatches: () =>
    hrmApi.get<{ items: Array<Record<string, unknown>> }>("/hrm/master-data/batches"),
  previewWorkerImport: (fileName: string, rows: Array<Record<string, unknown>>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/master-data/imports/preview", {
      fileName,
      rows,
    }),
  previewWorkerBulk: (effectiveDate: string, rows: Array<Record<string, unknown>>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/master-data/bulk/preview", {
      effectiveDate,
      rows,
    }),
  applyMasterDataBatch: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/master-data/batches/${id}/apply`, null),
  rollbackMasterDataBatch: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/master-data/batches/${id}/rollback`, null),
  reactivateWorker: (id: string, reason: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/master-data/workers/${id}/reactivate`, {
      reason,
    }),
  /** Upload a document for a worker. */
  uploadDocument: (workerId: string, file: File, category: string, title: string) =>
    hrmApi.uploadDocument(workerId, file, category, title),
  /** M31 shared import/export tool — all list pages reuse this surface. */
  importSchemas: () =>
    hrmApi.get<Array<{ typeKey: string; displayName: string; fields: Array<{ key: string; label: string; required: boolean; naturalKey: boolean; example?: string; formatNote?: string }> }>>("/hrm/import/schemas"),
  importPreview: (typeKey: string, fileName: string, mode: string, rows: Array<Record<string, string>>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/import/${typeKey}/preview`, { fileName, mode, rows }),
  importApply: (typeKey: string, previewId: string, rowIndexes: number[]) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/import/${typeKey}/apply`, { previewId, rowIndexes }),
  importExportBlob: (typeKey: string, filter?: string) =>
    hrmApi.getBlob(`/hrm/import/${typeKey}/export`, filter !== undefined ? { filter } : undefined),
  /** Org units (config) — used for department placement selects. */
  orgUnits: () => hrmApi.get<unknown[]>("/hrm/admin/org-units"),
  /** Recursive org-unit tree — used by the people structure page. */
  orgUnitsTree: () => hrmApi.get<unknown[]>("/hrm/admin/org-units/tree"),
  /** Legal entities with org units nested beneath (entity › branches › departments › teams). */
  entityTree: () => hrmApi.get<unknown[]>("/hrm/admin/org-units/entity-tree"),
  /** Work locations (config) — used for location placement selects. */
  locations: async () => {
    // Config endpoints return a paginated envelope { items, totalCount, ... }
    const page = await hrmApi.get<{ items?: unknown[] }>("/hrm/admin/locations");
    return (page.items ?? []) as unknown[];
  },
  /** Download a document and return { url, fileName }. Caller revokes url. */
  downloadDocument: async (documentId: string, fileName: string) => {
    const url = await hrmApi.downloadDocument(documentId);
    return { url, fileName };
  },

  /* ------------------------------------------------------------------ */
  /* Additional surfaces wired for M11 — same { items } envelope shape   */
  /* ------------------------------------------------------------------ */

  /**
   * M17 admin leave inbox (roles hr_ops / hr_admin / manager): company-wide
   * leave requests with optional status + worker filters. GET /hrm/time/leave.
   */
  leaveRequests: (params?: Record<string, unknown>) =>
    hrmApi.get<{
      items: {
        id: string;
        workerId: string;
        workerName: string;
        leaveTypeCode: string;
        startDate: string;
        endDate: string;
        requestedDays: number;
        status: string;
        balanceReserved: boolean;
        crossesCutoff: boolean;
        createdAt: string;
        locationId: string | null;
      }[];
      totalCount: number;
      page: number;
      pageSize: number;
    }>("/hrm/time/leave", params ?? {}),
  leaveBalances: (workerId: string) =>
    hrmApi.get<unknown[]>(`/hrm/time/leave/balances/${workerId}`),
  createLeaveRequest: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/leave", body),
  /** Backend expects { action: 'approve'|'reject'|'return', reason? } */
  decideLeaveRequest: (id: string, action: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/time/leave/${id}/decide`, { action, reason }),
  leaveTypes: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown>("/hrm/admin/leave-types/full", {
      includeInactive: false,
      ...(params ?? {}),
    }),
  createLeaveType: (body: Record<string, unknown>) => hrmApi.post<unknown>("/hrm/admin/leave-types", body),
  updateLeaveType: (id: string, body: Record<string, unknown>) => hrmApi.patch<unknown>(`/hrm/admin/leave-types/${id}`, body),
  timeCorrections: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/time/corrections", params ?? {}),
  createCorrection: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/corrections", body),
  decideCorrection: (id: string, action: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/time/corrections/${id}/decide`, { action, reason }),

  /** Time: attendance (clocking). */
  clockIn: (workerId: string) =>
    hrmApi.post<unknown>(`/hrm/time/attendance/${workerId}/clock-in`, null),
  clockOut: (workerId: string) =>
    hrmApi.post<unknown>(`/hrm/time/attendance/${workerId}/clock-out`, null),
  attendanceToday: (workerId: string) =>
    hrmApi.get<unknown>(`/hrm/time/attendance/${workerId}/today`),
  attendanceHistory: (workerId: string, params?: Record<string, unknown>) =>
    hrmApi.get<unknown>(`/hrm/time/attendance/${workerId}`, params ?? {}),
  /** Organization attendance summary, scoped by the selected branch/org unit on the request. */
  attendanceSummary: (params?: Record<string, unknown>) =>
    hrmApi.get<Array<{
      id: string;
      workerId: string;
      workerName: string;
      workDate: string;
      clockIn?: string | null;
      clockOut?: string | null;
      source: string;
      derivedStatus: string;
      totalHours: number;
      scheduledHours: number;
      regularHours: number;
      overtimeHours: number;
      overtimeMultiplier: number;
      shiftId?: string | null;
      importBatchId?: string | null;
      overtimeStatus: string;
      overtimeDecisionReason?: string | null;
      overtimeDecidedBySubjectId?: string | null;
      overtimeDecidedAt?: string | null;
      overtimePayrollRunId?: string | null;
      overtimePayrollLineId?: string | null;
    }>>(`/hrm/time/attendance`, params ?? {}),
  overtime: (params?: Record<string, unknown>) =>
    hrmApi.get<Array<{
      id: string;
      workerId: string;
      workerName: string;
      workDate: string;
      totalHours: number;
      regularHours: number;
      overtimeHours: number;
      overtimeMultiplier: number;
      overtimeStatus: string;
      overtimeDecisionReason?: string | null;
      overtimeDecidedBySubjectId?: string | null;
      overtimeDecidedAt?: string | null;
      overtimePayrollRunId?: string | null;
      overtimePayrollLineId?: string | null;
    }>>(`/hrm/time/overtime`, params ?? {}),
  decideOvertime: (id: string, action: "approve" | "reject", reason?: string) =>
    hrmApi.post<unknown>(`/hrm/time/overtime/${id}/decide`, { action, reason }),
  roster: (workerId: string, params?: Record<string, unknown>) =>
    hrmApi.get<unknown>(`/hrm/time/roster/${workerId}`, params ?? {}),
  shifts: () => hrmApi.get<unknown[]>("/hrm/time/shifts"),
  createShift: (body: Record<string, unknown>) => hrmApi.post<unknown>("/hrm/time/shifts", body),
  updateShift: (id: string, body: Record<string, unknown>) => hrmApi.patch<unknown>(`/hrm/time/shifts/${id}`, body),
  closeShift: (id: string) => hrmApi.post<unknown>(`/hrm/time/shifts/${id}/close`, null),
  assignShift: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/time/shifts/assign/${workerId}`, body),
  importAttendance: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/attendance/import", body),
  importOvertime: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/overtime/import", body),
  runLeaveAccrual: (period: string) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/leave/accruals/run", { period }),
  adjustLeaveBalance: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/leave/balances/adjust", body),
  escalateTimeApprovals: () =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/escalations/run", null),

  /** Leave encashment (M41 Gap 6a): HR converts unused leave to a cash payout. */
  encashmentRate: (workerId: string, leaveTypeCode: string, days: number) =>
    hrmApi.get<{
      monthlyBasic: number;
      dailyRate: number;
      estimatedGross: number;
      currency: string;
    }>(`/hrm/time/leave/encashments/rate/${workerId}`, { leaveTypeCode, days }),
  encashments: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: Record<string, unknown>[] }>(
      "/hrm/time/leave/encashments",
      params ?? {},
    ),
  createEncashment: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/time/leave/encashments", body),
  decideEncashment: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/time/leave/encashments/${id}/decide`, body),
  timeOperationsHistory: () =>
    hrmApi.get<{
      imports: Record<string, unknown>[];
      timeAudits?: Record<string, unknown>[];
      accruals: Record<string, unknown>[];
      adjustments: Record<string, unknown>[];
    }>("/hrm/time/operations/history"),

  /**
   * Flexible benefit claims (M41 Gap 6b): configurable benefit types with an
   * optional per-worker annual allowance override; claims are capped by the
   * allowance when one exists, otherwise by the type's annual cap. Submitted
   * claims go through an approve/reject/return inbox and a final pay step.
   */
  benefitTypes: () => hrmApi.get<unknown[]>("/hrm/benefits/types"),
  createBenefitType: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/benefits/types", body),
  updateBenefitType: (id: string, body: Record<string, unknown>) =>
    hrmApi.put<unknown>(`/hrm/benefits/types/${id}`, body),
  deleteBenefitType: (id: string) =>
    hrmApi.delete<unknown>(`/hrm/benefits/types/${id}`),
  benefitAllowances: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/benefits/allowances", params ?? {}),
  setBenefitAllowance: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/benefits/allowances", body),
  deleteBenefitAllowance: (id: string) =>
    hrmApi.delete<unknown>(`/hrm/benefits/allowances/${id}`),
  benefitClaims: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: Record<string, unknown>[]; totalCount: number }>(
      "/hrm/benefits/claims",
      params ?? {},
    ),
  createBenefitClaim: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/benefits/claims", body),
  decideBenefitClaim: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/benefits/claims/${id}/decide`, body),
  payBenefitClaim: (id: string) =>
    hrmApi.post<unknown>(`/hrm/benefits/claims/${id}/pay`, null),

  /** Workflow: shared approval queue + request detail/decisions. */
  workflowQueue: () => hrmApi.get<{ items: unknown[] }>("/hrm/workflow/queue"),
  workflowRequest: (id: string) => hrmApi.get<unknown>(`/hrm/workflow/requests/${id}`),
  /** Backend expects { action, reason? } (camelCase from WorkflowDecisionRequest). */
  workflowDecide: (id: string, action: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/workflow/requests/${id}/decisions`, { action, reason }),
  workflowEscalate: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/workflow/requests/${id}/escalate`, body),

  /** Experience: letters, service requests, speak-up. */
  experienceRequests: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[]; totalCount: number }>("/hrm/experience/requests", params ?? {}),
  createExperienceRequest: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/experience/requests", body),
  addRequestMessage: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/experience/requests/${id}/messages`, body),
  resolveRequest: (id: string) =>
    hrmApi.post<unknown>(`/hrm/experience/requests/${id}/resolve`, null),
  /** Onboarding readiness for one worker — 5-item statutory/banking checklist. */
  onboardingPlan: (workerId: string) =>
    hrmApi.get<{
      workerId?: string;
      isOnboarded?: boolean;
      tasksCompleted?: number;
      tasksTotal?: number;
    }>(`/hrm/workers/${workerId}/onboarding`),
  experienceLetters: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/experience/letters", params ?? {}),
  createLetter: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/experience/letters", body),
  approveLetter: (id: string) =>
    hrmApi.post<unknown>(`/hrm/experience/letters/${id}/approve`, null),
  speakUp: (body: Record<string, unknown>) =>
    hrmApi.post<{ caseReference?: string; accessCode?: string }>("/hrm/experience/speak-up", body),
  speakUpStatus: (caseReference: string, accessCode: string) =>
    hrmApi.get<unknown>("/hrm/experience/speak-up/status", { caseReference, accessCode }),

  /** Payroll: configuration + runs. */
  payrollComponents: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/payroll/components", params ?? {}),
  payrollPayGroups: () => hrmApi.get<unknown[]>("/hrm/payroll/pay-groups"),
  payrollPayGroupPeriods: (groupId: string) =>
    hrmApi.get<unknown[]>(`/hrm/payroll/pay-groups/${groupId}/periods`),
  createHistoricalPayrollPeriod: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/payroll/historical-periods", body),
  payrollTaxSlabs: (taxYear: string) =>
    hrmApi.get<unknown[]>("/hrm/payroll/tax-slabs", { taxYear }),
  payrollContributionRules: () => hrmApi.get<unknown[]>("/hrm/payroll/contribution-rules"),

  /* ------------------------------------------------------------------ */
  /* M23 statutory compliance: PAYE return + NAPSA/NHIMA remittance files */
  /* ------------------------------------------------------------------ */

  /** Generate a statutory CSV for one period and hand back a downloadable blob URL. */
  statutoryGenerate: async (exportType: string, periodId: string) => {
    const blob = await hrmApi.statutoryExport(exportType, periodId);
    const url = URL.createObjectURL(blob);
    const fileName = `${exportType}-${periodId}.csv`;
    return { url, fileName };
  },
  statutoryPreview: (exportType: string, periodId: string) =>
    hrmApi.statutoryExportPreview(exportType, periodId),
  /** Aggregate statutory liability summary for one period (no download). */
  statutorySummary: (periodId: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/statutory-exports/summary?periodId=${periodId}`),
  payrollStructures: () => hrmApi.get<unknown[]>("/hrm/payroll/structures"),
  updateStructure: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/payroll/structures/${id}`, body),
  payrollProfiles: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/payroll/profiles", params ?? {}),
  createPayrollProfile: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/payroll/profiles/${workerId}`, body),
  salaryAdvances: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown[]>("/hrm/payroll/salary-advances", params ?? {}),
  createSalaryAdvance: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/payroll/salary-advances", body),
  updateSalaryAdvance: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/payroll/salary-advances/${id}`, body),
  cancelSalaryAdvance: (id: string, reason: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/salary-advances/${id}/cancel`, { reason }),
  /** M41 Gap 3: pay-basis control — salary (default) vs timesheet (planning
   *  flag; timesheet-driven pay is not implemented yet). */
  setPayBasis: (workerId: string, payBasis: "salary" | "timesheet") =>
    hrmApi.put<unknown>(`/hrm/payroll/profiles/${workerId}/pay-basis`, { payBasis }),
  setOvertimePolicy: (workerId: string, body: {
    overtimeCategory: "ordinary" | "watchperson-guard";
    weeklyOvertimeThresholdHours?: number;
    monthlyOvertimeDivisor?: number;
  }) => hrmApi.put<unknown>(`/hrm/payroll/profiles/${workerId}/overtime-policy`, body),
  /** M27 operational run list with live totals and workflow state. */
  payrollRuns: () => hrmApi.get<{ items: unknown[]; totalCount: number }>("/hrm/payroll/runs"),
  /** M48: the top-HR approval queue — branch runs awaiting review with
   * control totals, branch names, and submission stamps. Confined (branch-only)
   * HR are refused here with 403. */
  payrollQueue: () => hrmApi.get<unknown[]>("/hrm/payroll/queue"),
  /** M49: first-time setup wizard — single decision endpoint the shell polls
   * on every render (pending → welcome overlay; complete → dashboard). */
  setupState: () =>
    hrmApi.get<{
      status: string;
      resumeStepKey: string | null;
      completedSteps: string[];
      optionalSteps: string[];
      completionPercent: number;
    }>("/hrm/setup/state"),
  /** M50.18: the saved input payload of a completed wizard step — the
   *  employees step reads step 3's grades and positions from here. */
  setupStepData: (key: string) =>
    hrmApi.get<{ dataJson: string | null }>(`/hrm/setup/steps/${key}/data`),
  /** M49: wizard step catalog with completion/open status for rendering. */
  setupSteps: () =>
    hrmApi.get<
      {
        key: string;
        label: string;
        description: string;
        mandatory: boolean;
        completed: boolean;
        open: boolean;
      }[]
    >("/hrm/setup/steps"),
  /** M49: mark a wizard step complete with optional context payload. */
  completeSetupStep: (key: string, dataJson?: string) =>
    hrmApi.post<unknown>(`/hrm/setup/steps/${key}`, { dataJson: dataJson ?? null }),
  /** M49: finish the wizard — refuses while the mandatory prefix is open. */
  finishSetup: () => hrmApi.post<unknown>("/hrm/setup/finish", null),
  /** M51: provision invited wizard emails with hr_admin + employee roles in
   *  the identity system. Returns per-email outcomes so the roles step can
   *  show which invitations actually took. */
  provisionAdmins: (emails: string[]) =>
    hrmApi.post<{ entries: Array<{ email: string; found: boolean; assigned: boolean; error?: string }>; assigned: number }>(
      "/hrm/setup/provision-admins",
      { emails },
    ),
  /** M51: first signed-in operator of a fresh tenant claims top-HR access.
   *  `{elevated: false, reason: "..."}` when already elevated or refused. */
  claimFirstUser: () => hrmApi.get<{ elevated: boolean; roles: string[]; reason?: string }>("/hrm/setup/first-user/claim"),
  /** M49: destructive start-afresh reset — hr_admin only, explicit confirm. */
  resetSetup: () =>
    hrmApi.post<unknown>("/hrm/setup/reset", { confirm: "RESET" }),
  payrollRun: (id: string) => hrmApi.get<unknown>(`/hrm/payroll/runs/${id}`),
  createPayrollRun: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/payroll/runs", body),
  updatePayrollRun: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/runs/${id}`, body),
  calculatePayrollRun: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/calculate`, null),
  lockPayrollRun: (id: string) => hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/lock`, null),
  /** M46: branch payroll drafts flow up for organisation-wide HR approval. */
  submitPayrollRun: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/submit-for-review`, null),
  payrollRunApprove: (id: string, note?: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/approve`, { note }),
  payrollRunRelease: (id: string) => hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/release`, null),
  payrollRunCancel: (id: string, reason: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/cancel`, { reason }),
  payrollRunReverse: (id: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/reverse`, { reason }),
  payrollRunPreflight: (payPeriodId: string, payGroupId: string, isHistorical = false, historicalReason?: string) =>
    hrmApi.post<unknown>("/hrm/payroll/runs/preflight", { payPeriodId, payGroupId, isHistorical, historicalReason }),
  payrollCalculationReadiness: (id: string) =>
    hrmApi.get<unknown>(`/hrm/payroll/runs/${id}/calculation-readiness`),
  payrollRunLines: (id: string) => hrmApi.get<unknown>(`/hrm/payroll/runs/${id}/lines`),
  workerPayslipPreview: (workerId: string) =>
    hrmApi.get<unknown>(`/hrm/payroll/workers/${workerId}/payslip-preview`),
  payrollExceptionDecision: (id: string, lineId: string, decision: string, reason: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/lines/${lineId}/exception`, { decision, reason }),
  payrollCorrection: (
    id: string,
    lineId: string,
    componentCode: string,
    amount: number,
    reason: string,
  ) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/lines/${lineId}/correction`, {
      componentCode,
      amount,
      reason,
    }),
  payrollPaymentGenerate: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/payments/generate`, {}),
  payrollPaymentReadiness: (id: string) =>
    hrmApi.get<unknown>(`/hrm/payroll/runs/${id}/payments/readiness`),
  payrollPaymentApprove: (id: string, note?: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/payments/approve`, { note }),
  payrollPaymentRelease: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/payments/release`, {}),
  payrollReconcile: (id: string, reference: string, actualAmount: number, note?: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${id}/reconcile`, { reference, actualAmount, note }),
  payrollRunAudit: (id: string) => hrmApi.get<unknown[]>(`/hrm/payroll/runs/${id}/audit`),
  payrollPaymentFile: (id: string) => hrmApi.getBlob(`/hrm/payroll/runs/${id}/payments/file`),
  payrollAuditExport: (id: string) => hrmApi.getBlob(`/hrm/payroll/runs/${id}/audit/export`),
  /** M24: per-run statutory identity readiness — who blocks the release gate. */
  payrollRunStatutoryReadiness: (id: string) =>
    hrmApi.get<{
      runId?: string;
      periodLabel?: string;
      isReady?: boolean;
      workerCount?: number;
      workers?: Array<{
        workerId?: string;
        employeeNo?: string;
        fullName?: string;
        hasNrc?: boolean;
        hasTpin?: boolean;
        hasNapsaNumber?: boolean;
        hasNhimaNumber?: boolean;
        ready?: boolean;
      }>;
    }>(`/hrm/payroll/runs/${id}/statutory-readiness`),
  /** M24: payslip by id — the snapshot includes statutory references. */
  payslipById: (id: string) => hrmApi.get<unknown>(`/hrm/payroll/payslips/id/${id}`),
  /** Payslips for one worker, newest first as returned by the API. */
  workerPayslips: (workerId: string) => hrmApi.get<{ items: unknown[]; totalCount: number }>(`/hrm/payroll/payslips/${workerId}`),

  /* ------------------------------------------------------------------ */
  /* M34: admin payslip surface per run.                                  */
  /* ------------------------------------------------------------------ */

  /** All payslips for a released run — real IDs for navigation. */
  payrollRunPayslips: (runId: string) => hrmApi.get<unknown>(`/hrm/payroll/runs/${runId}/payslips`),
  /** Bulk-generate PDFs for all payslips in a run. Idempotent. */
  payrollGenerateAllPayslips: (runId: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/runs/${runId}/payslips/generate-all`, null),
  /** Raw PDF bytes for inline preview. Returns a download URL string. */
  payslipPreviewUrl: (payslipId: string) =>
    `${import.meta.env.VITE_HRM_API_BASE ?? "/api"}/hrm/payroll/payslips/${payslipId}/preview`,
  /** Direct PDF download trigger via blob fetch. */
  payslipDownloadBlob: (payslipId: string) =>
    hrmApi.getBlob(`/hrm/payroll/payslips/${payslipId}/preview`, undefined, { Accept: "application/pdf" }),

  /* ------------------------------------------------------------------ */
  /* M25: employee self-service — own payslips and requests inbox,        */
  /* always keyed on the caller's OIDC subject.                           */
  /* ------------------------------------------------------------------ */

  /** Own payslips — empty list when the identity is not linked to a worker. */
  myPayslips: () => hrmApi.get<unknown>("/hrm/me/payslips"),
  myLeave: () => hrmApi.myLeave(),
  createMyLeaveRequest: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/me/leave", body),
  /** M35: single self-service dashboard payload — punch + balances + identity. */
  myDashboard: () =>
    hrmApi.get<{
      workerId: string;
      workerName: string;
      employeeNo: string | null;
      linked: boolean;
      todayPunch: { state: string; clockIn: string; clockOut: string; totalHours: number; derivedStatus: string } | null;
      balances: Array<{ leaveTypeCode: string; leaveTypeName: string; accrued: number; taken: number; reserved: number; available: number }>;
    }>("/hrm/me/dashboard"),
  myAttendanceToday: () => hrmApi.get<unknown>("/hrm/me/attendance/today"),
  myAttendance: (params?: Record<string, unknown>) =>
    hrmApi.get<unknown>("/hrm/me/attendance", params ?? {}),
  clockMyselfIn: () => hrmApi.post<unknown>("/hrm/me/attendance/clock-in", null),
  clockMyselfOut: () => hrmApi.post<unknown>("/hrm/me/attendance/clock-out", null),
  createMyCorrection: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/me/attendance/corrections", body),
  /** Full snapshot of one own payslip. */
  myPayslipById: (id: string) => hrmApi.get<unknown>(`/hrm/me/payslips/${id}`),
  myPayslipDownloadUrl: (id: string) =>
    hrmApi.get<{ url: string }>(`/hrm/me/payslips/${id}/download`),
  myPayslipDownloadBlob: (id: string) =>
    hrmApi.getBlob(`/hrm/me/payslips/${id}/preview`),
  /** Own HR-request inbox, optionally filtered by status. */
  myRequests: (status?: string) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/me/requests", status ? { status } : {}),
  myRequest: (id: string) => hrmApi.get<Record<string, unknown>>(`/hrm/me/requests/${id}`),
  createMyRequest: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/me/requests", body),
  addMyRequestMessage: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/me/requests/${id}/messages`, body),
  // M27 P0 UX audit: /hrm/me/letters now returns a linked-worker envelope
  // { workerId, workerName, employeeNo, linked, items } so unlinked identities
  // get linked:false instead of HTTP 422.
  myLetters: () =>
    hrmApi.get<{
      workerId: string;
      workerName: string | null;
      employeeNo: string | null;
      linked: boolean;
      items: unknown[];
    }>("/hrm/me/letters"),
  createMyLetter: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/me/letters", body),
  downloadMyLetter: async (id: string, fileName: string) => {
    const url = await hrmApi.downloadMyLetter(id);
    downloadUrl(url, fileName);
  },
  // M27 P0 UX audit: /hrm/me/documents now returns a linked-worker envelope
  // { workerId, workerName, employeeNo, linked, items } so unlinked identities
  // get linked:false instead of HTTP 422.
  myDocuments: () =>
    hrmApi.get<{
      workerId: string;
      workerName: string | null;
      employeeNo: string | null;
      linked: boolean;
      items: unknown[];
    }>("/hrm/me/documents"),
  uploadMyDocument: (file: File, category: string, title: string) =>
    hrmApi.uploadMyDocument(file, category, title),
  downloadMyDocument: async (id: string, fileName: string) => {
    const url = await hrmApi.downloadMyDocument(id);
    downloadUrl(url, fileName);
  },
  myNotifications: () =>
    hrmApi.get<{ unreadCount: number; items: Array<Record<string, unknown>> }>(
      "/hrm/me/notifications",
    ),
  markMyNotificationRead: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/me/notifications/${id}/read`, null),
  markAllMyNotificationsRead: () =>
    hrmApi.post<{ markedRead: number }>("/hrm/me/notifications/read-all", null),
  /** M35: self-service notification preferences. */
  myPreferences: () =>
    hrmApi.get<{ preferences: string | null }>("/hrm/me/preferences"),
  updateMyPreferences: (preferences: Record<string, unknown>) =>
    hrmApi.put<string>("/hrm/me/preferences", preferences),
  /** Trigger payslip document (PDF) generation, returns the updated payslip. */
  payslipGenerate: (id: string) =>
    hrmApi.post<unknown>(`/hrm/payroll/payslips/${id}/generate`, null),

  /** M38: requisition pipeline. */
  requisitions: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[]; total: number }>("/hrm/requisitions", params ?? {}),
  createRequisition: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/requisitions", body),
  requisitionDetail: (id: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/requisitions/${id}`),
  updateRequisition: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/requisitions/${id}`, body),
  submitRequisition: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/requisitions/${id}/submit`, null),
  approveRequisition: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/requisitions/${id}/approve`, body),
  returnRequisition: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/requisitions/${id}/return`, body),

  /** Recruitment: vacancies, candidates, offers. */
  recruitmentVacancies: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/recruitment/vacancies", params ?? {}),
  vacancyPipeline: (vacancyId: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/vacancies/${vacancyId}/pipeline`),
  offerLetter: (offerId: string) =>
    hrmApi.get<{ subject: string; body: string; format: string }>(`/hrm/offers/${offerId}/letter`),
  createVacancy: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/recruitment/vacancies", body),
  publishVacancy: (id: string) =>
    hrmApi.post<unknown>(`/hrm/recruitment/vacancies/${id}/publish`, null),
  closeVacancy: (id: string) =>
    hrmApi.post<unknown>(`/hrm/recruitment/vacancies/${id}/close`, null),
  vacancyCandidates: (vacancyId: string, params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>(
      `/hrm/recruitment/vacancies/${vacancyId}/candidates`,
      params ?? {},
    ),
  createCandidate: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/recruitment/candidates", body),
  advanceCandidate: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/recruitment/candidates/${id}/advance`, body),
  candidateDetail: (id: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/recruitment/candidates/${id}`),
  createInterview: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/recruitment/candidates/${id}/interviews`, body),
  decideInterview: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/recruitment/interviews/${id}/decision`, body),
  createOffer: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/recruitment/offers", body),
  recruitmentOffers: (status?: string) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/recruitment/offers", status ? { status } : {}),
  approveOffer: (id: string) => hrmApi.post<unknown>(`/hrm/recruitment/offers/${id}/approve`, null),
  issueOffer: (id: string) => hrmApi.post<unknown>(`/hrm/recruitment/offers/${id}/issue`, null),
  acceptOffer: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/recruitment/offers/${id}/accept`, body),
  declineOffer: (id: string) => hrmApi.post<unknown>(`/hrm/recruitment/offers/${id}/decline`, null),
  preboardingCases: (status?: string) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/recruitment/preboarding", status ? { status } : {}),
  updatePreboardingTask: (caseId: string, taskId: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/recruitment/preboarding/${caseId}/tasks/${taskId}`, body),
  activatePreboarding: (id: string) =>
    hrmApi.post<unknown>(`/hrm/recruitment/preboarding/${id}/activate`, null),
  /** M39: organization chart + reporting lines. */
  orgChart: () =>
    hrmApi.get<{ asAt: string; roots: unknown[] }>("/hrm/org-chart"),
  reportingLines: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[]; total: number }>("/hrm/reporting-lines", params ?? {}),
  updateReportingLines: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/reporting-lines", body),
  /** M40: HR analytics dashboard — workforce, leave, payroll cost, performance,
   * recruitment and attendance panels in a single call. */
  analyticsDashboard: () =>
    hrmApi.get<Record<string, unknown>>("/hrm/analytics/dashboard"),
  uploadCandidateDocument: (candidateId: string, file: File, category: string, title: string) =>
    hrmApi.uploadCandidateDocument(candidateId, file, category, title),

  /** Relations: cases. */
  relationsCases: (params?: Record<string, unknown>) =>
    hrmApi.get<{ items: unknown[] }>("/hrm/relations/cases", params ?? {}),
  createCase: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/relations/cases", body),
  relationsCase: (id: string) => hrmApi.get<Record<string, unknown>>(`/hrm/relations/cases/${id}`),
  declareRelationsAccess: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/relations/cases/${id}/access-declarations`, body),
  assignRelationsCase: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/relations/cases/${id}/assign`, body),
  transitionRelationsCase: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/relations/cases/${id}/transition`, body),
  createRelationsAction: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/relations/cases/${id}/actions`, body),
  updateRelationsAction: (caseId: string, actionId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(
      `/hrm/relations/cases/${caseId}/actions/${actionId}`,
      body,
    ),
  uploadRelationsEvidence: (caseId: string, file: File, title: string, evidenceType: string) =>
    hrmApi.uploadRelationsEvidence(caseId, file, title, evidenceType),
  downloadRelationsEvidence: async (evidenceId: string, fileName: string) => {
    const url = await hrmApi.downloadRelationsEvidence(evidenceId);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  },
  protectedDisclosures: (status?: string) =>
    hrmApi.get<{ items: unknown[] }>(
      "/hrm/relations/protected-disclosures",
      status ? { status } : {},
    ),
  protectedDisclosure: (id: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/relations/protected-disclosures/${id}`),
  transitionProtectedDisclosure: (id: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(
      `/hrm/relations/protected-disclosures/${id}/transition`,
      body,
    ),

  /** M44: echo of the resolved work scope (entity/branch) for the current request. */
  shell: () => hrmApi.get<{ locationId?: string | null; entityId?: string | null; scopedToBranch: boolean;
    assignedLocationIds?: string[]; confined?: boolean }>("/hrm/shell"),

  /** M45: branch access (confinement) — which platform users are confined to which branches. */
  branchAccess: () => hrmApi.get<{ items: { id: string; userId: string; userEmail: string; locationId: string; locationName?: string | null }[];
    locations: { id: string; name: string; legalEntityId: string }[] }>("/hrm/admin/branch-access"),
  assignBranchAccess: (body: { userId: string; userEmail?: string; locationId: string }) =>
    hrmApi.post<unknown>("/hrm/admin/branch-access", body),
  removeBranchAccess: (id: string) => hrmApi.delete<unknown>(`/hrm/admin/branch-access/${id}`),

  /** Admin config: org tree, legal entities, calendars, holidays, capabilities. */
  orgTree: () => hrmApi.get<unknown>("/hrm/admin/org-units/tree"),
  legalEntities: () => hrmApi.get<unknown[]>("/hrm/admin/legal-entities"),
  calendars: () => hrmApi.get<unknown[]>("/hrm/admin/calendars"),
  calendar: (id: string) => hrmApi.get<unknown>(`/hrm/admin/calendars/${id}`),
  createHoliday: (body: Record<string, unknown>) => hrmApi.post<unknown>("/hrm/admin/holidays", body),
  updateHoliday: (id: string, body: Record<string, unknown>) => hrmApi.patch<unknown>(`/hrm/admin/holidays/${id}`, body),
  deleteHoliday: (id: string) => hrmApi.delete<unknown>(`/hrm/admin/holidays/${id}`),
  capabilities: () => hrmApi.get<unknown[]>("/hrm/admin/capabilities"),
  branding: () => hrmApi.get<CompanyBranding>("/hrm/admin/branding"),
  updateBranding: (body: CompanyBrandingUpdate) => hrmApi.put<CompanyBranding>("/hrm/admin/branding", body),
  resetBranding: () => hrmApi.post<CompanyBranding>("/hrm/admin/branding/reset", {}),
  // ---------- M28 CRUD audit: jobs catalogue, roles, retention rules ----------
  jobs: (params?: { includeInactive?: boolean }) =>
    hrmApi.get<unknown[]>("/hrm/admin/jobs", params ?? {}),
  createJob: (body: Record<string, unknown>) => hrmApi.post<unknown>("/hrm/admin/jobs", body),
  updateJob: (id: string, body: Record<string, unknown>) => hrmApi.patch<unknown>(`/hrm/admin/jobs/${id}`, body),
  closeJob: (id: string) => hrmApi.post<unknown>(`/hrm/admin/jobs/${id}/close`, null),
  roles: () => hrmApi.get<unknown[]>("/hrm/admin/roles"),
  createRole: (body: Record<string, unknown>) => hrmApi.post<unknown>("/hrm/admin/roles", body),
  updateRole: (roleKey: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/admin/roles/${roleKey}`, body),
  retentionRules: () => hrmApi.get<unknown[]>("/hrm/admin/retention-rules"),
  createRetentionRule: (body: Record<string, unknown>) =>
    hrmApi.post<unknown>("/hrm/admin/retention-rules", body),
  updateRetentionRule: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/admin/retention-rules/${id}`, body),
  deleteRetentionRule: (id: string) => hrmApi.delete<unknown>(`/hrm/admin/retention-rules/${id}`),
  updateVacancy: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<unknown>(`/hrm/recruitment/vacancies/${id}`, body),

  /** M26 operational outbox status. Payloads and recipient addresses are never returned. */
  notificationDeliveries: (params?: { eventType?: string; status?: string; limit?: number }) =>
    hrmApi.get<{
      pending: number;
      publishing: number;
      published: number;
      failed: number;
      fallbackDelivered: number;
      items: Array<{
        id: string;
        publicId: string;
        eventType: string;
        status: string;
        publishAttempts: number;
        lastTransport?: string | null;
        lastError?: string | null;
        correlationId: string;
        createdAt: string;
        availableAt: string;
        publishedAt?: string | null;
      }>;
    }>("/hrm/admin/notifications", params ?? {}),
  retryNotification: (id: string) =>
    hrmApi.post<unknown>(`/hrm/admin/notifications/${id}/retry`, null),
  /** M33 external integration contracts, hand-offs, retry and reconciliation. */
  integrationDashboard: () =>
    hrmApi.get<{
      contracts: Array<{
        key: string;
        name: string;
        direction: string;
        contractVersion: string;
        transport: string;
        owner: string;
        retryStrategy: string;
        reconciliationProcess: string;
        status: string;
        detail?: string | null;
      }>;
      operations: Array<{
        id: string;
        publicId: string;
        integrationKey: string;
        operationType: string;
        status: string;
        sourceReference?: string | null;
        attemptCount: number;
        externalReference?: string | null;
        reconciliationOutcome?: string | null;
        lastError?: string | null;
        createdAt: string;
      }>;
      ready: number;
      delivered: number;
      failed: number;
      reconciled: number;
      activeWorkers: number;
      linkedWorkers: number;
      unlinkedWorkers: number;
      documentStorageMode: string;
    }>("/hrm/integrations"),
  createFinancePosting: (runId: string) =>
    hrmApi.post<Record<string, unknown>>("/hrm/integrations/finance-postings", {
      sourceId: runId,
    }),
  createPaymentHandoff: (runId: string) =>
    hrmApi.post<Record<string, unknown>>("/hrm/integrations/payment-handoffs", {
      sourceId: runId,
    }),
  createStatutoryHandoff: (exportType: string, periodId: string) =>
    hrmApi.post<Record<string, unknown>>("/hrm/integrations/statutory-handoffs", {
      exportType,
      periodId,
    }),
  createIdentitySync: (mode: "delta" | "full") =>
    hrmApi.post<Record<string, unknown>>("/hrm/integrations/identity-sync", { mode }),
  retryIntegration: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/integrations/operations/${id}/retry`, null),
  reconcileIntegration: (id: string, outcome: string, externalReference: string, note?: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/integrations/operations/${id}/reconcile`, {
      outcome,
      externalReference,
      note,
    }),
  downloadIntegration: async (id: string, fileName: string) => {
    const blob = await hrmApi.getBlob(`/hrm/integrations/operations/${id}/download`);
    const url = URL.createObjectURL(blob);
    downloadUrl(url, fileName);
  },
  /** M34 tenant-scoped security posture, role matrix, audit and retention evidence. */
  securityDashboard: () =>
    hrmApi.get<{
      tenantId: string;
      controls: Array<{
        key: string;
        name: string;
        status: string;
        detail: string;
        lastVerifiedAt?: string | null;
        expiresAt?: string | null;
        evidenceReference?: string | null;
      }>;
      roleMatrix: Array<{
        capability: string;
        description: string;
        roles: string[];
        dataScope: string;
        sensitive: boolean;
        control: string;
      }>;
      privilegedActions: Array<{
        id: string;
        actorSubjectId: string;
        actorRoles: string[];
        method: string;
        path: string;
        outcome: string;
        statusCode: number;
        requestId: string;
        createdAt: string;
      }>;
      entityAudit: Array<{
        id: string;
        entityType: string;
        entityId: string;
        action: string;
        actorSubjectId: string;
        correlationId?: string | null;
        beforeJson?: string | null;
        afterJson?: string | null;
        createdAt: string;
      }>;
      retentionRules: Array<{
        recordType: string;
        retentionMonths: number;
        legalBasis: string;
        disposition: string;
        legalHoldOverrides: boolean;
      }>;
      evidence: Array<{
        id: string;
        controlKey: string;
        status: string;
        evidenceReference: string;
        notes?: string | null;
        executedAt: string;
        expiresAt?: string | null;
        executedBySubjectId: string;
      }>;
      legalHolds: Array<{
        id: string;
        reference: string;
        scope: string;
        reason: string;
        status: string;
        placedAt: string;
        placedBySubjectId: string;
        releasedAt?: string | null;
        releasedBySubjectId?: string | null;
        releaseReason?: string | null;
      }>;
      openFindings: number;
      activeLegalHolds: number;
    }>("/hrm/security"),
  recordComplianceEvidence: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/security/evidence", body),
  placeLegalHold: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/security/legal-holds", body),
  releaseLegalHold: (id: string, reason: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/security/legal-holds/${id}/release`, { reason }),
  exportSecurityAudit: async () => {
    const blob = await hrmApi.getBlob("/hrm/security/audit/export");
    downloadUrl(URL.createObjectURL(blob), "hrm-privileged-audit.csv");
  },
  /** M36 fail-closed production readiness decision and acceptance ledger. */
  goLiveReadiness: () =>
    hrmApi.get<{
      decision: "blocked" | "ready-for-signoff" | "approved";
      canGoLive: boolean;
      evaluatedAt: string;
      passedGates: number;
      totalGates: number;
      blockers: string[];
      gates: Array<{
        key: string;
        category: string;
        name: string;
        status: string;
        detail: string;
        evidenceReference?: string | null;
        verifiedAt?: string | null;
      }>;
      signoffs: Array<{
        id: string;
        roleKey: string;
        roleName: string;
        decision: string;
        notes?: string | null;
        actorSubjectId: string;
        signedAt: string;
      }>;
    }>("/hrm/go-live"),
  recordGoLiveEvidence: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/go-live/evidence", body),
  recordGoLiveSignoff: (roleKey: string, decision: string, notes?: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/go-live/signoffs/${roleKey}`, {
      decision,
      notes,
    }),

  /* ------------------------------------------------------------------ */
  /* M19 organisation configuration CRUD (write surfaces)                */
  /* ------------------------------------------------------------------ */

  /** Create an org unit (department / cost centre). */
  createOrgUnit: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/admin/org-units", body),
  /** Patch-update an org unit (fields sent as-is, backend accepts partial). */
  updateOrgUnit: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/admin/org-units/${id}`, body),
  /** Effectively-date a unit closure (EffectiveDate must be today or later). */
  closeOrgUnit: (id: string, effectiveDate: string, reason?: string) =>
    hrmApi.post<unknown>(`/hrm/admin/org-units/${id}/close`, { effectiveDate, reason }), // OrgUnitCloseRequest.EffectiveDate
  /** Create a work location. */
  createLocation: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/admin/locations", body),
  /** Patch-update a work location. */
  updateLocation: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/admin/locations/${id}`, body),
  /** Create a legal entity. */
  createLegalEntity: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/admin/legal-entities", body),
  createWorkerAssignment: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/workers/${workerId}/assignments`, body),

  /* ------------------------------------------------------------------ */
  /* M20 payroll setup configuration (pay groups, ZRA PAYE slabs,        */
  /* NAPSA/NHIMA contribution rules, salary components)                  */
  /* ------------------------------------------------------------------ */

  /** Full pay-group list with statuses — GET /payroll/pay-groups/full. */
  payGroupsFull: () => hrmApi.get<unknown[]>("/hrm/payroll/pay-groups/full"),
  /** Patch-update a pay group (frequency, currency, payday calendar, defaults). */
  updatePayGroup: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/pay-groups/${id}`, body),
  /** Patch-update a ZRA PAYE tax slab (rate, band ceiling). */
  updateTaxSlab: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/tax-slabs/${id}`, body),
  /** Patch-update a statutory contribution rule (NAPSA/NHIMA rate/ceiling/floor). */
  updateContributionRule: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/contribution-rules/${id}`, body),
  /** Create an organisation salary component such as housing allowance. */
  createSalaryComponent: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>("/hrm/payroll/components", body),
  /** Patch-update a salary component (rate, fixed amount, taxable flag, archive). */
  updateSalaryComponent: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/payroll/components/${id}`, body),

  /** Worker lifecycle: onboarding snapshot, offboarding, bank details. */
  workerOnboarding: (workerId: string) =>
    hrmApi.get<unknown>(`/hrm/workers/${workerId}/onboarding`),
  offboardWorker: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<unknown>(`/hrm/workers/${workerId}/offboard`, body),
  addBankDetails: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/workers/${workerId}/bank-details`, body),
  updateBankDetails: (workerId: string, bankId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/workers/${workerId}/bank-details/${bankId}`, body),
  removeBankDetails: (workerId: string, bankId: string) =>
    hrmApi.delete<unknown>(`/hrm/workers/${workerId}/bank-details/${bankId}`),
  /** M33 worker history: education, external and internal work history. */
  education: (workerId: string) =>
    hrmApi.get<unknown[]>(`/hrm/workers/${workerId}/education`),
  addEducation: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/workers/${workerId}/education`, body),
  updateEducation: (workerId: string, recordId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/workers/${workerId}/education/${recordId}`, body),
  removeEducation: (workerId: string, recordId: string) =>
    hrmApi.delete<unknown>(`/hrm/workers/${workerId}/education/${recordId}`),
  externalWorkHistory: (workerId: string) =>
    hrmApi.get<unknown[]>(`/hrm/workers/${workerId}/external-work-history`),
  addExternalWorkHistory: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/workers/${workerId}/external-work-history`, body),
  updateExternalWorkHistory: (workerId: string, recordId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/workers/${workerId}/external-work-history/${recordId}`, body),
  removeExternalWorkHistory: (workerId: string, recordId: string) =>
    hrmApi.delete<unknown>(`/hrm/workers/${workerId}/external-work-history/${recordId}`),
  internalWorkHistory: (workerId: string) =>
    hrmApi.get<unknown[]>(`/hrm/workers/${workerId}/internal-work-history`),
  addInternalWorkHistory: (workerId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/workers/${workerId}/internal-work-history`, body),
  updateInternalWorkHistory: (workerId: string, recordId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/workers/${workerId}/internal-work-history/${recordId}`, body),
  removeInternalWorkHistory: (workerId: string, recordId: string) =>
    hrmApi.delete<unknown>(`/hrm/workers/${workerId}/internal-work-history/${recordId}`),
  /** M36: Performance & goal management */
  performanceCycles: (status?: string) =>
    hrmApi.get<unknown[]>(`/hrm/performance/cycles${status ? `?status=${status}` : ""}`),
  createPerformanceCycle: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/performance/cycles`, body),
  performanceCycle: (id: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/performance/cycles/${id}`),
  updatePerformanceCycle: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/performance/cycles/${id}`, body),
  closePerformanceCycle: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/performance/cycles/${id}/close`, {}),
  performanceGoals: (cycleId: string, workerId?: string) =>
    hrmApi.get<unknown[]>(`/hrm/performance/cycles/${cycleId}/goals${workerId ? `?workerId=${workerId}` : ""}`),
  createPerformanceGoal: (cycleId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/performance/cycles/${cycleId}/goals`, body),
  updatePerformanceGoal: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/performance/goals/${id}`, body),
  deletePerformanceGoal: (id: string) =>
    hrmApi.delete<unknown>(`/hrm/performance/goals/${id}`),
  performanceAssessments: (cycleId: string) =>
    hrmApi.get<unknown[]>(`/hrm/performance/cycles/${cycleId}/assessments`),
  performanceAssessment: (id: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/performance/assessments/${id}`),
  seedPerformanceAssessments: (cycleId: string) =>
    hrmApi.post<unknown[]>(`/hrm/performance/cycles/${cycleId}/assessments`, {}),
  submitManagerAssessment: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/performance/assessments/${id}/manager`, body),
  finalizeAssessment: (id: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/performance/assessments/${id}/finalize`, body),
  performanceCycleReport: (cycleId: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/performance/cycles/${cycleId}/report`),
  // M36 self-service
  myPerformance: () =>
    hrmApi.get<unknown[]>(`/hrm/me/performance`),
  myPerformanceCycle: (cycleId: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/me/performance/${cycleId}`),
  submitSelfAssessment: (assessmentId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/me/performance/${assessmentId}/self`, body),
  // M37: Offboarding & Exit Management
  offboardingRequests: (status?: string) =>
    hrmApi.get<unknown[]>(`/hrm/offboarding${status ? `?status=${status}` : ""}`),
  createOffboarding: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding`, body),
  offboardingRequest: (id: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/offboarding/${id}`),
  approveOffboarding: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${id}/approve`, {}),
  rejectOffboarding: (id: string, reason: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${id}/reject`, { reason }),
  cancelOffboarding: (id: string, reason: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${id}/cancel`, { reason }),
  markFinalPay: (id: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${id}/final-pay`, {}),
  addChecklistItem: (requestId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${requestId}/checklist`, body),
  updateChecklistItem: (requestId: string, itemId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/offboarding/${requestId}/checklist/${itemId}`, body),
  completeChecklistItem: (requestId: string, itemId: string) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${requestId}/checklist/${itemId}/complete`, {}),
  createExitInterview: (requestId: string, body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/offboarding/${requestId}/exit-interview`, body),
  getExitInterview: (requestId: string) =>
    hrmApi.get<Record<string, unknown>>(`/hrm/offboarding/${requestId}/exit-interview`),
  updateExitInterview: (requestId: string, body: Record<string, unknown>) =>
    hrmApi.patch<Record<string, unknown>>(`/hrm/offboarding/${requestId}/exit-interview`, body),
  // M37 self-service
  myOffboarding: () =>
    hrmApi.get<Record<string, unknown>>(`/hrm/me/offboarding`),
  submitResignation: (body: Record<string, unknown>) =>
    hrmApi.post<Record<string, unknown>>(`/hrm/me/offboarding`, body),
};
