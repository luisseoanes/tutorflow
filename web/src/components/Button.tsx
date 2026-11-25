import type { ButtonHTMLAttributes, ReactElement, ReactNode } from "react";
import { Spinner } from "./Spinner";

export type ButtonVariant = "primary" | "secondary" | "ghost";

interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "type"> {
  variant?: ButtonVariant;
  loading?: boolean;
  type?: "button" | "submit";
  children: ReactNode;
}

export function Button({
  variant = "primary",
  loading = false,
  type = "button",
  disabled,
  className,
  children,
  ...rest
}: ButtonProps): ReactElement {
  const classes = ["btn", `btn--${variant}`, loading ? "btn--loading" : "", className ?? ""]
    .filter((c) => c.length > 0)
    .join(" ");
  return (
    <button type={type} className={classes} disabled={disabled === true || loading} aria-busy={loading || undefined} {...rest}>
      {loading ? <Spinner /> : null}
      <span>{children}</span>
    </button>
  );
}
