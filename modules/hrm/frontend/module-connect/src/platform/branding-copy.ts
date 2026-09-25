import type { CompanyBranding } from "@/platform/api-client";

type LoginBranding = Pick<CompanyBranding, "companyName" | "companyDomain" | "loginHeading" | "emailPlaceholder">;

export function loginHeadingFor(branding: LoginBranding | null): string {
  return branding?.loginHeading?.trim() || `Sign in to ${branding?.companyName?.trim() || "your organisation"}`;
}

export function emailPlaceholderFor(branding: LoginBranding | null): string {
  if (branding?.emailPlaceholder?.trim()) return branding.emailPlaceholder.trim();
  return `name@${branding?.companyDomain?.trim() || "yourcompany.com"}`;
}
