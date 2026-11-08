import type { SetupCommand } from "./product-setup-api";

const storageKey = "kuubu.company-setup.v1";
type AttemptStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;
export type SetupAttempt = { version: 1; subject: string; command: SetupCommand };
export type StoredSetupAttempt =
  | { status: "empty" }
  | { status: "ready"; attempt: SetupAttempt }
  | { status: "unavailable" | "invalid" };

function valid(value: unknown): value is SetupAttempt {
  if (!value || typeof value !== "object") return false;
  const row = value as SetupAttempt;
  const command = row.command;
  return (
    Object.keys(row).sort().join(",") === "command,subject,version" &&
    row.version === 1 &&
    typeof row.subject === "string" &&
    /^[1-9][0-9]{0,18}$/.test(row.subject) &&
    !!command &&
    typeof command === "object" &&
    Object.keys(command).sort().join(",") === "companyName,customerId,idempotencyKey" &&
    typeof command.customerId === "string" &&
    /^[0-7][0-9a-hjkmnp-tv-z]{25}$/.test(command.customerId) &&
    typeof command.companyName === "string" &&
    command.companyName.length > 0 &&
    command.companyName.length <= 200 &&
    command.companyName.trim().length > 0 &&
    !/\p{Cc}/u.test(command.companyName) &&
    typeof command.idempotencyKey === "string" &&
    /^[A-Za-z0-9_-]{32,128}$/.test(command.idempotencyKey)
  );
}

/** Contains only an explicit reviewed command, never a bearer, cookie or CSRF. */
export function readSetupAttempt(storage: AttemptStorage): StoredSetupAttempt {
  let raw: string | null;
  try {
    raw = storage.getItem(storageKey);
  } catch {
    return { status: "unavailable" };
  }
  if (raw === null) return { status: "empty" };
  try {
    if (raw.length > 4096) return { status: "invalid" };
    const attempt: unknown = JSON.parse(raw);
    return valid(attempt) ? { status: "ready", attempt } : { status: "invalid" };
  } catch {
    return { status: "invalid" };
  }
}

export function saveSetupAttempt(storage: AttemptStorage, attempt: SetupAttempt): void {
  if (!valid(attempt)) throw new Error("Invalid reviewed company request.");
  const previous = readSetupAttempt(storage);
  if (
    previous.status !== "empty" &&
    (previous.status !== "ready" || JSON.stringify(previous.attempt) !== JSON.stringify(attempt))
  )
    throw new Error("An earlier company request needs checking.");
  const raw = JSON.stringify(attempt);
  storage.setItem(storageKey, raw);
  if (storage.getItem(storageKey) !== raw)
    throw new Error("The reviewed request could not be retained.");
}

/** Explicit correction after a capacity rejection. Preserve the key so a late
 * original request and the correction still cannot allocate two companies. */
export function reviseRejectedSetupAttempt(
  storage: AttemptStorage,
  original: SetupAttempt,
  revised: SetupAttempt,
): void {
  const current = readSetupAttempt(storage);
  if (
    !valid(original) ||
    !valid(revised) ||
    current.status !== "ready" ||
    JSON.stringify(current.attempt) !== JSON.stringify(original) ||
    original.subject !== revised.subject ||
    original.command.idempotencyKey !== revised.command.idempotencyKey
  )
    throw new Error("The retained request cannot be replaced.");
  const raw = JSON.stringify(revised);
  storage.setItem(storageKey, raw);
  if (storage.getItem(storageKey) !== raw) throw new Error("The correction could not be retained.");
}

/** Only after a server-confirmed successful outcome has been acknowledged. */
export function clearSetupAttempt(storage: AttemptStorage): void {
  storage.removeItem(storageKey);
  if (storage.getItem(storageKey) !== null) throw new Error("The request could not be cleared.");
}
