import type { PreguntarRequest, PreguntarResponse } from "../models/contract";
import { isPreguntarResponse, isProblemDetails, isSaludResponse } from "../validation/guards";

/**
 * Cliente HTTP único (SPEC-WEB §8.2). Base relativa `/api`, mismo origen; sin CORS.
 * Solo existen dos llamadas: GET /api/salud y POST /api/asistente/preguntar (SPEC §3.1).
 * Mapea cada estado HTTP a un resultado tipado; la web no decide nada de permisos (W-R3).
 */

export const API_BASE = "/api";
export const SALUD_PATH = `${API_BASE}/salud`;
export const PREGUNTAR_PATH = `${API_BASE}/asistente/preguntar`;

/** Tiempo máximo de espera de una pregunta (el LLM puede tardar). */
export const PREGUNTAR_TIMEOUT_MS = 60_000;
export const SALUD_TIMEOUT_MS = 5_000;

export type PreguntarOutcome =
  | { kind: "ok"; data: PreguntarResponse }
  /** 403 (acceso o identidad): cuerpo del contrato, mostrado tal cual. */
  | { kind: "forbidden"; data: PreguntarResponse }
  | { kind: "validation"; title: string | null }
  | { kind: "unavailable" }
  | { kind: "server"; requestId: string | null }
  | { kind: "network" }
  | { kind: "unexpected" }
  /** El usuario canceló el envío. */
  | { kind: "cancelled" };

export type SaludOutcome = { kind: "ok" } | { kind: "down" };

interface CallOptions {
  signal?: AbortSignal;
  timeoutMs?: number;
}

type RawResult =
  | { kind: "response"; response: Response }
  | { kind: "network" }
  | { kind: "cancelled" };

async function send(path: string, init: RequestInit, options: CallOptions, defaultTimeout: number): Promise<RawResult> {
  const controller = new AbortController();
  const external = options.signal;
  const onExternalAbort = (): void => controller.abort();
  if (external) {
    if (external.aborted) return { kind: "cancelled" };
    external.addEventListener("abort", onExternalAbort, { once: true });
  }
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, options.timeoutMs ?? defaultTimeout);
  try {
    const response = await fetch(path, { ...init, signal: controller.signal });
    return { kind: "response", response };
  } catch {
    if (!timedOut && external?.aborted) return { kind: "cancelled" };
    return { kind: "network" };
  } finally {
    clearTimeout(timer);
    external?.removeEventListener("abort", onExternalAbort);
  }
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

export async function getSalud(options: CallOptions = {}): Promise<SaludOutcome> {
  const raw = await send(SALUD_PATH, { method: "GET", headers: { Accept: "application/json" } }, options, SALUD_TIMEOUT_MS);
  if (raw.kind !== "response") return { kind: "down" };
  if (raw.response.status !== 200) return { kind: "down" };
  return isSaludResponse(await readJson(raw.response)) ? { kind: "ok" } : { kind: "down" };
}

export async function preguntar(request: PreguntarRequest, options: CallOptions = {}): Promise<PreguntarOutcome> {
  const body: PreguntarRequest = {
    pregunta: request.pregunta,
    usuario: { rol: request.usuario.rol, id: request.usuario.id },
  };
  const raw = await send(
    PREGUNTAR_PATH,
    {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(body),
    },
    options,
    PREGUNTAR_TIMEOUT_MS,
  );
  if (raw.kind === "cancelled") return { kind: "cancelled" };
  if (raw.kind === "network") return { kind: "network" };

  const { response } = raw;
  const status = response.status;

  if (status === 200 || status === 403) {
    const json = await readJson(response);
    if (!isPreguntarResponse(json)) return { kind: "unexpected" };
    return status === 200 ? { kind: "ok", data: json } : { kind: "forbidden", data: json };
  }
  if (status === 400) {
    const json = await readJson(response);
    const title = isProblemDetails(json) && typeof json.title === "string" && json.title.length > 0 ? json.title : null;
    return { kind: "validation", title };
  }
  if (status === 503) return { kind: "unavailable" };
  // 502/504: el servidor estático no alcanzó a la API (equivale a "sin conexión").
  if (status === 502 || status === 504) return { kind: "network" };
  if (status >= 500) return { kind: "server", requestId: response.headers.get("X-Request-Id") };
  return { kind: "unexpected" };
}
