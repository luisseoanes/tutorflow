# evals/

Conjunto etiquetado de TutorFlow (SPEC §7.6). Datos y runners: el runner `[ret]` (T-07a) está en `evals/TutorFlow.Evals/`; el runner `[eval]` (T-10) está en el mismo proyecto.

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

`RESULTADOS.md` solo se escribe con ejecuciones reales; los archivos crudos van en `evals/runs/` (`ret-<fecha>-<sha>.json` para `[ret]`; `eval-<fecha>-<sha>-<modelo>-<estrategia>.json` para `[eval]`).

## Runner `[ret]` (determinista, sin LLM ni red)

```bash
dotnet run --project evals/TutorFlow.Evals -- ret                       # tabla + métricas; exit 1 si algo falla
dotnet run --project evals/TutorFlow.Evals -- ret --json                # además escribe evals/runs/ret-<fecha>-<sha>.json
dotnet run --project evals/TutorFlow.Evals -- ret --json-out <ruta>     # JSON en una ruta concreta (CI)
# En la fábrica (SDK en Docker): docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet run --project evals/TutorFlow.Evals -- ret
```

Carga el corpus real (`q10/material/reglamento`, cuarentena `bienestar_y_becas.md#Anexo` por defecto). Los criterios `[ret]` se leen de `escenarios/`; se evalúan los casos del dataset con algún criterio `[ret]` y expectativas de recuperación.

**Regla (SPEC §7.6 y criterios `[ret]`: "en el top 4 de la estrategia por defecto").** Los criterios y los umbrales se evalúan sobre la **estrategia por defecto**, que es la que resuelve `RETRIEVAL_STRATEGY` (variable de entorno; si está vacía, `IaServiceCollectionExtensions.DefaultStrategy` de IA, hoy `full-context`, provisional hasta T-10; el runner referencia esa constante, no la duplica).

- `full-context` como defecto: exige presencia de `chunks_relevantes` y ausencia de `chunks_prohibidos` (`*` final = prefijo). Recall@4 y MRR **no aplican** (SPEC §7.6).
- `bm25` como defecto: todos los `chunks_relevantes` en el top 4 y ningún prohibido, más los umbrales Recall@4 ≥ 0.95 y MRR ≥ 0.85. Un caso es acierto de Recall@4 si **todos** sus relevantes están en el top 4; el rango del MRR es el del primer relevante (0 si ninguno).
- Las demás estrategias disponibles se evalúan y se **reportan siempre** (tabla, métricas y top-4 real de los casos que no cumplen; también en el JSON) como "experimento, no bloqueante": insumo de T-09/T-10.
- Código de salida 1 solo si falla un criterio `[ret]` o un umbral aplicable sobre la estrategia por defecto, o falta un caso. Los umbrales y casos no se relajan.

**Advertencia:** si T-10 elige `bm25` como estrategia por defecto, hoy no cumple (Recall@4 = 0.8333, MRR = 0.7500; fallan `q04`/ACAD-07.a y `RAG-12-a`/RAG-12.a). Se puede comprobar con `RETRIEVAL_STRATEGY=bm25`.

## Runner `[eval]` (LLM real; gasta cuota del proveedor)

```bash
evals/correr-eval.sh a                  # a = gemini-3.5-flash-lite + full-context (defecto)
evals/correr-eval.sh b                  # b = gemini-3.5-flash-lite + bm25
evals/correr-eval.sh c                  # c = gemini-3.8-flash + full-context
evals/correr-eval.sh a --solo q01       # un solo caso (peticion de validacion)
# Directo contra una API ya levantada:
dotnet run --project evals/TutorFlow.Evals -- eval --modelo <id> --estrategia <e> [--base-url http://localhost:8080] [--pausa-ms 5000] [--reintentos 2] [--max-fallos-seguidos 3] [--solo q01,q02] [--out <ruta>]
```

- El script levanta compose con el override de la combinación (`evals/compose.<x>.yml`, que sobrescribe **solo** modelo, estrategia y precios, nunca secretos), espera `healthy`, corre el runner y apaga el compose. La llave llega al contenedor por el `env_file` del compose base (`TUTORFLOW_ENV_FILE` puede apuntar a la **ruta** de un `.env` que no esté junto al compose). En CI (`EVAL_CI=1`) llega del entorno del proceso (secreto de Actions).
- El runner solo habla HTTP (`POST /api/asistente/preguntar`) con la API publicada. No lee `.env`, no recibe llaves y el crudo no guarda cabeceras; además enmascara en `respuesta` cualquier cadena con forma de credencial.
- Casos: los del dataset (sin `meta`) con al menos un criterio `[eval]` en `escenarios/` (los `RAG-12-*` son solo `[ret]` y se omiten). Cada caso se evalúa de forma **determinista** contra `esperado`: `status`, `cifras` (token numérico), `contiene`, `prohibido`, `citas` (⊇), `citas_permitidas` (⊆), `citas_prohibidas`, `abstencion` (texto exacto de SPEC 7.5.5 y `citas == []`) y `usoDatosEstudiante`.
- Lo que no se reduce a un patrón (`nota`) se marca `revisionManual`; `estudiantesConsultados` solo existe en la auditoría (no en la respuesta HTTP) y queda como `noVerificablesPorHttp`. Ninguno de los dos se da por cumplido automáticamente.
- Métricas de SPEC 7.6: precisión y cobertura de citas, exactitud de abstención y exactitud numérica, más tokens, `costoUsd`, latencia p50/p95 y nº de 429/503. Recall@4 y MRR salen del runner `[ret]`.
- Resultado por criterio `[eval]`: `CUMPLE` / `NO CUMPLE` / `NO EJECUTADO`. `AUTH-25.c` se evalúa solo por el código de estado.
- Un 503/429 se reintenta con backoff limitado (20 s, 40 s). Si `--max-fallos-seguidos` casos seguidos no obtienen respuesta útil (cuota agotada o proveedor caído), el runner se **detiene**, guarda lo hecho como corrida `PARCIAL` (exit 3) y lista los casos no ejecutados; nunca inventa resultados.
- `vector` y `hybrid` no tienen combinación: requieren `EMBEDDING_*` configurado y no se intentan sin él.
