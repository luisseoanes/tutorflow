# Resultados de las evaluaciones

> Solo con ejecuciones reales (SPEC §7.6). Todo lo que no se ejecutó figura como **NO EJECUTADO**; no hay cifras estimadas.
> Fecha de las ejecuciones: 2026-11-25. Commit del runner: `1666da3` (rama `agent/arnes/t10-evals`). Precios: https://ai.google.dev/gemini-api/docs/pricing, consultada el 2026-11-25.

## Resumen

**La batería `[eval]` con el LLM real NO pudo completarse (T-10 bloqueada por un defecto del producto, no por cuota).** La primera pregunta normativa responde bien, pero toda pregunta que necesita una herramienta (datos del estudiante o del programa) termina en 503: el proveedor (Gemini, endpoint compatible con OpenAI) rechaza con **HTTP 400** la segunda llamada, la que devuelve el resultado de la herramienta. Se detuvo la batería tras el segundo caso para no gastar cuota (instrucción del brief) y no se ejecutó ninguna combinación completa. Por eso **no hay recomendación de estrategia ni de modelo** y ningún umbral de SPEC §7.6 puede darse por cumplido o incumplido.

Quién debe actuar: IA (`OpenAiCompatibleLlmClient`), y después repetir la batería (a), (b) y (c). Detalle en [Hallazgo](#hallazgo-bloqueante).

## Ejecuciones reales

| Ejecución | Combinación | Resultado | Crudo |
|---|---|---|---|
| Validación (1 petición, q01) | (a) `gemini-3.5-flash-lite` + `full-context` | 200; cita esperada presente; `3.0` en la respuesta; tokens 2587 > 0; 953 ms | `evals/runs/validacion-q01.json` (corrió antes de añadir `--commit`, por eso `commit` es `null`; el runner de esa corrida es el del commit `1666da3` menos ese argumento) |
| Batería (a) | (a) | **DETENIDA** en q02 (503 en los 3 intentos; HTTP 400 del proveedor). q01 repitió 200 OK. Sin crudo de corrida | `evals/runs/diagnostico-20261125-q02-503.txt` |
| Batería (b) | `gemini-3.5-flash-lite` + `bm25` | **NO EJECUTADA** (mismo defecto; se detuvo según el brief) | - |
| Batería (c) | `gemini-3.8-flash` + `full-context` | **NO EJECUTADA** (ídem) | - |
| `vector` / `hybrid` | - | **NO EJECUTADO**: no hay proveedor de embeddings configurado que se pueda comprobar sin leer el `.env` | - |

Cuota gastada: 1 petición de validación + 1 repetición de q01 + 3 intentos de q02 (cada uno con 2 llamadas al proveedor, la segunda rechazada con 400), unas 8 llamadas en total. 0 respuestas 429; 3 respuestas 503 (todas por el 400 del proveedor).

### Validación (la única ejecución completa de un caso)

- Petición: pregunta 1 del material, usuario `EST-001`, combinación (a).
- HTTP 200; respuesta: "La nota mínima para aprobar una asignatura es 3.0 en una escala de 0.0 a 5.0."; cita `reglamento_academico_2025.md · Artículo 5`.
- `tokens` = 2587; `costoUsd` = 0.000939 (**equivalente a precio de pago** con 0.30/2.50 USD por millón; el gasto real en el plan gratuito es 0); latencia 953 ms en esa ejecución y 1543 ms en la repetición de la batería.
- Precisión de citas 1.0000, cobertura 1.0000, abstención 1.0000, exactitud numérica 1.0000, pero sobre **1 caso**: no es una evaluación de los umbrales.

## Hallazgo bloqueante

- **Síntoma:** q02 ("¿Cuál es mi promedio ponderado del periodo?", `EST-001`) devuelve 503 en los 3 intentos (1 + 2 reintentos con backoff de 20 s y 40 s).
- **Causa observada en los logs del contenedor:** la herramienta `obtener_resumen_academico` se ejecutó con éxito (permitida para `EST-001`; la API simulada respondió 200), y después `TutorFlow.Llm`: "El proveedor 'openai-compatible' rechazó la solicitud (HTTP 400)", seguido de "LLM no disponible (estado del proveedor: 400)". La primera llamada (que pide la herramienta) funciona; falla la segunda.
- **No es cuota:** un 400 no es un 429 y reintentar no lo cambia. Los límites diarios del plan gratuito siguen sin conocerse (INT-013): no se llegó a ellos.
- **Hipótesis (no verificada; no se vio el cuerpo del 400 ni se gastó más cuota):** los modelos Gemini 3 exigen reenviar la *thought signature* de la llamada a función en el turno siguiente (en el endpoint compatible con OpenAI viaja en `extra_content.google.thought_signature` del `tool_call`). `src/TutorFlow.Infrastructure/Llm/OpenAiCompatibleLlmClient.cs` reconstruye los `tool_calls` solo con id, nombre y argumentos. Es un riesgo ya anticipado en INT-012 e INT-013 (llamada a herramientas por el endpoint compatible nunca probada contra Gemini).
- **Alcance:** afecta a toda pregunta con herramientas (q02, q03, q05, q06, ACAD-*, AUTH-*, etc.); las preguntas normativas sin herramientas (q01) funcionan. Es un defecto del producto (ruta de IA); el Arnés no modifica `src/`.
- **Prueba que falta:** el LLM guionizado de las pruebas no puede detectar esto; solo una corrida con el proveedor real lo muestra.

## Criterios `[eval]`, uno por uno

Resultado por criterio, comprobación determinista contra `esperado`. Solo se ejecutó el caso q01.

| Criterio | Estado | Evidencia |
|---|---|---|
| RAG-01.c | **CUMPLE** | q01: 200, `3.0` en la respuesta, cita `reglamento_academico_2025.md · Artículo 5`, sin citas al 2023, `usoDatosEstudiante == false` (`validacion-q01.json`) |
| AUTH-25.c | NO EJECUTADO | solo q01 de las 10 preguntas (200); q02..q10 sin ejecutar |
| ACAD-01.d, ACAD-06.c, ACAD-07.c, ACAD-08.c, ACAD-09.b, ACAD-11.c, ACAD-12.c, ACAD-13.c, ACAD-14.b, ACAD-15.c, ACAD-16.c, ACAD-18.b, ACAD-19.c | NO EJECUTADO | requieren herramientas (hallazgo) o no se alcanzaron |
| ADV-01.b, ADV-06.b, ADV-07.c, ADV-08.b, ADV-14.b | NO EJECUTADO | no se alcanzaron |
| AUTH-01.d, AUTH-02.b, AUTH-06.e, AUTH-15.d, AUTH-16.c, AUTH-18.b, AUTH-19.c, AUTH-20.b | NO EJECUTADO | no se alcanzaron |
| RAG-02.b, RAG-04.b, RAG-05.b, RAG-06.b, RAG-07.b, RAG-08.b, RAG-09.b, RAG-10.a, RAG-10.b, RAG-10.c, RAG-11.a | NO EJECUTADO | no se alcanzaron |
| RAG-16.a | NO EJECUTADO | falta la corrida del experimento de estrategias con el LLM real (la parte `[ret]` está abajo) |
| RAG-16.b | NO EJECUTADO | sin corrida completa no hay estrategia que cumpla los umbrales de SPEC §7.6 ni la más barata entre las que los cumplen |
| RES-13.b | **NO EJECUTADO** | la batería no se ejecutó con ningún proveedor real; solo hay un proveedor configurado (Gemini); sin segundo proveedor, nada que reportar por separado |

Limitaciones del runner que se mantendrán al repetir la batería: las expectativas `estudiantesConsultados` (AUTH-15.d, ADV-08.b) solo existen en la auditoría y no en la respuesta HTTP, y los casos con `nota` (p. ej. RAG-04.b: "no presenta el 20% como vigente") se marcan `revisionManual`; ninguno de los dos se dará por cumplido automáticamente.

## Experimento de estrategias

### Recuperación `[ret]` (determinista, sin LLM): ejecución real

Crudo: `evals/runs/ret-20261125-t10.json` (corpus real, cuarentena `bienestar_y_becas.md#Anexo`; 12 casos con expectativas de recuperación; estrategia por defecto del código: `full-context`).

| Estrategia | Recall@4 | MRR | Casos que fallan | Umbral (≥ 0.95 / ≥ 0.85) | Coste en tokens de entrada |
|---|---|---|---|---|---|
| `full-context` | no aplica (SPEC §7.6) | no aplica | 0 de 12 (los relevantes están presentes) | no aplica | todos los chunks vigentes y confiables (unos 2 500 tokens); medido en la validación: 2587 tokens totales para q01 |
| `bm25` | 1.0000 | 0.9167 | 0 de 12 | CUMPLE ambos | menor (top 4); **no medido con el LLM** |
| `vector` | NO EJECUTADO | NO EJECUTADO | - | - | sin proveedor de embeddings comprobable |
| `hybrid` | NO EJECUTADO | NO EJECUTADO | - | - | ídem |

**Advertencia de sobreajuste de `bm25` (INT-011, T-09):** al principio `bm25` incumplía los umbrales (Recall@4 0.8333 y MRR 0.7500; fallaban q04/ACAD-07.a y RAG-12-a). Después de ver esos fallos, T-09 mejoró la normalización y el ranking sobre **estas mismas** preguntas del dataset. El 1.0000 y el 0.9167 de arriba se midieron sobre un conjunto de solo 12 casos con el que se ajustó el algoritmo: es posible que sean optimistas y no generalicen a preguntas nuevas o coloquiales. No se evaluó con preguntas no vistas. Tómese como un indicio, no como una garantía.

### Con el LLM real: NO EJECUTADO

Sin la corrida de las combinaciones no hay precisión/cobertura de citas, exactitud de abstención, exactitud numérica, tokens por pregunta ni latencias para ninguna estrategia.

| Combinación | Precisión citas (≥ 0.90) | Cobertura citas (≥ 0.90) | Abstención (= 1.00) | Numérica (= 1.00) | Tokens | Costo medio / pregunta | Costo equiv. 1 000 preguntas | p50 / p95 | 429 / 503 | Commit / fecha | Crudo |
|---|---|---|---|---|---|---|---|---|---|---|---|
| (a) `gemini-3.5-flash-lite` + `full-context` | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | - | - | - | - | 0 / 3 | `1666da3` / 2026-11-25 | `diagnostico-20261125-q02-503.txt` (detenida) |
| (b) `gemini-3.5-flash-lite` + `bm25` | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | - | - | - | - | - | - | - |
| (c) `gemini-3.8-flash` + `full-context` | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | NO EJECUTADO | - | - | - | - | - | - | - |

Costo por 1 000 preguntas (= costo medio por pregunta × 1 000; es un costo **equivalente a precio de pago**, porque el plan es gratuito): **no se calcula**. El único dato real es el de una pregunta normativa (0.000939 USD con (a); 0.94 USD por 1 000 preguntas **normativas** de ese tipo), que no representa la mezcla del conjunto (las preguntas con datos hacen una llamada más y consumen más). Precios usados: (a), (b) 0.30 entrada / 2.50 salida USD por millón de tokens; (c) 0.75 / 3.75 hasta 2026-12-31.

## Recomendación de estrategia y modelo por defecto (SPEC §7.3)

**No hay recomendación posible con estos datos.** La regla es elegir la estrategia más barata que cumpla todos los umbrales de SPEC §7.6, y ninguna combinación se evaluó con el LLM real. Lo único que se puede afirmar:

- `full-context` y `bm25` cumplen los criterios `[ret]` hoy (`bm25` con el riesgo de sobreajuste de arriba). Ninguna conclusión sobre citas, abstención ni cifras.
- El valor por defecto del código (`full-context`, provisional hasta T-10) **no queda confirmado** por T-10. Decidir entre `full-context` y `bm25` exige repetir las baterías (a) y (b) después de corregir el defecto, y (c) para comparar modelo.
- Por precio, `bm25` enviaría menos tokens de entrada que `full-context`, y `gemini-3.5-flash-lite` cuesta menos que `gemini-3.8-flash`; son expectativas, no resultados.

## Cómo repetir

```bash
# cuando el defecto esté corregido (la llave llega por el env_file; el runner solo habla HTTP)
evals/correr-eval.sh a && evals/correr-eval.sh b && evals/correr-eval.sh c
```

Cada corrida deja `evals/runs/eval-<fecha>-<sha>-<modelo>-<estrategia>.json`. Presupuesto de la fábrica: máximo 3 corridas completas por combinación (FABRICA §7.1); hasta ahora se usaron 0 completas. Los límites diarios del plan gratuito siguen desconocidos; si se agotan, el runner se detiene y guarda la corrida como `PARCIAL`.
