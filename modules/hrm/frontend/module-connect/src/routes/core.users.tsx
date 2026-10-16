import { createFileRoute, Outlet, useRouterState } from "@tanstack/react-router";
import { UsersConfiguration } from "./hrm.configuration.users";

export const Route = createFileRoute("/core/users")({
  head: () => ({ meta: [{ title: "Users — ERP Core" }] }),
  component: CoreUsersPage,
});

function CoreUsersPage() {
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  return pathname.startsWith("/core/users/") ? <Outlet /> : <UsersConfiguration />;
}
