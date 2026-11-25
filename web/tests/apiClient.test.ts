import { describe, expect, it, vi } from "vitest";
import { getSalud, preguntar } from "../src/services/apiClient";
import { jsonResponse, mockApi, RESPUESTA_403_ACCESO, RESPUESTA_403_IDENTIDAD, RESPUESTA_OK } from "./helpers";

const PETICION = { pregunta: "  ¿Cuál es mi promedio?  ", usuario: { rol: "estudiante", id: " Est-001 " } } as const;

describe("cliente HTTP: contrato y mismo origen", () => {
  // Criterio: WEB-01.a
  it("preguntar hace POST /api/asistente/preguntar con el cuerpo exacto de SPEC §3.1", async () => {
    const api = mockApi();
    await preguntar(PETICION);

    expect(api.fetch).toHaveBeenCalledTimes(1);
    const [url, init] = api.fetch.mock.calls[0] ?? [];
    expect(url).toBe("/api/asistente/preguntar");
    expect(init?.method).toBe("POST");
    const headers = new Headers(init?.headers);
    expect(headers.get("Content-Type")).toBe("application/json");
    // Cuerpo: solo {pregunta, usuario:{rol,id}}, sin normalizar la pregunta ni el id.
    expect(JSON.parse(String(init?.body))).toEqual({
      pregunta: "  ¿Cuál es mi promedio?  ",
      usuario: { rol: "estudiante", id: " Est-001 " },
    });
    expect(Object.keys(JSON.parse(String(init?.body)) as object).sort()).toEqual(["pregunta", "usuario"]);
  });

  // Criterio: WEB-01.a
  it("getSalud hace GET /api/salud (ruta relativa, mismo origen)", async () => {
    const api = mockApi();
    const result = await getSalud();
    expect(result).toEqual({ kind: "ok" });
    const [url, init] = api.fetch.mock.calls[0] ?? [];
    expect(url).toBe("/api/salud");
    expect(init?.method).toBe("GET");
    expect(init?.body).toBeUndefined();
  });

  // Criterio: WEB-01.a
  it("nunca usa una URL absoluta ni otra ruta que las dos del contrato", async () => {
    const api = mockApi();
    await getSalud();
    await preguntar(PETICION);
    const urls = api.fetch.mock.calls.map(([u]) => String(u));
    expect(urls).toEqual(["/api/salud", "/api/asistente/preguntar"]);
    for (const u of urls) expect(u.startsWith("/api/")).toBe(true);
  });
});

describe("cliente HTTP: mapeo de estados a resultados tipados", () => {
  // Criterio: WEB-07.a
  it("400 problem+json -> validation con el title; nunca el cuerpo crudo", async () => {
    mockApi({
      preguntar: () => jsonResponse(400, { title: "Pregunta inválida", status: 400, detail: "secreto-interno" }),
    });
    const r = await preguntar(PETICION);
    expect(r).toEqual({ kind: "validation", title: "Pregunta inválida" });
    expect(JSON.stringify(r)).not.toContain("secreto-interno");
  });

  // Criterio: WEB-07.a
  it("400 sin title o con cuerpo no JSON -> validation sin title", async () => {
    mockApi({ preguntar: () => new Response("<html>boom</html>", { status: 400 }) });
    expect(await preguntar(PETICION)).toEqual({ kind: "validation", title: null });
  });

  // Criterio: WEB-07.b
  it("403 con el cuerpo del contrato -> forbidden con ese cuerpo (acceso e identidad)", async () => {
    mockApi({ preguntar: () => jsonResponse(403, RESPUESTA_403_ACCESO) });
    expect(await preguntar(PETICION)).toEqual({ kind: "forbidden", data: RESPUESTA_403_ACCESO });
    mockApi({ preguntar: () => jsonResponse(403, RESPUESTA_403_IDENTIDAD) });
    expect(await preguntar(PETICION)).toEqual({ kind: "forbidden", data: RESPUESTA_403_IDENTIDAD });
  });

  // Criterio: WEB-07.c
  it("503 -> unavailable", async () => {
    mockApi({ preguntar: () => jsonResponse(503, { title: "x" }) });
    expect(await preguntar(PETICION)).toEqual({ kind: "unavailable" });
  });

  // Criterio: WEB-07.c
  it("500 -> server con el X-Request-Id recibido, sin detalles internos", async () => {
    mockApi({
      preguntar: () => jsonResponse(500, { title: "Error", detail: "System.NullReferenceException at ..." }, { "X-Request-Id": "req-abc-123" }),
    });
    const r = await preguntar(PETICION);
    expect(r).toEqual({ kind: "server", requestId: "req-abc-123" });
    expect(JSON.stringify(r)).not.toContain("NullReference");
  });

  // Criterio: WEB-07.c
  it("500 sin X-Request-Id -> server con requestId nulo", async () => {
    mockApi({ preguntar: () => jsonResponse(500, {}) });
    expect(await preguntar(PETICION)).toEqual({ kind: "server", requestId: null });
  });

  // Criterio: WEB-07.d
  it("error de red -> network", async () => {
    mockApi({
      preguntar: () => {
        throw new TypeError("Failed to fetch");
      },
    });
    expect(await preguntar(PETICION)).toEqual({ kind: "network" });
  });

  // Criterio: WEB-07.d
  it("502/504 del servidor estático (API caída) -> network", async () => {
    mockApi({ preguntar: () => new Response("", { status: 502 }) });
    expect(await preguntar(PETICION)).toEqual({ kind: "network" });
    mockApi({ preguntar: () => new Response("", { status: 504 }) });
    expect(await preguntar(PETICION)).toEqual({ kind: "network" });
  });

  // Criterio: WEB-07.d
  it("timeout del cliente -> network", async () => {
    vi.useFakeTimers();
    try {
      mockApi({
        preguntar: (_url, init) =>
          new Promise<Response>((_resolve, reject) => {
            init?.signal?.addEventListener("abort", () => reject(new DOMException("abort", "AbortError")));
          }),
      });
      const pending = preguntar(PETICION, { timeoutMs: 1_000 });
      await vi.advanceTimersByTimeAsync(1_001);
      expect(await pending).toEqual({ kind: "network" });
    } finally {
      vi.useRealTimers();
    }
  });

  // Criterio: WEB-07.d
  it("200 con forma inesperada -> unexpected (sin exponer el cuerpo)", async () => {
    mockApi({ preguntar: () => jsonResponse(200, { respuesta: 42, citas: "no" }) });
    const r = await preguntar(PETICION);
    expect(r).toEqual({ kind: "unexpected" });
  });

  // Criterio: WEB-07.d
  it("200 que no es JSON y estados no contemplados -> unexpected", async () => {
    mockApi({ preguntar: () => new Response("texto plano", { status: 200 }) });
    expect(await preguntar(PETICION)).toEqual({ kind: "unexpected" });
    mockApi({ preguntar: () => jsonResponse(404, {}) });
    expect(await preguntar(PETICION)).toEqual({ kind: "unexpected" });
  });

  // Criterio: WEB-05.a
  it("200 válido -> ok con los datos tal cual (sin recalcular costoUsd ni tokens)", async () => {
    mockApi();
    const r = await preguntar(PETICION);
    expect(r).toEqual({ kind: "ok", data: RESPUESTA_OK });
  });

  it("cancelación del llamador -> cancelled (no es un error)", async () => {
    mockApi({
      preguntar: (_url, init) =>
        new Promise<Response>((_resolve, reject) => {
          init?.signal?.addEventListener("abort", () => reject(new DOMException("abort", "AbortError")));
        }),
    });
    const controller = new AbortController();
    const pending = preguntar(PETICION, { signal: controller.signal });
    controller.abort();
    expect(await pending).toEqual({ kind: "cancelled" });
  });

  // Criterio: WEB-09.a
  it("salud: 200 {estado:ok} -> ok; error de red, 500 o cuerpo distinto -> down", async () => {
    mockApi();
    expect(await getSalud()).toEqual({ kind: "ok" });
    mockApi({ salud: () => jsonResponse(200, { estado: "degradado" }) });
    expect(await getSalud()).toEqual({ kind: "down" });
    mockApi({ salud: () => jsonResponse(500, {}) });
    expect(await getSalud()).toEqual({ kind: "down" });
    mockApi({
      salud: () => {
        throw new TypeError("Failed to fetch");
      },
    });
    expect(await getSalud()).toEqual({ kind: "down" });
  });
});
