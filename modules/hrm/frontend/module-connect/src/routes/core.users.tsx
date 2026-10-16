import { createFileRoute } from "@tanstack/react-router";
import { UsersConfiguration } from "./hrm.configuration.users";

export const Route = createFileRoute("/core/users")({
  head: () => ({ meta: [{ title: "Users — ERP Core" }] }),
  component: UsersConfiguration,
});
