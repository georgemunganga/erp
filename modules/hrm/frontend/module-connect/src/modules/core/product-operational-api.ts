/** Person continuity and company review only; never persists credentials or ERP grants. */
const prefix = "/api/core/product-identity/operational";
const uuid =
  /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const opaque = /^[A-Za-z0-9_-]{43}$/;
export type OperationalCompany = {
  id: string;
  linkId: string;
  name: string;
  role: "owner" | "administrator" | "member";
};
export type OperationalSession = { csrf: string; companiesAvailable: boolean };
export type OperationalReview = { company: OperationalCompany; modules: string[] };
export class OperationalError extends Error {
  readonly status: number;
  constructor(status: number) {
    super("Kuubu access could not be confirmed.");
    this.name = "OperationalError";
    this.status = status;
  }
}
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new OperationalError(502);
  return value as Record<string, unknown>;
}
function fields(data: Record<string, unknown>, names: string[]) {
  if (
    Object.keys(data).length !== names.length ||
    !names.every((name) => Object.hasOwn(data, name))
  )
    throw new OperationalError(502);
}
function company(value: unknown): OperationalCompany {
  const data = object(value);
  fields(data, ["company_id", "link_id", "name", "role"]);
  if (
    typeof data.company_id !== "string" ||
    !uuid.test(data.company_id) ||
    typeof data.link_id !== "string" ||
    !uuid.test(data.link_id) ||
    typeof data.name !== "string" ||
    !data.name.trim() ||
    data.name.length > 512 ||
    !["owner", "administrator", "member"].includes(String(data.role))
  )
    throw new OperationalError(502);
  return {
    id: data.company_id,
    linkId: data.link_id,
    name: data.name,
    role: data.role as OperationalCompany["role"],
  };
}
function authorizationUrl(value: unknown): string {
  if (typeof value !== "string" || value.length > 8192) throw new OperationalError(502);
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new OperationalError(502);
  }
  const keys = [
    "client_id",
    "response_type",
    "redirect_uri",
    "scope",
    "state",
    "code_challenge_method",
    "code_challenge",
  ];
  const params = url.searchParams;
  if (
    url.origin !== "https://www.kuubuerp.com" ||
    url.pathname !== "/account/authorize/" ||
    url.username ||
    url.password ||
    url.hash ||
    [...params.keys()].length !== keys.length ||
    keys.some((key) => params.getAll(key).length !== 1) ||
    !uuid.test(params.get("client_id") || "") ||
    params.get("response_type") !== "code" ||
    params.get("redirect_uri") !==
      "https://app.kuubuerp.com/api/core/product-identity/operational/callback" ||
    params.get("scope") !== "product:identity product:erp-session" ||
    params.get("code_challenge_method") !== "S256" ||
    !/^[A-Za-z0-9_-]{32,4096}$/.test(params.get("state") || "") ||
    !opaque.test(params.get("code_challenge") || "")
  )
    throw new OperationalError(502);
  return url.href;
}
export function createOperationalClient(fetcher: typeof fetch = fetch) {
  async function request(path: string, signal?: AbortSignal, csrf?: string) {
    let response: Response;
    try {
      response = await fetcher(prefix + path, {
        method: csrf ? "POST" : "GET",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        headers: {
          Accept: "application/json",
          ...(csrf ? { "Content-Type": "application/json", "X-Kuubu-Operational-CSRF": csrf } : {}),
        },
        ...(csrf ? { body: "{}" } : {}),
        signal: signal
          ? AbortSignal.any([signal, AbortSignal.timeout(20000)])
          : AbortSignal.timeout(20000),
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw new OperationalError(503);
    }
    if (!response.ok) throw new OperationalError(response.status);
    if (response.status === 204) return null;
    try {
      return object(await response.json());
    } catch {
      throw new OperationalError(502);
    }
  }
  return {
    bootstrap: async (signal?: AbortSignal) => {
      const data = object(await request("/bootstrap", signal));
      fields(data, ["csrf_token", "operational_access_granted"]);
      if (
        typeof data.csrf_token !== "string" ||
        !opaque.test(data.csrf_token) ||
        data.operational_access_granted !== false
      )
        throw new OperationalError(502);
      return data.csrf_token;
    },
    session: async (signal?: AbortSignal): Promise<OperationalSession> => {
      const data = object(await request("/session", signal));
      fields(data, [
        "verified",
        "purpose",
        "product_code",
        "csrf_token",
        "operational_access_granted",
        "next_step",
        "proof_until",
        "company_discovery_available",
      ]);
      if (
        data.verified !== true ||
        data.purpose !== "erp-session-v1" ||
        data.product_code !== "kuubu" ||
        data.operational_access_granted !== false ||
        data.next_step !== "company_selection" ||
        typeof data.csrf_token !== "string" ||
        !opaque.test(data.csrf_token) ||
        typeof data.company_discovery_available !== "boolean" ||
        typeof data.proof_until !== "string" ||
        !Number.isFinite(Date.parse(data.proof_until))
      )
        throw new OperationalError(502);
      return { csrf: data.csrf_token, companiesAvailable: data.company_discovery_available };
    },
    start: async (csrf: string, signal?: AbortSignal) => {
      if (!opaque.test(csrf)) throw new OperationalError(403);
      const data = object(await request("/start", signal, csrf));
      fields(data, ["authorization_url"]);
      return authorizationUrl(data.authorization_url);
    },
    companies: async (after: string | null = null, signal?: AbortSignal) => {
      if (after !== null && !uuid.test(after)) throw new OperationalError(400);
      const data = object(await request("/companies" + (after ? "?after=" + after : ""), signal));
      fields(data, ["companies", "next_cursor", "operational_access_granted"]);
      if (
        data.operational_access_granted !== false ||
        !Array.isArray(data.companies) ||
        data.companies.length > 20 ||
        !(
          data.next_cursor === null ||
          (typeof data.next_cursor === "string" && uuid.test(data.next_cursor))
        )
      )
        throw new OperationalError(502);
      const rows = data.companies.map(company);
      if (new Set(rows.map((row) => row.id + ":" + row.linkId)).size !== rows.length)
        throw new OperationalError(502);
      return { rows, next: data.next_cursor as string | null };
    },
    review: async (
      selected: OperationalCompany,
      signal?: AbortSignal,
    ): Promise<OperationalReview> => {
      if (!uuid.test(selected.id) || !uuid.test(selected.linkId)) throw new OperationalError(400);
      const data = object(
        await request(`/companies/${selected.id}?link_id=${selected.linkId}`, signal),
      );
      fields(data, ["company", "licensed_modules", "operational_access_granted", "next_step"]);
      const row = company(data.company);
      if (
        data.operational_access_granted !== false ||
        data.next_step !== "module_access_assignment" ||
        row.id !== selected.id ||
        row.linkId !== selected.linkId ||
        !Array.isArray(data.licensed_modules) ||
        data.licensed_modules.length > 128 ||
        data.licensed_modules.some(
          (value) => typeof value !== "string" || !/^[a-z][a-z0-9_-]{0,63}$/.test(value),
        ) ||
        new Set(data.licensed_modules).size !== data.licensed_modules.length
      )
        throw new OperationalError(502);
      return { company: row, modules: data.licensed_modules as string[] };
    },
    logout: async (csrf: string, signal?: AbortSignal) => {
      if (!opaque.test(csrf)) throw new OperationalError(403);
      await request("/logout", signal, csrf);
    },
  };
}
