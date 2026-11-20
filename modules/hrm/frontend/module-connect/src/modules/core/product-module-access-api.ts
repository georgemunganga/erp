import { OperationalError, type OperationalCompany } from "./product-operational-api.ts";

const prefix = "/api/core/product-identity/operational/module-access";
const uuid =
  /^(?!00000000-0000-0000-0000-000000000000$)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const key = /^[a-z][a-z0-9_-]{0,127}$/;
export type AccessModule = "hrm" | "procurement";
export type AccessMember = {
  id: string;
  name: string;
  role: string;
  active: boolean;
  identityAvailable: boolean;
  revision: number;
};
export type AccessLocation = { id: string; name: string | null; available: boolean };
export type AccessGrant = {
  id: string;
  revision: number;
  membershipRevision: number;
  state: "granted" | "revoked";
  roles: string[];
  actions: string[];
  scope: "company" | "locations" | "none";
  locations: AccessLocation[];
  stale: boolean;
};
export type AccessReview = {
  member: AccessMember;
  module: AccessModule;
  roles: string[];
  actions: string[];
  grant: AccessGrant | null;
};
export type AccessChange = {
  company: OperationalCompany;
  member: AccessMember;
  module: AccessModule;
  expectedRevision: number;
  commandKey: string;
  revoke: boolean;
  roles: string[];
  actions: string[];
  scope: "company" | "locations" | "none";
  locations: string[];
  allCompanyConfirmed: boolean;
};
function fail(): never {
  throw new OperationalError(502);
}
function object(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : fail();
}
function fields(data: Record<string, unknown>, names: string[]) {
  if (
    Object.keys(data).length !== names.length ||
    !names.every((name) => Object.hasOwn(data, name))
  )
    fail();
}
function id(value: unknown): string {
  return typeof value === "string" && uuid.test(value) ? value : fail();
}
function integer(value: unknown, minimum = 1): number {
  return typeof value === "number" &&
    Number.isInteger(value) &&
    value >= minimum &&
    value <= 2147483647
    ? value
    : fail();
}
function text(value: unknown): string {
  return typeof value === "string" && value.trim() && value.length <= 512 ? value : fail();
}
function bool(value: unknown): boolean {
  return typeof value === "boolean" ? value : fail();
}
function keys(value: unknown, max: number): string[] {
  if (
    !Array.isArray(value) ||
    value.length > max ||
    value.some((x) => typeof x !== "string" || !key.test(x)) ||
    new Set(value).size !== value.length
  )
    fail();
  return value as string[];
}
function member(value: unknown): AccessMember {
  const x = object(value);
  fields(x, [
    "membership_id",
    "display_name",
    "role",
    "membership_active",
    "identity_available",
    "revision",
  ]);
  if (!["owner", "administrator", "member"].includes(String(x.role))) fail();
  return {
    id: id(x.membership_id),
    name: text(x.display_name),
    role: String(x.role),
    active: bool(x.membership_active),
    identityAvailable: bool(x.identity_available),
    revision: integer(x.revision),
  };
}
function location(value: unknown): AccessLocation {
  const x = object(value);
  fields(x, ["location_id", "name", "available"]);
  const available = bool(x.available);
  if (!available && x.name !== null) fail();
  return { id: id(x.location_id), name: available ? text(x.name) : null, available };
}
function list<T extends { id: string }>(
  value: unknown,
  maximum: number,
  parse: (row: unknown) => T,
): T[] {
  if (!Array.isArray(value) || value.length > maximum) fail();
  const rows = value.map(parse);
  if (new Set(rows.map((x) => x.id)).size !== rows.length) fail();
  return rows;
}
function query(company: OperationalCompany, after?: string | null) {
  if (!uuid.test(company.id) || !uuid.test(company.linkId) || (after != null && !uuid.test(after)))
    throw new OperationalError(400);
  return new URLSearchParams({
    company_id: company.id,
    link_id: company.linkId,
    ...(after ? { after } : {}),
  });
}
function noAdmission(data: Record<string, unknown>) {
  if (data.operational_access_granted !== false) fail();
}
function cursor(value: unknown): string | null {
  return value === null ? null : id(value);
}
export function createModuleAccessClient(fetcher: typeof fetch = fetch) {
  async function request(path: string, signal?: AbortSignal, payload?: string, csrf?: string) {
    let response: Response;
    try {
      response = await fetcher(prefix + path, {
        method: payload ? "POST" : "GET",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        headers: {
          Accept: "application/json",
          ...(payload
            ? { "Content-Type": "application/json", "X-Kuubu-Operational-CSRF": csrf! }
            : {}),
        },
        ...(payload ? { body: payload } : {}),
        signal: signal
          ? AbortSignal.any([signal, AbortSignal.timeout(20000)])
          : AbortSignal.timeout(20000),
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      throw new OperationalError(503);
    }
    if (!response.ok) throw new OperationalError(response.status);
    try {
      return object(await response.json());
    } catch {
      return fail();
    }
  }
  return {
    members: async (company: OperationalCompany, after: string | null, signal?: AbortSignal) => {
      const data = await request("/members?" + query(company, after), signal);
      fields(data, ["members", "next_cursor", "operational_access_granted"]);
      noAdmission(data);
      return { rows: list(data.members, 20, member), next: cursor(data.next_cursor) };
    },
    locations: async (company: OperationalCompany, after: string | null, signal?: AbortSignal) => {
      const data = await request("/locations?" + query(company, after), signal);
      fields(data, ["locations", "next_cursor", "operational_access_granted"]);
      noAdmission(data);
      const rows = list(data.locations, 20, location);
      if (rows.some((x) => !x.available)) fail();
      return { rows, next: cursor(data.next_cursor) };
    },
    review: async (
      company: OperationalCompany,
      selected: string,
      module: AccessModule,
      signal?: AbortSignal,
    ): Promise<AccessReview> => {
      if (!uuid.test(selected) || !["hrm", "procurement"].includes(module))
        throw new OperationalError(400);
      const params = query(company);
      params.set("membership_id", selected);
      params.set("module", module);
      const data = await request("/review?" + params, signal);
      fields(data, ["member", "module", "roles", "actions", "grant", "operational_access_granted"]);
      noAdmission(data);
      const row = member(data.member);
      if (row.id !== selected || data.module !== module) fail();
      let grant: AccessGrant | null = null;
      if (data.grant !== null) {
        const x = object(data.grant);
        fields(x, [
          "version_id",
          "revision",
          "membership_revision",
          "state",
          "roles",
          "actions",
          "scope",
          "locations",
          "requires_reconfirmation",
        ]);
        if (
          !["granted", "revoked"].includes(String(x.state)) ||
          !["company", "locations", "none"].includes(String(x.scope))
        )
          fail();
        grant = {
          id: id(x.version_id),
          revision: integer(x.revision),
          membershipRevision: integer(x.membership_revision),
          state: x.state as AccessGrant["state"],
          roles: keys(x.roles, 16),
          actions: keys(x.actions, 256),
          scope: x.scope as AccessGrant["scope"],
          locations: list(x.locations, 128, location),
          stale: bool(x.requires_reconfirmation),
        };
        if (
          (grant.state === "revoked" &&
            (grant.scope !== "none" ||
              grant.roles.length ||
              grant.actions.length ||
              grant.locations.length)) ||
          (grant.state === "granted" &&
            (grant.scope === "none" ||
              (grant.scope === "company" && grant.locations.length) ||
              (grant.scope === "locations" && !grant.locations.length)))
        )
          fail();
      }
      const roles = keys(data.roles, 128);
      const actions = keys(data.actions, 1024);
      if (
        roles.includes("erp_admin") ||
        !actions.includes(module === "hrm" ? "hrm-access" : "procurement-access")
      )
        fail();
      if (
        grant?.state === "granted" &&
        !grant.stale &&
        (grant.membershipRevision !== row.revision ||
          grant.roles.some((value) => !roles.includes(value)) ||
          grant.actions.some((value) => !actions.includes(value)))
      )
        fail();
      return {
        member: row,
        module,
        roles,
        actions,
        grant,
      };
    },
    save: async (change: AccessChange, csrf: string, signal?: AbortSignal) => {
      query(change.company);
      if (
        !/^[A-Za-z0-9_-]{43}$/.test(csrf) ||
        !uuid.test(change.member.id) ||
        !uuid.test(change.commandKey) ||
        !["hrm", "procurement"].includes(change.module)
      )
        throw new OperationalError(400);
      integer(change.member.revision);
      integer(change.expectedRevision, 0);
      keys(change.roles, 16);
      keys(change.actions, 256);
      if (
        change.locations.length > 128 ||
        new Set(change.locations).size !== change.locations.length ||
        change.locations.some((x) => !uuid.test(x)) ||
        (change.revoke &&
          (change.scope !== "none" ||
            change.roles.length ||
            change.actions.length ||
            change.locations.length ||
            change.allCompanyConfirmed ||
            change.expectedRevision < 1)) ||
        (!change.revoke &&
          (!change.actions.includes(
            change.module === "hrm" ? "hrm-access" : "procurement-access",
          ) ||
            (change.module === "hrm" &&
              (!change.roles.length ||
                (change.actions.includes("hrm-admin") && !change.roles.includes("hr_admin")))) ||
            !(
              (change.scope === "company" &&
                change.allCompanyConfirmed &&
                !change.locations.length) ||
              (change.scope === "locations" &&
                !change.allCompanyConfirmed &&
                change.locations.length > 0)
            )))
      )
        throw new OperationalError(400);
      const data = await request(
        "/commands",
        signal,
        JSON.stringify({
          company_id: change.company.id,
          link_id: change.company.linkId,
          membership_id: change.member.id,
          membership_revision: change.member.revision,
          module: change.module,
          expected_revision: change.expectedRevision,
          command_key: change.commandKey,
          operation: change.revoke ? "revoke" : "grant",
          roles: change.roles,
          actions: change.actions,
          scope: change.scope,
          location_ids: change.locations,
          all_company_locations_confirmed: change.allCompanyConfirmed,
          confirmed: true,
        }),
        csrf,
      );
      fields(data, ["status", "version_id", "revision", "state", "operational_access_granted"]);
      noAdmission(data);
      id(data.version_id);
      const revision = integer(data.revision);
      if (
        !["Changed", "Unchanged", "Existing"].includes(String(data.status)) ||
        data.state !== (change.revoke ? "revoked" : "granted") ||
        revision < change.expectedRevision ||
        revision > change.expectedRevision + 1
      )
        fail();
      return { revision, state: data.state as "granted" | "revoked" };
    },
  };
}
