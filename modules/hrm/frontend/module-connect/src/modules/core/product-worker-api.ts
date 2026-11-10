import { parseCoreWorkspaceChoices } from "./core-workspace-context";
import { OperationalError } from "./product-operational-api";

const contextHeader = "X-Kuubu-Session-Context";
const workerPath = "/api/hrm/workers/";
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

// Match ERP:AuthMode=product at deployment. The API still verifies every request.
export const PRODUCT_ERP = import.meta.env.VITE_ERP_PRODUCT_MODE === "true";

export class ProductWorkerError extends Error {
  constructor(readonly status: number) {
    super("The employee workspace could not be loaded.");
  }
}

export function isWorkspaceLoss(error: unknown) {
  return (
    (error instanceof ProductWorkerError || error instanceof OperationalError) &&
    [401, 403, 409].includes(error.status)
  );
}

async function request(path: string, signal: AbortSignal, context?: string, csrf?: string) {
  const response = await fetch(path, {
    method: csrf ? "POST" : "GET",
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    headers: {
      Accept: "application/json",
      ...(context ? { [contextHeader]: context } : {}),
      ...(csrf ? { "Content-Type": "application/json", "X-Kuubu-Operational-CSRF": csrf } : {}),
    },
    body: csrf ? "{}" : undefined,
    signal: AbortSignal.any([signal, AbortSignal.timeout(20000)]),
  });
  if (!response.ok) throw new ProductWorkerError(response.status);
  return response;
}

export async function readProductWorkerContext(signal: AbortSignal, expected?: string) {
  const response = await request("/api/core/v1/workspace-context", signal, expected);
  const data = await response.json();
  const { companies, branches, coreActions } = parseCoreWorkspaceChoices(data);
  const company = companies[0];
  if (
    data.contextKind !== "product" ||
    data.selectedModule !== "hrm" ||
    companies.length !== 1 ||
    data.selectedCompanyId !== company.id ||
    typeof data.organisationWide !== "boolean" ||
    (!data.organisationWide && !branches.length) ||
    new Set(branches.map((branch) => branch.id)).size !== branches.length ||
    coreActions.length ||
    typeof data.sessionContext !== "string" ||
    !/^[0-9a-f]{64}$/.test(data.sessionContext) ||
    (expected && data.sessionContext !== expected) ||
    data.workerDirectory !== true ||
    typeof data.workerArchive !== "boolean"
  )
    throw new ProductWorkerError(403);
  return {
    company,
    branches,
    allLocations: data.organisationWide as boolean,
    marker: data.sessionContext as string,
    canArchive: data.workerArchive as boolean,
  };
}

export type ProductWorkerContext = Awaited<ReturnType<typeof readProductWorkerContext>>;
export type DirectoryWorker = {
  id: string;
  name: string;
  number: string;
  title: string;
  department: string;
  branch: string;
  status: string;
  archived: boolean;
};

export const PRODUCT_WORKER_PAGE_SIZE = 25;

export async function readProductWorkers(
  context: ProductWorkerContext,
  filter: { search: string; locationId: string; page: number; includeArchived: boolean },
  signal: AbortSignal,
) {
  const query = new URLSearchParams({
    page: String(filter.page),
    pageSize: String(PRODUCT_WORKER_PAGE_SIZE),
  });
  if (filter.search) query.set("search", filter.search);
  if (filter.locationId) query.set("locationId", filter.locationId);
  if (filter.includeArchived) query.set("includeArchived", "true");
  const response = await request(workerPath + "?" + query, signal, context.marker);
  const data = await response.json();
  if (
    !Array.isArray(data?.items) ||
    data.items.length > PRODUCT_WORKER_PAGE_SIZE ||
    !Number.isSafeInteger(data.totalCount) ||
    data.totalCount < data.items.length
  )
    throw new ProductWorkerError(502);
  const allowedLocations = new Set(context.branches.map((branch) => branch.id));
  const rows: DirectoryWorker[] = data.items.map((row: Record<string, unknown>) => {
    if (
      !row ||
      typeof row.id !== "string" ||
      !guid.test(row.id) ||
      typeof row.fullName !== "string" ||
      typeof row.employeeNo !== "string" ||
      typeof row.status !== "string"
    )
      throw new ProductWorkerError(502);
    if (
      (!context.allLocations && !allowedLocations.has(String(row.locationId))) ||
      (filter.locationId && row.locationId !== filter.locationId)
    )
      throw new ProductWorkerError(409);
    return {
      id: row.id,
      name: row.fullName,
      number: row.employeeNo,
      title: typeof row.jobTitle === "string" ? row.jobTitle : "",
      department: typeof row.orgUnitName === "string" ? row.orgUnitName : "",
      branch: typeof row.locationName === "string" ? row.locationName : "",
      status: row.status,
      archived: row.status === "archived",
    };
  });
  if (new Set(rows.map((row) => row.id)).size !== rows.length) throw new ProductWorkerError(502);
  return { rows, total: data.totalCount as number };
}

export async function archiveProductWorker(
  context: ProductWorkerContext,
  id: string,
  csrf: string,
  signal: AbortSignal,
) {
  if (!context.canArchive || !guid.test(id) || !/^[A-Za-z0-9_-]{43}$/.test(csrf))
    throw new ProductWorkerError(403);
  await request(workerPath + id + "/archive", signal, context.marker, csrf);
}
