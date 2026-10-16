export type CoreSetting = {
  label: string;
  path?: string;
  detail: string;
  owner: "ERP Core" | "Finance" | "Procurement";
};

export type CoreSettingGroup = { title: string; description: string; items: CoreSetting[] };

/** One directory for ERP configuration. Ownership remains with the named capability. */
export const coreSettingGroups: CoreSettingGroup[] = [
  {
    title: "Organization",
    description: "Company identity and the structure shared by every module.",
    items: [
      { label: "Profile", path: "/core/organization", detail: "Legal entities and company structure", owner: "ERP Core" },
      { label: "Branding", path: "/core/branding", detail: "Logo, colors and sign-in identity", owner: "ERP Core" },
      { label: "Locations", path: "/core/organization#locations", detail: "Branches and work locations", owner: "ERP Core" },
    ],
  },
  {
    title: "Users & Roles",
    description: "Identity, permissions and departments.",
    items: [
      { label: "Users", path: "/core/users", detail: "Organization identities and access", owner: "ERP Core" },
      { label: "Roles", path: "/core/roles", detail: "Current HRM role editor; shared roles follow", owner: "ERP Core" },
      { label: "Departments", path: "/core/organization#departments", detail: "Organization units and cost center references", owner: "ERP Core" },
    ],
  },
  {
    title: "Taxes & Compliance",
    description: "Financial tax rules stay with Finance; shared tags can be reused across modules.",
    items: [
      { label: "Taxes", detail: "Finance tax master and rates", owner: "Finance" },
      { label: "TDS", detail: "Finance withholding rules", owner: "Finance" },
      { label: "Reporting Tags", detail: "Cross-module reporting dimensions", owner: "ERP Core" },
    ],
  },
  {
    title: "Setup & Configurations",
    description: "Shared defaults with links to settings owned by a specific module.",
    items: [
      { label: "General", detail: "Locale, time zone and display defaults", owner: "ERP Core" },
      { label: "Currencies", detail: "Enabled currency reference and formatting", owner: "ERP Core" },
      { label: "Payment Terms", detail: "Finance-owned commercial reference", owner: "Finance" },
      { label: "Vendor Portal", path: "/procurement/workspace/settings/vendor-portal", detail: "Supplier access preferences", owner: "Procurement" },
      { label: "Punchout Catalogs", path: "/procurement/workspace/settings/punchout-catalogs", detail: "External buying catalogs", owner: "Procurement" },
    ],
  },
  {
    title: "Customization",
    description: "Shared document and communication templates; no duplicate module editors.",
    items: [
      { label: "Transaction Number Series", detail: "Document identifiers by entity and module", owner: "ERP Core" },
      { label: "PDF Templates", detail: "Company letterheads and document formats", owner: "ERP Core" },
      { label: "Email Notifications", detail: "Shared delivery templates and preferences", owner: "ERP Core" },
      { label: "Web Tabs", detail: "Approved module navigation links", owner: "ERP Core" },
    ],
  },
];
