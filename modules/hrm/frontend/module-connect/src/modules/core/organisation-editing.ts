/** Existing organization endpoint payloads; hidden HRM/history fields never round-trip. */
export type OrganisationEditorKind = "company" | "department";
export interface OrganisationEditorFields {
  code: string;
  name: string;
  tradingName: string;
  currency: string;
  country: string;
  companyId: string;
  costCentre: string;
}
export function organisationEditorPayload(kind: OrganisationEditorKind, fields: OrganisationEditorFields, editing: boolean): Record<string, unknown> {
  const name = fields.name.trim();
  if (!name) throw new Error("Enter a name.");
  if (!editing && !fields.code.trim()) throw new Error("Enter a code.");
  if (kind === "company") {
    const currencyInput = fields.currency.trim();
    const currency = currencyInput.toUpperCase();
    if (!/^[A-Za-z]{3}$/.test(currencyInput)) throw new Error("Enter a three-letter currency code, such as ZMW.");
    const change = { registeredName: name, tradingName: fields.tradingName.trim(), currency };
    if (editing) return change;
    const countryInput = fields.country.trim();
    const countryCode = countryInput.toUpperCase();
    if (!/^[A-Za-z]{2}$/.test(countryInput)) throw new Error("Enter a two-letter country code, such as ZM.");
    return { ...change, code: fields.code.trim(), countryCode, isDefault: false };
  }
  if (!editing && !fields.companyId) throw new Error("Select a company.");
  const change = { name, costCentreRef: fields.costCentre.trim() };
  return editing ? change : { ...change, code: fields.code.trim(), legalEntityId: fields.companyId };
}
