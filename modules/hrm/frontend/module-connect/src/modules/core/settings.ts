export type CoreSetting = {
  label: string;
  path?: string;
  detail: string;
  owner: "ERP Core";
};

export const settingSlug = (label: string) => label.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
export const settingPath = (setting: CoreSetting) => setting.path ?? `/core/workspace/settings/${settingSlug(setting.label)}`;

export type CoreSettingGroup = { title: string; description: string; items: CoreSetting[] };

/** One directory for ERP configuration. Ownership remains with the named capability. */
export const coreSettingGroups: CoreSettingGroup[] = [
  {
    title: "Organization",
    description: "Company identity and the structure shared by every module.",
    items: [
      { label: "Profile", path: "/core/workspace/settings/organization", detail: "Legal entities and company structure", owner: "ERP Core" },
      { label: "Branding", path: "/core/workspace/settings/branding", detail: "Logo, colors and sign-in identity", owner: "ERP Core" },
      { label: "Locations", path: "/core/workspace/settings/organization#locations", detail: "Branches and work locations", owner: "ERP Core" },
    ],
  },
  {
    title: "Users & Roles",
    description: "Identity, permissions and departments.",
    items: [
      { label: "Users", path: "/core/workspace/settings/users", detail: "Organization identities and access", owner: "ERP Core" },
      { label: "Roles", path: "/core/workspace/settings/roles", detail: "Current HRM role editor; shared roles follow", owner: "ERP Core" },
      { label: "Branch Access", path: "/core/workspace/settings/branch-access", detail: "Assign users to permitted branches", owner: "ERP Core" },
      { label: "Departments", path: "/core/workspace/settings/organization#departments", detail: "Organization units and cost center references", owner: "ERP Core" },
    ],
  },
  {
    title: "Taxes & Compliance",
    description: "Shared reporting dimensions and organization-wide compliance references.",
    items: [
      { label: "Reporting Tags", detail: "Cross-module reporting dimensions", owner: "ERP Core" },
    ],
  },
  {
    title: "Setup & Configurations",
    description: "Defaults used across all ERP modules.",
    items: [
      { label: "General", detail: "Locale, time zone and display defaults", owner: "ERP Core" },
      { label: "Currencies", detail: "Enabled currency reference and formatting", owner: "ERP Core" },
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

export const findCoreSetting = (slug: string) => coreSettingGroups
  .flatMap((group) => group.items.map((item) => ({ ...item, group: group.title })))
  .find((item) => !item.path && settingSlug(item.label) === slug);
