import { describe, expect, it } from "vitest";
import { archivosFuente } from "./sources";

/**
 * Pruebas de arquitectura del frontend: análisis estático de web/src (sin ejecutar la app).
 */
const todos = archivosFuente();
const fueraDeTokens = todos.filter(([ruta]) => !ruta.includes("/tokens/"));

function hallazgos(lista: [string, string][], patron: RegExp): string[] {
  const encontrados: string[] = [];
  for (const [ruta, contenido] of lista) {
    const lineas = contenido.split("\n");
    lineas.forEach((linea, i) => {
      if (patron.test(linea)) encontrados.push(`${ruta}:${i + 1}: ${linea.trim()}`);
    });
  }
  return encontrados;
}

describe("arquitectura del frontend", () => {
  it("el análisis ve los archivos esperados (la prueba no es vacía)", () => {
    expect(todos.length).toBeGreaterThan(15);
    expect(todos.some(([r, c]) => r.endsWith("services/apiClient.ts") && c.length > 0)).toBe(true);
    expect(todos.some(([r, c]) => r.endsWith("tokens/tokens.css") && c.includes("#3451D1"))).toBe(true);
    expect(todos.every(([, c]) => c.length > 0)).toBe(true);
  });

  // Criterio: WEB-03.a
  it("sin lógica de promedio, ponderación, redondeo, umbrales ni permisos por rol en web/src", () => {
    const numerosAcademicos = /(?<![\w.])(0\.3|0\.4|3\.0|15)(?![\w.])/;
    expect(hallazgos(fueraDeTokens, numerosAcademicos)).toEqual([]);
    const calculos = /Math\.(round|floor|ceil|trunc|min|max)\b|\.toFixed\(|\.toPrecision\(|\.reduce\(|Intl\.NumberFormat|toLocaleString/;
    expect(hallazgos(todos, calculos)).toEqual([]);
    const permisosPorRol = /[!=]==?\s*["'`](estudiante|coordinador)["'`]|["'`](estudiante|coordinador)["'`]\s*[!=]==?|switch\s*\(.*rol/;
    expect(hallazgos(todos, permisosPorRol)).toEqual([]);
    const palabras = /\b(promedio|ponderad|habilitaci|definitiva)\w*\s*(=|\()/i;
    expect(hallazgos(todos.filter(([r]) => r.endsWith(".ts") || r.endsWith(".tsx")), palabras)).toEqual([]);
  });

  // Criterio: WEB-03.a
  it("el rol solo se muestra o se envía: ningún componente lo compara ni ramifica por él", () => {
    const usos = hallazgos(todos, /\brol\b/).filter((l) => /\bif\b|\?|&&|\|\|/.test(l));
    // Las únicas ramificaciones permitidas sobre `rol` son la validación del valor del <select> (esRol) y tipos.
    const sospechosos = usos.filter((l) => !/esRol|useState<Rol>|setRol/.test(l));
    expect(sospechosos).toEqual([]);
  });

  // Criterio: WEB-04.a
  it("sin localStorage, sessionStorage, indexedDB, cookies ni service workers en web/src", () => {
    const patron = /localStorage|sessionStorage|indexedDB|document\.cookie|serviceWorker|\bcaches\b|CacheStorage/;
    expect(hallazgos(todos, patron)).toEqual([]);
  });

  // Criterio: WEB-05.b
  it("el render de respuestas nunca inyecta HTML", () => {
    const patron = /dangerouslySetInnerHTML|innerHTML|outerHTML|insertAdjacentHTML|document\.write|\beval\(|new Function\(/;
    expect(hallazgos(todos, patron)).toEqual([]);
  });

  // Criterio: WEB-01.a
  it("el cliente HTTP es único: fetch solo en services/apiClient.ts y sin URL absolutas", () => {
    const conFetch = todos.filter(([, c]) => /\bfetch\(|XMLHttpRequest|axios|WebSocket|EventSource/.test(c)).map(([r]) => r);
    expect(conFetch).toEqual(["src/services/apiClient.ts"]);
    expect(hallazgos(todos, /https?:\/\//)).toEqual([]);
  });

  // Criterio: WEB-14.a
  it("ningún any explícito en web/src", () => {
    const patron = /:\s*any\b|\bas\s+any\b|<any[,>\s]|\bany\[\]|Array<any>|@ts-ignore|@ts-nocheck|eslint-disable/;
    expect(hallazgos(todos.filter(([r]) => !r.endsWith(".css")), patron)).toEqual([]);
  });

  // Criterio: WEB-15.a
  it("sin literales de color ni px arbitrarios fuera del módulo de tokens", () => {
    expect(hallazgos(fueraDeTokens, /#[0-9a-fA-F]{3,8}\b/)).toEqual([]);
    expect(hallazgos(fueraDeTokens, /\b(rgb|rgba|hsl|hsla|oklch|color-mix)\(/)).toEqual([]);
    expect(hallazgos(fueraDeTokens, /\b\d*\.?\d+px\b/)).toEqual([]);
    // Colores con nombre en estilos (distintos de los valores semánticos currentColor/transparent).
    expect(hallazgos(fueraDeTokens.filter(([r]) => r.endsWith(".css")), /:\s*(white|black|red|blue|green|gray|grey)\b/i)).toEqual([]);
  });

  // Criterio: WEB-15.a
  it("sin gradientes ni emojis en web/src", () => {
    expect(hallazgos(todos, /gradient\(/)).toEqual([]);
    expect(hallazgos(todos, /\p{Extended_Pictographic}/u)).toEqual([]);
  });

  // Criterio: WEB-04.c
  it("sin variables VITE_* ni referencias a llaves en web/src", () => {
    expect(hallazgos(todos, /VITE_|import\.meta\.env|LLM_API_KEY|EMBEDDING_API_KEY|test-key-NOT-REAL/)).toEqual([]);
  });
});
