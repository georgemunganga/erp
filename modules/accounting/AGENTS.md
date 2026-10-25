# Accounting module

Follow the root GW-ENG-STD-001 Tier 3 standard. The imported Books reference is a UI-only prototype. Preserve its screens, fields, navigation and workflow paths; adapt shared ERP components and branding without reducing the presentation.

The imported local engine and seed data are preview mechanisms, never an accounting backend, bank connection, posting authority or durable ERP record. Keep the preview clearly identified and namespaced. Real accounting implementation requires the established 28-section feature specifications and owned contracts.

Core owns shared identity/organisation/permissions/branding/workflow/audit/documents. Procurement remains owner of purchasing and supplier procurement profiles; Accounting/Finance will own accounting/AP/AR/bank reconciliation and account references. Do not duplicate existing ERP backend services during this UI import. Never add a second login/password store or copy secrets into frontend source.
