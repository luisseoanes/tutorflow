import type { ReactElement, ReactNode, Ref } from "react";
import { Icon } from "./Icon";
import type { IconName } from "./Icon";

interface CardProps {
  children: ReactNode;
  tone?: "default" | "surface";
  as?: "div" | "section" | "article";
  className?: string;
}

export function Card({ children, tone = "default", as = "div", className }: CardProps): ReactElement {
  const Tag = as;
  return <Tag className={`card card--${tone}${className ? ` ${className}` : ""}`}>{children}</Tag>;
}

interface BadgeProps {
  children: ReactNode;
  tone?: "neutral" | "accent" | "danger" | "warning" | "success";
}

export function Badge({ children, tone = "neutral" }: BadgeProps): ReactElement {
  return <span className={`badge badge--${tone}`}>{children}</span>;
}

export type AlertVariant = "info" | "warning" | "danger" | "success";

const alertIcon: Record<AlertVariant, IconName> = {
  info: "info",
  warning: "alert",
  danger: "alert",
  success: "check",
};

interface AlertProps {
  variant: AlertVariant;
  title: string;
  children?: ReactNode;
  actions?: ReactNode;
  /** `alert` para errores (anuncio inmediato); `status` para avisos informativos. */
  role?: "alert" | "status";
  alertRef?: Ref<HTMLDivElement>;
}

export function Alert({ variant, title, children, actions, role, alertRef }: AlertProps): ReactElement {
  const effectiveRole = role ?? (variant === "info" || variant === "success" ? "status" : "alert");
  return (
    <div className={`alert alert--${variant}`} role={effectiveRole} ref={alertRef} tabIndex={-1}>
      <span className="alert__icon">
        <Icon name={alertIcon[variant]} />
      </span>
      <div className="alert__body">
        <p className="alert__title">{title}</p>
        {children ? <div className="alert__text">{children}</div> : null}
        {actions ? <div className="alert__actions">{actions}</div> : null}
      </div>
    </div>
  );
}

export type ApiStatus = "checking" | "ok" | "down";

const statusText: Record<ApiStatus, string> = {
  checking: "Comprobando API",
  ok: "Disponible",
  down: "Sin conexión",
};

const statusIcon: Record<ApiStatus, IconName> = { checking: "info", ok: "check", down: "offline" };

export function StatusIndicator({ status }: { status: ApiStatus }): ReactElement {
  return (
    <p className={`status status--${status}`} role="status">
      <Icon name={statusIcon[status]} size="sm" />
      <span>
        <span className="status__prefix">API · </span>
        {statusText[status]}
      </span>
    </p>
  );
}
