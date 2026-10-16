import { createFileRoute, Navigate } from "@tanstack/react-router";

export const Route = createFileRoute("/hrm/configuration/")({
  head: () => ({ meta: [{ title: "HRM settings" }] }),
  component: () => <Navigate to="/hrm/workspace/settings" replace />,
});
