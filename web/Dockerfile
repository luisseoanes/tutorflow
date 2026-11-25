# TutorFlow Web (SPEC-WEB §9): build con Node -> servidor estático nginx sin privilegios.
# Contexto de build: ./web   (docker build -t tutorflow-web ./web)

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY tsconfig.json vite.config.ts index.html ./
COPY public ./public
COPY src ./src
# El tipado estricto y las pruebas se verifican en CI (npm run build / npm test); la imagen solo empaqueta el bundle.
RUN npx vite build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 3000
HEALTHCHECK --interval=10s --timeout=3s --start-period=3s --retries=5 \
  CMD wget -qO- http://127.0.0.1:3000/healthz || exit 1
