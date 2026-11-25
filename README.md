# TutorFlow

Asistente académico para estudiantes y coordinadores (prueba técnica de Q10). Responde preguntas sobre el reglamento y sobre los datos académicos del propio usuario mediante un LLM, con citas verificadas, reglas académicas calculadas de forma determinista y permisos que **no dependen del modelo**.

Se construyó con una fábrica de agentes bajo reglas *lights-out* (el humano escribe especificación y configuración; los agentes escriben código, pruebas, evals, Docker y CI). Documentos de referencia:

| Documento | Contenido |
|---|---|
| [`SPEC.md`](SPEC.md) | Fuente de verdad: contrato HTTP, reglas académicas, seguridad, decisiones D-01..D-14. |
| [`FABRICA.md`](FABRICA.md) | Roles, propiedad de rutas, tareas, compuertas, mutantes y costos de la fábrica. |
| [`AGENTS.md`](AGENTS.md) | Reglas de ingeniería, niveles de verificación y trazabilidad. |
| [`INTERVENCIONES.md`](INTERVENCIONES.md) | Decisiones e intervenciones humanas. |
| [`evals/RESULTADOS.md`](evals/RESULTADOS.md) | Resultados de las evaluaciones, solo con ejecuciones reales. |
| [`escenarios/`](escenarios/) | Criterios de aceptación con id y nivel de verificación. |

## Arranque

Requisitos: Docker con Compose v2.

```bash
cp .env.example .env      # edite .env (ignorado por git; nunca lo suba)
docker compose up -d      # construye la imagen y arranca TutorFlow + la API simulada
curl http://localhost:8080/api/salud     # {"estado":"ok"}
```

- **8080** es el único puerto publicado (TutorFlow). La API simulada (puerto 4000) vive solo en la red interna de compose y el reglamento se monta de solo lectura.
- Sin llave del proveedor la aplicación **arranca**; los 400 y los 403 de identidad/referencia funcionan, y las preguntas que llegan al LLM responden 503.

```bash
curl -s http://localhost:8080/api/asistente/preguntar \
  -H 'Content-Type: application/json' \
  -d '{"pregunta":"¿Cuál es la nota mínima para aprobar una asignatura?","usuario":{"rol":"estudiante","id":"EST-001"}}'
```

### Variables de entorno

Referencia completa: `SPEC.md` §11 y `.env.example`. Las principales:

| Variable | Descripción |
|---|---|
| `LLM_API_KEY` | Llave del proveedor (secreto; solo por entorno). |
| `LLM_PROVIDER` | `anthropic` u `openai-compatible` (no se deduce de la llave). |
| `LLM_MODEL`, `LLM_BASE_URL` | Modelo y, para `openai-compatible`, la URL base. |
| `LLM_PRICE_INPUT_PER_MTOK`, `LLM_PRICE_OUTPUT_PER_MTOK` | Precios USD por millón de tokens, para `costoUsd`. |
| `RETRIEVAL_STRATEGY` | `full-context` o `bm25` (`vector`/`hybrid` solo con embeddings). Vacío = el valor por defecto del código. |
| `EMBEDDING_*` | Opcionales; solo para `vector`/`hybrid`. |

Ejemplo con Gemini por su endpoint compatible con OpenAI (la llave va solo en su `.env`, nunca en el repositorio):

```dotenv
LLM_PROVIDER=openai-compatible
LLM_BASE_URL=https://generativelanguage.googleapis.com/v1beta/openai
LLM_MODEL=gemini-3.5-flash-lite
LLM_PRICE_INPUT_PER_MTOK=0.30
LLM_PRICE_OUTPUT_PER_MTOK=2.50
LLM_API_KEY=<su llave>
```

## Pruebas, evals y verificaciones

El SDK de .NET 8 se puede usar local o en Docker (`docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet <args>`).

```bash
dotnet build TutorFlow.sln -warnaserror && dotnet test TutorFlow.sln    # pruebas [dom] [app] [api] [cont]
dotnet run --project evals/TutorFlow.Evals -- ret                       # evals [ret]: recuperación, sin LLM ni red
bash scripts/smoke/smoke.sh                                             # smoke en contenedor, sin llave del proveedor
node scripts/mutantes/mutantes.js                                       # mutantes del arnés (árbol limpio)
node scripts/trazabilidad/trazabilidad.js --completo                    # matriz criterio -> prueba/caso
evals/correr-eval.sh a                                                  # evals [eval] con el LLM REAL (a|b|c); gasta cuota
```

Las evals `[eval]` hablan solo HTTP con la API en ejecución; la llave llega al contenedor por `env_file` y el runner nunca la recibe. Detalles y combinaciones (modelo x estrategia): [`evals/README.md`](evals/README.md). Los resultados reales y la recomendación de estrategia/modelo están en [`evals/RESULTADOS.md`](evals/RESULTADOS.md).

### Interfaz web (P2, `SPEC-WEB.md`)

El servicio `web` (puerto **3000**, proxy de `/api/*` hacia `tutorflow:8080`) está en `docker-compose.yml` bajo el perfil `web`; su `Dockerfile` vive en `web/` (T-13). Hasta que `web/` exista, `docker compose up` no lo toca:

```bash
docker compose --profile web up -d --build     # TutorFlow en :8080 y la web en :3000
docker compose -f scripts/web/compose.yml run --rm node npm ci          # herramientas web sin instalar nada en el host
docker compose -f scripts/web/compose.yml run --rm playwright npx playwright test
```

Detalles (versiones fijadas, cómo la imagen de Playwright alcanza la app): [`scripts/web/README.md`](scripts/web/README.md). Los marcadores `// Criterio: WEB-nn.x` de las pruebas web y los informes de revisión que cuentan para la trazabilidad: [`scripts/trazabilidad/README.md`](scripts/trazabilidad/README.md).

La CI (`.github/workflows/ci.yml`) tiene jobs bloqueantes (build-test, retrieval, mutantes, trazabilidad, secretos con gitleaks, lights-out, smoke y `propiedad`, que verifica que cada commit con `Agente: X` toca solo las rutas de X según `scripts/propiedad/rutas.json`; `web` y `web-e2e` corren solo si existe `web/` y se omiten diciéndolo) y uno **no bloqueante**, `evals-llm`, que se lanza a mano (`workflow_dispatch`) y se omite si no existe el secreto `LLM_API_KEY`.

## Limitación conocida: no hay autenticación (SPEC §6.5)

El contrato HTTP transporta la identidad en el cuerpo (`usuario.rol` y `usuario.id`) y TutorFlow **no puede verificar quién envía la petición**. Lo que sí hace es validar la **consistencia** de la identidad declarada (rol válido, id con formato correcto, existente y coherente con el rol) y limitar el alcance de cada consulta al de ese principal, con la política de permisos en código determinista y no en el LLM.

Esto **no está resuelto**: quien conozca un id válido puede suplantarlo, y la diferencia entre 200 y 403-identidad permite enumerar ids. Ninguna de las dos cosas es mitigable sin cambiar el contrato (SPEC §6.5, D-01). **TutorFlow no es apto para producción con este contrato.**
