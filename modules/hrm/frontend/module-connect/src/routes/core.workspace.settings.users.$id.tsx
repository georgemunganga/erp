import { createFileRoute } from "@tanstack/react-router";
import { UserDetail } from "./hrm.configuration.users.$id";

export const Route = createFileRoute("/core/workspace/settings/users/$id")({
  head: () => ({ meta: [{ title: "User account — ERP Core" }] }),
  component: () => <UserDetail id={Route.useParams().id} />,
});
