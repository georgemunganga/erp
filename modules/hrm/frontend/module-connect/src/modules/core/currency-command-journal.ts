import type { CurrencyOperation } from "./currency-precisions";

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const text = (value: unknown): string => typeof value === "string" ? value : "";
const record = (value: unknown): Record<string, unknown> => {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("A saved currency command needs review before another change.");
  return value as Record<string, unknown>;
};

/** One durable command per actor and company. A new write never silently replaces it. */
export interface PendingCurrencyCommand {
  schemaVersion: 1;
  actorId: string;
  companyId: string;
  operation: CurrencyOperation;
  target: string;
  key: string;
  createdAtUtc: string;
}
const journalKey = (actorId: string, companyId: string) => `erp.core.currency.pending.v1:${actorId}:${companyId}`;
const parsePending = (raw: string | null, actorId: string, companyId: string): PendingCurrencyCommand | null => {
  if (!raw) return null;
  const value = record(JSON.parse(raw));
  if (value.schemaVersion !== 1 || value.actorId !== actorId || value.companyId !== companyId ||
    (value.operation !== "Publish" && value.operation !== "Retire") || (!/^[A-Z]{3}$/.test(text(value.target)) && value.operation === "Publish") ||
    (!guid.test(text(value.target)) && value.operation === "Retire") ||
    !guid.test(text(value.key)) || !text(value.createdAtUtc))
    throw new Error("A saved currency command needs review before another change.");
  return value as unknown as PendingCurrencyCommand;
};
export function createCurrencyCommandJournal(storage: Storage) {
return {
  read(actorId: string, companyId: string): PendingCurrencyCommand | null {
    return parsePending(storage.getItem(journalKey(actorId, companyId)), actorId, companyId);
  },
  begin(actorId: string, companyId: string, operation: CurrencyOperation, target: string): PendingCurrencyCommand {
    if (!actorId.trim() || !guid.test(companyId) ||
      (operation === "Publish" ? !/^[A-Z]{3}$/.test(target) : !guid.test(target)) || this.read(actorId, companyId))
      throw new Error("Check the saved currency command before starting another.");
    const entry: PendingCurrencyCommand = { schemaVersion: 1, actorId, companyId, operation, target,
      key: crypto.randomUUID(), createdAtUtc: new Date().toISOString() };
    storage.setItem(journalKey(actorId, companyId), JSON.stringify(entry));
    if (this.read(actorId, companyId)?.key !== entry.key) throw new Error("The command key could not be saved. No change was sent.");
    return entry;
  },
  clear(entry: PendingCurrencyCommand): void {
    if (this.read(entry.actorId, entry.companyId)?.key !== entry.key) throw new Error("The saved command changed. Refresh before editing.");
    storage.removeItem(journalKey(entry.actorId, entry.companyId));
  },
};
}

export const currencyCommandJournal = {
  read: (actorId: string, companyId: string) => createCurrencyCommandJournal(localStorage).read(actorId, companyId),
  begin: (actorId: string, companyId: string, operation: CurrencyOperation, target: string) =>
    createCurrencyCommandJournal(localStorage).begin(actorId, companyId, operation, target),
  clear: (entry: PendingCurrencyCommand) => createCurrencyCommandJournal(localStorage).clear(entry),
};
