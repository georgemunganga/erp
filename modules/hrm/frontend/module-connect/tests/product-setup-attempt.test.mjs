import { test } from "node:test";
import assert from "node:assert/strict";
import {
  readSetupAttempt,
  saveSetupAttempt,
  clearSetupAttempt,
} from "../src/modules/core/product-setup-attempt.ts";

const attempt = {
  version: 1,
  subject: "17",
  command: {
    customerId: "01arz3ndektsv4rrffq69g5fav",
    companyName: "Reviewed company",
    idempotencyKey: "synthetic_request_0000000000000001",
  },
};
function storage() {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key),
  };
}
test("reviewed request survives reload exactly and saves no credentials", () => {
  const s = storage();
  assert.equal(readSetupAttempt(s).status, "empty");
  saveSetupAttempt(s, attempt);
  const loaded = readSetupAttempt(s);
  assert.deepEqual(loaded, { status: "ready", attempt });
  assert.deepEqual(Object.keys(loaded.attempt).sort(), ["command", "subject", "version"]);
  saveSetupAttempt(s, loaded.attempt);
  assert.deepEqual(readSetupAttempt(s).attempt.command, attempt.command);
  clearSetupAttempt(s);
  assert.equal(readSetupAttempt(s).status, "empty");
});
test("an unresolved request cannot be replaced by another key, payload or identity", () => {
  const s = storage();
  saveSetupAttempt(s, attempt);
  for (const changed of [
    { ...attempt, subject: "18" },
    { ...attempt, command: { ...attempt.command, companyName: "Different company" } },
    {
      ...attempt,
      command: { ...attempt.command, idempotencyKey: "synthetic_request_0000000000000002" },
    },
  ]) {
    assert.throws(() => saveSetupAttempt(s, changed));
    assert.deepEqual(readSetupAttempt(s).attempt, attempt);
  }
});
for (const raw of [
  "invalid JSON",
  "{}",
  "x".repeat(4097),
  JSON.stringify({ ...attempt, csrfToken: "must-not-persist" }),
  JSON.stringify({ ...attempt, subject: "0" }),
  JSON.stringify({ ...attempt, command: { ...attempt.command, idempotencyKey: "short" } }),
  JSON.stringify({ ...attempt, command: { ...attempt.command, companyName: "\u0080" } }),
]) {
  test(`invalid retained state blocks a new request: ${raw.slice(0, 50)}`, () => {
    const s = {
      getItem: () => raw,
      setItem: () => assert.fail("must not replace invalid state"),
      removeItem() {},
    };
    assert.equal(readSetupAttempt(s).status, "invalid");
    assert.throws(() => saveSetupAttempt(s, attempt));
  });
}
test("blocked storage and a silently ignored write cannot be treated as retained", () => {
  const blocked = {
    getItem() {
      throw new Error("blocked");
    },
    setItem() {},
    removeItem() {},
  };
  assert.equal(readSetupAttempt(blocked).status, "unavailable");
  assert.throws(() => saveSetupAttempt(blocked, attempt));
  assert.throws(() =>
    saveSetupAttempt({ getItem: () => null, setItem() {}, removeItem() {} }, attempt),
  );
});
