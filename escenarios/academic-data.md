# Escenarios — Datos y cálculos académicos

> Formato y niveles: `AGENTS.md` §6. Los valores esperados son los golden de SPEC §5.9 y de la tabla final, calculados con aritmética decimal exacta (INTERVENCIONES NT-02).
> Patrón: el **valor** se prueba en `[dom]`; las **citas deterministas y el uso de datos**, en `[app]`/`[api]`; la **cifra dentro de la redacción**, en `[eval]`. Para `[eval]`, la cifra se busca como token numérico con punto decimal (por ejemplo, `(?<![\d.])4\.39(?!\d)`).

---

## Definitivas y redondeo

### ACAD-01 · Promedio ponderado de EST-001 (pregunta de ejemplo 2)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es mi promedio ponderado del periodo?"`
- **Resultado esperado:** 4.39; definitivas CAL 4.5, POO 4.8, BD 4.1 e ING 4.0.
- **Criterios:**
  - a. `[dom]` `Promedio(EST-001) == 4.39m` y las definitivas == {4.5m, 4.8m, 4.1m, 4.0m}.
  - b. `[app]` `obtener_resumen_academico()` devuelve `promedio = "4.39"` y `citas` ⊇ {`evaluacion_y_calificaciones.md · Sección 3`, `· Sección 4`, `Datos del estudiante · Resumen académico`}.
  - c. `[api]` `usoDatosEstudiante == true`.
  - d. `[eval]` `respuesta` contiene `4.39`.

### ACAD-02 · Punto medio 4.45 → 4.5 (EST-001, Cálculo Integral)
- **Severidad:** Crítica
- **Criterios:**
  - a. `[dom]` `Definitiva(4.2m, 4.5m, 4.6m) == 4.5m` (con `double` daría 4.4).

### ACAD-03 · Punto medio 4.55 → 4.6 (EST-003, Inglés III)
- **Severidad:** Crítica
- **Criterios:**
  - a. `[dom]` `Definitiva(4.5m, 4.4m, 4.7m) == 4.6m` (con `double` daría 4.5).

### ACAD-04 · Punto medio 4.15 → 4.2 (EST-006, Contabilidad)
- **Severidad:** Crítica
- **Criterios:**
  - a. `[dom]` `Definitiva(4.1m, 4.0m, 4.3m) == 4.2m` (con `double` daría 4.1).

### ACAD-05 · Redondeo bancario y tipos binarios prohibidos
- **Severidad:** Crítica
- **Criterios:**
  - a. `[dom]` Redondeo de definitivas: 4.45 → 4.5, 4.35 → 4.4, 2.25 → 2.3, 4.44 → 4.4, 0.05 → 0.1. Redondeo de promedios: 2.275 → 2.28, 2.265 → 2.27, 4.005 → 4.01. (Con `ToEven` fallarían 4.45, 2.25, 0.05, 2.265 y 4.005.)
  - b. `[dom]` Prueba de arquitectura: ningún tipo de `TutorFlow.Domain` usa `double` ni `float`.

### ACAD-06 · Definitiva de Contabilidad de EST-004 (pregunta de ejemplo 5)
- **Severidad:** Crítica · **Actor:** `EST-004`
- **Entrada:** `"¿Qué nota definitiva tengo en Contabilidad?"`
- **Criterios:**
  - a. `[dom]` CON-101 de EST-004 → 3.9 (bruta 3.89).
  - b. `[app]` `citas` ⊇ {`Datos del estudiante · Notas`, `evaluacion_y_calificaciones.md · Sección 3`}.
  - c. `[eval]` `respuesta` contiene `3.9`.

### ACAD-07 · Cómo se calcula la definitiva (pregunta de ejemplo 4; absorbe RAG-03)
- **Severidad:** Alta · **Actor:** `EST-001`
- **Entrada:** `"¿Cómo se calcula la nota definitiva de una asignatura?"`
- **Criterios:**
  - a. `[ret]` `evaluacion_y_calificaciones.md#Sección 2` y `#Sección 3` están en el top 4 de la estrategia por defecto.
  - b. `[app]` Un LLM guionizado que cita esos dos `chunkId` sin herramientas → `citas` == ambos; `usoDatosEstudiante == false`.
  - c. `[eval]` `respuesta` contiene `30` y `40`; `citas` ⊇ {`· Sección 2`, `· Sección 3`}; `usoDatosEstudiante == false`.

## Inasistencia (Reglamento 2025, Art. 11)

### ACAD-08 · Pérdida por inasistencia oculta por buenas notas (EST-002, Bases de Datos)
- **Severidad:** Crítica · **Actor:** `EST-002`
- **Entrada:** `"¿Aprobé Bases de Datos?"`
- **Criterios:**
  - a. `[dom]` BD-203 de EST-002: `pierdePorInasistencia == true` (9/48), `definitivaEfectiva == 0.0m`, `habilitable == false`.
  - b. `[app]` `citas` ⊇ {`reglamento_academico_2025.md · Artículo 11`} y ninguna cita a `reglamento_academico_2023.md`.
  - c. `[eval]` `respuesta` no afirma que aprobó y menciona la inasistencia.

### ACAD-09 · Promedio con una pérdida por inasistencia (EST-002)
- **Severidad:** Crítica · **Actor:** `EST-002`
- **Entrada:** `"¿Cuál es mi promedio del periodo?"`
- **Criterios:**
  - a. `[dom]` `Promedio(EST-002) == 2.28m` (27.3/12 = 2.275). Con BD sin aplicar la inasistencia daría 3.23 (debe fallar).
  - b. `[eval]` `respuesta` contiene `2.28` y no contiene `3.23`.

### ACAD-10 · Límite exacto del 15%
- **Severidad:** Crítica
- **Criterios:**
  - a. `[dom]` (clases, faltas) → pierde: (40,6) no · (20,3) no · (48,7) no · (48,8) sí · (64,9) no · (64,10) sí · (32,4) no · (32,5) sí.

### ACAD-11 · Faltas en una asignatura (pregunta de ejemplo 3)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Cuántas faltas tengo en Bases de Datos?"`
- **Criterios:**
  - a. `[app]` `obtener_asistencia(asignatura="Bases de Datos")` → 2 faltas de 48; `citas` ⊇ {`Datos del estudiante · Asistencia`}; ninguna abstención (B4: sin abstención previa al LLM).
  - b. `[api]` `usoDatosEstudiante == true`.
  - c. `[eval]` `respuesta` contiene `2` y `48`.

### ACAD-12 · Asistencia no registrada (EST-003, Inglés III)
- **Severidad:** Alta · **Actor:** `EST-003`
- **Entrada:** `"¿Cuántas faltas tengo en Inglés III?"`
- **Criterios:**
  - a. `[dom]` `inasistencias = null` → `datoDisponible == false`, `pierdePorInasistencia == null` (desconocido), definitiva 4.6.
  - b. `[app]` `citas` ⊇ {`reglamento_academico_2025.md · Artículo 12`}.
  - c. `[eval]` `respuesta` no contiene "0 faltas" ni "ninguna falta" y remite al coordinador.

## Asignaturas sin definitiva

### ACAD-13 · Corte pendiente (EST-006, Estadística)
- **Severidad:** Crítica · **Actor:** `EST-006`
- **Entrada:** `"¿Qué nota definitiva tengo en Estadística?"`
- **Criterios:**
  - a. `[dom]` EST-103 de EST-006: sin definitiva; `aporteAcumulado == 2.40m`; `porcentajeEvaluado == 60`.
  - b. `[app]` `citas` ⊇ {`evaluacion_y_calificaciones.md · Sección 3`}.
  - c. `[eval]` `respuesta` indica que aún no hay definitiva y no presenta ningún número como definitiva.

### ACAD-14 · Promedio excluyendo la asignatura sin definitiva (EST-006)
- **Severidad:** Crítica · **Actor:** `EST-006`
- **Entrada:** `"¿Cuál es mi promedio ponderado?"`
- **Criterios:**
  - a. `[dom]` `Promedio(EST-006) == 4.05m` (32.4/8). Con Estadística como 0 sobre 12 créditos daría 2.70 (debe fallar).
  - b. `[eval]` `respuesta` contiene `4.05`.

## Pérdidas, habilitación, situación académica y beca

### ACAD-15 · Habilitación con el umbral 2025 (EST-005)
- **Severidad:** Crítica · **Actor:** `EST-005`
- **Entrada:** `"¿Qué materias puedo habilitar?"`
- **Criterios:**
  - a. `[dom]` EST-103 (2.3) habilitable; MIC-102 (1.8) **no** habilitable (< 2.0; con 1.5 del 2023 lo sería).
  - b. `[app]` `citas` ⊇ {`reglamento_academico_2025.md · Artículo 6`}.
  - c. `[eval]` `respuesta` no afirma que Microeconomía sea habilitable.

### ACAD-16 · Beca: el estudiante cumple (EST-001)
- **Severidad:** Alta · **Actor:** `EST-001`
- **Entrada:** `"¿Cumplo los requisitos de la beca por rendimiento académico?"`
- **Criterios:**
  - a. `[dom]` `EvaluarBeca(EST-001) == Cumple` (4.39 ≥ 4.3; sin perdidas; 12 créditos).
  - b. `[app]` `citas` ⊇ {`bienestar_y_becas.md · Sección 3.1`}.
  - c. `[eval]` `respuesta` afirma que cumple y no contiene datos de otros estudiantes.

### ACAD-17 · Beca: el estudiante no cumple (EST-004)
- **Severidad:** Alta · **Actor:** `EST-004`
- **Criterios:**
  - a. `[dom]` `EvaluarBeca(EST-004) == NoCumple`, con el requisito de promedio fallido (3.88) y los otros dos cumplidos.

### ACAD-18 · Requisitos de la beca sin datos personales (pregunta de ejemplo 8)
- **Severidad:** Crítica · **Actor:** `EST-004`
- **Entrada:** `"¿Qué requisitos tiene la beca por rendimiento académico?"`
- **Criterios:**
  - a. `[ret]` `bienestar_y_becas.md#Sección 3.1` en el top 4; `#Anexo` ausente.
  - b. `[eval]` `respuesta` contiene `4.3`, `12` y `50%`; `citas` ⊇ {`· Sección 3.1`}; sin nombres de estudiantes; `usoDatosEstudiante == false`.

### ACAD-19 · Promedio y situación académica de EST-005
- **Severidad:** Alta · **Actor:** `EST-005`
- **Entrada:** `"¿Cuál es mi promedio y mi situación académica?"`
- **Criterios:**
  - a. `[dom]` `Promedio(EST-005) == 2.65m`; `SituacionAcademica == PruebaAcademica` (< 3.0).
  - b. `[app]` `citas` ⊇ {`reglamento_academico_2025.md · Artículo 20`}.
  - c. `[eval]` `respuesta` contiene `2.65` y menciona la prueba académica.

### ACAD-20 · Dato corrupto de la API
- **Severidad:** Alta · **Actor:** prueba de aplicación con una fuente de datos falsa
- **Entrada:** un corte de `6.2`; un corte `"4.0"` (string); `clasesProgramadas: 0`; `creditos: -1`.
- **Criterios:**
  - a. `[app]` La herramienta devuelve un error de dato inválido; no recorta ni inventa valores; la petición responde 200 sin esas cifras, o 503, **nunca** 500.

## Tabla golden completa (para pruebas parametrizadas `[dom]`)

| Est. | Asignatura | c1 | c2 | c3 | Def. | Faltas/Clases | Pierde inasist. | Def. efectiva |
|---|---|---|---|---|---|---|---|---|
| EST-001 | CAL-201 | 4.2 | 4.5 | 4.6 | 4.5 | 1/64 | no | 4.5 |
| EST-001 | POO-202 | 4.8 | 4.6 | 4.9 | 4.8 | 0/48 | no | 4.8 |
| EST-001 | BD-203 | 4.0 | 3.8 | 4.4 | 4.1 | 2/48 | no | 4.1 |
| EST-001 | ING-204 | 3.9 | 4.1 | 4.0 | 4.0 | 1/32 | no | 4.0 |
| EST-002 | CAL-201 | 2.5 | 2.8 | 2.6 | 2.6 | 3/64 | no | 2.6 |
| EST-002 | POO-202 | 3.5 | 3.2 | 3.6 | 3.5 | 2/48 | no | 3.5 |
| EST-002 | BD-203 | 3.8 | 3.6 | 3.9 | 3.8 | 9/48 | **sí** | **0.0** |
| EST-002 | ING-204 | 3.0 | 3.4 | 3.1 | 3.2 | 1/32 | no | 3.2 |
| EST-003 | CAL-201 | 4.0 | 3.9 | 4.2 | 4.1 | 2/64 | no | 4.1 |
| EST-003 | POO-202 | 3.7 | 4.0 | 3.8 | 3.8 | 1/48 | no | 3.8 |
| EST-003 | BD-203 | 4.1 | 4.3 | 4.0 | 4.1 | 0/48 | no | 4.1 |
| EST-003 | ING-204 | 4.5 | 4.4 | 4.7 | 4.6 | null/32 | desconocido | 4.6 |
| EST-004 | CON-101 | 3.6 | 3.9 | 4.1 | 3.9 | 0/48 | no | 3.9 |
| EST-004 | MIC-102 | 3.4 | 3.0 | 3.5 | 3.3 | 2/48 | no | 3.3 |
| EST-004 | EST-103 | 4.0 | 4.2 | 3.9 | 4.0 | 3/64 | no | 4.0 |
| EST-004 | COM-104 | 4.5 | 4.6 | 4.4 | 4.5 | 1/32 | no | 4.5 |
| EST-005 | CON-101 | 3.2 | 3.0 | 3.3 | 3.2 | 4/48 | no | 3.2 |
| EST-005 | MIC-102 | 1.9 | 1.6 | 1.9 | 1.8 | 5/48 | no | 1.8 |
| EST-005 | EST-103 | 2.4 | 2.2 | 2.3 | 2.3 | 6/64 | no | 2.3 |
| EST-005 | COM-104 | 3.8 | 3.5 | 3.9 | 3.8 | 2/32 | no | 3.8 |
| EST-006 | CON-101 | 4.1 | 4.0 | 4.3 | 4.2 | 1/48 | no | 4.2 |
| EST-006 | MIC-102 | 3.6 | 3.8 | 3.9 | 3.8 | 0/48 | no | 3.8 |
| EST-006 | EST-103 | 3.9 | 4.1 | null | — | 2/64 | no | — (aporte 2.40) |
| EST-006 | COM-104 | 4.2 | 4.0 | 4.4 | 4.2 | 0/32 | no | 4.2 |

| Est. | Promedio | Créditos matriculados | Perdidas | Situación | Beca |
|---|---|---|---|---|---|
| EST-001 | 4.39 | 12 | — | Normal | **Cumple** |
| EST-002 | 2.28 | 12 | CAL-201 (2.6, habilitable), BD-203 (0.0 inasist., no habilitable) | Prueba académica | No |
| EST-003 | 4.11 | 12 | — | Normal | No (promedio) |
| EST-004 | 3.88 | 12 | — | Normal | No (promedio) |
| EST-005 | 2.65 | 12 | MIC-102 (1.8, no habilitable), EST-103 (2.3, habilitable) | Prueba académica | No |
| EST-006 | 4.05 | 12 | — | Normal | No (promedio) |
