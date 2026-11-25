import { expect, test } from "@playwright/test";
import { axeGrave, escribirYEnviar, vigilarConsola } from "./support";

/**
 * [e2e] contra la API REAL **sin llave** (LLM_API_KEY vacía): salud, 400, 403 y 503. Nunca consumen cuota del LLM.
 * Requiere la API detrás de /api del mismo origen (servicio `web` de compose, o `vite preview` con proxy).
 * Los ids de abajo son los del conjunto de datos de pruebas (escenarios/academic-data.md), solo en pruebas.
 */

const PREGUNTA_AUTH_03 = "¿Cuál es la nota de Bases de Datos de Andrés Martínez?";

test.describe("API real sin llave", () => {
  // Criterio: WEB-16.a
  test("el servicio web responde en el origen con CSP estricta, nosniff y no-referrer, y hace proxy de /api", async ({ request }) => {
    for (const ruta of ["/", "/api/salud"]) {
      const r = await request.get(ruta);
      expect(r.status(), ruta).toBe(200);
      const h = r.headers();
      const csp = h["content-security-policy"] ?? "";
      expect(csp, ruta).toContain("default-src 'self'");
      const scriptSrc = csp.split(";").map((d) => d.trim()).find((d) => d.startsWith("script-src")) ?? "";
      expect(scriptSrc).not.toContain("unsafe-inline");
      expect(scriptSrc).not.toContain("unsafe-eval");
      expect(csp).not.toMatch(/https?:\/\//);
      expect(h["x-content-type-options"], ruta).toBe("nosniff");
      expect(h["referrer-policy"], ruta).toBe("no-referrer");
    }
    const salud = await request.get("/api/salud");
    expect(await salud.json()).toEqual({ estado: "ok" });
  });

  // Criterio: WEB-01.b
  test("sin CORS: la respuesta de la API no declara cabeceras de CORS y la UI funciona en el mismo origen", async ({ page, request }) => {
    const r = await request.get("/api/salud");
    expect(Object.keys(r.headers()).filter((k) => k.startsWith("access-control-"))).toEqual([]);
    await page.goto("/");
    await expect(page.getByText("Disponible")).toBeVisible();
  });

  // Criterio: WEB-09.a
  test("salud real: el indicador muestra Disponible", async ({ page }) => {
    await page.goto("/");
    const estado = page.getByRole("status").filter({ hasText: "Disponible" });
    await expect(estado).toBeVisible();
    await expect(estado.locator("svg")).toHaveCount(1);
  });

  // Criterio: WEB-07.e
  test("AUTH-03 (nombre de otro estudiante) -> la UI muestra el 403-acceso del contrato", async ({ page }) => {
    const consola = vigilarConsola(page);
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_AUTH_03, "EST-001");
    const alerta = page.getByRole("alert");
    await expect(alerta.getByText("Acceso denegado")).toBeVisible();
    await expect(alerta).toContainText("No tienes permiso para consultar esa información. Solo puedes consultar tus propios datos académicos.");
    await expect(alerta.getByRole("button", { name: "reglamento_academico_2025.md · Artículo 22" })).toBeVisible();
    expect(await axeGrave(page)).toEqual([]);
    expect(consola.mensajes.filter((m) => !m.includes("Failed to load resource"))).toEqual([]);
  });

  // Criterio: WEB-07.e
  test("rol válido con id inexistente -> la UI muestra el 403-identidad (sin cita del Art. 22)", async ({ page }) => {
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_AUTH_03, "EST-999", "estudiante");
    const alerta = page.getByRole("alert");
    await expect(alerta.getByText("Identidad no válida")).toBeVisible();
    await expect(alerta).toContainText("No fue posible validar la identidad indicada en la solicitud.");
    await expect(alerta.getByRole("button")).toHaveCount(0);
    await expect(page.getByText("Acceso denegado")).toHaveCount(0);
  });

  // Criterio: WEB-07.e
  test("pregunta de 1001 caracteres forzada (saltando la UX) -> la UI muestra el 400", async ({ page }) => {
    // La UI impide enviar >1000: se envía una pregunta válida y se reescribe el cuerpo en tránsito.
    await page.route("**/api/asistente/preguntar", async (route) => {
      const cuerpo = JSON.parse(route.request().postData() ?? "{}") as { pregunta: string; usuario: unknown };
      await route.continue({ postData: JSON.stringify({ ...cuerpo, pregunta: "a".repeat(1001) }) });
    });
    await page.goto("/");
    await escribirYEnviar(page, "pregunta corta", "EST-001");
    const alerta = page.getByRole("alert");
    await expect(alerta.getByText("La solicitud no es válida")).toBeVisible();
    await expect(alerta).not.toContainText("a".repeat(20));
    expect(await axeGrave(page)).toEqual([]);
  });

  // Criterio: WEB-07.e
  test("pregunta normativa sin llave del LLM -> la UI muestra el 503 con reintento", async ({ page }) => {
    await page.goto("/");
    await escribirYEnviar(page, "¿Cuál es el máximo de faltas permitido según el reglamento?", "EST-001");
    const alerta = page.getByRole("alert");
    await expect(alerta.getByText("Servicio no disponible temporalmente")).toBeVisible();
    await expect(alerta.getByRole("button", { name: "Reintentar" })).toBeVisible();
    expect(await axeGrave(page)).toEqual([]);
  });
});
