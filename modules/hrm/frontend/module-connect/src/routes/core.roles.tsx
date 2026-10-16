import { createFileRoute, redirect } from "@tanstack/react-router";
import { RolesConfig } from "./hrm.configuration.roles";

export const Route = createFileRoute("/core/roles")({
  beforeLoad: () => { throw redirect({ to: "/core/workspace/settings/$slug", params: { slug: "roles" }, replace: true }); },
  head: () => ({ meta: [{ title: "Roles — ERP Core" }] }),
  component: RolesConfig,
});
