# ERP UI component standard

ERP Core sets the tenant brand and module-wide font. HRM and Procurement should use the same type scale, control dimensions, and semantic colors while retaining page layouts that suit each workflow.

| Element | Standard |
| --- | --- |
| Root text and tenant font | 17px root; `--erp-font-family` from Core Branding |
| Page title | `text-2xl font-semibold` |
| Navigation and form labels | `text-sm` |
| Standard input, select, and button | Minimum height `2.75rem` (about 47px at the ERP root) |
| Standard control corners | `rounded-lg` |
| Primary action | Pill button, `text-base`, `px-6`; one primary action per page |
| Card | `rounded-xl`, semantic `bg-card` and `border-border` |
| Content | `max-w-6xl`; wide tables scroll within their card |

Use the wrappers under `src/components/ui/` for common controls. Button, Input, Textarea, and Card now re-export the pinned `@amizopower/erp-ui` package; other controls continue to use shadcn/Radix wrappers. Use `src/platform/components/PageHeader.tsx`, the shared ERP top navigation and sidebar, and the standard table wrapper for repeated page structures. Use semantic colors from `src/theme/tokens.css`, not tenant-specific hex values in a feature page. A branding change must flow through Core Branding.

The Procurement workspace is a separate build with adapters in `reference-ui/src/components/ui/index.tsx`. Its common Button, Input, Textarea, and Card controls consume the same pinned package revision. Select, Switch, table behavior, and module navigation still have separate implementations; coordinate changes to those until they are extracted. Use compact typography only for secondary metadata and document workflow-specific exceptions.
