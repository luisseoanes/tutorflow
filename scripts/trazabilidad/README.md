# scripts/trazabilidad

Matriz criterio → pruebas/casos (AGENTS.md §6.4). Node >= 18, sin dependencias.

```
node scripts/trazabilidad/trazabilidad.js                       # tareas integradas (CI)
node scripts/trazabilidad/trazabilidad.js --completo            # todos los criterios (cierre de F2)
node scripts/trazabilidad/trazabilidad.js --json out/matriz.json
node scripts/trazabilidad/trazabilidad.js --integradas T-01,T-02   # forzar la lista (pruebas del script)
```

Fuentes: `escenarios/*.md` (criterios, nivel, severidad; ignora lápidas `fusionado`), `[Trait("Criterio","X")]` en `tests/**/*.cs` (los comentarios se ignoran), `criterios` de `evals/dataset.jsonl` y marcadores `# Criterio: ID` en `scripts/smoke/**` (para `[cont]`).

Tareas integradas: ids `T-xx` de los asuntos `merge: T-xx ...` de `git log --merges` en la rama actual. La asignación criterio → tarea está en `asignacion.json` (listas de FABRICA §4.2 y §6 más reglas por nivel; `inferidas` documenta lo interpretado).

Falla (exit 1) si:

- (a) una prueba, caso o marcador declara un id inexistente o mal formado;
- (b) un criterio dom/app/api/ret/cont de un escenario Crítica o Alta de una tarea exigida no tiene prueba o caso;
- (c) un criterio `[eval]` de una tarea exigida no tiene caso en el dataset;
- (d) un criterio sin tarea asignada, o un dataset/asignación inválidos.

Evidencia aceptada por nivel: `dom/app/api` → Trait en `tests/`; `ret` → caso del dataset (o Trait); `cont` → Trait o marcador de smoke; `eval` → caso del dataset. Un aviso (no bloquea) señala una prueba etiquetada en un proyecto que no corresponde a su nivel.

Que exista una prueba etiquetada no demuestra que sus aserciones correspondan al criterio: eso lo revisan G6 y los mutantes.

## Niveles `[web]`, `[e2e]` y `[qa]` (T-12, INT-015)

Los ids `WEB-nn.x` de `escenarios/web.md` se reconocen como los demás. Asignación (`asignacion.json`): `[web]` y `[e2e]` → **T-13** (Core); `[qa]` → **T-14** (QA). WEB-16.a/b (FABRICA §6.1 los menciona en T-12 "con T-13") se asignan a T-13 porque su prueba vive en `web/e2e`. **T-13 y T-14 llevan `solo_si_integrada`: se exigen únicamente cuando su `merge: T-13 …`/`merge: T-14 …` está en la historia, también con `--completo`** (la web es P2 y no bloquea P0/P1). Con T-13 integrada, los `[web]`/`[e2e]` Críticos/Altos sin prueba hacen fallar el job.

**Marcador para pruebas del frontend (formato para Core).** En cualquier `web/**/*.test.ts`, `*.test.tsx`, `*.spec.ts` o `*.spec.tsx`, una línea de comentario inmediatamente antes (hasta 3 líneas) del `it(`/`test(`/`describe(` que prueba el criterio:

```ts
// Criterio: WEB-05.a
it("muestra respuesta, citas, datos del estudiante, tokens y costo exacto", () => { /* … */ });

// Criterio: WEB-05.c, WEB-12.b      (varios ids separados por coma o espacio)
test("foco y aria-live en la nueva respuesta", async ({ page }) => { /* … */ });
```

- `[e2e]` se reconoce solo si el marcador está en `web/e2e/**`; `[web]` fuera de `web/e2e/` (si no, aviso, no falla). Un marcador sin `it/test/describe` cercano da aviso.
- Un id inexistente o mal formado falla (regla (a)). `node_modules`, `dist`, `build`, `coverage`, `playwright-report` y `test-results` se ignoran.

**Criterios `[qa]`.** Se satisfacen con el **informe de la última ronda** `qa/revisiones/T-13-r<n>.md` (AGENTS.md §13.4) si contiene la línea `Resultado: APPROVED` y una sección `## Criterios [qa] verificados` que lista los ids (`WEB-01.c, WEB-03.b, …`). Solo cuentan los ids de esa sección (los de la tabla de hallazgos no). Si la última ronda es CHANGES_REQUESTED o BLOCKED, o falta la sección, hay un aviso y los `[qa]` quedan sin evidencia. **El perfil del revisor (`.claude/agents/qa.md`) debe pedir esa sección** (lo decide el humano/orquestador; Arnés no escribe `.claude/**`).

`node scripts/trazabilidad/trazabilidad.js --integradas T-13,T-14` fuerza la lista para probar el comportamiento antes de integrar.
