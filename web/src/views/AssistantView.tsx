import { useCallback, useEffect, useReducer, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent, ReactElement } from "react";
import { Button } from "../components/Button";
import { Select, TextArea, TextField } from "../components/Fields";
import { Icon } from "../components/Icon";
import { Card, StatusIndicator } from "../components/Surfaces";
import { LIMITE_ID, LIMITE_PREGUNTA, ROLES } from "../models/contract";
import type { Rol } from "../models/contract";
import { preguntar } from "../services/apiClient";
import { conversationReducer, initialConversation } from "./conversation";
import { TurnView } from "./TurnView";
import { useApiStatus } from "./useApiStatus";

const EJEMPLOS: readonly string[] = [
  "¿Cuál es mi promedio del periodo actual?",
  "¿Cuántas faltas puedo tener antes de perder una asignatura?",
  "¿Qué requisitos tiene la beca por rendimiento académico?",
];

const ROL_OPCIONES = ROLES.map((r) => ({ value: r, label: r }));

function esRol(value: string): value is Rol {
  return (ROLES as readonly string[]).includes(value);
}

/** En pantallas táctiles Enter inserta nueva línea (SPEC-WEB §4.1: Enter envía en escritorio). */
function esTactil(): boolean {
  return typeof window.matchMedia === "function" && window.matchMedia("(pointer: coarse)").matches;
}

export function AssistantView(): ReactElement {
  const { status, markDown } = useApiStatus();
  const [state, dispatch] = useReducer(conversationReducer, initialConversation);
  const [rol, setRol] = useState<Rol>("estudiante");
  const [id, setId] = useState("");
  const [pregunta, setPregunta] = useState("");
  const [idTouched, setIdTouched] = useState(false);

  const controllers = useRef(new Map<number, AbortController>());
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const idRef = useRef<HTMLInputElement | null>(null);

  const sending = state.turns.some((t) => t.status.phase === "pending");

  useEffect(() => {
    const map = controllers.current;
    return () => {
      for (const c of map.values()) c.abort();
    };
  }, []);

  const run = useCallback(
    async (turnId: number, texto: string, usuario: { rol: Rol; id: string }): Promise<void> => {
      const controller = new AbortController();
      controllers.current.set(turnId, controller);
      const outcome = await preguntar({ pregunta: texto, usuario }, { signal: controller.signal });
      controllers.current.delete(turnId);
      if (outcome.kind === "cancelled") return;
      if (outcome.kind === "network") markDown();
      dispatch({ type: "resolved", id: turnId, outcome });
    },
    [markDown],
  );

  const idLength = id.length;
  const idError = !idTouched
    ? null
    : idLength === 0
      ? "Ingresa tu identificador."
      : idLength > LIMITE_ID
        ? `Máximo ${LIMITE_ID} caracteres.`
        : null;
  const preguntaLength = pregunta.length;
  const preguntaTooLong = preguntaLength > LIMITE_PREGUNTA;
  const canSubmit = pregunta.trim().length > 0 && !preguntaTooLong && !sending;

  const submit = (): void => {
    if (!canSubmit) return;
    if (idLength === 0 || idLength > LIMITE_ID) {
      setIdTouched(true);
      idRef.current?.focus();
      return;
    }
    const usuario = { rol, id };
    const texto = pregunta;
    const turnId = state.nextId;
    dispatch({ type: "send", pregunta: texto, usuario });
    setPregunta("");
    void run(turnId, texto, usuario);
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    submit();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>): void => {
    if (event.key !== "Enter" || event.shiftKey || event.nativeEvent.isComposing || esTactil()) return;
    event.preventDefault();
    submit();
  };

  const onRetry = (turnId: number): void => {
    const turn = state.turns.find((t) => t.id === turnId);
    if (!turn || sending) return;
    dispatch({ type: "retry", id: turnId });
    void run(turnId, turn.pregunta, turn.usuario);
  };

  const onCancel = (turnId: number): void => {
    const turn = state.turns.find((t) => t.id === turnId);
    controllers.current.get(turnId)?.abort();
    controllers.current.delete(turnId);
    dispatch({ type: "remove", id: turnId });
    if (turn) setPregunta(turn.pregunta);
    // El campo vuelve a habilitarse tras el renderizado; el foco se restituye allí.
    setTimeout(() => textareaRef.current?.focus(), 0);
  };

  const usarEjemplo = (texto: string): void => {
    setPregunta(texto); // solo rellena el campo: no envía (WEB-08.a)
    textareaRef.current?.focus();
  };

  // Turnos del más reciente al más antiguo: la última respuesta queda visible.
  const turnos = [...state.turns].reverse();

  return (
    <>
      <header className="app-header">
        <div className="app-header__inner">
          <h1 className="brand">
            <span className="brand__mark">
              <Icon name="logo" />
            </span>
            TutorFlow
          </h1>
          <StatusIndicator status={status} />
        </div>
      </header>

      <main className="app-main">
        <form className="stack" onSubmit={onSubmit} noValidate>
          <fieldset className="identity">
            <legend className="identity__legend">Identidad declarada — no es una autenticación</legend>
            <p className="identity__note">
              <Icon name="lock" size="sm" />
              <span>La API solo comprueba que el rol y el identificador sean consistentes entre sí.</span>
            </p>
            <div className="identity__fields">
              <Select
                label="Rol"
                options={ROL_OPCIONES}
                value={rol}
                onChange={(e) => {
                  if (esRol(e.target.value)) setRol(e.target.value);
                }}
              />
              <TextField
                label="Identificador"
                value={id}
                inputRef={idRef}
                onChange={(e) => setId(e.target.value)}
                onBlur={() => setIdTouched(true)}
                error={idError}
                hint={`Hasta ${LIMITE_ID} caracteres`}
                autoComplete="off"
                autoCapitalize="off"
                spellCheck={false}
              />
            </div>
          </fieldset>

          <Card tone="default" className="composer">
            <TextArea
              label="Tu pregunta"
              textareaRef={textareaRef}
              value={pregunta}
              onChange={(e) => setPregunta(e.target.value)}
              onKeyDown={onKeyDown}
              disabled={sending}
              rows={3}
              placeholder="Escribe tu pregunta académica"
              error={preguntaTooLong ? `La pregunta supera el límite de ${LIMITE_PREGUNTA} caracteres.` : null}
              hint={<span className="hint-keys">Enter envía · Shift+Enter inserta una línea nueva</span>}
              counter={
                <span className={`counter${preguntaTooLong ? " counter--over" : ""}`}>
                  {preguntaLength} / {LIMITE_PREGUNTA}
                </span>
              }
            />
            <div className="composer__actions">
              <Button type="submit" loading={sending} disabled={!canSubmit}>
                {sending ? "Enviando" : "Enviar"}
                {sending ? null : <Icon name="send" size="sm" />}
              </Button>
            </div>
          </Card>
        </form>

        <section className="conversation" aria-label="Conversación" aria-live="polite">
          {turnos.length === 0 ? (
            <Card tone="surface" className="welcome">
              <h2 className="welcome__title">Bienvenido a TutorFlow</h2>
              <p className="welcome__text">
                Consulta tus notas, asistencia y la normativa académica. Cada pregunta se responde de forma
                independiente, con las fuentes que la respaldan.
              </p>
              <p className="welcome__label">Prueba con una de estas preguntas</p>
              <ul className="examples">
                {EJEMPLOS.map((texto) => (
                  <li key={texto}>
                    <button type="button" className="example" onClick={() => usarEjemplo(texto)}>
                      {texto}
                    </button>
                  </li>
                ))}
              </ul>
            </Card>
          ) : (
            turnos.map((turn) => (
              <TurnView key={turn.id} turn={turn} busy={sending} onRetry={onRetry} onCancel={onCancel} />
            ))
          )}
        </section>
      </main>
    </>
  );
}
