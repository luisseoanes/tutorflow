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
  6. Encargó al agente redactar SPEC.md, AGENTS.md, FABRICA.md, INTERVENCIONES.md y `/escenarios/{authorization,rag-quality,academic-data,adversarial,resilience}.md`, más `.gitignore`, `.env.example` y la configuración de escaneo de secretos, **sin** código de aplicación, pruebas, Docker ni *workflows*, y pidió detenerse al terminar.
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
