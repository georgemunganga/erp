import { expect, test } from "@playwright/test";
import { canManageCore, canManageOrganisation } from "../../src/platform/entry-route";

test("Core organization actions admit ERP-only admin and retain existing HR editors", () => {
  for (const role of ["erp_admin", "hr_admin", "hr_ops"]) expect(canManageOrganisation([role])).toBe(true);
  expect(canManageCore(["hr_ops"])).toBe(false); // Existing HR editor does not imply Core-shell administration.
  for (const roles of [[], ["employee"], ["manager"], ["payroll"], ["procurement_manager"]]) {
    expect(canManageOrganisation(roles)).toBe(false);
  }
});
