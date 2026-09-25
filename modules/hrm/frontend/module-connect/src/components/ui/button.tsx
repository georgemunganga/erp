import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "@/lib/utils";

const buttonVariants = cva(
  // Pill buttons — Newworldcargo's current direction. Fully rounded reads as friendlier
  // and, at these target sizes, makes the hit area obvious to someone scanning a
  // dense screen. Radius lives here rather than on --radius so cards, inputs and
  // dialogs keep their softer rectangle and the buttons stay the thing that pops.
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-full text-base font-semibold cursor-pointer transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 disabled:cursor-not-allowed [&_svg]:pointer-events-none [&_svg]:size-5 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default: "bg-button-primary text-button-primary-foreground shadow hover:bg-button-primary/90",
        destructive: "bg-destructive text-destructive-foreground shadow-sm hover:bg-destructive/90",
        outline:
          "border border-input bg-background shadow-sm hover:bg-accent hover:text-accent-foreground",
        secondary: "bg-secondary text-secondary-foreground shadow-sm hover:bg-secondary/80",
        ghost: "hover:bg-accent hover:text-accent-foreground",
        link: "text-primary underline-offset-4 hover:underline",
      },
      size: {
        // Minimum 44px on the default and icon sizes: the WCAG 2.1 AAA / platform
        // HIG target size. This product is used daily by managers and approvers who
        // are not all young-eyed, so `sm` is still 40px rather than genuinely small.
        // Horizontal padding is a step wider than a square button would need:
        // a pill's curve eats into the usable edge, so text needs the room.
        default: "h-11 px-6 py-2",
        sm: "h-10 px-5 text-sm",
        lg: "h-12 px-9 text-lg",
        icon: "h-11 w-11",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button";
    return (
      <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />
    );
  },
);
Button.displayName = "Button";

export { Button, buttonVariants };
