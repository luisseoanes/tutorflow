import { test, expect, type Page, type Route } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { mkdirSync, writeFileSync } from "node:fs";

const OUT = "../evidencias/T-13-r2";
const SIZES = [
  { name: "movil-375x812", width: 375, height: 812 },
  { name: "tablet-768x1024", width: 768, height: 1024 },
  { name: "escritorio-1440x900", width: 1440, height: 900 },
] as const;
const Q_AUTH03 = "¿Cuál es la nota de Bases de Datos de Andrés Martínez?";
const Q_NORM = "¿Qué dice el reglamento sobre faltas?";
const log: string[] = [];
const note = (s: string) => {
  log.push(s);
  console.log(s);
};

const OK = {
  respuesta: "Tu promedio es 4.39.\n\n- Matemáticas: 4.5\n- Bases de Datos: 4.2\n\nEste texto es una prueba.",
  citas: [
    { documento: "reglamento_academico_2025.md", seccion: "Artículo 22" },
    { documento: "reglamento_academico_2025.md", seccion: "Artículo 12" },
    { documento: "guia_evaluacion.md", seccion: "Sección 3" },
  ],
  usoDatosEstudiante: true,
  tokens: 1234,
  costoUsd: 0.00395,
};

function watch(page: Page) {
  const consola: string[] = [];
  const ext: string[] = [];
  page.on("console", (m) => {
    if (["error", "warning"].includes(m.type())) consola.push(`${m.type()}: ${m.text()}`);
  });
  page.on("pageerror", (e) => consola.push("pageerror: " + e.message));
  page.on("request", (r) => {
    const u = new URL(r.url());
    if (!["localhost", "127.0.0.1"].includes(u.hostname) && u.protocol.startsWith("http")) ext.push(r.url());
  });
  return { consola, ext };
}
async function overflow(page: Page) {
  return page.evaluate(() => ({
    sw: document.documentElement.scrollWidth,
    cw: document.documentElement.clientWidth,
    bsw: document.body.scrollWidth,
  }));
}
async function axe(page: Page) {
  const r = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"])
    .analyze();
  return {
    bad: r.violations
      .filter((v) => ["serious", "critical"].includes(v.impact ?? ""))
      .map((v) => `${v.id}:${v.impact}:${v.nodes.length}`),
    all: r.violations.map((v) => `${v.id}:${v.impact}`),
    contrast: r.passes.some((p) => p.id === "color-contrast"),
  };
}
async function check(page: Page, size: (typeof SIZES)[number], tag: string, full = true) {
  mkdirSync(`${OUT}/${size.name}`, { recursive: true });
  await page.waitForTimeout(250);
  const o = await overflow(page);
  const a = await axe(page);
  await page.screenshot({ path: `${OUT}/${size.name}/${tag}.png`, fullPage: full });
  note(
    `[${size.name}] ${tag}: scrollW=${o.sw} clientW=${o.cw} bodySW=${o.bsw} axeSerious/Critical=${JSON.stringify(a.bad)} axeAll=${JSON.stringify(a.all)} contrastRuleRan=${a.contrast}`,
  );
  expect(o.sw, `overflow ${tag} ${size.name}`).toBeLessThanOrEqual(o.cw);
  expect(o.bsw).toBeLessThanOrEqual(o.cw);
  expect(a.bad, `axe ${tag} ${size.name}`).toEqual([]);
}
async function fill(page: Page, q: string, id = "EST-001", rol = "estudiante") {
  await page.getByLabel("Rol").selectOption(rol);
  await page.getByLabel("Identificador").fill(id);
  await page.getByLabel("Tu pregunta").fill(q);
  await page.getByRole("button", { name: /^Enviar/ }).click();
}
const json = (route: Route, status: number, body: unknown, headers: Record<string, string> = {}) =>
  route.fulfill({ status, contentType: "application/json", headers, body: JSON.stringify(body) });

for (const size of SIZES) {
  test.describe(size.name, () => {
    test.use({ viewport: { width: size.width, height: size.height } });

    test("estados con API real sin llave", async ({ page }) => {
      const w = watch(page);
      await page.goto("/");
      await expect(page.getByRole("status").filter({ hasText: "Disponible" })).toBeVisible();
      await check(page, size, "q01-inicial");
      await page.getByLabel("Identificador").fill("EST-001");
      await page.getByLabel("Tu pregunta").fill(Q_AUTH03);
      await check(page, size, "q02-lista-para-enviar");
      await page.getByRole("button", { name: /^Enviar/ }).click();
      await expect(page.getByRole("alert").getByText("Acceso denegado")).toBeVisible();
      await check(page, size, "q03-403-acceso-real");
      await page.reload();
      await fill(page, "hola", "NOEXISTE");
      await expect(page.getByRole("alert").getByText("Identidad no válida")).toBeVisible();
      await check(page, size, "q04-403-identidad-real");
      await page.reload();
      await fill(page, Q_NORM);
      await expect(page.getByRole("alert").getByText("Servicio no disponible temporalmente")).toBeVisible();
      await expect(page.getByRole("button", { name: "Reintentar" })).toBeVisible();
      await check(page, size, "q05-503-real");
      await page.reload();
      await page.route("**/api/asistente/preguntar", (route) => {
        const b = JSON.parse(route.request().postData() ?? "{}");
        b.pregunta = "a".repeat(1001);
        return route.continue({ postData: JSON.stringify(b) });
      });
      await fill(page, "hola");
      await expect(page.getByRole("alert").getByText("La solicitud no es válida")).toBeVisible();
      await check(page, size, "q06-400-real");
      const txt = await page.getByRole("alert").first().innerText();
      note(`[${size.name}] 400 alert text: ${JSON.stringify(txt)}`);
      expect(txt).not.toContain("{");
      expect(w.ext).toEqual([]);
      note(`[${size.name}] consola(real): ${JSON.stringify(w.consola)}`);
      expect(w.consola.filter((m) => !/Failed to load resource/.test(m))).toEqual([]);
    });

    test("estados simulados", async ({ page }) => {
      const w = watch(page);
      let mode = "ok";
      let gate: (() => void) | null = null;
      await page.route("**/api/salud", (r) => json(r, 200, { estado: "ok" }));
      await page.route("**/api/asistente/preguntar", async (route) => {
        if (mode === "slow") {
          await new Promise<void>((res) => {
            gate = res;
            setTimeout(res, 8000);
          });
          return json(route, 200, OK).catch(() => undefined);
        }
        if (mode === "ok") return json(route, 200, OK);
        if (mode === "nocites")
          return json(route, 200, {
            respuesta: "No encontré información suficiente en las fuentes disponibles para responder esa pregunta.",
            citas: [],
            usoDatosEstudiante: false,
            tokens: 87,
            costoUsd: 0.0001,
          });
        if (mode === "xss")
          return json(route, 200, { ...OK, respuesta: "<img src=x onerror=alert(1)> <script>alert(2)</script>" });
        if (mode === "500")
          return json(route, 500, { title: "boom", detail: "System.NullReferenceException at X" }, { "X-Request-Id": "req-abc-123" });
        if (mode === "400") return json(route, 400, { type: "about:blank", title: "Solicitud inválida", status: 400 });
        if (mode === "net") return route.abort("connectionrefused");
        if (mode === "weird") return json(route, 200, { foo: "SECRETO_CUERPO", citas: "x" });
        if (mode === "html")
          return route.fulfill({ status: 200, contentType: "text/html", body: "<h1>SECRETO_CUERPO</h1>" });
      });
      await page.goto("/");
      const dialogs: string[] = [];
      page.on("dialog", (d) => {
        dialogs.push(d.message());
        void d.dismiss();
      });

      mode = "slow";
      await fill(page, "pregunta lenta");
      await expect(page.getByText("Consultando a TutorFlow")).toBeVisible();
      await expect(page.getByLabel("Tu pregunta")).toBeDisabled();
      const loadingBtn = page.getByRole("button", { name: /Enviando/ });
      note(
        `[${size.name}] boton en envio: count=${await loadingBtn.count()} disabled=${await loadingBtn.isDisabled().catch(() => "n/a")}`,
      );
      await check(page, size, "q10-envio-en-curso");
      await page.getByRole("button", { name: "Cancelar" }).click();
      await expect(page.getByLabel("Tu pregunta")).toBeEnabled();
      expect(await page.getByLabel("Tu pregunta").inputValue()).toBe("pregunta lenta");
      (gate as (() => void) | null)?.();

      mode = "ok";
      await page.getByRole("button", { name: /^Enviar/ }).click();
      await expect(page.getByText("US$ 0.00395")).toBeVisible();
      const focusInfo = await page.evaluate(() => {
        const a = document.activeElement;
        return {
          tag: a?.tagName,
          text: (a as HTMLElement)?.innerText?.slice(0, 40),
          role: a?.getAttribute("role"),
          inLive: !!a?.closest("[aria-live]"),
        };
      });
      note(`[${size.name}] foco tras 200: ${JSON.stringify(focusInfo)}`);
      expect(focusInfo.tag).not.toBe("BODY");
      expect(focusInfo.tag).not.toBe("TEXTAREA");
      const body = await page.locator("body").innerText();
      for (const s of ["1234", "Usó datos del estudiante", "Artículo 22", "Artículo 12", "Sección 3", "estudiante"])
        expect(body, s).toContain(s);
      const cita = page.getByRole("button", { name: /reglamento_academico_2025\.md · Artículo 22/ }).first();
      await expect(cita).toHaveAttribute("aria-expanded", "false");
      await cita.click();
      await expect(cita).toHaveAttribute("aria-expanded", "true");
      await check(page, size, "q11-200-varias-citas-expandida");
      note(`[${size.name}] texto 200: ${JSON.stringify(body.slice(0, 700))}`);
      await cita.click();
      await expect(cita).toHaveAttribute("aria-expanded", "false");

      for (const [m, tag, tit] of [
        ["nocites", "q12-200-sin-citas", "Sin citas"],
        ["500", "q13-500", "Ocurrió un error en el servicio"],
        ["400", "q14-400-simulado", "La solicitud no es válida"],
        ["weird", "q15-respuesta-no-reconocida", "Respuesta no reconocida"],
        ["html", "q16-respuesta-html-no-reconocida", "Respuesta no reconocida"],
        ["net", "q17-sin-conexion", "Sin conexión con la API"],
      ] as const) {
        mode = m;
        await page.reload();
        await fill(page, "una pregunta");
        await expect(page.getByText(tit).first()).toBeVisible();
        await check(page, size, tag);
        const t = await page.locator("main").innerText();
        if (m === "500") {
          expect(t).toContain("req-abc-123");
          expect(t).not.toContain("NullReference");
          expect(t).not.toContain("boom");
        }
        if (m === "weird" || m === "html") expect(t).not.toContain("SECRETO_CUERPO");
        if (m === "400") {
          expect(t).toContain("Solicitud inválida");
          expect(t).not.toContain("about:blank");
        }
        if (m === "net") {
          await expect(page.getByRole("status").filter({ hasText: "Sin conexión" }).first()).toBeVisible();
          await check(page, size, "q17b-sin-conexion-indicador");
        }
      }
      mode = "xss";
      await page.reload();
      await fill(page, "xss");
      await expect(page.getByText("<img src=x onerror=alert(1)>")).toBeVisible();
      expect(await page.locator("main img").count()).toBe(0);
      expect(dialogs).toEqual([]);
      const bodies: string[] = [];
      page.on("request", (r) => {
        if (r.method() === "POST") bodies.push(r.postData() ?? "");
      });
      mode = "ok";
      await page.reload();
      await fill(page, "primera");
      await expect(page.getByText("US$ 0.00395")).toBeVisible();
      await page.getByLabel("Tu pregunta").fill("segunda");
      await page.getByRole("button", { name: /^Enviar/ }).click();
      await expect(page.getByText("segunda").first()).toBeVisible();
      await page.waitForTimeout(400);
      note(`[${size.name}] cuerpos POST: ${JSON.stringify(bodies)}`);
      expect(bodies.length).toBe(2);
      expect(bodies[1]).not.toContain("primera");
      const st = await page.evaluate(async () => ({
        ls: localStorage.length,
        ss: sessionStorage.length,
        ck: document.cookie,
        idb: (await (indexedDB as unknown as { databases?: () => Promise<unknown[]> }).databases?.())?.length ?? 0,
        caches: (await caches.keys()).length,
        sw: (await navigator.serviceWorker.getRegistrations()).length,
      }));
      note(`[${size.name}] almacenamiento: ${JSON.stringify(st)}`);
      expect(st).toEqual({ ls: 0, ss: 0, ck: "", idb: 0, caches: 0, sw: 0 });
      expect(await page.context().cookies()).toEqual([]);
      expect(w.ext).toEqual([]);
      note(`[${size.name}] consola(sim): ${JSON.stringify(w.consola)}`);
      expect(w.consola.filter((m) => !/Failed to load resource/.test(m))).toEqual([]);
    });
  });
}

test.describe("teclado y foco (1440)", () => {
  test.use({ viewport: { width: 1440, height: 900 } });
  test("recorrido solo con teclado", async ({ page }) => {
    await page.route("**/api/salud", (r) => json(r, 200, { estado: "ok" }));
    await page.route("**/api/asistente/preguntar", (r) => json(r, 200, OK));
    await page.goto("/");
    const seq: string[] = [];
    const snap = async (label: string) => {
      const i = await page.evaluate(() => {
        const a = document.activeElement as HTMLElement | null;
        if (!a) return null;
        const cs = getComputedStyle(a);
        const r = a.getBoundingClientRect();
        return {
          tag: a.tagName,
          name: a.getAttribute("aria-label") ?? a.innerText?.slice(0, 40) ?? "",
          outline: `${cs.outlineStyle} ${cs.outlineWidth} ${cs.outlineColor}`,
          shadow: cs.boxShadow,
          w: r.width,
          h: r.height,
        };
      });
      seq.push(`${label}: ${JSON.stringify(i)}`);
      return i;
    };
    const visible = async () => {
      const i = await snap("tab");
      const hasRing =
        i && ((i.outline.startsWith("solid") && !i.outline.includes(" 0px")) || (i.shadow && i.shadow !== "none"));
      expect(hasRing, JSON.stringify(i)).toBeTruthy();
      return i;
    };
    await page.keyboard.press("Tab");
    await visible();
    await page.keyboard.press("ArrowDown");
    await page.keyboard.press("ArrowUp");
    await page.keyboard.press("Tab");
    await visible();
    await page.keyboard.type("EST-001");
    for (let i = 0; i < 6; i++) {
      await page.keyboard.press("Tab");
      await visible();
      if ((await page.evaluate(() => document.activeElement?.tagName)) === "TEXTAREA") break;
    }
    expect(await page.evaluate(() => document.activeElement?.tagName)).toBe("TEXTAREA");
    await page.keyboard.type("linea1");
    await page.keyboard.down("Shift");
    await page.keyboard.press("Enter");
    await page.keyboard.up("Shift");
    await page.keyboard.type("linea2");
    expect(await page.getByLabel("Tu pregunta").inputValue()).toBe("linea1\nlinea2");
    await page.keyboard.press("Tab");
    await visible();
    await page.keyboard.press("Enter");
    await expect(page.getByText("US$ 0.00395")).toBeVisible();
    await snap("tras-envio");
    for (let i = 0; i < 6; i++) {
      const a = await page.evaluate(() => (document.activeElement as HTMLElement)?.getAttribute("aria-expanded"));
      if (a !== null) break;
      await page.keyboard.press("Tab");
    }
    await visible();
    expect(await page.evaluate(() => document.activeElement?.getAttribute("aria-expanded"))).toBe("false");
    await page.keyboard.press("Enter");
    expect(await page.evaluate(() => document.activeElement?.getAttribute("aria-expanded"))).toBe("true");
    await page.keyboard.press("Space");
    expect(await page.evaluate(() => document.activeElement?.getAttribute("aria-expanded"))).toBe("false");
    await page.screenshot({ path: `${OUT}/escritorio-1440x900/q20-foco-teclado-cita.png` });
    note("TECLADO:\n" + seq.join("\n"));
  });

  test("ejemplos solo rellenan; Enter envia; contador; controles", async ({ page }) => {
    let posts = 0;
    await page.route("**/api/salud", (r) => json(r, 200, { estado: "ok" }));
    await page.route("**/api/asistente/preguntar", (r) => {
      posts++;
      return json(r, 200, OK);
    });
    await page.goto("/");
    const ejemplos = page.locator("ul.examples button");
    expect(await ejemplos.count()).toBe(3);
    await ejemplos.nth(1).click();
    expect(await page.getByLabel("Tu pregunta").inputValue()).toBe(await ejemplos.nth(1).innerText());
    expect(posts).toBe(0);
    await page.getByLabel("Identificador").fill("EST-001");
    await page.getByLabel("Tu pregunta").press("Enter");
    await expect(page.getByText("US$ 0.00395")).toBeVisible();
    expect(posts).toBe(1);
    await expect(page.getByRole("button", { name: /^Enviar/ })).toBeDisabled();
    await page.getByLabel("Tu pregunta").fill("x".repeat(1001));
    note("contador: " + (await page.locator(".counter").innerText()));
    await expect(page.getByRole("button", { name: /^Enviar/ })).toBeDisabled();
    await page.getByLabel("Identificador").fill("");
    await page.getByLabel("Tu pregunta").fill("hola");
    await page.getByRole("button", { name: /^Enviar/ }).click();
    expect(posts).toBe(1);
    note("validacion id vacio: " + JSON.stringify(await page.locator("form").innerText()));
    let sent = "";
    await page.unroute("**/api/asistente/preguntar");
    await page.route("**/api/asistente/preguntar", (r) => {
      sent = r.request().postData() ?? "";
      return json(r, 200, OK);
    });
    await page.getByLabel("Rol").selectOption("coordinador");
    await page.getByLabel("Identificador").fill(" Abc 1 ");
    await page.getByRole("button", { name: /^Enviar/ }).click();
    await page.waitForTimeout(400);
    note("cuerpo enviado: " + sent);
    expect(JSON.parse(sent)).toEqual({ pregunta: "hola", usuario: { rol: "coordinador", id: " Abc 1 " } });
    note("opciones rol: " + JSON.stringify(await page.getByLabel("Rol").locator("option").allInnerTexts()));
    const ctrls = await page.evaluate(() =>
      [...document.querySelectorAll("button,a,select,input,textarea,[role=button],[tabindex]")].map(
        (e) =>
          `${e.tagName}:${(e as HTMLElement).innerText?.slice(0, 30) || e.getAttribute("aria-label") || ""}:disabled=${(e as HTMLButtonElement).disabled}`,
      ),
    );
    note("controles: " + JSON.stringify(ctrls));
    note("enlaces <a>: " + (await page.locator("a").count()));
  });

  test("reduced motion, tipografia, gradientes", async ({ browser }) => {
    const ctx = await browser.newContext({ reducedMotion: "reduce", viewport: { width: 1440, height: 900 } });
    const page = await ctx.newPage();
    await page.route("**/api/salud", (r) => json(r, 200, { estado: "ok" }));
    await page.route("**/api/asistente/preguntar", (r) => json(r, 200, OK));
    await page.goto("/");
    await fill(page, "hola");
    await expect(page.getByText("US$ 0.00395")).toBeVisible();
    const t = await page.evaluate(
      () =>
        [...document.querySelectorAll("*")]
          .map((e) => getComputedStyle(e))
          .filter(
            (s) =>
              (parseFloat(s.transitionDuration) > 0 && s.transitionDuration !== "0s") ||
              (parseFloat(s.animationDuration) > 0.001 && s.animationName !== "none"),
          ).length,
    );
    note("elementos con transicion/animacion bajo reduced-motion: " + t);
    expect(t).toBe(0);
    const small = await page.evaluate(
      () =>
        [...document.querySelectorAll("main *, header *")]
          .filter((e) => [...e.childNodes].some((n) => n.nodeType === 3 && n.textContent?.trim()))
          .map((e) => parseFloat(getComputedStyle(e).fontSize))
          .filter((f) => f < 13).length,
    );
    note("textos < 13px: " + small);
    const fonts = await page.evaluate(() => [
      ...new Set([...document.querySelectorAll("body, h1, button, textarea")].map((e) => getComputedStyle(e).fontFamily)),
    ]);
    note("font-family: " + JSON.stringify(fonts));
    const grads = await page.evaluate(
      () => [...document.querySelectorAll("*")].filter((e) => getComputedStyle(e).backgroundImage.includes("gradient")).length,
    );
    note("gradientes: " + grads);
    await ctx.close();
  });
});

test.describe("tactil 375", () => {
  test.use({ viewport: { width: 375, height: 812 }, hasTouch: true });
  test("objetivos tactiles >= 44px", async ({ page }) => {
    await page.route("**/api/salud", (r) => json(r, 200, { estado: "ok" }));
    await page.route("**/api/asistente/preguntar", (r) => json(r, 200, OK));
    await page.goto("/");
    const meas = async (label: string) => {
      const r = await page.evaluate(() =>
        [...document.querySelectorAll("button,select,input,textarea,a")]
          .filter((e) => {
            const b = e.getBoundingClientRect();
            return b.width > 0 && b.height > 0;
          })
          .map((e) => {
            const b = e.getBoundingClientRect();
            return {
              n: `${e.tagName}:${(e as HTMLElement).innerText?.slice(0, 25) || e.getAttribute("aria-label") || ""}`,
              w: Math.round(b.width),
              h: Math.round(b.height),
            };
          }),
      );
      note(`[tactil ${label}] ` + JSON.stringify(r));
      return r.filter((x) => x.h < 44 || x.w < 44);
    };
    const a = await meas("inicial");
    await fill(page, "hola");
    await expect(page.getByText("US$ 0.00395")).toBeVisible();
    const b = await meas("respuesta");
    note("objetivos < 44: " + JSON.stringify([...a, ...b]));
    expect([...a, ...b]).toEqual([]);
  });
});

test("cabeceras", async ({ request }) => {
  for (const p of ["/", "/api/salud", "/assets/nope.js"]) {
    const r = await request.get(p);
    const h = r.headers();
    note(
      `cabeceras ${p} [${r.status()}]: csp=${h["content-security-policy"]} nosniff=${h["x-content-type-options"]} ref=${h["referrer-policy"]}`,
    );
    if (r.status() < 400) {
      expect(h["content-security-policy"]).toContain("default-src 'self'");
      expect(h["content-security-policy"]).not.toMatch(/script-src[^;]*unsafe-inline/);
      expect(h["x-content-type-options"]).toBe("nosniff");
      expect(h["referrer-policy"]).toBe("no-referrer");
    }
  }
});

test.afterAll(() => {
  writeFileSync("../evidencias/T-13-r2/salida-verificaciones.txt", log.join("\n") + "\n");
});
