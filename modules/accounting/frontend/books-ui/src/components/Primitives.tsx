import { useEffect } from "react";
import { X } from "lucide-react";
import type { ButtonHTMLAttributes, ReactNode } from "react";
import type { DemoStatus } from "../types";

export function Button({
  variant = "primary",
  size = "md",
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  size?: "sm" | "md";
}) {
  return (
    <button
      className={`button button-${variant} button-${size} ${className}`.trim()}
      {...props}
    />
  );
}

export function PageHeading({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        {eyebrow && <div className="eyebrow">{eyebrow}</div>}
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="heading-actions">{actions}</div>}
    </div>
  );
}

const statusTone: Record<DemoStatus, string> = {
  Draft: "neutral",
  Sent: "blue",
  Paid: "green",
  Overdue: "red",
  Open: "blue",
  Approved: "green",
  Active: "green",
  Recorded: "green",
  Pending: "amber",
  Accepted: "green",
  Declined: "red",
  Converted: "blue",
  Void: "neutral",
  "Partially Paid": "amber",
  Applied: "green",
  Refunded: "blue",
  Paused: "amber",
  Completed: "green",
  Published: "green",
  Reversed: "neutral",
  Inactive: "neutral",
  Billed: "green",
};

export function StatusPill({ status }: { status: DemoStatus }) {
  return <span className={`status-pill status-${statusTone[status]}`}>{status}</span>;
}

export function Panel({
  title,
  action,
  children,
  className = "",
}: {
  title?: string;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`panel ${className}`.trim()}>
      {(title || action) && (
        <div className="panel-heading">
          {title && <h2>{title}</h2>}
          {action}
        </div>
      )}
      {children}
    </section>
  );
}

export function Notice({
  children,
  tone = "blue",
}: {
  children: ReactNode;
  tone?: "blue" | "amber" | "green";
}) {
  return <div className={`notice notice-${tone}`}>{children}</div>;
}

export function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon?: ReactNode;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <div className="empty-icon">{icon}</div>
      <h2>{title}</h2>
      <p>{description}</p>
      {action && <div className="empty-action">{action}</div>}
    </div>
  );
}

export function Modal({
  open,
  title,
  description,
  onClose,
  children,
  footer,
  width = "md",
}: {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children?: ReactNode;
  footer?: ReactNode;
  width?: "sm" | "md" | "lg";
}) {
  useEffect(() => {
    if (!open) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", closeOnEscape);
    return () => window.removeEventListener("keydown", closeOnEscape);
  }, [open, onClose]);

  if (!open) return null;
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget) onClose();
    }}>
      <section className={`modal modal-${width}`} role="dialog" aria-modal="true" aria-labelledby="modal-title">
        <header className="modal-header">
          <div>
            <h2 id="modal-title">{title}</h2>
            {description && <p>{description}</p>}
          </div>
          <button className="icon-button" aria-label="Close dialog" onClick={onClose}>
            <X size={18} />
          </button>
        </header>
        {children && <div className="modal-body">{children}</div>}
        {footer && <footer className="modal-footer">{footer}</footer>}
      </section>
    </div>
  );
}
