/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Cabeceras de SPEC-WEB §9. La misma política la aplica nginx en el contenedor (web/nginx.conf);
// aquí se replica para `vite preview`, de modo que las pruebas e2e las verifiquen también sin Docker.
const csp = [
  "default-src 'self'",
  "script-src 'self'",
  "style-src 'self'",
  "img-src 'self' data:",
  "font-src 'self'",
  "connect-src 'self'",
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
].join("; ");

const securityHeaders = {
  "Content-Security-Policy": csp,
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "no-referrer",
};

// Mismo origen: el servidor estático hace de proxy de /api (SPEC-WEB §9). Sin CORS.
const apiTarget = process.env["API_PROXY_TARGET"] ?? "http://localhost:8080";

export default defineConfig({
  plugins: [react()],
  build: {
    target: "es2022",
    sourcemap: false,
    // Sin recursos en línea (data:): la fuente se sirve como archivo del mismo origen (WEB-16.b).
    assetsInlineLimit: 0,
  },
  server: { proxy: { "/api": { target: apiTarget, changeOrigin: false } } },
  preview: {
    headers: securityHeaders,
    proxy: { "/api": { target: apiTarget, changeOrigin: false } },
  },
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.{ts,tsx}"],
    setupFiles: ["tests/setup.ts"],
    restoreMocks: true,
  },
});
