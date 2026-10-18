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

Use the shadcn/Radix wrappers under `src/components/ui/` for common controls. Use `src/platform/components/PageHeader.tsx`, the shared ERP top navigation and sidebar, and the standard table wrapper for repeated page structures. Use semantic colors from `src/theme/tokens.css`, not tenant-specific hex values in a feature page. A branding change must flow through Core Branding.

The Procurement workspace is currently a separate build with matching adapters in `reference-ui/src/components/ui/index.tsx`. Keep its sizing and tokens aligned with this standard. The two frontends do not yet import one React component package. When they share a package, move the primitives there; until then, new behavior or variants must be checked in both modules. Use compact typography only for secondary metadata and document workflow-specific exceptions.
