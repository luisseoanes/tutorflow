import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: ".",
  testMatch: /.*\.qa\.ts/,
  timeout: 90_000,
  workers: 1,
  reporter: [["list"]],
  use: { baseURL: process.env.QA_BASE_URL ?? "http://localhost:13000", browserName: "chromium", locale: "es-CO" },
});
