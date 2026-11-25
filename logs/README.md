# /logs — Transcripciones de las sesiones de agentes

Entregable exigido por el enunciado (regla lights-out 5). Contiene las transcripciones **sanitizadas** de cada sesión de agente (orquestador y subagentes).

## Convención de nombres

`<AAAA-MM-DD>_<fase>_<rol>_<id-corto>.jsonl` + `<mismo-nombre>.md` (resumen: tarea, resultado, commits, tokens).

## Origen

Claude Code guarda cada sesión como JSONL en `~/.claude/projects/<proyecto>/<sesion>.jsonl` (los subagentes en su subcarpeta). Se copian primero a `logs/raw/` (ignorada por git) y después se sanitizan hacia `logs/`.

## Sanitizado (obligatorio: el repositorio es público)

Se eliminan o enmascaran **solo** los datos personales y secretos, sin alterar el contenido técnico:

- Direcciones de correo electrónico (`[EMAIL]`).
- Llaves y tokens (patrones de `.gitleaks.toml` → `[SECRETO]`).
- Contenido de archivos `.env`.

Antes de cada commit en `logs/`, gitleaks se ejecuta con la regla `tutorflow-email-en-logs`.

## Estado

| Sesión | Fase | Estado |
|---|---|---|
| Análisis + especificación (F0) | F0 | Pendiente de exportar al cierre de la sesión |
