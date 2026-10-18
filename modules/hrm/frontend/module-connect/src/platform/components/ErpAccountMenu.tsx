import { LogOut, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { useAuth } from "@/platform/auth";

export function ErpAccountMenu({ settingsHref }: { settingsHref?: string }) {
  const { user, signOut } = useAuth();
  return <DropdownMenu>
    <DropdownMenuTrigger asChild><Button variant="ghost" size="icon" aria-label="User menu"><UserRound className="size-5" aria-hidden /></Button></DropdownMenuTrigger>
    <DropdownMenuContent align="end" className="w-64">
      <DropdownMenuLabel className="flex flex-col"><span>ERP account</span><span className="truncate text-xs font-normal text-muted-foreground">{user?.preferredUsername ?? user?.email ?? "Organisation account"}</span></DropdownMenuLabel>
      {settingsHref ? <><DropdownMenuSeparator /><DropdownMenuItem asChild><a href={settingsHref}>Settings</a></DropdownMenuItem></> : null}
      <DropdownMenuSeparator />
      <DropdownMenuItem onSelect={() => signOut()}><LogOut className="size-4" aria-hidden />Sign out</DropdownMenuItem>
    </DropdownMenuContent>
  </DropdownMenu>;
}
