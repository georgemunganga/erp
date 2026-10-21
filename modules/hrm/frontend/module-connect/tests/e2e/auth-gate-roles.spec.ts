import { expect, test } from "@playwright/test";
import { admitsPageRoles } from "../../src/platform/auth-gate-access";

// Pure presentation guard contract. Backend role/data-scope tests remain authoritative.
test("page roles narrow an admitted module without changing default module access", () => {
  expect(admitsPageRoles(true, ["employee"])).toBe(true);
  expect(admitsPageRoles(false, ["erp_admin"])).toBe(false);
  expect(admitsPageRoles(false, ["erp_admin"], ["erp_admin"])).toBe(false);
  expect(admitsPageRoles(false, ["hr_admin"], ["hr_admin"])).toBe(false);
});
test("workforce chart/reporting role list does not admit other module or payroll roles", () => {
  const allowed = ["hr_ops", "hr_admin"];
  for (const roles of [["employee"], ["manager"], ["payroll"], ["erp_admin"], ["erp_admin", "payroll"], ["procurement_manager"], []])
    expect(admitsPageRoles(true, roles, allowed)).toBe(false);
  expect(admitsPageRoles(true, ["HR_OPS"], allowed)).toBe(true);
  expect(admitsPageRoles(true, ["erp_admin", "hr_admin"], allowed)).toBe(true);
});
test("empty explicit page role list denies rather than silently removing restrictions", () => {
  expect(admitsPageRoles(true, ["hr_admin"], [])).toBe(false);
  expect(admitsPageRoles(true, ["hr_admin"], ["hr_ops"])).toBe(false);
});
