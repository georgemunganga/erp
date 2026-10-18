import { forwardRef } from "react";
import type { TableHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

/** Shared visual surface for record lists and denser business grids. */
export const RecordTable = forwardRef<HTMLTableElement, TableHTMLAttributes<HTMLTableElement> & { variant?: "regular" | "compact" | "matrix" }>(
  ({ className, variant = "regular", ...props }, ref) => (
    <table ref={ref} className={cn("erp-record-table w-full border-collapse text-left text-sm", `erp-record-table--${variant}`, className)} {...props} />
  ),
);

RecordTable.displayName = "RecordTable";
