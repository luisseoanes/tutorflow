import { expect, test } from "@playwright/test";
import {
  axeGrave,
  escribirYEnviar,
  json,
  PREGUNTA_GENERICA,
  RESPUESTA_403_ACCESO,
  RESPUESTA_OK,
  simularApi,
  sinDesbordamiento,
  VIEWPORTS,
  vigilarConsola,
} from "./support";

/** [e2e] con rutas de API SIMULADAS (200/500/red/inesperada y demás estados). Nunca toca la API real. */

test.describe("recorrido con respuestas simuladas", () => {
  // Criterio: WEB-05.c
  test("el foco pasa a la nueva respuesta y la región aria-live la contiene", async ({ page }) => {
    await simularApi(page);
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_GENERICA);
    const respuesta = page.locator(".message--answer");
    await expect(respuesta).toBeVisible();
    await expect(respuesta).toBeFocused();
    const region = page.getByRole("region", { name: "Conversación" });
    await expect(region).toHaveAttribute("aria-live", "polite");
    await expect(region.locator(".message--answer")).toHaveCount(1);
    // La región ya existía antes de la respuesta (requisito para que se anuncie).
    await expect(region.getByText("Tokens: 1234")).toBeVisible();
  });

  // Criterio: WEB-05.c
  test("el estado de carga se anuncia con role=status y el campo queda deshabilitado", async ({ page }) => {
    await simularApi(page, { retardo: 1200 });
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_GENERICA);
    await expect(page.getByRole("status").filter({ hasText: "Consultando a TutorFlow" })).toBeVisible();
    await expect(page.getByLabel("Tu pregunta")).toBeDisabled();
    await expect(page.locator(".message--answer")).toBeVisible();
  });

  // Criterio: WEB-04.b
  test("tras una conversación completa el almacenamiento del navegador está vacío", async ({ page }) => {
    await simularApi(page);
    await page.goto("/");
    await escribirYEnviar(page, "Primera pregunta");
    await expect(page.locator(".message--answer")).toBeVisible();
    await page.getByLabel("Tu pregunta").fill("Segunda pregunta");
    await page.getByRole("button", { name: "Enviar" }).click();
    await expect(page.locator(".message--answer")).toHaveCount(2);
    await page.getByRole("button", { name: /·/ }).first().click();

    const almacenamiento = await page.evaluate(async () => ({
      local: window.localStorage.length,
      session: window.sessionStorage.length,
      cookies: document.cookie,
      indexedDb: (await window.indexedDB.databases()).length,
      cache: typeof window.caches === "undefined" ? 0 : (await window.caches.keys()).length,
      workers: typeof navigator.serviceWorker === "undefined" ? 0 : (await navigator.serviceWorker.getRegistrations()).length,
    }));
    expect(almacenamiento).toEqual({ local: 0, session: 0, cookies: "", indexedDb: 0, cache: 0, workers: 0 });
    expect(await page.context().cookies()).toEqual([]);
  });

  // Criterio: WEB-01.b
  test("todas las peticiones salen a /api/* del mismo origen", async ({ page, baseURL }) => {
    await simularApi(page);
    const urls: string[] = [];
    page.on("request", (r) => urls.push(r.url()));
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_GENERICA);
    await expect(page.locator(".message--answer")).toBeVisible();
    const origen = new URL(baseURL ?? "http://localhost:3000").origin;
    for (const u of urls) expect(new URL(u).origin, u).toBe(origen);
    const apis = urls.filter((u) => new URL(u).pathname.startsWith("/api/")).map((u) => new URL(u).pathname);
    expect(apis).toContain("/api/salud");
    expect(apis).toContain("/api/asistente/preguntar");
    for (const p of apis) expect(["/api/salud", "/api/asistente/preguntar"]).toContain(p);
  });

  // Criterio: WEB-16.b
  test("la fuente Inter se sirve del mismo origen y no hay peticiones a dominios externos", async ({ page, baseURL }) => {
    await simularApi(page);
    const externas: string[] = [];
    const fuentes: string[] = [];
    const origen = new URL(baseURL ?? "http://localhost:3000").origin;
    page.on("request", (r) => {
      if (new URL(r.url()).origin !== origen && !r.url().startsWith("data:")) externas.push(r.url());
      if (r.resourceType() === "font") fuentes.push(r.url());
    });
    await page.goto("/");
    await page.evaluate(() => document.fonts.ready);
    expect(externas).toEqual([]);
    expect(fuentes.length).toBeGreaterThan(0);
    for (const f of fuentes) expect(new URL(f).origin).toBe(origen);
    expect(await page.evaluate(() => document.fonts.check('16px "Inter"'))).toBe(true);
    expect(await page.evaluate(() => getComputedStyle(document.body).fontFamily)).toContain("Inter");
  });

  // Criterio: WEB-13.a
  test("ningún console.error/warn ni error de página en el recorrido completo de estados", async ({ page }) => {
    const consola = vigilarConsola(page);
    let n = 0;
    const respuestas = [
      (route: import("@playwright/test").Route) => route.fulfill(json(200, RESPUESTA_OK)),
      (route: import("@playwright/test").Route) => route.fulfill(json(400, { title: "Solicitud inválida" })),
      (route: import("@playwright/test").Route) => route.fulfill(json(403, RESPUESTA_403_ACCESO)),
      (route: import("@playwright/test").Route) =>
        route.fulfill(json(403, { respuesta: "No fue posible validar la identidad indicada en la solicitud.", citas: [], usoDatosEstudiante: false, tokens: 0, costoUsd: 0 })),
      (route: import("@playwright/test").Route) => route.fulfill(json(503, { title: "Servicio no disponible" })),
      (route: import("@playwright/test").Route) => route.fulfill(json(500, { title: "Error" }, { "x-request-id": "req-e2e-1" })),
      (route: import("@playwright/test").Route) => route.fulfill(json(200, { respuesta: 1 })),
      (route: import("@playwright/test").Route) => route.abort("connectionrefused"),
      (route: import("@playwright/test").Route) => route.fulfill(json(200, { ...RESPUESTA_OK, citas: [] })),
    ];
    await simularApi(page, {
      preguntar: (route) => {
        const f = respuestas[Math.min(n++, respuestas.length - 1)];
        return f ? f(route) : undefined;
      },
    });
    await page.goto("/");
    for (let i = 0; i < respuestas.length; i += 1) {
      await escribirYEnviar(page, `Pregunta ${i + 1}`);
      await expect(page.getByText(`Pregunta ${i + 1}`)).toBeVisible();
      await expect(page.locator(".turn").first().locator(".pending")).toHaveCount(0);
    }
    await page.getByRole("button", { name: /·/ }).first().click();
    expect(consola.mensajes).toEqual([]);
  });
});

test.describe("accesibilidad en navegador real", () => {
  // Criterio: WEB-12.b
  test("axe sin violaciones serious/critical en cada estado de §4.3", async ({ page }) => {
    const respuestas: Record<string, (route: import("@playwright/test").Route) => Promise<void>> = {
      "200": (route) => route.fulfill(json(200, RESPUESTA_OK)),
      "200 sin citas": (route) => route.fulfill(json(200, { ...RESPUESTA_OK, citas: [] })),
      "400": (route) => route.fulfill(json(400, { title: "Solicitud inválida" })),
      "403 acceso": (route) => route.fulfill(json(403, RESPUESTA_403_ACCESO)),
      "403 identidad": (route) => route.fulfill(json(403, { respuesta: "No fue posible validar la identidad indicada en la solicitud.", citas: [], usoDatosEstudiante: false, tokens: 0, costoUsd: 0 })),
      "503": (route) => route.fulfill(json(503, {})),
      "500": (route) => route.fulfill(json(500, {}, { "x-request-id": "req-axe" })),
      "inesperada": (route) => route.fulfill(json(200, { x: 1 })),
      "red": (route) => route.abort("connectionrefused"),
    };
    let actual = "200";
    await simularApi(page, { preguntar: (route) => respuestas[actual]?.(route) });

    await page.goto("/");
    await expect(page.getByText("Disponible")).toBeVisible();
    expect(await axeGrave(page), "estado inicial").toEqual([]);

    await page.getByLabel("Identificador").fill("EST-001");
    await page.getByLabel("Tu pregunta").fill(PREGUNTA_GENERICA);
    expect(await axeGrave(page), "lista para enviar").toEqual([]);

    await page.getByLabel("Tu pregunta").fill("a".repeat(1001));
    expect(await axeGrave(page), "límite excedido").toEqual([]);
    await page.getByLabel("Tu pregunta").fill("");
    await page.getByLabel("Identificador").fill("");
    await page.getByLabel("Identificador").blur();
    expect(await axeGrave(page), "id vacío").toEqual([]);

    for (const estado of Object.keys(respuestas)) {
      actual = estado;
      await escribirYEnviar(page, `Pregunta ${estado}`);
      await expect(page.getByText(`Pregunta ${estado}`)).toBeVisible();
      await expect(page.locator(".turn").first().locator(".pending")).toHaveCount(0);
      if (estado === "200") await page.getByRole("button", { name: /·/ }).first().click();
      expect(await axeGrave(page), estado).toEqual([]);
    }
  });

  // Criterio: WEB-12.b
  test("axe en el estado de carga", async ({ page }) => {
    await simularApi(page, { retardo: 2500 });
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_GENERICA);
    await expect(page.getByText("Consultando a TutorFlow")).toBeVisible();
    expect(await axeGrave(page)).toEqual([]);
  });

  // Criterio: WEB-12.b
  test("recorrido solo con teclado: rol, id, pregunta, enviar y expandir una cita, con foco visible en cada paso", async ({ page }) => {
    await simularApi(page);
    await page.goto("/");
    // Esperar al formulario montado: sin esto el primer Tab puede ocurrir antes de que React monte la vista.
    await expect(page.getByLabel("Rol")).toBeVisible();

    const foco = async (): Promise<{ etiqueta: string; anchoContorno: number; estilo: string }> =>
      page.evaluate(() => {
        const el = document.activeElement as HTMLElement;
        const cs = getComputedStyle(el);
        const etiqueta =
          el.getAttribute("aria-label") ??
          (el.id ? document.querySelector(`label[for="${el.id}"]`)?.textContent : null) ??
          el.textContent?.trim() ??
          el.tagName;
        return { etiqueta: etiqueta ?? "", anchoContorno: Number.parseFloat(cs.outlineWidth), estilo: cs.outlineStyle };
      });
    const visible = (f: { anchoContorno: number; estilo: string }): void => {
      expect(f.estilo).not.toBe("none");
      expect(f.anchoContorno).toBeGreaterThanOrEqual(2);
    };

    await page.keyboard.press("Tab");
    const rol = await foco();
    expect(rol.etiqueta).toBe("Rol");
    visible(rol);
    await page.keyboard.type("c"); // type-ahead: coordinador
    await expect(page.getByLabel("Rol")).toHaveValue("coordinador");

    await page.keyboard.press("Tab");
    const id = await foco();
    expect(id.etiqueta).toBe("Identificador");
    visible(id);
    await page.keyboard.type("EST-001");

    await page.keyboard.press("Tab");
    const pregunta = await foco();
    expect(pregunta.etiqueta).toBe("Tu pregunta");
    visible(pregunta);
    await page.keyboard.type(PREGUNTA_GENERICA);

    await page.keyboard.press("Tab");
    const enviar = await foco();
    expect(enviar.etiqueta).toBe("Enviar");
    visible(enviar);
    await page.keyboard.press("Enter");

    await expect(page.locator(".message--answer")).toBeFocused();
    await page.keyboard.press("Tab");
    const cita = await foco();
    expect(cita.etiqueta).toContain("evaluacion_y_calificaciones.md · Sección 4");
    visible(cita);
    const boton = page.getByRole("button", { name: /evaluacion_y_calificaciones/ });
    await expect(boton).toHaveAttribute("aria-expanded", "false");
    await page.keyboard.press("Enter");
    await expect(boton).toHaveAttribute("aria-expanded", "true");
    await expect(page.getByText("Sección 4", { exact: true })).toBeVisible();
    await page.keyboard.press("Space");
    await expect(boton).toHaveAttribute("aria-expanded", "false");
  });

  // Criterio: WEB-12.c
  test("con prefers-reduced-motion: reduce no hay transiciones ni animaciones", async ({ page }) => {
    await page.emulateMedia({ reducedMotion: "reduce" });
    await simularApi(page, { retardo: 800 });
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_GENERICA);
    await expect(page.locator(".spinner").first()).toBeVisible();
    const duraciones = await page.evaluate(() =>
      Array.from(document.querySelectorAll<HTMLElement>(".btn, .spinner, .skeleton__line, .control")).map((el) => {
        const cs = getComputedStyle(el);
        return { clase: el.className, transicion: cs.transitionDuration, animacion: cs.animationDuration };
      }),
    );
    expect(duraciones.length).toBeGreaterThan(2);
    for (const d of duraciones) {
      expect(d.transicion.split(",").every((x) => Number.parseFloat(x) === 0), d.clase).toBe(true);
      expect(d.animacion.split(",").every((x) => Number.parseFloat(x) === 0), d.clase).toBe(true);
    }
  });
});

test.describe("responsive", () => {
  for (const vp of VIEWPORTS) {
    // Criterio: WEB-11.a
    test(`sin desbordamiento horizontal en ${vp.nombre}: inicial, 200 con varias citas, 403 y 503`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      let actual: "200" | "403" | "503" = "200";
      await simularApi(page, {
        preguntar: (route) => {
          if (actual === "403") return route.fulfill(json(403, RESPUESTA_403_ACCESO));
          if (actual === "503") return route.fulfill(json(503, {}));
          return route.fulfill(json(200, { ...RESPUESTA_OK, respuesta: `${RESPUESTA_OK.respuesta} ${"palabralargasinespacios".repeat(8)}`, citas: [...RESPUESTA_OK.citas, { documento: "documento_con_un_nombre_extremadamente_largo_para_probar_el_ajuste.md", seccion: "Sección 3.1.2" }] }));
        },
      });
      await page.goto("/");
      await expect(page.getByText("Disponible")).toBeVisible();
      await sinDesbordamiento(page);

      await escribirYEnviar(page, PREGUNTA_GENERICA);
      await expect(page.locator(".message--answer")).toBeVisible();
      await page.getByRole("button", { name: /documento_con_un_nombre/ }).click();
      await sinDesbordamiento(page);

      actual = "403";
      await page.getByLabel("Tu pregunta").fill("Notas de otra persona");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Acceso denegado")).toBeVisible();
      await sinDesbordamiento(page);

      actual = "503";
      await page.getByLabel("Tu pregunta").fill("Pregunta normativa");
      await page.getByRole("button", { name: "Enviar" }).click();
      await expect(page.getByText("Servicio no disponible temporalmente")).toBeVisible();
      await sinDesbordamiento(page);
    });
  }

  // Criterio: WEB-11.a
  test("en móvil (375 px) los controles interactivos miden al menos 44 x 44 px", async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await simularApi(page);
    await page.goto("/");
    await escribirYEnviar(page, PREGUNTA_GENERICA);
    await expect(page.locator(".message--answer")).toBeVisible();
    const pequenos = await page.evaluate(() =>
      Array.from(document.querySelectorAll<HTMLElement>("button, input, select, textarea"))
        .filter((el) => !el.hasAttribute("disabled"))
        .map((el) => ({ texto: el.textContent?.trim() || el.tagName, r: el.getBoundingClientRect() }))
        .filter(({ r }) => r.width < 44 || r.height < 44)
        .map(({ texto, r }) => `${texto}: ${Math.round(r.width)}x${Math.round(r.height)}`),
    );
    expect(pequenos).toEqual([]);
  });
});
