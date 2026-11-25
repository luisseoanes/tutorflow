import type { Cita, PreguntarResponse, ProblemDetails, SaludResponse } from "../models/contract";

/**
 * Guards en tiempo de ejecución (SPEC-WEB §8.2) sobre respuestas externas, escritos a mano.
 * Aceptan propiedades desconocidas (SPEC §3.2: se ignoran) pero exigen los tipos del contrato.
 */

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function isCita(value: unknown): value is Cita {
  return (
    isRecord(value) && typeof value["documento"] === "string" && typeof value["seccion"] === "string"
  );
}

export function isPreguntarResponse(value: unknown): value is PreguntarResponse {
  if (!isRecord(value)) return false;
  const { respuesta, citas, usoDatosEstudiante, tokens, costoUsd } = value;
  return (
    typeof respuesta === "string" &&
    Array.isArray(citas) &&
    citas.every(isCita) &&
    typeof usoDatosEstudiante === "boolean" &&
    typeof tokens === "number" &&
    Number.isFinite(tokens) &&
    typeof costoUsd === "number" &&
    Number.isFinite(costoUsd)
  );
}

export function isSaludResponse(value: unknown): value is SaludResponse {
  return isRecord(value) && value["estado"] === "ok";
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (!isRecord(value)) return false;
  const title = value["title"];
  return title === undefined || typeof title === "string";
}
