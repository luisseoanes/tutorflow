import { defineConfig, devices } from "@playwright/test";

/**
 * Capturas de evidencia (AGENTS.md §13.3): estados de SPEC-WEB §4.3 en 375x812, 768x1024 y 1440x900.
 * Uso:  WEB_BASE_URL=http://localhost:3000 EVIDENCIAS_DIR=evidencias/T-13-r1 npm run evidencias
 * Requiere el servicio `web` y la API SIN llave (los estados 400/403/503 son reales; 200/500/red/inesperada, simulados).
 */
const baseURL = process.env["WEB_BASE_URL"] ?? "http://localhost:3000";

export default defineConfig({
  testDir: "./e2e/evidencias",
  outputDir: "./node_modules/.playwright-results-evidencias",
  workers: 1,
  retries: 0,
  timeout: 60_000,
  reporter: [["list"]],
  use: { baseURL, locale: "es-CO", trace: "off" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
