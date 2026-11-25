import { describe, expect, it } from "vitest";
import { isCita, isPreguntarResponse, isProblemDetails, isSaludResponse } from "../src/validation/guards";
import { RESPUESTA_OK } from "./helpers";

describe("guards en tiempo de ejecución", () => {
  // Criterio: WEB-07.d
  it("acepta una PreguntarResponse válida, también con propiedades desconocidas", () => {
    expect(isPreguntarResponse(RESPUESTA_OK)).toBe(true);
    expect(isPreguntarResponse({ ...RESPUESTA_OK, extra: 1 })).toBe(true);
    expect(isPreguntarResponse({ ...RESPUESTA_OK, citas: [] })).toBe(true);
  });

  // Criterio: WEB-07.d
  it.each([
    ["null", null],
    ["array", []],
    ["string", "ok"],
    ["sin respuesta", { ...RESPUESTA_OK, respuesta: undefined }],
    ["respuesta no string", { ...RESPUESTA_OK, respuesta: 1 }],
    ["citas null", { ...RESPUESTA_OK, citas: null }],
    ["cita sin seccion", { ...RESPUESTA_OK, citas: [{ documento: "a" }] }],
    ["cita con tipos erróneos", { ...RESPUESTA_OK, citas: [{ documento: 1, seccion: 2 }] }],
    ["uso no booleano", { ...RESPUESTA_OK, usoDatosEstudiante: "si" }],
    ["tokens string", { ...RESPUESTA_OK, tokens: "10" }],
    ["tokens NaN", { ...RESPUESTA_OK, tokens: Number.NaN }],
    ["costo string", { ...RESPUESTA_OK, costoUsd: "0.1" }],
  ])("rechaza %s", (_nombre, valor) => {
    expect(isPreguntarResponse(valor)).toBe(false);
  });

  // Criterio: WEB-07.d
  it("isCita, isSaludResponse e isProblemDetails", () => {
    expect(isCita({ documento: "a", seccion: "b" })).toBe(true);
    expect(isCita({ documento: "a" })).toBe(false);
    expect(isSaludResponse({ estado: "ok" })).toBe(true);
    expect(isSaludResponse({ estado: "OK" })).toBe(false);
    expect(isSaludResponse(null)).toBe(false);
    expect(isProblemDetails({ title: "x", status: 400 })).toBe(true);
    expect(isProblemDetails({})).toBe(true);
    expect(isProblemDetails({ title: 5 })).toBe(false);
    expect(isProblemDetails("x")).toBe(false);
  });
});
