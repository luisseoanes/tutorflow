# Escenarios — Recuperación y respuesta fundamentada

> Formato y niveles: `AGENTS.md` §6. `[ret]` = métricas de recuperación sin LLM sobre la estrategia por defecto (y reportadas para todas en el experimento). Las citas son `documento` (nombre de archivo exacto) + `seccion` (`Artículo N` | `Sección N` | `Sección N.M`).

---

### RAG-01 · Nota mínima aprobatoria (pregunta de ejemplo 1)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es la nota mínima para aprobar una asignatura?"`
- **Criterios:**
  - a. `[ret]` `reglamento_academico_2025.md#Artículo 5` en el top 4; ningún chunk de `reglamento_academico_2023.md` en el resultado.
  - b. `[app]` Un LLM guionizado que cita `reglamento_academico_2023.md#Artículo 5` (no recuperado) → esa cita se descarta.
  - c. `[eval]` `respuesta` contiene `3.0`; `citas` ⊇ {`reglamento_academico_2025.md · Artículo 5`}; sin citas al 2023; `usoDatosEstudiante == false`.

### RAG-02 · Horario de Bienestar (pregunta de ejemplo 7)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es el horario de atención de Bienestar?"`
- **Criterios:**
  - a. `[ret]` `bienestar_y_becas.md#Sección 1` en el top 4.
  - b. `[eval]` `respuesta` contiene `8:00`, `17:00`, `12:00` y `B`; `citas` ⊇ {`bienestar_y_becas.md · Sección 1`}.

### RAG-03 · *(fusionado en ACAD-07)*

### RAG-04 · Precedencia 2025 > 2023: porcentaje de inasistencia
- **Severidad:** Crítica
- **Entrada:** `"¿Con qué porcentaje de fallas se pierde una materia?"`
- **Criterios:**
  - a. `[ret]` `reglamento_academico_2025.md#Artículo 11` en el top 4 y ningún chunk del 2023.
  - b. `[eval]` `respuesta` contiene `15` y no presenta el `20%` como vigente; `citas` ⊇ {`· Artículo 11`} del 2025.

### RAG-05 · Precedencia 2025 > 2023: plazo de cancelación
- **Severidad:** Alta
- **Entrada:** `"¿Hasta qué semana puedo cancelar una asignatura?"`
- **Criterios:**
  - a. `[ret]` `reglamento_academico_2025.md#Artículo 15` en el top 4 y ningún chunk del 2023.
  - b. `[eval]` `respuesta` contiene `8` y no presenta la semana 10 como vigente.

### RAG-06 · Pregunta explícita por el reglamento derogado
- **Severidad:** Media
- **Entrada:** `"¿Qué decía el reglamento 2023 sobre la habilitación?"`
- **Criterios:**
  - a. `[ret]` Con la mención "2023", `reglamento_academico_2023.md#Artículo 6` está en el top 4.
  - b. `[eval]` `respuesta` contiene `1.5` y aclara que está derogado; `citas` ⊇ {`reglamento_academico_2023.md · Artículo 6`}.

### RAG-07 · Subsección anidada
- **Severidad:** Alta
- **Entrada:** `"¿Cómo se solicita la beca de apoyo socioeconómico?"`
- **Criterios:**
  - a. `[ret]` `bienestar_y_becas.md#Sección 3.2` en el top 4.
  - b. `[eval]` `citas` ⊇ {`bienestar_y_becas.md · Sección 3.2`}; `respuesta` menciona `30%` y los estratos 1 y 2.

### RAG-08 · Protección de datos
- **Severidad:** Media
- **Entrada:** `"¿Quién puede consultar mi información académica?"`
- **Criterios:**
  - a. `[ret]` `reglamento_academico_2025.md#Artículo 22` en el top 4.
  - b. `[eval]` `citas` ⊇ {`· Artículo 22`}.

### RAG-09 · Abstención: tema ausente (pregunta de ejemplo 9)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Qué dice el reglamento sobre el uso de parqueaderos?"`
- **Criterios:**
  - a. `[app]` Un LLM guionizado que responde un texto inventado **sin** `chunkIdsCitados` válidos y sin herramientas → el servidor reemplaza la respuesta por el texto de abstención (SPEC §7.5.5); `citas == []`; `usoDatosEstudiante == false`; status 200.
  - b. `[eval]` Con el LLM real: abstención; `citas == []`; `respuesta` no contiene "Artículo" seguido de un número.

### RAG-10 · Abstención: temas cercanos pero ausentes
- **Severidad:** Alta
- **Entrada:** (a) `"¿Cuál es el valor de la matrícula?"` (b) `"¿Cuál es el horario de la biblioteca?"` (c) `"¿Cuántas faltas justificadas se permiten con excusa médica?"`
- **Criterios:**
  - a. `[eval]` (a) abstención.
  - b. `[eval]` (b) no inventa un horario de la biblioteca; si menciona las tutorías (martes y jueves de 14:00 a 18:00), las cita (`Sección 2`).
  - c. `[eval]` (c) explica que las excusas aprobadas no cuentan para el límite (`Artículo 12`) y no inventa una cifra.

### RAG-11 · Pregunta fuera de dominio
- **Severidad:** Media
- **Entrada:** `"¿Quién ganó el mundial de 2022?"` · `"Escríbeme un poema"`
- **Criterios:**
  - a. `[eval]` Abstención o redirección; `citas == []`.

### RAG-12 · Paráfrasis y lenguaje coloquial
- **Severidad:** Media
- **Entrada:** `"con cuánto paso una materia?"` · `"si falto mucho pierdo la materia?"` · `"cuanto descuento da la beca de notas"`
- **Criterios:**
  - a. `[ret]` Los chunks esperados (2025 Art. 5, 2025 Art. 11, becas §3.1) en el top 4 de la estrategia por defecto. El experimento reporta el resultado de las demás estrategias (es el escenario que más discrimina entre BM25 y vectorial).

### RAG-13 · No se aceptan citas inventadas
- **Severidad:** Crítica · **Actor:** prueba de aplicación
- **Criterios:**
  - a. `[app]` Un LLM guionizado devuelve `chunkIdsCitados = ["reglamento_academico_2025.md#Artículo 99", "<chunk existente no recuperado>", "<chunk recuperado válido>"]` → `citas` contiene **solo** el válido.
  - b. `[app]` Si ninguna cita es válida y no hubo herramientas → abstención (igual que RAG-09.a).

### RAG-14 · Cuarentena del Anexo
- **Severidad:** Crítica · **Actor:** prueba de indexación y recuperación
- **Criterios:**
  - a. `[app]` El chunk `bienestar_y_becas.md#Anexo` existe con `untrusted == true` y **no** aparece en el resultado de ninguna estrategia (incluida `full-context`) para ninguna consulta del dataset.
  - b. `[app]` Los chunks `bienestar_y_becas.md#Sección 1`, `#Sección 2`, `#Sección 3.1` y `#Sección 3.2` existen con `untrusted == false` (se conserva la información legítima).
  - c. `[app]` El detector de instrucciones, ejecutado sobre todo el corpus, marca **exactamente** los chunks de `QUARANTINE_CHUNKS` y ningún otro. Si marca uno nuevo, la prueba falla para que un humano decida.

### RAG-15 · Chunking estructural determinista
- **Severidad:** Alta · **Actor:** prueba de indexación
- **Criterios:**
  - a. `[app]` Cada artículo de los dos reglamentos produce exactamente un chunk con `seccion = "Artículo N"`; todo el 2023 lleva `vigente == false`.
  - b. `[app]` La tabla de `evaluacion_y_calificaciones.md` §2 queda completa en un solo chunk.
  - c. `[app]` Las secciones padre sin texto propio (`## 3. Becas`) no generan chunk.
  - d. `[app]` Dos indexaciones del mismo corpus producen los mismos `chunkId` y `hash`.

### RAG-16 · Comparación de estrategias (experimento)
- **Severidad:** Alta · **Actor:** Arnés
- **Criterios:**
  - a. `[eval]` `evals/RESULTADOS.md` contiene, para `full-context`, `bm25`, `vector` e `hybrid`: Recall@4, MRR, precisión y cobertura de citas, exactitud de abstención, exactitud numérica, tokens, costo y latencia; cada fila con commit, fecha, modelo y el archivo crudo en `evals/runs/`.
  - b. `[eval]` La estrategia configurada por defecto cumple los umbrales de SPEC §7.6 y es la más barata entre las que los cumplen (SPEC §7.3).
