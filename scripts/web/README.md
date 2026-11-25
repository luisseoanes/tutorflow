# Herramientas del frontend en Docker (T-12)

Node y Playwright sin instalar nada en el host (AGENTS.md §10, igual que `scripts/sdk/`).

    docker compose -f scripts/web/compose.yml run --rm node npm ci
    docker compose -f scripts/web/compose.yml run --rm node npx tsc --noEmit
    docker compose -f scripts/web/compose.yml run --rm node npm test
    docker compose -f scripts/web/compose.yml run --rm node npm run build
    docker compose -f scripts/web/compose.yml run --rm playwright npx playwright test

- Imagenes fijadas: `node:22.12.0-bookworm-slim` y `mcr.microsoft.com/playwright:v1.49.1-noble`.
- **Version acordada de Playwright: `1.49.1`.** `web/package.json` (Core) debe fijar `"@playwright/test": "1.49.1"` exacta; si Core necesita otra, Arnés cambia la imagen en este archivo y en el job `web-e2e` a la vez (un cambio de una linea cada uno).
- La raiz del repo se monta en `/work`; el trabajo ocurre en `/work/web`. `node_modules` vive en el volumen `tutorflow-web-tools_web-node-modules` (binarios de Linux, compartido por ambos servicios), no en el host.
- Los `[e2e]` contra la app real: primero `docker compose -f docker-compose.yml -f scripts/smoke/compose.sin-llave.yml --profile web up -d --wait` (la API arranca **sin llave**), luego el servicio `playwright`. Se llega a la app por **`host.docker.internal`** (`extra_hosts: host-gateway`, funciona en Docker Desktop y en Linux): variables `E2E_BASE_URL=http://host.docker.internal:3000` y `E2E_API_URL=http://host.docker.internal:8080`, que `playwright.config.ts` (Core) debe leer, con `http://localhost:3000` como valor por defecto fuera de Docker. No se une la red de compose para que estas herramientas funcionen sin la app levantada.
- No se pasan secretos, `.env`, socket de Docker ni `$HOME`.
- Las rutas que imprime el contenedor empiezan por `/work`.
