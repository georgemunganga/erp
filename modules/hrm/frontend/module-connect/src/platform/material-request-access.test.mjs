import test from "node:test";
import assert from "node:assert/strict";
import { mayCreateMaterialRequest } from "./material-request-access.ts";
const grant = { principalId: "user-a", legalEntityId: "company-a", permissions: ["procurement-access", "procurement-request-read", "procurement-request-create"] };
test("request shortcut requires the current actor, company and all request permissions", () => {
 assert.equal(mayCreateMaterialRequest(grant, "user-a", "company-a"), true);
 assert.equal(mayCreateMaterialRequest(grant, "user-b", "company-a"), false);
 assert.equal(mayCreateMaterialRequest(grant, "user-a", "company-b"), false);
 for (const permission of grant.permissions) assert.equal(mayCreateMaterialRequest({ ...grant, permissions: grant.permissions.filter(x => x !== permission) }, "user-a", "company-a"), false);
});
test("malformed/unavailable authority never creates a dashboard shortcut", () => {
 for (const value of [null, {}, { ...grant, permissions: "procurement-request-create" }, { ...grant, permissions: [...grant.permissions, 42] }]) assert.equal(mayCreateMaterialRequest(value, "user-a", "company-a"), false);
});
