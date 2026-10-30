import { expect, test } from "@playwright/test";
import { createCurrencyCommandJournal } from "../../src/modules/core/currency-command-journal";

const actor = "6115aa71-334a-4e3a-97c0-24e1d7463c0f";
const otherActor = "7a6795a5-85da-47ef-8ae5-cfc024806ecc";
const company = "01a17921-a5a8-7ac2-8912-f5c2197c82ec";
const otherCompany = "01a17921-a5a8-7ac2-8912-f5c2197c82ed";

function memoryStorage(): Storage {
  const values = new Map<string, string>();
  return {
    get length() { return values.size; },
    clear: () => values.clear(),
    getItem: (key) => values.get(key) ?? null,
    key: (index) => [...values.keys()][index] ?? null,
    removeItem: (key) => { values.delete(key); },
    setItem: (key, value) => { values.set(key, value); },
  };
}

test("currency command key survives reloading and blocks duplicate writes", () => {
  const storage = memoryStorage();
  const firstPage = createCurrencyCommandJournal(storage);
  const pending = firstPage.begin(actor, company, "Publish", "ZMW");
  expect(pending.key).toMatch(/^[0-9a-f-]{36}$/i);
  expect(() => firstPage.begin(actor, company, "Retire", "01a17921-a5a8-7ac2-8912-f5c2197c82ee")).toThrow();
  const afterReload = createCurrencyCommandJournal(storage);
  expect(afterReload.read(actor, company)).toEqual(pending);
  expect(afterReload.read(otherActor, company)).toBeNull();
  expect(afterReload.read(actor, otherCompany)).toBeNull();
  expect(() => afterReload.clear({ ...pending, key: crypto.randomUUID() })).toThrow();
  expect(afterReload.read(actor, company)).toEqual(pending);
  afterReload.clear(pending);
  expect(afterReload.read(actor, company)).toBeNull();
});

test("corrupt pending command fails closed without replacing its key", () => {
  const storage = memoryStorage();
  const journal = createCurrencyCommandJournal(storage);
  const pending = journal.begin(actor, company, "Publish", "ZMW");
  const slot = storage.key(0)!;
  storage.setItem(slot, JSON.stringify({ ...pending, actorId: otherActor }));
  expect(() => journal.read(actor, company)).toThrow();
  expect(() => journal.begin(actor, company, "Publish", "USD")).toThrow();
  expect(storage.getItem(slot)).toContain(otherActor);
});
