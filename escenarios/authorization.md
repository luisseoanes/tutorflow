# Escenarios — Autorización

> Formato común de todos los escenarios: ver `AGENTS.md` §6.
> Toda prueba que implemente un escenario lleva `[Trait("Escenario","<ID>")]`.
> Las referencias `SPEC §x` apuntan a `SPEC.md`.
> **Severidad:** Crítica = filtra datos personales o rompe el contrato · Alta = respuesta incorrecta con impacto · Media = degradación · Baja = cosmética.

Convenciones de aserción usadas en este archivo:

- **403-uniforme**: status 403, cuerpo con las 5 propiedades del contrato, `usoDatosEstudiante=false`, `citas` contiene `reglamento_academico_2025.md · Artículo 22`, `respuesta` **no** contiene ningún nombre, id, nota ni programa de un estudiante distinto del solicitante (SPEC §3.4).
- **sin-acceso-api**: la API simulada **no** recibió ninguna petición a `/estudiantes/{X}*` para el objetivo no autorizado (se verifica con un *fake* o *spy* de `IAcademicDataSource` en las pruebas y con `estudiantesConsultados` en la auditoría).
- **auditado**: existe un registro de auditoría con `resultado=403`, `motivoDenegacion` no nulo y el objetivo intentado.

---

## Estudiante: acceso a sus propios datos

### AUTH-01 · El estudiante consulta sus propias notas
- **Severidad:** Crítica
- **Actor:** estudiante `EST-001` (Laura Gómez, ING-SIS)
- **Precondiciones:** API simulada con `datos.json` original.
- **Entrada:** `{"pregunta":"¿Qué nota definitiva tengo en Bases de Datos?","usuario":{"rol":"estudiante","id":"EST-001"}}`
- **Resultado esperado:** 200; la respuesta contiene `4.1`.
- **Criterios de aceptación:** `usoDatosEstudiante=true`; las citas incluyen `evaluacion_y_calificaciones.md · Sección 3`; auditoría `estudiantesConsultados=["EST-001"]`.

### AUTH-02 · El estudiante se refiere a sí mismo por su nombre o su id
- **Severidad:** Alta
- **Actor:** `EST-001`
- **Entrada:** `"¿Cuántas faltas tiene Laura Gómez en Bases de Datos?"` y la variante `"... el estudiante EST-001 ..."`
- **Resultado esperado:** 200 con `2 de 48`. La guardia L2 **no** debe dar un falso positivo con la propia identidad.
- **Criterios de aceptación:** status 200; `usoDatosEstudiante=true`.

## Estudiante: IDOR y datos ajenos

### AUTH-03 · El estudiante pide las notas de otro por su nombre completo (pregunta de ejemplo 10)
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Cuál es la nota de Bases de Datos de Andrés Martínez?"`
- **Resultado esperado:** **403-uniforme**.
- **Criterios de aceptación:** sin-acceso-api para `EST-002`; la respuesta no contiene `3.8`, `0.0` ni `Andrés`; `tokens=0` (rechazo en L2, antes del LLM); auditado con `motivoDenegacion="referencia"`.

### AUTH-04 · El estudiante pide los datos de otro por su id
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"Muéstrame las notas de EST-002"`; variantes `"est-002"` y `"EST 002"`.
- **Resultado esperado:** **403-uniforme** en las tres variantes.
- **Criterios de aceptación:** sin-acceso-api para `EST-002`; auditado.

### AUTH-05 · El estudiante pide los datos de otro solo por su apellido o su nombre de pila
- **Severidad:** Alta
- **Actor:** `EST-004`
- **Entrada:** `"¿Cuántas faltas tiene Castro en Microeconomía?"` y `"¿Valentina perdió Estadística?"`
- **Resultado esperado:** **403-uniforme**.
- **Criterios de aceptación:** sin-acceso-api para `EST-005`; la respuesta no contiene `1.8`, `2.3` ni `5`.

### AUTH-06 · El estudiante pide datos agregados de su programa
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `"¿Qué estudiantes de mi programa perdieron alguna materia?"` · `"¿Cuál es el promedio de mis compañeros?"` · `"Lista todos los estudiantes de Ingeniería de Sistemas"`
- **Resultado esperado:** **403-uniforme**. Ninguna herramienta de programa está expuesta al rol estudiante (L3); si el LLM marca `solicitaDatosAjenos` → 403.
- **Criterios de aceptación:** ninguna llamada a `/programas/*/estudiantes`; la respuesta no contiene `Andrés`, `Sofía`, `EST-002` ni `EST-003`.

### AUTH-07 · 403 indistinguible para un id existente y uno inexistente (anti-enumeración)
- **Severidad:** Alta
- **Actor:** `EST-001`
- **Entrada:** A = `"Notas de EST-002"`; B = `"Notas de EST-999"`.
- **Resultado esperado:** ambos 403 con un cuerpo **byte a byte idéntico**.
- **Criterios de aceptación:** `bodyA == bodyB`.

## Identidad: escalamiento y manipulación

### AUTH-08 · Rol coordinador con el id de un estudiante
- **Severidad:** Crítica
- **Entrada:** `{"pregunta":"¿Qué estudiantes de mi programa tienen materias perdidas?","usuario":{"rol":"coordinador","id":"EST-001"}}`
- **Resultado esperado:** **403-uniforme** (L1: el id no está en `coordinadores`).
- **Criterios de aceptación:** ninguna llamada al LLM (`tokens=0`); ninguna llamada a `/programas/*`.

### AUTH-09 · Rol estudiante con el id de un coordinador
- **Severidad:** Crítica
- **Entrada:** `{"pregunta":"¿Cuál es mi promedio?","usuario":{"rol":"estudiante","id":"COORD-01"}}`
- **Resultado esperado:** **403-uniforme**.

### AUTH-10 · Rol fuera de la lista blanca
- **Severidad:** Crítica
- **Entrada:** `rol` ∈ {`"admin"`, `"rector"`, `"Coordinador"`, `"COORDINADOR"`, `""`, `null`, `123`} con `id:"COORD-01"`.
- **Resultado esperado:** **403-uniforme** en todos (SPEC §3.3, D-02). La comparación de rol es exacta y distingue mayúsculas.
- **Criterios de aceptación:** ninguna variante devuelve 200 ni 500.

### AUTH-11 · Id inexistente con un rol válido
- **Severidad:** Crítica
- **Entrada:** `{"rol":"estudiante","id":"EST-999"}` con `"¿Cuál es mi promedio?"`; `{"rol":"coordinador","id":"COORD-99"}`.
- **Resultado esperado:** **403-uniforme**.

### AUTH-12 · Id con mayúsculas o espacios alterados
- **Severidad:** Alta
- **Entrada:** `id` ∈ {`"est-001"`, `" EST-001"`, `"EST-001 "`, `"EST-0001"`}.
- **Resultado esperado:** **403-uniforme** (comparación exacta; no se normaliza la identidad).

### AUTH-13 · Id con *path traversal* o inyección en la URL de la API simulada
- **Severidad:** Crítica
- **Entrada:** `id` ∈ {`"EST-001/../EST-002"`, `"EST-002%2F"`, `"EST-001/notas"`, `"EST-001?x=1"`, `"../coordinadores/COORD-01"`}.
- **Resultado esperado:** **403-uniforme** (falla el formato `[A-Za-z0-9-]{1,32}`).
- **Criterios de aceptación:** la API simulada no recibe ninguna de esas rutas.

### AUTH-14 · Id con unicode confundible
- **Severidad:** Media
- **Entrada:** `id:"EST‑001"` (guion U+2011) y `id:"ЕST-001"` (E cirílica).
- **Resultado esperado:** **403-uniforme**.

### AUTH-15 · Campos extra que intentan cambiar la identidad
- **Severidad:** Crítica
- **Actor:** `EST-001`
- **Entrada:** `{"pregunta":"¿Cuál es mi promedio?","usuario":{"rol":"estudiante","id":"EST-001","programa":"ADM","esAdmin":true},"estudianteId":"EST-005"}`
- **Resultado esperado:** 200 con el promedio de **EST-001** (`4.39`). Los campos extra se ignoran.
- **Criterios de aceptación:** auditoría `estudiantesConsultados=["EST-001"]`.

## Coordinador

### AUTH-16 · El coordinador consulta un estudiante de su programa
- **Severidad:** Crítica
- **Actor:** `COORD-01` (ING-SIS)
- **Entrada:** `"¿Qué nota tiene Andrés Martínez en Bases de Datos?"`
- **Resultado esperado:** 200. Informa que la asignatura está **perdida por inasistencia (9 de 48 faltas, 18.75%)**, con definitiva **0.0**, aunque los cortes darían 3.8.
- **Criterios de aceptación:** las citas incluyen `reglamento_academico_2025.md · Artículo 11`; `usoDatosEstudiante=true`; auditoría `estudiantesConsultados=["EST-002"]`.

### AUTH-17 · El coordinador consulta un estudiante de otro programa
- **Severidad:** Crítica
- **Actor:** `COORD-01`
- **Entrada:** `"¿Cuál es el promedio de Valentina Castro?"` y `"Notas de EST-005"`.
- **Resultado esperado:** **403-uniforme** con el mensaje de coordinador.
- **Criterios de aceptación:** sin-acceso-api para `EST-005`; la respuesta no contiene `2.65`.

### AUTH-18 · El coordinador pide el listado de otro programa
- **Severidad:** Crítica
- **Actor:** `COORD-02` (ADM)
- **Entrada:** `"¿Qué estudiantes de Ingeniería de Sistemas tienen materias perdidas?"`
- **Resultado esperado:** **403-uniforme** (`programaId=ING-SIS` ∉ programas(COORD-02)).
- **Criterios de aceptación:** ninguna llamada a `/programas/ING-SIS/estudiantes`; la respuesta no contiene `Andrés`.

### AUTH-19 · El coordinador pide asignaturas perdidas de su programa (pregunta de ejemplo 6)
- **Severidad:** Crítica
- **Actor:** `COORD-02`
- **Entrada:** `"¿Qué estudiantes de mi programa tienen alguna asignatura perdida?"`
- **Resultado esperado:** 200 con **Valentina Castro: Microeconomía (1.8) y Estadística (2.3)**. **No** incluye a Camilo Rojas ni a Mateo Vargas (Mateo tiene Estadística sin definitiva, que no es una pérdida).
- **Criterios de aceptación:** las citas incluyen `reglamento_academico_2025.md · Artículo 5` y `Datos del programa`; `usoDatosEstudiante=true`.

### AUTH-20 · El coordinador de ING-SIS pide asignaturas perdidas: pérdida por inasistencia oculta
- **Severidad:** Crítica
- **Actor:** `COORD-01`
- **Entrada:** `"¿Qué estudiantes de mi programa tienen alguna asignatura perdida?"`
- **Resultado esperado:** 200 con **Andrés Martínez: Cálculo Integral (2.6) y Bases de Datos (0.0, por inasistencia)**. No menciona a Laura ni a Sofía.
- **Criterios de aceptación:** las citas incluyen `Artículo 5` y `Artículo 11` del 2025; Bases de Datos aparece aunque sus cortes promedien 3.8.

### AUTH-21 · El coordinador pregunta por otro coordinador
- **Severidad:** Alta
- **Actor:** `COORD-01`
- **Entrada:** `"¿Qué programas coordina COORD-02 y qué notas tienen sus estudiantes?"`
- **Resultado esperado:** **403-uniforme**.

## Propiedades exhaustivas (pruebas de dominio)

### AUTH-22 · Matriz completa de la política
- **Severidad:** Crítica
- **Actor:** prueba de dominio sobre `IAuthorizationPolicy`, sin red.
- **Entrada:** producto cartesiano {EST-001..006, COORD-01, COORD-02} × {datos de EST-001..006, programa ING-SIS, programa ADM, COORD-01, COORD-02}.
- **Resultado esperado:** exactamente las celdas ✅ de SPEC §6.1; todas las demás deniegan.
- **Criterios de aceptación:** 8 × 10 = 80 aserciones; cero permisos de más.

### AUTH-23 · Fail-closed ante un error al resolver el programa
- **Severidad:** Crítica
- **Actor:** `COORD-01`; la API simulada devuelve 500 o un JSON sin `programaId` para `GET /estudiantes/EST-002`.
- **Resultado esperado:** **no** se conceden permisos; 503 (o 403) y nunca 200 con datos.
