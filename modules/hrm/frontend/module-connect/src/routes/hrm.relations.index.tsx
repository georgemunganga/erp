import { createFileRoute } from "@tanstack/react-router";
import { areas } from "@/modules/hrm/areas";
import { AreaOverview } from "@/platform/components/AreaOverview";

export const Route = createFileRoute("/hrm/relations/")({
  head: () => ({
    meta: [
      { title: "Relations and safety — Kuubu HRM" },
      { name: "description", content: "Cases, discipline, safety, ethics and protected disclosures." },
      { property: "og:title", content: "Relations and safety — Kuubu HRM" },
      { property: "og:description", content: "Cases, discipline, safety, ethics and protected disclosures." },
    ],
  }),
  component: () => <AreaOverview area={areas.relations} />,
});
