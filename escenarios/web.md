# Escenarios — Interfaz web (TutorFlow Web)

> Especificación: `SPEC-WEB.md`. Formato y severidad: `AGENTS.md` §6. Niveles propios del frontend (`AGENTS.md` §6.3):
> - `[web]` — prueba automatizada del frontend (Vitest + Testing Library, `tsc --noEmit`, `vite build`, axe en jsdom). Bloquea la CI.
> - `[e2e]` — Playwright contra la app servida (rutas de API **simuladas** para 200/500/red, o API **real sin llave** para salud/400/403/503). Bloquea la CI. Nunca consume cuota del LLM.
> - `[qa]` — verificación del **revisor independiente** (agente QA, `AGENTS.md` §13): levanta la app por su cuenta, ejecuta sus propias comprobaciones y adjunta evidencia (capturas, salidas). No es un job de CI: lo exige la compuerta G9 (`FABRICA.md` §5.1) mediante un informe APPROVED.
> Prioridad P2 (SPEC-WEB, INT-015): ningún criterio de este archivo puede bloquear tareas P0/P1.

---

### WEB-01 · Contrato intacto y mismo origen
- **Severidad:** Crítica
- **Criterios:**
  - a. `[web]` El cliente HTTP solo llama a `GET /api/salud` y `POST /api/asistente/preguntar` con el cuerpo `{pregunta, usuario:{rol,id}}` exacto de SPEC §3.1 (prueba del servicio con `fetch` simulado: método, ruta, cabeceras, cuerpo).
  - b. `[e2e]` Con la app servida por el servicio `web`, las peticiones salen a `/api/*` del mismo origen (sin URL absoluta a otro host) y la API responde sin cambios de CORS.
  - c. `[qa]` El revisor confirma con `git diff` que la rama no modifica `src/` del backend ni el contrato.

### WEB-02 · Identidad declarada, no autenticación
- **Severidad:** Crítica
- **Criterios:**
  - a. `[web]` La vista muestra el texto "Identidad declarada — no es una autenticación" junto al selector de rol y el campo id; no existe pantalla, botón ni texto de "iniciar sesión".
  - b. `[web]` El selector solo ofrece `estudiante` y `coordinador` (valores exactos del contrato) y el id se envía tal cual se escribió (sin normalizar mayúsculas ni espacios).

### WEB-03 · Sin permisos ni cálculos académicos en el cliente
- **Severidad:** Crítica
- **Criterios:**
  - a. `[web]` Prueba de arquitectura del frontend: ningún archivo de `web/src` contiene lógica de promedio, ponderación (`0.3`, `0.4`), redondeo académico, umbrales (`15`, `3.0`) ni decisiones de acceso por rol; la UI no oculta ni muestra datos según el rol.
  - b. `[qa]` El revisor busca en el bundle y en `web/src` cálculos o reglas de permiso y lo documenta.

### WEB-04 · Sin almacenamiento persistente ni secretos
- **Severidad:** Crítica
- **Criterios:**
  - a. `[web]` Ningún archivo de `web/src` usa `localStorage`, `sessionStorage`, `indexedDB`, `document.cookie` ni registra service workers.
  - b. `[e2e]` Tras una conversación completa, el almacenamiento del navegador (local, session, IndexedDB, cookies, Cache Storage) está vacío.
  - c. `[web]` El bundle de producción no contiene `LLM_API_KEY`, `EMBEDDING_API_KEY`, `test-key-NOT-REAL` ni variables `VITE_*` con secretos.

### WEB-05 · Respuesta 200 completa
- **Severidad:** Crítica
- **Criterios:**
  - a. `[web]` Con un 200 simulado: se muestran la `respuesta` (párrafos y listas), cada cita como `documento · seccion`, "Usó datos del estudiante: sí/no", `tokens` y `costoUsd` con el valor exacto devuelto (p. ej. `0.00395` → `US$ 0.00395`, sin recálculo).
  - b. `[web]` La respuesta se renderiza como texto seguro: un 200 cuya `respuesta` contiene `<img src=x onerror=alert(1)>` se muestra literal y no crea elementos.
  - c. `[e2e]` El foco se mueve a la nueva respuesta y la región `aria-live` la anuncia.

### WEB-06 · Citas: ninguna, varias, expansión
- **Severidad:** Alta
- **Criterios:**
  - a. `[web]` `citas: []` → texto "Sin citas"; tres citas → tres elementos en orden de la API, sin inventar contenido.
  - b. `[web]` Cada cita es expandible con ratón y teclado (Enter/Espacio), con `aria-expanded` correcto, y solo muestra `documento` y `seccion`.

### WEB-07 · Estados de error HTTP
- **Severidad:** Crítica
- **Criterios:**
  - a. `[web]` 400 `problem+json` → alerta de validación con el `title` y guía; nunca el cuerpo crudo.
  - b. `[web]` 403 con cita Art. 22 → alerta "acceso denegado" con la `respuesta` del contrato; 403 sin citas → alerta "identidad no válida". (Distinción solo por el cuerpo recibido, sin lógica de permisos.)
  - c. `[web]` 503 → alerta de servicio no disponible con reintento; 500 → alerta genérica con el `X-Request-Id` recibido; sin stack traces ni detalles internos.
  - d. `[web]` Error de red y timeout del cliente → alerta "Sin conexión" con reintento; respuesta con forma inesperada (falla la validación en tiempo de ejecución) → "Respuesta no reconocida" sin mostrar el cuerpo.
  - e. `[e2e]` Contra la API real **sin llave**: AUTH-03 → la UI muestra el 403-acceso; `rol` válido + id inexistente → 403-identidad; pregunta de 1001 caracteres forzada (saltando la UX) → 400; pregunta normativa → 503 (LLM no disponible).

### WEB-08 · Estados de envío y formulario
- **Severidad:** Alta
- **Criterios:**
  - a. `[web]` Estado inicial con bienvenida y 3 ejemplos que solo rellenan el campo (no envían).
  - b. `[web]` Botón deshabilitado con pregunta vacía o en envío; durante el envío el campo se deshabilita y el indicador de carga es anunciado; contador de caracteres con límite 1000.
  - c. `[web]` Enter envía, Shift+Enter inserta nueva línea.

### WEB-09 · Estado de la API
- **Severidad:** Media
- **Criterios:**
  - a. `[web]` `GET /api/salud` 200 `{"estado":"ok"}` → "Disponible"; error de red → "Sin conexión" (texto + color + icono, no solo color); sondeo cada 30 s, pausado con la pestaña oculta.

### WEB-10 · Sin memoria conversacional
- **Severidad:** Alta
- **Criterios:**
  - a. `[web]` Dos envíos seguidos producen dos peticiones cuyo cuerpo contiene solo la pregunta actual y el usuario actual (nunca turnos previos).

### WEB-11 · Responsive
- **Severidad:** Alta
- **Criterios:**
  - a. `[e2e]` En 375×812, 768×1024 y 1440×900: sin desbordamiento horizontal (`scrollWidth <= clientWidth` del documento) en los estados inicial, 200 con varias citas, 403 y 503.
  - b. `[qa]` Capturas del revisor en los tres tamaños y en esos estados, adjuntas al informe; sin elementos solapados ni recortados; objetivos táctiles ≥ 44 px en móvil.

### WEB-12 · Accesibilidad
- **Severidad:** Alta
- **Criterios:**
  - a. `[web]` axe (jsdom) sin violaciones `serious`/`critical` en los estados inicial, cargando, 200, 400, 403, 503, 500 y sin conexión.
  - b. `[e2e]` axe en navegador real sin violaciones `serious`/`critical`; recorrido completo solo con teclado (rol → id → pregunta → enviar → expandir cita) con foco visible en cada paso.
  - c. `[web]` Con `prefers-reduced-motion: reduce` no hay transiciones.
  - d. `[qa]` El revisor verifica contraste, orden de foco y anuncios (`aria-live`/`role="alert"`) con su propia ejecución.

### WEB-13 · Consola y controles
- **Severidad:** Alta
- **Criterios:**
  - a. `[e2e]` Ningún `console.error`/`console.warn` atribuible a la app durante el recorrido completo de estados.
  - b. `[qa]` Todo control visible tiene una función verificable; no hay botones ni enlaces muertos.

### WEB-14 · Calidad técnica
- **Severidad:** Alta
- **Criterios:**
  - a. `[web]` `tsc --noEmit` con `strict: true` y `noUncheckedIndexedAccess` sin errores; ningún `any` explícito en `web/src`.
  - b. `[web]` `vite build` de producción sin errores.
  - c. `[qa]` Las dependencias de `web/package.json` ⊆ lista permitida de SPEC-WEB §8.3, o cada extra tiene justificación aprobada.

### WEB-15 · Sistema de diseño centralizado
- **Severidad:** Media
- **Criterios:**
  - a. `[web]` Ningún archivo de `web/src` fuera del módulo de tokens contiene literales de color (`#hex`, `rgb(`) ni tamaños en px arbitrarios en estilos (lista de excepciones vacía o justificada).
  - b. `[qa]` Consistencia visual (tipografía, espaciado, radios, color de acento con moderación, sin gradientes ni emojis) frente a SPEC-WEB §3 en todas las capturas.

### WEB-16 · Despliegue y cabeceras
- **Severidad:** Alta
- **Criterios:**
  - a. `[e2e]` El servicio `web` en compose sirve la app en `:3000`, hace de proxy de `/api/*` a `tutorflow:8080` y devuelve `Content-Security-Policy` (`default-src 'self'`, sin `unsafe-inline` en `script-src`), `X-Content-Type-Options: nosniff` y `Referrer-Policy: no-referrer`.
  - b. `[e2e]` La fuente Inter se sirve desde el mismo origen (ninguna petición a dominios externos durante la carga).

### WEB-17 · Revisión independiente
- **Severidad:** Crítica
- **Criterios:**
  - a. `[qa]` Existe `qa/revisiones/T-13-r<n>.md` con resultado **APPROVED** sobre el commit exacto que se integra, en el formato de `AGENTS.md` §13, con pruebas ejecutadas por el revisor y evidencias; sin hallazgos críticos ni altos abiertos.
  - b. `[qa]` Los hallazgos medios abiertos están corregidos o aceptados explícitamente por el humano en `INTERVENCIONES.md`.
