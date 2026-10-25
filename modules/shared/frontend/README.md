# Shared browser presentation helpers

Core owns the values returned by `/api/hrm/branding`; these framework-neutral helpers apply its validated colors/fonts and the existing ERP presentation-cache key. They are reused by the Accounting adapter. Initial source is the established Procurement `shared/branding.ts` at module-connect c370275. This directory owns no identity, permissions, organisation master or business records. The existing HRM and Procurement deployments retain their current adapters until a separately verified migration; this import does not replace them.
