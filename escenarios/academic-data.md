# Escenarios — Datos y cálculos académicos

> Los valores esperados salen de SPEC §5.8, calculados con aritmética decimal exacta sobre `mock-api/datos.json`.
> **Doble nivel de prueba:** cada escenario se verifica (a) en `TutorFlow.Domain.Tests`, con el valor exacto de la función pura, y (b) *end-to-end* en `/evals`, con la cifra presente en `respuesta`.
> La cifra se busca en `respuesta` con punto decimal y como token numérico completo (por ejemplo, la regex `(?<![\d.])4\.39(?![\d])`).

---

## Definitivas y redondeo

### ACAD-01 · Promedio ponderado de EST-001 (pregunta de ejemplo 2)
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es mi promedio ponderado del periodo?"`
- **Resultado esperado:** **4.39**, con las definitivas Cálculo Integral 4.5, POO 4.8, Bases de Datos 4.1 e Inglés III 4.0.
- **Criterios de aceptación:** dominio: `Promedio(EST-001) == 4.39m` (exacto 52.7/12); E2E: la respuesta contiene `4.39`; citas ⊇ {`evaluacion_y_calificaciones.md · Sección 3`, `· Sección 4`}; `usoDatosEstudiante=true`.

### ACAD-02 · Trampa de redondeo: punto medio exacto en un `double` (4.45 → 4.5)
- **Severidad:** Crítica
- **Actor:** prueba de dominio + `EST-001`
- **Entrada:** cortes (4.2, 4.5, 4.6) · E2E: `"¿Cuál es mi definitiva en Cálculo Integral?"`
- **Resultado esperado:** **4.5** (con `double`/`Math.Round` daría 4.4).
- **Criterios de aceptación:** `Definitiva(4.2m,4.5m,4.6m) == 4.5m`.

### ACAD-03 · Trampa de redondeo 4.55 → 4.6 (EST-003, Inglés III)
- **Severidad:** Crítica
- **Entrada:** cortes (4.5, 4.4, 4.7)
- **Resultado esperado:** **4.6** (con `double` daría 4.5).

### ACAD-04 · Trampa de redondeo 4.15 → 4.2 (EST-006, Contabilidad)
- **Severidad:** Crítica
- **Entrada:** cortes (4.1, 4.0, 4.3)
- **Resultado esperado:** **4.2** (con `double` daría 4.1).

### ACAD-05 · Redondeo bancario prohibido
- **Severidad:** Crítica
- **Actor:** prueba de dominio
- **Entrada:** brutas sintéticas 4.45, 4.35, 2.25, 4.44 y 0.05; promedios 2.275, 2.265 y 4.005.
- **Resultado esperado:** 4.5, 4.4, 2.3, 4.4 y 0.1; promedios 2.28, 2.27 y 4.01. Con *ToEven* (el error que se busca detectar) darían 4.45 → 4.4, 2.25 → 2.2, 0.05 → 0.0, 2.265 → 2.26 y 4.005 → 4.00.
- **Criterios de aceptación:** todas las aserciones pasan; además, una prueba de arquitectura verifica que `TutorFlow.Domain` no referencia `double` ni `float`.

### ACAD-06 · Definitiva de Contabilidad de EST-004 (pregunta de ejemplo 5)
- **Severidad:** Crítica
- **Actor:** `EST-004`
- **Entrada:** `"¿Qué nota definitiva tengo en Contabilidad?"`
- **Resultado esperado:** **3.9** (bruta 3.89).
- **Criterios de aceptación:** citas ⊇ {`Datos del estudiante`, `evaluacion_y_calificaciones.md · Sección 3`}.

### ACAD-07 · Cómo se calcula la definitiva (pregunta de ejemplo 4: solo normativa)
- **Severidad:** Alta
- **Actor:** `EST-001`
- **Entrada:** `"¿Cómo se calcula la nota definitiva de una asignatura?"`
- **Resultado esperado:** 30% corte 1, 30% corte 2, 40% corte 3, redondeada a una cifra decimal.
- **Criterios de aceptación:** la respuesta contiene `30` y `40`; citas ⊇ {`evaluacion_y_calificaciones.md · Sección 2`, `· Sección 3`}; `usoDatosEstudiante=false` (no se consultan datos personales para una pregunta general).

## Inasistencia (Reglamento 2025, Art. 11)

### ACAD-08 · Pérdida por inasistencia oculta por buenas notas (EST-002, Bases de Datos)
- **Severidad:** Crítica
- **Actor:** `EST-002`
- **Entrada:** `"¿Aprobé Bases de Datos?"`
- **Resultado esperado:** **No**: perdida por inasistencia (9/48 = 18.75% > 15%), definitiva **0.0** aunque los cortes darían 3.8. **No** es habilitable (Art. 6–7).
- **Criterios de aceptación:** citas ⊇ {`reglamento_academico_2025.md · Artículo 11`}; la respuesta **no** afirma que aprobó; nunca cita `reglamento_academico_2023.md` como vigente (con el 20% del 2023 no la habría perdido).

### ACAD-09 · Promedio con una pérdida por inasistencia (EST-002)
- **Severidad:** Crítica
- **Actor:** `EST-002`
- **Entrada:** `"¿Cuál es mi promedio del periodo?"`
- **Resultado esperado:** **2.28** (27.3/12 = 2.275 → 2.28). Error típico si se usa la definitiva sin inasistencia: 3.23 → **debe fallar la prueba**.
- **Criterios de aceptación:** la respuesta contiene `2.28` y no contiene `3.23`.

### ACAD-10 · Límite exacto del 15% (no pierde)
- **Severidad:** Crítica
- **Actor:** prueba de dominio
- **Entrada:** (clases, faltas) = (40, 6) → 15.00%; (20, 3) → 15.00%; (48, 7) → 14.58%; (48, 8) → 16.67%; (64, 9) → 14.06%; (64, 10) → 15.63%; (32, 4) → 12.5%; (32, 5) → 15.63%.
- **Resultado esperado:** pierde ⇔ faltas×100 > 15×clases → no, no, no, sí, no, sí, no, sí.

### ACAD-11 · Faltas en una asignatura (pregunta de ejemplo 3)
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Cuántas faltas tengo en Bases de Datos?"`
- **Resultado esperado:** **2 de 48** clases programadas.
- **Criterios de aceptación:** la respuesta contiene `2` y `48`; citas ⊇ {`Datos del estudiante`}; `usoDatosEstudiante=true`.

### ACAD-12 · Asistencia no registrada (`inasistencias: null`, EST-003, Inglés III)
- **Severidad:** Alta
- **Actor:** `EST-003`
- **Entrada:** `"¿Cuántas faltas tengo en Inglés III?"`
- **Resultado esperado:** indica que **no hay registro** de inasistencias (no dice "0") y sugiere consultar al coordinador (Art. 12).
- **Criterios de aceptación:** la respuesta no contiene `0 faltas` ni `ninguna falta`; citas ⊇ {`reglamento_academico_2025.md · Artículo 12`}.

## Asignaturas sin definitiva

### ACAD-13 · Corte pendiente (EST-006, Estadística)
- **Severidad:** Crítica
- **Actor:** `EST-006`
- **Entrada:** `"¿Qué nota definitiva tengo en Estadística?"`
- **Resultado esperado:** **aún no tiene definitiva** (falta el corte 3); aporte acumulado **2.40** con el 60% evaluado (D-07).
- **Criterios de aceptación:** la respuesta no presenta ningún número como "definitiva"; citas ⊇ {`evaluacion_y_calificaciones.md · Sección 3`}.

### ACAD-14 · Promedio excluyendo la asignatura sin definitiva (EST-006)
- **Severidad:** Crítica
- **Actor:** `EST-006`
- **Entrada:** `"¿Cuál es mi promedio ponderado?"`
- **Resultado esperado:** **4.05** (32.4/8). Aclara que Estadística no entra en el cálculo.
- **Criterios de aceptación:** la respuesta contiene `4.05`; errores típicos que deben fallar: `2.70` (Estadística como 0 con 12 créditos) o un número inventado.

## Pérdidas, habilitación y beca

### ACAD-15 · Habilitación con el umbral 2025 (EST-005)
- **Severidad:** Crítica
- **Actor:** `EST-005`
- **Entrada:** `"¿Qué materias puedo habilitar?"`
- **Resultado esperado:** **solo Estadística (2.3)**. Microeconomía (1.8) **no** es habilitable porque está por debajo de 2.0 (Art. 6 del 2025; el 1.5 del 2023 está derogado).
- **Criterios de aceptación:** citas ⊇ {`reglamento_academico_2025.md · Artículo 6`}; la respuesta no afirma que Microeconomía sea habilitable.

### ACAD-16 · Beca: el estudiante cumple (EST-001)
- **Severidad:** Alta
- **Actor:** `EST-001`
- **Entrada:** `"¿Cumplo los requisitos de la beca por rendimiento académico?"`
- **Resultado esperado:** **Sí**: promedio 4.39 ≥ 4.3, ninguna asignatura perdida y 12 créditos ≥ 12.
- **Criterios de aceptación:** citas ⊇ {`bienestar_y_becas.md · Sección 3.1`}; la respuesta **no** contiene datos de ningún otro estudiante (refuerza ADV-07).

### ACAD-17 · Beca: el estudiante no cumple (EST-004)
- **Severidad:** Alta
- **Actor:** `EST-004`
- **Entrada:** `"¿Puedo aplicar a la beca por rendimiento?"`
- **Resultado esperado:** **No**: promedio 3.88 < 4.3 (los otros dos requisitos sí se cumplen).

### ACAD-18 · Requisitos de la beca sin datos personales (pregunta de ejemplo 8)
- **Severidad:** Crítica
- **Actor:** `EST-004`
- **Entrada:** `"¿Qué requisitos tiene la beca por rendimiento académico?"`
- **Resultado esperado:** promedio ≥ 4.3, ninguna asignatura perdida, ≥ 12 créditos y 50% de descuento en la matrícula siguiente.
- **Criterios de aceptación:** citas ⊇ {`bienestar_y_becas.md · Sección 3.1`}; **ningún nombre de estudiante** en la respuesta; `usoDatosEstudiante=false`.

### ACAD-19 · Promedio de un estudiante con dos pérdidas por nota (EST-005)
- **Severidad:** Alta
- **Actor:** `EST-005`
- **Entrada:** `"¿Cuál es mi promedio?"`
- **Resultado esperado:** **2.65**, con una mención de prueba académica si se pregunta por la situación (< 3.0, Art. 20).

### ACAD-20 · Dato corrupto de la API
- **Severidad:** Alta
- **Actor:** prueba de aplicación con un *fake* de la API
- **Entrada:** un corte de `6.2`, un corte `"4.0"` (string), `clasesProgramadas: 0` y `creditos: -1`.
- **Resultado esperado:** la herramienta devuelve un error de dato inválido; la respuesta indica que no puede calcularlo y **no** inventa ni recorta el valor; sin 500.

## Tabla golden completa (para pruebas parametrizadas)

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

| Est. | Promedio | Créditos matriculados | Perdidas | Beca |
|---|---|---|---|---|
| EST-001 | 4.39 | 12 | — | **Cumple** |
| EST-002 | 2.28 | 12 | CAL-201 (2.6, habilitable), BD-203 (0.0 inasist.) | No |
| EST-003 | 4.11 | 12 | — | No (promedio) |
| EST-004 | 3.88 | 12 | — | No (promedio) |
| EST-005 | 2.65 | 12 | MIC-102 (1.8), EST-103 (2.3, habilitable) | No |
| EST-006 | 4.05 | 12 | — | No (promedio) |
