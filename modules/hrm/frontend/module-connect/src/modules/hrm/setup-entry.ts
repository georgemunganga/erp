/** Navigation only: the setup API remains the authority for access and completion. */
export function shouldOpenHrmSetup(input: {
  real: boolean;
  product: boolean;
  roles: readonly string[];
  confined: boolean | undefined;
  status: string | undefined;
}) {
  return input.real && input.confined === false && input.status === "pending"
    && (!input.product || input.roles.some(role => role === "hr_admin" || role === "hr_ops"));
}
