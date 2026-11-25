#!/bin/sh
# Ejecuta los [e2e] de Playwright (Core) dentro del contenedor playwright de scripts/web/compose.yml.
# La app (compose principal, puerto 3000) se reenvia a localhost para que Chromium la trate como contexto seguro.
set -eu
node /work/scripts/web/reenviar.mjs 3000 host.docker.internal:3000 &
npm ci --no-audit --no-fund
WEB_BASE_URL=http://localhost:3000 npx playwright test "$@"
