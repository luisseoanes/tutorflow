import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AssistantView } from "../src/views/AssistantView";
import {
  jsonResponse,
  mockApi,
  RESPUESTA_403_ACCESO,
  RESPUESTA_403_IDENTIDAD,
  RESPUESTA_OK,
  violacionesGraves,
} from "./helpers";
import type { Handler } from "./helpers";

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
});

function abrir(): ReturnType<typeof userEvent.setup> {
  render(<AssistantView />);
  return userEvent.setup();
}

async function enviar(
  user: ReturnType<typeof userEvent.setup>,
  pregunta = "¿Cuál es mi promedio?",
  id = "EST-001",
): Promise<void> {
  const campoId = screen.getByLabelText("Identificador");
  if ((campoId as HTMLInputElement).value !== id) {
    await user.clear(campoId);
    await user.type(campoId, id);
  }
  const campo = screen.getByLabelText("Tu pregunta");
  await user.clear(campo);
  await user.type(campo, pregunta);
  await user.click(screen.getByRole("button", { name: /^enviar/i }));
}

/** Handler que no responde hasta que se cancela (estado "cargando"). */
const nuncaResponde: Handler = (_url, init) =>
  new Promise<Response>((_resolve, reject) => {
    init?.signal?.addEventListener("abort", () => reject(new DOMException("abort", "AbortError")));
  });

describe("identidad declarada", () => {
  // Criterio: WEB-02.a
  it("muestra la leyenda 'Identidad declarada — no es una autenticación' junto al rol y el id, sin login", () => {
    mockApi();
    abrir();
    const grupo = screen.getByRole("group", { name: "Identidad declarada — no es una autenticación" });
    expect(within(grupo).getByLabelText("Rol")).toBeInTheDocument();
    expect(within(grupo).getByLabelText("Identificador")).toBeInTheDocument();
    expect(document.body.textContent ?? "").not.toMatch(/iniciar sesi[oó]n|inicia sesi[oó]n|login|contrase[nñ]a|password/i);
    expect(document.querySelector('input[type="password"]')).toBeNull();
  });

  // Criterio: WEB-02.b
  it("el selector solo ofrece estudiante y coordinador", () => {
    mockApi();
    abrir();
    const opciones = within(screen.getByLabelText("Rol")).getAllByRole("option").map((o) => (o as HTMLOptionElement).value);
    expect(opciones).toEqual(["estudiante", "coordinador"]);
  });

  // Criterio: WEB-02.b
  it("envía el id tal cual se escribió (sin normalizar mayúsculas ni espacios) y el rol elegido", async () => {
    const api = mockApi();
    const user = abrir();
    await user.selectOptions(screen.getByLabelText("Rol"), "coordinador");
    await enviar(user, "Pregunta de prueba", " cOord-01 ");
    await screen.findByText(/Tokens: 1234/);
    expect(api.preguntas()).toEqual([
      { pregunta: "Pregunta de prueba", usuario: { rol: "coordinador", id: " cOord-01 " } },
    ]);
  });

  it("no envía si el id está vacío: muestra el error de UX y no llama a la API", async () => {
    const api = mockApi();
    const user = abrir();
    await user.type(screen.getByLabelText("Tu pregunta"), "Hola");
    await user.click(screen.getByRole("button", { name: /^enviar/i }));
    expect(await screen.findByText("Ingresa tu identificador.")).toBeInTheDocument();
    expect(api.preguntas()).toEqual([]);
  });
});

describe("respuesta 200", () => {
  // Criterio: WEB-05.a
  it("muestra respuesta (párrafos y listas), citas, uso de datos, tokens y costo exacto", async () => {
    mockApi();
    const user = abrir();
    await enviar(user);

    expect(await screen.findByText("Tu promedio del periodo es el indicado por la API.")).toBeInTheDocument();
    expect(screen.getByText("Primer punto").closest("ul")).not.toBeNull();
    expect(screen.getByText("Primer punto").tagName).toBe("LI");
    expect(screen.getByText("Segundo punto").tagName).toBe("LI");

    expect(screen.getByText("evaluacion_y_calificaciones.md · Sección 4")).toBeInTheDocument();
    expect(screen.getByText("reglamento_academico_2025.md · Artículo 5")).toBeInTheDocument();
    expect(screen.getByText("Datos del estudiante · Resumen académico")).toBeInTheDocument();
    expect(screen.getByText("Usó datos del estudiante: sí")).toBeInTheDocument();
    expect(screen.getByText("Tokens: 1234")).toBeInTheDocument();
    expect(screen.getByText("Costo: US$ 0.00395")).toBeInTheDocument();
    expect(screen.getByText("Rol declarado: estudiante")).toBeInTheDocument();
  });

  // Criterio: WEB-05.a
  it("usoDatosEstudiante=false se muestra como 'no' y el costo 0 tal cual", async () => {
    mockApi({ preguntar: () => jsonResponse(200, { ...RESPUESTA_OK, usoDatosEstudiante: false, costoUsd: 0, tokens: 0 }) });
    const user = abrir();
    await enviar(user);
    expect(await screen.findByText("Usó datos del estudiante: no")).toBeInTheDocument();
    expect(screen.getByText("Costo: US$ 0")).toBeInTheDocument();
  });

  // Criterio: WEB-05.b
  it("renderiza la respuesta como texto seguro: HTML literal, sin crear elementos", async () => {
    const xss = '<img src=x onerror=alert(1)> y <script>alert(2)</script>';
    mockApi({ preguntar: () => jsonResponse(200, { ...RESPUESTA_OK, respuesta: xss }) });
    const user = abrir();
    await enviar(user);
    expect(await screen.findByText(xss)).toBeInTheDocument();
    expect(document.querySelector("img")).toBeNull();
    expect(document.querySelector("main script")).toBeNull();
    expect(document.querySelectorAll("[onerror]")).toHaveLength(0);
  });

  it("la abstención (sin citas, sin datos del estudiante) se muestra como respuesta normal informativa", async () => {
    mockApi({
      preguntar: () =>
        jsonResponse(200, {
          respuesta: "No encuentro esa información en los documentos disponibles.",
          citas: [],
          usoDatosEstudiante: false,
          tokens: 10,
          costoUsd: 0.0001,
        }),
    });
    const user = abrir();
    await enviar(user);
    const texto = await screen.findByText("No encuentro esa información en los documentos disponibles.");
    expect(texto.closest(".message--informative")).not.toBeNull();
    expect(screen.getByText("Sin citas")).toBeInTheDocument();
  });
});

describe("citas", () => {
  // Criterio: WEB-06.a
  it("citas vacías -> texto 'Sin citas'", async () => {
    mockApi({ preguntar: () => jsonResponse(200, { ...RESPUESTA_OK, citas: [] }) });
    const user = abrir();
    await enviar(user);
    expect(await screen.findByText("Sin citas")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /·/ })).toBeNull();
  });

  // Criterio: WEB-06.a
  it("tres citas -> tres elementos en el orden de la API, sin contenido inventado", async () => {
    mockApi();
    const user = abrir();
    await enviar(user);
    await screen.findByText("Tokens: 1234");
    const botones = screen.getAllByRole("button", { name: /·/ });
    expect(botones.map((b) => b.textContent?.trim())).toEqual([
      "evaluacion_y_calificaciones.md · Sección 4",
      "reglamento_academico_2025.md · Artículo 5",
      "Datos del estudiante · Resumen académico",
    ]);
  });

  // Criterio: WEB-06.b
  it("cada cita se expande con ratón, Enter y Espacio; aria-expanded correcto; solo documento y sección", async () => {
    mockApi();
    const user = abrir();
    await enviar(user);
    await screen.findByText("Tokens: 1234");
    const [primera, segunda, tercera] = screen.getAllByRole("button", { name: /·/ });
    if (!primera || !segunda || !tercera) throw new Error("faltan citas");

    expect(primera).toHaveAttribute("aria-expanded", "false");
    await user.click(primera);
    expect(primera).toHaveAttribute("aria-expanded", "true");
    const panel = document.getElementById(primera.getAttribute("aria-controls") ?? "");
    expect(panel).not.toBeNull();
    expect(panel?.textContent).toBe("Documentoevaluacion_y_calificaciones.mdSecciónSección 4");
    await user.click(primera);
    expect(primera).toHaveAttribute("aria-expanded", "false");

    segunda.focus();
    await user.keyboard("{Enter}");
    expect(segunda).toHaveAttribute("aria-expanded", "true");
    await user.keyboard("{Enter}");
    expect(segunda).toHaveAttribute("aria-expanded", "false");

    tercera.focus();
    await user.keyboard(" ");
    expect(tercera).toHaveAttribute("aria-expanded", "true");
    expect(primera).toHaveAttribute("aria-expanded", "false");
  });
});

describe("estados de error", () => {
  // Criterio: WEB-07.a
  it("400 -> alerta de validación con el title y guía; nunca el cuerpo crudo", async () => {
    mockApi({
      preguntar: () => jsonResponse(400, { title: "La pregunta excede el límite", type: "about:blank", detail: "CUERPO-CRUDO-INTERNO" }),
    });
    const user = abrir();
    await enviar(user);
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("La solicitud no es válida")).toBeInTheDocument();
    expect(alerta).toHaveTextContent("La pregunta excede el límite");
    expect(alerta).toHaveTextContent(/no puede estar vacía ni superar 1000 caracteres/);
    expect(document.body.textContent).not.toContain("CUERPO-CRUDO-INTERNO");
    expect(document.body.textContent).not.toContain("about:blank");
  });

  // Criterio: WEB-07.b
  it("403 con cita Art. 22 -> 'Acceso denegado' con la respuesta del contrato y su cita", async () => {
    mockApi({ preguntar: () => jsonResponse(403, RESPUESTA_403_ACCESO) });
    const user = abrir();
    await enviar(user, "Notas de otra persona");
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Acceso denegado")).toBeInTheDocument();
    expect(alerta).toHaveTextContent(RESPUESTA_403_ACCESO.respuesta);
    expect(within(alerta).getByText("reglamento_academico_2025.md · Artículo 22")).toBeInTheDocument();
    expect(screen.queryByText("Identidad no válida")).toBeNull();
  });

  // Criterio: WEB-07.b
  it("403 sin citas -> 'Identidad no válida' con la respuesta del contrato", async () => {
    mockApi({ preguntar: () => jsonResponse(403, RESPUESTA_403_IDENTIDAD) });
    const user = abrir();
    await enviar(user);
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Identidad no válida")).toBeInTheDocument();
    expect(alerta).toHaveTextContent(RESPUESTA_403_IDENTIDAD.respuesta);
    expect(screen.queryByText("Acceso denegado")).toBeNull();
    expect(within(alerta).queryByRole("button", { name: /·/ })).toBeNull();
  });

  // Criterio: WEB-07.c
  it("503 -> 'Servicio no disponible temporalmente' con reintento que repite la misma pregunta", async () => {
    let llamadas = 0;
    const api = mockApi({
      preguntar: () => (++llamadas === 1 ? jsonResponse(503, { title: "x" }) : jsonResponse(200, RESPUESTA_OK)),
    });
    const user = abrir();
    await enviar(user, "Pregunta con reintento");
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Servicio no disponible temporalmente")).toBeInTheDocument();
    await user.click(within(alerta).getByRole("button", { name: /reintentar/i }));
    expect(await screen.findByText("Tokens: 1234")).toBeInTheDocument();
    expect(api.preguntas()).toEqual([
      { pregunta: "Pregunta con reintento", usuario: { rol: "estudiante", id: "EST-001" } },
      { pregunta: "Pregunta con reintento", usuario: { rol: "estudiante", id: "EST-001" } },
    ]);
    expect(screen.queryByRole("alert")).toBeNull();
  });

  // Criterio: WEB-07.c
  it("500 -> alerta genérica con el X-Request-Id recibido, sin detalles internos", async () => {
    mockApi({
      preguntar: () =>
        jsonResponse(500, { title: "Error", detail: "System.InvalidOperationException: stack trace" }, { "X-Request-Id": "req-777" }),
    });
    const user = abrir();
    await enviar(user);
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Ocurrió un error en el servicio")).toBeInTheDocument();
    expect(alerta).toHaveTextContent("req-777");
    expect(document.body.textContent).not.toMatch(/InvalidOperation|stack trace/);
  });

  // Criterio: WEB-07.d
  it("error de red -> 'Sin conexión con la API', reintento e indicador de la API en 'Sin conexión'", async () => {
    let falla = true;
    mockApi({
      preguntar: () => {
        if (falla) throw new TypeError("Failed to fetch");
        return jsonResponse(200, RESPUESTA_OK);
      },
    });
    const user = abrir();
    await screen.findByText("Disponible");
    await enviar(user);
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Sin conexión con la API")).toBeInTheDocument();
    expect(screen.queryByText("Disponible")).toBeNull();
    expect(screen.getAllByText(/Sin conexión/).length).toBeGreaterThanOrEqual(2);
    falla = false;
    await user.click(within(alerta).getByRole("button", { name: /reintentar/i }));
    expect(await screen.findByText("Tokens: 1234")).toBeInTheDocument();
  });

  // Criterio: WEB-07.d
  it("respuesta con forma inesperada -> 'Respuesta no reconocida' sin mostrar el cuerpo", async () => {
    mockApi({ preguntar: () => jsonResponse(200, { respuesta: 123, ladron: "CUERPO-INESPERADO" }) });
    const user = abrir();
    await enviar(user);
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Respuesta no reconocida")).toBeInTheDocument();
    expect(document.body.textContent).not.toContain("CUERPO-INESPERADO");
  });

  // Criterio: WEB-07.d
  it("timeout del cliente (sin respuesta) -> 'Sin conexión con la API'", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockApi({ preguntar: nuncaResponde });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<AssistantView />);
    await enviar(user);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(61_000);
    });
    const alerta = await screen.findByRole("alert");
    expect(within(alerta).getByText("Sin conexión con la API")).toBeInTheDocument();
  });
});

describe("estado inicial y formulario", () => {
  // Criterio: WEB-08.a
  it("estado inicial: bienvenida y 3 ejemplos que solo rellenan el campo (no envían)", async () => {
    const api = mockApi();
    const user = abrir();
    expect(screen.getByRole("heading", { name: "Bienvenido a TutorFlow" })).toBeInTheDocument();
    const ejemplos = within(screen.getByRole("region", { name: "Conversación" })).getAllByRole("button");
    expect(ejemplos).toHaveLength(3);
    const texto = ejemplos[1]?.textContent ?? "";
    await user.click(ejemplos[1] as HTMLElement);
    expect(screen.getByLabelText("Tu pregunta")).toHaveValue(texto);
    expect(api.preguntas()).toEqual([]);
    expect(screen.getByRole("heading", { name: "Bienvenido a TutorFlow" })).toBeInTheDocument();
  });

  // Criterio: WEB-08.b
  it("el botón Enviar está deshabilitado con pregunta vacía o solo espacios y se habilita con texto", async () => {
    mockApi();
    const user = abrir();
    const boton = screen.getByRole("button", { name: /^enviar/i });
    expect(boton).toBeDisabled();
    await user.type(screen.getByLabelText("Tu pregunta"), "   ");
    expect(boton).toBeDisabled();
    await user.type(screen.getByLabelText("Tu pregunta"), "hola");
    expect(boton).toBeEnabled();
  });

  // Criterio: WEB-08.b
  it("durante el envío: botón en carga y deshabilitado, campo deshabilitado e indicador anunciado (role=status)", async () => {
    mockApi({ preguntar: nuncaResponde });
    const user = abrir();
    await enviar(user);
    const estado = await screen.findByText("Consultando a TutorFlow…");
    expect(estado.closest('[role="status"]')).not.toBeNull();
    expect(screen.getByLabelText("Tu pregunta")).toBeDisabled();
    const boton = screen.getByRole("button", { name: /enviando/i });
    expect(boton).toBeDisabled();
    expect(boton).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("region", { name: "Conversación" })).toHaveAttribute("aria-live", "polite");
  });

  // Criterio: WEB-08.b
  it("el envío es cancelable: vuelve a habilitar el campo con la pregunta y sin alerta de error", async () => {
    mockApi({ preguntar: nuncaResponde });
    const user = abrir();
    await enviar(user, "Pregunta a cancelar");
    await user.click(await screen.findByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(screen.getByLabelText("Tu pregunta")).toBeEnabled());
    expect(screen.getByLabelText("Tu pregunta")).toHaveValue("Pregunta a cancelar");
    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.queryByText("Consultando a TutorFlow…")).toBeNull();
  });

  // Criterio: WEB-08.b
  it("contador de caracteres con límite 1000: al pasarlo muestra error y deshabilita el envío", () => {
    const api = mockApi();
    render(<AssistantView />);
    const campo = screen.getByLabelText("Tu pregunta");
    expect(screen.getByText("0 / 1000")).toBeInTheDocument();
    fireEvent.change(campo, { target: { value: "a".repeat(1000) } });
    expect(screen.getByText("1000 / 1000")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^enviar/i })).toBeEnabled();
    fireEvent.change(campo, { target: { value: "a".repeat(1001) } });
    expect(screen.getByText("1001 / 1000")).toBeInTheDocument();
    expect(screen.getByText("La pregunta supera el límite de 1000 caracteres.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^enviar/i })).toBeDisabled();
    expect(api.preguntas()).toEqual([]);
  });

  // Criterio: WEB-08.c
  it("Enter envía y Shift+Enter inserta una nueva línea", async () => {
    const api = mockApi();
    const user = abrir();
    await user.type(screen.getByLabelText("Identificador"), "EST-001");
    const campo = screen.getByLabelText("Tu pregunta");
    await user.type(campo, "línea uno{Shift>}{Enter}{/Shift}línea dos");
    expect(campo).toHaveValue("línea uno\nlínea dos");
    expect(api.preguntas()).toEqual([]);
    await user.keyboard("{Enter}");
    await screen.findByText("Tokens: 1234");
    expect(api.preguntas()).toEqual([
      { pregunta: "línea uno\nlínea dos", usuario: { rol: "estudiante", id: "EST-001" } },
    ]);
  });

  // Criterio: WEB-05.c
  it("el foco pasa a la nueva respuesta al recibirla (contenedor enfocable dentro de la región aria-live)", async () => {
    mockApi();
    const user = abrir();
    await enviar(user);
    await screen.findByText("Tokens: 1234");
    const respuesta = screen.getByText("Respuesta de TutorFlow").closest(".message--answer");
    await waitFor(() => expect(respuesta).toHaveFocus());
    expect(respuesta?.closest('[aria-live="polite"]')).not.toBeNull();
  });
});

describe("sin memoria conversacional", () => {
  // Criterio: WEB-10.a
  it("dos envíos seguidos producen dos peticiones con solo la pregunta y el usuario actuales", async () => {
    const api = mockApi();
    const user = abrir();
    await enviar(user, "Primera pregunta", "EST-001");
    await screen.findByText("Tokens: 1234");
    await user.selectOptions(screen.getByLabelText("Rol"), "coordinador");
    await enviar(user, "Segunda pregunta", "COORD-9");
    await waitFor(() => expect(api.preguntas()).toHaveLength(2));
    const cuerpos = api.preguntas();
    expect(cuerpos).toEqual([
      { pregunta: "Primera pregunta", usuario: { rol: "estudiante", id: "EST-001" } },
      { pregunta: "Segunda pregunta", usuario: { rol: "coordinador", id: "COORD-9" } },
    ]);
    expect(JSON.stringify(cuerpos[1])).not.toContain("Primera");
    expect(JSON.stringify(cuerpos[1])).not.toContain(RESPUESTA_OK.respuesta.slice(0, 20));
    // El historial visible conserva ambos turnos (solo visual).
    expect(screen.getByText("Primera pregunta")).toBeInTheDocument();
    expect(screen.getByText("Segunda pregunta")).toBeInTheDocument();
  });
});

describe("estado de la API", () => {
  // Criterio: WEB-09.a
  it("salud 200 {estado:ok} -> 'Disponible' (texto + icono)", async () => {
    mockApi();
    abrir();
    const estado = await screen.findByText("Disponible");
    const indicador = estado.closest('[role="status"]');
    expect(indicador).not.toBeNull();
    expect(indicador?.querySelector("svg")).not.toBeNull();
    expect(indicador?.className).toContain("status--ok");
  });

  // Criterio: WEB-09.a
  it("error de red -> 'Sin conexión' (texto + icono, no solo color)", async () => {
    mockApi({
      salud: () => {
        throw new TypeError("Failed to fetch");
      },
    });
    abrir();
    const estado = await screen.findByText(/Sin conexión/);
    const indicador = estado.closest('[role="status"]');
    expect(indicador?.className).toContain("status--down");
    expect(indicador?.querySelector("svg")).not.toBeNull();
  });

  // Criterio: WEB-09.a
  it("sondea cada 30 s y se pausa con la pestaña oculta", async () => {
    vi.useFakeTimers();
    const api = mockApi();
    render(<AssistantView />);
    const salud = (): number => api.fetch.mock.calls.filter(([u]) => u === "/api/salud").length;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(salud()).toBe(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(29_999);
    });
    expect(salud()).toBe(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1);
    });
    expect(salud()).toBe(2);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(salud()).toBe(3);

    // Pestaña oculta: sin sondeo.
    Object.defineProperty(document, "visibilityState", { value: "hidden", configurable: true });
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
      await vi.advanceTimersByTimeAsync(120_000);
    });
    expect(salud()).toBe(3);

    // Al volver: comprueba de inmediato y reanuda.
    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(salud()).toBe(4);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(30_000);
    });
    expect(salud()).toBe(5);
  });
});

describe("accesibilidad automatizada (axe en jsdom)", () => {
  async function sinViolaciones(): Promise<void> {
    expect(await violacionesGraves(document.body)).toEqual([]);
  }

  // Criterio: WEB-12.a
  it("estado inicial", async () => {
    mockApi();
    abrir();
    await screen.findByText("Disponible");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("cargando", async () => {
    mockApi({ preguntar: nuncaResponde });
    const user = abrir();
    await enviar(user);
    await screen.findByText("Consultando a TutorFlow…");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("200 con citas expandidas", async () => {
    mockApi();
    const user = abrir();
    await enviar(user);
    await screen.findByText("Tokens: 1234");
    await user.click(screen.getAllByRole("button", { name: /·/ })[0] as HTMLElement);
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("400", async () => {
    mockApi({ preguntar: () => jsonResponse(400, { title: "Solicitud inválida" }) });
    const user = abrir();
    await enviar(user);
    await screen.findByRole("alert");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("403 acceso e identidad", async () => {
    mockApi({ preguntar: () => jsonResponse(403, RESPUESTA_403_ACCESO) });
    const user = abrir();
    await enviar(user);
    await screen.findByText("Acceso denegado");
    await sinViolaciones();
    mockApi({ preguntar: () => jsonResponse(403, RESPUESTA_403_IDENTIDAD) });
    await enviar(user, "Otra");
    await screen.findByText("Identidad no válida");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("503", async () => {
    mockApi({ preguntar: () => jsonResponse(503, {}) });
    const user = abrir();
    await enviar(user);
    await screen.findByText("Servicio no disponible temporalmente");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("500", async () => {
    mockApi({ preguntar: () => jsonResponse(500, {}, { "X-Request-Id": "req-1" }) });
    const user = abrir();
    await enviar(user);
    await screen.findByText("Ocurrió un error en el servicio");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("sin conexión (API desconectada y error de red)", async () => {
    mockApi({
      salud: () => {
        throw new TypeError("Failed to fetch");
      },
      preguntar: () => {
        throw new TypeError("Failed to fetch");
      },
    });
    const user = abrir();
    await enviar(user);
    await screen.findByText("Sin conexión con la API");
    await sinViolaciones();
  });

  // Criterio: WEB-12.a
  it("respuesta no reconocida y campo con error de límite", async () => {
    mockApi({ preguntar: () => jsonResponse(200, { x: 1 }) });
    const user = abrir();
    await enviar(user);
    await screen.findByText("Respuesta no reconocida");
    fireEvent.change(screen.getByLabelText("Tu pregunta"), { target: { value: "a".repeat(1001) } });
    await sinViolaciones();
  });
});
