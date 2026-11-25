# CLAUDE.md — Punto de entrada

TutorFlow se construye con una **fábrica de agentes** (orquestador + Core, IA y Arnés) bajo reglas *lights-out*: el humano escribe especificación y configuración; los agentes escriben código, pruebas, evals, Docker y CI.

## Lee antes de actuar

| Documento | Para qué |
|---|---|
| `SPEC.md` | **Fuente de verdad** funcional y técnica (contrato HTTP, reglas académicas, seguridad, decisiones D-01..D-14). |
| `SPEC-WEB.md` | Interfaz web P2: producto, sistema de diseño y aceptación (subordinada a `SPEC.md`). |
| `AGENTS.md` | Reglas comunes de ingeniería, niveles de verificación, trazabilidad, git y handoff. |
| `FABRICA.md` | Roles, propiedad de rutas (§4.1), tareas y dependencias (§6), compuertas G1–G8, mutantes, costos y parada. |
| `INTERVENCIONES.md` | Decisiones e intervenciones humanas (qué está ratificado). |
| `escenarios/` | Criterios de aceptación, con id y nivel de verificación. |

Perfiles de los agentes: constructores `.claude/agents/{core,ia,arnes}.md` y revisor independiente `.claude/agents/qa.md`. Permisos: `.claude/settings.json`.

## Reglas que no se negocian

1. **No cambies decisiones ratificadas** (SPEC, escenarios, INTERVENCIONES) sin autorización humana expresa. Ante una contradicción: detente y escala (`AGENTS.md` §12).
2. **Escribe solo en tus rutas** (`FABRICA.md` §4.1). Si necesitas otra, pídela en el handoff.
3. **Trazabilidad:** cada prueba lleva `[Trait("Criterio","<ID>.<letra>")]`; cada caso de eval declara sus `criterios`; ninguna afirmación sin evidencia ejecutada (`AGENTS.md` §6, §11).
4. **Git:** nunca reescribas la historia: nada de `rebase`, `commit --amend`, `push --force`, `reset --hard` sobre ramas publicadas ni *squash*. Integración solo con `git merge --no-ff` (orquestador).
5. **Commits de agente:** el trailer `Generado-por: Claude Code` va en el bloque de trailers (`AGENTS.md` §9).
6. **Secretos:** nunca leas `.env`, ni imprimas, registres o commitees llaves.
7. **No leas `q10/enunciado/`** (documento confidencial; las transcripciones se publican).
8. **Quien construye no se aprueba a sí mismo:** las entregas sujetas a revisión independiente (interfaz web, y otras a criterio del orquestador) necesitan un informe **APPROVED** del revisor QA sobre el commit exacto y, para la web, la aceptación humana (`AGENTS.md` §13, `FABRICA.md` §5.4).
