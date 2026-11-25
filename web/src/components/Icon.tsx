import type { ReactElement } from "react";

/** Iconos SVG inline propios (sin emojis, sin set externo). Decorativos: aria-hidden. */
export type IconName =
  | "logo"
  | "check"
  | "alert"
  | "info"
  | "offline"
  | "chevron"
  | "send"
  | "lock"
  | "doc"
  | "retry";

const paths: Record<IconName, ReactElement> = {
  logo: <path d="M6 7h12M12 7v11" />,
  check: <path d="M5 12.5l4.5 4.5L19 7.5" />,
  alert: (
    <>
      <path d="M12 8v5" />
      <path d="M12 16.5v.01" />
      <path d="M10.3 4.3L2.9 17.2A2 2 0 0 0 4.6 20h14.8a2 2 0 0 0 1.7-2.8L13.7 4.3a2 2 0 0 0-3.4 0z" />
    </>
  ),
  info: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5" />
      <path d="M12 7.5v.01" />
    </>
  ),
  offline: (
    <>
      <path d="M3 3l18 18" />
      <path d="M8.5 16.4a5 5 0 0 1 7 0" />
      <path d="M5 12.9a10 10 0 0 1 3.4-2" />
      <path d="M12 20h.01" />
    </>
  ),
  chevron: <path d="M7 10l5 5 5-5" />,
  send: (
    <>
      <path d="M12 19V5" />
      <path d="M6 11l6-6 6 6" />
    </>
  ),
  lock: (
    <>
      <rect x="5" y="11" width="14" height="9" rx="2" />
      <path d="M8 11V8a4 4 0 0 1 8 0v3" />
    </>
  ),
  doc: (
    <>
      <path d="M7 3h7l4 4v14H7z" />
      <path d="M14 3v4h4" />
    </>
  ),
  retry: (
    <>
      <path d="M20 12a8 8 0 1 1-2.3-5.7" />
      <path d="M20 4v5h-5" />
    </>
  ),
};

interface IconProps {
  name: IconName;
  size?: "sm" | "md" | "lg";
}

export function Icon({ name, size = "md" }: IconProps): ReactElement {
  return (
    <svg
      className={`icon icon--${size}`}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {paths[name]}
    </svg>
  );
}
