import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  Outlet,
  Link,
  createRootRouteWithContext,
  useRouter,
  HeadContent,
  Scripts,
} from "@tanstack/react-router";
import { useEffect, useRef, type ReactNode } from "react";
import { Toaster } from "@/components/ui/sonner";

import appCss from "../styles.css?url";
import { reportLovableError } from "../lib/lovable-error-reporting";
import { TimesheetBoundary } from "../platform/components/TimesheetWorkspace";
import { AppProvider } from "../platform/app-context";
import { AuthProvider, useAuth } from "../platform/auth";
import { BrandingProvider, useBranding } from "@/platform/branding";

function PreloaderController() {
  const { loading } = useAuth();
  const { branding, ready } = useBranding();
  const startedAt = useRef(Date.now());

  useEffect(() => {
    const image = document.getElementById("erp-preloader-logo") as HTMLImageElement | null;
    const fallback = document.getElementById("erp-preloader-fallback");
    const logo = branding?.logoDarkDataUri || branding?.logoLightDataUri;
    if (image && fallback && logo?.startsWith("data:image/")) {
      image.src = logo;
      image.hidden = false;
      fallback.hidden = true;
    }
  }, [branding]);

  useEffect(() => {
    if (loading || !ready) return;
    const overlay = document.getElementById("erp-global-preloader");
    if (!overlay) return;
    const delay = Math.max(0, 450 - (Date.now() - startedAt.current));
    const timer = window.setTimeout(() => {
      overlay.classList.add("is-leaving");
      window.setTimeout(() => overlay.classList.add("is-gone"), 280);
    }, delay);
    return () => window.clearTimeout(timer);
  }, [loading, ready]);
  return null;
}

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
      { title: "HR workspace" },
      { name: "description", content: "HR workspace for leave, attendance, requests and pay." },
      { property: "og:title", content: "HR workspace" },
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
        href: "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=Montserrat:wght@400;500;600;700&family=Plus+Jakarta+Sans:wght@400;500;600;700&family=IBM+Plex+Mono:wght@400;500&display=swap",
      },
      { rel: "icon", href: "/favicon.svg", type: "image/svg+xml" },
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
        <div id="erp-global-preloader" className="erp-global-preloader" role="status" aria-label="Loading ERP">
          <div className="erp-preloader-orbit" aria-hidden="true">
            <span id="erp-preloader-fallback" className="erp-preloader-fallback">ERP</span>
            <img id="erp-preloader-logo" className="erp-preloader-logo" alt="" hidden />
          </div>
        </div>
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
        <BrandingProvider>
          <PreloaderController />
          <AppProvider>
            {/* Required: nested routes render here. Removing <Outlet /> breaks all child routes. */}
            <TimesheetBoundary><Outlet /></TimesheetBoundary>
          </AppProvider>
        </BrandingProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
