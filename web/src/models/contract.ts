/** DTO del contrato HTTP (SPEC §3.1). La web no añade ni transforma campos. */

export const ROLES = ["estudiante", "coordinador"] as const;
export type Rol = (typeof ROLES)[number];

export interface Usuario {
  rol: Rol;
  id: string;
}

export interface PreguntarRequest {
  pregunta: string;
  usuario: Usuario;
}

export interface Cita {
  documento: string;
  seccion: string;
}

export interface PreguntarResponse {
  respuesta: string;
  citas: Cita[];
  usoDatosEstudiante: boolean;
  tokens: number;
  costoUsd: number;
}

export interface SaludResponse {
  estado: "ok";
}

/** problem+json (RFC 7807): solo se lee `title`, si existe, y como texto. */
export interface ProblemDetails {
  title?: string;
}

/** Límites de UX (SPEC-WEB §4.1, D-12). La validación real es la del servidor. */
export const LIMITE_PREGUNTA = 1000;
export const LIMITE_ID = 32;
