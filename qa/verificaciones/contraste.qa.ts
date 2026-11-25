import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { appendFileSync } from "node:fs";

const OK = {
  respuesta: "Texto.",
  citas: [{ documento: "reglamento_academico_2025.md", seccion: "Artículo 22" }],
  usoDatosEstudiante: true,
  tokens: 10,
  costoUsd: 0.001,
};

function lum(c: string): number {
  const m = c.match(/[\d.]+/g)!.map(Number);
  const f = (v: number) => { const s = v / 255; return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4; };
  return 0.2126 * f(m[0]!) + 0.7152 * f(m[1]!) + 0.0722 * f(m[2]!);
}
const ratio = (a: string, b: string) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x! + 0.05) / (y! + 0.05); };

test("contraste de dt y dd de la cita expandida: en reposo y a mitad de animacion", async ({ page }) => {
  await page.route("**/api/salud", (r) => r.fulfill({ status: 200, contentType: "application/json", body: '{"estado":"ok"}' }));
  await page.route("**/api/asistente/preguntar", (r) => r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(OK) }));
  await page.goto("/");
  await page.getByLabel("Identificador").fill("EST-001");
  await page.getByLabel("Tu pregunta").fill("hola");
  await page.getByRole("button", { name: /^Enviar/ }).click();
  await expect(page.getByText("US$ 0.001")).toBeVisible();
  await page.getByRole("button", { name: /Artículo 22/ }).click();
  // medicion inmediata (posible animacion en curso)
  const mid = await new AxeBuilder({ page }).withTags(["wcag2aa"]).analyze();
  const midBad = mid.violations.filter((v) => v.id === "color-contrast").length;
  // reposo: esperar animaciones finitas
  await page.evaluate(async () => { await Promise.all(document.getAnimations().filter((a) => Number.isFinite(a.effect?.getComputedTiming().endTime ?? Infinity)).map((a) => a.finished.catch(() => undefined))); });
  const rest = await new AxeBuilder({ page }).withTags(["wcag2aa"]).analyze();
  const restBad = rest.violations.filter((v) => v.id === "color-contrast").length;
  const medidas = await page.evaluate(() => {
    const out: { el: string; color: string; bg: string; op: string }[] = [];
    for (const sel of ["dt", "dd"]) {
      const e = document.querySelector(`.citation__panel ${sel}`) as HTMLElement;
      let bg = "rgba(0, 0, 0, 0)"; let n: HTMLElement | null = e;
      while (n) { const b = getComputedStyle(n).backgroundColor; if (b !== "rgba(0, 0, 0, 0)") { bg = b; break; } n = n.parentElement; }
      const cs = getComputedStyle(e);
      out.push({ el: sel, color: cs.color, bg, op: getComputedStyle(e.closest(".citation__panel")!).opacity });
    }
    return out;
  });
  const lines = medidas.map((m) => `${m.el}: color=${m.color} fondo=${m.bg} opacidad panel=${m.op} ratio=${ratio(m.color, m.bg).toFixed(2)}`);
  const msg = `contraste cita en reposo: ${lines.join(" | ")} | axe color-contrast tras click inmediato=${midBad} violaciones, en reposo=${restBad}`;
  console.log(msg);
  appendFileSync("../evidencias/T-13-r3/salida-verificaciones.txt", msg + "\n");
  for (const m of medidas) expect(ratio(m.color, m.bg)).toBeGreaterThanOrEqual(4.5);
  expect(restBad).toBe(0);
});
