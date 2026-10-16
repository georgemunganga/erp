import { createFileRoute } from "@tanstack/react-router";
import { AuthGate } from "@/platform/components/AuthGate";
import { AppShell } from "@/platform/components/AppShell";
import { BusinessConfig } from "./hrm.configuration.business";
import { HolidaysPage } from "./hrm.configuration.holidays";
import { PayrollSetup } from "./hrm.configuration.payroll";
import { LeaveTypesPage } from "./hrm.configuration.leave-types";
import { ContractTypesPage } from "./hrm.configuration.contract-types";
import { LeavePeriodsPage } from "./hrm.configuration.leave-periods";
import { ProcessConfig } from "./hrm.configuration.process";
import { ComplianceConfig } from "./hrm.configuration.compliance";
import { IntegrationOperations } from "./hrm.configuration.integrations";
import { GoLiveConfiguration } from "./hrm.configuration.go-live";
import { TechnicalConfig } from "./hrm.configuration.technical";

const pages = {
  business: BusinessConfig,
  holidays: HolidaysPage,
  payroll: PayrollSetup,
  "leave-types": LeaveTypesPage,
  "contract-types": ContractTypesPage,
  "leave-periods": LeavePeriodsPage,
  process: ProcessConfig,
  compliance: ComplianceConfig,
  integrations: IntegrationOperations,
  "go-live": GoLiveConfiguration,
  technical: TechnicalConfig,
};

export const Route = createFileRoute("/hrm/workspace/settings/$slug")({
  head: ({ params }) => ({ meta: [{ title: `${params.slug.replaceAll("-", " ")} — HRM settings` }] }),
  component: HrmSettingPage,
});

function HrmSettingPage() {
  const { slug } = Route.useParams();
  const Page = pages[slug as keyof typeof pages];
  return <AuthGate>{Page ? <Page /> : <AppShell><div className="rounded-xl border bg-surface p-6"><h1 className="text-xl font-semibold">Setting not found</h1><a className="mt-3 inline-block text-primary underline" href="/hrm/workspace/settings">All HRM settings</a></div></AppShell>}</AuthGate>;
}
