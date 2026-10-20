import { expect, test } from "@playwright/test";
import { organisationEditorPayload, type OrganisationEditorFields } from "../../src/modules/core/organisation-editing";

const fields: OrganisationEditorFields = {
  code: " CORE ", name: " Company ", tradingName: " Trading ", currency: " zmw ",
  country: " zm ", companyId: "owned-company", costCentre: " CC-1 ",
};

test("company updates carry only editable metadata, never defaults/statutory/history fields", () => {
  expect(organisationEditorPayload("company", fields, true)).toEqual({ registeredName: "Company", tradingName: "Trading", currency: "ZMW" });
  expect(organisationEditorPayload("company", fields, false)).toEqual({ registeredName: "Company", tradingName: "Trading", currency: "ZMW", code: "CORE", countryCode: "ZM", isDefault: false });
});

test("department updates preserve manager/parent/company/type/status and immutable code", () => {
  expect(organisationEditorPayload("department", fields, true)).toEqual({ name: "Company", costCentreRef: "CC-1" });
  expect(organisationEditorPayload("department", fields, false)).toEqual({ name: "Company", costCentreRef: "CC-1", code: "CORE", legalEntityId: "owned-company" });
});

test("invalid input cannot produce mutation payloads", () => {
  for (const currency of ["", "ZMWX", "123", "€MW"]) expect(() => organisationEditorPayload("company", { ...fields, currency }, true)).toThrow("currency");
  expect(() => organisationEditorPayload("company", { ...fields, name: " " }, true)).toThrow("name");
  expect(() => organisationEditorPayload("company", { ...fields, country: "ZMB" }, false)).toThrow("country");
  expect(() => organisationEditorPayload("department", { ...fields, companyId: "" }, false)).toThrow("company");
});
