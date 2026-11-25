# Escenarios — Resiliencia, contrato y operación

---

### RES-01 · Salud
- **Severidad:** Crítica
- **Actor:** cliente HTTP
- **Precondiciones:** `docker compose up` en una máquina limpia, **sin** `LLM_API_KEY`.
- **Entrada:** `GET http://localhost:8080/api/salud`
- **Resultado esperado:** 200 con el cuerpo exacto `{"estado":"ok"}` en menos de 30 s desde el arranque.
- **Criterios de aceptación:** la salud no depende del LLM ni de la API simulada.

### RES-02 · Forma exacta del contrato
- **Severidad:** Crítica
- **Actor:** prueba de contrato (`WebApplicationFactory` + LLM `fake`)
- **Entrada:** las 10 preguntas de ejemplo.
- **Resultado esperado:** cada 200 tiene exactamente las propiedades `respuesta` (string), `citas` (array de `{documento: string, seccion: string}`), `usoDatosEstudiante` (bool), `tokens` (entero ≥ 0) y `costoUsd` (número ≥ 0); `Content-Type` es `application/json`.
- **Criterios de aceptación:** validación con JSON Schema con `additionalProperties: false` (decisión: no se añaden propiedades fuera del contrato).

### RES-03 · Arranque con Docker Compose
- **Severidad:** Crítica
- **Entrada:** `cp .env.example .env && docker compose up -d`
- **Resultado esperado:** los servicios `tutorflow` (puerto 8080 publicado) y `mock-api` (solo red interna) quedan healthy; el reglamento se monta de solo lectura desde el material original.
- **Criterios de aceptación:** un *smoke test* de CI ejecuta RES-01 + AUTH-03 (403 sin LLM) contra el contenedor real.

### RES-04 · La API simulada no está disponible
- **Severidad:** Alta
- **Actor:** `EST-001`
- **Precondiciones:** `mock-api` detenida.
- **Entrada:** `"¿Cuál es mi promedio?"`
- **Resultado esperado:** **503** `problem+json` en menos de 10 s (timeout de 3 s + reintentos); **nunca** 200 con datos inventados ni 403 engañoso.
- **Criterios de aceptación:** la auditoría registra un `resultado=503`.

### RES-05 · El LLM no está disponible o la llave es inválida
- **Severidad:** Alta
- **Precondiciones:** el proveedor devuelve 401, 429 o 5xx, o hay un timeout.
- **Resultado esperado:** 401 → 503 inmediato (sin reintentar); 429/5xx → 2 reintentos con *backoff* y después 503; los 403 deterministas (AUTH-03, AUTH-08) siguen respondiendo 403 sin LLM.
- **Criterios de aceptación:** los logs no contienen la llave.

### RES-06 · El LLM devuelve una salida inválida
- **Severidad:** Alta
- **Actor:** prueba de aplicación con un LLM *fake* que devuelve texto no JSON, JSON sin `respuesta` o un bucle infinito de herramientas.
- **Resultado esperado:** 1 reintento de corrección y después abstención; el bucle se corta en 3 rondas; status 200 con una abstención (no 500).

### RES-07 · Tokens y costo coherentes
- **Severidad:** Alta
- **Actor:** prueba de aplicación con un LLM *fake* que reporta un *usage* conocido (2 llamadas: 1000/200 y 1200/150 tokens) y precios configurados de 1.00/5.00 USD por millón.
- **Resultado esperado:** `tokens = 2550`; `costoUsd = (2200×1.00 + 350×5.00)/1e6 = 0.003950`.
- **Criterios de aceptación:** igualdad exacta (en `decimal`); proveedor local o `fake` sin precios → `costoUsd = 0`.

### RES-08 · La auditoría se escribe en todos los caminos
- **Severidad:** Alta
- **Entrada:** una petición de cada tipo: 200, 400, 403 (L1), 403 (L2), 403 (L4) y 503.
- **Resultado esperado:** exactamente un registro por petición con `requestId`, `usuario`, `pregunta`, `estudiantesConsultados` y `resultado`.
- **Criterios de aceptación:** si la escritura de auditoría falla (*fake* que lanza una excepción) → 500 y no se devuelven datos.

### RES-09 · Errores sin filtración interna
- **Severidad:** Alta
- **Entrada:** forzar una excepción no controlada en una herramienta.
- **Resultado esperado:** 500 `problem+json` con `requestId`; sin *stack trace*, rutas, nombres de clases ni la configuración.

### RES-10 · Cultura del contenedor
- **Severidad:** Alta
- **Precondiciones:** el contenedor con `LANG=es_CO.UTF-8` / `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`.
- **Entrada:** ACAD-01.
- **Resultado esperado:** la respuesta contiene `4.39` (punto), no `4,39`.

### RES-11 · Indexación ante documentos ausentes o vacíos
- **Severidad:** Media
- **Precondiciones:** `KNOWLEDGE_PATH` vacío o inexistente.
- **Resultado esperado:** la aplicación arranca, la salud responde 200 y escribe un error en el log; las preguntas normativas → abstención (no 500). Con un documento adicional válido, este se indexa sin cambiar el código.

### RES-12 · Datos de la API distintos a los originales
- **Severidad:** Alta
- **Precondiciones:** la API simulada sirve un `datos.json` alternativo (otro estudiante `EST-007` en ADM con notas conocidas).
- **Resultado esperado:** los cálculos y permisos funcionan sin cambios de código: **nada de ids, nombres ni notas fijos en el código**.
- **Criterios de aceptación:** una búsqueda en `src/` de `EST-00`, `COORD-0`, `Laura` o `Andrés` no encuentra coincidencias fuera de las pruebas.

### RES-13 · Proveedor intercambiable
- **Severidad:** Media
- **Entrada:** la misma batería de evals deterministas con `LLM_PROVIDER=fake`, y la batería E2E con `anthropic` y con `openai-compatible` (si hay una llave o un modelo local disponibles).
- **Resultado esperado:** cambiar de proveedor solo requiere variables de entorno; los resultados de cada proveedor se reportan por separado en `evals/RESULTADOS.md`.
