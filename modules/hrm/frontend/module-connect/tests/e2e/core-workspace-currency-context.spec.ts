import { expect, test } from "@playwright/test";
import { parseCoreWorkspaceChoices, selectedCoreCompanyId } from "../../src/modules/core/core-workspace-context";

const first = "01a17921-a5a8-7ac2-8912-f5c2197c82ec";
const second = "01a17921-a5a8-7ac2-8912-f5c2197c82ed";
const branch = "01a17921-a5a8-7ac2-8912-f5c2197c82ee";
const fixture = {
  schemaVersion: 1,
  tenantId: "core-tenant-private",
  principalId: "6115aa71-334a-4e3a-97c0-24e1d7463c0f",
  organisationWide: false,
  companies: [
    { id: first, code: "ONE", registeredName: "Company One", currency: "ZMW", isDefault: false },
    { id: second, code: "TWO", registeredName: "Company Two", currency: "USD", isDefault: true },
  ],
  branches: [{ id: branch, code: "HQ", name: "Head office", legalEntityId: second, type: "headoffice" }],
  coreActions: ["core-currency-precision-read"],
};

test("Core currency context uses only permitted returned companies", () => {
  const choices = parseCoreWorkspaceChoices(fixture);
  expect(selectedCoreCompanyId(choices, first)).toBe(first);
  expect(selectedCoreCompanyId(choices, "01a17921-a5a8-7ac2-8912-f5c2197c82ef")).toBe(second);
  expect(choices.branches).toEqual([{ id: branch, name: "Head office", legalEntityId: second }]);
  expect(choices.coreActions).toEqual(["core-currency-precision-read"]);
});

test("Core context rejects unknown actions and grants none when an older context omits them", () => {
  expect(() => parseCoreWorkspaceChoices({ ...fixture, coreActions: ["hr-admin-all"] })).toThrow();
  const older: Record<string, unknown> = { ...fixture }; delete older.coreActions;
  expect(parseCoreWorkspaceChoices(older).coreActions).toEqual([]);
});

test("Core currency context rejects a branch outside returned company scope", () => {
  expect(() => parseCoreWorkspaceChoices({ ...fixture,
    branches: [{ ...fixture.branches[0], legalEntityId: "01a17921-a5a8-7ac2-8912-f5c2197c82ef" }],
  })).toThrow();
  expect(selectedCoreCompanyId(parseCoreWorkspaceChoices({ ...fixture, companies: [], branches: [] }), first)).toBe("");
});
