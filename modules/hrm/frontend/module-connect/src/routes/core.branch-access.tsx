import { createFileRoute } from "@tanstack/react-router";
import { CoreShell } from "@/modules/core/CoreShell";
import { CoreAdminGate } from "@/platform/components/AuthGate";
import { BranchAccessPage } from "./hrm.configuration.branch-access";

export const Route = createFileRoute("/core/branch-access")({
  head: () => ({ meta: [{ title: "Branch access — ERP Core" }] }),
  component: () => <CoreAdminGate><CoreShell><BranchAccessPage /></CoreShell></CoreAdminGate>,
});
