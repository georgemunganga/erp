import { forwardRef, type ComponentPropsWithoutRef } from "react";
import { Card as SharedCard } from "@amizopower/erp-ui";
export {
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  CardFooter,
} from "@amizopower/erp-ui";

export const Card = forwardRef<HTMLDivElement, ComponentPropsWithoutRef<typeof SharedCard>>(
  function Card(props, ref) {
    return <SharedCard ref={ref} data-slot="card" {...props} />;
  },
);
