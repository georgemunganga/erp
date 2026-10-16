import { createFileRoute } from "@tanstack/react-router";
import { UserDetail } from "./hrm.configuration.users.$id";

export const Route = createFileRoute("/core/users/$id")({
  head: () => ({ meta: [{ title: "User account — ERP Core" }] }),
  component: CoreUserDetail,
});

function CoreUserDetail() {
  const { id } = Route.useParams();
  return <UserDetail id={id} />;
}
