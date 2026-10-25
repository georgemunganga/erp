import { createFileRoute, redirect } from "@tanstack/react-router";
import { UserDetail } from "./hrm.configuration.users.$id";

export const Route = createFileRoute("/core/users/$id")({
  beforeLoad: ({ params }) => { throw redirect({ to: "/core/workspace/settings/users/$id", params: { id: params.id }, replace: true }); },
  head: () => ({ meta: [{ title: "User account — ERP Core" }] }),
  component: CoreUserDetail,
});

function CoreUserDetail() {
  const { id } = Route.useParams();
  return <UserDetail id={id} />;
}
