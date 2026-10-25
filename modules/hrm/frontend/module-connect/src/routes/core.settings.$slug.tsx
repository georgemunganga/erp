import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/core/settings/$slug")({
  beforeLoad: ({ params }) => {
    throw redirect({ to: "/core/workspace/settings/$slug", params: { slug: params.slug }, replace: true });
  },
  component: () => null,
});
