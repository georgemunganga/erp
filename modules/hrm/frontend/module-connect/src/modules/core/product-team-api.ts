/** Company governance only. Proofs and CSRF tokens remain in memory. */
const prefix = "/api/core/product-identity/team";
const uuid =
  /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
export const productTeamStart = `${prefix}/start`;
export type TeamRole = "owner" | "administrator" | "member";
export type TeamSession = {
  csrf: string;
  expiresAt: string;
  invitationManagementAvailable: boolean;
};
export type TeamInvitation = {
  id: string;
  recipient: string;
  role: "administrator" | "member";
  state: "pending" | "accepted" | "revoked" | "expired";
  revision: number;
  expiresAt: string;
  delivery: {
    state:
      | "queued"
      | "retrying"
      | "accepted"
      | "suppressed"
      | "invalid"
      | "exhausted"
      | "not-required"
      | "unavailable";
    attempts: number;
    retryAt: string | null;
    acceptedAt: string | null;
  };
};
export type InvitationCommand = { companyId: string; linkId: string; key: string } & (
  | { operation: "issue"; email: string; role: "administrator" | "member" }
  | { operation: "revoke"; invitationId: string; revision: number }
);
export type TeamCompany = {
  id: string;
  linkId: string;
  name: string;
  role: Exclude<TeamRole, "member">;
};
export type TeamMember = {
  id: string;
  name: string;
  role: TeamRole;
  active: boolean;
  identityAvailable: boolean;
  revision: number;
};
export type TeamCommand = {
  companyId: string;
  linkId: string;
  memberId: string;
  key: string;
  revision: number;
} & ({ operation: "role-change"; role: TeamRole } | { operation: "revoke" });
export class ProductTeamError extends Error {
  readonly status: number;
  readonly code: string;
  constructor(status: number, code = "team_request_failed") {
    super(code);
    this.name = "ProductTeamError";
    this.status = status;
    this.code = code;
  }
}
function invalid(): never {
  throw new ProductTeamError(502, "invalid_team_response");
}
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return invalid();
  return value as Record<string, unknown>;
}
function role(value: unknown): value is TeamRole {
  return value === "owner" || value === "administrator" || value === "member";
}
function id(value: unknown): value is string {
  return typeof value === "string" && uuid.test(value);
}
function revision(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value > 0 && value <= 2147483647;
}
function text(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0 && value.length <= 512;
}
function page(data: Record<string, unknown>, field: string) {
  if (
    !Array.isArray(data[field]) ||
    data[field].length > 20 ||
    data.operational_access_granted !== false ||
    !(data.next_cursor === null || id(data.next_cursor))
  )
    return invalid();
  return { rows: data[field] as unknown[], next: data.next_cursor as string | null };
}

function invitationDelivery(value: unknown): TeamInvitation["delivery"] {
  // Older APIs can roll out independently; absence must never imply email sent.
  if (value === undefined)
    return { state: "unavailable", attempts: 0, retryAt: null, acceptedAt: null };
  const data = object(value);
  const state = data.state;
  const attempts = data.attempts;
  const date = (v: unknown): v is string => typeof v === "string" && Number.isFinite(Date.parse(v));
  if (
    typeof state !== "string" ||
    ![
      "queued",
      "retrying",
      "accepted",
      "suppressed",
      "invalid",
      "exhausted",
      "not-required",
      "unavailable",
    ].includes(state) ||
    typeof attempts !== "number" ||
    !Number.isInteger(attempts) ||
    attempts < 0 ||
    attempts > 8 ||
    !(data.retry_at === null || date(data.retry_at)) ||
    !(data.accepted_at === null || date(data.accepted_at))
  )
    return invalid();
  if (
    (state === "accepted" ? attempts < 1 || !date(data.accepted_at) : data.accepted_at !== null) ||
    (state === "retrying"
      ? attempts < 1 || attempts > 7 || !date(data.retry_at)
      : data.retry_at !== null) ||
    ((state === "queued" || state === "unavailable") && attempts !== 0) ||
    (state === "not-required" && attempts > 7) ||
    ((state === "suppressed" || state === "invalid") && attempts < 1) ||
    (state === "exhausted" && attempts !== 8)
  )
    return invalid();
  return {
    state: state as TeamInvitation["delivery"]["state"],
    attempts,
    retryAt: data.retry_at as string | null,
    acceptedAt: data.accepted_at as string | null,
  };
}

export function createProductTeamClient(fetcher: typeof fetch = fetch) {
  async function request(
    path: string,
    signal?: AbortSignal,
    post?: { csrf: string; body: unknown },
  ) {
    const response = await fetcher(prefix + path, {
      method: post ? "POST" : "GET",
      credentials: "same-origin",
      mode: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal: signal
        ? AbortSignal.any([signal, AbortSignal.timeout(30000)])
        : AbortSignal.timeout(30000),
      headers: post
        ? {
            Accept: "application/json",
            "Content-Type": "application/json",
            "X-Kuubu-Team-CSRF": post.csrf,
          }
        : { Accept: "application/json" },
      body: post ? JSON.stringify(post.body) : undefined,
    });
    if (!response.ok) {
      let code = "team_request_failed";
      if (response.status === 409) {
        try {
          const data = object(await response.json());
          if (data.status === "LastOwner" || data.status === "Conflict") code = data.status;
        } catch {
          /* Never display raw response content. */
        }
      }
      throw new ProductTeamError(response.status, code);
    }
    return response;
  }
  async function json(response: Response) {
    if (!response.headers.get("content-type")?.toLowerCase().startsWith("application/json"))
      return invalid();
    try {
      return object(await response.json());
    } catch {
      return invalid();
    }
  }
  function cursor(after?: string) {
    if (after !== undefined && !id(after)) throw new ProductTeamError(400);
    return after ? `after=${after}` : "";
  }
  return {
    async session(signal?: AbortSignal): Promise<TeamSession> {
      const data = await json(await request("/session", signal));
      if (
        data.verified !== true ||
        data.product_code !== "kuubu" ||
        data.purpose !== "company-team-v1" ||
        data.operational_access_granted !== false ||
        typeof data.csrf_token !== "string" ||
        !/^[A-Za-z0-9_-]{64,4096}$/.test(data.csrf_token) ||
        typeof data.expires_at !== "string" ||
        !Number.isFinite(Date.parse(data.expires_at)) ||
        (data.invitation_management_available !== undefined &&
          typeof data.invitation_management_available !== "boolean")
      )
        return invalid();
      return {
        csrf: data.csrf_token,
        expiresAt: data.expires_at,
        invitationManagementAvailable: data.invitation_management_available === true,
      };
    },
    async companies(after?: string, signal?: AbortSignal) {
      const query = cursor(after);
      const data = await json(await request(`/companies${query ? `?${query}` : ""}`, signal));
      if (data.license_check_required !== true) return invalid();
      const result = page(data, "companies");
      const companies = result.rows.map((value) => {
        const row = object(value);
        if (
          !id(row.company_id) ||
          !id(row.link_id) ||
          !text(row.company_name) ||
          (row.role !== "owner" && row.role !== "administrator")
        )
          return invalid();
        return {
          id: row.company_id,
          linkId: row.link_id,
          name: row.company_name,
          role: row.role,
        } satisfies TeamCompany;
      });
      if (new Set(companies.map((row) => row.id)).size !== companies.length) return invalid();
      return { companies, next: result.next };
    },
    async members(
      company: Pick<TeamCompany, "id" | "linkId">,
      after?: string,
      signal?: AbortSignal,
    ) {
      if (!id(company.id) || !id(company.linkId)) throw new ProductTeamError(400);
      const tail = cursor(after);
      const data = await json(
        await request(
          `/members?company_id=${company.id}&link_id=${company.linkId}${tail ? `&${tail}` : ""}`,
          signal,
        ),
      );
      if (data.actor_role !== "owner" && data.actor_role !== "administrator") return invalid();
      const result = page(data, "members");
      const members = result.rows.map((value) => {
        const row = object(value);
        if (
          !id(row.membership_id) ||
          !text(row.display_name) ||
          !role(row.role) ||
          typeof row.membership_active !== "boolean" ||
          typeof row.identity_available !== "boolean" ||
          !revision(row.revision)
        )
          return invalid();
        return {
          id: row.membership_id,
          name: row.display_name,
          role: row.role,
          active: row.membership_active,
          identityAvailable: row.identity_available,
          revision: row.revision,
        } satisfies TeamMember;
      });
      if (new Set(members.map((row) => row.id)).size !== members.length) return invalid();
      return {
        members,
        next: result.next,
        actorRole: data.actor_role as "owner" | "administrator",
      };
    },
    async execute(command: TeamCommand, csrf: string) {
      if (
        ![command.companyId, command.linkId, command.memberId, command.key].every(id) ||
        !revision(command.revision) ||
        !(
          command.operation === "revoke" ||
          (command.operation === "role-change" && role(command.role))
        )
      )
        throw new ProductTeamError(400);
      const body = {
        company_id: command.companyId,
        link_id: command.linkId,
        membership_id: command.memberId,
        command_key: command.key,
        expected_revision: command.revision,
        operation: command.operation,
        confirmed: true,
        ...(command.operation === "role-change" ? { role: command.role } : {}),
      };
      const data = await json(await request("/commands", undefined, { csrf, body }));
      if (
        !["Changed", "Unchanged", "Existing"].includes(String(data.status)) ||
        data.membership_id !== command.memberId ||
        !revision(data.revision) ||
        !role(data.role) ||
        typeof data.membership_active !== "boolean" ||
        data.operational_access_granted !== false ||
        (command.operation === "revoke"
          ? data.membership_active !== false
          : data.role !== command.role || data.membership_active !== true) ||
        (data.revision !== command.revision && data.revision !== command.revision + 1)
      )
        return invalid();
      // Historical Existing receipts are acknowledged, never installed as current
      // member state. The screen must fetch members again after every success.
    },
    async invitations(
      company: Pick<TeamCompany, "id" | "linkId">,
      after?: string,
      signal?: AbortSignal,
    ) {
      if (!id(company.id) || !id(company.linkId)) throw new ProductTeamError(400);
      const ending = cursor(after);
      const query = `company_id=${company.id}&link_id=${company.linkId}${ending ? `&${ending}` : ""}`;
      const data = await json(await request(`/invitations?${query}`, signal));
      if (data.actor_role !== "owner" && data.actor_role !== "administrator") return invalid();
      const result = page(data, "invitations");
      let previous = after ?? "";
      const invitations = result.rows.map((value): TeamInvitation => {
        const row = object(value);
        if (
          !id(row.invitation_id) ||
          row.invitation_id <= previous ||
          typeof row.recipient !== "string" ||
          row.recipient.length > 254 ||
          !row.recipient.includes("@") ||
          /[\s\p{Cc}]/u.test(row.recipient) ||
          (row.role !== "administrator" && row.role !== "member") ||
          !["pending", "accepted", "revoked", "expired"].includes(String(row.state)) ||
          !revision(row.revision) ||
          row.revision !== (row.state === "pending" || row.state === "expired" ? 1 : 2) ||
          typeof row.expires_at !== "string" ||
          !Number.isFinite(Date.parse(row.expires_at))
        )
          return invalid();
        previous = row.invitation_id;
        return {
          id: row.invitation_id,
          recipient: row.recipient,
          role: row.role,
          state: row.state as TeamInvitation["state"],
          revision: row.revision,
          expiresAt: row.expires_at,
          delivery: invitationDelivery(row.delivery),
        };
      });
      if (result.next !== null && (invitations.length !== 20 || result.next !== previous))
        return invalid();
      return {
        invitations,
        next: result.next,
        actorRole: data.actor_role as "owner" | "administrator",
      };
    },
    async invitationCommand(command: InvitationCommand, csrf: string) {
      if (command.operation !== "issue" && command.operation !== "revoke")
        throw new ProductTeamError(400);
      if (![command.companyId, command.linkId, command.key].every(id))
        throw new ProductTeamError(400);
      const body = {
        company_id: command.companyId,
        link_id: command.linkId,
        command_key: command.key,
        confirmed: true,
        ...(command.operation === "issue"
          ? { email: command.email, role: command.role }
          : { invitation_id: command.invitationId, expected_revision: command.revision }),
      };
      if (
        command.operation === "issue"
          ? (command.role !== "member" && command.role !== "administrator") ||
            command.email.length > 254 ||
            !command.email.includes("@") ||
            /[\s\p{Cc}]/u.test(command.email)
          : !id(command.invitationId) || !revision(command.revision)
      )
        throw new ProductTeamError(400);
      const response = await request(`/invitations/${command.operation}`, undefined, {
        csrf,
        body,
      });
      const data = await json(response);
      if (
        !id(data.invitation_id) ||
        data.operational_access_granted !== false ||
        (command.operation === "issue"
          ? !(
              (response.status === 201 && data.status === "Issued") ||
              (response.status === 200 && data.status === "Existing")
            ) ||
            data.state !== "pending" ||
            data.revision !== 1
          : response.status !== 200 ||
            !["Revoked", "Existing"].includes(String(data.status)) ||
            data.state !== "revoked" ||
            data.revision !== 2 ||
            data.invitation_id !== command.invitationId)
      )
        return invalid();
      // Receipt is historical; callers must refresh the current invitation list.
    },
    async cancel(csrf: string) {
      await request("/cancel", undefined, { csrf, body: {} });
    },
  };
}
