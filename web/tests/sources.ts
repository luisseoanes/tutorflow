import { readdirSync, readFileSync } from "node:fs";

// Vitest se ejecuta desde web/ (npm test).
const RAIZ_SRC = `${process.cwd()}/src`;

/** Todos los archivos de web/src (ts, tsx, css) con su contenido real (se lee del disco: vitest vacía los CSS importados). */
export function archivosFuente(): [string, string][] {
  return readdirSync(RAIZ_SRC, { recursive: true })
    .filter((ruta) => /\.(ts|tsx|css)$/.test(ruta))
    .sort()
    .map((ruta): [string, string] => [`src/${ruta.replaceAll("\\", "/")}`, readFileSync(`${RAIZ_SRC}/${ruta}`, "utf8")]);
}

export function leerFuente(rutaRelativaASrc: string): string {
  return readFileSync(`${RAIZ_SRC}/${rutaRelativaASrc}`, "utf8");
}
