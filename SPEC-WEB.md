# SPEC-WEB — TutorFlow Web (React + TypeScript)

> Especificación de producto, diseño y aceptación de la interfaz web de TutorFlow.
> Complementa a `SPEC.md` (que sigue siendo la fuente de verdad del contrato, la seguridad y las reglas académicas) y **no la contradice**: si hay conflicto, gana `SPEC.md` y se escala.
> Autorizada en INT-015. Prioridad **P2**: no puede retrasar ni bloquear T-08, T-10, T-11 ni ningún criterio P0/P1.
>
> Versión: 1.0 · 2026-11-25 · Criterios verificables en `escenarios/web.md`.

---

## 1. Visión

Interfaz de calidad de producto SaaS comercial, no una demo ni un formulario administrativo. Referencia visual: la sobriedad de Linear, Stripe y Vercel — minimalismo, fondo blanco, jerarquía tipográfica, precisión, espacios generosos y detalles discretos.

## 2. Restricciones de producto (no negociables)

| Id | Restricción | Origen |
|---|---|---|
| W-R1 | La API es la **única** fuente de verdad. La web solo consume `GET /api/salud` y `POST /api/asistente/preguntar` tal como están en SPEC §3. **No se modifica el contrato HTTP.** | SPEC §3 |
| W-R2 | **Sin autenticación.** El rol y el id se presentan explícitamente como **identidad declarada** (texto visible: "Identidad declarada — no es una autenticación"), coherente con SPEC §6.5. No se simula un login. | SPEC §6.5 |
| W-R3 | **Sin permisos ni cálculos en el navegador**: ni filtros por rol, ni promedios, ni redondeos, ni decisiones de 403. Lo que muestra la web es lo que devolvió la API. | SPEC §5, §6 |
| W-R4 | **Sin almacenamiento persistente** de preguntas, ids, respuestas o citas: prohibidos `localStorage`, `sessionStorage`, IndexedDB, cookies propias y cachés de service worker. El historial visible vive solo en memoria de la pestaña. | SPEC §9 (minimización) |
| W-R5 | **Sin memoria conversacional**: cada envío es una petición independiente; el historial en pantalla es solo visual y no se reenvía a la API. | SPEC §0.2 |
| W-R6 | **Sin secretos** en el bundle ni en variables `VITE_*` (no hay ninguno que poner: la web no habla con el LLM). | SPEC §10.5 |
| W-R7 | **Sin fuentes inventadas**: las citas se muestran exactamente como `{documento, seccion}` de la respuesta; la web no genera enlaces, extractos ni textos de documentos que la API no devolvió. | SPEC §7.5 |
| W-R8 | **Sin dependencias injustificadas** (§8.3). Sin CDNs ni recursos externos en tiempo de ejecución (fuentes autoalojadas). | AGENTS §4 |

## 3. Sistema de diseño (tokens normativos)

Todos los valores viven en **un único módulo de tokens** (CSS custom properties generadas desde un objeto TypeScript tipado o un archivo `tokens.css` + su espejo tipado). Prohibidos los valores arbitrarios dispersos (colores hex, px sueltos) fuera de ese módulo; lo verifica WEB-15.

### 3.1 Color

| Token | Valor | Uso | Contraste verificado |
|---|---|---|---|
| `color.bg` | `#FFFFFF` | Fondo predominante | — |
| `color.surface` | `#F8FAFC` | Superficies secundarias (paneles, citas) | — |
| `color.text` | `#0F172A` | Texto principal | 17.85:1 sobre `bg` |
| `color.textSecondary` | `#475569` | Texto secundario | 7.58:1 sobre `bg`; 7.24:1 sobre `surface` |
| `color.textTertiary` | `#64748B` | Metadatos discretos (tokens, costo) | 4.76:1 sobre `bg` (AA texto normal) |
| `color.border` | `#E2E8F0` | Separadores **decorativos** únicamente | 1.23:1 (no se usa para delimitar controles) |
| `color.borderControl` | `#858FA1` | Bordes de campos, selectores y botones secundarios | 3.26:1 sobre `bg` (WCAG 1.4.11) |
| `color.accent` | `#3451D1` | Acento sobrio: botón primario, enlaces, foco | 6.47:1; texto blanco sobre acento 6.47:1 |
| `color.focus` | `#3451D1` | Anillo de foco (2 px + separación 2 px) | 6.47:1 sobre `bg` |
| `color.danger` / `dangerBg` | `#B42318` / `#FEF3F2` | Errores (403, 500, red) | 6.05:1 |
| `color.warning` / `warningBg` | `#B54708` / `#FFFAEB` | 503, API degradada | 5.20:1 |
| `color.success` / `successBg` | `#067647` / `#ECFDF3` | API disponible | 5.40:1 |

Sin gradientes decorativos. El color nunca es el único portador de información (estado de la API = color + texto).

### 3.2 Tipografía

- Familia: **Inter** (licencia OFL, **autoalojada** en el bundle), pila de respaldo `system-ui, -apple-system, "Segoe UI", sans-serif`. Monoespaciada para metadatos técnicos: `ui-monospace, SFMono-Regular, Menlo, monospace`.
- Escala (rem, base 16 px): `xs 0.8125` (13 px, solo metadatos), `sm 0.875`, `base 1`, `lg 1.125`, `xl 1.375`, `2xl 1.75`. Ningún texto de lectura < 14 px; ningún texto < 13 px.
- Pesos: 400, 500, 600. Interlineado: 1.6 cuerpo, 1.25 títulos.
- Longitud de línea de respuestas: máx. `68ch`.

### 3.3 Espaciado, radios, bordes, sombras, movimiento, breakpoints

| Grupo | Tokens |
|---|---|
| Espaciado (px) | `1:4, 2:8, 3:12, 4:16, 5:20, 6:24, 8:32, 10:40, 12:48, 16:64` |
| Radios | `sm 6px` (etiquetas), `md 8px` (campos, botones), `lg 12px` (tarjetas) |
| Bordes | `1px` (único grosor; el foco usa `outline` de 2 px) |
| Sombras | `sm: 0 1px 2px rgb(15 23 42 / 0.05)`, `md: 0 4px 12px rgb(15 23 42 / 0.06)` (solo superficies elevadas) |
| Movimiento | `fast 120ms`, `base 180ms`, curva `cubic-bezier(0.2, 0, 0, 1)`; con `prefers-reduced-motion: reduce` → 0 ms |
| Breakpoints | `sm 640px`, `md 768px`, `lg 1024px`, `xl 1280px` (mobile-first) |

### 3.4 Componentes base (reutilizables, tipados)

`Button` (primary, secondary, ghost; estados hover, focus-visible, active, disabled, loading), `TextField`, `TextArea`, `Select`, `Card`, `Badge`, `Alert` (info, warning, danger, success), `StatusIndicator`, `MessageQuestion`, `MessageAnswer`, `CitationList` / `CitationItem` (expandible), `MetaRow` (tokens, costo, uso de datos), `Spinner`/`Skeleton`. Iconografía: SVG inline propios o de un único set justificado; **sin emojis**.

## 4. Experiencia de usuario

### 4.1 Pantalla principal (una sola vista)

1. **Cabecera discreta**: marca TutorFlow (texto + isotipo SVG simple) y `StatusIndicator` de la API (§4.4).
2. **Identidad declarada** (panel compacto): `Select` de rol (`estudiante` | `coordinador`, valores exactos del contrato) + `TextField` de id + la leyenda de W-R2. Validación de forma en cliente **solo de UX** (no vacío, ≤ 32 caracteres) sin reemplazar la del servidor: la web envía lo que el usuario escribió y muestra lo que la API responda.
3. **Área de consulta**: `TextArea` (contador de caracteres, límite 1000 = D-12), botón Enviar (Enter envía en escritorio, Shift+Enter nueva línea; deshabilitado si vacío o en envío).
4. **Área de conversación**: historial en memoria (W-R4/W-R5) con pregunta y respuesta por turno; la más reciente visible; el foco va a la nueva respuesta tras recibirla.

### 4.2 Respuesta

- Texto con párrafos y listas (render **seguro**: texto plano con saltos de línea y listas detectadas por prefijo; **prohibido** `dangerouslySetInnerHTML`/HTML de la respuesta).
- `CitationList`: cada cita como `documento · seccion`; expandible para mostrar ambos campos completos (sin contenido inventado, W-R7). Sin citas → texto "Sin citas" explícito.
- `MetaRow` secundario: `usoDatosEstudiante` ("Usó datos del estudiante: sí/no"), `tokens`, `costoUsd` (el número tal como lo devuelve la API, con punto decimal, prefijo `US$` y **sin redondeo ni recálculo en el cliente**), y el rol declarado del turno.
- Abstención (texto de SPEC §7.5.5) se muestra como respuesta normal con estilo informativo.

### 4.3 Estados obligatorios (cada uno con diseño propio)

| Estado | Comportamiento |
|---|---|
| Inicial | Mensaje de bienvenida breve + 3 ejemplos de pregunta clicables que solo rellenan el campo (no envían). |
| Lista para enviar | Botón habilitado. |
| Envío en curso | Botón en `loading`, campo deshabilitado, indicador anunciado por `aria-live`; cancelable al cambiar de pregunta. |
| 200 | Respuesta + citas + metadatos. |
| 400 | `Alert` de validación: "La solicitud no es válida" + guía accionable (p. ej. revisar longitud); muestra `title` del problem+json si existe; nunca el cuerpo crudo. |
| 403 (acceso o identidad) | `Alert` danger con la `respuesta` del cuerpo 403 (es texto del contrato) y sus citas si las hay. Distingue visualmente "acceso denegado" de "identidad no válida" por presencia/ausencia de cita Art. 22, sin lógica de permisos. |
| 503 | `Alert` warning: "Servicio no disponible temporalmente" + reintentar. |
| 500 | `Alert` danger genérico + `requestId` (de la cabecera `X-Request-Id`) para soporte; sin detalles internos. |
| API desconectada / error de red / timeout | `Alert` + `StatusIndicator` en "Sin conexión"; reintento manual. |
| Respuesta inesperada | Si la respuesta no pasa la validación en tiempo de ejecución (§8.2): `Alert` "Respuesta no reconocida" sin mostrar el cuerpo. |
| Sin citas / múltiples citas | Ambos con diseño propio. |

### 4.4 Estado de la API

`GET /api/salud` al cargar y cada 30 s (con pausa si la pestaña no está visible): "Disponible" / "Sin conexión" (texto + color + icono).

## 5. Responsive

Mobile-first; diseño íntegro en **375×812** (móvil), **768×1024** (tablet) y **1440×900** (escritorio). Sin desbordamiento horizontal en ninguno; objetivos táctiles ≥ 44×44 px en móvil.

## 6. Accesibilidad (WCAG 2.2 AA como objetivo)

- Landmarks: `header`, `main`, región de conversación con `aria-live="polite"` para nuevas respuestas y `role="alert"` para errores.
- Todos los controles con etiqueta accesible; orden de tabulación lógico; foco visible (`:focus-visible`, `color.focus`).
- Navegación íntegra por teclado (incluida la expansión de citas con Enter/Espacio y `aria-expanded`).
- Contraste según §3.1; `prefers-reduced-motion` respetado.
- `axe-core` sin violaciones `serious`/`critical` en todos los estados de §4.3.

## 7. Interacciones y acabado

Transiciones de `motion.fast/base`; expansión de citas con transición sutil de altura/opacidad; estados hover/focus/active/disabled en todos los botones; feedback inmediato en errores de formulario. Sin animaciones decorativas.

## 8. Arquitectura frontend

### 8.1 Estructura (`web/`)

```
web/
  src/
    tokens/        tokens tipados + tokens.css
    components/    componentes base (§3.4)
    views/         vista principal
    services/      cliente HTTP centralizado (fetch), mapeo de estados HTTP a errores tipados
    models/        DTO tipados del contrato (SPEC §3.1) y del problem+json
    validation/    guards en tiempo de ejecución de las respuestas externas (sin librería)
  tests/           unitarias e integración (Vitest + Testing Library)
  e2e/             Playwright (estados contra API real sin llave y contra rutas simuladas)
  evidencias/      capturas de la entrega (por tarea y ronda)
```

### 8.2 Reglas

- TypeScript `strict: true` (+ `noUncheckedIndexedAccess`), sin `any` explícito.
- Cliente HTTP único; base URL relativa (`/api`) servida por el mismo origen (§9) — **no se cambia CORS ni la API**.
- Validación en tiempo de ejecución de `PreguntarResponse`, `SaludResponse` y `problem+json` con guards escritos a mano.
- Sin estado global innecesario (estado local + un reducer para la conversación).

### 8.3 Dependencias permitidas (otras requieren justificación aprobada por el orquestador)

Ejecución: `react`, `react-dom`. Desarrollo: `typescript`, `vite`, `@vitejs/plugin-react`, `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `@playwright/test`, `axe-core`, `@axe-core/playwright`, y la fuente Inter (archivos OFL autoalojados o `@fontsource/inter`). Versiones fijadas con `package-lock.json`.

## 9. Despliegue

Servicio `web` en `docker-compose.yml` (lo implementa Arnés): imagen multi-etapa (build con Node → servidor estático), sirve el bundle y **hace de proxy de `/api/*` hacia `tutorflow:8080`** (mismo origen; sin CORS). Puerto publicado **3000** (no colisiona con 8080). Cabeceras: `Content-Security-Policy` estricta (`default-src 'self'`; sin `unsafe-inline` en scripts), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`. El servicio `tutorflow` y su contrato no cambian.

## 10. Pruebas exigidas (los escenarios fijan los criterios)

Unitarias de componentes, servicio HTTP (cada código de estado), estados de carga y error, render de citas, guards de validación; accesibilidad automatizada (axe) por estado; integración contra API simulada (rutas de Playwright) y contra la API real **sin llave** (403/400/503/salud: no consume cuota del LLM); `tsc --noEmit` estricto y `vite build`. **No se afirma cobertura ni resultados no ejecutados.**

## 11. Criterio de aceptación visual (resumen; detalle en `escenarios/web.md`)

1. Consistencia visual en todas las pantallas. 2. Sin desbordamientos ni elementos rotos en los 3 tamaños de §5. 3. Cada estado de §4.3 con diseño propio. 4. Respuestas y citas legibles y explorables. 5. Sin errores de consola atribuibles a la app. 6. Ningún control sin función. 7. Accesible por teclado. 8. Build de producción y pruebas frontend en verde. 9. **Informe del revisor independiente con resultado APPROVED** (AGENTS §13) y aceptación humana registrada.
