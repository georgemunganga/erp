import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { ChevronLeft, Palette, RotateCcw, Save } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { CompanyBranding } from "@/platform/api-client";
import { useBranding } from "@/platform/branding";
import { emailPlaceholderFor, loginHeadingFor } from "@/platform/branding-copy";
import { AppShell } from "@/platform/components/AppShell";
import { Async } from "@/platform/components/Async";
import { AuthGate } from "@/platform/components/AuthGate";
import { PageHeader } from "@/platform/components/PageHeader";
import { feedback } from "@/platform/feedback";
import { realApi, useApi } from "@/platform/use-api";

export const Route = createFileRoute("/hrm/configuration/branding")({
  head: () => ({ meta: [{ title: "Branding — HR workspace" }] }),
  component: BrandingPage,
});

type BrandingForm = {
  displayName: string;
  companyName: string;
  companyDomain: string;
  loginHeading: string;
  loginDescription: string;
  emailPlaceholder: string;
  passwordPlaceholder: string;
  supportEmail: string;
  primaryColor: string;
  primaryForegroundColor: string;
  buttonColor: string;
  buttonForegroundColor: string;
  secondaryColor: string;
  secondaryForegroundColor: string;
  accentColor: string;
  accentForegroundColor: string;
  railColor: string;
  railForegroundColor: string;
  railMutedColor: string;
  railActiveColor: string;
  logoLightDataUri: string;
  logoDarkDataUri: string;
  faviconDataUri: string;
};
type AssetKey = "logoLightDataUri" | "logoDarkDataUri" | "faviconDataUri";

const defaults: BrandingForm = {
  displayName: "HR workspace",
  companyName: "Company",
  companyDomain: "",
  loginHeading: "",
  loginDescription: "",
  emailPlaceholder: "",
  passwordPlaceholder: "Enter your password",
  supportEmail: "",
  primaryColor: "#012642",
  primaryForegroundColor: "#FFFFFF",
  buttonColor: "#012642",
  buttonForegroundColor: "#FFFFFF",
  secondaryColor: "#E8F0F5",
  secondaryForegroundColor: "#012642",
  accentColor: "#E8F0F5",
  accentForegroundColor: "#012642",
  railColor: "#012642",
  railForegroundColor: "#FFFFFF",
  railMutedColor: "#A7C7DA",
  railActiveColor: "#0B3A5D",
  logoLightDataUri: "",
  logoDarkDataUri: "",
  faviconDataUri: "",
};

const colourGroups = [
  {
    title: "Primary actions",
    description: "Buttons have their own colours, separate from the header.",
    fields: [
      ["buttonColor", "Button background"],
      ["buttonForegroundColor", "Button text"],
    ],
  },
  {
    title: "Header and navigation",
    description: "Choose readable text on every dark surface.",
    fields: [
      ["primaryColor", "Header background"],
      ["primaryForegroundColor", "Header text"],
      ["railColor", "Sidebar background"],
      ["railForegroundColor", "Sidebar text"],
      ["railMutedColor", "Sidebar secondary text"],
      ["railActiveColor", "Selected navigation background"],
    ],
  },
  {
    title: "Supporting surfaces",
    description: "Colours used by secondary controls, highlights, and selected states.",
    fields: [
      ["secondaryColor", "Secondary background"],
      ["secondaryForegroundColor", "Secondary text"],
      ["accentColor", "Highlight background"],
      ["accentForegroundColor", "Highlight text"],
    ],
  },
] as const;

function fromBrand(value: CompanyBranding): BrandingForm {
  return {
    ...defaults,
    ...value,
    logoLightDataUri: value.logoLightDataUri ?? "",
    logoDarkDataUri: value.logoDarkDataUri ?? "",
    faviconDataUri: value.faviconDataUri ?? "",
  };
}

function contrastRatio(first: string, second: string): number {
  const luminance = (hex: string) => {
    if (!/^#[0-9a-f]{6}$/i.test(hex)) return 0;
    const rgb = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
    const linear = rgb.map((v) => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
    return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
  };
  const values = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (values[0] + 0.05) / (values[1] + 0.05);
}

function ContrastHint({ foreground, background }: { foreground: string; background: string }) {
  const ratio = contrastRatio(foreground, background);
  return <p className={`text-xs ${ratio < 4.5 ? "text-warning-foreground" : "text-muted-foreground"}`}>
    Text contrast {ratio.toFixed(1)}:1 {ratio < 4.5 ? "— increase to at least 4.5:1 for comfortable reading." : "— meets normal-text contrast guidance."}
  </p>;
}

function BrandingPage() {
  const branding = useApi(() => realApi.branding());
  const { setBranding } = useBranding();
  const [form, setForm] = useState<BrandingForm>(defaults);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (branding.data) setForm(fromBrand(branding.data));
  }, [branding.data]);

  function set(key: keyof BrandingForm, value: string) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  async function pickAsset(key: AssetKey, file?: File) {
    if (!file) return;
    const allowed = ["image/png", "image/jpeg", "image/webp", "image/svg+xml", "image/x-icon", "image/vnd.microsoft.icon"];
    if (!allowed.includes(file.type)) {
      feedback.blocked("Choose an image file.", "Use PNG, JPEG, WebP, SVG, or ICO.");
      return;
    }
    if (file.size > 512 * 1024) {
      feedback.blocked("Image is too large.", "Brand assets must be 512 KB or smaller.");
      return;
    }
    const value = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
    set(key, value);
  }

  async function save() {
    setSaving(true);
    try {
      const value = await realApi.updateBranding(form);
      setBranding(value);
      setForm(fromBrand(value));
      feedback.submitted("Branding saved.", "The logo and colours are now applied throughout this workspace, including sign-in.");
    } catch (error) {
      feedback.blocked("Branding was not saved.", error instanceof Error ? error.message : "Check the values and try again.");
    } finally {
      setSaving(false);
    }
  }

  async function reset() {
    if (!window.confirm("Restore the default theme and remove the uploaded logos for this tenant?")) return;
    try {
      const value = await realApi.resetBranding();
      setBranding(value);
      setForm(fromBrand(value));
      feedback.submitted("Default branding restored.", "The uploaded logos have been removed from this tenant.");
    } catch (error) {
      feedback.blocked("Branding could not be reset.", error instanceof Error ? error.message : "Try again.");
    }
  }

  return <AuthGate><AppShell>
    <Link to="/hrm/configuration" className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-primary">
      <ChevronLeft className="size-3.5" aria-hidden /> All configuration
    </Link>
    <PageHeader eyebrow="Configuration" title="Company branding" description="Control company identity, sign-in text, logos, buttons, and colours across your HR workspace." />
    <Async state={branding} rows={6}>{() => <div className="space-y-6" data-testid="company-branding-settings">
      <section className="rounded-lg border bg-surface p-5">
        <div className="mb-4 flex items-start gap-3"><Palette className="mt-0.5 size-5 text-primary" aria-hidden /><div>
          <h2 className="font-semibold">Company identity</h2>
          <p className="text-sm text-muted-foreground">These public details identify your organisation on sign-in and in the workspace.</p>
        </div></div>
        <div className="grid gap-4 md:grid-cols-2">
          <div><Label htmlFor="company-name">Company name</Label>
            <Input id="company-name" className="mt-2" value={form.companyName} maxLength={120} onChange={(event) => set("companyName", event.target.value)} />
          </div>
          <div><Label htmlFor="brand-name">Workspace display name</Label>
            <Input id="brand-name" className="mt-2" value={form.displayName} maxLength={80} onChange={(event) => set("displayName", event.target.value)} />
          </div>
          <div className="md:col-span-2"><Label htmlFor="company-domain">Company email domain</Label>
            <Input id="company-domain" className="mt-2 max-w-xl" value={form.companyDomain} maxLength={253} placeholder="example.com" onChange={(event) => set("companyDomain", event.target.value)} />
            <p className="mt-1 text-xs text-muted-foreground">Used for the email example on sign-in. Adding a domain here does not configure DNS, verify ownership, or route tenants.</p>
          </div>
        </div>
        <div className="mt-5 grid gap-4 md:grid-cols-3">
          {([ ["logoLightDataUri", "Logo for dark backgrounds"], ["logoDarkDataUri", "Logo for light backgrounds"], ["faviconDataUri", "Browser icon"] ] as const).map(([key, label]) =>
            <div key={key} className="rounded-lg border bg-surface-muted p-4">
              <Label htmlFor={key}>{label}</Label>
              <Input id={key} className="mt-2" type="file" accept="image/png,image/jpeg,image/webp,image/svg+xml,image/x-icon" onChange={(event) => { void pickAsset(key, event.target.files?.[0]); event.target.value = ""; }} />
              {form[key] ? <div className="mt-3 flex h-20 items-center justify-center rounded border bg-background p-2"><img src={form[key]} alt={`${label} preview`} className="max-h-full max-w-full object-contain" /></div>
                : <p className="mt-3 text-xs text-muted-foreground">No custom image uploaded.</p>}
              {form[key] ? <Button type="button" variant="ghost" size="sm" className="mt-2" onClick={() => set(key, "")}>Remove image</Button> : null}
            </div>
          )}
        </div>
      </section>

      <section className="rounded-lg border bg-surface p-5">
        <h2 className="font-semibold">Sign-in experience</h2>
        <p className="mb-4 text-sm text-muted-foreground">Set the words people see before they log in. Leave a heading or email example blank to generate it from the company name and domain.</p>
        <div className="grid gap-4 md:grid-cols-2">
          <div><Label htmlFor="login-heading">Sign-in heading</Label>
            <Input id="login-heading" className="mt-2" value={form.loginHeading} maxLength={80} placeholder={loginHeadingFor(form)} onChange={(event) => set("loginHeading", event.target.value)} />
          </div>
          <div><Label htmlFor="email-placeholder">Email input example</Label>
            <Input id="email-placeholder" className="mt-2" value={form.emailPlaceholder} maxLength={120} placeholder={emailPlaceholderFor(form)} onChange={(event) => set("emailPlaceholder", event.target.value)} />
          </div>
          <div className="md:col-span-2"><Label htmlFor="login-description">Sign-in helper text</Label>
            <Input id="login-description" className="mt-2" value={form.loginDescription} maxLength={240} placeholder="Use your work account to continue." onChange={(event) => set("loginDescription", event.target.value)} />
          </div>
          <div><Label htmlFor="password-placeholder">Password input placeholder</Label>
            <Input id="password-placeholder" className="mt-2" value={form.passwordPlaceholder} maxLength={80} placeholder="Enter your password" onChange={(event) => set("passwordPlaceholder", event.target.value)} />
          </div>
          <div><Label htmlFor="support-email">Help email</Label>
            <Input id="support-email" type="email" className="mt-2" value={form.supportEmail} maxLength={254} placeholder="support@example.com" onChange={(event) => set("supportEmail", event.target.value)} />
          </div>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-6">
          {colourGroups.map((group) => <section key={group.title} className="rounded-lg border bg-surface p-5">
            <h2 className="font-semibold">{group.title}</h2><p className="mb-4 text-sm text-muted-foreground">{group.description}</p>
            <div className="grid gap-4 sm:grid-cols-2">
              {group.fields.map(([key, label]) => <div key={key}>
                <Label htmlFor={key}>{label}</Label>
                <div className="mt-2 flex gap-2">
                  <Input id={key} type="color" className="w-14 shrink-0 p-1" value={form[key]} onChange={(event) => set(key, event.target.value.toUpperCase())} />
                  <Input aria-label={`${label} hex value`} value={form[key]} maxLength={7} onChange={(event) => set(key, event.target.value.toUpperCase())} />
                </div>
              </div>)}
            </div>
          </section>)}
        </div>
        <aside className="h-fit space-y-4 rounded-lg border bg-surface p-5 xl:sticky xl:top-20">
          <h2 className="font-semibold">Live preview</h2>
          <p className="text-sm text-muted-foreground">Changes here are a preview until you save.</p>
          <div className="rounded-lg border p-4">
            <p className="font-semibold">{loginHeadingFor(form)}</p>
            <p className="mt-1 text-xs text-muted-foreground">{form.loginDescription || "Use your work account to continue."}</p>
            <p className="mt-3 rounded border bg-background px-3 py-2 text-sm text-muted-foreground">{emailPlaceholderFor(form)}</p>
            <p className="mt-2 rounded border bg-background px-3 py-2 text-sm text-muted-foreground">{form.passwordPlaceholder || "Enter your password"}</p>
          </div>
          <div className="rounded-lg p-4" style={{ backgroundColor: form.primaryColor, color: form.primaryForegroundColor }}>
            <p className="font-semibold">{form.displayName}</p><p className="text-sm">Header text</p>
          </div>
          <div className="rounded-lg p-4" style={{ backgroundColor: form.railColor, color: form.railForegroundColor }}>
            <p className="font-semibold">Navigation</p>
            <p className="mt-2 text-sm" style={{ color: form.railMutedColor }}>Secondary navigation text</p>
            <p className="mt-2 rounded px-2 py-1 text-sm" style={{ backgroundColor: form.railActiveColor }}>Selected page</p>
          </div>
          <button type="button" className="rounded-full px-5 py-2 font-semibold" style={{ backgroundColor: form.buttonColor, color: form.buttonForegroundColor }}>Primary button</button>
          <ContrastHint foreground={form.buttonForegroundColor} background={form.buttonColor} />
          <ContrastHint foreground={form.railForegroundColor} background={form.railColor} />
        </aside>
      </div>

      <div className="flex flex-wrap gap-2">
        <Button type="button" onClick={() => void save()} disabled={saving}><Save className="size-4" />{saving ? "Saving…" : "Save branding"}</Button>
        <Button type="button" variant="outline" onClick={() => void reset()}><RotateCcw className="size-4" />Restore defaults</Button>
      </div>
    </div>}</Async>
  </AppShell></AuthGate>;
}
