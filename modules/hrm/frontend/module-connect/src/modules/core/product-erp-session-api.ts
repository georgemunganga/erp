import { OperationalError, type OperationalCompany } from "./product-operational-api.ts";

const prefix = "/api/core/product-identity/operational/erp-session";
const uuid =
  /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const opaque = /^[A-Za-z0-9_-]{43}$/;
export type ErpModule = "hrm" | "procurement";
export type ErpSessionReview = Readonly<{
  companyId: string;
  linkId: string;
  module: ErpModule;
  versionId: string;
  revision: number;
  scope: "company" | "locations";
  locations: readonly string[];
  roles: readonly string[];
  actions: readonly string[];
}>;
export type SelectedErpSession = Readonly<{
  companyId: string;
  module: ErpModule;
  expiresAt: string;
}>;
export type ErpSelectionAttempt = Readonly<{
  review: ErpSessionReview;
  body: string;
}>;
function fail(): never {
  throw new OperationalError(502);
}
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return fail();
  return value as Record<string, unknown>;
}
function fields(data: Record<string, unknown>, names: string[]) {
  if (Object.keys(data).length !== names.length || names.some((name) => !Object.hasOwn(data, name)))
    fail();
}
function strings(value: unknown, pattern: RegExp, minimum = 0): readonly string[] {
  if (
    !Array.isArray(value) ||
    value.length < minimum ||
    value.length > 128 ||
    value.some((id) => typeof id !== "string" || !pattern.test(id)) ||
    new Set(value).size !== value.length
  )
    return fail();
  return Object.freeze([...value]);
}
function selected(data: Record<string, unknown>, expected?: ErpSessionReview): SelectedErpSession {
  if (
    data.session_established !== true ||
    data.operational_access_granted !== false ||
    data.next_step !== "erp_access_pending" ||
    typeof data.company_id !== "string" ||
    !uuid.test(data.company_id) ||
    (data.module !== "hrm" && data.module !== "procurement") ||
    typeof data.expires_at !== "string" ||
    !Number.isFinite(Date.parse(data.expires_at)) ||
    Date.parse(data.expires_at) <= Date.now() ||
    (expected && (expected.companyId !== data.company_id || expected.module !== data.module))
  )
    return fail();
  return Object.freeze({
    companyId: data.company_id,
    module: data.module,
    expiresAt: data.expires_at,
  });
}
/** Keep this exact serialized command in memory after an uncertain response. Never auto-retry. */
export function createErpSelectionAttempt(
  review: ErpSessionReview,
  companyWideConfirmed: boolean,
): ErpSelectionAttempt {
  if ((review.scope === "company") !== companyWideConfirmed) throw new OperationalError(400);
  const snapshot = Object.freeze({
    ...review,
    locations: Object.freeze([...review.locations]),
    roles: Object.freeze([...review.roles]),
    actions: Object.freeze([...review.actions]),
  });
  return Object.freeze({
    review: snapshot,
    body: JSON.stringify({
      company_id: snapshot.companyId,
      link_id: snapshot.linkId,
      module: snapshot.module,
      grant_version_id: snapshot.versionId,
      grant_revision: snapshot.revision,
      scope: snapshot.scope,
      location_ids: snapshot.locations,
      all_company_locations_confirmed: companyWideConfirmed,
      confirmed: true,
      command_key: crypto.randomUUID(),
    }),
  });
}
export function createErpSessionClient(fetcher: typeof fetch = fetch) {
  async function request(path: string, signal?: AbortSignal, csrf?: string, body?: string) {
    if (csrf !== undefined && !opaque.test(csrf)) throw new OperationalError(403);
    let response: Response;
    try {
      response = await fetcher(prefix + path, {
        method: csrf === undefined ? "GET" : "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        headers: {
          Accept: "application/json",
          ...(csrf === undefined
            ? {}
            : {
                "Content-Type": "application/json",
                "X-Kuubu-Operational-CSRF": csrf,
              }),
        },
        ...(csrf === undefined ? {} : { body: body ?? "{}" }),
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
      return fail();
    }
  }
  return {
    review: async (
      company: OperationalCompany,
      module: ErpModule,
      signal?: AbortSignal,
    ): Promise<ErpSessionReview> => {
      if (
        !uuid.test(company.id) ||
        !uuid.test(company.linkId) ||
        !["hrm", "procurement"].includes(module)
      )
        throw new OperationalError(400);
      const data = object(
        await request(
          `/review?company_id=${company.id}&link_id=${company.linkId}&module=${module}`,
          signal,
        ),
      );
      fields(data, [
        "company_id",
        "module",
        "grant_version_id",
        "grant_revision",
        "scope",
        "location_ids",
        "roles",
        "actions",
        "session_established",
        "operational_access_granted",
      ]);
      if (
        data.company_id !== company.id ||
        data.module !== module ||
        data.session_established !== false ||
        data.operational_access_granted !== false ||
        typeof data.grant_version_id !== "string" ||
        !uuid.test(data.grant_version_id) ||
        !Number.isSafeInteger(data.grant_revision) ||
        Number(data.grant_revision) < 1 ||
        (data.scope !== "company" && data.scope !== "locations")
      )
        return fail();
      const locations = strings(data.location_ids, uuid, data.scope === "locations" ? 1 : 0);
      if (data.scope === "company" && locations.length !== 0) return fail();
      return Object.freeze({
        companyId: company.id,
        linkId: company.linkId,
        module,
        versionId: data.grant_version_id,
        revision: Number(data.grant_revision),
        scope: data.scope,
        locations,
        roles: strings(data.roles, /^[a-z][a-z0-9_-]{0,63}$/, 1),
        actions: strings(data.actions, /^[a-z][a-z0-9_-]{0,127}$/, 1),
      });
    },
    select: async (attempt: ErpSelectionAttempt, csrf: string, signal?: AbortSignal) => {
      const data = object(await request("/select", signal, csrf, attempt.body));
      fields(data, [
        "session_established",
        "company_id",
        "module",
        "expires_at",
        "operational_access_granted",
        "next_step",
      ]);
      return selected(data, attempt.review);
    },
    session: async (signal?: AbortSignal) => {
      const data = object(await request("/session", signal));
      fields(data, [
        "session_established",
        "company_id",
        "module",
        "scope",
        "location_ids",
        "expires_at",
        "operational_access_granted",
        "next_step",
      ]);
      const value = selected(data);
      if (data.scope !== "company" && data.scope !== "locations") return fail();
      const locations = strings(data.location_ids, uuid, data.scope === "locations" ? 1 : 0);
      if (data.scope === "company" && locations.length) return fail();
      return Object.freeze({ ...value, scope: data.scope, locations });
    },
    logout: async (csrf: string, signal?: AbortSignal) => {
      if ((await request("/logout", signal, csrf)) !== null) fail();
    },
  };
}
