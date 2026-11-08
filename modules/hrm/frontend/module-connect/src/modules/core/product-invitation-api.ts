/** This client never stores an invitation secret or a provider bearer token. */
const prefix = "/api/core/product-identity/invitation";
export const invitationStart = prefix + "/start";
const opaque = /^[A-Za-z0-9_-]{43}$/;
const uuid =
  /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
export type InvitationSession = { verified: boolean; csrf: string; expiresAt: string };
export type InvitationReview = {
  id: string;
  companyId: string;
  companyName: string;
  role: "member" | "administrator";
  revision: 1;
  expiresAt: string;
};
export type InvitationAttempt = { key: string; revision: 1; invitationId: string };
export class InvitationError extends Error {
  readonly status: number;
  constructor(status: number) {
    super("invitation_request_failed");
    this.name = "InvitationError";
    this.status = status;
  }
}
function invalid(): never {
  throw new InvitationError(502);
}
function object(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : invalid();
}
function fields(value: Record<string, unknown>, allowed: string[]) {
  if (
    Object.keys(value).length !== allowed.length ||
    Object.keys(value).some((key) => !allowed.includes(key)) ||
    value.operational_access_granted !== false
  )
    invalid();
}
function date(value: unknown, max?: number): string {
  if (typeof value !== "string" || !/(?:Z|[+-]\d{2}:\d{2})$/.test(value)) return invalid();
  const time = Date.parse(value);
  if (!Number.isFinite(time) || time <= Date.now() || (max && time > Date.now() + max))
    return invalid();
  return value;
}
function id(value: unknown): value is string {
  return typeof value === "string" && uuid.test(value);
}
function secret(value: unknown): value is string {
  return typeof value === "string" && opaque.test(value);
}
export function createInvitationClient(fetcher: typeof fetch = fetch) {
  async function request(
    path: string,
    signal?: AbortSignal,
    post?: { csrf: string; body: unknown },
    expectedStatus = 200,
  ) {
    let response: Response;
    try {
      response = await fetcher(prefix + path, {
        method: post ? "POST" : "GET",
        credentials: "same-origin",
        mode: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: signal
          ? AbortSignal.any([signal, AbortSignal.timeout(30000)])
          : AbortSignal.timeout(30000),
        headers: {
          Accept: "application/json",
          ...(post
            ? { "Content-Type": "application/json", "X-Kuubu-Invitation-CSRF": post.csrf }
            : {}),
        },
        ...(post ? { body: JSON.stringify(post.body) } : {}),
      });
    } catch {
      throw new InvitationError(503);
    }
    if (!response.ok) throw new InvitationError(response.status);
    if (response.status !== expectedStatus) return invalid();
    if (response.status === 204) return null;
    if (response.headers.get("content-type")?.split(";")[0] !== "application/json")
      return invalid();
    let text: string;
    try {
      text = await response.text();
    } catch {
      throw new InvitationError(503);
    }
    if (text.length > 8192) return invalid();
    try {
      return object(JSON.parse(text));
    } catch {
      return invalid();
    }
  }
  function csrf(value: string) {
    if (!secret(value)) throw new InvitationError(400);
  }
  return {
    async session(signal?: AbortSignal): Promise<InvitationSession> {
      const value = object(await request("/session", signal));
      fields(value, [
        "verified",
        "purpose",
        "csrf_token",
        "expires_at",
        "operational_access_granted",
      ]);
      if (
        typeof value.verified !== "boolean" ||
        value.purpose !== "company-invitation-v1" ||
        typeof value.csrf_token !== "string" ||
        !opaque.test(value.csrf_token)
      )
        return invalid();
      return {
        verified: value.verified,
        csrf: value.csrf_token,
        expiresAt: date(value.expires_at, 305000),
      };
    },
    async prepare(token: string, nonce: string): Promise<void> {
      csrf(nonce);
      if (!secret(token)) throw new InvitationError(400);
      const value = object(
        await request("/prepare", undefined, { csrf: nonce, body: { token } }, 201),
      );
      fields(value, ["continue_url", "operational_access_granted"]);
      if (value.continue_url !== invitationStart) return invalid();
    },
    async review(signal?: AbortSignal): Promise<InvitationReview> {
      const value = object(await request("/review", signal));
      fields(value, [
        "invitation_id",
        "company_id",
        "company_name",
        "role",
        "revision",
        "expires_at",
        "operational_access_granted",
      ]);
      if (
        !id(value.invitation_id) ||
        !id(value.company_id) ||
        typeof value.company_name !== "string" ||
        value.company_name.trim().length === 0 ||
        value.company_name.length > 200 ||
        [...value.company_name].some(
          (char) => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127,
        ) ||
        (value.role !== "member" && value.role !== "administrator") ||
        value.revision !== 1
      )
        return invalid();
      return {
        id: value.invitation_id,
        companyId: value.company_id,
        companyName: value.company_name,
        role: value.role,
        revision: 1,
        expiresAt: date(value.expires_at, 172805000),
      };
    },
    async accept(attempt: InvitationAttempt, nonce: string): Promise<void> {
      csrf(nonce);
      if (!id(attempt.key) || !id(attempt.invitationId) || attempt.revision !== 1)
        throw new InvitationError(400);
      const value = object(
        await request("/accept", undefined, {
          csrf: nonce,
          body: { command_key: attempt.key, expected_revision: attempt.revision, confirmed: true },
        }),
      );
      fields(value, [
        "status",
        "invitation_id",
        "membership_id",
        "revision",
        "operational_access_granted",
      ]);
      if (
        (value.status !== "Accepted" && value.status !== "Existing") ||
        value.invitation_id !== attempt.invitationId ||
        !id(value.membership_id) ||
        value.revision !== 2
      )
        return invalid();
    },
    async cancel(nonce: string): Promise<void> {
      csrf(nonce);
      const value = await request("/cancel", undefined, { csrf: nonce, body: {} }, 204);
      if (value !== null) return invalid();
    },
  };
}
