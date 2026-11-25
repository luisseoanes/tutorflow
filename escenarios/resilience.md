# Escenarios — Resiliencia, contrato y operación

> Formato y niveles: `AGENTS.md` §6.

---

### RES-01 · Salud
- **Severidad:** Crítica
- **Precondiciones:** sin `LLM_API_KEY`.
- **Entrada:** `GET /api/salud`
- **Criterios:**
  - a. `[api]` 200 con el cuerpo exacto `{"estado":"ok"}`, aunque la fuente de datos y el roster fallen.
  - b. `[cont]` `docker compose up` en un *runner* limpio → 200 en menos de 30 s desde el arranque.

### RES-02 · Forma exacta del contrato
- **Severidad:** Crítica
- **Criterios:**
  - a. `[api]` Con un LLM guionizado, las 10 preguntas de ejemplo producen respuestas 200 o 403 que validan contra un JSON Schema del contrato con `additionalProperties: false` (en la raíz y en `citas[]`), con `tokens` entero ≥ 0 y `costoUsd` ≥ 0.
  - b. `[api]` Los cuerpos 403-acceso y 403-identidad validan contra el mismo esquema.
  - c. `[api]` `Content-Type` de la respuesta: `application/json`.

### RES-03 · Arranque con Docker Compose
- **Severidad:** Crítica
- **Entrada:** `cp .env.example .env && docker compose up -d`
- **Criterios:**
  - a. `[cont]` `tutorflow` (8080 publicado) y `mock-api` (solo red interna) quedan healthy; `q10/material/reglamento` se monta de solo lectura.
  - b. `[cont]` Sin `LLM_API_KEY`, AUTH-03 contra el contenedor real → 403-acceso (L2, sin LLM).

### RES-04 · La API simulada no está disponible
- **Severidad:** Alta · **Actor:** `EST-001`
- **Criterios:**
  - a. `[api]` Una fuente de datos falsa con timeout → **503** `problem+json` en menos de 10 s; nunca 200 con datos ni 403.
  - b. `[api]` Auditoría `resultado == 503`.

### RES-05 · El LLM no está disponible o la llave es inválida
- **Severidad:** Alta
- **Criterios:**
  - a. `[app]` Un proveedor que responde 401 → 503 sin reintentos (1 sola llamada).
  - b. `[app]` Un proveedor que responde 429 y luego 5xx → 3 llamadas en total y después 503; respeta `retry-after`.
  - c. `[api]` Con el LLM fallando, AUTH-03 sigue en 403-acceso y AUTH-08 en 403-identidad.
  - d. `[app]` Los logs capturados no contienen `test-key-NOT-REAL`.

### RES-06 · El LLM devuelve una salida inválida
- **Severidad:** Alta
- **Criterios:**
  - a. `[app]` Texto no JSON → 1 reintento con corrección; si persiste → 200 con abstención.
  - b. `[app]` JSON sin `respuesta` → igual que a.

### RES-07 · Tokens y costo exactos
- **Severidad:** Alta
- **Criterios:**
  - a. `[app]` Un LLM guionizado con 2 llamadas de *usage* (1000/200) y (1200/150), y precios 1.00/5.00 USD por millón → `tokens == 2550` y `costoUsd == 0.003950m`.
  - b. `[app]` Sin precios configurados → `costoUsd == 0`.
  - c. `[app]` Los adaptadores `anthropic` y `openai-compatible` extraen el *usage* correcto de respuestas HTTP grabadas (sin red).

### RES-08 · La auditoría se escribe en todos los caminos
- **Severidad:** Alta
- **Criterios:**
  - a. `[api]` Una petición de cada tipo (200, 400, 403-identidad, 403-acceso por L2, 403-acceso por L4, 503) produce **exactamente un** evento `pregunta` en `stdout` con `requestId`, `usuario` (o `null` si no es legible), `pregunta`, `estudiantesConsultados` y `resultado`.
  - b. `[api]` Con un archivo de auditoría que falla al escribir (doble que lanza una excepción), la petición se responde igual (200) y se registra un error de operación (SPEC §9.3).

### RES-09 · Errores sin filtración interna
- **Severidad:** Alta
- **Criterios:**
  - a. `[api]` Una excepción no controlada en una herramienta → 500 `problem+json` con `requestId`; sin *stack trace*, rutas, nombres de clases ni configuración.

### RES-10 · Cultura del proceso
- **Severidad:** Alta
- **Criterios:**
  - a. `[app]` Con `CultureInfo.CurrentCulture = es-CO`, las herramientas serializan `4.39` (punto), no `4,39`.

### RES-11 · Documentos ausentes
- **Severidad:** Media
- **Criterios:**
  - a. `[api]` Un `KNOWLEDGE_PATH` vacío → la aplicación arranca; `GET /api/salud` responde 200; una pregunta normativa (con un LLM guionizado sin citas) → 200 con abstención.
  - b. `[app]` Un documento adicional válido en `KNOWLEDGE_PATH` se indexa sin cambiar el código.

### RES-12 · Datos distintos a los originales
- **Severidad:** Alta
- **Criterios:**
  - a. `[api]` Una fuente de datos alternativa con `EST-007` (ADM, notas conocidas) → `COORD-02` obtiene sus datos y `EST-007` los suyos, sin cambios de código.
  - b. `[dom]` Prueba de arquitectura: ningún archivo de `src/` contiene `EST-00`, `COORD-0`, `Laura` ni `Andrés`.

### RES-13 · Proveedor intercambiable
- **Severidad:** Media
- **Criterios:**
  - a. `[app]` `LLM_PROVIDER=anthropic` y `openai-compatible` resuelven sus adaptadores; un valor desconocido falla al arrancar con un error de configuración claro.
  - b. `[eval]` La batería `[eval]` se ejecuta con al menos un proveedor real; si hay un segundo disponible, se reporta por separado en `RESULTADOS.md`.
