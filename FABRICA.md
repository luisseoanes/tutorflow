# FABRICA.md — La fábrica de agentes de TutorFlow

> Cómo se construye TutorFlow sin que el humano escriba código: qué agentes existen, cómo se planifica, ejecuta, revisa e integra el trabajo, cuánto cuesta, qué pasa cuando algo falla y cuándo se detiene la fábrica.
> Reglas comunes de los agentes: `AGENTS.md`. Qué se construye: `SPEC.md`. Cómo se verifica: `/escenarios`.

---

## 1. Principios de diseño

1. **Pocos agentes, bien delimitados.** Cada rol es dueño de un conjunto de archivos que no se solapa con los demás. Así se evitan conflictos de *merge* y la culpa queda clara cuando algo falla.
2. **Verificar en lugar de confiar.** El orquestador nunca acepta un "terminado": reejecuta la compilación, las pruebas y las evals, y revisa el diff (§5).
3. **Prueba primero, desde los escenarios.** Los escenarios humanos son la especificación ejecutable. Las pruebas se escriben a partir de ellos, no a partir del código.
4. **El arnés también se prueba.** Se inyectan mutaciones conocidas (redondeo bancario, umbral del 20%, autorización desactivada) y se exige que la CI falle (§5.3).
5. **Determinismo antes que LLM.** Todo lo que puede ser código determinista lo es. El LLM solo interpreta y redacta.
6. **Transparencia total.** Cada sesión de agente se exporta a `/logs` y cada intervención humana va a `INTERVENCIONES.md`. No se inventan métricas.
7. **El costo es un requisito.** El modelo se asigna por rol según la dificultad, el contexto de cada agente se limita a lo necesario y la reintención tiene tope.

## 2. Implementación concreta

| Elemento | Implementación |
|---|---|
| Herramienta | Claude Code (CLI / extensión VS Code). |
| Orquestador | La sesión principal de Claude Code, que planifica, despacha, revisa e integra. |
| Agentes especializados | Subagentes de Claude Code definidos en `.claude/agents/<rol>.md` (configuración de agentes que **escribe y commitea el humano** al inicio de la fase 2). Cada definición fija el *prompt* del rol, las herramientas permitidas y el modelo. |
| Aislamiento | Cada tarea paralela corre en un **git worktree** propio (`isolation: worktree`) sobre la rama `agent/<rol>/<tarea>`. |
| Permisos | `.claude/settings.json` (humano) con una lista de comandos permitidos (`dotnet`, `docker compose`, `git` sin `push --force`/`rebase`/`reset --hard`) y `deny` explícito para los comandos destructivos de git y la lectura de `.env`. |
| Integración | El orquestador ejecuta `git merge --no-ff agent/<rol>/<tarea>` en `main` tras pasar las compuertas (§5). |
| CI | GitHub Actions (la escribe DevOps) como compuerta final, independiente de los agentes. |

### 2.1 Asignación de modelos (costo frente a capacidad)

| Rol | Modelo | Justificación |
|---|---|---|
| Orquestador | `claude-opus-5-5` | Planificación, revisión crítica de diffs y decisiones de integración: errores aquí son caros. |
| Architect | `claude-opus-5-5` | Una sola tarea corta que fija contratos para todos. |
| Backend, Retrieval, Security, DevOps, QA | `claude-sonnet-5-5` | Implementación guiada por especificación y pruebas: buena relación calidad/costo. |
| Tareas mecánicas (sanitizar logs, generar matrices) | `claude-haiku-5-5` | Trabajo repetitivo de bajo riesgo. |
| LLM del **producto** (en tiempo de ejecución) | `claude-haiku-5-5` por defecto | Se valida en evals frente a `claude-sonnet-5-5`. Si Haiku cumple los umbrales de SPEC §7.6, se queda por costo. |

## 3. Ciclo de vida

```
F0 Especificación (humano + asistencia)  →  F1 Revisión humana  →  F2 Construcción (agentes)
→  F3 Evaluación y endurecimiento  →  F4 Cierre (reporte, logs)  →  F5 Corrida en vivo (requisito nuevo)
```

| Fase | Entrada | Salida | Compuerta de salida |
|---|---|---|---|
| **F0** | Enunciado + material | SPEC, AGENTS, FABRICA, escenarios, INTERVENCIONES, `.gitignore`, `.env.example`, `.gitleaks.toml` | Documentos completos y coherentes (estado actual) |
| **F1** | Documentos de F0 | Aprobación humana, `.claude/agents/*.md`, `.claude/settings.json`, primer commit | El humano autoriza la fase 2 (queda en INTERVENCIONES) |
| **F2** | Backlog §6 | Código, pruebas, Docker y CI en `main` | §14 de SPEC, puntos 2–6 |
| **F3** | `main` en verde | `evals/RESULTADOS.md`, estrategia elegida, mutaciones detectadas | SPEC §14 completo |
| **F4** | Todo lo anterior | `/logs` sanitizados; el humano redacta `REPORTE.md` con los datos reales | Revisión humana |
| **F5** | Requisito nuevo (en vivo) | Delta de SPEC + escenarios (humano) → ciclo F2–F3 reducido | §10 |

### 3.1 Ciclo interno de cada tarea

```
Orquestador: elige una tarea lista (dependencias integradas) → escribe el brief (§4.0) → despacha
Agente:      lee AGENTS/SPEC/escenarios → escribe pruebas a partir de los escenarios (rojo) → implementa (verde)
             → refactoriza → ejecuta las verificaciones locales → commit(s) con trailer → handoff con evidencia
Orquestador: compuertas §5 → { aceptar → merge --no-ff | rechazar → feedback concreto (máx. 3 ciclos) | escalar }
```

## 4. Matriz de agentes

### 4.0 Brief de despacho (plantilla que usa el orquestador)

```
Tarea T-xx · Rol · Rama agent/<rol>/<tarea>
Objetivo: (1–3 frases)
Escenarios: (ids)  ·  Secciones de SPEC: (§)
Archivos autorizados: (globs)  ·  Prohibidos: (globs; además AGENTS §10)
Dependencias integradas: (T-xx con sha)
Verificación exigida: (comandos exactos)
Presupuesto: (ciclos, tiempo)
```

### 4.1 Resumen

| Agente | Responsabilidad principal | Archivos autorizados | Paralelo con | Depende de |
|---|---|---|---|---|
| **Orquestador** | Plan, despacho, revisión, integración y métricas | Solo `git merge` en `main`; no edita código (si corrige algo, eso es una intervención de fábrica y se registra) | — | — |
| **Architect** | Solución, proyectos, puertos (interfaces), DTOs del contrato, prueba de arquitectura | `TutorFlow.sln`, `global.json`, `Directory.Build.props`, `src/*/*.csproj`, `src/**/Ports/**`, `src/TutorFlow.Api/Contracts/**`, `tests/TutorFlow.Architecture.Tests/**`, `docs/arquitectura.md` | nadie (va primero) | F1 |
| **Backend** | Dominio académico, cliente de la API simulada, herramientas, caso de uso, endpoints, errores, auditoría | `src/TutorFlow.Domain/**` (salvo `Authorization/`), `src/TutorFlow.Application/**` (salvo `Retrieval/`, `Llm/`), `src/TutorFlow.Infrastructure/{MockApi,Audit}/**`, `src/TutorFlow.Api/**` (salvo `Contracts/`), `tests/**/{Domain,Application,Api}/**` (salvo `Security/`) | Retrieval, Security, DevOps, QA | Architect |
| **Retrieval** | Chunking, índices BM25/vectorial, RRF, proveedores LLM, bucle de herramientas, validación de citas | `src/TutorFlow.Application/{Retrieval,Llm}/**`, `src/TutorFlow.Infrastructure/{Retrieval,Llm}/**`, `tests/**/Retrieval/**`, `tests/**/Llm/**` | Backend, Security, DevOps, QA | Architect |
| **Security** | Identidad (L1), política (L4), guardias L2/L5, pruebas adversariales | `src/TutorFlow.Domain/Authorization/**`, `src/TutorFlow.Application/Security/**`, `tests/**/Security/**` | Backend, Retrieval, DevOps, QA | Architect |
| **QA / Evaluation** | Dataset de evals, runner, métricas, comparación de estrategias, mutaciones y trazabilidad | `evals/**`, `tests/TutorFlow.Api.Tests/Contract/**`, `scripts/mutantes/**` | todos en W1 | Architect (W1); integración (W3) |
| **DevOps** | Dockerfile, Compose, CI, gitleaks, verificación del trailer, *smoke test* y sanitizado de logs | `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.github/**`, `scripts/**` (salvo `mutantes/`) | todos | Architect (para las rutas de los proyectos) |

**Prohibido para todos:** `SPEC.md`, `AGENTS.md`, `FABRICA.md`, `INTERVENCIONES.md`, `REPORTE.md`, `escenarios/**`, `.claude/**`, `q10/material/**`, `.env*` (salvo lectura de `.env.example`), `logs/**` (solo DevOps añade archivos exportados y sanitizados).

### 4.2 Fichas por agente

#### Architect
- **Responsabilidades:** crear la solución .NET 8 con los 4 proyectos de SPEC §12; definir los puertos (`IAcademicDataSource`, `ILlmClient`, `IEmbeddingClient`, `IRetriever`, `IAuditLog`, `IClock`, `IAuthorizationPolicy`) y los DTOs del contrato exactamente como en SPEC §3; escribir la prueba de arquitectura (dependencias entre capas, sin `double`/`float` en Domain).
- **Herramientas:** Read, Write, Edit, Bash (`dotnet new`, `dotnet build`, `dotnet test`).
- **Entradas:** SPEC §3, §8, §12; AGENTS §3–§5.
- **Salidas:** compila con 0 advertencias; la prueba de arquitectura pasa; `docs/arquitectura.md` (≤ 1 página).
- **Criterios de aceptación:** los nombres JSON de los DTOs coinciden con SPEC §3 (lo verifica una prueba de serialización); ningún proyecto referencia uno no permitido.
- **Condiciones de fallo:** añadir lógica de negocio; añadir paquetes fuera de la lista aprobada; DTOs que no coinciden con el contrato.
- **Handoff:** al orquestador, con la lista de interfaces y su propósito. Desbloquea W1.
- **Evidencia:** salida de `dotnet build -warnaserror` y `dotnet test`; árbol de proyectos.

#### Backend
- **Responsabilidades:** reglas académicas de SPEC §5 (*value objects* `Nota`, `Definitiva`, `Asistencia` y servicio `CalculadoraAcademica`); `MockApiAcademicDataSource` (timeouts, reintentos, URL-encoding, validación de datos); herramientas de SPEC §8 que llaman a `IAuthorizationPolicy` **antes** de leer; caso de uso `PreguntarAsistente`; endpoints, validación (400/403), *middleware* de errores, `X-Request-Id`; auditoría JSONL (SPEC §9).
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`, `node` para la API simulada en pruebas de integración).
- **Entradas:** SPEC §3, §5, §8, §9, §10; escenarios ACAD-*, RES-02, RES-04, RES-07..RES-10, RES-12; puertos del Architect.
- **Salidas:** pruebas de dominio con la tabla golden completa (24 asignaturas + 6 promedios); pruebas de API con LLM `fake`.
- **Criterios de aceptación:** todos los ACAD-* de dominio en verde; cero `double` en Domain; ningún dato fijo del conjunto de datos en `src/` (RES-12).
- **Condiciones de fallo:** el LLM calcula algo; una herramienta lee datos antes de autorizar; se usa `MidpointRounding.ToEven`; se ignora `null` en cortes o asistencia.
- **Handoff:** al orquestador; avisa a QA de que el caso de uso está disponible para las evals E2E.
- **Evidencia:** salida de `dotnet test --filter "Escenario~ACAD|Escenario~RES"`; prueba roja antes de la corrección del redondeo.

#### Retrieval
- **Responsabilidades:** indexación estructural (SPEC §7.2) con metadatos y cuarentena; BM25 con normalización en español; vectorial (`IEmbeddingClient`, caché en disco); `hybrid` con RRF; `full-context`; proveedores `anthropic`, `openai-compatible` y `fake` detrás de `ILlmClient` con *usage* exacto; bucle de herramientas (≤ 3 rondas); salida estructurada; validación de citas (SPEC §7.5); cálculo de `tokens` y `costoUsd`.
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`).
- **Entradas:** SPEC §7, §10.2–§10.3, §11; escenarios RAG-*, RES-05, RES-06, RES-11, RES-13.
- **Salidas:** estrategias intercambiables por configuración; un LLM `fake` determinista que sirve a todas las pruebas de API.
- **Criterios de aceptación:** RAG-13..RAG-15 en verde; el Anexo queda en cuarentena; cambiar `RETRIEVAL_STRATEGY` o `LLM_PROVIDER` no requiere cambiar código.
- **Condiciones de fallo:** citas de chunks no recuperados; chunks `untrusted` o derogados en el contexto por defecto; una llave en un log; un SDK de proveedor sin aprobación.
- **Handoff:** al orquestador y a QA (las interfaces de estrategia quedan listas para el experimento).
- **Evidencia:** salida de las pruebas; volcado de los chunks generados (ids + sección) adjunto al handoff.

#### Security
- **Responsabilidades:** L1 (`IdentityResolver` → `PrincipalAcademico`); L4 (`AuthorizationPolicy`, la matriz de SPEC §6.1); L2 (`ReferenceGuard` sobre el *roster* obtenido de la API, nunca fijo); L5 (`OutputGuard`); el 403 uniforme; el LLM *fake* malicioso para las pruebas ADV; revisión de seguridad del diff de Backend y Retrieval antes de la integración W3.
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`).
- **Entradas:** SPEC §3.3–§3.4, §6, §9; escenarios AUTH-*, ADV-*.
- **Salidas:** AUTH-22 (80 aserciones); pruebas ADV con LLM comprometido; informe de revisión de seguridad en el handoff.
- **Criterios de aceptación:** todos los AUTH-* y ADV-* Críticos en verde; un 403 idéntico byte a byte para un id existente y uno inexistente (AUTH-07).
- **Condiciones de fallo:** cualquier permiso concedido desde el texto o el LLM; una respuesta parcial con datos autorizados ante una petición mixta (ADV-05); PII en la auditoría.
- **Handoff:** al orquestador, con una lista de hallazgos (bloqueantes y no bloqueantes) sobre el código de otros agentes. Los hallazgos bloqueantes se convierten en tareas para el dueño del archivo; Security **no** edita archivos ajenos.
- **Evidencia:** `dotnet test --filter "Escenario~AUTH|Escenario~ADV"`; resultado de los mutantes de seguridad (§5.3).

#### QA / Evaluation
- **Responsabilidades:** dataset etiquetado (`evals/dataset.jsonl`, con las 10 preguntas + escenarios RAG/ACAD/ADV y el `chunkId` relevante); runner de evals (recuperación sin LLM y E2E con LLM real); métricas de SPEC §7.6; experimento de estrategias; `evals/RESULTADOS.md` **solo con ejecuciones reales** (con commit, fecha, modelo y estrategia); mutantes (`scripts/mutantes`); pruebas de contrato (RES-02) con JSON Schema.
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`, `docker compose`); acceso a `LLM_API_KEY` **solo** en el entorno de evals.
- **Entradas:** escenarios (todos), SPEC §7.6, §14.
- **Salidas:** evals deterministas (recuperación, contrato, cálculos) que corren en cada PR; evals con LLM que corren manualmente o en `main` si existe el secreto.
- **Criterios de aceptación:** cada mutante del catálogo §5.3 hace fallar al menos una prueba; la estrategia elegida cumple los umbrales; los costos medidos se registran por pregunta.
- **Condiciones de fallo:** cifras no ejecutadas; umbrales relajados; un valor golden calculado con el código del producto.
- **Handoff:** al orquestador, con la tabla de resultados y una recomendación de estrategia y modelo.
- **Evidencia:** archivos crudos de resultados (`evals/runs/<fecha>-<sha>.json`) y el comando exacto.

#### DevOps
- **Responsabilidades:** Dockerfile multi-etapa (SDK → runtime aspnet, usuario no root, cultura invariante para formato); `docker-compose.yml` (servicio `mock-api` con `node:20-alpine` montando `q10/material/mock-api` de solo lectura, sin publicar el puerto; servicio `tutorflow` en `8080:8080` con `env_file: .env` opcional, el reglamento montado de solo lectura en `/app/knowledge`, `./audit` y *healthchecks*); la CI (§5.2); el script de trazabilidad; el sanitizado y la exportación de logs.
- **Herramientas:** Read, Write, Edit, Bash (`docker`, `docker compose`, `dotnet`).
- **Entradas:** SPEC §3, §10, §11; escenarios RES-01, RES-03, RES-10; AGENTS §9.4.
- **Salidas:** `docker compose up` funcional; un *workflow* que bloquea.
- **Criterios de aceptación:** RES-01 y RES-03 en un *runner* limpio; un commit de prueba sin trailer que toca `src/` es rechazado; un secreto de prueba es detectado por gitleaks.
- **Condiciones de fallo:** la llave dentro de la imagen o en los logs de la CI; un *job* de evals con LLM obligatorio en PRs sin secreto (rompería los PRs de *forks*); `continue-on-error` en las compuertas.
- **Handoff:** al orquestador, con el enlace a la ejecución de CI en verde y en rojo (demostración de bloqueo).
- **Evidencia:** salida de `docker compose up` + `curl /api/salud`; ejecuciones de CI.

## 5. Revisión y compuertas (el orquestador no confía)

### 5.1 Compuertas de aceptación de una rama (en orden; si una falla, se rechaza)

| # | Compuerta | Cómo se verifica |
|---|---|---|
| G1 | Alcance | `git diff --name-only main...<rama>` ⊆ archivos autorizados del rol (§4.1). |
| G2 | Trailer | `git log main..<rama> --format=%B` → cada commit contiene `Generado-por: Claude Code`. |
| G3 | Compilación | El orquestador ejecuta `dotnet build -warnaserror`. |
| G4 | Pruebas | El orquestador ejecuta `dotnet test` **completo** (no solo el filtro del agente). |
| G5 | Trazabilidad | Los escenarios que el handoff declara tienen pruebas con el `Trait` correspondiente y estas pasan. |
| G6 | Revisión del diff | Lectura humana-equivalente buscando: `double`/`float` en Domain, `ToEven`, ids o nombres fijos, `catch (Exception)`, `Skip`, umbrales cambiados, secretos, TODOs que oculten requisitos y permisos derivados del LLM. |
| G7 | Evidencia | El handoff cumple AGENTS §11 y la salida reportada coincide con la reejecución. **Una discrepancia equivale a un rechazo automático y queda registrada.** |
| G8 | Integración | `merge --no-ff` en una rama temporal + `dotnet test` completo antes de mover `main`. |

### 5.2 CI (GitHub Actions, la escribe DevOps): bloquea el *merge*

1. `dotnet build -warnaserror` + `dotnet test` (unitarias, de aplicación, de API con LLM `fake`).
2. Evals **deterministas**: métricas de recuperación (sin LLM) con umbrales de SPEC §7.6, golden académicos y contrato.
3. Trazabilidad: falla si un escenario Crítico o Alto no tiene prueba.
4. Mutantes (§5.3): falla si algún mutante **sobrevive**.
5. Gitleaks sobre el diff y la historia.
6. Regla lights-out (AGENTS §9.4).
7. *Smoke* con Docker: `docker compose up` → RES-01 + AUTH-03.
8. (No bloqueante en PR, manual o en `main` con secreto) Evals E2E con el LLM real.

### 5.3 Catálogo de mutantes: prueba de que el arnés frena errores

| Mutante | Debe detectarlo |
|---|---|
| M1 `AwayFromZero` → `ToEven` | ACAD-02..ACAD-05 |
| M2 `decimal` → `double` en la definitiva | ACAD-02..ACAD-04, prueba de arquitectura |
| M3 Umbral de inasistencia 15 → 20 | ACAD-08, ACAD-09, AUTH-20 |
| M4 `>` → `>=` en el umbral | ACAD-10 |
| M5 Habilitación 2.0 → 1.5 | ACAD-15 |
| M6 Las asignaturas sin definitiva cuentan como 0 | ACAD-14 |
| M7 `Authorize` devuelve siempre `Permitido` | AUTH-03..AUTH-22, ADV-10 |
| M8 L2 desactivada | AUTH-03 (`tokens=0`) |
| M9 La cuarentena del Anexo desactivada | RAG-14, ADV-07 |
| M10 La validación de citas desactivada | RAG-13 |
| M11 Comparación de rol que ignora mayúsculas | AUTH-10 |

## 6. Plan de ejecución y paralelismo

```
W0 ─ T-01 Architect ──────────────────────────────────────────────┐
W1 ─┬ T-02 Backend: dominio académico (ACAD dominio)               │ en paralelo,
    ├ T-03 Security: identidad + política + guardias (AUTH-22)     │ worktrees
    ├ T-04 Retrieval: chunking + BM25 + cuarentena (RAG-14/15)     │ separados
    ├ T-05 DevOps: Docker + CI base + gitleaks + trailer           │
    └ T-06 QA: dataset + runner de recuperación + mutantes (esq.)  ┘
W2 ─┬ T-07 Backend: API simulada + herramientas + endpoints + auditoría  (dep. T-02, T-03)
    └ T-08 Retrieval: proveedores LLM + fake + bucle + citas + vector/hybrid  (dep. T-04)
W3 ─┬ T-09 Backend: caso de uso E2E integrado  (dep. T-07, T-08)
    ├ T-10 Security: suite ADV con LLM comprometido + revisión de seguridad  (dep. T-09)
    └ T-11 DevOps: smoke Docker en CI + job de evals  (dep. T-09)
W4 ─ T-12 QA: evals E2E reales + experimento de estrategias + RESULTADOS.md  (dep. T-09..T-11)
W5 ─ T-13 Orquestador: verificación final de SPEC §14 + exportación de logs (DevOps, Haiku)
```

- Paralelismo máximo: **5 agentes en W1**, y es seguro porque los archivos son disjuntos (§4.1).
- Las tareas de W2 y W3 consumen los puertos y no los modifican. Un cambio de puerto vuelve a pasar por el Architect (tarea nueva y explícita).
- Ruta crítica: T-01 → T-02/T-03 → T-07 → T-09 → T-12.

## 7. Costos y presupuestos

### 7.1 Presupuestos de la fábrica (límites, no mediciones)

| Concepto | Límite | Al superarlo |
|---|---|---|
| Ciclos de rechazo por tarea | 3 | Se escala al humano (intervención) |
| Tiempo por tarea | 45 min de reloj | El orquestador la divide o la escala |
| Fase F2 + F3 completa | ≈ 4 h de reloj | El humano decide recortar alcance (se prioriza P0 de SPEC §0.1) |
| Evals E2E con el LLM real | ≤ 3 corridas completas por estrategia | Se justifica cada corrida extra en `RESULTADOS.md` |

### 7.2 Tácticas de eficiencia

- Briefs acotados (§4.0): cada agente recibe solo las secciones de SPEC y los escenarios de su tarea, no toda la conversación.
- Un modelo por rol (§2.1); Opus solo donde el juicio lo justifica.
- Los agentes reutilizan la salida de las pruebas en lugar de releer archivos completos.
- Evals deterministas en la CI (gratis) y E2E con el LLM bajo demanda.
- Producto: `full-context` o `bm25` con *prompt caching*, salida estructurada corta y Haiku por defecto si cumple los umbrales.

### 7.3 Medición (para `REPORTE.md`)

- **Fábrica:** tokens de entrada, salida y caché por sesión, extraídos de las transcripciones exportadas (campo `usage`). Si se usa un plan de suscripción, se reporta el **costo equivalente** a precio de API con la fuente y la fecha de la tabla de precios, aclarando que no es un gasto facturado.
- **Producto:** `tokens` y `costoUsd` por pregunta, sacados de los resultados de evals → costo medio × 1 000 = costo estimado por 1 000 preguntas, por estrategia y modelo.
- **No** se reporta ninguna cifra que no provenga de esas fuentes.

## 8. Manejo de fallos

| Fallo | Detección | Respuesta |
|---|---|---|
| Las pruebas fallan en G4 | Reejecución del orquestador | Se devuelve al agente con la salida literal; contador de ciclos +1 |
| Archivos fuera de alcance (G1) | `git diff --name-only` | Rechazo y re-despacho con el recordatorio de AGENTS §10; si se repite, se escala |
| Evidencia falsa o inconsistente (G7) | Salida distinta a la reejecución | Rechazo; se registra en el log de la fábrica y en `INTERVENCIONES.md` si requiere acción humana |
| Contradicción de especificación | El agente escala (AGENTS §12) | El orquestador **no** decide cambios de contrato o reglas: escala al humano → intervención registrada → el humano edita SPEC o escenarios |
| Agente en bucle o sin progreso | Mismo error 3 veces | Se detiene la sesión y se re-despacha en contexto limpio con una tarea más pequeña |
| Fallo de infraestructura (NuGet, Docker, proveedor) | Error de herramienta | 1 reintento; si persiste, se pausa la tarea y se escala |
| Conflicto de *merge* | G8 | El dueño de los archivos lo resuelve en su rama; el orquestador nunca resuelve conflictos en código |
| Secreto detectado | gitleaks / G6 | **Parada total** (§9); el humano rota la llave; no se reescribe la historia: se documenta |

## 9. Condiciones de parada

**Éxito:** se cumplen todos los criterios de SPEC §14, la CI está en verde en `main`, la exportación de logs está hecha y el humano firma el cierre en `INTERVENCIONES.md`.

**Parada obligatoria (la fábrica se detiene y espera al humano):**

1. Un secreto o PII real en el árbol o la historia.
2. Una contradicción que requiere cambiar SPEC, los escenarios o el contrato.
3. 3 ciclos fallidos en la misma tarea.
4. Se supera un presupuesto de §7.1.
5. Una compuerta G7 con evidencia falsa repetida por el mismo agente.
6. Una orden explícita del humano.

**Nunca** se "termina" desactivando compuertas, saltando pruebas o recortando escenarios sin una decisión humana registrada.

## 10. Corrida en vivo (F5, 60 minutos)

1. **(Humano, unos 10 min)** Escribe el delta: una nueva sección en SPEC (marcada `[F5]`) + escenarios nuevos con ids `NEW-nn` y su severidad.
2. **(Orquestador, unos 5 min)** Plan: qué rol es el dueño, si toca puertos (→ Architect) o seguridad (→ Security revisa).
3. **(Agentes, unos 30 min)** Ciclo §3.1 con el mismo arnés: los escenarios nuevos deben fallar en rojo antes de implementarse.
4. **(Orquestador, unos 10 min)** Compuertas G1–G8 + CI + mutantes.
5. **(Humano, unos 5 min)** Revisión, intervenciones registradas y exportación de logs.

El diseño mantiene tareas pequeñas y archivos con dueño único para que un requisito nuevo no requiera coordinación compleja.

## 11. Transparencia y logs

- Cada sesión (orquestador y subagentes) se exporta a `/logs/<fecha>_<fase>_<rol>_<id-corto>.jsonl` junto con un resumen `.md` (tarea, resultado y commits).
- **Sanitizado obligatorio antes de commitear** (el repositorio es público): se eliminan correos electrónicos, llaves y tokens (patrones de gitleaks), contenido de `.env` y el contexto de sistema con datos personales. El sanitizado **no** altera el contenido técnico de las conversaciones y se aplica de forma documentada y reproducible (`scripts/sanitizar-logs`, que escribe DevOps).
- Los logs se añaden y nunca se editan después de commiteados.
- Las intervenciones humanas van a `INTERVENCIONES.md` en el formato de ese archivo, incluidas las correcciones de rumbo y los rechazos manuales.
