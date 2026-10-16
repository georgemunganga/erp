import { createFileRoute } from "@tanstack/react-router";
import { BrandingPage } from "./hrm.configuration.branding";

export const Route = createFileRoute("/core/branding")({
  head: () => ({ meta: [{ title: "Company branding — ERP Core" }] }),
  component: BrandingPage,
});
