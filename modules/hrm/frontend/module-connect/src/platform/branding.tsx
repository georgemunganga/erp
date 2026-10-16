import { createContext, useContext, useEffect, useLayoutEffect, useState, type ReactNode } from "react";
import { useRouterState } from "@tanstack/react-router";
import type { CompanyBranding } from "@/platform/api-client";
import { useAuth } from "@/platform/auth";
import { realApi } from "@/platform/use-api";

type BrandingContextValue = {
  branding: CompanyBranding | null;
  ready: boolean;
  setBranding: (value: CompanyBranding) => void;
};

const BrandingContext = createContext<BrandingContextValue | null>(null);
const BRANDING_CACHE_KEY = "erp.company-branding.v1";

function cachedBranding(): CompanyBranding | null {
  if (typeof window === "undefined") return null;
  try {
    const value = JSON.parse(localStorage.getItem(BRANDING_CACHE_KEY) || "null") as CompanyBranding | null;
    return value && typeof value.primaryColor === "string" && typeof value.fontFamily === "string" ? value : null;
  } catch { return null; }
}

function rememberBranding(value: CompanyBranding) {
  try { localStorage.setItem(BRANDING_CACHE_KEY, JSON.stringify(value)); } catch { /* Storage can be disabled or full. */ }
}

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
  const [branding, setCurrentBranding] = useState<CompanyBranding | null>(null);
  const [ready, setReady] = useState(false);
  const setBranding = (value: CompanyBranding) => {
    rememberBranding(value);
    setCurrentBranding(value);
  };

  useLayoutEffect(() => {
    const cached = cachedBranding();
    if (cached) {
      setCurrentBranding(cached);
      setReady(true);
    }
    const onStorage = (event: StorageEvent) => {
      if (event.key === BRANDING_CACHE_KEY) {
        const next = cachedBranding();
        if (next) setCurrentBranding(next);
      }
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  useEffect(() => {
    if (import.meta.env.VITE_USE_REAL_API !== "true") {
      setReady(true);
      return;
    }
    let active = true;
    const timeout = window.setTimeout(() => { if (active) setReady(true); }, 3000);
    // This endpoint contains public presentation settings only. Fetch it before
    // sign-in and again after authentication so tenant claims are respected.
    realApi.publicBranding().then((value) => {
      if (active) setBranding(value);
    }).catch(() => { /* A branding outage must not prevent sign-in. */ })
      .finally(() => { if (active) setReady(true); window.clearTimeout(timeout); });
    return () => { active = false; window.clearTimeout(timeout); };
  }, [authenticated]);

  useLayoutEffect(() => {
    if (!branding) return;
    const root = document.documentElement;
    for (const [token, field] of Object.entries(colourTokens)) {
      const value = branding[field];
      if (typeof value === "string") root.style.setProperty(token, value);
    }
    const fonts: Record<string, string> = {
      Montserrat: '"Montserrat", ui-sans-serif, system-ui, sans-serif',
      Poppins: '"Poppins", ui-sans-serif, system-ui, sans-serif',
      Inter: '"Inter", ui-sans-serif, system-ui, sans-serif',
    };
    root.style.setProperty("--erp-font-family", fonts[branding.fontFamily] ?? fonts.Montserrat);
    document.title = `${branding.displayName} — HR workspace`;
    const icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (icon) icon.href = branding.faviconDataUri || "/favicon.svg";
  }, [branding, pathname]);

  return <BrandingContext.Provider value={{ branding, ready, setBranding }}>{children}</BrandingContext.Provider>;
}

export function useBranding() {
  const value = useContext(BrandingContext);
  if (!value) throw new Error("useBranding requires BrandingProvider");
  return value;
}
