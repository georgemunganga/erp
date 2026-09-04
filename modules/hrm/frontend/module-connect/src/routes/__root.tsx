import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  Outlet,
  Link,
  createRootRouteWithContext,
  useRouter,
  HeadContent,
  Scripts,
} from "@tanstack/react-router";
import { useEffect, type ReactNode } from "react";
import { Toaster } from "@/components/ui/sonner";

import appCss from "../styles.css?url";
import { reportLovableError } from "../lib/lovable-error-reporting";
import { AppProvider } from "../platform/app-context";
import { AuthProvider, useAuth } from "../platform/auth";
import { realApi } from "@/platform/use-api";

function NotFoundComponent() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-7xl font-bold text-foreground">404</h1>
        <h2 className="mt-4 text-xl font-semibold text-foreground">Page not found</h2>
        <p className="mt-2 text-sm text-muted-foreground">
          The page you're looking for doesn't exist or has been moved.
        </p>
        <div className="mt-6">
          <Link
            to="/"
            className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          >
            Go home
          </Link>
        </div>
      </div>
    </div>
  );
}

function ErrorComponent({ error, reset }: { error: Error; reset: () => void }) {
  console.error(error);
  if (typeof window !== "undefined") {
    (window as unknown as Record<string, unknown>).__pageLoadError = {
      message: error?.message ?? String(error),
      stack: error?.stack,
      route: window.location.pathname,
    };
  }
  const router = useRouter();
  useEffect(() => {
    reportLovableError(error, { boundary: "tanstack_root_error_component" });
  }, [error]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-xl font-semibold tracking-tight text-foreground">
          This page didn't load
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Something went wrong on our end. You can try refreshing or head back home.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-2">
          <button
            onClick={() => {
              router.invalidate();
              reset();
            }}
            className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
          >
            Try again
          </button>
          <a
            href="/"
            className="inline-flex items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium text-foreground transition-colors hover:bg-accent"
          >
            Go home
          </a>
        </div>
      </div>
    </div>
  );
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  head: () => ({
    meta: [
      { charSet: "utf-8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      // Product identity is Mightyfin; employer data remains tenant-scoped and configurable.
      { title: "Mightyfin HRMS — HR workspace" },
      { name: "description", content: "Mightyfin HRMS workspace for leave, attendance, requests and pay." },
      { name: "author", content: "Mightyfin" },
      { property: "og:title", content: "Mightyfin HRMS — HR workspace" },
      { property: "og:description", content: "HR operations workspace for leave, attendance, requests and pay." },
      { property: "og:type", content: "website" },
      { name: "twitter:card", content: "summary_large_image" },
    ],
    links: [
      {
        rel: "stylesheet",
        href: appCss,
      },
      { rel: "preconnect", href: "https://fonts.googleapis.com" },
      { rel: "preconnect", href: "https://fonts.gstatic.com", crossOrigin: "anonymous" },
      {
        rel: "stylesheet",
        href: "https://fonts.googleapis.com/css2?family=Montserrat:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500&display=swap",
      },
      { rel: "icon", href: "/mightyfin-mark.png", type: "image/png" },
      { rel: "icon", href: "/favicon.ico", type: "image/x-icon" },
    ],
  }),
  shellComponent: RootShell,
  component: RootComponent,
  notFoundComponent: NotFoundComponent,
  errorComponent: ErrorComponent,
});

function RootShell({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <head>
        <HeadContent />
      </head>
      <body>
        {children}
        <Toaster position="bottom-right" closeButton richColors />
        <Scripts />
      </body>
    </html>
  );
}

function RootComponent() {
  const { queryClient } = Route.useRouteContext();

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TenantBranding>
          <AppProvider>
            {/* Required: nested routes render here. Removing <Outlet /> breaks all child routes. */}
            <Outlet />
          </AppProvider>
        </TenantBranding>
      </AuthProvider>
    </QueryClientProvider>
  );
}

/** Applies only the tenant's approved semantic tokens after authentication.
 * The sign-in/IdP screen intentionally retains the platform identity. */
function TenantBranding({ children }: { children: ReactNode }) {
  const { authenticated } = useAuth();
  useEffect(() => {
    if (!authenticated || import.meta.env.VITE_USE_REAL_API !== "true") return;
    let active = true;
    realApi.branding().then((brand) => {
      if (!active) return;
      const root = document.documentElement;
      root.style.setProperty("--primary", brand.primaryColor);
      root.style.setProperty("--secondary", brand.secondaryColor);
      root.style.setProperty("--accent", brand.accentColor);
      root.style.setProperty("--rail", brand.railColor);
      root.style.setProperty("--ring", brand.primaryColor);
      document.title = `${brand.displayName} — HR workspace`;
      if (brand.faviconDataUri) {
        const icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
        if (icon) icon.href = brand.faviconDataUri;
      }
      const logo = brand.logoLightDataUri;
      if (logo) document.querySelectorAll<HTMLImageElement>('img[data-company-logo="light"]').forEach((image) => { image.src = logo; image.alt = brand.displayName; });
    }).catch(() => { /* Branding must never block the workforce shell. */ });
    return () => { active = false; };
  }, [authenticated]);
  return <>{children}</>;
}
