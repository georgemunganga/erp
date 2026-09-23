import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useRouterState } from "@tanstack/react-router";
import type { CompanyBranding } from "@/platform/api-client";
import { useAuth } from "@/platform/auth";
import { realApi } from "@/platform/use-api";

type BrandingContextValue = {
  branding: CompanyBranding | null;
  setBranding: (value: CompanyBranding) => void;
};

const BrandingContext = createContext<BrandingContextValue | null>(null);

const colourTokens: Record<string, keyof CompanyBranding> = {
  "--primary": "primaryColor",
  "--primary-foreground": "primaryForegroundColor",
  "--button-primary": "buttonColor",
  "--button-primary-foreground": "buttonForegroundColor",
  "--secondary": "secondaryColor",
  "--secondary-foreground": "secondaryForegroundColor",
  "--accent": "accentColor",
  "--accent-foreground": "accentForegroundColor",
  "--rail": "railColor",
  "--rail-foreground": "railForegroundColor",
  "--rail-muted": "railMutedColor",
  "--rail-active": "railActiveColor",
  "--ring": "primaryColor",
};

export function BrandingProvider({ children }: { children: ReactNode }) {
  const { authenticated } = useAuth();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const [branding, setBranding] = useState<CompanyBranding | null>(null);

  useEffect(() => {
    if (import.meta.env.VITE_USE_REAL_API !== "true") return;
    let active = true;
    // This endpoint contains public presentation settings only. Fetch it before
    // sign-in and again after authentication so tenant claims are respected.
    realApi.publicBranding().then((value) => {
      if (active) setBranding(value);
    }).catch(() => { /* A branding outage must not prevent sign-in. */ });
    return () => { active = false; };
  }, [authenticated]);

  useEffect(() => {
    if (!branding) return;
    const root = document.documentElement;
    for (const [token, field] of Object.entries(colourTokens)) {
      const value = branding[field];
      if (typeof value === "string") root.style.setProperty(token, value);
    }
    document.title = `${branding.displayName} — HR workspace`;
    const icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (icon) icon.href = branding.faviconDataUri || "/favicon.svg";
  }, [branding, pathname]);

  return <BrandingContext.Provider value={{ branding, setBranding }}>{children}</BrandingContext.Provider>;
}

export function useBranding() {
  const value = useContext(BrandingContext);
  if (!value) throw new Error("useBranding requires BrandingProvider");
  return value;
}
