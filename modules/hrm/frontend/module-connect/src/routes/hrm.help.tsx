import { createFileRoute } from "@tanstack/react-router";
import { AppShell } from "@/platform/components/AppShell";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { EmptyState } from "@/platform/components/States";

export const Route = createFileRoute("/hrm/help")({
  head: () => ({
    meta: [
      { title: "Help — Kuubu HRM" },
      { name: "description", content: "Guidance, contact routes and service status." },
      { property: "og:title", content: "Help — Kuubu HRM" },
      { property: "og:description", content: "Guidance, contact routes and service status." },
    ],
  }),
  component: Page,
});

function Page() {
  return (
    <AuthGate>
      <AppShell>
      <PageHeader eyebrow="HRM" title="Help" description="Guidance, contact routes and service status." />
      <EmptyState title="Nothing here yet" body="This screen is part of the next build slice. The shell, navigation and component library it will use are already in place." />
    </AppShell>
      </AuthGate>
  );
}
