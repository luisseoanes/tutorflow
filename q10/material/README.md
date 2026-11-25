# TutorFlow · Material de la prueba técnica (Q10)

Material para construir TutorFlow. Las instrucciones completas están en el PDF de la prueba.

Todo es ficticio: la institución, las personas y los datos.

## Contenido

| Carpeta o archivo | Qué es |
| --- | --- |
| `reglamento/` | 4 documentos de la institución en Markdown. Son la base de conocimiento del asistente. |
| `mock-api/` | API simulada con estudiantes, notas y asistencia. |
| `preguntas_ejemplo.md` y `.json` | 10 preguntas con la respuesta y la cita que esperamos. |

## API simulada

Requiere Node 18 o superior, sin dependencias:

```bash
cd mock-api
node server.js          # http://localhost:4000  (cambia el puerto con PORT=xxxx)
```

| Método y ruta | Devuelve |
| --- | --- |
| `GET /salud` | Estado de la API |
| `GET /programas` | Programas académicos |
| `GET /programas/{id}/estudiantes` | Estudiantes de un programa |
| `GET /coordinadores/{id}` | Coordinador y los programas a su cargo |
| `GET /estudiantes/{id}` | Datos básicos del estudiante |
| `GET /estudiantes/{id}/notas` | Notas por corte de cada asignatura (sin definitiva) |
| `GET /estudiantes/{id}/asistencia` | Clases programadas e inasistencias por asignatura |

La API **no valida permisos**: devuelve lo que le pidan. Controlar quién puede ver qué es responsabilidad de tu aplicación.

La API entrega las notas por corte. Las definitivas, los promedios y la pérdida por fallas se calculan con las reglas de los documentos.

## Usuarios de prueba

| Id | Rol | Programa |
| --- | --- | --- |
| EST-001 a EST-003 | Estudiante | Ingeniería de Sistemas (ING-SIS) |
| EST-004 a EST-006 | Estudiante | Administración de Empresas (ADM) |
| COORD-01 | Coordinador | ING-SIS |
| COORD-02 | Coordinador | ADM |
