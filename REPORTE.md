# REPORTE — TutorFlow Lights-Out (F2)

> Fecha: 2026-11-25 · Rama: `main` · Último commit verificado: `95b54d9` (+ commits de cierre).
> Convención de estado: **Implementado** (código en `main`) · **Probado** (prueba automática en CI) · **Evaluado** (corrida con LLM real) · **Revisado** (revisión independiente QA o del orquestador) · **Pendiente** · **No verificado**.
> Todo dato de este reporte sale de una ejecución registrada (CI, `evals/runs/`, `qa/revisiones/`, git). Lo que no se ejecutó se marca como tal.

## 1. Resumen ejecutivo

TutorFlow es un asistente académico (.NET 8, Minimal API, puerto 8080) que responde preguntas sobre el reglamento y sobre datos académicos con **cálculos y permisos deterministas en código**; el LLM solo redacta y cita. Lo construyó una fábrica de agentes (orquestador + Core, IA, Arnés y un revisor QA independiente) bajo reglas *lights-out*: el humano escribió especificación, escenarios y configuración; los agentes escribieron todo el código, pruebas, evals, Docker y CI.

| Aspecto | Estado |
|---|---|
| Contrato HTTP, dominio académico, autorización L1–L5, auditoría | Implementado · Probado (635 pruebas) · mutantes 16/16 detectados |
| CI (11 jobs: 10 bloqueantes + evals-llm manual) | Verde en `ceae2a0`; las 3 corridas rojas de `main` fueron bloqueos reales de trazabilidad (§13) |
| Interfaz web (P2) | Implementado · Probado · Revisado (QA r4 APPROVED) · aceptada por el humano (INT-024) |
| Calidad RAG con LLM real | Evaluado con Gemini flash-lite: **ninguna combinación cumple todos los umbrales** (§9). Comparación con Claude Sonnet 5.5: **pendiente (T-10.4 en curso)** |
| Proveedor Anthropic (default de SPEC) | Defecto real hallado por la primera llamada real (HTTP 400 por `temperature`), corregido en T-06.5; **validación real pendiente** |

**TutorFlow no se declara completo**: el criterio `[eval]` RAG-10.b (la estrategia por defecto cumple los umbrales de SPEC §7.6) no está satisfecho.

## 2. Arquitectura implementada

- **Capas:** `Domain` (reglas académicas en `decimal`, `MidpointRounding.AwayFromZero`, política de autorización) → `Application` (flujo `PreguntarAsistente`, guardias, herramientas, `IToolDispatcher`, generación y validación de citas) → `Infrastructure` (API simulada, roster, auditoría JSON por línea, adaptadores LLM y recuperación) → `Api` (Minimal API, middleware de errores). Pruebas de arquitectura fijan las dependencias.
- **Seguridad en capas (SPEC §6):** L1 consistencia de identidad declarada (no es autenticación, SPEC §6.5) · L2a referencias explícitas · L2b solicitudes de terceros (R1–R6) · L3 herramientas por rol · L4 política dentro de cada herramienta antes de leer · L5 guardia de salida. El LLM **no** concede ni deniega permisos (D-15: argumentos con forma inválida vuelven al LLM como error genérico; L3/L4 siguen en 403).
- **Recuperación:** segmentación por artículo/sección, cuarentena del anexo inyectado de `bienestar_y_becas.md`, reglamento 2023 solo con mención explícita; estrategias `full-context` (por defecto), `bm25`, y `vector`/`hybrid` (RRF) solo para experimento.
- **LLM:** `anthropic` y `openai-compatible` (Gemini) por HttpClient; bucle de ≤3 rondas que solo **propone** llamadas; validación determinista de citas y abstención posterior.
- **Web:** React 19 + TypeScript estricto + Vite, servida por nginx en `:3000` con proxy `/api` y CSP; sin permisos, cálculos ni almacenamiento en el cliente.

## 3. Tareas ejecutadas y responsables

| Agente | Tareas integradas |
|---|---|
| Core | T-01, T-02, T-05a–T-05h, T-08, T-08.1, T-13, T-13.1–T-13.3 |
| IA | T-03, T-06, T-06.1–T-06.5, T-09 |
| Arnés | T-00, T-04, T-07a (+.1–.3), T-07b, T-10, T-10.1–T-10.3, T-11 (intermedia), T-12, T-12.1/T-12.2 · T-10.4 en curso |
| QA (independiente) | T-14 r1–r4 sobre T-13 |
| Orquestador | Briefs, compuertas G1–G9, verificación independiente, merges `--no-ff`, INTERVENCIONES |

Devoluciones relevantes por compuerta: T-05d (cálculo fuera de Domain), T-06 (E/S en Application), T-06.3 (declaró pruebas inexistentes, INT-017), T-06.4 (un valor golden en el prompt), T-13 (e2e intermitente que QA r2 no detectó y el orquestador reprodujo 5/6).

## 4. Commits generados

150 commits en `main` (55 merges `--no-ff`). Por trailer `Agente:` — orquestador 61, arnes 41, core 26, ia 12, qa 8; 2 commits humanos sin trailer (`.gitignore`, autorizado). Sin *squash*, *rebase* ni *force push*; el job `lights-out` verifica el trailer `Generado-por: Claude Code` y `propiedad` verifica que cada agente solo tocó sus rutas (148 commits evaluados, 0 incumplimientos).

## 5. Criterios de aceptación cumplidos y pendientes

233 criterios trazados (script `scripts/trazabilidad`, RESULTADO: OK): `dom` 19 · `app` 68 · `api` 49 · `cont` 3 · `ret` 10 · `eval` 42 · `web` 24 · `e2e` 9 · `qa` 9.

- **Probados automáticamente (dom/app/api/cont/ret/web/e2e):** 182 con prueba localizada y en verde en CI.
- **`[qa]`:** 9/9 cumplen (QA r4).
- **`[eval]`:** "OK" en trazabilidad significa que el caso existe en el dataset, **no** que pasa. Resultado real (T-10.2, Gemini flash-lite): 36/42 CUMPLE en full-context y 36/42 en bm25.
- **Pendientes / no satisfechos:** RAG-10.b (estrategia por defecto cumple umbrales) — **no satisfecho**; RAG-16.a/b y RES-13.b — **no ejecutados** (requieren embeddings / corrida específica).

## 6. Escenarios ejecutados, aprobados y fallidos

6 archivos de escenarios (107 escenarios con id): `academic-data` 20 · `authorization` 25 · `adversarial` 16 · `rag-quality` 16 · `resilience` 13 · `web` 17. Todos los criterios deterministas pasan. Fallos de `[eval]` en la última corrida completa (6 por batería), con causa en §9.

## 7. Resultados de tests

Suite completa en el SDK .NET 8 en Docker sobre `95b54d9`: **Domain 167 · Architecture 8 · Application 306 · Api 154 = 635/635**. Web: 78 pruebas Vitest, `tsc --noEmit` estricto, `vite build`, 21 e2e Playwright (45/45 corridas estables en la revisión QA r4).

## 8. Resultados de mutantes

Catálogo de 16 mutantes (`scripts/mutantes/catalogo.json`): redondeo `AwayFromZero`→`ToEven`, `decimal`→`double`, umbral de inasistencia 15→20 y `>`→`>=`, habilitación 2.0→1.5, promedio con asignaturas sin definitiva, `Authorize` siempre permitido, L2a/L2b desactivadas, cuarentena desactivada, validación de citas y abstención desactivadas, rol sin distinguir mayúsculas, coordinador sin verificación de programa, roster contado como consulta, regla R2 invertida. `--completo` sobre `95b54d9`: **MUERTO 16 · SOBREVIVE 0 · PENDIENTE 0**. El job `mutantes` bloquea la CI.

## 9. Resultados de evaluaciones RAG

Umbrales SPEC §7.6 (v1.6): precisión y cobertura de citas ≥ 0.90; abstención y exactitud numérica **≥ 0.90 (eran 1.00; relajados por decisión humana después de ver T-10.2, INT-022)**; Recall@4 ≥ 0.95; MRR ≥ 0.85.

| Corrida | Commit | Estrategia | Prec. citas | Cob. citas | Abst. | Numérica | Cumple |
|---|---|---|---|---|---|---|---|
| T-10 (dataset v1) | 980131f | full-context | 0.50 | 0.93 | 1.00 | 0.65 | No |
| T-10 (dataset v1) | 980131f | bm25 | 0.44 | 0.89 | 1.00 | 0.70 | No |
| T-10.2 (dataset v2) | cd31f51 | full-context | 0.887 | 0.963 | 1.00 | 0.85 | No |
| T-10.2 (dataset v2) | cd31f51 | bm25 | 0.911 | **0.889** | 1.00 | 0.95 | No |
| T-10.4 Sonnet 5.5 | (pendiente) | full-context / bm25 | — | — | — | — | Pendiente |

`[ret]` (determinista, bm25): Recall@4 1.00, MRR 0.917 — cumple. `vector`/`hybrid`: implementados y probados offline, **no evaluados** con LLM (no hay llave de embeddings).

**Por qué no se cumplen los umbrales (lectura de las respuestas crudas, T-06.4):** la mayoría de fallos eran **instrucciones insuficientes del prompt** (cifras reformateadas "0.3" en vez de "30%", denominador omitido en faltas, herramientas de datos invocadas en preguntas solo normativas, citas de más). Se corrigieron en T-06.4 sin cifras del dataset en el prompt. El resto es **límite del modelo** (flash-lite, capa gratuita) o **interpretación del dataset** (q04, Art. 7 en ACAD-08-c/AUTH-16-c) pendiente de decisión humana. La atribución por caso con evidencia (mismo commit, flash-lite contra Sonnet) es el objeto de T-10.4 y se añadirá aquí; no se afirma "es culpa del modelo" sin esos datos.

## 10. Métricas de tokens y costos reales

**Producto (evals con LLM real, Gemini flash-lite, plan gratuito: gasto real US$ 0; costo equivalente a precio de pago 0.30/2.50 USD por M):**

| Corrida | Tokens | Costo equivalente | Por 1.000 preguntas |
|---|---|---|---|
| T-10 (a) full-context | 157 002 | US$ 0.054 | US$ 1.15 |
| T-10 (b) bm25 | 72 438 | US$ 0.029 | US$ 0.62 |
| T-10.2 (a) full-context | 193 108 | US$ 0.066 | **US$ 1.40** |
| T-10.2 (b) bm25 | 107 993 | US$ 0.041 | **US$ 0.87** |
| T-10.4 Sonnet 5.5 (2/10 USD por M) | pendiente | pendiente | pendiente |

**Fábrica (agentes de Claude Code), medido desde las transcripciones locales de la sesión (34 archivos):**

| Modelo | Mensajes | Salida | Entrada fresca | Escritura de caché | Lectura de caché | Costo equivalente API* |
|---|---|---|---|---|---|---|
| Opus 5.5 (orquestador) | 458 | 0.68 M | 1 062 | 1.92 M | 204.6 M | ≈ US$ 70 |
| Sonnet 5.5 (Core, IA, Arnés, QA) | 1 224 | 1.43 M | 2 500 | 4.95 M | 105.9 M | ≈ US$ 45 |
| Haiku 5.5 | 3 | 5 114 | 6 | 31 k | 9 k | < US$ 0.01 |
| **Total** | | | | | | **≈ US$ 115** |

\* Estimación a precio de lista de la API (Opus 4/20, Sonnet 2/10 USD por M; lectura de caché 0.20/0.10; escritura de caché de 1 h a 2× la entrada). La fábrica corrió con suscripción de Claude Code, no facturada por token. El 98 % de la entrada es lectura de caché.

**Tiempo:** primera y última actividad registradas el 2026-11-25, 13:49–21:34 UTC (≈ 7 h 45 min de reloj para F0–F2, incluidas las pausas por el límite de uso del plan y un cambio de cuenta).

## 11. Configuración del modelo y proveedor

- Variables: `LLM_PROVIDER` (`anthropic` | `openai-compatible`), `LLM_MODEL`, `LLM_API_KEY`, `LLM_BASE_URL`, `LLM_PRICE_INPUT_PER_MTOK`, `LLM_PRICE_OUTPUT_PER_MTOK`, `LLM_MAX_TOKENS_PER_REQUEST`, `RETRIEVAL_STRATEGY` (por defecto `full-context`). Ver `.env.example` y `README.md`.
- Evaluado: `openai-compatible` + `gemini-3.5-flash-lite` (endpoint compatible con OpenAI de Gemini, reenviando `extra_content` de forma opaca).
- `anthropic`: defecto con modelos Claude 5 (`temperature` rechazado con HTTP 400; thinking no preservado) hallado en la primera llamada real y corregido en T-06.5 con pruebas grabadas. **Validación real con `claude-sonnet-5-5`: pendiente (T-10.4).**

## 12. Resultado de Docker Compose

`docker compose up -d` levanta `mock-api` (red interna), `tutorflow` (`:8080`) y `web` (`:3000`), los tres *healthy*. El job `smoke` (bloqueante) lo verifica en cada push **sin llave** (override `scripts/smoke/compose.sin-llave.yml`): salud, 400, 403-acceso (AUTH-03), 403-identidad, 503 sin LLM, web 200 y proxy `/api/salud` 200; RES-01.b mide ≈ 8 s desde `up` hasta el primer 200 (límite 30 s).

## 13. Resultado de GitHub Actions

Jobs bloqueantes: `build-test`, `trazabilidad`, `secretos` (gitleaks v8.24.3 sobre toda la historia), `lights-out`, `retrieval`, `mutantes`, `smoke`, `web`, `web-e2e`, `propiedad`; no bloqueante y manual: `evals-llm`. Último run de `main` (`ceae2a0`, run 45703932406): **10/10 bloqueantes en verde**, `evals-llm` omitido (sin secreto). Historial: 27 verdes y 3 rojos en las últimas 30 corridas de `main`; los 3 rojos fueron `trazabilidad` bloqueando de verdad (decisión D-15 añadida a la especificación antes de su prueba) hasta integrar T-05g.

## 14. Hallazgos de seguridad

- **Inyección en el material:** el anexo de `bienestar_y_becas.md` (falso "correo de becas") se excluye por cuarentena antes del LLM; escenarios adversariales y mutante dedicado.
- **Identidad declarada no es autenticación:** documentado como limitación (SPEC §6.5), no presentado como resuelto; la UI lo dice explícitamente.
- **Permisos deterministas** L1–L5; el LLM no puede ampliar acceso (pruebas y mutantes de política).
- **Secretos:** gitleaks sobre toda la historia; excepciones acotadas y justificadas (INT-019/020); ningún agente leyó `.env`; las llaves del proveedor se pasaron solo por entorno/interpolación de compose.
- **Hallazgo de la fábrica:** una prueba de prompt filtraba un valor golden del dataset (18.75 %) — detectado en compuerta y bloqueado con una prueba que falla si reaparece.
- **Web:** CSP sin `unsafe-inline` en scripts, `nosniff`, `no-referrer`, sin almacenamiento en el navegador, sin peticiones a dominios externos (verificado por QA).

## 15. Limitaciones conocidas

1. Sin autenticación real (la identidad llega en el cuerpo).
2. Umbrales de calidad RAG no cumplidos con Gemini flash-lite; relajación de dos umbrales decidida después de ver resultados (INT-022).
3. Latencia p95 de full-context 12.9 s (> 10 s objetivo de SPEC §10.1); bm25 7.6 s.
4. Estado en memoria del adaptador LLM para `extra_content`/thinking (un reinicio entre rondas de una misma petición lo perdería; no ocurre en una petición normal).
5. L2a de entrada rechaza preguntas con un código de asignatura con forma de id.
6. Riesgo de sobreajuste de `bm25` (equivalencias académicas, INT-011).
7. `vector`/`hybrid` no evaluados con LLM.
8. Hallazgos bajos aceptados de la web (INT-024).

## 16. Intervenciones humanas

24 entradas en `INTERVENCIONES.md` (INT-001..INT-024) más notas de transparencia NT-01/NT-02. Tipos: ratificación de decisiones de especificación (D-01..D-15), autorizaciones de tareas, cambios de configuración que los agentes no pueden hacer (p. ej. `.gitignore`, parches de `SPEC.md` aplicados por el humano), decisiones de producto (perfil web, D-15 ampliado, umbrales INT-022, experimento Sonnet INT-023) y la aceptación de la web (INT-024). El humano no escribió código ni pruebas.

## 17. Riesgos residuales

- Pruebas ocultas que comparen texto generado (cifras/citas) pueden fallar en 1–2 de cada 20 casos con flash-lite.
- El formato de citas de datos (D-04: `"Datos del estudiante"`/`"Datos del programa"`) es una interpretación documentada; otra lectura del evaluador fallaría.
- El proveedor por defecto (`anthropic`) queda validado solo con respuestas grabadas hasta que termine T-10.4.
- Variabilidad del LLM: 47 casos y una corrida por combinación no permiten conclusiones estadísticas fuertes.

## 18. Instrucciones reproducibles desde cero

```bash
git clone <repositorio> tutorflow && cd tutorflow
cp .env.example .env          # completar LLM_PROVIDER, LLM_MODEL, LLM_API_KEY (y LLM_BASE_URL para openai-compatible)
docker compose up -d --build --wait
curl -s localhost:8080/api/salud                       # {"estado":"ok"}
curl -s -X POST localhost:8080/api/asistente/preguntar \
  -H 'Content-Type: application/json' \
  -d '{"pregunta":"¿Cuál es mi promedio?","usuario":{"rol":"estudiante","id":"EST-001"}}'
open http://localhost:3000                             # interfaz web
```

- Pruebas sin instalar .NET: `docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet test`.
- Mutantes: `DOTNET_CMD="docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet" node scripts/mutantes/mutantes.js --completo`.
- Trazabilidad: `node scripts/trazabilidad/trazabilidad.js` · Evals reales: `evals/correr-eval.sh <a|b|d|e>` (ver `evals/README.md`).
- **Relanzar la fábrica** (corrida en vivo): abrir el repositorio en Claude Code; `CLAUDE.md` es el punto de entrada, los perfiles están en `.claude/agents/{core,ia,arnes,qa}.md` y los permisos en `.claude/settings.json`. Un requisito nuevo entra como cambio de `SPEC.md` + escenario con id y nivel + tarea en `FABRICA.md` §6; el orquestador lo asigna, verifica las compuertas G1–G9 e integra con `merge --no-ff`.

**Mapa de la raíz:** documentos de entrega (`SPEC.md`, `SPEC-WEB.md`, `AGENTS.md`, `FABRICA.md`, `INTERVENCIONES.md`, `REPORTE.md`, `README.md`, `CLAUDE.md`), código (`src/`, `tests/`, `web/`), verificación (`escenarios/`, `evals/`, `qa/`, `scripts/`, `.github/`), transcripciones (`logs/`), material del enunciado (`q10/`) y archivos que las herramientas exigen en la raíz (`docker-compose.yml`, `Dockerfile`, `.dockerignore`, `TutorFlow.sln`, `global.json`, `Directory.Build.props`, `.env.example`, `.gitignore`, `.gitleaks.toml`).
