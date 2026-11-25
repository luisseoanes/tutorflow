// @vitest-environment node
import { mkdirSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { build } from "vite";
import { beforeAll, describe, expect, it } from "vitest";

const SALIDA = `${process.cwd()}/node_modules/.tmp/bundle-test`;

let archivos: [string, string][] = [];
let errorDeBuild: unknown = null;

beforeAll(async () => {
  rmSync(SALIDA, { recursive: true, force: true });
  mkdirSync(SALIDA, { recursive: true });
  try {
    await build({ logLevel: "silent", build: { outDir: SALIDA, emptyOutDir: true } });
  } catch (e) {
    errorDeBuild = e;
  }
  archivos = readdirSync(SALIDA, { recursive: true })
    .filter((ruta) => /\.(js|css|html)$/.test(ruta))
    .map((ruta): [string, string] => [ruta, readFileSync(`${SALIDA}/${ruta}`, "utf8")]);
}, 120_000);

describe("bundle de producción", () => {
  // Criterio: WEB-14.b
  it("el build de producción (vite build) termina sin errores y genera index.html, JS y CSS", () => {
    expect(errorDeBuild).toBeNull();
    const nombres = archivos.map(([r]) => r);
    expect(nombres).toContain("index.html");
    expect(nombres.some((r) => r.endsWith(".js"))).toBe(true);
    expect(nombres.some((r) => r.endsWith(".css"))).toBe(true);
  });

  // Criterio: WEB-04.c
  it("no contiene llaves, centinelas ni variables VITE_* con secretos", () => {
    const prohibidos = [
      "LLM_API_KEY",
      "EMBEDDING_API_KEY",
      "test-key-NOT-REAL",
      "VITE_",
      "GEMINI",
      "OPENAI_API",
      "Bearer ",
      "AIza",
    ];
    for (const [ruta, contenido] of archivos) {
      for (const texto of prohibidos) {
        expect(contenido.includes(texto), `${ruta} contiene ${texto}`).toBe(false);
      }
      expect(/\bsk-[A-Za-z0-9]{20,}/.test(contenido), `${ruta} parece contener una llave`).toBe(false);
    }
  });

  // Criterio: WEB-04.c
  it("no hay variables de entorno incrustadas (import.meta.env solo con valores de modo, sin VITE_*)", () => {
    const js = archivos.filter(([r]) => r.endsWith(".js")).map(([, c]) => c).join("\n");
    expect(js).not.toMatch(/import\.meta\.env\.VITE_/);
  });

  // Criterio: WEB-16.b
  it("el HTML y el CSS no referencian orígenes externos: la fuente Inter es un archivo del mismo origen", () => {
    for (const [ruta, contenido] of archivos.filter(([r]) => r.endsWith(".html") || r.endsWith(".css"))) {
      expect(/https?:\/\//.test(contenido), `${ruta} referencia un origen externo`).toBe(false);
    }
    const css = archivos.filter(([r]) => r.endsWith(".css")).map(([, c]) => c).join("\n");
    expect(css).toMatch(/font-family:\s*"?Inter"?/);
    expect(css).toMatch(/url\(\/assets\/inter-latin-400-normal-[\w-]+\.woff2\)/);
    expect(css).not.toMatch(/url\(data:/);
    const fuentes = readdirSync(SALIDA, { recursive: true }).filter((r) => r.endsWith(".woff2"));
    expect(fuentes.length).toBeGreaterThanOrEqual(3);
  });
});
