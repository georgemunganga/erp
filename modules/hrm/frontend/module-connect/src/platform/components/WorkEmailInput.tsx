import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

export function completeWorkEmail(value: string, domain?: string | null): string {
  const address = value.trim();
  const suffix = domain?.trim().replace(/^@/, "").toLowerCase();
  return address.includes("@") || !suffix || !address ? address : `${address}@${suffix}`;
}

export function isValidWorkEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

export function WorkEmailInput({ id, value, onChange, domain, domainChoices, selectedDomain, onDomainChange, placeholder, autoComplete = "off", required = false }: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  domain?: string | null;
  domainChoices?: string[];
  selectedDomain?: string;
  onDomainChange?: (domain: string) => void;
  placeholder?: string;
  autoComplete?: string;
  required?: boolean;
}) {
  const suffix = domain?.trim().replace(/^@/, "").toLowerCase();
  const selectable = Boolean(domainChoices?.length && selectedDomain && onDomainChange);
  const showSuffix = Boolean(!selectable && suffix && !value.includes("@"));
  const choices = [...new Set(domainChoices?.map((choice) => choice.replace(/^@/, "").toLowerCase()).filter(Boolean) ?? [])];
  return <div className="flex h-11 w-full overflow-hidden rounded-lg border border-input bg-background shadow-sm focus-within:ring-2 focus-within:ring-ring/30">
    <Input
      id={id}
      type="text"
      inputMode="email"
      autoCapitalize="none"
      autoCorrect="off"
      spellCheck={false}
      autoComplete={autoComplete}
      className="h-full min-w-0 flex-1 rounded-none border-0 bg-transparent shadow-none focus-visible:ring-0"
      placeholder={placeholder ?? (selectable && selectedDomain === "custom" ? "name@example.com" : suffix ? "first.last" : "name@company.com")}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      required={required}
    />
    {showSuffix && <span aria-hidden="true" className="flex shrink-0 items-center border-l bg-surface-muted px-3 text-sm text-muted-foreground">@{suffix}</span>}
    {selectable && <Select value={selectedDomain} onValueChange={onDomainChange}>
      <SelectTrigger aria-label="Email domain" className="h-full w-auto min-w-25 max-w-[55%] shrink-0 rounded-none border-0 border-l bg-surface-muted px-2 text-xs shadow-none focus:ring-0 sm:text-sm">
        <SelectValue />
      </SelectTrigger>
      <SelectContent align="end">
        {choices.map((choice) => <SelectItem key={choice} value={choice}>@{choice}</SelectItem>)}
        <SelectItem value="custom">Other email address</SelectItem>
      </SelectContent>
    </Select>}
  </div>;
}
