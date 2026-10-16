import { createFileRoute, redirect } from "@tanstack/react-router";
import { BrandingPage } from "./hrm.configuration.branding";

export const Route = createFileRoute("/core/branding")({
  beforeLoad: () => { throw redirect({ to: "/core/workspace/settings/branding", replace: true }); },
  head: () => ({ meta: [{ title: "Company branding — ERP Core" }] }),
  component: BrandingPage,
});
