/**
 * Real HTTP client for the ASP.NET Core HRM backend.
 *
 * - Base URL comes from `VITE_HRM_API_BASE` (defaults to `/api` for same-host
 *   reverse-proxy deployments and `http://localhost:5199/api` in local dev).
 * - Every request carries the `HRM-Default-TenantId` header so the backend can
 *   scope queries to the right tenant.
 * - All responses follow the backend's problem-details-ish envelope and this
 *   client normalises them into an `ApiError` class the UI can surface.
 * - OIDC auth: requests carry the current ERP-realm access token. Cookies stay
 *   enabled so the same client remains compatible with explicitly configured
 *   standalone/local deployments.
 */

import { getSession } from "@/platform/oidc";

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

const BASE = (import.meta.env.VITE_HRM_API_BASE as string | undefined)?.trim()
  ? (import.meta.env.VITE_HRM_API_BASE as string).replace(/\/$/, "")
  : "/api";
const TENANT_ID =
  (import.meta.env.VITE_HRM_TENANT_ID as string | undefined)?.trim() ||
  "019ffa8b-0fb0-71e6-849a-f76e5a28e0b5";

// M15 self-service: only the fields a worker may edit on their own record.
// Admin-only fields (name, grade, job title, status, ...) are deliberately
// absent so they can never be submitted from the client.
export interface SelfProfileUpdate {
  preferredName?: string;
  email?: string;
  phone?: string;
  nrc?: string;
  passportNo?: string;
  tpin?: string;
  napsaNumber?: string;
  nhimaNumber?: string;
  nationality?: string;
  dateOfBirth?: string;
  emergencyContacts?: {
    relationship: string;
    fullName: string;
    phone?: string;
    isPrimary: boolean;
  }[];
  bankDetails?: {
    bankName: string;
    branchCode: string;
    accountNumber: string;
    accountName: string;
    isPrimary: boolean;
    paymentMethod?: string;
    mobileMoneyNumber?: string;
  }[];
}

export interface LocalAuthUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  workerId?: string | null;
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginAt?: string | null;
  createdAt?: string;
}

export interface LocalAuthResult {
  authenticated: boolean;
  user: LocalAuthUser;
}

export interface IdentityAccessUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  isActive: boolean;
  federated: boolean;
  source?: "idp" | "local";
}

export interface IdentityDirectoryUser {
  id: string;
  email: string;
  displayName: string;
}

export interface CompanyBranding {
  displayName: string;
  primaryColor: string;
  secondaryColor: string;
  accentColor: string;
  railColor: string;
  logoLightDataUri?: string | null;
  logoDarkDataUri?: string | null;
  faviconDataUri?: string | null;
  updatedAt?: string | null;
}
export type CompanyBrandingUpdate = Partial<CompanyBranding>;

/** Minimal shape of the linked worker returned by `hrmApi.myProfile()`. */
export interface LinkedWorker {
  id: string;
  employeeNo: string;
  fullName: string;
  preferredName?: string | null;
  jobTitle?: string | null;
  grade?: string | null;
  email?: string | null;
  photoUrl?: string | null;
  status: string;
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (res.status === 204) return undefined as T;
  let payload: unknown = undefined;
  const text = await res.text();
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }
  if (!res.ok) {
    const problem = payload && typeof payload === "object" ? (payload as { title?: unknown; message?: unknown; code?: unknown }) : null;
    const title =
      res.status === 403 ? "You do not have permission to access this page."
        : res.status === 401 ? "Your session has expired. Please sign in again."
          : problem?.message ? String(problem.message)
            : problem?.title ? String(problem.title)
              : `HTTP ${res.status}`;
    const code =
      problem?.code
        ? String(problem.code)
        : undefined;
    throw new ApiError(title || text || `HTTP ${res.status}`, res.status, code);
  }
  return payload as T;
}

function headers(extra?: Record<string, string>): Record<string, string> {
  // M44 branch scoping: the top-nav org switcher writes the selected scope
  // into localStorage (`erp.shell.state.v1` → { entityId?, branch? }). When a
  // branch is selected the operator works inside that branch; when only an
  // entity is selected the operator sees the whole entity. The backend
  // middleware validates the header against the DB, so an invalid guid is
  // rejected with 400 rather than silently ignored.
  const shellHeaders: Record<string, string> = {};
  try {
    const raw = typeof localStorage !== "undefined" ? localStorage.getItem("erp.shell.state.v1") : null;
    if (raw) {
      const shell = JSON.parse(raw) as { entityId?: string; branch?: string } | null;
      if (shell) {
        if (shell.branch) shellHeaders["X-Shell-Location"] = shell.branch;
        else if (shell.entityId) shellHeaders["X-Shell-Entity"] = shell.entityId;
      }
    }
  } catch {
    // Corrupt or missing shell state — send no scope and let the backend
    // treat the operator as global (entity-wide) view.
  }
  const session = typeof localStorage !== "undefined" ? getSession() : null;
  const authHeaders: Record<string, string> = {};
  if (session?.accessToken) authHeaders.Authorization = `Bearer ${session.accessToken}`;
  return {
    Accept: "application/json",
    "HRM-Default-TenantId": TENANT_ID,
    ...authHeaders,
    ...shellHeaders,
    ...extra,
  };
}

function qs(params: Record<string, unknown>): string {
  const entries: [string, string][] = [];
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    entries.push([encodeURIComponent(key), encodeURIComponent(String(value))]);
  }
  if (entries.length === 0) return "";
  return "?" + entries.map(([k, v]) => `${k}=${v}`).join("&");
}

/** Generic typed wrapper around the HRM API surface. */
export const hrmApi = {
  identity: {
    users: () =>
      hrmApi.get<{ provider: string; realm: string; items: IdentityAccessUser[] }>(
        "/hrm/identity/users",
      ),
    searchDirectory: (query: string) =>
      hrmApi.get<{ items: IdentityDirectoryUser[] }>("/hrm/identity/users/directory", { query }),
    inviteUser: (body: {
      email: string;
      displayName: string;
      roles: string[];
      sourceUserId: string;
    }) =>
      hrmApi.post<IdentityAccessUser>("/hrm/identity/users", body),
    updateUser: (id: string, body: Partial<{ roles: string[]; isActive: boolean }>) =>
      hrmApi.patch<IdentityAccessUser>(`/hrm/identity/users/${id}`, body),
    sendPasswordLink: (id: string) =>
      hrmApi.post<{ sent: boolean }>(`/hrm/identity/users/${id}/send-password-link`, {}),
  },
  auth: {
    login: (email: string, password: string) =>
      hrmApi.post<LocalAuthResult>("/hrm/auth/login", { email, password }),
    me: () => hrmApi.get<{ authenticated: boolean; user: LocalAuthUser | null }>("/hrm/auth/me"),
    logout: () => hrmApi.post<{ authenticated: false }>("/hrm/auth/logout", {}),
    changePassword: (currentPassword: string, newPassword: string) =>
      hrmApi.post<{ changed: boolean }>("/hrm/auth/change-password", { currentPassword, newPassword }),
    users: () => hrmApi.get<{ items: LocalAuthUser[] }>("/hrm/auth/users"),
    user: (id: string) => hrmApi.get<{ user: LocalAuthUser; activity: { action: string; actor: string; at: string }[]; sessions: { createdAt: string; lastSeenAt: string; expiresAt: string; revokedAt?: string | null; userAgent?: string | null }[] }>(`/hrm/auth/users/${id}`),
    createUser: (body: { email: string; displayName: string; roles: string[]; workerId?: string }) =>
      hrmApi.post<LocalAuthUser>("/hrm/auth/users", body),
    updateUser: (id: string, body: Partial<{ email: string; displayName: string; roles: string[]; isActive: boolean; workerId: string }>) =>
      hrmApi.patch<LocalAuthUser>(`/hrm/auth/users/${id}`, body),
    resetPassword: (id: string, newPassword: string) =>
      hrmApi.post<{ reset: boolean }>(`/hrm/auth/users/${id}/reset-password`, { newPassword }),
    sendPasswordLink: (id: string) =>
      hrmApi.post<{ sent: boolean }>(`/hrm/auth/users/${id}/send-password-link`, {}),
    setPassword: (token: string, newPassword: string) =>
      hrmApi.post<{ changed: boolean }>("/hrm/auth/set-password", { token, newPassword }),
  },

  async get<T>(path: string, params?: Record<string, unknown>): Promise<T> {
    const res = await fetch(`${BASE}${path}${qs(params ?? {})}`, {
      credentials: "include",
      headers: headers(),
    });
    return handleResponse<T>(res);
  },

  async post<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "POST",
      credentials: "include",
      headers: { ...headers(), "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return handleResponse<T>(res);
  },

  async put<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "PUT",
      credentials: "include",
      headers: { ...headers(), "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return handleResponse<T>(res);
  },

  async patch<T>(path: string, body: unknown): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "PATCH",
      credentials: "include",
      headers: { ...headers(), "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    return handleResponse<T>(res);
  },

  async delete<T>(path: string): Promise<T> {
    const res = await fetch(`${BASE}${path}`, {
      method: "DELETE",
      credentials: "include",
      headers: headers(),
    });
    return handleResponse<T>(res);
  },

  /** Upload a file for a worker document (multipart). */
  async uploadDocument(
    workerId: string,
    file: File,
    category: string,
    title: string,
  ): Promise<unknown> {
    const form = new FormData();
    form.append("file", file);
    form.append("workerId", workerId);
    form.append("category", category);
    form.append("title", title);
    const res = await fetch(`${BASE}/hrm/documents/upload`, {
      method: "POST",
      headers: headers(),
      body: form,
    });
    return handleResponse(res);
  },

  /** Bulk import employees from a CSV file. POST /hrm/workers/import (multipart). */
  async uploadWorkersCsv(
    file: File,
  ): Promise<{ created: number; skipped: number; errors: Array<{ row: number; detail: string }> }> {
    const form = new FormData();
    form.append("file", file);
    const res = await fetch(`${BASE}/hrm/workers/import`, {
      method: "POST",
      headers: headers(),
      body: form,
    });
    return handleResponse(res);
  },

  async uploadCandidateDocument(
    candidateId: string,
    file: File,
    category: string,
    title: string,
  ): Promise<unknown> {
    const form = new FormData();
    form.append("file", file);
    form.append("category", category);
    form.append("title", title);
    const res = await fetch(`${BASE}/hrm/recruitment/candidates/${candidateId}/documents`, {
      method: "POST",
      headers: headers(),
      body: form,
    });
    return handleResponse(res);
  },

  async uploadRelationsEvidence(
    caseId: string,
    file: File,
    title: string,
    evidenceType: string,
  ): Promise<unknown> {
    const form = new FormData();
    form.append("file", file);
    form.append("title", title);
    form.append("evidenceType", evidenceType);
    const res = await fetch(`${BASE}/hrm/relations/cases/${caseId}/evidence`, {
      method: "POST",
      headers: headers(),
      body: form,
    });
    return handleResponse(res);
  },

  async uploadMyDocument(file: File, category: string, title: string): Promise<unknown> {
    const form = new FormData();
    form.append("file", file);
    form.append("category", category);
    form.append("title", title);
    const res = await fetch(`${BASE}/hrm/me/documents`, {
      method: "POST",
      headers: headers(),
      body: form,
    });
    return handleResponse(res);
  },

  async downloadMyDocument(documentId: string): Promise<string> {
    const res = await fetch(`${BASE}/hrm/me/documents/${documentId}/download`, {
      headers: headers(),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(text || `HTTP ${res.status}`, res.status);
    }
    return URL.createObjectURL(await res.blob());
  },

  async downloadMyLetter(letterId: string): Promise<string> {
    const res = await fetch(`${BASE}/hrm/me/letters/${letterId}/download`, {
      headers: headers(),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(text || `HTTP ${res.status}`, res.status);
    }
    return URL.createObjectURL(await res.blob());
  },

  async downloadRelationsEvidence(evidenceId: string): Promise<string> {
    const res = await fetch(`${BASE}/hrm/relations/evidence/${evidenceId}/download`, {
      headers: headers(),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(text || `HTTP ${res.status}`, res.status);
    }
    return URL.createObjectURL(await res.blob());
  },

  /** Stream a document by id and return a Blob URL caller must revoke. */
  async downloadDocument(documentId: string): Promise<string> {
    const res = await fetch(`${BASE}/hrm/documents/${documentId}/download`, {
      headers: headers(),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(text || `HTTP ${res.status}`, res.status);
    }
    const blob = await res.blob();
    return URL.createObjectURL(blob);
  },

  // M23: non-JSON download (statutory CSV filings). Errors still surface as
  // ApiError; a successful response is returned as a raw Blob.
  async getBlob(
    path: string,
    params?: Record<string, unknown>,
    extra?: Record<string, string>,
  ): Promise<Blob> {
    const res = await fetch(`${BASE}${path}${qs(params ?? {})}`, {
      credentials: "include",
      headers: { ...headers(), Accept: "text/csv", ...extra },
    });
    if (!res.ok) {
      const text = await res.text();
      throw new ApiError(text || `HTTP ${res.status}`, res.status);
    }
    return res.blob();
  },

  /**
   * M14 identity link: resolve the worker record bound to the caller's Keycloak
   * subject. Returns `{ linked, worker, subject }` — worker is null when the
   * signed-in identity is not linked to an HRM worker yet.
   */
  myProfile: () =>
    hrmApi.get<{ linked: boolean; worker: LinkedWorker | null; subject: string }>("/hrm/me"),

  // M15 self-service: update the worker record linked to the caller's token
  // subject. PUT /hrm/me/profile — the backend re-reads the subject from the
  // token, so the client cannot target another worker.
  updateSelfProfile: (body: SelfProfileUpdate) =>
    hrmApi.put<{ worker: LinkedWorker }>("/hrm/me/profile", body),

  /** Data-quality check summary for the tenant. */
  dqChecks: () => hrmApi.get<unknown>("/hrm/dq/checks"),

  /** Statutory export CSV (NAPSA / NHIMA / ZRA / napsa-bankfile). */
  statutoryExport: (exportType: string, periodId: string) =>
    hrmApi.getBlob(`/hrm/statutory-exports`, { exportType, periodId }),
  statutoryExportPreview: (exportType: string, periodId: string) =>
    hrmApi.get<unknown>(`/hrm/statutory-exports/preview`, { exportType, periodId }),

  // ---- M16: self-service leave (always keyed on the caller's token subject)

  /**
   * The signed-in worker's own leave inbox: identity, balances across every
   * leave type, and their own leave requests. GET /hrm/me/leave.
   */
  myLeave: () => hrmApi.get<MyLeave>("/hrm/me/leave"),

  /**
   * Cancel an open leave request owned by the caller. POST
   * /hrm/me/leave/{id}/cancel — only submitted/in-review/returned requests can
   * be cancelled and the balance reservation is released.
   */
  cancelLeave: (leaveId: string) =>
    hrmApi.post<LeaveRequestLine>(`/hrm/me/leave/${leaveId}/cancel`, {}),

  /* ------------------------------------------------------------------ */
  /* M25: employee self-service payslips + requests inbox. All keyed on    */
  /* the caller's OIDC subject — the client can never misdirect them.      */
  /* ------------------------------------------------------------------ */

  /**
   * The signed-in worker's own payslips. GET /hrm/me/payslips — released and
   * final slips for the worker linked to the caller's token. An unlinked
   * identity gets an empty list, never another worker's data.
   */
  myPayslips: () => hrmApi.get<MyPayslips>("/hrm/me/payslips"),

  /** Full snapshot of one own payslip. GET /hrm/me/payslips/{id}. */
  myPayslipById: (id: string) => hrmApi.get<MyPayslip>(`/hrm/me/payslips/${id}`),

  /**
   * The signed-in worker's own HR-request inbox (optionally filtered by
   * status). GET /hrm/me/requests — an empty subject or unlinked identity
   * always returns an empty list.
   */
  myRequests: (status?: string) =>
    hrmApi.get<MyRequests>("/hrm/me/requests", status ? { status } : {}),
};

/** M25: the signed-in worker's own payslips — Paged envelope of MyPayslip. */
export interface MyPayslips {
  items: MyPayslip[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/**
 * M25: one own payslip row. Matches `PayslipDto` (M21 fields plus the M24
 * statutory snapshot: workerNrc / workerTpin / workerNapsaNumber /
 * workerNhimaNumber).
 */
export interface MyPayslip {
  id: string;
  payslipNo: string;
  version: number;
  grossPay: number;
  totalDeductions: number;
  netPay: number;
  ytdGross?: string | null;
  ytdTax?: string | null;
  ytdNet?: string | null;
  status: string;
  documentUrl?: string | null;
  releasedAt?: string | null;
  supersedesId?: string | null;
  workerNrc?: string | null;
  workerTpin?: string | null;
  workerNapsaNumber?: string | null;
  workerNhimaNumber?: string | null;
}

/** M25: the signed-in worker's own HR requests — Paged envelope of MyHrRequest. */
export interface MyRequests {
  items: MyHrRequest[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** M25: one own HR-request row. Matches `HrRequestDto`. */
export interface MyHrRequest {
  id: string;
  workerId?: string | null;
  workerName: string;
  category: string;
  subject: string;
  status: string;
  confidentiality: string;
  createdAt: string;
  messages: Array<{ body: string; createdAt: string }>;
}

/** One balance row returned by the self-service leave inbox. */
export interface MyLeaveBalance {
  leaveTypeCode: string;
  leaveTypeName: string;
  accrued: number;
  taken: number;
  reserved: number;
  available: number;
}

/** One leave request row inside the caller's own inbox. */
export interface SelfLeaveRequest {
  id: string;
  leaveTypeCode: string;
  startDate: string;
  endDate: string;
  requestedDays: number;
  status: string;
  rejectionReason?: string | null;
  crossesCutoff: boolean;
  createdAt: string;
}

/** Full self-service leave envelope: identity, balances and own requests. */
export interface MyLeave {
  workerId: string;
  workerName: string;
  employeeNo?: string | null;
  linked: boolean;
  balances: MyLeaveBalance[];
  requests: SelfLeaveRequest[];
}

/** Cancel endpoint returns the updated admin-style leave row. */
export interface LeaveRequestLine {
  id: string;
  status: string;
  requestedDays: number;
}
