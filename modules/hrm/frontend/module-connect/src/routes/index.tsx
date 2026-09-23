import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, Clock } from "lucide-react";
import { modules } from "@/platform/modules";
import { BrandIdentity } from "@/platform/components/BrandIdentity";

export const Route = createFileRoute("/")({
  head: () => ({
    meta: [
      { title: "HR workspace" },
      { name: "description", content: "Choose a module to enter." },
      { property: "og:title", content: "HR workspace" },
      { property: "og:description", content: "Choose a module to enter." },
    ],
  }),
  component: Entrance,
});

function Entrance() {
  return (
    <div className="min-h-screen bg-background">
      <header className="border-b border-primary/40 bg-primary text-primary-foreground shadow-sm">
        <div className="mx-auto flex h-14 max-w-5xl items-center gap-2 px-4">
          <BrandIdentity onDark />
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-12">
        <h1 className="text-2xl font-semibold text-foreground">Choose a module</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Modules are isolated workspaces inside the same ERP. Pick one to enter.
        </p>

        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {modules.map((m) =>
            m.available ? (
              <Link
                key={m.id}
                to={m.to}
                className="group flex flex-col gap-2 rounded-lg border bg-surface p-5 transition-colors hover:border-primary hover:bg-primary-soft"
              >
                <div className="flex items-start justify-between gap-3">
                  <span className="text-base font-semibold text-foreground">{m.label}</span>
                  <ArrowRight
                    className="mt-0.5 size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-primary"
                    aria-hidden
                  />
                </div>
                <p className="text-sm text-muted-foreground">{m.description}</p>
              </Link>
            ) : (
              <div
                key={m.id}
                className="flex cursor-not-allowed flex-col gap-2 rounded-lg border p-5 opacity-60"
                aria-disabled="true"
                title={`${m.label} — not enabled`}
              >
                <div className="flex items-start justify-between gap-3">
                  <span className="text-base font-semibold text-foreground">{m.label}</span>
                  <span className="inline-flex shrink-0 items-center gap-1 rounded-full border border-info/30 bg-info-soft px-2 py-0.5 text-[10px] font-medium text-info">
                    <Clock className="size-3" aria-hidden />
                    Not enabled
                  </span>
                </div>
                <p className="text-sm text-muted-foreground">{m.description}</p>
              </div>
            ),
          )}
        </div>
      </main>
    </div>
  );
}
