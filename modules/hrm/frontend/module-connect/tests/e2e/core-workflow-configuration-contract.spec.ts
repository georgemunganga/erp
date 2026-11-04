import { expect, test } from "@playwright/test";
import { parseWorkflowConfiguration } from "../../src/modules/core/workflow-configuration-contract";

const row = () => ({
  id: "procurement.purchase-request.submitted",
  moduleId: "procurement",
  moduleLabel: "Procurement",
  recordType: "purchase-request",
  recordLabel: "Purchase request",
  trigger: "submitted",
  triggerLabel: "When submitted",
  description: "Review the need.",
  availabilityNote: "Current approval rules still apply.",
  settings: [{ label: "Buying rules", path: "/procurement/workspace/settings/buying-rules" }],
});

test("registered workflows are navigation metadata, with separate record/trigger identity", () => {
  expect(parseWorkflowConfiguration({ schemaVersion: 1, items: [row()] })).toEqual([row()]);
  expect(parseWorkflowConfiguration({ schemaVersion: 1, items: [] })).toEqual([]);
});
for (const path of [
  "https://external.example/",
  "//external.example/",
  "/core/workspace/settings/users",
  "/procurement/workspace/settings/../../users",
  "/procurement/workspace/settings/%2f%2fexternal",
]) {
  test(`reject an unsafe workflow settings path: ${path}`, () => {
    expect(() =>
      parseWorkflowConfiguration({
        schemaVersion: 1,
        items: [{ ...row(), settings: [{ label: "Unsafe", path }] }],
      }),
    ).toThrow();
  });
}
test("duplicate, malformed and unversioned catalogs cannot leave a partial valid selection", () => {
  expect(() => parseWorkflowConfiguration({ schemaVersion: 1, items: [row(), row()] })).toThrow();
  expect(() =>
    parseWorkflowConfiguration({ schemaVersion: 1, items: [row(), { ...row(), id: "bad" }] }),
  ).toThrow();
  expect(() => parseWorkflowConfiguration({ items: [row()] })).toThrow();
  expect(() =>
    parseWorkflowConfiguration({
      schemaVersion: 1,
      items: [{ ...row(), moduleLabel: "Procurement\u0085" }],
    }),
  ).toThrow();
  expect(() =>
    parseWorkflowConfiguration({ schemaVersion: 1, items: [{ ...row(), settings: [] }] }),
  ).toThrow();
});
