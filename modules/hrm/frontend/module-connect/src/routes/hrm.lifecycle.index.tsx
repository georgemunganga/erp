import { createFileRoute } from "@tanstack/react-router";
import { areas } from "@/modules/hrm/areas";
import { AreaOverview } from "@/platform/components/AreaOverview";

export const Route = createFileRoute("/hrm/lifecycle/")({
  head: () => ({
    meta: [
      { title: "Lifecycle — Kuubu HRM" },
      { name: "description", content: "Onboarding, movements, assets, separation and rehire." },
      { property: "og:title", content: "Lifecycle — Kuubu HRM" },
      { property: "og:description", content: "Onboarding, movements, assets, separation and rehire." },
    ],
  }),
  component: () => <AreaOverview area={areas.lifecycle} />,
});
