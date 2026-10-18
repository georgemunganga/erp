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

Page titles sit directly on the page background. Keep the header to the title, essential context, and an action or search when needed. Use cards for settings groups, forms, tables, and record content rather than wrapping the page title.

Use the wrappers under `src/components/ui/` for common controls. Button, Input, Textarea, Card, Select, FormSelect, Switch, and Table now re-export the pinned `@amizopower/erp-ui` package. The package also supplies the top bar frame and sidebar header. Use `src/platform/components/PageHeader.tsx` and keep module navigation links in their modules. Use semantic colors from `src/theme/tokens.css`, not tenant-specific hex values in a feature page. A branding change must flow through Core Branding.

The Procurement workspace is a separate build with adapters in `reference-ui/src/components/ui/index.tsx`. It consumes the same package revision. Radix handles select and switch keyboard behavior; Procurement keeps its clean dropdown/list presentation and its own filters, selection, and row actions. Core and module navigation destinations remain separate, while the shell frame and sidebar heading are shared. Use compact typography only for secondary metadata and document workflow-specific exceptions.
