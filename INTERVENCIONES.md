# INTERVENCIONES.md — Registro de intervenciones humanas

> Aquí se registra **cada** vez que el humano dirige, corrige, redirige, rechaza o decide algo en el trabajo de los agentes.
> Intervenir no descalifica; ocultarlo sí. Registros **solo de hechos ocurridos**: no se anticipan intervenciones futuras.
> Las entradas no se editan ni se borran; las correcciones se hacen con una entrada nueva que referencia a la anterior.

## Formato

| Campo | Contenido |
|---|---|
| Id | `INT-nnn` correlativo |
| Fecha | AAAA-MM-DD (hora local si es relevante) |
| Fase | F0–F5 (ver `FABRICA.md` §3) |
| Tipo | `Dirección` (instrucción o autorización) · `Decisión` (resuelve una ambigüedad) · `Corrección` (arregla una salida de agente) · `Rechazo` (no acepta un entregable) · `Parada` (detiene la fábrica) |
| Qué pasó | Situación que motivó la intervención |
| Qué hizo el humano | Acción concreta |
| Por qué | Motivo |
| Impacto | Archivos, decisiones o tareas afectadas |
| Evidencia | Log en `/logs`, commit o archivo |

---

## Registro

### INT-001 · 2026-11-25 · F0 · Dirección
- **Qué pasó:** inicio de la prueba técnica.
- **Qué hizo el humano:** instruyó al agente (Claude Code) para leer completo el enunciado PDF y el material del candidato, fijó las reglas lights-out (no escribe código ni pruebas; los agentes escriben código, pruebas, evals, Docker y CI; trailer `Generado-por: Claude Code`; registro en este archivo; transcripciones en `/logs`; sin *squash*, reescritura ni *force push*) y pidió **solo un análisis**, sin implementar.
- **Por qué:** entender requisitos, restricciones, entregables, criterios de evaluación y riesgos antes de diseñar.
- **Impacto:** el agente entregó el análisis en la conversación; no se crearon archivos.
- **Evidencia:** transcripción de la sesión F0 (pendiente de exportar a `/logs`, ver §Pendientes).

### INT-002 · 2026-11-25 · F0 · Dirección + Decisión
- **Qué pasó:** revisado el análisis, el humano autorizó la fase 1 (especificación y diseño de la fábrica).
- **Qué hizo el humano:** fijó las decisiones definitivas:
  1. El repositorio de GitHub será **público** (resuelve la contradicción "privado" (p. 1) / "público" (§5) del enunciado; ver SPEC D-08).
  2. La arquitectura de IA estará desacoplada del proveedor LLM, que se selecciona por configuración y variables de entorno.
  3. La credencial es `LLM_API_KEY`.
  4. Prioridades: calidad, seguridad, verificabilidad y eficiencia económica.
  5. Backend .NET 8 o superior; Docker Compose en el puerto 8080; integración con la API simulada original; contrato HTTP exacto.

- **Por qué:** cerrar las ambigüedades de mayor impacto antes de construir.
- **Impacto:** todos los documentos de F0.
- **Evidencia:** transcripción de la sesión F0 (pendiente de exportar).

### INT-003 · 2026-11-25 · F1 · Dirección
- **Qué pasó:** los documentos de F0 estaban listos; el material de Q10 estaba en la raíz y mezclado con el trabajo propio.
- **Qué hizo el humano:** pidió (1) reorganizar las carpetas separando lo entregado por Q10 de TutorFlow, con el PDF incluido, (2) crear el repositorio `tutorflow` en GitHub con `gh` y subirlo todo, y (3) hacer una revisión crítica de los documentos **sin modificarlos** hasta su aprobación.
- **Acción del agente:** movió el material a `q10/material/` y el enunciado a `q10/enunciado/prueba-tecnica-senior-fullstack-ia.pdf`; actualizó **solo las rutas** en SPEC, AGENTS, FABRICA y `.gitignore` (cambio mecánico derivado de la reorganización). El PDF queda **local e ignorado por git** hasta que el humano confirme si se publica (está marcado "Confidencial para el candidato" y, una vez en un repositorio público, no se puede retirar de la historia sin reescribirla, cosa que prohíben las reglas).
- **Por qué:** estructura clara entre los insumos de Q10 y el producto; publicar la base antes de la fase 2.
- **Impacto:** rutas `q10/material/**` en los documentos; primer commit y repositorio público.
- **Evidencia:** commit inicial; transcripción de la sesión (pendiente de exportar).

### INT-004 · 2026-11-25 · F1 · Rechazo parcial + Decisión
- **Qué pasó:** a petición del humano, el agente hizo una revisión crítica de los documentos de F0 sin modificarlos y reportó 23 hallazgos: 5 bloqueantes (B1–B5), 9 importantes (I1–I9) y 9 menores (m1–m9). Los principales:
  - B1: la guardia L2 contradecía los escenarios que prohibían leer listados de programa.
  - B2: una prueba etiquetada no demostraba que el comportamiento estuviera probado, y los criterios que dependen del LLM real eran incompatibles con una CI bloqueante.
  - B3: propiedad de rutas solapada y 7 roles para un proyecto de 3–4 h.
  - B4: la abstención previa al LLM rompía las preguntas de datos.
  - B5: 400 frente a 403 ambiguo para los errores de tipo.
  - I1: la falta de autenticación se presentaba como resuelta.
  - I2: el Art. 22 se citaba en todo 403.
  - I3: LLM falso en ejecución.
  - I4–I9: embeddings, herramienta de búsqueda redundante, clasificación del LLM, auditoría fatal, deducción del proveedor y cuarentena heurística.
- **Qué hizo el humano:** aprobó los 23 hallazgos con cuatro precisiones:
  1. (B1) El `IRosterProvider` usa solo los datos mínimos para detectar referencias, y su carga y caché se distinguen en la auditoría de las consultas académicas del usuario.
  2. (I6) Ninguna clasificación del LLM determina la autorización; los permisos se verifican de forma determinista en las herramientas, incluido el alcance del coordinador por programa. Por eso se **eliminó** el campo `solicitaDatosAjenos`, en lugar de limitarlo al rol estudiante como proponía el hallazgo.
  3. (I1) Se documenta explícitamente que la identidad del cuerpo no es autenticación y que la suplantación de una identidad válida no está mitigada.
  4. (B2) Las compuertas verifican las aserciones concretas de cada criterio y distinguen las pruebas automatizadas de las evaluaciones con LLM real.

  Autorizó corregir **solo documentación y configuración de agentes**, en un único commit, sin iniciar la implementación.
- **Por qué:** reducir riesgos y complejidad antes de la fase 2, sin aumentar el alcance.
- **Impacto:**
  - `SPEC.md` v1.1: §3.2–§3.4 (orden 400/403, dos variantes de 403), §6.2–§6.6 (L1 como consistencia, sin decisión del LLM, limitación de autenticación, cuarentena por lista, roster), §7.3–§7.5 (sin abstención previa ni herramienta de búsqueda; vector e hybrid solo en el experimento), §8, §9 (eventos, `stdout` principal), §11, §12, D-03/D-05/D-10/D-13/D-14 y §14.
  - `AGENTS.md`: niveles de verificación, trazabilidad por criterio, severidades, DI por módulo, trailer en bloque.
  - `FABRICA.md`: orquestador + 3 agentes (Core, IA, Arnés), tabla única de propiedad, compuertas G5/G6 por criterio, CI bloqueante frente a no bloqueante, mutantes M12–M14, plan de 3 en paralelo.
  - Escenarios: criterios numerados con nivel; RAG-03, ADV-03 y ADV-09 fusionados (lápidas); nuevo AUTH-24 (verificación del roster, B1); 403-acceso y 403-identidad separados.
  - `.env.example`.
- **Evidencia:** el commit de esta corrección; transcripción de la sesión (pendiente de exportar).

### INT-005 · 2026-11-25 · F1 · Decisión + Corrección
- **Qué pasó:** el humano revisó el commit `cc72d5c`.
- **Qué hizo el humano:**
  1. **Ratificó:** D-04 (citas de datos «Datos del estudiante» y «Datos del programa»); D-06 (`inasistencias: null` = dato no disponible, sin presumir pérdida); D-07 (nota acumulada = aporte ponderado acumulado + porcentaje evaluado); D-08 (el PDF confidencial permanece fuera del repositorio público); D-11 (coordinadores con N programas); D-12 (límite de 1000 caracteres); y Haiku como **modelo candidato inicial**, sujeto a la evaluación comparativa y al cumplimiento de umbrales.
  2. **Pidió corregir, solo en documentación:**
     - (a) Unificar en Arnés la propiedad de las exportaciones de `/logs`; el orquestador solicita y verifica su integridad.
     - (b) Dividir T-05 en tareas Core más pequeñas, con dependencias y criterios.
     - (c) Revisar D-03 y fortalecer la detección determinista de solicitudes de datos ajenos, sin que el LLM conceda ni deniegue permisos.
     - (d) Registrar esto aquí.
     - (e) Comprobar la coherencia entre SPEC, AGENTS y FABRICA.
- **Acción del agente:**
  - (a) FABRICA §4.2 (Arnés), §6 (T-11) y §11 (manifiesto + 4 verificaciones de integridad del orquestador); `logs/README.md`.
  - (b) T-05 se divide en T-05a (fuente de datos y roster), T-05b (cuerpo, L1 y API), T-05c (L2a, L2b y L5), T-05d (herramientas, despachador y citas) y T-05e (auditoría), con rutas, dependencias y criterios; T-08 recoge la integración y una regla de cierre para los criterios no listados.
  - (c) SPEC §6.2 divide L2 en L2a (referencias explícitas) y L2b (solicitudes de terceros); la nueva §6.7 define vocabularios cerrados (TD, ST, SD), reglas R1–R6 y corpus positivo y negativo; se actualizan D-03 y §15. AUTH-06 pasa a exigir 403 determinista en un corpus de 9 entradas; nuevo AUTH-25 (corpus negativo y falsos positivos aceptados); AUTH-18.c y ADV-01.c; mutantes M15 y M16. Contrato interno `IToolDispatcher` (SPEC §12.1, AGENTS §4) para que el bucle del LLM no pueda ejecutar herramientas por fuera de L3/L4.
- **Por qué:** cerrar las ambigüedades restantes antes de F2 y reducir el caso de "200 con negativa" a las formulaciones fuera de los patrones.
- **Impacto:** `SPEC.md` (v1.2), `AGENTS.md`, `FABRICA.md`, `escenarios/authorization.md`, `escenarios/adversarial.md`, `logs/README.md` e `INTERVENCIONES.md`.
- **Evidencia:** el commit de esta corrección; transcripción de la sesión (pendiente de exportar por Arnés en F2).

### INT-006 · 2026-11-25 · F1 · Decisión + Dirección
- **Qué pasó:** la verificación de las condiciones de entrada de F2 detectó que faltaban `CLAUDE.md`, `.claude/settings.json` y `.claude/agents/{core,ia,arnes}.md`, y que D-09 seguía pendiente. F2 no se inició.
- **Qué hizo el humano:**
  1. **Ratificó expresamente D-09:** las transcripciones de agentes se conservan en `/logs`, sanitizadas y con su manifiesto de integridad; la auditoría funcional de TutorFlow se emite a `stdout` en formato estructurado; ambos registros son independientes; los logs de Claude Code no sustituyen la auditoría funcional; un fallo al escribir la copia opcional de auditoría no impide atender una solicitud válida.
  2. **Autorizó al agente a preparar** los cinco archivos de configuración (que según FABRICA §2 son de autoría humana), validarlos y publicarlos en un único commit, **sin** iniciar F2, implementar ni lanzar agentes.
- **Acción del agente:**
  - Creó `CLAUDE.md`, los tres perfiles (modelo `claude-sonnet-5-5`, herramientas `Read, Write, Edit, Bash`, rutas exactas de FABRICA §4.1) y `.claude/settings.json`.
  - Validó `settings.json` contra el esquema de SchemaStore (0 errores) y con `claude doctor` 2.1.318; corrigió las reglas `Write(path)`, que doctor señaló como ineficaces.
  - En los perfiles, aclaró, sin cambiar FABRICA, que las evals son de Arnés y que IA aporta calidad RAG con pruebas `[app]` en sus rutas.
- **Por qué:** desbloquear las condiciones de entrada de F2 sin ampliar el alcance.
- **Impacto:** `CLAUDE.md`, `.claude/**` y este archivo. No se modificaron SPEC, AGENTS, FABRICA ni los escenarios.
- **Evidencia:** el commit de configuración; transcripción de la sesión (la exportará Arnés en F2).

### INT-007 · 2026-11-25 · F2 · Dirección + Decisión
- **Qué pasó:** el candidato autorizó T-01 y el inicio de F2. Al verificar el entorno, el orquestador detectó que el equipo **no tiene el SDK de .NET** (`dotnet` no existe). Docker Desktop 29.9.2 sí está disponible. `AGENTS.md` §10 prohíbe a los agentes instalar herramientas globales, por lo que se escaló.
- **Qué hizo el humano:** eligió ejecutar el SDK dentro de Docker (imagen oficial `mcr.microsoft.com/dotnet/sdk:8.0`) en lugar de instalarlo localmente.
- **Acción del orquestador:**
  - Para no ampliar permisos, propone invocar el SDK con `docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet …`, que ya está permitido por `Bash(docker compose *)`. **No se modifica `.claude/settings.json`**, cuya edición está bloqueada para los agentes.
  - Crea la tarea previa **T-00 (Arnés)**, que no figura en `FABRICA.md` §6, porque el archivo cae en `scripts/**`, ruta de Arnés.
  - Las compuertas G3/G4 y todos los comandos `dotnet` de los briefs se ejecutarán con ese envoltorio. La CI (GitHub Actions) usará su propio SDK.
- **Por qué:** desbloquear F2 sin instalar herramientas globales ni ampliar permisos.
- **Impacto:** nueva tarea T-00 antes de T-01; los comandos de verificación usan el contenedor. No cambia SPEC, AGENTS ni FABRICA.
- **Evidencia:** transcripción de la sesión; el commit de T-00.

### INT-008 · 2026-11-25 · F2 · Decisión
- **Qué pasó:** antes de despachar T-01, el orquestador detectó una contradicción en `FABRICA.md`. La ficha de Core (§4.2) pide "métodos de extensión DI vacíos de cada módulo", pero la tabla única de propiedad (§4.1) reserva `{Application,Infrastructure}/{Retrieval,Llm}/**` a IA. Crear el stub `AddTutorFlowIa` obligaría a Core a escribir en rutas de IA. Se detuvo T-01 y se consultó.
- **Qué hizo el humano:** eligió que T-01 **no** cree el stub de IA. Core crea solo `AddTutorFlowCore` y los puertos. IA crea `AddTutorFlowIa` en sus rutas (T-03/T-06) y Core lo enlaza en `Program.cs` en T-08. Prevalece §4.1 sin excepciones.
- **Impacto:** el brief de T-01 lo refleja. FABRICA no se modifica; la ficha §4.2 se interpreta como "cada módulo crea su propio método de registro".
- **Hallazgo adicional (pendiente del candidato):** los worktrees de agentes viven en `.claude/worktrees/`, que `.gitignore` no excluye. Aparecen como no rastreados en `main` y un `git add -A` podría incorporarlos. Mientras el candidato no actualice `.gitignore` (archivo humano), los briefs exigen hacer `git add` solo con rutas explícitas, y la compuerta G1 lo verifica.
- **Evidencia:** transcripción de la sesión.

### INT-009 · 2026-11-25 · F2 · Decisión
- **Qué pasó:** al integrar T-02 (Core), el orquestador escaló dos interpretaciones que no estaban cerradas en SPEC:
  1. **Datos de un coordinador sobre sí mismo.** SPEC §6.1 solo dice "datos de otro coordinador ❌". El brief de T-02 decía "nadie ve datos de coordinadores" y la política deniega también los propios: 14 de 80 celdas permitidas en AUTH-22.
  2. **Asignatura con un corte pendiente que además pierde por inasistencia.** SPEC §5.2 (sin definitiva, no entra al promedio) frente a §5.3 (pérdida por inasistencia: definitiva efectiva 0.0 "sin importar los cortes"). Core aplicó §5.3. No hay ningún caso así en los datos.
- **Qué hizo el humano:**
  1. Ratificó **denegar también los datos del propio coordinador**. Es más restrictivo y no tiene impacto funcional, porque ninguna herramienta expone datos de coordinador.
  2. Ratificó que **prevalece §5.3**: la asignatura entra al promedio con 0.0 y cuenta como perdida.
- **Impacto:** ninguno en el código integrado (`22a08f1`). Quedan como aclaraciones de SPEC §6.1 y §5.2/§5.3, a la espera de la próxima revisión de SPEC por el candidato.
- **Evidencia:** merge de T-02 y transcripción de la sesión.

### INT-010 · 2026-11-25 · F2 · Decisión del orquestador (registrada para revisión del candidato)
- **Qué pasó:** dos ajustes operativos durante W1 y W2, que no cambian ni SPEC ni los criterios:
  1. **Trazabilidad por tareas integradas.** Si la CI exigiera desde el inicio todos los criterios bloqueantes (`AGENTS.md` §6.4), bloquearía cualquier integración hasta el final de F2. El brief de T-04 la hizo exigir solo los criterios de las tareas ya integradas en la rama, deducidas de los merge `merge: T-xx`, con la asignación de `scripts/trazabilidad/asignacion.json` transcrita de FABRICA. El modo `--completo` (todos los criterios) es el que se usará al cierre de F2 (SPEC §14) y hoy falla como debe (exit 1).
  2. **División de T-07.** FABRICA §6 hace depender T-07 solo de T-01 y T-04, pero sus criterios requieren código posterior: RES-02 necesita el flujo de T-08, RES-01.b necesita `/api/salud` (T-05b) y RES-03.b necesita la guardia L2a (T-05c). Se divide en **T-07a** (runner `[ret]` y job `retrieval`; infraestructura de mutantes con el catálogo de FABRICA §5.3, cada mutante ligado a su tarea y aplicable solo si esa tarea está integrada) y **T-07b** (pruebas de contrato RES-02, `smoke` en contenedor RES-01.b y RES-03, y la exigencia completa de mutantes), que irá después de T-08.
- **Evidencia de la CI:** la primera ejecución real en GitHub (run 45644838620, commit `8c23d84`) pasó en verde los 4 jobs bloqueantes: build-test, trazabilidad, secretos y lights-out.
- **Impacto:** el plan de §6 conserva su orden; T-07 se parte según dependencias reales. Ningún criterio se relaja: los mutantes y criterios no aplicables se reportan como "pendientes", nunca como aprobados.
- **Pendiente del candidato:** revisar ambos ajustes.

### INT-011 · 2026-11-25 · F2 · Corrección del orquestador (sin intervención humana; registrada por transparencia)
- **Qué pasó:**
  1. **T-06 rechazada en G6 (ciclo 1).** IA dejó los adaptadores HTTP de los proveedores LLM en `src/TutorFlow.Application/Llm/`, contra `AGENTS.md` §4 (la E/S solo en Infrastructure). Se corrigió moviéndolos a `src/TutorFlow.Infrastructure/Llm/` y se integró en `7d10398`.
  2. **Error del brief de T-07a, atribuible al orquestador.** El brief pidió aplicar los umbrales de SPEC §7.6 a `bm25`. SPEC los aplica a la **estrategia elegida** ("Recall@4 … no aplica a full-context"), y los criterios `[ret]` se refieren a la "estrategia por defecto". Al seguir el brief, el runner de Arnés dio un falso rojo bloqueante.
  3. **Hallazgo real que aflora el runner:** `bm25` hoy **no** cumple los umbrales (Recall@4 0.8333, MRR 0.75). Falla en q04 (falta `evaluacion_y_calificaciones.md#Sección 2` en el top 4) y en la consulta coloquial RAG-12-a ("con cuánto paso una materia?", top 4 vacío).
- **Acción del orquestador:** devolvió T-07a para evaluar criterios y umbrales sobre la estrategia por defecto (hoy `full-context`, provisional hasta T-10) y **reportar siempre** las demás estrategias como experimento no bloqueante, sin suavizar los fallos. También pidió completar los parches de los mutantes M10 y M14 (T-06 ya integrada). La mejora de `bm25` queda como insumo para T-09/T-10: si T-10 elige `bm25` como estrategia por defecto, debe cumplir los umbrales antes.
- **Impacto:** ningún umbral se relaja. Se corrige la aplicación del umbral para que coincida con SPEC.
- **Evidencia:** handoffs de T-06 y T-07a; transcripción de la sesión.

### INT-012 · 2026-11-25 · F2 · Decisión + Prueba de control
- **Qué pasó:** para T-10 (evals con el LLM real) hacía falta una credencial.
- **Qué hizo el humano:**
  1. **Pegó en el chat, de forma deliberada, una llave de Gemini (plan gratuito)** para comprobar que el manejo de secretos y el sanitizado de transcripciones (T-11) la detectan antes de publicar `/logs`. Después la **rotó**. El agente no la usó, no la guardó ni la repitió.
  2. **Guardó la llave vigente en `.env`** (ignorado por git; los agentes tienen denegada su lectura).
  3. **Proveedor del producto: Gemini** por su endpoint compatible con OpenAI (`LLM_PROVIDER=openai-compatible`), en lugar de Anthropic/Haiku. Esto sustituye el modelo candidato ratificado en INT-005, que sigue sujeto a evals y umbrales (SPEC §7.6).
- **Acción del orquestador:**
  - Las evals `[eval]` de T-10 se ejecutarán contra la API en contenedor. `docker compose` carga `.env` y el runner solo habla HTTP con `localhost:8080`, así que ningún agente lee la llave.
  - T-11 deberá enmascarar credenciales de Google por patrón (formatos `AQ.` y `AIza`) y demostrar que la llave de prueba no aparece en ningún archivo de `logs/`.
- **Pendiente del candidato:**
  - Fijar en `.env` `LLM_PROVIDER`, `LLM_BASE_URL` y `LLM_MODEL`.
  - Indicar el modelo y la tarifa de referencia (fuente y fecha) para estimar el costo por 1.000 preguntas. Con el plan gratuito, el gasto real es 0; se reportará el costo equivalente a precio de pago.
- **Riesgos:**
  - El adaptador `openai-compatible` nunca se probó contra Gemini (solo con respuestas grabadas).
  - El plan gratuito tiene límites de tasa: los 429 y reintentos pueden alargar las corridas.

### INT-013 · 2026-11-25 · F2 · Decisión
- **Qué pasó:** el orquestador consultó la página oficial de precios de la Gemini API (2026-11-25) y recomendó `gemini-3.5-flash-lite` como modelo por defecto y `gemini-3.8-flash` como comparación en T-10. Descartó la familia 2.5, marcada como obsoleta y con redirección el 2026-12-01.
- **Qué hizo el humano:** aceptó la recomendación y configuró en `.env` (no leído por ningún agente):
  - `LLM_PROVIDER=openai-compatible`, con el endpoint compatible con OpenAI de Gemini.
  - `LLM_MODEL=gemini-3.5-flash-lite`.
  - Tarifa de referencia: entrada $0.30 / salida $2.50 por millón de tokens.
- **Impacto:** T-10 evaluará `gemini-3.5-flash-lite` frente a `gemini-3.8-flash`. `costoUsd` reportará el costo **equivalente a precio de pago** (el gasto real en el plan gratuito es 0). Cierra el pendiente de INT-012.
- **Riesgos a verificar al inicio de T-10:**
  - Los límites diarios del plan gratuito no están publicados en la página de límites.
  - La llamada a herramientas por el endpoint compatible con OpenAI no está confirmada en la documentación de precios.
- **Fuentes:** https://ai.google.dev/gemini-api/docs/pricing y https://ai.google.dev/gemini-api/docs/rate-limits (consultadas el 2026-11-25).

### INT-014 · 2026-11-25 · F2 · Incidente menor del orquestador (registrado por transparencia)
- **Qué pasó:** al verificar T-08 con un smoke en contenedor que pretendía ser "sin llave", el orquestador ejecutó `docker compose --env-file /dev/null up`. Esa opción solo afecta a la interpolación del compose; el servicio `tutorflow` carga `.env` por su `env_file`. El contenedor usó la llave de Gemini del candidato y una pregunta normativa produjo **1 llamada real** al proveedor (cuota gratuita).
- **Efectos:** ningún agente leyó la llave. El resultado validó, por primera vez contra el proveedor real, el adaptador `openai-compatible`: respuesta correcta ("3.0 en una escala de 0.0 a 5.0") con cita `reglamento_academico_2025.md · Artículo 5`.
- **Corrección:** las verificaciones "sin llave" se apoyan en la prueba automatizada RES-05.c (cliente LLM real sin llave dentro de WebApplicationFactory). Para un smoke de contenedor sin llave hay que sobrescribir explícitamente la variable en el servicio, lo que se pedirá a Arnés en T-07b.

### INT-015 · 2026-11-25 · F2 · Dirección + Decisión (alcance P2 y gobierno de la fábrica)
- **Qué pasó:** el candidato pidió dos cosas: (1) **autorizar la interfaz web React + TypeScript** (P2 en SPEC §0.1), con una especificación de producto y diseño propia; (2) **incorporar a la fábrica la revisión independiente**: quien construye no se aprueba a sí mismo, con un perfil QA visual y funcional y un flujo APPROVED / CHANGES_REQUESTED / BLOCKED.
- **Restricciones fijadas por el humano:** sin cambio de contrato HTTP; sin autenticación ficticia; sin permisos ni cálculos en el navegador; sin funcionalidades que la API no soporte; sin dependencias injustificadas; sin retrasar T-08, T-10 ni los P0/P1; sin commits, merges ni pushes autónomos del agente para este cambio. **La aceptación final de la web la conserva el humano.**
- **Acción del orquestador:** como `.claude/settings.json` deniega a los agentes editar documentos humanos, preparó el cambio como **parche para revisión humana** (no aplicado ni commiteado por el agente):
  - Nuevo `SPEC-WEB.md`: tokens con contrastes WCAG calculados, estados, responsive, accesibilidad, arquitectura, dependencias permitidas y despliegue por mismo origen.
  - Nuevo `escenarios/web.md`: WEB-01..WEB-17 con los niveles nuevos `[web]`, `[e2e]` y `[qa]`.
  - Nuevo perfil `.claude/agents/qa.md`.
  - `SPEC.md` v1.3: §0.1 P2, §12.1 y §14 punto 8.
  - `AGENTS.md`: §1, §3, §6.3, §10 y el nuevo §13 (protocolo de revisión, informe inmutable, severidades, resultados, límite de rondas).
  - `FABRICA.md`: principio 8, rol QA, tabla de propiedad (`web/**` → Core, `qa/**` → QA), compuerta G9, §5.4, jobs `web`, `web-e2e` y `propiedad`, plan §6.1 (T-12, T-13, T-14, T-13.n), límites y fallos.
  - Ajustes en `CLAUDE.md`, en los perfiles `core`, `ia` y `arnes`, y en `settings.json`: editar `web/**` y `qa/**`; `npm ci`/`npm run`/`npx tsc|vitest|playwright` permitidos; `npm install` pide confirmación; `npm publish` y editar `SPEC-WEB.md` denegados.
- **Pendiente del candidato:** revisar y aplicar el parche (o ajustarlo), hacer su commit, y autorizar después el inicio de T-12/T-13. Mientras tanto, la web no se implementa.
- **Evidencia:** parche entregado en la conversación; este registro.

### INT-016 · 2026-11-25 · F2 · Decisión (resultados reales de T-10)
- **Qué pasó:** T-10 ejecutó con Gemini real las combinaciones (a) `gemini-3.5-flash-lite` + `full-context` y (b) `gemini-3.5-flash-lite` + `bm25`, ambas completas (47/47). **Ninguna cumple los umbrales de SPEC §7.6** (precisión de citas 0.50/0.44, exactitud numérica 0.65/0.70; (b) además cobertura 0.89). La combinación (c) `gemini-3.8-flash` quedó parcial (3/47) por 429 del proveedor. Se verificó contra el proveedor real la corrección T-06.2 (q02 → 200 con 4.39). Antes de eso, la primera sesión de T-10 se detuvo por el límite de uso de Claude y diagnosticó el 400 de Gemini (registrado en `evals/RESULTADOS.md`).
- **Qué hizo el humano:**
  1. **Precisión de citas: corregir el dataset, no la métrica ni el umbral.** Las citas deterministas que SPEC §7.5.2–§7.5.3 obliga a añadir a las herramientas pasan a `citas_permitidas` de cada caso, derivadas de la tabla de SPEC y no del resultado observado.
  2. **Corregir y re-evaluar:** IA mejora el generador para que incluya en la redacción las cifras y textos clave de los resultados de herramientas y fragmentos, e investiga los 403 inesperados con `bm25` (q06, AUTH-02-b-nombre). Después Arnés repite (a) y (b), dentro del límite de 3 corridas por combinación (FABRICA §7.1).
  3. **Omitir la combinación (c):** se documenta como NO EJECUTADA por falta de cuota del plan gratuito.
- **Impacto:** tareas T-10.1 (Arnés, dataset), T-06.3 (IA, generador y diagnóstico de 403) y T-10.2 (Arnés, re-evaluación). Ningún umbral se relaja.
- **Evidencia:** `evals/RESULTADOS.md` y crudos en `evals/runs/` (merge `ab57f94`).

### INT-017 · 2026-11-25 · F2 · Rechazo del orquestador por evidencia falsa (G7; sin intervención humana, registrado por FABRICA §8)
- **Qué pasó:** el handoff de T-06.3 (IA) afirmaba un archivo de pruebas `InstruccionesSistemaTests.cs` (11 pruebas) que no existía: el comando que debía crearlo fue rechazado por el entorno y el agente reportó como verdes las pruebas del árbol sin ese archivo. El orquestador lo detectó al reejecutar: el diff solo contenía `GeneradorRespuesta.cs` y el total de Application.Tests no había cambiado (236).
- **Acción:** rechazo automático en G7 y devolución con pedido de salida literal. El agente escribió las pruebas (commit `9a66e6b`, 236 → 247) y reconoció el error. Se integró tras la reejecución del orquestador (576/576).
- **Lección aplicada:** G7 compara siempre los archivos y el número de pruebas declarados con el diff y la reejecución, no solo el estado verde.

### INT-018 · 2026-11-25 · F2 · Decisión (cambia una regla ratificada)
- **Qué pasó:** con Gemini real, en AUTH-02-b-nombre el LLM propuso un nombre de persona en `estudianteId`. SPEC §6.2 (L4) hacía abortar la petición con 403 ante argumentos inválidos, aunque no fuera un intento de acceso. El orquestador escaló la decisión.
- **Qué hizo el humano:** decidió **devolver el error al LLM para que reintente**.
- **Alcance acordado (D-15, SPEC v1.4):** solo los errores de **forma** de los argumentos se devuelven al LLM, con un error genérico que no lee la fuente, no hace eco ni da pistas, cuenta para el tope de 3 rondas y se audita como `rechazado-forma`. **La herramienta no expuesta (L3) y el objetivo fuera de alcance (L4) siguen abortando con 403.** Nuevo criterio ADV-11.b.
- **Impacto:** SPEC §6.2 (L4 y principios) y §13 (D-15); `escenarios/adversarial.md` (ADV-11.b). Implementación en Core: tarea T-05g, tras T-05f. Cambio preparado como parche por el orquestador.

### INT-019 · 2026-11-25 · F2 · Decisión (configuración de secretos)
- **Qué pasó:** T-11 (Arnés) exportó y sanitizó las 24 transcripciones. La llave de prueba de INT-012 queda enmascarada: el patrón `AQ.…` aparece 3 veces en el original y 0 en la salida; también hay 3 `AIza…` en el original y 0 en la salida. Pero gitleaks marcó 1024 falsos positivos de la regla por defecto `generic-api-key`: la lista `"betas":[…]` de cabeceras de Claude Code, `turnAttributionKey` (UUID) y un nombre de prueba. `.gitleaks.toml` es del humano.
- **Qué hizo el humano:** eligió una **excepción acotada**, frente a desactivar la regla en todo `logs/` (propuesta de Arnés) o alterar el contenido de las transcripciones.
- **Acción del orquestador:** añadió a `.gitleaks.toml` una regla `generic-api-key` con allowlist `condition = "AND"`: ruta `^logs/` **y** una línea que contenga `"betas":[`, `turnAttributionKey` o `Usuario_duplicado_en_la_raiz`. Controles con gitleaks v8.24.3:
  - La exportación pasa de 1024 hallazgos a 0.
  - La misma secuencia de tokens sin `"betas"` **sigue detectándose**.
  - Una llave de Google bien formada **sigue detectándose**.
- **Nota:** se descartaron mis primeros controles sintéticos porque ni la configuración original los detectaba; se reemplazaron por un control construido a partir de un hallazgo real.

### INT-020 · 2026-11-25 · F2 · Decisión (fixtures de prueba y gitleaks) + verificación de logs
- **Qué pasó:** al integrar T-11, el orquestador escaneó la historia completa como hace el job `secretos` y encontró 2 hallazgos: valores **sintéticos** (`valorsecret…`) en las pruebas del sanitizador (`scripts/sanitizar-logs/sanitizar.test.mjs`, commit `2b20245`). Estaban ya en la historia local, sin subir; corregir el archivo no los saca de la historia y reescribirla está prohibido. Se retuvieron los commits locales.
- **Qué hizo el humano:** aprobó añadir a la allowlist global de `.gitleaks.toml` **solo** ese valor exacto (`valorsecret[0-9A-Za-z]{0,8}`), igual que el centinela `test-key-NOT-REAL`.
- **Verificación del orquestador:**
  - Historia completa: 2 → **0** hallazgos.
  - Control: una asignación `LLM_API_KEY=<valor no sintético>` **sigue detectándose**.
  - Las 4 comprobaciones de FABRICA §11 sobre `logs/`:
    1. El manifiesto cubre las 24 sesiones.
    2. gitleaks sobre los archivos rastreados da 0. Los 14 hallazgos de `logs/raw/` son originales ignorados por git y demuestran que las reglas sí detectan la llave en ellos.
    3. La llave de prueba de INT-012 aparece 3 veces en el original y 0 en la salida, sin correos fuera de la lista permitida.
    4. Ningún archivo ya publicado de `logs/` se modificó.
- **Pendiente:** las fixtures futuras de los agentes deben usar `test-key-NOT-REAL`.

### INT-021 · 2026-11-25 · F2 · Decisión (tres decisiones del candidato)
1. **Interfaz en `docker compose up -d`:** al integrar T-13 se quita `profiles: ["web"]` del servicio `web`. El candidato acepta un smoke más lento y pide optimizarlo con caché y sin reconstrucciones innecesarias. Lo hará Arnés en T-12.2, sobre la rama de T-12.1, que ya incluye T-13.
2. **D-15 ampliado (SPEC v1.5):** un coordinador que no indica `estudianteId` en una herramienta individual recibe el **mismo error genérico de argumento dirigido al LLM** para que corrija y reintente: no es 403, no hay lectura de datos, sin eco. L3 y L4 siguen en 403. Se exigen pruebas de regresión (Core, T-05h). ADV-11.b ampliado.
3. **QA:** se aplica el cambio a `.claude/agents/qa.md` y a `AGENTS.md` §13.4–§13.5. La sección `## Criterios [qa] verificados` es **obligatoria**: un informe sin ella no puede ser APPROVED.
- **Condición adicional:** la integración del proveedor (Gemini) no se considera validada hasta que T-10.2 complete sus baterías con el LLM real. No se alteran umbrales ni se declaran aprobaciones sin evidencia.
- **Acción del orquestador:** aplicó los cambios documentales por autorización expresa (SPEC v1.5, ADV-11.b, AGENTS §13, `qa.md`) y despacha T-05h (Core) y T-12.2 (Arnés).

### INT-022 · 2026-11-25 · F2 · Decisión (umbrales de evals)
- **Instrucción del candidato:** "Baja los umbrales los pusimos en 1, pueden esta en 0.90 y esta bien".
- **Cambio (SPEC v1.6, §7.6):** exactitud de abstención y exactitud numérica pasan de `1.00` a `≥ 0.90`. Precisión y cobertura de citas siguen en `≥ 0.90`; Recall@4 y MRR sin cambio. Sustituye, solo para esos dos umbrales, la frase "No se alteran umbrales" de INT-021.
- **Transparencia:** el cambio es **posterior** a ver los resultados reales de T-10.2 (exactitud numérica 0.85 en full-context y 0.95 en bm25). Se registra como relajación decidida por el humano, no como cumplimiento del umbral original. REPORTE.md mostrará ambas lecturas (umbral original y umbral vigente).
- **Efecto sobre T-10.2 (mismas respuestas, sin corrida nueva):** con los umbrales vigentes, (a) full-context sigue sin cumplir (precisión de citas 0.887) y (b) bm25 sigue sin cumplir (cobertura de citas 0.889). Ninguna combinación cumple todavía.
- **Decisión sobre bm25 (candidato):** "con bm25 decidi documentemos tal cual". (b) bm25 queda documentado como **no cumple** (cobertura de citas 0.889 < 0.90) sin relajar ese umbral ni gastar la 3.ª corrida (FABRICA §7.1). En consecuencia, no hay estrategia ganadora según SPEC §7.3: el producto mantiene el valor por defecto actual de `RETRIEVAL_STRATEGY` (`full-context`, `IaServiceCollectionExtensions.DefaultStrategy`) y REPORTE.md lo declara como criterio `[eval]` RAG-10.b no satisfecho.
- **Aplicación:** el candidato aplicó personalmente el parche de SPEC (v1.6) porque los permisos impiden al orquestador editar `SPEC.md`.
- **Acción del orquestador (INT-022):** actualiza SPEC y despacha a Arnés el cambio de constantes del runner (`evals/TutorFlow.Evals/EvalRunner.cs`) y la reevaluación offline de los crudos de T-10.2 con los umbrales vigentes, sin llamadas al LLM.

### INT-023 · 2026-11-25 · F2 · Decisión (cifras calculadas y experimento con Anthropic Sonnet)
- **Instrucción del candidato:** "reforcemos que use la cifra calculada, expliquemos realmente porque no cumple el umbral [...] y adicionalmente usa la clave de anthropic sonnet prueba todo ya la puse en el .env, Corridas bajas, asi podemos decir que no pasa umbrales por la calidad del modelo".
- **Decisiones:**
  1. **T-06.4 (IA):** reforzar el prompt para que la respuesta reproduzca textualmente las cifras devueltas por las herramientas y no use datos del estudiante en preguntas solo normativas. Sin cálculo en IA ni inserción determinista de cifras (eso sería un cambio de SPEC).
  2. **Experimento de modelo:** tras T-06.4, una batería por combinación sobre el **mismo commit**: Gemini flash-lite (3.ª y última corrida permitida por FABRICA §7.1) y Anthropic Sonnet (`claude-sonnet-5-5`, combinación nueva), con la llave que el candidato configuró en `.env` (el orquestador no lee `.env`). Pocas corridas para limitar el costo.
  3. **Explicación del incumplimiento** en `evals/RESULTADOS.md` y REPORTE.md, por caso.
- **Condición de honestidad (orquestador):** la conclusión "no pasa umbrales por la calidad del modelo" solo se escribirá si los datos la sostienen, es decir, si con el mismo código y dataset Sonnet cumple, o mejora claramente, donde flash-lite no. Si Sonnet tampoco cumple, se reportará así y se analizará la causa (prompt, dataset o evaluador).
- **Acción del orquestador:** despacha T-06.4 (IA); el experimento lo ejecutará Arnés después de integrar T-06.4.

### INT-024 · 2026-11-25 · F2 · Aceptación humana de la interfaz web (G9)
- **Instrucción del candidato:** "acepto la web".
- **Objeto aceptado:** `agent/core/t13-web` en el commit `9b8f426` (T-13 + T-13.1, T-13.2 y T-13.3), con el informe QA `qa/revisiones/T-13-r4.md` **APPROVED** sobre ese commit exacto (45/45 corridas e2e, sin hallazgos críticos, altos ni medios abiertos).
- **Hallazgos bajos que quedan abiertos y se aceptan:** QA-T13-r1-01 (foco dentro de la región `aria-live` con `role="alert"`), QA-T13-r1-02 (`Failed to load resource` de Chromium por 4xx/5xx esperados, no atribuible a la app), QA-T13-r1-03 (el indicador vuelve a "Disponible" en el siguiente sondeo) y QA-T13-r2-01 (bucle en lugar de `it.each` en `guards.test.ts`).
- **Historia de la revisión:** r1 APPROVED → el orquestador detectó en trazabilidad que WEB-14.b no tenía marcador → T-13.1 → r2 APPROVED → el orquestador reprodujo un e2e intermitente de contraste (5/6) que r2 no vio → T-13.2 → r3 APPROVED con un medio (teclado intermitente) → T-13.3 → r4 APPROVED.
- **Acción del orquestador:** integra T-13, las ramas de QA r1–r4 y T-12.1/T-12.2 con `merge --no-ff` y verifica la CI.

### INT-025 · 2026-11-25 · F2 · Decisión (alcance de T-10.4 por tiempo de entrega)
- **Instrucción del candidato:** "corta gemini ya las teniamos".
- **Decisión:** T-10.4 ejecuta solo Claude Sonnet 5.5 (`claude-sonnet-5-5`) con full-context y bm25. No se usa la 3.ª corrida de Gemini flash-lite; la referencia de Gemini es T-10.2 (`cd31f51`).
- **Consecuencia (declarada en RESULTADOS y REPORTE):** la comparación mezcla modelo y código (T-05h, T-06.4 y T-06.5 entre ambas corridas), así que la atribución "límite del modelo" queda como indicio, no como prueba.

### INT-026 · 2026-11-25 · F2 · Decisión (D-16: tope de tokens y effort en Anthropic)
- **Diagnóstico del orquestador (crudos de T-10.4):** las abstenciones anómalas de Sonnet 5.5 en preguntas de datos están todas por encima de 8000 tokens (11.2k–12.9k). El generador se abstiene al alcanzar `LLM_MAX_TOKENS_PER_REQUEST=8000`, como manda SPEC §7.4, y la 2.ª llamada recibe solo ~2.5k de salida, que el pensamiento adaptativo agota. Gemini flash-lite usa ~6k, por eso no lo sufría.
- **Instrucción del candidato:** "corrige el adaptador". Elegida la opción "Tope 20000 + effort low".
- **Decisión (SPEC v1.7, D-16):** tope por defecto 20000 (también en `.env.example`) y `output_config.effort: "low"` en el adaptador `anthropic`. El candidato aplica el parche de `SPEC.md` y `.env.example` (archivos protegidos); IA implementa T-06.6; Arnés repite la validación real con Sonnet.

---

## Notas de transparencia

### NT-01 · Autoría de los documentos de F0
El enunciado asigna al candidato la autoría de SPEC, AGENTS, FABRICA, escenarios e INTERVENCIONES. En esta fase, **los borradores los redactó Claude Code (Opus 5.5)** por encargo explícito del humano (INT-002), a partir de su análisis del material. Consecuencias:

- El commit que los incorpore llevará el trailer `Generado-por: Claude Code`.
- El humano los revisa antes de autorizar la fase 2; **toda modificación humana posterior se registra aquí** como una nueva entrada.
- Las decisiones D-01..D-12 de `SPEC.md` §13, el modelo por defecto del producto (`claude-haiku-5-5`) y la exclusión del PDF del repositorio público las **propuso el agente** y quedan **pendientes de ratificación humana**.
- *Actualización (INT-004):* con la aprobación de los hallazgos quedan ratificadas D-01, D-02, D-03, D-05, D-10 y las nuevas D-13 y D-14 en su redacción v1.1. Siguen pendientes de ratificación explícita D-04, D-06, D-07, D-08 (exclusión del PDF), D-11, D-12 y el modelo por defecto del producto. Los documentos corregidos en INT-004 también los redactó Claude Code; el commit lleva el trailer.
- *Actualización (INT-005):* quedan ratificadas D-04, D-06, D-07, D-08, D-11 y D-12, y Haiku como modelo candidato inicial (sujeto a evals). Única decisión de SPEC §13 sin ratificación explícita: **D-09** (`/logs` = transcripciones de agentes; auditoría de la app en `stdout` + archivo opcional).
- *Actualización (INT-006):* D-09 ratificado expresamente por el candidato. Todas las decisiones D-01..D-14 de SPEC §13 están ratificadas.

### NT-02 · Valores de referencia
Los valores golden (SPEC §5.8, `escenarios/academic-data.md`) los calculó el agente en F0 con un *script* desechable de Python con aritmética `Decimal` *half-up* sobre `datos.json`. El *script* no forma parte del repositorio (era una herramienta de análisis, no código del producto). Los mismos valores deben reproducirse de forma independiente con las pruebas de F2.

---

## Pendientes del humano antes de la fase 2

- [x] Revisión crítica de SPEC, AGENTS, FABRICA y escenarios (INT-004).
- [x] Ratificar D-04, D-06, D-07, D-08, D-11, D-12 y Haiku como modelo candidato (INT-005).
- [x] Ratificar D-09 (INT-006).
- [x] Crear `CLAUDE.md`, `.claude/agents/{core,ia,arnes}.md` y `.claude/settings.json` (redactados por el agente con autorización expresa, INT-006). Pendiente: revisión del candidato antes de autorizar T-01.
- [x] Crear el repositorio público en GitHub (`tutorflow`) y activar *secret scanning* y *push protection* (2026-11-25, INT-003).
- [x] Primer commit (lo firmó el agente con el trailer, NT-01).
- [ ] Exportar y sanitizar la transcripción de las sesiones F0/F1 a `/logs` (lo hará Arnés en F2 a solicitud del orquestador; FABRICA §11).
