import type { ReactNode } from "react";
import { BrandIdentity } from "@/platform/components/BrandIdentity";
import { ModuleSwitcher } from "@/platform/components/ModuleSwitcher";
import { ErpTopBar } from "@amizopower/erp-ui";

/** One header frame for ERP modules. Each module supplies its own scoped controls. */
export function ErpTopNav({ current, navigation, context, actions }: {
  current: "core" | "hrm" | "procurement";
  navigation?: ReactNode;
  context?: ReactNode;
  actions?: ReactNode;
}) {
  return <ErpTopBar>
    <div className="flex h-14 min-w-0 items-center gap-1 px-2 sm:gap-2 sm:px-3">
      {navigation}
      <a href="/" aria-label="ERP home" className="flex shrink-0 items-center gap-2 rounded-md px-1.5 py-1 text-primary-foreground hover:bg-primary-foreground/10 sm:px-2">
        <BrandIdentity onDark showName={false} />
      </a>
      <div className="flex shrink-0 items-center"><ModuleSwitcher current={current} /></div>
      {context ? <div className="min-w-0">{context}</div> : null}
      <div className="ml-auto flex min-w-0 items-center gap-0.5 sm:gap-1">{actions}</div>
    </div>
  </ErpTopBar>;
}
