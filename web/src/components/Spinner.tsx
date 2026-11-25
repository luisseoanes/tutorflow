import type { ReactElement } from "react";

/** Indicador de carga decorativo; el anuncio accesible lo da el texto de estado que lo acompaña. */
export function Spinner(): ReactElement {
  return <span className="spinner" aria-hidden="true" />;
}

interface SkeletonProps {
  lines?: number;
}

/** Marcador de posición mientras llega la respuesta. */
export function Skeleton({ lines = 3 }: SkeletonProps): ReactElement {
  return (
    <div className="skeleton" aria-hidden="true">
      {Array.from({ length: lines }, (_, i) => (
        <span key={i} className="skeleton__line" />
      ))}
    </div>
  );
}
