import { expect, test } from "@playwright/test";
import { canManageCore, canManageOrganisation, optionalOrganisationHeadcounts, hasCompleteOrganisationHeadcounts } from "../../src/platform/entry-route";

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

test("partial or unproven workforce pages never advertise organization totals", () => {
  expect(hasCompleteOrganisationHeadcounts({ items: Array.from({ length: 500 }), totalCount: 501 })).toBe(false);
  expect(hasCompleteOrganisationHeadcounts({ items: [], totalCount: 501 })).toBe(false);
  expect(hasCompleteOrganisationHeadcounts({ items: [], totalCount: Number.NaN })).toBe(false);
  expect(hasCompleteOrganisationHeadcounts(null)).toBe(false);
  expect(hasCompleteOrganisationHeadcounts({ items: [], totalCount: 0 })).toBe(true);
  expect(hasCompleteOrganisationHeadcounts({ items: [{ id: "synthetic" }], totalCount: 1 })).toBe(true);
});
