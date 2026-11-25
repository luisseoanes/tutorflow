# FABRICA.md — La fábrica de agentes de TutorFlow

> Cómo se construye TutorFlow sin que el humano escriba código: agentes, planificación, ejecución, revisión, integración, costos, fallos y parada.
> Reglas comunes: `AGENTS.md`. Qué se construye: `SPEC.md`. Cómo se verifica: `escenarios/` (criterio a criterio, con niveles de verificación de `AGENTS.md` §6.3).

---

## 1. Principios de diseño

1. **Pocos agentes, propiedad disjunta.** Un orquestador y tres constructores. Cada ruta del repositorio tiene **un solo dueño** (§4.1): nada de conflictos de *merge* ni de coordinación sobre archivos compartidos.
2. **Verificar en lugar de confiar.** El orquestador reejecuta la compilación, las pruebas y las verificaciones y lee el diff. Un "terminado" sin evidencia reproducible se rechaza.
3. **Prueba primero, desde los criterios.** Cada criterio de los escenarios se convierte en una aserción concreta antes de implementar.
4. **El arnés también se prueba.** Los mutantes (§5.3) demuestran que las pruebas detectan errores reales; una prueba etiquetada no es evidencia por sí sola.
5. **Determinismo antes que LLM.** Lo que puede ser código determinista lo es. El LLM solo interpreta y redacta, y nunca decide permisos.
6. **Transparencia.** Sesiones exportadas a `/logs`, intervenciones en `INTERVENCIONES.md` y ninguna métrica inventada.
7. **El costo es un requisito.** Menos agentes = menos contexto repetido. El modelo se asigna por rol y los reintentos tienen tope.
8. **Quien construye no se aprueba a sí mismo.** Donde la verificación automática no basta (interfaz web) o el riesgo lo justifica, un **revisor independiente** (agente QA) evalúa la entrega con su propia ejecución y evidencia; puede rechazarla; el implementador corrige; el revisor re-verifica. La aceptación final la conserva el humano (§5.4, `AGENTS.md` §13).

## 2. Implementación concreta

| Elemento | Implementación |
|---|---|
| Herramienta | Claude Code (CLI / extensión VS Code). |
| Orquestador | La sesión principal de Claude Code: planifica, despacha, revisa e integra. No escribe código de producto. |
| Constructores | 3 subagentes definidos en `.claude/agents/{core,ia,arnes}.md` (configuración de agentes **escrita por el humano** antes de la fase 2): *prompt* del rol, herramientas permitidas y modelo. |
| Revisor independiente | Subagente `.claude/agents/qa.md` (INT-015): revisa entregas ajenas, escribe solo en `qa/**`, nunca implementa ni corrige lo que revisa (§5.4). |
| Aislamiento | En paralelo, cada tarea corre en un **git worktree** propio sobre la rama `agent/<rol>/<tarea>`. |
| Permisos | `.claude/settings.json` (humano): permite `dotnet`, `docker compose` y `git` sin operaciones destructivas; `deny` explícito para `push --force`, `rebase`, `reset --hard`, `filter-branch` y la lectura de `.env`. |
| Integración | El orquestador ejecuta `git merge --no-ff` en `main` tras pasar las compuertas (§5.1). |
| CI | GitHub Actions (la escribe Arnés) como compuerta final independiente (§5.2). |

### 2.1 Modelos por rol

| Rol | Modelo | Justificación |
|---|---|---|
| Orquestador | `claude-opus-5-5` | Planificación y revisión crítica de diffs: un error de juicio aquí es lo más caro. |
| Core, IA, Arnés | `claude-sonnet-5-5` | Implementación guiada por especificación y criterios: buena relación calidad/costo. |
| QA (revisor) | `claude-sonnet-5-5` | Revisión con ejecución propia (navegador, axe, capturas); mismo nivel que el implementador para que la revisión sea creíble. |
| Tareas mecánicas (sanitizar logs, matrices) | `claude-haiku-5-5` | Repetitivas y de bajo riesgo. |
| LLM del **producto** | `claude-haiku-5-5` por defecto | Se valida en evals frente a `claude-sonnet-5-5`; se queda si cumple los umbrales de SPEC §7.6. |

## 3. Ciclo de vida

| Fase | Entrada | Salida | Compuerta de salida |
|---|---|---|---|
| **F0** Especificación | Enunciado + material | SPEC, AGENTS, FABRICA, escenarios, INTERVENCIONES y configuración base | Documentos coherentes |
| **F1** Revisión humana | F0 | Revisión crítica aplicada (INT-004), `.claude/agents/*.md`, `.claude/settings.json` | El humano autoriza la fase 2 (queda en INTERVENCIONES) |
| **F2** Construcción | Plan §6 | Código, pruebas, Docker y CI en `main` | SPEC §14 puntos 1–3 y 5–7 |
| **F3** Evaluación | `main` en verde | `evals/RESULTADOS.md`, estrategia y modelo elegidos | SPEC §14 punto 4 |
| **F4** Cierre | Todo lo anterior | `/logs` sanitizados; el humano redacta `REPORTE.md` con datos reales | Revisión humana |
| **F5** Corrida en vivo | Requisito nuevo | Delta de SPEC + escenarios (humano) → F2–F3 reducido | §10 |

### 3.1 Ciclo de cada tarea

```
Orquestador: elige una tarea lista → brief (§4.0) → despacha
Agente:      lee AGENTS + secciones de SPEC + criterios → pruebas por criterio (rojo) → implementa (verde)
             → refactoriza → verificaciones locales → commit(s) con trailer → handoff (AGENTS §11)
Orquestador: compuertas G1–G8 → { merge --no-ff | rechazo con feedback concreto (máx. 3 ciclos) | escalar al humano }
```

## 4. Agentes

### 4.0 Brief de despacho

```
Tarea T-xx · Rol · Rama agent/<rol>/<tarea>
Objetivo: (1–3 frases)
Criterios: (ids con su nivel)  ·  Secciones de SPEC: (§)
Rutas autorizadas: (de §4.1)  ·  Dependencias integradas: (T-xx con sha)
Verificación exigida: (comandos exactos)  ·  Presupuesto: (ciclos, tiempo)
```

### 4.1 Propiedad de rutas (tabla única; ninguna ruta tiene dos dueños)

| Ruta | Dueño |
|---|---|
| `TutorFlow.sln`, `global.json`, `Directory.Build.props`, `src/*/*.csproj`, `tests/*/*.csproj` | **Core** |
| `src/TutorFlow.Domain/**` | **Core** |
| `src/TutorFlow.Application/**` salvo `Retrieval/` y `Llm/` | **Core** |
| `src/TutorFlow.Infrastructure/**` salvo `Retrieval/` y `Llm/` | **Core** |
| `src/TutorFlow.Api/**` | **Core** |
| `src/TutorFlow.Application/{Retrieval,Llm}/**`, `src/TutorFlow.Infrastructure/{Retrieval,Llm}/**` | **IA** |
| `tests/TutorFlow.Domain.Tests/**`, `tests/TutorFlow.Architecture.Tests/**` | **Core** |
| `tests/TutorFlow.Application.Tests/**` salvo `Retrieval/` y `Llm/` | **Core** |
| `tests/TutorFlow.Application.Tests/{Retrieval,Llm}/**` | **IA** |
| `tests/TutorFlow.Api.Tests/**` salvo `Contract/` | **Core** |
| `tests/TutorFlow.Api.Tests/Contract/**` | **Arnés** |
| `evals/**`, `scripts/**`, `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.github/**`, `README.md` | **Arnés** |
| `web/**` (incluidos `web/Dockerfile` y la configuración del servidor estático de la web) | **Core** |
| `qa/**` (informes, evidencias y verificaciones de la revisión independiente) | **QA** |
| `logs/*.jsonl`, `logs/*.md` (salvo `logs/README.md`) | **Arnés** (solo añade exportaciones sanitizadas) |
| `SPEC.md`, `SPEC-WEB.md`, `AGENTS.md`, `FABRICA.md`, `INTERVENCIONES.md`, `REPORTE.md`, `logs/README.md`, `escenarios/**`, `.claude/**`, `.gitignore`, `.env.example`, `.gitleaks.toml`, `q10/**` | **Humano** (los agentes solo leen) |

Reglas de borde:

- Un paquete NuGet nuevo en un `.csproj` → lo pide IA o Arnés en su handoff y lo añade Core en una tarea explícita. Los paquetes aprobados de antemano (AGENTS §4) ya los incluye Core al crear el esqueleto.
- DI: cada módulo registra sus servicios en su propio método de extensión (AGENTS §4); `Program.cs` (Core) solo los invoca.
- Un cambio de puerto (interfaz de SPEC §12.1) lo hace **Core** en una tarea explícita; IA y Arnés lo consumen.

### 4.2 Fichas

#### Core
- **Responsabilidades:** el esqueleto (solución, proyectos, puertos, DTOs de salida, métodos de extensión DI vacíos de cada módulo, prueba de arquitectura); el dominio académico (SPEC §5); `PrincipalAcademico` y la política de autorización (§6.1); L1, el roster (§6.6), L2a/L2b (§6.2, §6.7), L5 y el 403 en sus dos variantes; el cliente de la API simulada; las herramientas (§8) y su **despachador** (`IToolDispatcher`: recibe una propuesta `{nombre, argumentos}` y aplica L3 → esquema → L4 → ejecución); el caso de uso y el flujo (§12.3); la API con la lectura y validación del cuerpo (§3.3); el *middleware* de errores; la auditoría (§9).
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`, `node` para la API simulada local).
- **Criterios:** todos los `[dom]` y todos los `[app]`/`[api]` que no estén asignados a IA (ficha IA) ni a Arnés (RES-02).
- **Criterios de aceptación:** criterios asignados en verde con aserciones que corresponden al texto; mutantes M1–M8, M11–M13, M15 y M16 detectados; cero `double` en Domain; ningún dato fijo en `src/`; los vocabularios de L2b son exactamente los de SPEC §6.7 (o un superconjunto que mantiene AUTH-25 en verde).
- **Tareas:** T-01, T-02, T-05a–T-05e y T-08 (§6); interfaz web T-13 y sus correcciones (§6.1).
- **Interfaz web (P2, `SPEC-WEB.md`):** implementa `web/**` según los tokens, componentes, estados y restricciones de SPEC-WEB (sin permisos ni cálculos en el navegador, sin almacenamiento persistente, contrato intacto); entrega la evidencia de `AGENTS.md` §13.3; **no** escribe en `qa/**` ni puede aprobar su propia entrega.
- **Condiciones de fallo:** una herramienta lee antes de autorizar; un permiso derivado del texto o del LLM; `ToEven`; se ignora un `null` en cortes o asistencia; el roster se atribuye a la petición o se envía al LLM.
- **Handoff / evidencia:** AGENTS §11, con la lista criterio → prueba → aserción.

#### IA
- **Responsabilidades:** el chunking estructural y la cuarentena por lista (SPEC §6.4, §7.2); el detector de instrucciones **como prueba**; las estrategias `full-context` y `bm25` (ejecución) y `vector`/`hybrid` (experimento, §7.3); los proveedores `anthropic` y `openai-compatible` detrás de `ILlmClient`, con *usage* exacto, reintentos y tope de tokens; el bucle de herramientas (≤ 3 rondas) que **solo propone** invocaciones y las entrega al `IToolDispatcher` de Core (IA nunca llama a las herramientas ni a la fuente de datos directamente); la validación de citas y la abstención (§7.5); `tokens` y `costoUsd`.
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`).
- **Criterios:** RAG-01.b, RAG-09.a, RAG-13..RAG-15, ACAD-07.b, RES-05.a/b/d, RES-06, RES-07, RES-11.b, RES-13.a, ADV-07.a/b, ADV-08.a, ADV-14.a, ADV-15.b/c (todos `[app]` en `tests/TutorFlow.Application.Tests/{Retrieval,Llm}/`). Los criterios `[api]` relacionados son de Core.
- **Criterios de aceptación:** criterios en verde; mutantes M9, M10 y M14 detectados; los adaptadores de proveedor se prueban con respuestas HTTP grabadas (sin red).
- **Condiciones de fallo:** citas de chunks no recuperados; el Anexo o el 2023 (sin mención) en el contexto; una llave en un log; un SDK de proveedor sin aprobación; una abstención previa al LLM.
- **Handoff / evidencia:** AGENTS §11 + el volcado de los chunks generados (`chunkId`, `seccion`, `untrusted`, `vigente`).

#### Arnés
- **Responsabilidades:** las pruebas de contrato (RES-02, JSON Schema con `additionalProperties: false`, para 200 y los dos 403); `evals/` (dataset con `criterios`, runner en modo `[ret]` determinista y modo `[eval]` con LLM real, métricas de SPEC §7.6, experimento de estrategias, `RESULTADOS.md` **solo con ejecuciones reales**); `scripts/trazabilidad` (AGENTS §6.4); `scripts/mutantes` (§5.3); Dockerfile, Compose y CI (§5.2); gitleaks; la verificación del trailer; `README.md` (cómo ejecutar, variables y limitación de autenticación de SPEC §6.5); **la exportación y el sanitizado de todas las transcripciones a `/logs`** (`scripts/sanitizar-logs`, que puede ejecutarse con Haiku), siempre **a solicitud del orquestador** (§11).
- **Herramientas:** Read, Write, Edit, Bash (`dotnet`, `docker`, `docker compose`); acceso a `LLM_API_KEY`/`EMBEDDING_API_KEY` **solo** en el modo `[eval]`.
- **Criterios:** todos los `[ret]`, `[cont]` y `[eval]`, más RES-02.
- **Criterios de aceptación:** la CI bloquea de verdad (con evidencia de una ejecución en rojo provocada a propósito: commit sin trailer, secreto de prueba, mutante sobreviviente); `docker compose up` funcional; RESULTADOS con commit, fecha, modelo y estrategia.
- **Condiciones de fallo:** cifras no ejecutadas; umbrales relajados; valores golden calculados con el código del producto; el job `[eval]` como requisito de los PR; `continue-on-error` en las compuertas; una llave en una imagen o en los logs de la CI.
- **Handoff / evidencia:** AGENTS §11 + enlaces a ejecuciones de CI en verde y en rojo + archivos crudos `evals/runs/<fecha>-<sha>.json`.
- **Infraestructura web (T-12):** servicio `web` en `docker-compose.yml` (puerto 3000, proxy de `/api/*` a `tutorflow:8080`, cabeceras de SPEC-WEB §9; la imagen la define `web/Dockerfile` de Core), jobs de CI `web` y `web-e2e`, soporte de los niveles `[web]`, `[e2e]` y `[qa]` en `scripts/trazabilidad`, y el job `propiedad` (§5.2). No escribe en `web/src` ni en `qa/**`.

#### QA (revisor independiente)
- **Responsabilidades:** revisión independiente de entregas ajenas según `AGENTS.md` §13: obligatoria para la interfaz web (T-14 sobre cada entrega y corrección de T-13), opcional para componentes críticos (§5.4). Diseño y consistencia visual, UX, calidad funcional con la API, accesibilidad y calidad técnica (`SPEC-WEB.md`, `escenarios/web.md`).
- **Herramientas:** Read, Write, Edit, Bash (`npm`, `npx playwright`, `docker compose` con el override sin llave). Nunca la llave del LLM.
- **Rutas:** solo `qa/**` (y `handoff.md` sin commitear).
- **Criterios:** todos los `[qa]` de `escenarios/web.md`.
- **Criterios de aceptación de su trabajo:** informe completo en el formato de §13.4 sobre el commit candidato exacto, con salidas literales de sus propias ejecuciones y evidencias (capturas propias en 375×812, 768×1024, 1440×900) que respalden cada hallazgo y el resultado.
- **Condiciones de fallo:** aprobar sin ejecutar nada propio; aprobar con hallazgos críticos o altos abiertos; modificar el trabajo revisado; modificar un informe ya commiteado; revisar una tarea que implementó; usar la llave del LLM.
- **Handoff / evidencia:** el informe (`qa/revisiones/<tarea>-r<n>.md`) + evidencias (`qa/evidencias/<tarea>-r<n>/`), commiteados con `Revision: <tarea> r<n> <RESULTADO>`, `Agente: qa` y los trailers obligatorios.

## 5. Revisión y compuertas

### 5.1 Compuertas de aceptación de una rama (en orden; si una falla, se rechaza)

| # | Compuerta | Cómo se verifica |
|---|---|---|
| G1 | Alcance | `git diff --name-only main...<rama>` ⊆ rutas del dueño (§4.1). |
| G2 | Trailer | `git log main..<rama> --format=%B` → cada commit tiene `Generado-por: Claude Code` en su bloque de trailers. |
| G3 | Compilación | El orquestador ejecuta `dotnet build -warnaserror`. |
| G4 | Pruebas | El orquestador ejecuta `dotnet test` **completo** y, si aplica, `evals` en modo `[ret]`. |
| G5 | Trazabilidad **por criterio** | `scripts/trazabilidad` (en cuanto exista; antes, una revisión manual): cada criterio que declara el handoff tiene una prueba con `Trait("Criterio", …)` del **nivel indicado** en el escenario, y pasa. |
| G6 | **Aserciones y diff** | Por cada criterio declarado, el orquestador **lee la prueba** y confirma que sus aserciones corresponden al texto del criterio (por ejemplo, AUTH-03.b exige comprobar el espía de `IAcademicDataSource`, no solo el status). Además revisa el diff buscando: `double`/`float` en Domain, `ToEven`, datos fijos, `catch (Exception)`, `Skip`, umbrales cambiados, secretos, permisos derivados del texto o del LLM, lecturas antes de `Authorize`, el roster atribuido o enviado al LLM, y vocabularios de L2b modificados sin que AUTH-06 y AUTH-25 sigan en verde. |
| G7 | Evidencia | El handoff cumple AGENTS §11 y la salida reportada coincide con la reejecución. **Una discrepancia equivale a un rechazo automático y queda registrada.** |
| G8 | Integración y mutantes | `merge --no-ff` en una rama temporal + `dotnet test` completo + los mutantes del área tocada (§5.3) antes de mover `main`. |
| G9 | Revisión independiente (solo entregas sujetas a §5.4) | Existe `qa/revisiones/<tarea>-r<n>.md` con **APPROVED** sobre el **mismo commit** que se integra, sin hallazgos críticos ni altos abiertos; los medios están corregidos o aceptados por el humano; y, para la interfaz web, la **aceptación humana** está registrada en `INTERVENCIONES.md`. Sin esto no hay *merge*. |

### 5.2 CI (GitHub Actions): qué bloquea y qué no

| Job | Contenido | ¿Bloquea el PR? |
|---|---|---|
| `build-test` | `dotnet build -warnaserror` + `dotnet test` (`[dom]`, `[app]`, `[api]`) | Sí |
| `retrieval` | evals en modo `[ret]` con los umbrales de SPEC §7.6 aplicables | Sí |
| `trazabilidad` | AGENTS §6.4 | Sí |
| `mutantes` | §5.3, **solo** si el PR toca `src/TutorFlow.Domain/**`, `src/TutorFlow.Application/**` o `src/TutorFlow.Infrastructure/{Retrieval,Llm}/**`; siempre en `main` | Sí (cuando corre) |
| `secretos` | gitleaks sobre el diff y la historia | Sí |
| `lights-out` | AGENTS §9.4 | Sí |
| `smoke` | `docker compose up` → criterios `[cont]` (RES-01, RES-03) | Sí |
| `evals-llm` | Criterios `[eval]` con el LLM real | **No**: manual o en `main` si existe el secreto; publica las métricas como artefacto |
| `web` | `npm ci`, `tsc --noEmit`, pruebas `[web]` (incluido axe en jsdom), `vite build` — solo si el PR toca `web/**`; siempre en `main` cuando `web/` exista | Sí (cuando corre) |
| `web-e2e` | Playwright `[e2e]` contra el servicio `web` + API **sin llave** (override de T-07b) y rutas simuladas | Sí (cuando corre) |
| `propiedad` | Cada commit de agente solo toca las rutas de su `Agente:` según la tabla §4.1 (en particular, ningún commit `Agente: core` toca `qa/**` y ningún `Agente: qa` toca algo fuera de `qa/**`) | Sí |

### 5.3 Catálogo de mutantes (prueba de eficacia del arnés)

Cada mutante es un parche aplicado en una copia temporal; **debe** hacer fallar al menos uno de los criterios listados.

| Mutante | Criterios que deben detectarlo |
|---|---|
| M1 `AwayFromZero` → `ToEven` | ACAD-02.a, ACAD-05.a |
| M2 `decimal` → `double` en la definitiva | ACAD-02.a..ACAD-04.a, ACAD-05.b |
| M3 Umbral de inasistencia 15 → 20 | ACAD-08.a, ACAD-09.a, AUTH-20.a |
| M4 `>` → `>=` en el umbral | ACAD-10.a |
| M5 Habilitación 2.0 → 1.5 | ACAD-15.a |
| M6 Las asignaturas sin definitiva cuentan como 0 | ACAD-14.a |
| M7 `Authorize` devuelve siempre `Permitido` | AUTH-22.a, ADV-10.a |
| M8 L2a desactivada | AUTH-03.c |
| M9 La cuarentena del Anexo desactivada | RAG-14.a, ADV-07.a |
| M10 La validación de citas desactivada | RAG-13.a |
| M11 Comparación de rol que ignora mayúsculas | AUTH-10.a |
| M12 El coordinador sin verificación de programa en L4 | AUTH-17.b, AUTH-18.a |
| M13 El roster se cuenta en `estudiantesConsultados` | AUTH-24.b |
| M14 La abstención posterior desactivada (responde sin citas) | RAG-09.a |
| M15 L2b desactivada | AUTH-06.b, AUTH-18.c, ADV-01.c |
| M16 R2 invertida (un sujeto genérico SD basta para marcar) | AUTH-25.a |

### 5.4 Revisión independiente

- **Obligatoria** para la interfaz web (cada entrega y cada corrección de T-13). **Opcional**, a criterio del orquestador, para cambios en autorización/identidad (L1–L5, política, despachador), contrato HTTP o auditoría; en ese caso el brief al revisor usa la lista de verificación de seguridad y el mismo formato de informe. No se aplica a tareas triviales.
- **Ciclo:** implementador entrega (evidencia §13.3) → orquestador aplica G1–G8 → despacha al revisor con el commit candidato exacto → informe → si `CHANGES_REQUESTED`, el orquestador devuelve los hallazgos tal cual al implementador → nuevo commit → **nueva ronda** del revisor (verifica cada hallazgo anterior) → … → `APPROVED` → G9 → aceptación humana (web) → `merge --no-ff`.
- **Independencia:** el revisor trabaja en su propio worktree a partir del commit candidato, levanta la app por su cuenta y no reutiliza las pruebas del implementador como única evidencia. El implementador no edita `qa/**`; el revisor no edita el trabajo revisado; lo comprueban G1 y el job `propiedad`.
- **Límite:** 3 rondas `CHANGES_REQUESTED`; después, escalado al humano (§8).
- **Limitación conocida:** `.claude/settings.json` no puede asignar rutas por agente; la separación de rutas la garantizan G1, el job `propiedad` y la revisión del orquestador, no los permisos de la herramienta.

## 6. Plan de ejecución y paralelismo

```
W0 ─ T-01 Core: esqueleto (solución, puertos, DTOs, extensiones DI, prueba de arquitectura)        [secuencial]
W1 ─┬ T-02 Core: dominio académico + política + PrincipalAcademico (criterios [dom])               ┐
    ├ T-03 IA: chunking + cuarentena + bm25 + full-context (RAG-14, RAG-15)                        │ paralelo
    └ T-04 Arnés: CI base (build-test, secretos, lights-out) + Docker + dataset [ret] + trazabilidad┘ (3 worktrees)
W2 ─┬ Core (secuencial): T-05a → T-05b → T-05c → T-05d → T-05e   (ver tabla)                       ┐ paralelo
    ├ T-06 IA: proveedores LLM + bucle + citas/abstención + tokens/costo (dep. T-03)               │
    └ T-07 Arnés: pruebas de contrato + mutantes + smoke (dep. T-01, T-04)                         ┘
W3 ─┬ T-08 Core: integración E2E (bucle IA + despachador Core) + suite ADV con LLM guionizado malicioso (dep. T-05e, T-06)
    └ T-09 IA: vector + hybrid para el experimento (dep. T-06)
W4 ─ T-10 Arnés: evals [eval] reales + experimento de estrategias y modelos + RESULTADOS.md + README (dep. T-08, T-09)
W5 ─ T-11 Arnés: exportación y sanitizado de logs (a solicitud del orquestador) · Orquestador: verificación de integridad de logs (§11) y de SPEC §14
```

**División de T-05 (Core, secuencial, cada una ≤ 45 min).** Las rutas son subcarpetas de las de Core (§4.1); las pruebas van en las carpetas homónimas de `tests/`. G1 verifica que cada tarea toque solo sus rutas.

| Tarea | Alcance (SPEC) | Rutas | Dep. | Criterios que debe dejar en verde |
|---|---|---|---|---|
| **T-05a** Fuente de datos y roster | §6.6, §10.2 (API simulada) | `src/TutorFlow.Infrastructure/{MockApi,Roster}/**` | T-02 | AUTH-23.b, AUTH-24.a + pruebas internas del cliente (timeouts, reintentos, URL-encoding, validación de datos) |
| **T-05b** Cuerpo, identidad (L1) y API | §3.2–§3.4, §6.2 L1, §6.5 | `src/TutorFlow.Api/**`, `src/TutorFlow.Application/{Identity,Preguntar}/**` (crea el caso de uso hasta L1) | T-05a | AUTH-08.a, AUTH-09.a..AUTH-14.a, AUTH-13.b, AUTH-15.a, AUTH-15.c, AUTH-23.a, ADV-15.a, ADV-16.a, ADV-16.b, RES-01.a, RES-04.a, RES-09.a |
| **T-05c** Guardias L2a, L2b y L5 | §6.2 L2a/L2b/L5, §6.7 | `src/TutorFlow.Application/Security/**`, `src/TutorFlow.Application/Preguntar/**` (añade el paso L2/L5) | T-05a, T-05b | AUTH-02.a, AUTH-03.a/b/c, AUTH-04.a/b, AUTH-05.a/b/c, AUTH-06.a/b, AUTH-07.a, AUTH-17.a, AUTH-18.c, AUTH-21.a/b, AUTH-25.a/b, ADV-01.c, ADV-02.a, ADV-05.a, ADV-13.a; mutantes M8, M15, M16 |
| **T-05d** Herramientas, despachador (L3 + esquema + L4) y citas deterministas | §6.2 L3/L4, §7.5.2–§7.5.3, §8 | `src/TutorFlow.Application/Tools/**` | T-02, T-05a | AUTH-06.c, AUTH-16.a, AUTH-19.a, AUTH-20.a, AUTH-23.c, ADV-11.a, ACAD-01.b, ACAD-06.b, ACAD-08.b, ACAD-12.b, ACAD-13.b, ACAD-15.b, ACAD-16.b, ACAD-19.b, ACAD-20.a, RES-10.a |
| **T-05e** Auditoría | §9 | `src/TutorFlow.Infrastructure/Audit/**`, `src/TutorFlow.Application/{Audit,Preguntar}/**` (añade la auditoría al flujo) | T-05b, T-05c, T-05d | AUTH-03.d, AUTH-08.b, AUTH-24.b, RES-04.b, RES-08.b; mutante M13 |
| **T-08** Integración E2E | §12.3 completo | rutas de Core (solo el cableado y las pruebas E2E) | T-05e, T-06 | AUTH-01.a/b/c, AUTH-06.d, AUTH-15.b, AUTH-16.b, AUTH-17.b, AUTH-18.a, AUTH-19.b, AUTH-24.c, ADV-01.a, ADV-04.a, ADV-06.a, ADV-10.a, ADV-12.a, ACAD-01.c, ACAD-11.a/b, RES-05.c, RES-08.a, RES-11.a, RES-12.a; mutantes M7 (E2E) y M12 |

Regla de cierre: **todo criterio `[app]`/`[api]` de Core que no aparezca en esta tabla ni en T-02 se asigna a T-08**. El orquestador lo comprueba con la matriz de trazabilidad antes de cerrar T-08.

- Paralelismo máximo: **3 agentes**, con rutas disjuntas (§4.1). Las subtareas T-05a–e son secuenciales dentro de Core (mismo dueño, sin worktrees concurrentes). El único punto de sincronización entre agentes es W0 y, después, T-08 (que espera a T-06).
- Ruta crítica: T-01 → T-02 → T-05a → T-05b → T-05c → T-05d → T-05e → T-08 → T-10.

### 6.1 Interfaz web (P2, INT-015) — no bloquea P0/P1

Empieza solo cuando T-10 y T-11 no dependan de los mismos agentes en ese momento; ninguna tarea web consume cuota del LLM (la API se usa sin llave o con rutas simuladas).

| Tarea | Rol | Alcance | Dep. | Criterios |
|---|---|---|---|---|
| **T-12** Infra web | Arnés | Servicio `web` en compose (puerto 3000, proxy `/api`, cabeceras), jobs `web`, `web-e2e` y `propiedad`, niveles `[web]`/`[e2e]`/`[qa]` en trazabilidad | T-07b | WEB-16.a/b (con T-13), job `propiedad` con evidencia de rojo provocado |
| **T-13** Interfaz web | Core | `web/**` según SPEC-WEB (tokens, componentes, estados, responsive, a11y), pruebas `[web]`/`[e2e]`, evidencia §13.3 | T-12 (para el servicio y los jobs; el código puede empezar antes) | Todos los `[web]` y `[e2e]` de `escenarios/web.md` |
| **T-14** Revisión independiente | QA | Ronda por cada entrega de T-13 (`AGENTS.md` §13) | Cada entrega de T-13 aceptada en G1–G8 | Todos los `[qa]` de `escenarios/web.md` (WEB-17) |
| **T-13.n** Correcciones | Core | Hallazgos de la ronda n | Informe `CHANGES_REQUESTED` | Los criterios afectados + verificación del revisor en la ronda n+1 |

Integración: G1–G9 + aceptación humana registrada. La trazabilidad exige los criterios `[web]`/`[e2e]` cuando T-13 esté integrada y los `[qa]` mediante el informe APPROVED.
- **Revisión de seguridad:** no es un agente; es la lista G6 que el orquestador aplica a todo diff de Core e IA.

## 7. Costos y presupuestos

### 7.1 Límites de la fábrica (son límites, no mediciones)

| Concepto | Límite | Al superarlo |
|---|---|---|
| Ciclos de rechazo por tarea | 3 | Se escala al humano (intervención) |
| Tiempo por tarea | 45 min de reloj | El orquestador la divide o la escala |
| F2 + F3 | ≈ 4 h de reloj | El humano decide recortar alcance (se prioriza P0 de SPEC §0.1) |
| Corridas `[eval]` con el LLM real | ≤ 3 completas por combinación estrategia × modelo | Cada corrida extra se justifica en `RESULTADOS.md` |
| Rondas de revisión independiente | 3 `CHANGES_REQUESTED` por tarea | Escalado al humano |

### 7.2 Tácticas de eficiencia

- 3 constructores en lugar de 6: menos arranques en frío y menos contexto duplicado.
- Briefs acotados: solo las secciones de SPEC y los criterios de la tarea.
- Opus solo en el orquestador.
- La CI determinista es gratuita; el LLM real solo en `evals-llm`.
- Producto: una sola pasada de recuperación, sin herramienta de búsqueda, salida estructurada corta y Haiku si cumple.

### 7.3 Medición (para `REPORTE.md`)

- **Fábrica:** tokens de entrada, salida y caché por sesión, sacados de las transcripciones exportadas (campo `usage`). Con un plan de suscripción, se reporta el **costo equivalente** a precio de API (con la fuente y la fecha), aclarando que no es un gasto facturado.
- **Producto:** `tokens` y `costoUsd` por pregunta en las corridas `[eval]` → costo medio × 1 000, por estrategia y modelo.
- **Ninguna** cifra que no provenga de esas fuentes.

## 8. Manejo de fallos

| Fallo | Detección | Respuesta |
|---|---|---|
| Las pruebas fallan (G4) | Reejecución | Se devuelve con la salida literal; ciclos +1 |
| Rutas fuera de propiedad (G1) | `git diff --name-only` | Rechazo y re-despacho; si se repite, se escala |
| Prueba sin aserciones del criterio (G6) | Lectura de la prueba | Rechazo con el criterio y la aserción faltante concretos |
| Evidencia inconsistente (G7) | Salida distinta a la reejecución | Rechazo; queda registrado |
| Mutante sobreviviente (G8/CI) | `scripts/mutantes` | Tarea al dueño del área para reforzar la prueba (no para cambiar el mutante) |
| Contradicción de especificación | El agente escala | El orquestador **no** cambia el contrato ni las reglas: escala al humano → intervención → el humano edita SPEC o escenarios |
| Agente sin progreso | Mismo error 3 veces | Se detiene y se re-despacha en contexto limpio con una tarea más pequeña |
| Fallo de infraestructura | Error de herramienta | 1 reintento; si persiste, se pausa y se escala |
| Conflicto de *merge* | G8 | Lo resuelve el dueño de la ruta en su rama (con §4.1 no debería ocurrir: si ocurre, se revisa la tabla) |
| Secreto detectado | gitleaks / G6 | **Parada total** (§9); el humano rota la llave; no se reescribe la historia: se documenta |
| Revisión `CHANGES_REQUESTED` | Informe del revisor | Los hallazgos se devuelven sin reinterpretar al implementador; nueva entrega; nueva ronda |
| Revisión `BLOCKED` | Informe del revisor | El orquestador resuelve lo que falta (evidencia, entorno, dependencia) o escala; nunca se integra |
| Desacuerdo implementador–revisor | Handoff con objeción | Decide el humano (intervención registrada); ni el implementador ni el orquestador anulan un hallazgo crítico o alto |
| Más de 3 rondas | Contador de rondas | Escalado al humano |

## 9. Condiciones de parada

**Éxito:** SPEC §14 completo, la CI en verde en `main`, logs exportados y el cierre firmado por el humano en `INTERVENCIONES.md`.

**Parada obligatoria (espera al humano):**

1. Un secreto o PII real en el árbol o la historia.
2. Una contradicción que requiere cambiar SPEC, los escenarios o el contrato.
3. 3 ciclos fallidos en la misma tarea.
4. Se supera un límite de §7.1.
5. Evidencia falsa repetida por el mismo agente (G7).
6. Una orden explícita del humano.

**Nunca** se "termina" desactivando compuertas, saltando pruebas, debilitando mutantes o recortando criterios sin una decisión humana registrada.

## 10. Corrida en vivo (F5, 60 minutos)

1. **(Humano, unos 10 min)** Delta en SPEC (sección marcada `[F5]`) + escenarios `NEW-nn` con criterios, nivel y severidad.
2. **(Orquestador, unos 5 min)** Asigna el dueño según §4.1; si toca puertos → Core primero.
3. **(Agente, unos 30 min)** Ciclo §3.1: los criterios nuevos en rojo antes de implementar.
4. **(Orquestador, unos 10 min)** G1–G8 + CI.
5. **(Humano, unos 5 min)** Revisión e intervenciones. El orquestador solicita a Arnés la exportación de logs y verifica su integridad (§11).

## 11. Transparencia y logs

- **Responsable único de las exportaciones: Arnés.** Cada sesión (orquestador y subagentes) se exporta a `/logs/<fecha>_<fase>_<rol>_<id-corto>.jsonl` + un resumen `.md`, más un `logs/MANIFIESTO.md` que lista cada sesión con su id, fase, rol, hash SHA-256 del original sin sanitizar y número de reemplazos por tipo.
- **El orquestador solicita y verifica:** pide la exportación al cerrar cada fase (y en F5) y antes de aceptarla comprueba (1) que cada sesión de la fase aparece en el manifiesto, (2) que gitleaks pasa con la regla `tutorflow-email-en-logs`, (3) que la diferencia entre el original y la versión sanitizada se limita a los reemplazos declarados (el script los cuenta y el orquestador lo reejecuta) y (4) que ningún archivo de `logs/` commiteado antes se modificó (`git log --diff-filter=M -- logs/` vacío, salvo `MANIFIESTO.md`, que solo crece).
- **Sanitizado obligatorio** antes de commitear (el repositorio es público): correos, llaves y tokens, contenido de `.env` y el contexto de sistema con datos personales. No se altera el contenido técnico. El proceso es reproducible (`scripts/sanitizar-logs`) y gitleaks lo verifica (regla `tutorflow-email-en-logs`).
- Los logs se añaden y no se editan después de commiteados.
