import { createFileRoute, Navigate } from "@tanstack/react-router";

export const Route = createFileRoute("/core/")({
  head: () => ({ meta: [{ title: "ERP Core" }] }),
  component: () => <Navigate to="/core/workspace/settings" replace />,
});
