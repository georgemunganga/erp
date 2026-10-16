import { createFileRoute } from "@tanstack/react-router";
import { OrganisationConfig } from "./hrm.configuration.organisation";

export const Route = createFileRoute("/core/organization")({
  head: () => ({ meta: [{ title: "Organization — ERP Core" }] }),
  component: OrganisationConfig,
});
