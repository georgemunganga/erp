/** Setup-only browser contract. No bearer, refresh token or ERP session is stored here. */
const prefix = "/api/core/product-identity/setup";
const ulid = /^[0-7][0-9a-hjkmnp-tv-z]{25}$/;
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export const productSetupStart = `${prefix}/start`;
export type SetupSession = { subject: string; csrfToken: string; expiresAt: string };
export type SetupPayer = { customerId: string; name: string; workspaceLimit: number };
export type SetupPayerPage = { customers: SetupPayer[]; nextCursor: string | null };
export type SetupCompany = { status: "Created" | "Existing"; companyId: string; bindingId: string };
export type SetupCommand = { customerId: string; companyName: string; idempotencyKey: string };

export class ProductSetupError extends Error {
  readonly status: number;
  readonly code: string;
  constructor(status: number, code = "setup_request_failed") {
    super(code);
    this.name = "ProductSetupError";
    this.status = status;
    this.code = code;
  }
}

function invalid(): never {
  throw new ProductSetupError(502, "invalid_setup_response");
}
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return invalid();
  return value as Record<string, unknown>;
}

export function createProductSetupClient(fetcher: typeof fetch = fetch) {
  async function request(
    path: string,
    signal?: AbortSignal,
    command?: { csrf: string; body: unknown },
  ) {
    const response = await fetcher(`${prefix}${path}`, {
      method: command ? "POST" : "GET",
      credentials: "same-origin",
      mode: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal: signal
        ? AbortSignal.any([signal, AbortSignal.timeout(30000)])
        : AbortSignal.timeout(30000),
      headers: command
        ? {
            Accept: "application/json",
            "Content-Type": "application/json",
            "X-Kuubu-Setup-CSRF": command.csrf,
          }
        : { Accept: "application/json" },
      body: command ? JSON.stringify(command.body) : undefined,
    });
    if (!response.ok) {
      let code = "setup_request_failed";
      // Only these fixed conflict codes affect UI recovery; never surface raw bodies.
      if (response.status === 409) {
        try {
          const data = object(await response.json());
          if (data.status === "Conflict" || data.status === "CapacityExhausted") code = data.status;
        } catch {
          /* A malformed error body remains a generic conflict. */
        }
      }
      throw new ProductSetupError(response.status, code);
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
  return {
    async session(signal?: AbortSignal): Promise<SetupSession> {
      const data = await json(await request("/session", signal));
      if (
        data.verified !== true ||
        data.operational_access_granted !== false ||
        typeof data.subject !== "string" ||
        !/^[1-9][0-9]{0,18}$/.test(data.subject) ||
        typeof data.csrf_token !== "string" ||
        !/^[A-Za-z0-9_-]{43}$/.test(data.csrf_token) ||
        typeof data.expires_at !== "string" ||
        !Number.isFinite(Date.parse(data.expires_at))
      )
        return invalid();
      return { subject: data.subject, csrfToken: data.csrf_token, expiresAt: data.expires_at };
    },
    async payers(after?: string, signal?: AbortSignal): Promise<SetupPayerPage> {
      if (after !== undefined && !ulid.test(after))
        throw new ProductSetupError(400, "invalid_setup_cursor");
      const data = await json(await request(`/payers${after ? `?after=${after}` : ""}`, signal));
      if (
        !Array.isArray(data.customers) ||
        data.customers.length > 20 ||
        !(
          data.next_cursor === null ||
          (typeof data.next_cursor === "string" && ulid.test(data.next_cursor))
        )
      )
        return invalid();
      let previous = after ?? "";
      const customers = data.customers.map((value): SetupPayer => {
        const row = object(value);
        if (
          typeof row.customer_id !== "string" ||
          !ulid.test(row.customer_id) ||
          row.customer_id <= previous ||
          typeof row.name !== "string" ||
          row.name.trim().length === 0 ||
          row.name.length > 255 ||
          /\p{Cc}/u.test(row.name) ||
          typeof row.workspace_limit !== "number" ||
          !Number.isInteger(row.workspace_limit) ||
          row.workspace_limit < 1 ||
          row.workspace_limit > 10000
        )
          return invalid();
        previous = row.customer_id;
        return { customerId: row.customer_id, name: row.name, workspaceLimit: row.workspace_limit };
      });
      if (data.next_cursor !== null && (customers.length !== 20 || data.next_cursor !== previous))
        return invalid();
      return { customers, nextCursor: data.next_cursor as string | null };
    },
    async create(
      session: SetupSession,
      command: SetupCommand,
      signal?: AbortSignal,
    ): Promise<SetupCompany> {
      // The UI retains the same key and reviewed values across an ambiguous retry.
      const response = await request("/companies", signal, {
        csrf: session.csrfToken,
        body: {
          customer_id: command.customerId,
          company_name: command.companyName,
          idempotency_key: command.idempotencyKey,
          confirm: true,
        },
      });
      const data = await json(response);
      if (
        !(
          (response.status === 201 && data.status === "Created") ||
          (response.status === 200 && data.status === "Existing")
        ) ||
        data.operational_access_granted !== false ||
        typeof data.company_id !== "string" ||
        !uuid.test(data.company_id) ||
        typeof data.binding_id !== "string" ||
        !uuid.test(data.binding_id)
      )
        return invalid();
      return {
        status: data.status as SetupCompany["status"],
        companyId: data.company_id,
        bindingId: data.binding_id,
      };
    },
    async cancel(session: SetupSession, signal?: AbortSignal): Promise<void> {
      const response = await request("/cancel", signal, { csrf: session.csrfToken, body: {} });
      if (response.status !== 204) return invalid();
    },
  };
}
