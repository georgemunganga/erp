import { createFileRoute } from "@tanstack/react-router";
import { areas } from "@/modules/hrm/areas";
import { AreaOverview } from "@/platform/components/AreaOverview";

export const Route = createFileRoute("/hrm/talent/")({
  head: () => ({
    meta: [
      { title: "Talent — Kuubu HRM" },
      { name: "description", content: "Performance, learning, succession and skills." },
      { property: "og:title", content: "Talent — Kuubu HRM" },
      { property: "og:description", content: "Performance, learning, succession and skills." },
    ],
  }),
  component: () => <AreaOverview area={areas.talent} />,
});
