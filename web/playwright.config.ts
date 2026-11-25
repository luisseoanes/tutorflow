import { defineConfig, devices } from "@playwright/test";

/**
 * Pruebas [e2e] (escenarios/web.md). Dos formas de ejecutarlas:
 *  - Contra el servicio `web` ya levantado (compose, :3000):  WEB_BASE_URL=http://web:3000 npx playwright test
 *  - Sin Docker (desarrollo): sin WEB_BASE_URL, Playwright compila el bundle y lo sirve con `vite preview` en :3000,
 *    con las mismas cabeceras y proxy de /api hacia API_PROXY_TARGET (por defecto http://localhost:8080).
 * Las pruebas contra la API real exigen la API SIN llave (LLM_API_KEY vacía): nunca consumen cuota del LLM.
 */
const baseURL = process.env["WEB_BASE_URL"] ?? "http://localhost:3000";

export default defineConfig({
  testDir: "./e2e",
  testIgnore: "**/evidencias/**",
  outputDir: "./node_modules/.playwright-results",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 30_000,
  expect: { timeout: 7_500 },
  reporter: [["list"]],
  use: {
    baseURL,
    viewport: { width: 1440, height: 900 },
    locale: "es-CO",
    trace: "off",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } } }],
  webServer: process.env["WEB_BASE_URL"]
    ? undefined
    : {
        command: "npx vite build && npx vite preview --port 3000 --strictPort",
        url: "http://localhost:3000/",
        reuseExistingServer: true,
        timeout: 120_000,
      },
});
