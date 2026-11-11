import { forwardRef } from "react";
import { Button as SharedButton, type ButtonProps } from "@amizopower/erp-ui";
export { buttonVariants, type ButtonProps } from "@amizopower/erp-ui";

// Preserve shared behavior and expose variants to the product theme, including asChild links.
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = "default", ...props },
  ref,
) {
  return <SharedButton ref={ref} variant={variant} data-erp-button={variant} {...props} />;
});
