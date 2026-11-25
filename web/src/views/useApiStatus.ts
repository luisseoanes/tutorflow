import { useCallback, useEffect, useState } from "react";
import type { ApiStatus } from "../components/Surfaces";
import { getSalud } from "../services/apiClient";

/** Sondeo de GET /api/salud (SPEC-WEB §4.4): al cargar y cada 30 s; en pausa con la pestaña oculta. */
export const SALUD_INTERVAL_MS = 30_000;

export function useApiStatus(intervalMs: number = SALUD_INTERVAL_MS): { status: ApiStatus; markDown: () => void } {
  const [status, setStatus] = useState<ApiStatus>("checking");

  const check = useCallback(async (signal?: AbortSignal): Promise<void> => {
    const outcome = await getSalud(signal ? { signal } : {});
    if (signal?.aborted) return;
    setStatus(outcome.kind === "ok" ? "ok" : "down");
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let timer: ReturnType<typeof setInterval> | null = null;

    const start = (): void => {
      if (timer !== null) return;
      timer = setInterval(() => void check(controller.signal), intervalMs);
    };
    const stop = (): void => {
      if (timer === null) return;
      clearInterval(timer);
      timer = null;
    };
    const onVisibility = (): void => {
      if (document.visibilityState === "visible") {
        void check(controller.signal);
        start();
      } else {
        stop();
      }
    };

    void check(controller.signal);
    if (document.visibilityState !== "hidden") start();
    document.addEventListener("visibilitychange", onVisibility);
    return () => {
      controller.abort();
      stop();
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [check, intervalMs]);

  const markDown = useCallback(() => setStatus("down"), []);
  return { status, markDown };
}
