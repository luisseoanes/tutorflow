import { useEffect, useRef } from "react";
import type { ReactElement, RefObject } from "react";
import { Button } from "../components/Button";
import { CitationList, MessageAnswer, MessageQuestion } from "../components/Messages";
import { Icon } from "../components/Icon";
import { Skeleton, Spinner } from "../components/Spinner";
import { Alert } from "../components/Surfaces";
import type { ResolvedOutcome, Turn } from "./conversation";

interface TurnViewProps {
  turn: Turn;
  busy: boolean;
  onRetry: (id: number) => void;
  onCancel: (id: number) => void;
}

function RetryButton({ onClick, disabled }: { onClick: () => void; disabled: boolean }): ReactElement {
  return (
    <Button variant="secondary" onClick={onClick} disabled={disabled}>
      <Icon name="retry" size="sm" /> Reintentar
    </Button>
  );
}

interface OutcomeProps {
  outcome: ResolvedOutcome;
  turn: Turn;
  busy: boolean;
  onRetry: () => void;
  targetRef: RefObject<HTMLDivElement | null>;
}

function OutcomeView({ outcome, turn, busy, onRetry, targetRef }: OutcomeProps): ReactElement {
  switch (outcome.kind) {
    case "ok":
      return <MessageAnswer data={outcome.data} rol={turn.usuario.rol} answerRef={targetRef} />;
    case "forbidden": {
      // Distinción solo por el cuerpo recibido (cita presente o no); sin lógica de permisos (W-R3).
      const withCitation = outcome.data.citas.length > 0;
      return (
        <Alert
          variant="danger"
          title={withCitation ? "Acceso denegado" : "Identidad no válida"}
          alertRef={targetRef}
        >
          <p>{outcome.data.respuesta}</p>
          {withCitation ? <CitationList citas={outcome.data.citas} /> : null}
        </Alert>
      );
    }
    case "validation":
      return (
        <Alert variant="warning" title="La solicitud no es válida" alertRef={targetRef}>
          <p>
            Revisa la pregunta (no puede estar vacía ni superar 1000 caracteres) e inténtalo de nuevo.
          </p>
          {outcome.title ? <p className="alert__detail">Detalle: {outcome.title}</p> : null}
        </Alert>
      );
    case "unavailable":
      return (
        <Alert
          variant="warning"
          title="Servicio no disponible temporalmente"
          alertRef={targetRef}
          actions={<RetryButton onClick={onRetry} disabled={busy} />}
        >
          <p>El servicio no pudo atender la consulta en este momento. Inténtalo de nuevo en unos instantes.</p>
        </Alert>
      );
    case "server":
      return (
        <Alert
          variant="danger"
          title="Ocurrió un error en el servicio"
          alertRef={targetRef}
          actions={<RetryButton onClick={onRetry} disabled={busy} />}
        >
          <p>No pudimos completar la consulta. Si el problema continúa, comparte este código con soporte.</p>
          {outcome.requestId ? (
            <p className="alert__detail">
              Código de soporte: <span className="mono">{outcome.requestId}</span>
            </p>
          ) : null}
        </Alert>
      );
    case "network":
      return (
        <Alert
          variant="danger"
          title="Sin conexión con la API"
          alertRef={targetRef}
          actions={<RetryButton onClick={onRetry} disabled={busy} />}
        >
          <p>No fue posible comunicarse con el servicio. Revisa tu conexión e inténtalo de nuevo.</p>
        </Alert>
      );
    case "unexpected":
      return (
        <Alert
          variant="danger"
          title="Respuesta no reconocida"
          alertRef={targetRef}
          actions={<RetryButton onClick={onRetry} disabled={busy} />}
        >
          <p>El servicio devolvió una respuesta con un formato que la aplicación no reconoce.</p>
        </Alert>
      );
  }
}

export function TurnView({ turn, busy, onRetry, onCancel }: TurnViewProps): ReactElement {
  const targetRef = useRef<HTMLDivElement | null>(null);
  const phase = turn.status.phase;

  // SPEC-WEB §4.1: el foco va a la nueva respuesta tras recibirla.
  useEffect(() => {
    if (phase === "done") targetRef.current?.focus();
  }, [phase]);

  return (
    <article className="turn" aria-label={`Consulta ${turn.id}`}>
      <MessageQuestion pregunta={turn.pregunta} rol={turn.usuario.rol} />
      {turn.status.phase === "pending" ? (
        <div className="pending">
          <div className="pending__row" role="status">
            <Spinner />
            <span>Consultando a TutorFlow…</span>
            <Button variant="ghost" onClick={() => onCancel(turn.id)}>
              Cancelar
            </Button>
          </div>
          <Skeleton />
        </div>
      ) : (
        <OutcomeView
          outcome={turn.status.outcome}
          turn={turn}
          busy={busy}
          onRetry={() => onRetry(turn.id)}
          targetRef={targetRef}
        />
      )}
    </article>
  );
}
