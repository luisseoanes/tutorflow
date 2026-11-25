# Herramientas del frontend en Docker (T-12)

Node y Playwright sin instalar nada en el host (AGENTS.md §10, igual que `scripts/sdk/`).

    docker compose -f scripts/web/compose.yml run --rm node npm ci
    docker compose -f scripts/web/compose.yml run --rm node npx tsc --noEmit
    docker compose -f scripts/web/compose.yml run --rm node npm test
    docker compose -f scripts/web/compose.yml run --rm node npm run build
    docker compose -f scripts/web/compose.yml run --rm playwright sh /work/scripts/web/e2e.sh

- Imagenes fijadas: `node:22-bookworm-slim` (Node 22 como web/Dockerfile; con 22.12 jsdom 31/undici falla al arrancar los workers de Vitest: falta zstd, de Node >= 22.15) y `mcr.microsoft.com/playwright:v1.64.1-noble`.
- **Version de Playwright: `1.64.1`** (la fijada por Core en `web/package.json`, exacta; tag `v1.64.1-noble` verificado con `docker manifest inspect`); si Core necesita otra, Arnés cambia la imagen en este archivo y en el job `web-e2e` a la vez (un cambio de una linea cada uno).
- La raiz del repo se monta en `/work`; el trabajo ocurre en `/work/web`. `node_modules` vive en el volumen `tutorflow-web-tools_web-node-modules` (binarios de Linux, compartido por ambos servicios), no en el host.
- Los `[e2e]` contra la app real: primero `docker compose -f docker-compose.yml -f scripts/smoke/compose.sin-llave.yml up -d --wait` (la API arranca **sin llave**), luego el servicio `playwright`. Se llega a la app por **`host.docker.internal`** (`extra_hosts: host-gateway`, funciona en Docker Desktop y en Linux): variables `WEB_BASE_URL=http://host.docker.internal:3000` (la que lee `playwright.config.ts` de Core; sin ella levanta `vite preview`) y `API_PROXY_TARGET=http://host.docker.internal:8080` (solo para ese `vite preview` de respaldo). No se une la red de compose para que estas herramientas funcionen sin la app levantada.
- No se pasan secretos, `.env`, socket de Docker ni `$HOME`.
- Las rutas que imprime el contenedor empiezan por `/work`.
- **Contexto seguro:** Chromium solo expone `caches`/`serviceWorker` en `localhost`/HTTPS. Con `http://host.docker.internal:3000` la prueba de almacenamiento vacio (`simulado.spec.ts:44`) falla con `Cannot read properties of undefined (reading 'keys')`. Por eso `scripts/web/e2e.sh` levanta `scripts/web/reenviar.mjs` (reenviador TCP en Node, sin dependencias) que expone `localhost:3000` -> `host.docker.internal:3000`, y ejecuta Playwright con `WEB_BASE_URL=http://localhost:3000`.
