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

---

## Notas de transparencia

### NT-01 · Autoría de los documentos de F0
El enunciado asigna al candidato la autoría de SPEC, AGENTS, FABRICA, escenarios e INTERVENCIONES. En esta fase, **los borradores los redactó Claude Code (Opus 5.5)** por encargo explícito del humano (INT-002), a partir de su análisis del material. Consecuencias:

- El commit que los incorpore llevará el trailer `Generado-por: Claude Code`.
- El humano los revisa antes de autorizar la fase 2; **toda modificación humana posterior se registra aquí** como una nueva entrada.
- Las decisiones D-01..D-12 de `SPEC.md` §13, el modelo por defecto del producto (`claude-haiku-5-5`) y la exclusión del PDF del repositorio público las **propuso el agente** y quedan **pendientes de ratificación humana**.
- *Actualización (INT-004):* con la aprobación de los hallazgos quedan ratificadas D-01, D-02, D-03, D-05, D-10 y las nuevas D-13 y D-14 en su redacción v1.1. Siguen pendientes de ratificación explícita D-04, D-06, D-07, D-08 (exclusión del PDF), D-11, D-12 y el modelo por defecto del producto. Los documentos corregidos en INT-004 también los redactó Claude Code; el commit lleva el trailer.

### NT-02 · Valores de referencia
Los valores golden (SPEC §5.8, `escenarios/academic-data.md`) los calculó el agente en F0 con un *script* desechable de Python con aritmética `Decimal` *half-up* sobre `datos.json`. El *script* no forma parte del repositorio (era una herramienta de análisis, no código del producto). Los mismos valores deben reproducirse de forma independiente con las pruebas de F2.

---

## Pendientes del humano antes de la fase 2

- [x] Revisión crítica de SPEC, AGENTS, FABRICA y escenarios (INT-004).
- [ ] Ratificar D-04, D-06, D-07, D-11, D-12 y el modelo por defecto del producto.
- [ ] Ratificar la exclusión del PDF ("Confidencial para el candidato") del repositorio público (D-08).
- [ ] Crear `.claude/agents/{core,ia,arnes}.md` y `.claude/settings.json` (configuración de agentes, de autoría humana; FABRICA §2).
- [x] Crear el repositorio público en GitHub (`tutorflow`) y activar *secret scanning* y *push protection* (2026-11-25, INT-003).
- [x] Primer commit (lo firmó el agente con el trailer, NT-01).
- [ ] Exportar y sanitizar la transcripción de las sesiones F0/F1 a `/logs`.
