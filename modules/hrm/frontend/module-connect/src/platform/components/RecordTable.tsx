import { forwardRef } from "react";
import type { TableHTMLAttributes } from "react";
import { cn } from "@/lib/utils";

/** Shared visual surface for record lists that need their own cells and actions. */
export const RecordTable = forwardRef<HTMLTableElement, TableHTMLAttributes<HTMLTableElement>>(
  ({ className, ...props }, ref) => (
    <table ref={ref} className={cn("erp-record-table w-full border-collapse text-left text-sm", className)} {...props} />
  ),
);

RecordTable.displayName = "RecordTable";
