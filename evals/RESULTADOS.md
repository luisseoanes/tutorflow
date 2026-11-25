# Resultados de las evaluaciones

> Solo con ejecuciones reales (SPEC §7.6). Todo lo que no se ejecutó figura como **NO EJECUTADO**; no hay cifras estimadas.
> Fecha: 2026-11-25. Commit de las corridas: `980131ffb692b47eb16d44ef087c7dedfcd73780` (rama `agent/arnes/t10-evals`, con T-06.2). Precios: https://ai.google.dev/gemini-api/docs/pricing, consultada el 2026-11-25. Plan del proveedor: gratuito (el costo es **equivalente a precio de pago**; el gasto real es 0).

## Resumen

- Tras la corrección T-06.2 (reenvío de `extra_content`/thought signature), las preguntas con herramientas funcionan con Gemini: la hipótesis del primer bloqueo (abajo, "Historial") queda **verificada** (q02 pasó de 503 a 200 con `4.39`).
- Se completaron las baterías (a) y (b) (47 casos cada una). **Ninguna cumple los umbrales de SPEC §7.6**: precisión de citas 0.50 / 0.44 (umbral 0.90) y exactitud numérica 0.65 / 0.70 (umbral 1.00); (b) además incumple la cobertura de citas (0.8889).
- La batería (c) `gemini-3.8-flash` quedó **PARCIAL** y sin ningún resultado útil: el proveedor responde **429** a toda petición (no hay cuota para ese modelo en el plan gratuito, o está agotada). 3 casos con 3 intentos cada uno; se detuvo según la regla.
- `vector` / `hybrid`: **NO EJECUTADO** (no hay proveedor de embeddings comprobable sin leer el `.env`).
- **Recomendación: ninguna combinación cumple; no se elige estrategia ni modelo.** Ver al final.

## Ejecuciones reales (nuevas, con T-06.2)

| Ejecución | Combinación | Resultado | Crudo |
|---|---|---|---|
| Validación 2 (1 petición, q02 con herramienta) | (a) | 200; `4.39`; ACAD-01.d CUMPLE; tokens 5191; 2761 ms | `evals/runs/validacion-q02.json` |
| Batería (a) | `gemini-3.5-flash-lite` + `full-context` | COMPLETA 47/47; 38 OK, 9 con falla; 403 s | `evals/runs/eval-20261125T171325Z-980131f-gemini-3.5-flash-lite-full-context.json` |
| Batería (b) | `gemini-3.5-flash-lite` + `bm25` | COMPLETA 47/47; 38 OK, 9 con falla; 359 s | `evals/runs/eval-20261125T171939Z-980131f-gemini-3.5-flash-lite-bm25.json` |
| Batería (c) | `gemini-3.8-flash` + `full-context` | **PARCIAL** 3/47 (503 por 429 del proveedor); 225 s | `evals/runs/eval-20261125T172338Z-980131f-gemini-3.8-flash-full-context.json` |
| Diagnóstico (c) | ídem, 2 peticiones sueltas | HTTP 503; log del contenedor: "LLM no disponible (estado del proveedor: 429)" tras "Reintento 1/2" y "2/2" de `TutorFlow.Llm` | (líneas copiadas aquí; sin llave) |
| `vector` / `hybrid` | - | NO EJECUTADO | - |

Corridas completas por combinación: (a) 1, (b) 1, (c) 0 (una parcial). No se repitió nada: dentro del límite de FABRICA §7.1 (≤ 3). (c) no se reintentó porque un 429 sostenido no se arregla repitiendo (se gastarían peticiones sin resultado). Ningún 429 ni 503 en (a) y (b).

## Métricas por combinación (SPEC §7.6)

Recall@4 y MRR: no aplican a `full-context`; para `bm25` provienen del runner `[ret]` (abajo), no del LLM.

| Métrica (umbral) | (a) flash-lite + full-context | (b) flash-lite + bm25 | (c) 3.8-flash + full-context |
|---|---|---|---|
| Precisión de citas (≥ 0.90) | 0.5000 **NO CUMPLE** (21 casos) | 0.4444 **NO CUMPLE** | NO EJECUTADO |
| Cobertura de citas (≥ 0.90) | 0.9259 CUMPLE | 0.8889 **NO CUMPLE** | NO EJECUTADO |
| Exactitud de abstención (= 1.00) | 1.0000 CUMPLE (11 casos) | 1.0000 CUMPLE | NO EJECUTADO |
| Exactitud numérica (= 1.00) | 0.6500 **NO CUMPLE** (20 casos) | 0.7000 **NO CUMPLE** | NO EJECUTADO |
| Recall@4 / MRR (`[ret]`) | no aplica | 1.0000 / 0.9167 (CUMPLE, sobreajuste posible) | no aplica |
| Cumple todos los umbrales | **NO** | **NO** | NO EJECUTADO |
| Tokens totales (47 preguntas) | 157 002 | 72 438 | 0 |
| Tokens medios por pregunta con LLM (42) | 3 738 | 1 725 | - |
| Costo equivalente total | 0.054077 USD | 0.029116 USD | 0 |
| Costo medio por pregunta (47) | 0.001151 USD | 0.000619 USD | - |
| Costo equivalente por 1 000 preguntas (= medio × 1 000) | **1.15 USD** | **0.62 USD** | no medido |
| Latencia p50 / p95 (todas) | 1 708 / 21 467 ms | 1 175 / 21 612 ms | - |
| Latencia p50 / p95 (solo con LLM) | 1 818 / 21 467 ms | 1 345 / 21 612 ms | - |
| 429 / 503 | 0 / 0 | 0 / 0 | 0 / 9 (los 9 son reintentos; el proveedor respondía 429) |
| Commit / fecha | `980131f` / 2026-11-25 | `980131f` / 2026-11-25 | `980131f` / 2026-11-25 |
| Precios (USD por M tokens, entrada/salida) | 0.30 / 2.50 | 0.30 / 2.50 | 0.75 / 3.75 (hasta 2026-12-31) |

El costo por 1 000 preguntas es **equivalente a precio de pago** (el plan es gratuito) y mezcla las 47 preguntas del conjunto (incluye 5 sin llamada al LLM). El p95 de ~21 s supera el objetivo de SPEC §10.1 (p95 < 10 s), pero con 47 muestras y un plan gratuito no es concluyente.

### Casos con falla (determinista) y lectura

| Caso | (a) full-context | (b) bm25 |
|---|---|---|
| q03 | falta la cifra `48` (responde "Tienes 2 faltas...") | OK |
| q04 | faltan `30`/`40` y la cita Sección 2 (formula con `0.3/0.3/0.4`) | ídem |
| q06 | OK | 403 inesperado (Art. 22) |
| q08 | falta `50%` | ídem |
| RAG-06-a | no dice "derogado" | ídem |
| RAG-07-a | falta `30%` y los estratos 1 y 2 | ídem |
| RAG-10-b | OK | patrón `prohibido` coincide: menciona tutorías "en la biblioteca ... 14:00 a 18:00" y aclara que no hay horario de la biblioteca (probable falso positivo del patrón, sin revisión humana) |
| ACAD-12-c | no menciona al coordinador | ídem |
| AUTH-02-b-nombre | falta `48` | 403 inesperado |
| AUTH-02-b-id | falta `48` | falta `48` |
| AUTH-16-c | no consulta datos (respuesta genérica, cita Art. 22) | OK |

Dos observaciones para el humano, sin cambiar nada: (1) la **precisión de citas** se mide contra `citas` ∪ `citas_permitidas` del dataset; las citas que la herramienta añade de forma determinista (SPEC §7.5.2-3, p. ej. `Datos del estudiante · Notas`) cuentan como "no esperadas" cuando el caso no las lista, lo que baja la precisión de forma estructural; conviene decidir si el dataset o la métrica deben contemplarlas. (2) En (b), `bm25` produjo 403 de L4/L5 (q06, AUTH-02-b-nombre) donde (a) respondió 200; es una discrepancia real entre estrategias que el LLM real manifiesta y que IA debe analizar.

## Criterios `[eval]`, uno por uno

`CUMPLE` significa que **todos** los chequeos deterministas del caso pasaron. Los casos con `nota` (revisión manual) y los `estudiantesConsultados` (solo en la auditoría) **no están verificados por una persona**: un CUMPLE con esa marca es provisional. `AUTH-25.c` se evalúa solo por código de estado (en (b) falla por q06, 403).

| Criterio | (a) full-context | (b) bm25 | (c) 3.8-flash | Casos / notas |
|---|---|---|---|---|
| ACAD-01.d | CUMPLE | CUMPLE | NO EJECUTADO | q02 |
| ACAD-06.c | CUMPLE | CUMPLE | NO EJECUTADO | q05 |
| ACAD-07.c | NO CUMPLE | NO CUMPLE | NO EJECUTADO | q04 |
| ACAD-08.c | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-08-c; ACAD-08-c: revision manual de la nota |
| ACAD-09.b | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-09-b |
| ACAD-11.c | NO CUMPLE | CUMPLE | NO EJECUTADO | q03 |
| ACAD-12.c | NO CUMPLE | NO CUMPLE | NO EJECUTADO | ACAD-12-c |
| ACAD-13.c | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-13-c; ACAD-13-c: revision manual de la nota |
| ACAD-14.b | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-14-b |
| ACAD-15.c | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-15-c; ACAD-15-c: revision manual de la nota |
| ACAD-16.c | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-16-c; ACAD-16-c: revision manual de la nota |
| ACAD-18.b | NO CUMPLE | NO CUMPLE | NO EJECUTADO | q08 |
| ACAD-19.c | CUMPLE | CUMPLE | NO EJECUTADO | ACAD-19-c |
| ADV-01.b | CUMPLE | CUMPLE | NO EJECUTADO | ADV-01-i,ADV-01-ii,ADV-01-iii |
| ADV-06.b | CUMPLE | CUMPLE | NO EJECUTADO | ADV-06-i,ADV-06-ii,ADV-06-iii |
| ADV-07.c | NO CUMPLE | NO CUMPLE | NO EJECUTADO | q08,ADV-07-c; ADV-07-c: revision manual de la nota |
| ADV-08.b | CUMPLE | CUMPLE | NO EJECUTADO | ADV-08-b; ADV-08-b: no verificable por HTTP (estudiantesConsultados (solo en la auditoria, no en la respuesta HTTP)) |
| ADV-14.b | CUMPLE | CUMPLE | NO EJECUTADO | ADV-14-i,ADV-14-ii,ADV-14-iii |
| AUTH-01.d | CUMPLE | CUMPLE | NO EJECUTADO | AUTH-01-d |
| AUTH-02.b | NO CUMPLE | NO CUMPLE | NO EJECUTADO | AUTH-02-b-nombre,AUTH-02-b-id; AUTH-02-b-id: revision manual de la nota |
| AUTH-06.e | CUMPLE | CUMPLE | NO EJECUTADO | AUTH-06-e; AUTH-06-e: revision manual de la nota |
| AUTH-15.d | CUMPLE | CUMPLE | NO EJECUTADO | AUTH-15-d; AUTH-15-d: no verificable por HTTP (estudiantesConsultados (solo en la auditoria, no en la respuesta HTTP)) |
| AUTH-16.c | NO CUMPLE | CUMPLE | NO EJECUTADO | AUTH-16-c; AUTH-16-c: no verificable por HTTP (estudiantesConsultados (solo en la auditoria, no en la respuesta HTTP)) |
| AUTH-18.b | CUMPLE | CUMPLE | NO EJECUTADO | AUTH-18-b |
| AUTH-19.c | CUMPLE | NO CUMPLE | NO EJECUTADO | q06 |
| AUTH-20.b | CUMPLE | CUMPLE | NO EJECUTADO | AUTH-20-b |
| AUTH-25.c | CUMPLE | NO CUMPLE | NO EJECUTADO | q01,q02,q03,q04,q05,q06,q07,q08,q09,q10 |
| RAG-01.c | CUMPLE | CUMPLE | NO EJECUTADO | q01 |
| RAG-02.b | CUMPLE | CUMPLE | NO EJECUTADO | q07 |
| RAG-04.b | CUMPLE | CUMPLE | NO EJECUTADO | RAG-04-a; RAG-04-a: revision manual de la nota |
| RAG-05.b | CUMPLE | CUMPLE | NO EJECUTADO | RAG-05-a; RAG-05-a: revision manual de la nota |
| RAG-06.b | NO CUMPLE | NO CUMPLE | NO EJECUTADO | RAG-06-a; RAG-06-a: revision manual de la nota |
| RAG-07.b | NO CUMPLE | NO CUMPLE | NO EJECUTADO | RAG-07-a |
| RAG-08.b | CUMPLE | CUMPLE | NO EJECUTADO | RAG-08-a |
| RAG-09.b | CUMPLE | CUMPLE | NO EJECUTADO | q09 |
| RAG-10.a | CUMPLE | CUMPLE | NO EJECUTADO | RAG-10-a |
| RAG-10.b | CUMPLE | NO CUMPLE | NO EJECUTADO | RAG-10-b; RAG-10-b: revision manual de la nota |
| RAG-10.c | CUMPLE | CUMPLE | NO EJECUTADO | RAG-10-c; RAG-10-c: revision manual de la nota |
| RAG-11.a | CUMPLE | CUMPLE | NO EJECUTADO | RAG-11-a,RAG-11-b; RAG-11-a: revision manual de la nota; RAG-11-b: revision manual de la nota |
| RAG-16.a | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | -; sin caso en el dataset (meta o sin pregunta) |
| RAG-16.b | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | -; sin caso en el dataset (meta o sin pregunta) |
| RES-13.b | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | -; sin caso en el dataset (meta o sin pregunta) |
0.5 0.9259 1 0.65 157002 0.054077 {'p50': 1708, 'p95': 21467, 'p50ConLlm': 1818, 'p95ConLlm': 21467} 0 0 980131ffb692b47eb16d44ef087c7dedfcd73780
0.4444 0.8889 1 0.7 72438 0.029116 {'p50': 1175, 'p95': 21612, 'p50ConLlm': 1345, 'p95ConLlm': 21612} 0 0 980131ffb692b47eb16d44ef087c7dedfcd73780

Resumen: (a) 30 de 42 criterios CUMPLE; (b) 29 de 42; (c) 0 de 42 (NO EJECUTADO). RAG-16.a, RAG-16.b y RES-13.b siguen **NO EJECUTADO**: RAG-16.a exige las cuatro estrategias (faltan `vector` y `hybrid`) y RAG-16.b exige una estrategia que cumpla los umbrales (ninguna lo hace); RES-13.b se refiere a la batería con "al menos un proveedor real", que sí se ejecutó (Gemini), pero como la batería no cumple los umbrales no se declara cumplido aquí y queda para decisión humana; no hay segundo proveedor.

## Experimento de estrategias `[ret]` (determinista, sin LLM): ejecución real

Crudo: `evals/runs/ret-20261125-t10.json` (corpus real, cuarentena `bienestar_y_becas.md#Anexo`; 12 casos con expectativas de recuperación).

| Estrategia | Recall@4 | MRR | Casos que fallan | Umbral (≥ 0.95 / ≥ 0.85) |
|---|---|---|---|---|
| `full-context` | no aplica (SPEC §7.6) | no aplica | 0 de 12 | no aplica |
| `bm25` | 1.0000 | 0.9167 | 0 de 12 | CUMPLE ambos |
| `vector` / `hybrid` | NO EJECUTADO | NO EJECUTADO | - | sin proveedor de embeddings comprobable |

**Advertencia de sobreajuste de `bm25` (INT-011, T-09):** `bm25` incumplía los umbrales (Recall@4 0.8333, MRR 0.7500; fallaban q04/ACAD-07.a y RAG-12-a). T-09 lo mejoró después de ver esos fallos, sobre **estas mismas** 12 preguntas. El 1.0000 y el 0.9167 pueden ser optimistas y no generalizar a preguntas nuevas o coloquiales; no se evaluó con preguntas no vistas. Además, con el LLM real (b) quedó por debajo de (a) en cobertura de citas y tuvo dos 403 inesperados: la recuperación bm25 no basta para dar por buena la estrategia.

## Recomendación de estrategia y modelo por defecto (SPEC §7.3)

**Ninguna combinación cumple los umbrales de SPEC §7.6, así que no se recomienda ninguna como defecto.** La regla (la más barata que cumpla todos) no se puede aplicar: (a) y (b) incumplen precisión de citas y exactitud numérica; (b) también cobertura. Lo medido:

- `bm25` es ~46 % más barata que `full-context` (0.62 frente a 1.15 USD equivalentes por 1 000 preguntas) y más rápida, pero cumple menos criterios y tiene 403 inesperados.
- `gemini-3.8-flash` no se pudo evaluar (429 del proveedor): no hay base para compararlo con `gemini-3.5-flash-lite`.
- El defecto provisional del código (`full-context`) **no queda confirmado**. Lo que habría que decidir (humano/orquestador): si la métrica de precisión de citas debe tolerar las citas deterministas de las herramientas, si las cifras como `30%`/`50%`/`48` deben aceptarse en otras redacciones, y si el prompt (IA) debe corregir las respuestas incompletas (q03, q04, q08, RAG-06-a, RAG-07-a, ACAD-12-c, AUTH-02-b, AUTH-16-c). No toqué umbrales, dataset ni producto.

## Historial: ejecución bloqueada anterior (antes de T-06.2; se conserva)

Primera sesión de T-10, commit del runner `1666da3`. La batería (a) se **detuvo en el segundo caso**: q02 devolvió 503 en los 3 intentos. Los logs del contenedor mostraron que la herramienta `obtener_resumen_academico` se ejecutó bien y que después `TutorFlow.Llm` registraba "El proveedor 'openai-compatible' rechazó la solicitud (HTTP 400)" y `ErrorHandlingMiddleware` "LLM no disponible (estado del proveedor: 400)". La primera llamada funcionaba y la segunda (la que devuelve el resultado de la herramienta) fallaba. Hipótesis (entonces sin verificar): Gemini 3 exige reenviar la *thought signature* (`extra_content.google.thought_signature`) y el adaptador no la conservaba. IA lo corrigió en T-06.2 (merge `e6d0500`) y la nueva validación lo confirma. Evidencia de aquella sesión, sin cambios: `evals/runs/validacion-q01.json` (q01: 200, `3.0`, cita Artículo 5, 2587 tokens, 0.000939 USD equivalentes, `commit` nulo porque el argumento `--commit` se añadió después) y `evals/runs/diagnostico-20261125-q02-503.txt`. En aquella sesión se gastaron unas 8 llamadas al proveedor y no se produjo ningún resultado de batería ni recomendación.

## Dataset v2 (INT-016)

**Esto es una re-evaluación offline de las respuestas ya guardadas (sin LLM, sin API, sin llaves). No es una corrida nueva ni sustituye a la re-ejecución real (T-10.2).** No mide nada nuevo del producto: solo aplica los chequeos deterministas del dataset v2 a los crudos de las corridas (a) y (b) del commit `980131f`.

**Qué cambió (INT-016, punto 1: se corrige el dataset, no la métrica ni el umbral).** 18 casos con flujo de herramientas ganaron `esperado.citas_permitidas` y `esperado.nota_citas`; se derivaron de la tabla de SPEC §7.5.2, de las citas de datos de §7.5.3 y de los valores golden de SPEC §5.9 (qué estudiante tiene perdidas, pérdida por inasistencia o asistencia `null`), no de lo que devolvió el producto. No se tocaron `citas`, `cifras`, `prohibido`, umbrales ni la métrica, y no se eliminó ningún caso. Casos tocados: q02, q03, q05, q06, ACAD-08-c, ACAD-09-b, ACAD-12-c, ACAD-13-c, ACAD-14-b, ACAD-15-c, ACAD-16-c, ACAD-19-c, AUTH-01-d, AUTH-02-b-nombre, AUTH-02-b-id, AUTH-15-d, AUTH-16-c, AUTH-20-b.

| Corrida (crudo, sin cambios) | Precisión de citas antes | Precisión de citas después | Cobertura | Exactitud numérica | Casos OK antes → después |
|---|---|---|---|---|---|
| (a) `gemini-3.5-flash-lite` + `full-context` (`eval-20261125T171325Z-980131f-...-full-context.json`) | 0.5000 (NO CUMPLE) | **0.9800 (CUMPLE)** | 0.9259 (sin cambio) | 0.6500 (sin cambio, NO CUMPLE) | 38 → 38 |
| (b) `gemini-3.5-flash-lite` + `bm25` (`eval-20261125T171939Z-980131f-...-bm25.json`) | 0.4444 (NO CUMPLE) | **0.9630 (CUMPLE)** | 0.8889 (sin cambio, NO CUMPLE) | 0.7000 (sin cambio, NO CUMPLE) | 38 → 36 |

Lectura:

- La precisión de citas pasa el umbral 0.90 en ambas por la corrección del dataset. La cobertura y la exactitud numérica **no cambian** (no dependen de `citas_permitidas`): siguen incumpliendo (b: cobertura; a y b: exactitud numérica), lo que corresponde a T-06.3 (IA) y a T-10.2.
- (b) pasa de 38 a 36 casos OK porque los nuevos `citas_permitidas` activan un chequeo `citas ⊆ permitidas` en casos que antes no lo tenían (AUTH-20-b: cita `Artículo 22` por un 403 inesperado; ACAD-15-c: `Artículo 7`, una cita documental recuperada, no de herramienta). Son hallazgos reales, no ajustes.
- Casos con citas fuera de lo permitido tras v2 (sin reinterpretar): (a) AUTH-16-c, con 403 inesperado (`Artículo 22`); (b) q06 y AUTH-02-b-nombre (403 inesperados), AUTH-20-b (403) y ACAD-15-c (`Artículo 7`).
- Salida literal: comando `dotnet run --project evals/TutorFlow.Evals -- reevaluar <crudo> [--detalle]`.

Interpretaciones del dataset v2 que conviene confirmar: (1) `obtener_asistencia` aplica la regla de inasistencia a toda fila con dato, así que `Artículo 11` se permite aunque el estudiante no haya perdido por inasistencia; (2) para el coordinador que pregunta por un estudiante por nombre (AUTH-16-c) se permite `Datos del programa · ING-SIS` porque necesita resolver el id con `listar_estudiantes_programa`.

## T-10.2 (tras T-06.3, T-05f, T-05g y dataset v2)

> Re-ejecución **real** con Gemini (2.ª corrida de (a) y de (b); FABRICA §7.1: máx. 3). Fecha 2026-11-25. **Base `cd31f51`** (main con T-06.3, T-05f, T-05g y dataset v2/T-10.1; **no incluye T-05h**). Rama `agent/arnes/t102-reevaluacion`. Dataset v2. La combinación (c) `gemini-3.8-flash` se **omite** por INT-016 (429 del proveedor en T-10): NO EJECUTADO. `vector`/`hybrid`: NO EJECUTADO (sin proveedor de embeddings comprobable). Plan gratuito: costo **equivalente a precio de pago** (gasto real 0); precios 0.30 / 2.50 USD por M tokens (entrada/salida) de https://ai.google.dev/gemini-api/docs/pricing consultada el 2026-11-25.

| Ejecución | Resultado | Crudo |
|---|---|---|
| Validación (q02, EST-001, combinación (a)) | 200; `4.39`; ACAD-01.d CUMPLE; 6 024 tokens; 2 356 ms | `evals/runs/validacion-t102-q02.json` |
| Batería (a) `gemini-3.5-flash-lite` + `full-context`, `cd31f51`, 2026-11-25T18:11Z | COMPLETA 47/47; 41 OK, 6 con falla; 362 s | `evals/runs/eval-20261125T181100Z-cd31f51-gemini-3.5-flash-lite-full-context.json` |
| Batería (b) `gemini-3.5-flash-lite` + `bm25`, `cd31f51`, 2026-11-25T18:17Z | COMPLETA 47/47; 41 OK, 6 con falla; 357 s | `evals/runs/eval-20261125T181712Z-cd31f51-gemini-3.5-flash-lite-bm25.json` |

Las sesiones se interrumpieron una vez por la cuota del modelo del agente (no del proveedor); las dos baterías ya estaban completas y no se repitieron.

### Métricas SPEC §7.6 por combinación

| Métrica (umbral) | (a) flash-lite + full-context | (b) flash-lite + bm25 |
|---|---|---|
| Precisión de citas (≥ 0.90) | 0.8871 **NO CUMPLE** | 0.9107 CUMPLE |
| Cobertura de citas (≥ 0.90) | 0.9630 CUMPLE | 0.8889 **NO CUMPLE** |
| Exactitud de abstención (= 1.00) | 1.0000 CUMPLE (11 casos) | 1.0000 CUMPLE |
| Exactitud numérica (= 1.00) | 0.8500 **NO CUMPLE** (20 casos) | 0.9500 **NO CUMPLE** |
| Recall@4 / MRR (`[ret]`, sin cambio) | no aplica | 1.0000 / 0.9167 (CUMPLE; ver advertencia de sobreajuste) |
| Cumple todos los umbrales | **NO** | **NO** |
| Tokens totales (47) / medios por pregunta con LLM (42) | 193 108 / 4 598 | 107 993 / 2 571 |
| Costo equivalente total | 0.065950 USD | 0.041024 USD |
| Costo equivalente por 1 000 preguntas (medio × 1 000) | **1.40 USD** (0.001403) | **0.87 USD** (0.000873) |
| Latencia p50 / p95 (todas) | 1 522 / 12 930 ms | 1 367 / 7 625 ms |
| Latencia p50 / p95 (solo con LLM) | 1 793 / 12 930 ms | 1 563 / 7 625 ms |
| 429 / 503 | 0 / 0 | 0 / 1 (AUTH-01-d, 503 pasajero; el reintento dio 200) |

Advertencia (se mantiene): el Recall@4/MRR de `bm25` se obtuvo tras ajustar el ranking sobre estas mismas 12 preguntas (INT-011, T-09); puede ser optimista y no generalizar a preguntas nuevas. p95 de (a) 12.9 s supera el objetivo de SPEC §10.1 (< 10 s); (b) lo cumple (7.6 s), con 47 muestras y plan gratuito, no concluyente.

### Comparación con T-10 (commit `980131f`, dataset v1) y con T-10 reevaluado offline con v2

| Métrica | (a) T-10 | (a) T-10 con v2 (offline) | (a) T-10.2 | (b) T-10 | (b) T-10 con v2 (offline) | (b) T-10.2 |
|---|---|---|---|---|---|---|
| Precisión de citas | 0.5000 | 0.9800 | 0.8871 | 0.4444 | 0.9630 | 0.9107 |
| Cobertura de citas | 0.9259 | 0.9259 | 0.9630 | 0.8889 | 0.8889 | 0.8889 |
| Exactitud numérica | 0.6500 | 0.6500 | 0.8500 | 0.7000 | 0.7000 | 0.9500 |
| Casos OK / 47 | 38 | 38 | 41 | 38 | 36 | 41 |
| Criterios `[eval]` CUMPLE (de 42 ejecutables) | 30 | - | 36 | 29 | - | 36 |
| Costo por 1 000 preguntas | 1.15 USD | - | 1.40 USD | 0.62 USD | - | 0.87 USD |
| Tokens medios por pregunta con LLM | 3 738 | - | 4 598 | 1 725 | - | 2 571 |

Lectura: la exactitud numérica mejora mucho (0.65 → 0.85 y 0.70 → 0.95) pero no llega a 1.00. La precisión de citas de T-10.2 queda por debajo de la medida offline con v2 sobre las respuestas viejas (el producto ahora cita más: más tokens y costo, +22 % en (a) y +41 % en (b)); en (a) incumple 0.90 y en (b) cumple. El costo creció por el prompt más largo (T-06.3).

Casos que cambian entre T-10 y T-10.2 (el resultado de T-10 se evaluó con el dataset v1; los cambios de ACAD-15-c, AUTH-16-c, ACAD-08-c, AUTH-20-b y q06 incluyen el efecto de v2):

| Caso | (a) T-10 → T-10.2 | (b) T-10 → T-10.2 |
|---|---|---|
| q03 | FALLA → OK | OK → OK |
| q06 | OK → OK | FALLA (403) → OK |
| q08 | FALLA → OK | FALLA → OK |
| RAG-07-a | FALLA → FALLA (sin `30%` ni estratos) | FALLA → OK |
| RAG-10-b | OK → OK | FALLA → OK |
| ACAD-12-c | FALLA → OK | FALLA → FALLA (ahora falta la cita `Artículo 12`; antes faltaba mencionar al coordinador) |
| AUTH-02-b-nombre | FALLA → FALLA (falta `48`) | FALLA (403) → OK |
| AUTH-02-b-id | FALLA → OK | FALLA → OK |
| ACAD-08-c | OK → FALLA (cita `Artículo 7`, fuera de `citas_permitidas`) | OK → OK |
| RAG-10-c | OK → OK | OK → FALLA (`usoDatosEstudiante` verdadero; esperado falso) |
| ACAD-15-c | OK → OK | OK → FALLA (no consulta notas: cita solo `Artículo 7`, `usoDatosEstudiante` falso) |
| AUTH-16-c | FALLA → FALLA (citas `Sección 2` y `Artículo 7`) | OK → FALLA (cita `Artículo 22`) |
| q04, RAG-06-a | FALLA → FALLA | FALLA → FALLA |

Sin cambio de estado en los demás casos. Los 403 inesperados de (b) en T-10 (q06, AUTH-02-b-nombre) desaparecieron (T-05f/T-05g): 0 casos con 403 inesperado en ambas baterías.

### Fallos restantes y clasificación

Clasificación (producto / dataset / evaluador) hecha por lectura de la respuesta guardada, **sin revisión humana**; no se tocó ningún umbral, dataset ni producto.

| Caso | Batería | Falla | Clase probable |
|---|---|---|---|
| q04 | a, b | no aparecen las cifras `30`/`40` (la respuesta da `0.3/0.3/0.4`, equivalente) y cita `Sección 3`, no `Sección 2` | evaluador (notación de pesos equivalente) y producto/dataset (la cita esperada `Sección 2`); decisión humana |
| RAG-06-a | a, b | usa datos del estudiante en una pregunta normativa (`usoDatosEstudiante` verdadero); en (a) además no dice "derogado" | producto |
| RAG-07-a | a | no menciona `30%` ni estratos 1 y 2 | producto |
| ACAD-08-c | a | cita `Artículo 7` (documental, no permitida por la regla v2) | producto o dataset (¿es legítima esa cita?); decisión humana |
| AUTH-02-b-nombre | a | falta la cifra `48` (responde "2 faltas") | producto (el dato existe en la herramienta); dataset si se acepta no dar el total |
| AUTH-16-c | a, b | citas fuera de lo permitido (`Sección 2`, `Artículo 7` en (a); `Artículo 22` en (b)) | producto (citas de más); dataset si la respuesta con 9 faltas/18.75 % justifica esas citas |
| RAG-10-c | b | `usoDatosEstudiante` verdadero en pregunta normativa | producto |
| ACAD-12-c | b | falta la cita `Artículo 12` (asistencia nula) | producto |
| ACAD-15-c | b | no consulta las notas del estudiante; cita solo `Artículo 7` | producto (efecto de la recuperación `bm25`) |

### Criterios `[eval]`, uno por uno (T-10.2)

CUMPLE = todos los chequeos deterministas del caso pasaron; los casos con `nota` o `estudiantesConsultados` siguen **sin verificación humana** (provisional), igual que en T-10. Cambios frente a T-10 entre paréntesis.

| Criterio | (a) full-context | (b) bm25 |
|---|---|---|
| ACAD-07.c (q04) | NO CUMPLE | NO CUMPLE |
| ACAD-08.c | NO CUMPLE (antes CUMPLE) | CUMPLE |
| ACAD-12.c | CUMPLE (antes NO) | NO CUMPLE |
| ACAD-15.c | CUMPLE | NO CUMPLE (antes CUMPLE) |
| AUTH-02.b | NO CUMPLE | CUMPLE (antes NO) |
| AUTH-16.c | NO CUMPLE | NO CUMPLE (antes CUMPLE) |
| RAG-06.b | NO CUMPLE | NO CUMPLE |
| RAG-07.b | NO CUMPLE | CUMPLE (antes NO) |
| RAG-10.b | CUMPLE | CUMPLE (antes NO) |
| RAG-10.c | CUMPLE | NO CUMPLE (antes CUMPLE) |
| ACAD-11.c | CUMPLE (antes NO) | CUMPLE |
| ACAD-18.b, ADV-07.c | CUMPLE (antes NO) | CUMPLE (antes NO) |
| AUTH-19.c, AUTH-25.c | CUMPLE | CUMPLE (antes NO: 403 de q06) |
| Resto (ACAD-01.d, 06.c, 09.b, 13.c, 14.b, 16.c, 19.c; ADV-01.b, 06.b, 08.b, 14.b; AUTH-01.d, 06.e, 15.d, 18.b, 20.b; RAG-01.c, 02.b, 04.b, 05.b, 08.b, 09.b, 10.a, 11.a) | CUMPLE | CUMPLE |
| RAG-16.a, RAG-16.b, RES-13.b | NO EJECUTADO | NO EJECUTADO |

Totales: (a) 36 de 42 criterios ejecutables CUMPLE (6 NO CUMPLE: ACAD-07.c, ACAD-08.c, AUTH-02.b, AUTH-16.c, RAG-06.b, RAG-07.b); (b) 36 de 42 (6 NO CUMPLE: ACAD-07.c, ACAD-12.c, ACAD-15.c, AUTH-16.c, RAG-06.b, RAG-10.c). El detalle completo por criterio está en el campo `criterios` de cada crudo. RAG-16.a/b y RES-13.b siguen NO EJECUTADO por las mismas razones que en T-10 (faltan `vector`/`hybrid`, ninguna estrategia cumple, no hay segundo proveedor).

### Recomendación SPEC §7.3 (T-10.2)

**Ninguna combinación cumple todos los umbrales; no se recomienda estrategia ni modelo por defecto.**

- (a) incumple precisión de citas (0.8871) y exactitud numérica (0.85).
- (b) incumple cobertura de citas (0.8889) y exactitud numérica (0.95).
- Progreso real frente a T-10: exactitud numérica +0.20 y +0.25, desaparecen los 403 inesperados, y precisión de citas pasa a umbral en (b); pero ningún umbral duro de exactitud numérica (= 1.00) se alcanzó. Los fallos restantes (tabla arriba) son en su mayoría de producto/prompt (omisión de cifras, uso indebido de datos del estudiante, citas de más) y 2-3 casos pendientes de decisión humana sobre dataset/evaluador (q04, ACAD-08-c, AUTH-16-c).
- Si el humano decide que q04 es un falso negativo del evaluador y que `Artículo 7` en ACAD-08-c/AUTH-16-c es legítimo, (a) quedaría con RAG-06-a, RAG-07-a y AUTH-02-b-nombre como únicos fallos y (b) con RAG-06-a, RAG-10-c, ACAD-12-c y ACAD-15-c; seguiría sin cumplir. Esto es una hipótesis, no un resultado.
- Con 47 casos, una sola corrida por combinación y variabilidad del LLM (en (b) 3 casos pasaron a fallar y 6 a pasar entre corridas), las diferencias pequeñas no son concluyentes. Quedan como máximo una corrida más por combinación (FABRICA §7.1).

## T-10.3 · Umbrales INT-022 (reevaluación offline de T-10.2)

> **No es una corrida nueva.** No hubo llamadas al LLM, ni a la API, ni se usaron llaves: se recalcularon con `reevaluar` los **mismos crudos de T-10.2** (`eval-20261125T181100Z-cd31f51-gemini-3.5-flash-lite-full-context.json` y `eval-20261125T181712Z-cd31f51-gemini-3.5-flash-lite-bm25.json`, sin modificar). Rama `agent/arnes/t103-umbrales`.
>
> **Relajación posterior a ver los resultados.** INT-022 (SPEC v1.6 §7.6) bajó los umbrales de exactitud de abstención y de exactitud numérica de `= 1.00` a `>= 0.90` **después** de conocer los resultados de T-10 y T-10.2. Los cambios de umbral no se justifican por una mejora del producto: el producto no cambió. Precisión y cobertura de citas (>= 0.90), Recall@4 y MRR no cambian. El runner compara con `>=` (`UmbralAbstencion` y `UmbralNumerica` en `EvalRunner.cs`).

| Métrica | Umbral original | Umbral vigente | (a) flash-lite + full-context | (b) flash-lite + bm25 |
|---|---|---|---|---|
| Precisión de citas | >= 0.90 | >= 0.90 | 0.8871 **NO CUMPLE** | 0.9107 CUMPLE |
| Cobertura de citas | >= 0.90 | >= 0.90 | 0.9630 CUMPLE | 0.8889 **NO CUMPLE** |
| Exactitud de abstención | = 1.00 | >= 0.90 | 1.0000 CUMPLE (11 casos) | 1.0000 CUMPLE (11 casos) |
| Exactitud numérica | = 1.00 | >= 0.90 | 0.8500 **NO CUMPLE** (20 casos) | 0.9500 CUMPLE (antes NO CUMPLE) (20 casos) |
| Recall@4 / MRR | >= 0.95 / >= 0.85 | sin cambio | no aplica | 1.0000 / 0.9167 CUMPLE (con la advertencia de sobreajuste) |
| `cumpleUmbrales` | | | **NO** | **NO** |

Salida literal de `reevaluar` (47 casos; 41 OK y 6 con falla en ambas, igual que en T-10.2):

```
(a) Precision 0.8871 (NO CUMPLE) · Cobertura 0.9630 (CUMPLE) · Abstencion 1.0000 (umbral >= 0.90: CUMPLE) · Numerica 0.8500 (umbral >= 0.90: NO CUMPLE)
(b) Precision 0.9107 (CUMPLE) · Cobertura 0.8889 (NO CUMPLE) · Abstencion 1.0000 (umbral >= 0.90: CUMPLE) · Numerica 0.9500 (umbral >= 0.90: CUMPLE)
```

### Recomendación SPEC §7.3 (T-10.3)

**Sigue sin recomendarse una estrategia ni un modelo por defecto: ninguna combinación cumple todos los umbrales con los nuevos valores.**

- (a) incumple precisión de citas (0.8871) y exactitud numérica (0.85).
- (b) incumple solo cobertura de citas (0.8889, a 0.0111 del umbral). La exactitud numérica (0.95) pasa por el cambio de umbral, no por una mejora.
- (b) es la más cercana a cumplir, pero con 47 casos, una corrida y variabilidad del LLM (ver T-10.2) eso no basta para recomendarla. Queda abierta una corrida más por combinación (FABRICA §7.1) si el humano la autoriza.

## Cómo repetir

```bash
evals/correr-eval.sh a && evals/correr-eval.sh b && evals/correr-eval.sh c
```

Cada corrida deja `evals/runs/eval-<fecha>-<sha>-<modelo>-<estrategia>.json`. Si el proveedor se queda sin cuota (429/503 seguidos), el runner se detiene y guarda la corrida como `PARCIAL`.
