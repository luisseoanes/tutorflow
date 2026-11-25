import axe from "axe-core";
import { vi } from "vitest";
import type { Mock } from "vitest";

export const RESPUESTA_OK = {
  respuesta: "Tu promedio del periodo es el indicado por la API.\n\n- Primer punto\n- Segundo punto",
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
  respuesta:
    "No tienes permiso para consultar esa información. Solo puedes consultar tus propios datos académicos.",
  citas: [{ documento: "reglamento_academico_2025.md", seccion: "Artículo 22" }],
  usoDatosEstudiante: false,
  tokens: 0,
  costoUsd: 0,
};

export const RESPUESTA_403_IDENTIDAD = {
  respuesta: "No fue posible validar la identidad indicada en la solicitud.",
  citas: [],
  usoDatosEstudiante: false,
  tokens: 0,
  costoUsd: 0,
};

export function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", ...headers },
  });
}

export type Handler = (url: string, init: RequestInit | undefined) => Response | Promise<Response>;

interface ApiMock {
  fetch: Mock<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>;
  /** Peticiones POST a preguntar, en orden: cuerpo ya parseado. */
  preguntas: () => unknown[];
}

/** Instala un `fetch` simulado. Por defecto: salud ok y 200 con RESPUESTA_OK. */
export function mockApi(opts: { salud?: Handler; preguntar?: Handler } = {}): ApiMock {
  const salud: Handler = opts.salud ?? (() => jsonResponse(200, { estado: "ok" }));
  const preguntar: Handler = opts.preguntar ?? (() => jsonResponse(200, RESPUESTA_OK));
  const fn = vi.fn(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.pathname : input.url;
    if (url === "/api/salud") return salud(url, init);
    if (url === "/api/asistente/preguntar") return preguntar(url, init);
    throw new Error(`Petición inesperada a ${url}`);
  });
  vi.stubGlobal("fetch", fn);
  return {
    fetch: fn,
    preguntas: () =>
      fn.mock.calls
        .filter(([input]) => input === "/api/asistente/preguntar")
        .map(([, init]) => JSON.parse(String(init?.body)) as unknown),
  };
}

/** Violaciones axe serious/critical (el contraste real se verifica en el navegador: jsdom no calcula estilos). */
export async function violacionesGraves(container: Element): Promise<string[]> {
  const results = await axe.run(container, {
    rules: { "color-contrast": { enabled: false } },
  });
  return results.violations
    .filter((v) => v.impact === "serious" || v.impact === "critical")
    .map((v) => `${v.id}: ${v.help} (${v.nodes.length})`);
}
