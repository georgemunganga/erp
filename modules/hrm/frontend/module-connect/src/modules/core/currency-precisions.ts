import { hrmApi } from "@/platform/api-client";

export type CurrencyOperation = "Publish" | "Retire";
export type CurrencyRounding = "ToEven" | "AwayFromZero";

export interface CurrencyPrecisionVersion {
  id: string;
  currencyCode: string;
  revision: number;
  minorUnits: number;
  rounding: CurrencyRounding;
  sourceReference: string;
  publishedAtUtc: string;
  retiredAtUtc: string | null;
  retirementReason: string | null;
  digest: string;
  current: boolean;
}

export interface CurrencyPrecisionList {
  schemaVersion: 1;
  companyId: string;
  companyCurrency: string;
  canConfigure: boolean;
  items: CurrencyPrecisionVersion[];
}

export interface CurrencyCommandResult extends CurrencyPrecisionVersion {
  commandKey: string;
  operation: CurrencyOperation;
}

export type PublishCurrencyInput = {
  companyId: string;
  currencyCode: string;
  expectedRevision: number;
  minorUnits: number;
  rounding: CurrencyRounding;
  sourceReference: string;
  reason: string;
};
export type RetireCurrencyInput = {
  companyId: string;
  expectedRevision: number;
  reason: string;
};

const root = "/core/v1/currency-precisions";
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const record = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("The currency response was incomplete. Refresh this page.");
  return value as Record<string, unknown>;
};
const text = (value: unknown): string => typeof value === "string" ? value : "";
const code = (value: unknown): string => {
  const result = text(value);
  if (!/^[A-Z]{3}$/.test(result)) throw new Error("The currency code was invalid. Refresh this page.");
  return result;
};
const version = (value: unknown): CurrencyPrecisionVersion => {
  const row = record(value);
  if (!guid.test(text(row.id)) || typeof row.revision !== "number" || !Number.isInteger(row.revision) || row.revision < 1 ||
    typeof row.minorUnits !== "number" || !Number.isInteger(row.minorUnits) || row.minorUnits < 0 || row.minorUnits > 4 ||
    (row.rounding !== "ToEven" && row.rounding !== "AwayFromZero") ||
    !text(row.sourceReference).trim() || !text(row.publishedAtUtc) || !/^[0-9a-f]{64}$/.test(text(row.digest)) ||
    typeof row.current !== "boolean" || (row.retiredAtUtc !== null && typeof row.retiredAtUtc !== "string") ||
    (row.retirementReason !== null && typeof row.retirementReason !== "string")) {
    throw new Error("The currency reference was incomplete. Refresh this page.");
  }
  return { id: text(row.id), currencyCode: code(row.currencyCode), revision: Number(row.revision),
    minorUnits: Number(row.minorUnits), rounding: row.rounding as CurrencyRounding, sourceReference: text(row.sourceReference),
    publishedAtUtc: text(row.publishedAtUtc), retiredAtUtc: row.retiredAtUtc as string | null,
    retirementReason: row.retirementReason as string | null, digest: text(row.digest), current: row.current };
};
const commandResult = (value: unknown, key: string, operation: CurrencyOperation): CurrencyCommandResult => {
  const row = record(value);
  if (row.commandKey !== key || row.operation !== operation) throw new Error("The saved command did not match. Keep its key and ask an administrator to investigate.");
  return { ...version(row), commandKey: key, operation };
};

export const coreCurrencyApi = {
  async list(companyId: string): Promise<CurrencyPrecisionList> {
    if (!guid.test(companyId)) throw new Error("Choose a company before opening currencies.");
    const data = record(await hrmApi.get<unknown>(root, { companyId }));
    if (data.schemaVersion !== 1 || data.companyId !== companyId || typeof data.canConfigure !== "boolean" || !Array.isArray(data.items))
      throw new Error("The currency list was incomplete. Refresh this page.");
    return { schemaVersion: 1, companyId, companyCurrency: code(data.companyCurrency), canConfigure: data.canConfigure,
      items: data.items.map(version) };
  },
  async detail(companyId: string, id: string): Promise<CurrencyPrecisionVersion> {
    if (!guid.test(companyId) || !guid.test(id)) throw new Error("Choose a valid company and currency revision.");
    return version(await hrmApi.get<unknown>(`${root}/${id}`, { companyId }));
  },
  async publish(input: PublishCurrencyInput, key: string): Promise<CurrencyCommandResult> {
    if (!guid.test(input.companyId) || !guid.test(key)) throw new Error("The command scope or key was invalid.");
    return commandResult(await hrmApi.postKeyed<unknown>(root, input, key), key, "Publish");
  },
  async retire(id: string, input: RetireCurrencyInput, key: string): Promise<CurrencyCommandResult> {
    if (!guid.test(id) || !guid.test(input.companyId) || !guid.test(key)) throw new Error("The command scope or key was invalid.");
    return commandResult(await hrmApi.postKeyed<unknown>(`${root}/${id}/retire`, input, key), key, "Retire");
  },
  async recover(companyId: string, operation: CurrencyOperation, key: string): Promise<CurrencyCommandResult> {
    if (!guid.test(companyId) || !guid.test(key)) throw new Error("The command scope or key was invalid.");
    return commandResult(await hrmApi.get<unknown>(`${root}/commands/${operation}/${key}`, { companyId }), key, operation);
  },
};
