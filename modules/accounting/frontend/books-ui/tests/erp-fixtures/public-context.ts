// Synthetic public presentation/context only. No live user, tenant or credential data.
export const companyId = "11111111-1111-4111-8111-111111111111";
export const branchId = "22222222-2222-4222-8222-222222222222";
export const principalId = "33333333-3333-4333-8333-333333333333";
export const publicBranding = {
  displayName: "Fixture ERP", companyName: "Fixture Trading Limited", companyDomain: "example.test",
  loginHeading: "Sign in to Fixture ERP", loginDescription: "Synthetic integration fixture",
  emailPlaceholder: "name@example.test", passwordPlaceholder: "Password", supportEmail: "help@example.test",
  fontFamily: "Poppins", primaryColor: "#123456", primaryForegroundColor: "#ffffff",
  buttonColor: "#9f1239", buttonForegroundColor: "#ffffff", secondaryColor: "#f1f5f9",
  secondaryForegroundColor: "#123456", accentColor: "#e0f2fe", accentForegroundColor: "#123456",
  railColor: "#102030", railForegroundColor: "#ffffff", railMutedColor: "#cbd5e1", railActiveColor: "#334155",
  logoLightDataUri: null, logoDarkDataUri: null, faviconDataUri: null,
};
export const adminUser = {
  id: principalId, email: "preview-admin@example.test", displayName: "Preview Administrator",
  roles: ["erp_admin"], isActive: true, mustChangePassword: false, workerId: null,
};
export const workspaceContext = {
  schemaVersion: 1, tenantId: "fixture-tenant", principalId, organisationWide: true,
  companies: [{ id: companyId, code: "FIX", registeredName: "Fixture Trading Limited", currency: "ZMW", isDefault: true }],
  branches: [{ id: branchId, code: "FIX-HQ", name: "Fixture Head Office", legalEntityId: companyId, type: "Office" }],
};
