import { expect, test } from "@playwright/test";
import type { Page, Route } from "@playwright/test";
import { escribirYEnviar, json, RESPUESTA_OK, simularApi, VIEWPORTS } from "../support";

/** Genera las capturas de evidencia. No es una prueba de aceptación: no se ejecuta en `npm run e2e`. */

const DIR = process.env["EVIDENCIAS_DIR"] ?? "evidencias/T-13-r1";

const larga = {
  ...RESPUESTA_OK,
  respuesta:
    "Tu promedio ponderado del periodo es el que calculó la API a partir de tus notas.\n\nPuntos a tener en cuenta:\n- La definitiva de cada asignatura sale de los cortes registrados.\n- Una asignatura con definitiva menor al mínimo se considera perdida.\n- La asistencia puede afectar el resultado.\n\nSi tienes dudas, revisa las fuentes citadas debajo.",
};

async function captura(page: Page, vp: string, nombre: string): Promise<void> {
  await page.screenshot({ path: `${DIR}/${vp}/${nombre}.png`, fullPage: true });
}

for (const vp of VIEWPORTS) {
  test.describe(`capturas ${vp.nombre}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    test("estados simulados: inicial, lista, envío, 200, sin citas, validación, 500, red, inesperada", async ({ page }) => {
      let modo: "200" | "sin-citas" | "500" | "red" | "inesperada" | "lenta" = "200";
      await simularApi(page, {
        preguntar: async (route: Route) => {
          if (modo === "lenta") {
            await new Promise((r) => setTimeout(r, 4000));
            return route.fulfill(json(200, larga));
          }
          if (modo === "sin-citas") {
            return route.fulfill(
              json(200, {
                respuesta: "No encuentro esa información en los documentos disponibles.",
                citas: [],
                usoDatosEstudiante: false,
                tokens: 412,
                costoUsd: 0.000316,
              }),
            );
          }
          if (modo === "500") return route.fulfill(json(500, { title: "Error" }, { "x-request-id": "9f2c1a7e4b3d4c0f" }));
          if (modo === "red") return route.abort("connectionrefused");
          if (modo === "inesperada") return route.fulfill(json(200, { forma: "distinta" }));
          return route.fulfill(json(200, larga));
        },
      });
      await page.goto("/");
      await expect(page.getByText("Disponible")).toBeVisible();
      await captura(page, vp.nombre, "01-inicial");

      await page.getByLabel("Identificador").fill("EST-001");
      await page.getByLabel("Tu pregunta").fill("¿Cuál es mi promedio del periodo actual?");
      await captura(page, vp.nombre, "02-lista-para-enviar");

      // Validación de UX: límite excedido e id vacío
      await page.getByLabel("Tu pregunta").fill("a".repeat(1001));
      await page.getByLabel("Identificador").fill("");
      await page.getByLabel("Identificador").blur();
      await captura(page, vp.nombre, "03-validacion-ux");
      await page.getByLabel("Identificador").fill("EST-001");

      // Envío en curso
      modo = "lenta";
      await page.getByLabel("Tu pregunta").fill("¿Cuál es mi promedio del periodo actual?");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Consultando a TutorFlow")).toBeVisible();
      await captura(page, vp.nombre, "04-envio-en-curso");
      await expect(page.locator(".message--answer")).toBeVisible({ timeout: 10_000 });
      await page.getByRole("button", { name: /reglamento_academico_2025/ }).click();
      await captura(page, vp.nombre, "05-respuesta-200-varias-citas-una-expandida");

      modo = "sin-citas";
      await page.getByLabel("Tu pregunta").fill("¿Hay clases en vacaciones?");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Sin citas")).toBeVisible();
      await captura(page, vp.nombre, "06-respuesta-sin-citas-abstencion");

      modo = "500";
      await page.getByLabel("Tu pregunta").fill("Pregunta que provoca error 500");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Ocurrió un error en el servicio")).toBeVisible();
      await captura(page, vp.nombre, "07-error-500-con-codigo-de-soporte");

      modo = "inesperada";
      await page.getByLabel("Tu pregunta").fill("Pregunta con respuesta inesperada");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Respuesta no reconocida")).toBeVisible();
      await captura(page, vp.nombre, "08-respuesta-no-reconocida");

      modo = "red";
      await page.getByLabel("Tu pregunta").fill("Pregunta sin conexión");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Sin conexión con la API")).toBeVisible();
      await captura(page, vp.nombre, "09-sin-conexion");
    });

    test("estados con la API real sin llave: 403 acceso, 403 identidad, 503, 400", async ({ page }) => {
      await page.goto("/");
      await escribirYEnviar(page, "¿Cuál es la nota de Bases de Datos de Andrés Martínez?", "EST-001");
      await expect(page.getByText("Acceso denegado")).toBeVisible();
      await captura(page, vp.nombre, "10-403-acceso-denegado-real");

      await page.reload();
      await escribirYEnviar(page, "¿Cuál es mi promedio?", "EST-999");
      await expect(page.getByText("Identidad no válida")).toBeVisible();
      await captura(page, vp.nombre, "11-403-identidad-no-valida-real");

      await page.reload();
      await escribirYEnviar(page, "¿Cuál es el máximo de faltas permitido según el reglamento?", "EST-001");
      await expect(page.getByText("Servicio no disponible temporalmente")).toBeVisible();
      await captura(page, vp.nombre, "12-503-servicio-no-disponible-real");

      await page.reload();
      await page.route("**/api/asistente/preguntar", async (route) => {
        const cuerpo = JSON.parse(route.request().postData() ?? "{}") as object;
        await route.continue({ postData: JSON.stringify({ ...cuerpo, pregunta: "a".repeat(1001) }) });
      });
      await escribirYEnviar(page, "pregunta corta", "EST-001");
      await expect(page.getByText("La solicitud no es válida")).toBeVisible();
      await captura(page, vp.nombre, "13-400-solicitud-no-valida-real");
    });

    test("API desconectada: indicador de estado en 'Sin conexión'", async ({ page }) => {
      await simularApi(page, { salud: "down" });
      await page.goto("/");
      await expect(page.getByText("Sin conexión")).toBeVisible();
      await captura(page, vp.nombre, "14-api-desconectada");
    });
  });
}
