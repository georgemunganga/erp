import { expect, test } from "@playwright/test";
import { canManageCore, canManageOrganisation, optionalOrganisationHeadcounts } from "../../src/platform/entry-route";

test("Core organization actions admit ERP-only admin and retain existing HR editors", () => {
  for (const role of ["erp_admin", "hr_admin", "hr_ops"]) expect(canManageOrganisation([role])).toBe(true);
  expect(canManageCore(["hr_ops"])).toBe(false); // Existing HR editor does not imply Core-shell administration.
  for (const roles of [[], ["employee"], ["manager"], ["payroll"], ["procurement_manager"]]) {
    expect(canManageOrganisation(roles)).toBe(false);
  }
});

test("ERP-only organization loading does not query workforce and never supplies fabricated zero counts", async () => {
  let calls = 0;
  const load = async () => { calls++; return { items: [{ id: "synthetic-worker" }] }; };
  expect(await optionalOrganisationHeadcounts(["erp_admin"], load)).toBeNull();
  expect(calls).toBe(0);
  expect(await optionalOrganisationHeadcounts(["erp_admin", "hr_admin"], load)).toEqual({ items: [{ id: "synthetic-worker" }] });
  expect(calls).toBe(1);
});

test("authorized workforce failures remain visible rather than fabricated empty results", async () => {
  await expect(optionalOrganisationHeadcounts(["hr_ops"], async () => { throw new Error("Store unavailable"); })).rejects.toThrow("Store unavailable");
});
