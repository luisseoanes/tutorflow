#!/usr/bin/env bash
# Corre UNA combinacion de la bateria [eval] contra la API real (T-10). Gasta cuota del proveedor.
# Uso:   evals/correr-eval.sh <a|b|c> [argumentos del runner, p. ej. --solo q01 --pausa-ms 6000]
#   a = gemini-3.5-flash-lite + full-context (defecto)   b = gemini-3.5-flash-lite + bm25   c = gemini-3.8-flash + full-context
# Entorno:
#   TUTORFLOW_ENV_FILE  ruta de un .env (solo la RUTA; este script no lo lee). Por defecto ./.env junto al compose.
#   EVAL_CI=1           la llave viene del entorno del proceso (secreto de Actions): agrega evals/compose.llave-entorno.yml.
#   EVAL_RUNNER=host    usa 'dotnet' local (por defecto: SDK en Docker, scripts/sdk/compose.yml).
# El runner solo habla HTTP con la API publicada; nunca recibe ni imprime la llave.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RAIZ"

COMBO="${1:-}"
[ -n "$COMBO" ] || { echo "Uso: $0 <a|b|c> [argumentos del runner]" >&2; exit 2; }
shift
case "$COMBO" in
  a) OVERRIDE=evals/compose.a-flash-lite-full-context.yml; MODELO=gemini-3.5-flash-lite; ESTRATEGIA=full-context ;;
  b) OVERRIDE=evals/compose.b-flash-lite-bm25.yml;          MODELO=gemini-3.5-flash-lite; ESTRATEGIA=bm25 ;;
  c) OVERRIDE=evals/compose.c-flash-full-context.yml;       MODELO=gemini-3.8-flash;      ESTRATEGIA=full-context ;;
  *) echo "Combinacion desconocida: $COMBO (a, b o c)" >&2; exit 2 ;;
esac

PROYECTO="${EVAL_PROYECTO:-tutorflow-evals}"
ARCHIVOS=(-f docker-compose.yml -f "$OVERRIDE")
[ "${EVAL_CI:-0}" = "1" ] && ARCHIVOS+=(-f evals/compose.llave-entorno.yml)
dc() { docker compose -p "$PROYECTO" "${ARCHIVOS[@]}" "$@"; }

limpiar() { dc down -v --remove-orphans >/dev/null 2>&1 || true; }
trap limpiar EXIT

COMMIT="$(git rev-parse HEAD)"
echo "== Combinacion $COMBO: modelo=$MODELO estrategia=$ESTRATEGIA =="
dc up -d --build --wait --wait-timeout 180 >/dev/null

if [ "${EVAL_RUNNER:-docker}" = "host" ]; then
  URL="http://localhost:8080"
  dotnet run --project evals/TutorFlow.Evals -- eval --modelo "$MODELO" --estrategia "$ESTRATEGIA" --base-url "$URL" --commit "$COMMIT" "$@"
else
  URL="http://host.docker.internal:8080"
  docker compose -f scripts/sdk/compose.yml run --rm sdk \
    dotnet run --project evals/TutorFlow.Evals -- eval --modelo "$MODELO" --estrategia "$ESTRATEGIA" --base-url "$URL" --commit "$COMMIT" "$@"
fi
