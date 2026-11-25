import { test, expect } from "@playwright/test";
import { appendFileSync } from "node:fs";

test("sondeo de salud cada 30 s y estado Sin conexion si cae", async ({ page }) => {
  let calls = 0;
  let up = true;
  await page.clock.install();
  await page.route("**/api/salud", (r) => {
    calls++;
    return up ? r.fulfill({ status: 200, contentType: "application/json", body: '{"estado":"ok"}' }) : r.abort("connectionrefused");
  });
  await page.goto("/");
  await expect(page.getByRole("status").filter({ hasText: "Disponible" })).toBeVisible();
  const c0 = calls;
  await page.clock.runFor(29_000);
  await page.waitForTimeout(500);
  const c1 = calls;
  await page.clock.runFor(2_000);
  await page.waitForTimeout(500);
  const c2 = calls;
  up = false;
  await page.clock.runFor(31_000);
  await expect(page.getByRole("status").filter({ hasText: "Sin conexión" })).toBeVisible();
  const msg = `sondeo: inicial=${c0} a los 29s=${c1} a los 31s=${c2} tras caida: Sin conexion visible`;
  console.log(msg);
  appendFileSync("../evidencias/T-13-r3/salida-verificaciones.txt", msg + "\n");
  expect(c1).toBe(c0);
  expect(c2).toBe(c0 + 1);
});
