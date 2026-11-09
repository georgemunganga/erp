import { useEffect, useRef, useState } from "react";
import { PackagePlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/platform/auth";
import { isSessionValid } from "@/platform/oidc";
import { useApp } from "@/platform/app-context";
import { mayCreateMaterialRequest } from "@/platform/material-request-access";

/** Shared ERP entry point into the existing Procurement-owned request workflow. */
export function MaterialRequestShortcut() {
  const { authenticated, user, coreWorkspace, session } = useAuth();
  const { entityId, hydrated } = useApp();
  const companyId = entityId
    ? coreWorkspace?.companies.find(company => company.id === entityId)?.id
    : (coreWorkspace?.companies.find(company => company.isDefault) ?? coreWorkspace?.companies[0])?.id;
  const token = isSessionValid(session) ? session?.accessToken : undefined;
  const key = JSON.stringify([authenticated, user?.subject, user?.roles, companyId, hydrated, session?.expiresAt]);
  const current = useRef(key); current.current = key;
  const [grant, setGrant] = useState({ key: "", allowed: false });
  const real = import.meta.env.VITE_USE_REAL_API === "true";
  useEffect(() => {
    if (!real || !authenticated || !user?.subject || !companyId || !hydrated) return;
    const controller = new AbortController();
    const captured = key;
    setGrant({ key: captured, allowed: false });
    const headers: Record<string, string> = { "X-Shell-Entity": companyId, "X-Shell-Branch-Scope": "all" };
    if (token) headers.Authorization = `Bearer ${token}`;
    Promise.all(["context", "meta"].map(path => fetch(`/api/procurement/v1/${path}`, {
      credentials: "include", cache: "no-store", headers, signal: controller.signal,
    }).then(response => response.ok ? response.json() : null)))
      .then(([context, meta]) => {
        if (!controller.signal.aborted && current.current === captured)
          setGrant({ key: captured, allowed: mayCreateMaterialRequest(context, user.subject, companyId)
            && meta?.capabilities?.requestDraft === true });
      }).catch(() => { /* An unavailable permission lookup never grants access. */ });
    return () => controller.abort();
  }, [real, authenticated, user?.subject, companyId, hydrated, key, token]);
  if (!real || !authenticated || grant.key !== key || !grant.allowed) return null;
  return <section aria-label="Material and service requests" className="my-6 flex flex-wrap items-center justify-between gap-4 rounded-xl border border-border bg-card p-5">
    <div className="min-w-0"><p className="text-base font-semibold">Need materials or a service?</p><p className="mt-1 text-sm text-muted-foreground">Create an internal request and follow its progress in My Requests.</p></div>
    <div className="flex flex-wrap items-center gap-3"><Button asChild><a href="/procurement/workspace/requests/new?entry=department"><PackagePlus className="size-4" aria-hidden />Request materials or services</a></Button><a href="/procurement/workspace/requests" className="text-sm font-medium text-primary hover:underline">My Requests</a></div>
  </section>;
}
