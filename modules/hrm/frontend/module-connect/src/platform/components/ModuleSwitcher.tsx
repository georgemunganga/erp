import { FULL_PRODUCT_HRM } from "@/modules/core/product-hrm-session";
import { ChevronDown, LayoutGrid } from "lucide-react";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { useAuth } from "@/platform/auth";
import { assignedModules, modules } from "@/platform/modules";

export function ModuleSwitcher({ current }: { current: "core" | "hrm" | "procurement" }) {
  const { user, coreActions } = useAuth();
  const real = import.meta.env.VITE_USE_REAL_API === "true";
  const visible = FULL_PRODUCT_HRM ? modules.filter(module => module.id === "hrm") : real ? assignedModules(user?.roles ?? [], coreActions) : modules.filter((module) => module.available);
  const active = modules.find((module) => module.id === current);

  return <DropdownMenu>
    <DropdownMenuTrigger asChild>
      <Button variant="ghost" size="sm" aria-label="Switch ERP module" className="h-9 gap-1 rounded-md px-2 text-primary-foreground hover:bg-primary-foreground/10 hover:text-primary-foreground sm:px-3">
        <LayoutGrid className="size-4" aria-hidden />
        <span className="hidden lg:inline">{active?.label ?? "Modules"}</span>
        <ChevronDown className="size-3.5" aria-hidden />
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="start" className="w-56">
      <DropdownMenuLabel>My modules</DropdownMenuLabel>
      {FULL_PRODUCT_HRM ? <DropdownMenuItem asChild><a href="https://www.kuubuerp.com/account/#/companies">Choose company or module in Kuubu</a></DropdownMenuItem> : null}
      {visible.map((module) => <DropdownMenuItem key={module.id} asChild>
        <a href={module.to} aria-current={module.id === current ? "page" : undefined} className={module.id === current ? "font-semibold" : undefined}>{module.label}</a>
      </DropdownMenuItem>)}
    </DropdownMenuContent>
  </DropdownMenu>;
}
