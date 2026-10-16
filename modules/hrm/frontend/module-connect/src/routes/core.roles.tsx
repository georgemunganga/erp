import { createFileRoute } from "@tanstack/react-router";
import { RolesConfig } from "./hrm.configuration.roles";

export const Route = createFileRoute("/core/roles")({
  head: () => ({ meta: [{ title: "Roles — ERP Core" }] }),
  component: RolesConfig,
});
