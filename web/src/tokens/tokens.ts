/**
 * Espejo tipado de tokens.css (SPEC-WEB §3). Los valores deben coincidir exactamente con la hoja
 * de estilos; tests/tokens.test.ts lo verifica. La interfaz no calcula nada con ellos: solo los nombra.
 */
export const tokens = {
  color: {
    bg: "#FFFFFF",
    surface: "#F8FAFC",
    text: "#0F172A",
    textSecondary: "#475569",
    textTertiary: "#64748B",
    border: "#E2E8F0",
    borderControl: "#858FA1",
    accent: "#3451D1",
    focus: "#3451D1",
    danger: "#B42318",
    dangerBg: "#FEF3F2",
    warning: "#B54708",
    warningBg: "#FFFAEB",
    success: "#067647",
    successBg: "#ECFDF3",
  },
  fontSize: {
    xs: "0.8125rem",
    sm: "0.875rem",
    base: "1rem",
    lg: "1.125rem",
    xl: "1.375rem",
    "2xl": "1.75rem",
  },
  fontWeight: { regular: 400, medium: 500, semibold: 600 },
  lineHeight: { body: 1.6, heading: 1.25 },
  space: {
    1: "4px",
    2: "8px",
    3: "12px",
    4: "16px",
    5: "20px",
    6: "24px",
    8: "32px",
    10: "40px",
    12: "48px",
    16: "64px",
  },
  radius: { sm: "6px", md: "8px", lg: "12px" },
  borderWidth: "1px",
  shadow: {
    sm: "0 1px 2px rgb(15 23 42 / 0.05)",
    md: "0 4px 12px rgb(15 23 42 / 0.06)",
  },
  motion: { fast: "120ms", base: "180ms", ease: "cubic-bezier(0.2, 0, 0, 1)" },
  breakpoint: { sm: "640px", md: "768px", lg: "1024px", xl: "1280px" },
} as const;

/** Nombres de las variables CSS (para pruebas y documentación; los estilos usan var(--…) directamente). */
export type Tokens = typeof tokens;
