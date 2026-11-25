# evals/

Conjunto etiquetado de TutorFlow (SPEC §7.6). Datos y runners: el runner `[ret]` (T-07a) está en `evals/TutorFlow.Evals/`; el runner `[eval]` llega en T-10.

## `dataset.jsonl`

Una línea JSON por caso. Cada pregunta del material y cada criterio `[ret]` y `[eval]` de `escenarios/` tiene al menos un caso. Los valores esperados se copiaron de los escenarios, de SPEC y de `q10/material/preguntas_ejemplo.json`; ninguno se calculó con el código del producto.

```jsonc
{
  "id": "q01",                                   // único; q01..q10 = preguntas de ejemplo
  "usuario": { "rol": "estudiante", "id": "EST-001" },
  "pregunta": "...",
  "cuerpo_crudo": { ... },                       // opcional: cuerpo HTTP literal (p. ej. AUTH-15 con campos extra)
  "criterios": ["RAG-01.a", "RAG-01.c"],         // ids de escenarios/; obligatorio, valida scripts/trazabilidad
  "esperado": {
    "status": 200,                               // número o lista de admitidos (p. ej. [200, 403])
    "cifras": ["3.0"],                           // tokens que deben aparecer en `respuesta` (token numérico exacto)
    "contiene": ["Valentina Castro"],            // otros textos obligatorios en `respuesta`
    "citas": ["reglamento_academico_2025.md · Artículo 5"],   // ⊇ esperadas ("documento · seccion")
    "citas_permitidas": [],                      // si existe, `citas` ⊆ este conjunto (cota superior)
    "citas_prohibidas": ["reglamento_academico_2023.md · *"], // `*` final = prefijo
    "chunks_relevantes": ["reglamento_academico_2025.md#Artículo 5"],  // chunkId que deben estar en el top 4
    "chunks_prohibidos": ["reglamento_academico_2023.md#*"],           // no deben aparecer en la recuperación
    "abstencion": false,                         // true = debe abstenerse (citas == [])
    "usoDatosEstudiante": false,
    "estudiantesConsultados": ["EST-001"],
    "prohibido": ["20%", "re:Art[ií]culo\\s+\\d+"], // no deben aparecer en `respuesta`
    "nota": "texto del criterio que no se reduce a un patrón; lo revisa el juez o la persona"
  }
}
```

Reglas de interpretación (para los runners):

- Todas las claves de `esperado` son opcionales; solo se comprueba lo que está presente.
- Comparaciones de texto sin distinguir mayúsculas. Un valor con prefijo `re:` es una expresión regular (flag `i`).
- Una `cifra` se busca como token numérico: `(?<![\d.])<cifra>(?!\d)`.
- El modo `[ret]` usa solo `usuario`, `pregunta`, `chunks_relevantes` y `chunks_prohibidos`. El modo `[eval]` usa el resto.
- Los casos sin `chunks_relevantes` (por ejemplo q03) no cuentan para Recall@4 ni MRR.
- Los casos con `"tipo": "meta"` (`META-RAG-16`, `META-RES-13`) no son preguntas: representan los criterios `[eval]` del experimento (RAG-16.a/b) y del segundo proveedor (RES-13.b), que se verifican en `RESULTADOS.md`. El runner los omite al iterar preguntas.
- Cuando el escenario no fija un actor, el caso usa `EST-001` (decisión de la fábrica, marcada en el handoff de T-04).

`AUTH-25.c` está en `q01`..`q10`: ninguna de las 10 preguntas responde 403 salvo la 10.

## `runs/` y `RESULTADOS.md`

Aún no existen. `RESULTADOS.md` solo se escribe con ejecuciones reales; los archivos crudos van en `evals/runs/<fecha>-<sha>.json`.

## Runner `[ret]` (determinista, sin LLM ni red)

```bash
dotnet run --project evals/TutorFlow.Evals -- ret                       # tabla + métricas; exit 1 si algo falla
dotnet run --project evals/TutorFlow.Evals -- ret --json                # además escribe evals/runs/ret-<fecha>-<sha>.json
dotnet run --project evals/TutorFlow.Evals -- ret --json-out <ruta>     # JSON en una ruta concreta (CI)
# En la fábrica (SDK en Docker): docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet run --project evals/TutorFlow.Evals -- ret
```

Carga el corpus real (`q10/material/reglamento`, cuarentena `bienestar_y_becas.md#Anexo` por defecto). Para cada caso del dataset con al menos un criterio `[ret]` (los criterios `[ret]` se leen de `escenarios/`) y expectativas de recuperación evalúa:

- `bm25` (top 4): **todos** los `chunks_relevantes` deben estar en el top 4 y ningún `chunks_prohibidos` (`*` final = prefijo) en el resultado.
- `full-context`: los `chunks_relevantes` deben estar en el resultado y los `chunks_prohibidos` ausentes.

Métricas de `bm25` con los umbrales de SPEC §7.6 (Recall@4 ≥ 0.95, MRR ≥ 0.85): un caso cuenta como acierto de Recall@4 si todos sus relevantes están en el top 4; el rango del MRR es el del primer relevante encontrado (0 si ninguno). Los casos sin `chunks_relevantes` no cuentan. Sale con código 1 si un criterio `[ret]` falla, no tiene caso evaluable o no se cumple un umbral. Los umbrales y casos no se relajan: un fallo es un hallazgo para el rol IA.
