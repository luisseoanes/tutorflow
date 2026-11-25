# Escenarios — Calidad de recuperación y respuesta fundamentada

> Se verifican en dos niveles: (a) **recuperación** en `/evals`, sin LLM (el `chunkId` esperado debe estar en el top-k de cada estrategia), y (b) **E2E** con el LLM real (contenido + citas).
> Formato de cita esperado: `documento` = nombre de archivo exacto; `seccion` = `Artículo N` | `Sección N` | `Sección N.M`.

---

### RAG-01 · Nota mínima aprobatoria (pregunta de ejemplo 1)
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es la nota mínima para aprobar una asignatura?"`
- **Resultado esperado:** **3.0** en escala de 0.0 a 5.0.
- **Criterios de aceptación:** citas ⊇ {`reglamento_academico_2025.md · Artículo 5`}; **no** cita `reglamento_academico_2023.md` (aunque su Art. 5 es idéntico, el vigente es el 2025); `usoDatosEstudiante=false`; recuperación: `reglamento_academico_2025.md#Artículo 5` en el top 4.

### RAG-02 · Horario de Bienestar (pregunta de ejemplo 7)
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es el horario de atención de Bienestar?"`
- **Resultado esperado:** lunes a viernes de 8:00 a 17:00 y sábados de 8:00 a 12:00, en el edificio B, piso 2.
- **Criterios de aceptación:** la respuesta contiene `8:00`, `17:00`, `12:00` y `B`; citas ⊇ {`bienestar_y_becas.md · Sección 1`}.

### RAG-03 · Fórmula de la definitiva (pregunta de ejemplo 4)
- Igual que **ACAD-07** (escenario compartido; una sola prueba con los dos traits).

### RAG-04 · Precedencia 2025 > 2023: porcentaje de inasistencia
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Con qué porcentaje de fallas se pierde una materia?"`
- **Resultado esperado:** **más del 15%** de las clases programadas.
- **Criterios de aceptación:** la respuesta contiene `15` y **no** presenta `20%` como vigente; citas ⊇ {`reglamento_academico_2025.md · Artículo 11`}; ninguna cita a `reglamento_academico_2023.md`.

### RAG-05 · Precedencia 2025 > 2023: plazo de cancelación
- **Severidad:** Alta
- **Entrada:** `"¿Hasta qué semana puedo cancelar una asignatura?"`
- **Resultado esperado:** **semana 8** (no la 10 del 2023).
- **Criterios de aceptación:** citas ⊇ {`reglamento_academico_2025.md · Artículo 15`}.

### RAG-06 · Pregunta explícita por el reglamento derogado
- **Severidad:** Media
- **Entrada:** `"¿Qué decía el reglamento 2023 sobre la habilitación?"`
- **Resultado esperado:** responde que el umbral era 1.5 **y aclara** que ese reglamento está derogado por el Art. 3 del 2025 (hoy el umbral es 2.0).
- **Criterios de aceptación:** citas ⊇ {`reglamento_academico_2023.md · Artículo 6`, `reglamento_academico_2025.md · Artículo 3`}; la respuesta contiene "derogado" o un equivalente.

### RAG-07 · Subsección anidada
- **Severidad:** Alta
- **Entrada:** `"¿Cómo se solicita la beca de apoyo socioeconómico?"`
- **Resultado esperado:** se solicita en Bienestar con documentos que acrediten la situación del hogar; hasta 30%; estratos 1 y 2; cupos por orden de llegada.
- **Criterios de aceptación:** cita `bienestar_y_becas.md · Sección 3.2` (no solo `Sección 3`).

### RAG-08 · Protección de datos
- **Severidad:** Media
- **Entrada:** `"¿Quién puede consultar mi información académica?"`
- **Resultado esperado:** el propio estudiante, el coordinador de su programa y las dependencias autorizadas (Ley 1581 de 2012).
- **Criterios de aceptación:** citas ⊇ {`reglamento_academico_2025.md · Artículo 22`}.

### RAG-09 · Abstención: tema ausente (pregunta de ejemplo 9)
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Qué dice el reglamento sobre el uso de parqueaderos?"`
- **Resultado esperado:** indica que **no encuentra** esa información en los documentos disponibles.
- **Criterios de aceptación:** status 200; `citas=[]`; `usoDatosEstudiante=false`; la respuesta no inventa normas (no contiene "Artículo" seguido de un número).

### RAG-10 · Abstención: temas cercanos pero ausentes
- **Severidad:** Alta
- **Entrada:** (a) `"¿Cuál es el valor de la matrícula?"` (b) `"¿Cuál es el horario de la biblioteca?"` (solo existe el horario de tutorías *en* la biblioteca) (c) `"¿Cuántas faltas justificadas se permiten con excusa médica?"` (no hay un número).
- **Resultado esperado:** (a) abstención; (b) abstención sobre el horario de la biblioteca, mencionando a lo sumo que las tutorías son martes y jueves de 14:00 a 18:00 en la biblioteca, con su cita; (c) explica que las excusas aprobadas no cuentan para el límite (Art. 12) sin inventar una cifra.
- **Criterios de aceptación:** ninguna cifra que no esté en el corpus.

### RAG-11 · Pregunta fuera de dominio
- **Severidad:** Media
- **Entrada:** `"¿Quién ganó el mundial de 2022?"` · `"Escríbeme un poema"`
- **Resultado esperado:** abstención o redirección al ámbito académico; `citas=[]`.

### RAG-12 · Paráfrasis y lenguaje coloquial
- **Severidad:** Media
- **Entrada:** `"con cuánto paso una materia?"` · `"si falto mucho pierdo la materia?"` · `"cuanto descuento da la beca de notas"`
- **Resultado esperado:** igual que RAG-01, RAG-04 y ACAD-18 respectivamente (sin tildes y en minúsculas).
- **Criterios de aceptación:** recuperación: el chunk esperado está en el top 4 en **todas** las estrategias candidatas. Este escenario es el que más discrimina entre BM25 y vectorial.

### RAG-13 · Validación de citas: no se aceptan citas inventadas
- **Severidad:** Crítica
- **Actor:** prueba de aplicación con un LLM *fake* que devuelve `chunkIdsCitados = ["reglamento_academico_2025.md#Artículo 99", "<chunk no recuperado>"]`.
- **Resultado esperado:** las citas inválidas se descartan; si no queda ninguna válida en una respuesta normativa → abstención.
- **Criterios de aceptación:** `citas` nunca contiene una sección inexistente en el corpus.

### RAG-14 · Nunca se citan chunks `untrusted`
- **Severidad:** Crítica
- **Actor:** prueba de indexación
- **Resultado esperado:** el chunk `bienestar_y_becas.md#Anexo` está marcado `untrusted=true` y no aparece en el resultado de **ninguna** estrategia para ninguna consulta del dataset.

### RAG-15 · Chunking estructural determinista
- **Severidad:** Alta
- **Actor:** prueba de indexación
- **Resultado esperado:** cada artículo de los dos reglamentos produce exactamente un chunk con `seccion="Artículo N"`; las tablas de `evaluacion_y_calificaciones.md` §2 no se parten; dos indexaciones producen los mismos `hash`; el 2023 lleva `vigente=false`.

### RAG-16 · Comparación de estrategias (experimento)
- **Severidad:** Alta
- **Actor:** QA/Evaluation Agent
- **Entrada:** dataset etiquetado (RAG-01..12, ACAD-07, ACAD-18 y las 10 preguntas de ejemplo).
- **Resultado esperado:** una tabla en `evals/RESULTADOS.md` con Recall@4, MRR, precisión y cobertura de citas, exactitud de abstención, tokens, costo y latencia para `full-context`, `bm25`, `vector` y `hybrid` (más `rerank` si se implementa).
- **Criterios de aceptación:** cifras de ejecuciones reales, con el *commit* y la fecha; la estrategia por defecto cumple los umbrales de SPEC §7.6 y es la más barata entre las que los cumplen.
