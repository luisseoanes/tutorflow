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
