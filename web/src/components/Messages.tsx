import { useId, useState } from "react";
import type { ReactElement, Ref } from "react";
import type { Cita, PreguntarResponse, Rol } from "../models/contract";
import { parseAnswer } from "./answerBlocks";
import { Icon } from "./Icon";

/* ---------- Citas (SPEC-WEB §4.2, W-R7: solo {documento, seccion}) ---------- */

export function CitationItem({ cita }: { cita: Cita }): ReactElement {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  return (
    <li className="citation">
      <button
        type="button"
        className="citation__toggle"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((v) => !v)}
      >
        <Icon name="doc" size="sm" />
        <span className="citation__label">
          {cita.documento} · {cita.seccion}
        </span>
        <span className={`citation__chevron${open ? " citation__chevron--open" : ""}`}>
          <Icon name="chevron" size="sm" />
        </span>
      </button>
      {open ? (
        <dl className="citation__panel" id={panelId}>
          <div>
            <dt>Documento</dt>
            <dd>{cita.documento}</dd>
          </div>
          <div>
            <dt>Sección</dt>
            <dd>{cita.seccion}</dd>
          </div>
        </dl>
      ) : null}
    </li>
  );
}

export function CitationList({ citas }: { citas: readonly Cita[] }): ReactElement {
  const headingId = useId();
  if (citas.length === 0) {
    return <p className="citations__empty">Sin citas</p>;
  }
  return (
    <section className="citations" aria-labelledby={headingId}>
      <h3 className="citations__title" id={headingId}>
        Fuentes ({citas.length})
      </h3>
      <ul className="citations__list">
        {citas.map((cita, i) => (
          <CitationItem key={`${i}-${cita.documento}-${cita.seccion}`} cita={cita} />
        ))}
      </ul>
    </section>
  );
}

/* ---------- Metadatos (§4.2): valores tal como los devuelve la API, sin recálculo ---------- */

interface MetaRowProps {
  data: PreguntarResponse;
  rol: Rol;
}

export function MetaRow({ data, rol }: MetaRowProps): ReactElement {
  return (
    <ul className="meta" aria-label="Metadatos de la respuesta">
      <li>Usó datos del estudiante: {data.usoDatosEstudiante ? "sí" : "no"}</li>
      <li className="mono">Tokens: {String(data.tokens)}</li>
      <li className="mono">Costo: US$ {String(data.costoUsd)}</li>
      <li>Rol declarado: {rol}</li>
    </ul>
  );
}

/* ---------- Mensajes ---------- */

export function MessageQuestion({ pregunta, rol }: { pregunta: string; rol: Rol }): ReactElement {
  return (
    <div className="message message--question">
      <p className="message__who">Tu pregunta · {rol}</p>
      <p className="message__text">{pregunta}</p>
    </div>
  );
}

function AnswerText({ text }: { text: string }): ReactElement {
  const blocks = parseAnswer(text);
  return (
    <div className="answer-text">
      {blocks.map((block, i) => {
        if (block.kind === "bullets") {
          return (
            <ul key={i}>
              {block.items.map((item, j) => (
                <li key={j}>{item}</li>
              ))}
            </ul>
          );
        }
        if (block.kind === "numbered") {
          return (
            <ol key={i}>
              {block.items.map((item, j) => (
                <li key={j}>{item}</li>
              ))}
            </ol>
          );
        }
        return (
          <p key={i}>
            {block.lines.map((line, j) => (
              <span key={j}>
                {j > 0 ? <br /> : null}
                {line}
              </span>
            ))}
          </p>
        );
      })}
    </div>
  );
}

interface MessageAnswerProps {
  data: PreguntarResponse;
  rol: Rol;
  answerRef?: Ref<HTMLDivElement>;
}

export function MessageAnswer({ data, rol, answerRef }: MessageAnswerProps): ReactElement {
  // Sin citas y sin datos del estudiante: es la abstención o una negativa en texto; estilo informativo.
  const informative = data.citas.length === 0 && !data.usoDatosEstudiante;
  return (
    <div
      className={`message message--answer${informative ? " message--informative" : ""}`}
      ref={answerRef}
      tabIndex={-1}
    >
      <p className="message__who">Respuesta de TutorFlow</p>
      <AnswerText text={data.respuesta} />
      <CitationList citas={data.citas} />
      <MetaRow data={data} rol={rol} />
    </div>
  );
}
