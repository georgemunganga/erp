import { Input } from "@/components/ui/input";

export function completeWorkEmail(value: string, domain?: string | null): string {
  const address = value.trim();
  const suffix = domain?.trim().replace(/^@/, "").toLowerCase();
  return address.includes("@") || !suffix || !address ? address : `${address}@${suffix}`;
}

export function isValidWorkEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
}

export function WorkEmailInput({ id, value, onChange, domain, placeholder, autoComplete = "off", required = false }: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  domain?: string | null;
  placeholder?: string;
  autoComplete?: string;
  required?: boolean;
}) {
  const suffix = domain?.trim().replace(/^@/, "").toLowerCase();
  const showSuffix = Boolean(suffix && !value.includes("@"));
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
      placeholder={placeholder ?? (suffix ? "first.last" : "name@company.com")}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      required={required}
    />
    {showSuffix && <span aria-hidden="true" className="flex shrink-0 items-center border-l bg-surface-muted px-3 text-sm text-muted-foreground">@{suffix}</span>}
  </div>;
}
