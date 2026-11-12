import { createFileRoute } from "@tanstack/react-router";
import { areas } from "@/modules/hrm/areas";
import { AreaOverview } from "@/platform/components/AreaOverview";

export const Route = createFileRoute("/hrm/recruitment/")({
  head: () => ({
    meta: [
      { title: "Recruitment — Kuubu HRM" },
      { name: "description", content: "Requisitions, vacancies, candidates, interviews and offers." },
      { property: "og:title", content: "Recruitment — Kuubu HRM" },
      { property: "og:description", content: "Requisitions, vacancies, candidates, interviews and offers." },
    ],
  }),
  component: () => <AreaOverview area={areas.recruitment} />,
});
