import { describe, expect, it } from "vitest";
import { archivosFuente, leerFuente } from "./sources";
import { tokens } from "../src/tokens/tokens";

function variablesCss(css: string): Map<string, string> {
  const mapa = new Map<string, string>();
  const bloque = /:root\s*\{([\s\S]*?)\n\}/.exec(css)?.[1] ?? "";
  for (const m of bloque.matchAll(/(--[a-z0-9-]+):\s*([^;]+);/g)) {
    const nombre = m[1];
    const valor = m[2];
    if (nombre !== undefined && valor !== undefined) mapa.set(nombre, valor.trim());
  }
  return mapa;
}

const kebab = (s: string): string => s.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`);
const tokensCss = leerFuente("tokens/tokens.css");
const css = variablesCss(tokensCss);

// Valores normativos copiados de SPEC-WEB §3 (no derivados del código bajo prueba).
const ESPERADOS: Record<string, string> = {
  "--color-bg": "#FFFFFF",
  "--color-surface": "#F8FAFC",
  "--color-text": "#0F172A",
  "--color-text-secondary": "#475569",
  "--color-text-tertiary": "#64748B",
  "--color-border": "#E2E8F0",
  "--color-border-control": "#858FA1",
  "--color-accent": "#3451D1",
  "--color-focus": "#3451D1",
  "--color-danger": "#B42318",
  "--color-danger-bg": "#FEF3F2",
  "--color-warning": "#B54708",
  "--color-warning-bg": "#FFFAEB",
  "--color-success": "#067647",
  "--color-success-bg": "#ECFDF3",
  "--font-size-xs": "0.8125rem",
  "--font-size-sm": "0.875rem",
  "--font-size-base": "1rem",
  "--font-size-lg": "1.125rem",
  "--font-size-xl": "1.375rem",
  "--font-size-2xl": "1.75rem",
  "--font-weight-regular": "400",
  "--font-weight-medium": "500",
  "--font-weight-semibold": "600",
  "--line-height-body": "1.6",
  "--line-height-heading": "1.25",
  "--measure-answer": "68ch",
  "--space-1": "4px",
  "--space-2": "8px",
  "--space-3": "12px",
  "--space-4": "16px",
  "--space-5": "20px",
  "--space-6": "24px",
  "--space-8": "32px",
  "--space-10": "40px",
  "--space-12": "48px",
  "--space-16": "64px",
  "--radius-sm": "6px",
  "--radius-md": "8px",
  "--radius-lg": "12px",
  "--border-width": "1px",
  "--shadow-sm": "0 1px 2px rgb(15 23 42 / 0.05)",
  "--shadow-md": "0 4px 12px rgb(15 23 42 / 0.06)",
  "--motion-fast": "120ms",
  "--motion-base": "180ms",
  "--motion-ease": "cubic-bezier(0.2, 0, 0, 1)",
  "--breakpoint-sm": "640px",
  "--breakpoint-md": "768px",
  "--breakpoint-lg": "1024px",
  "--breakpoint-xl": "1280px",
};

describe("tokens de diseño (SPEC-WEB §3)", () => {
  // Criterio: WEB-15.a
  it("tokens.css contiene exactamente los valores normativos de §3.1–§3.3", () => {
    for (const [nombre, valor] of Object.entries(ESPERADOS)) {
      expect(css.get(nombre), nombre).toBe(valor);
    }
  });

  // Criterio: WEB-15.a
  it("el espejo tipado tokens.ts coincide con tokens.css", () => {
    const pares: [string, string | number][] = [];
    for (const [grupo, valores] of Object.entries(tokens)) {
      if (typeof valores === "object") {
        for (const [clave, valor] of Object.entries(valores)) {
          pares.push([`--${kebab(grupo)}-${kebab(clave)}`, valor as string | number]);
        }
      } else {
        pares.push([`--${kebab(grupo)}`, valores]);
      }
    }
    expect(pares.length).toBeGreaterThan(40);
    for (const [nombre, valor] of pares) {
      expect(css.get(nombre), nombre).toBe(String(valor));
    }
  });

  // Criterio: WEB-12.c
  it("con prefers-reduced-motion: reduce los tokens de movimiento son 0 ms y se anulan transiciones y animaciones", () => {
    const bloque = /@media \(prefers-reduced-motion: reduce\)\s*\{([\s\S]*)\n\}/.exec(tokensCss)?.[1] ?? "";
    expect(bloque).toContain("--motion-fast: 0ms");
    expect(bloque).toContain("--motion-base: 0ms");
    expect(bloque).toMatch(/transition-duration:\s*0ms\s*!important/);
    expect(bloque).toMatch(/animation-duration:\s*0ms\s*!important/);
  });

  // Criterio: WEB-12.c
  it("ninguna hoja define transiciones con duración fija fuera de los tokens de movimiento", () => {
    const hojas = archivosFuente().filter(([ruta]) => ruta.startsWith("src/styles/"));
    expect(hojas.length).toBe(3);
    for (const [ruta, contenido] of hojas) {
      const fijas = contenido.split("\n").filter((l) => /(transition|animation)[^;]*\b\d+(\.\d+)?m?s\b/.test(l));
      expect(fijas, ruta).toEqual([]);
    }
  });

  it("los breakpoints en em de las media queries equivalen a los tokens (40em, 48em, 64em, 80em)", () => {
    expect(Number.parseInt(String(tokens.breakpoint.sm), 10) / 16).toBe(40);
    expect(Number.parseInt(String(tokens.breakpoint.md), 10) / 16).toBe(48);
    expect(Number.parseInt(String(tokens.breakpoint.lg), 10) / 16).toBe(64);
    expect(Number.parseInt(String(tokens.breakpoint.xl), 10) / 16).toBe(80);
  });
});
