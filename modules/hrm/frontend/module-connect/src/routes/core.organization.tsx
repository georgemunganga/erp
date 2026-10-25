import { createFileRoute, redirect } from "@tanstack/react-router";
import { OrganisationConfig } from "./hrm.configuration.organisation";

export const Route = createFileRoute("/core/organization")({
  beforeLoad: () => { throw redirect({ to: "/core/workspace/settings/$slug", params: { slug: "organization" }, replace: true }); },
  head: () => ({ meta: [{ title: "Organization — ERP Core" }] }),
  component: OrganisationConfig,
});
