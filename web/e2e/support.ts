import AxeBuilder from "@axe-core/playwright";
import { expect } from "@playwright/test";
import type { Page, Route } from "@playwright/test";

export const RESPUESTA_OK = {
  respuesta:
    "Tu promedio del periodo es el que devolvió la API.\n\n- Primer punto de la respuesta\n- Segundo punto de la respuesta\n\nConsulta las fuentes para más detalle.",
  citas: [
    { documento: "evaluacion_y_calificaciones.md", seccion: "Sección 4" },
    { documento: "reglamento_academico_2025.md", seccion: "Artículo 5" },
    { documento: "Datos del estudiante", seccion: "Resumen académico" },
  ],
  usoDatosEstudiante: true,
  tokens: 1234,
  costoUsd: 0.00395,
};

export const RESPUESTA_403_ACCESO = {
  respuesta: "No tienes permiso para consultar esa información. Solo puedes consultar tus propios datos académicos.",
  citas: [{ documento: "reglamento_academico_2025.md", seccion: "Artículo 22" }],
  usoDatosEstudiante: false,
  tokens: 0,
  costoUsd: 0,
};

export interface MockOptions {
  salud?: "ok" | "down";
  preguntar?: (route: Route) => Promise<void> | void;
  /** Retardo (ms) antes de responder a preguntar. */
  retardo?: number;
}

export function json(status: number, body: unknown, headers: Record<string, string> = {}) {
  return { status, headers: { "content-type": "application/json; charset=utf-8", ...headers }, body: JSON.stringify(body) };
}

/** Rutas simuladas de la API (200/500/red/inesperada). No toca la red real. */
export async function simularApi(page: Page, opts: MockOptions = {}): Promise<{ cuerpos: unknown[] }> {
  const cuerpos: unknown[] = [];
  await page.route("**/api/salud", async (route) => {
    if (opts.salud === "down") await route.abort("connectionrefused");
    else await route.fulfill(json(200, { estado: "ok" }));
  });
  await page.route("**/api/asistente/preguntar", async (route) => {
    cuerpos.push(JSON.parse(route.request().postData() ?? "null"));
    if (opts.retardo) await new Promise((r) => setTimeout(r, opts.retardo));
    if (opts.preguntar) await opts.preguntar(route);
    else await route.fulfill(json(200, RESPUESTA_OK));
  });
  return { cuerpos };
}

export async function escribirYEnviar(page: Page, pregunta: string, id = "EST-001", rol: "estudiante" | "coordinador" = "estudiante"): Promise<void> {
  await page.getByLabel("Rol").selectOption(rol);
  await page.getByLabel("Identificador").fill(id);
  await page.getByLabel("Tu pregunta").fill(pregunta);
  await page.getByRole("button", { name: "Enviar" }).click();
}

/** Recoge console.error/warn y errores de página; las pruebas lo verifican al final (WEB-13.a). */
export function vigilarConsola(page: Page): { mensajes: string[] } {
  const mensajes: string[] = [];
  page.on("console", (m) => {
    if (m.type() !== "error" && m.type() !== "warning") return;
    // Chromium registra por su cuenta "Failed to load resource" por cada respuesta HTTP no 2xx de /api
    // (403, 400, 503...). No lo emite el código de la app y los estados de error son el comportamiento esperado.
    const esRegistroDeRedDeLaApi = m.text().startsWith("Failed to load resource") && new URL(m.location().url || "http://x/").pathname.startsWith("/api/");
    if (esRegistroDeRedDeLaApi) return;
    mensajes.push(`${m.type()}: ${m.text()}`);
  });
  page.on("pageerror", (e) => mensajes.push(`pageerror: ${e.message}`));
  return { mensajes };
}

/** Espera a que terminen las animaciones y transiciones de la página: axe debe medir el estado estable; las infinitas (spinner, skeleton) no se esperan (p. ej. el fundido de una cita recién expandida). */
export async function esperarAnimaciones(page: Page): Promise<void> {
  await page.evaluate(async () => {
    await Promise.all(document
        .getAnimations()
        .filter((a) => Number.isFinite(a.effect?.getComputedTiming().endTime ?? Number.POSITIVE_INFINITY))
        .map((a) => a.finished.catch(() => undefined)));
  });
}

export async function axeGrave(page: Page): Promise<string[]> {
  await esperarAnimaciones(page);
  const r = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"]).analyze();
  return r.violations
    .filter((v) => v.impact === "serious" || v.impact === "critical")
    .map((v) => `${v.id}: ${v.help} -> ${v.nodes.map((n) => n.target.join(" ")).join(" | ")}`);
}

export async function sinDesbordamiento(page: Page): Promise<void> {
  const medidas = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
    bodyScroll: document.body.scrollWidth,
  }));
  expect(medidas.scroll).toBeLessThanOrEqual(medidas.client);
  expect(medidas.bodyScroll).toBeLessThanOrEqual(medidas.client);
}

export const VIEWPORTS = [
  { nombre: "movil-375x812", width: 375, height: 812 },
  { nombre: "tablet-768x1024", width: 768, height: 1024 },
  { nombre: "escritorio-1440x900", width: 1440, height: 900 },
] as const;

/** Respuesta del contrato como cuerpo de un 503/400 problem+json de la API real. */
export const PREGUNTA_GENERICA = "¿Cuál es mi promedio del periodo?";
