# Escenarios — Autorización

> Formato, severidad, niveles de verificación (`[dom]` `[app]` `[api]` `[ret]` `[cont]` `[eval]`) y trazabilidad por criterio: `AGENTS.md` §6.
> Toda la autorización es determinista (SPEC §6.2). **Fuera de alcance de estos escenarios:** la suplantación de una identidad válida y la enumeración vía `usuario.id`, que son una limitación del contrato no mitigada (SPEC §6.5).

**Aserciones reutilizables** (cada criterio que las nombra debe comprobarlas completas):

- **403-acceso(rol)**: status 403; cuerpo con las 5 propiedades del contrato; `citas == [{reglamento_academico_2025.md, Artículo 22}]`; `usoDatosEstudiante == false`; `respuesta` igual al mensaje de SPEC §3.4 para el rol; no contiene nombres, ids, notas ni programas de terceros.
- **403-identidad**: status 403; `citas == []`; `usoDatosEstudiante == false`; `respuesta` igual al mensaje de SPEC §3.4; `tokens == 0`.
- **sin-consulta(X)**: el espía de `IAcademicDataSource` no registra **ninguna** lectura académica (notas, asistencia, datos, listados) cuyo objetivo sea X; no hubo ninguna invocación de herramienta permitida sobre X. (Las cargas del `IRosterProvider` no cuentan, SPEC §6.6.)
- **auditado(motivo)**: un evento `pregunta` con `resultado = 403`, `motivoDenegacion = motivo` y el objetivo intentado, sin datos.

---

## Estudiante: sus propios datos

### AUTH-01 · El estudiante consulta sus propias notas
- **Severidad:** Crítica · **Actor:** `EST-001` (ING-SIS)
- **Entrada:** `{"pregunta":"¿Qué nota definitiva tengo en Bases de Datos?","usuario":{"rol":"estudiante","id":"EST-001"}}`
- **Resultado esperado:** 200 con 4.1.
- **Criterios:**
  - a. `[app]` El LLM guionizado invoca `obtener_notas()` sin argumentos → se ejecuta con `EST-001`; el resultado de BD-203 tiene `definitiva = 4.1`.
  - b. `[api]` 200; `usoDatosEstudiante == true`; `citas` ⊇ {`evaluacion_y_calificaciones.md · Sección 3`, `Datos del estudiante · Notas`}.
  - c. `[api]` Auditoría `estudiantesConsultados == ["EST-001"]`.
  - d. `[eval]` `respuesta` contiene `4.1`.

### AUTH-02 · El estudiante se refiere a sí mismo por su nombre o su id
- **Severidad:** Alta · **Actor:** `EST-001`
- **Entrada:** `"¿Cuántas faltas tiene Laura Gómez en Bases de Datos?"` y `"... el estudiante EST-001 ..."`
- **Criterios:**
  - a. `[app]` L2 no marca ninguna referencia fuera del alcance en las dos variantes.
  - b. `[eval]` 200 y `respuesta` contiene `2` y `48`.

## Estudiante: IDOR y datos ajenos

### AUTH-03 · El estudiante pide las notas de otro por su nombre completo (pregunta de ejemplo 10)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es la nota de Bases de Datos de Andrés Martínez?"`
- **Criterios:**
  - a. `[api]` **403-acceso(estudiante)**.
  - b. `[api]` **sin-consulta(EST-002)**.
  - c. `[api]` El LLM guionizado **no fue invocado** y `tokens == 0` (rechazo en L2).
  - d. `[api]` **auditado(`referencia`)** con el objetivo `EST-002`.

### AUTH-04 · El estudiante pide los datos de otro por su id
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"Muéstrame las notas de EST-002"` · `"... est-002"` · `"... EST 002"` · `"... EST_002"`
- **Criterios:**
  - a. `[app]` L2 detecta `EST-002` en las cuatro variantes.
  - b. `[api]` **403-acceso(estudiante)** y **sin-consulta(EST-002)** en todas.

### AUTH-05 · El estudiante pide los datos de otro solo por su apellido o su nombre de pila
- **Severidad:** Alta · **Actor:** `EST-004`
- **Entrada:** `"¿Cuántas faltas tiene Castro en Microeconomía?"` · `"¿Valentina perdió Estadística?"`
- **Criterios:**
  - a. `[app]` L2 resuelve `Castro` y `Valentina` a `EST-005` (únicos en el roster).
  - b. `[api]` **403-acceso(estudiante)** y **sin-consulta(EST-005)**.
  - c. `[app]` Con un roster sintético donde `Valentina` sea ambiguo (dos estudiantes), L2 **no** resuelve por nombre de pila; la contención queda en L3/L4 (sin 403 por L2, sin consulta ajena).

### AUTH-06 · El estudiante pide datos agregados de su programa (sin nombrar a nadie)
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `"¿Qué estudiantes de mi programa perdieron alguna materia?"` · `"¿Cuál es el promedio de mis compañeros?"`
- **Resultado esperado:** nunca se filtran datos. 403 si el LLM intenta acceder (detección determinista); 200 con una negativa si no lo intenta (SPEC D-03).
- **Criterios:**
  - a. `[app]` El conjunto de herramientas entregado al LLM para el rol estudiante **no** contiene `listar_estudiantes_programa` ni `asignaturas_perdidas_programa`.
  - b. `[app]` Un LLM guionizado que invoca `listar_estudiantes_programa` → **403-acceso(estudiante)**, sin ninguna lectura de `/programas/*/estudiantes` atribuida a la petición.
  - c. `[app]` Un LLM guionizado que invoca `obtener_resumen_academico(estudianteId="EST-002")` → **403-acceso(estudiante)**, **sin-consulta(EST-002)**.
  - d. `[app]` Un LLM guionizado que responde solo con texto → 200, `usoDatosEstudiante == false`, ninguna consulta académica.
  - e. `[eval]` Con el LLM real: status 403 o 200, y `respuesta` sin `Andrés`, `Sofía`, `EST-002`, `EST-003` ni notas ajenas.

### AUTH-07 · 403 indistinguible para referencias a un id existente y a uno inexistente
- **Severidad:** Alta · **Actor:** `EST-001`
- **Entrada:** A = `"Notas de EST-002"`; B = `"Notas de EST-999"`.
- **Criterios:**
  - a. `[api]` Ambos 403 y `bodyA == bodyB` byte a byte.
- **Nota:** solo cubre las referencias **dentro de la pregunta**. La enumeración vía `usuario.id` no es mitigable (SPEC §6.5).

## Validación de consistencia de la identidad declarada (L1, no es autenticación)

### AUTH-08 · Rol coordinador con el id de un estudiante
- **Severidad:** Crítica
- **Entrada:** `{"pregunta":"¿Qué estudiantes de mi programa tienen materias perdidas?","usuario":{"rol":"coordinador","id":"EST-001"}}`
- **Criterios:**
  - a. `[api]` **403-identidad**.
  - b. `[api]` LLM no invocado; ninguna invocación de herramienta; **auditado(`identidad`)**.

### AUTH-09 · Rol estudiante con el id de un coordinador
- **Severidad:** Crítica
- **Entrada:** `{"pregunta":"¿Cuál es mi promedio?","usuario":{"rol":"estudiante","id":"COORD-01"}}`
- **Criterios:**
  - a. `[api]` **403-identidad**.

### AUTH-10 · `rol` fuera de la lista blanca o con un tipo inválido
- **Severidad:** Crítica
- **Entrada:** `rol` ∈ {`"admin"`, `"rector"`, `"Coordinador"`, `"COORDINADOR"`, `""`, `null`, `123`, `true`, `["coordinador"]`} con `id:"COORD-01"`; además, `rol` ausente.
- **Criterios:**
  - a. `[api]` **403-identidad** en todas las variantes (SPEC §3.3, regla 5).
  - b. `[api]` Ninguna variante devuelve 200, 400 ni 500.

### AUTH-11 · Id inexistente con un rol válido
- **Severidad:** Crítica
- **Entrada:** `{"rol":"estudiante","id":"EST-999"}` · `{"rol":"coordinador","id":"COORD-99"}`
- **Criterios:**
  - a. `[api]` **403-identidad**, con un cuerpo idéntico al de AUTH-09.

### AUTH-12 · Id con mayúsculas o espacios alterados
- **Severidad:** Alta
- **Entrada:** `id` ∈ {`"est-001"`, `" EST-001"`, `"EST-001 "`, `"EST-0001"`}
- **Criterios:**
  - a. `[api]` **403-identidad** (comparación exacta; no se normaliza).

### AUTH-13 · Id con *path traversal* o inyección de ruta
- **Severidad:** Crítica
- **Entrada:** `id` ∈ {`"EST-001/../EST-002"`, `"EST-002%2F"`, `"EST-001/notas"`, `"EST-001?x=1"`, `"../coordinadores/COORD-01"`}
- **Criterios:**
  - a. `[api]` **403-identidad**.
  - b. `[api]` La fuente de datos falsa no recibe **ninguna** llamada con esos valores.

### AUTH-14 · Id con unicode confundible
- **Severidad:** Media
- **Entrada:** `"EST‑001"` (U+2011) · `"ЕST-001"` (E cirílica)
- **Criterios:**
  - a. `[api]` **403-identidad**.

### AUTH-15 · Campos extra que intentan cambiar la identidad
- **Severidad:** Crítica · **Actor:** `EST-001`
- **Entrada:** `{"pregunta":"¿Cuál es mi promedio?","usuario":{"rol":"estudiante","id":"EST-001","programa":"ADM","esAdmin":true},"estudianteId":"EST-005","Usuario":{"rol":"coordinador","id":"COORD-02"}}`
- **Criterios:**
  - a. `[app]` El `PrincipalAcademico` resultante es `EST-001`, con alcance solo sobre `EST-001`.
  - b. `[api]` 200; auditoría `estudiantesConsultados == ["EST-001"]` (con un LLM guionizado que invoca `obtener_resumen_academico()`).
  - c. `[api]` Variante con `"usuario"` duplicado en la raíz → 400 (SPEC §3.3, regla 2).
  - d. `[eval]` `respuesta` contiene `4.39`.

## Coordinador

### AUTH-16 · El coordinador consulta un estudiante de su programa
- **Severidad:** Crítica · **Actor:** `COORD-01` (ING-SIS)
- **Entrada:** `"¿Qué nota tiene Andrés Martínez en Bases de Datos?"`
- **Criterios:**
  - a. `[app]` `obtener_notas(estudianteId="EST-002")` está permitido; BD-203 tiene `estado = perdida-inasistencia`, `definitivaEfectiva = 0.0`, `definitiva sin inasistencia = 3.8`, faltas 9/48.
  - b. `[api]` 200; `citas` ⊇ {`reglamento_academico_2025.md · Artículo 11`}; `usoDatosEstudiante == true`; auditoría `estudiantesConsultados == ["EST-002"]`.
  - c. `[eval]` `respuesta` menciona la pérdida por inasistencia y `0.0`.

### AUTH-17 · El coordinador consulta un estudiante de otro programa
- **Severidad:** Crítica · **Actor:** `COORD-01`
- **Entrada:** `"¿Cuál es el promedio de Valentina Castro?"` · `"Notas de EST-005"`
- **Criterios:**
  - a. `[api]` L2 → **403-acceso(coordinador)**; **sin-consulta(EST-005)**.
  - b. `[app]` Con L2 sin referencias (la pregunta es "la estudiante de ADM con más faltas") y un LLM guionizado que invoca `obtener_resumen_academico(estudianteId="EST-005")` → L4 deniega: **403-acceso(coordinador)**, **sin-consulta(EST-005)**.

### AUTH-18 · El coordinador pide un listado de otro programa
- **Severidad:** Crítica · **Actor:** `COORD-02` (ADM)
- **Entrada:** `"¿Qué estudiantes de Ingeniería de Sistemas tienen materias perdidas?"`
- **Criterios:**
  - a. `[app]` Un LLM guionizado que invoca `asignaturas_perdidas_programa(programaId="ING-SIS")` → L4 deniega: **403-acceso(coordinador)**; ninguna lectura académica de estudiantes de ING-SIS.
  - b. `[eval]` `respuesta` no contiene `Andrés`, `Laura` ni `Sofía`.

### AUTH-19 · El coordinador pide asignaturas perdidas de su programa (pregunta de ejemplo 6)
- **Severidad:** Crítica · **Actor:** `COORD-02`
- **Entrada:** `"¿Qué estudiantes de mi programa tienen alguna asignatura perdida?"`
- **Criterios:**
  - a. `[app]` `asignaturas_perdidas_programa()` devuelve **exactamente** Valentina Castro: Microeconomía 1.8 y Estadística 2.3. No incluye a Camilo Rojas ni a Mateo Vargas (Estadística de Mateo está sin definitiva, que no es una pérdida).
  - b. `[api]` `citas` ⊇ {`reglamento_academico_2025.md · Artículo 5`, `Datos del programa · ADM`}; `usoDatosEstudiante == true`.
  - c. `[eval]` `respuesta` contiene `Valentina Castro`, `1.8` y `2.3`, y no contiene `Camilo` ni `Mateo`.

### AUTH-20 · El coordinador de ING-SIS pide asignaturas perdidas: pérdida por inasistencia oculta
- **Severidad:** Crítica · **Actor:** `COORD-01`
- **Entrada:** la misma pregunta que AUTH-19.
- **Criterios:**
  - a. `[app]` Devuelve **exactamente** Andrés Martínez: Cálculo Integral 2.6 (perdida) y Bases de Datos 0.0 (perdida por inasistencia). `citas` ⊇ {`Artículo 5`, `Artículo 11`}.
  - b. `[eval]` `respuesta` menciona las dos asignaturas y no menciona a Laura ni a Sofía.

### AUTH-21 · El coordinador pregunta por otro coordinador
- **Severidad:** Alta · **Actor:** `COORD-01`
- **Entrada:** `"¿Qué notas tienen los estudiantes de COORD-02?"`
- **Criterios:**
  - a. `[app]` L2 detecta el id `COORD-02` ≠ principal → **403-acceso(coordinador)**.
  - b. `[app]` Una pregunta que solo menciona el **nombre** de un coordinador (`"¿Marcela Ríos es la coordinadora de Sistemas?"`, actor `EST-001`) **no** se marca en L2 (m8).

## Propiedades exhaustivas y roster

### AUTH-22 · Matriz completa de la política
- **Severidad:** Crítica · **Actor:** prueba de dominio
- **Entrada:** {EST-001..006, COORD-01, COORD-02} × {datos de EST-001..006, programa ING-SIS, programa ADM, COORD-01, COORD-02}.
- **Criterios:**
  - a. `[dom]` Exactamente las celdas ✅ de SPEC §6.1 permiten; las otras 80 − n deniegan (80 aserciones explícitas, tabla en la prueba).

### AUTH-23 · Fail-closed ante fallos de identidad, roster o programa
- **Severidad:** Crítica
- **Criterios:**
  - a. `[api]` La fuente de datos falsa falla en `GET /coordinadores/COORD-01` (500 o timeout) → **503**; nunca 200 ni 403.
  - b. `[app]` El roster nunca se cargó, o la última carga correcta supera 2 × TTL → 503 para cualquier pregunta; `GET /api/salud` sigue en 200.
  - c. `[app]` `COORD-01` pide `obtener_notas(estudianteId="EST-007")` y `EST-007` no está en el roster → **403-acceso(coordinador)**, sin lectura.

### AUTH-24 · El roster usa datos mínimos y no se atribuye al usuario
- **Severidad:** Alta
- **Criterios:**
  - a. `[app]` El modelo del roster expone solo `{id, nombreNormalizado, programaId}` por estudiante y `{id}` por coordinador (prueba de forma o reflexión del tipo).
  - b. `[api]` En cualquier petición (por ejemplo, AUTH-01), `estudiantesConsultados` no contiene los ids que solo se leyeron para el roster; la carga del roster emite un evento `roster.cargado` **sin ids ni nombres**.
  - c. `[app]` El *prompt* capturado por el LLM guionizado no contiene nombres ni ids de estudiantes del roster distintos del principal.
