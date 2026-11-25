#!/usr/bin/env bash
# Smoke en contenedor de TutorFlow (FABRICA 5.2, job smoke). Requiere docker compose y curl.
# - NO lee .env ni imprime variables; el servicio arranca SIN llave (compose.sin-llave.yml, INT-014).
# - SMOKE_URL permite apuntar a otro puerto (solo para demostrar que el smoke falla si no hay respuesta).
# - Cada marcador "Criterio" lo lee scripts/trazabilidad (AGENTS.md 6.4).
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$RAIZ"

PROYECTO="${SMOKE_PROYECTO:-tutorflow-smoke}"
URL="${SMOKE_URL:-http://localhost:8080}"
LIMITE_SALUD_S=30
dc() { docker compose -p "$PROYECTO" -f docker-compose.yml -f scripts/smoke/compose.sin-llave.yml "$@"; }

fallos=0
ok()   { echo "OK    $*"; }
fallo() { echo "FALLO $*" >&2; fallos=$((fallos + 1)); }

limpiar() {
  local codigo=$?
  if [ "$codigo" -ne 0 ] || [ "$fallos" -ne 0 ]; then
    echo "--- logs de los servicios (el smoke fallo) ---" >&2
    dc logs --no-color --tail 60 >&2 || true
  fi
  dc down -v --remove-orphans >/dev/null 2>&1 || true
}
trap limpiar EXIT

# Estado de salud de un servicio ("healthy" cuando el healthcheck de compose lo da por bueno).
salud_servicio() {
  local id
  id="$(dc ps -q "$1")"
  [ -n "$id" ] || { echo "sin-contenedor"; return; }
  docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}sin-healthcheck{{end}}' "$id"
}

esperar_healthy() { # servicio, segundos
  local limite=$(( SECONDS + $2 ))
  while [ "$SECONDS" -lt "$limite" ]; do
    [ "$(salud_servicio "$1")" = "healthy" ] && return 0
    sleep 1
  done
  return 1
}

# Medicion de RES-01.b: el limite de 30 s corre desde que 'up' crea los contenedores (imagenes ya construidas, 'up --no-build')
# hasta el primer 200 de /api/salud. El build (tutorflow y web) es previo, usa la cache de capas de BuildKit y NO cuenta.
echo "== Construyendo las imagenes (fuera de la medicion de arranque; cache de capas) =="
dc build
dc down -v --remove-orphans >/dev/null 2>&1 || true

# Criterio: RES-01.b
echo "== RES-01.b: compose up en limpio, GET /api/salud 200 en menos de ${LIMITE_SALUD_S} s =="
inicio=$SECONDS
dc up -d --no-build
cuerpo=""
codigo=""
while :; do
  codigo="$(curl -s -o /tmp/smoke-salud.$$ -w '%{http_code}' --max-time 2 "$URL/api/salud" 2>/dev/null || true)"
  [ "$codigo" = "200" ] && { cuerpo="$(cat /tmp/smoke-salud.$$)"; break; }
  [ $(( SECONDS - inicio )) -ge "$LIMITE_SALUD_S" ] && break
  sleep 1
done
rm -f /tmp/smoke-salud.$$
transcurrido=$(( SECONDS - inicio ))
if [ "$codigo" = "200" ] && [ "$cuerpo" = '{"estado":"ok"}' ] && [ "$transcurrido" -lt "$LIMITE_SALUD_S" ]; then
  ok "GET $URL/api/salud -> 200 {\"estado\":\"ok\"} en ${transcurrido} s"
else
  fallo "GET $URL/api/salud no dio 200 con el cuerpo exacto en menos de ${LIMITE_SALUD_S} s (ultimo codigo: ${codigo:-ninguno})"
  exit 1
fi

# Criterio: RES-03.a
echo "== RES-03.a: healthy, puertos y montaje de solo lectura =="
for servicio in tutorflow mock-api web; do
  if esperar_healthy "$servicio" 60; then ok "$servicio healthy"; else fallo "$servicio no llego a healthy (estado: $(salud_servicio "$servicio"))"; fi
done

publicado="$(dc port tutorflow 8080 2>/dev/null || true)"
case "$publicado" in
  *:8080) ok "tutorflow publica 8080 ($publicado)" ;;
  *) fallo "tutorflow no publica 8080 (compose port: '${publicado}')" ;;
esac

id_mock="$(dc ps -q mock-api)"
enlaces="$(docker inspect -f '{{json .HostConfig.PortBindings}}' "$id_mock")"
if [ "$enlaces" = "{}" ] || [ "$enlaces" = "null" ]; then
  ok "mock-api sin puertos publicados en el host (PortBindings=$enlaces)"
else
  fallo "mock-api publica puertos en el host: $enlaces"
fi
puerto_mock="$(dc port mock-api 4000 2>/dev/null || true)"
# Sin puerto publicado compose devuelve vacio o ":0".
if [ -z "$puerto_mock" ] || [ "${puerto_mock##*:}" = "0" ]; then
  ok "docker compose port mock-api 4000 no devuelve ningun puerto del host ('${puerto_mock}')"
else
  fallo "docker compose port mock-api 4000 devuelve un puerto del host: $puerto_mock"
fi

id_tf="$(dc ps -q tutorflow)"
rw="$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/app/knowledge"}}{{.RW}}{{end}}{{end}}' "$id_tf")"
if [ "$rw" = "false" ]; then ok "/app/knowledge montado de solo lectura (RW=false)"; else fallo "/app/knowledge no es de solo lectura (RW='${rw}')"; fi
if dc exec -T tutorflow sh -c 'touch /app/knowledge/.smoke-escritura' >/dev/null 2>&1; then
  fallo "se pudo escribir en /app/knowledge"
else
  ok "escribir en /app/knowledge falla (sistema de archivos de solo lectura)"
fi

# Interfaz web (INT-021; sin criterio propio en escenarios/: no lleva marcador 'Criterio')
echo "== Web: la interfaz web queda healthy y responde en :3000 =="
URL_WEB="${SMOKE_URL_WEB:-http://localhost:3000}"
codigo_web="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$URL_WEB/" || true)"
if [ "$codigo_web" = "200" ]; then ok "GET $URL_WEB/ -> 200"; else fallo "GET $URL_WEB/ -> ${codigo_web:-ninguno} (se esperaba 200)"; fi
codigo_proxy="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$URL_WEB/api/salud" || true)"
if [ "$codigo_proxy" = "200" ]; then ok "GET $URL_WEB/api/salud (proxy a la API) -> 200"; else fallo "proxy /api/salud en :3000 -> ${codigo_proxy:-ninguno}"; fi

# Criterio: RES-03.b
echo "== RES-03.b: sin LLM_API_KEY, AUTH-03 contra el contenedor real -> 403-acceso (L2a, sin LLM) =="
# Comprueba SIN imprimir el valor que el servicio no tiene llave.
if dc exec -T tutorflow sh -c 'test -z "$LLM_API_KEY" && test -z "$EMBEDDING_API_KEY"'; then
  ok "el contenedor no tiene LLM_API_KEY ni EMBEDDING_API_KEY"
else
  fallo "el contenedor tiene una llave definida: el smoke no debe llamar al proveedor"
  exit 1
fi

esperado='{"respuesta":"No tienes permiso para consultar esa información. Solo puedes consultar tus propios datos académicos.","citas":[{"documento":"reglamento_academico_2025.md","seccion":"Artículo 22"}],"usoDatosEstudiante":false,"tokens":0,"costoUsd":0}'
peticion='{"pregunta":"¿Cuál es la nota de Bases de Datos de Andrés Martínez?","usuario":{"rol":"estudiante","id":"EST-001"}}'
archivo="/tmp/smoke-auth03.$$"
codigo="$(curl -s -o "$archivo" -w '%{http_code}' --max-time 20 -H 'Content-Type: application/json' -d "$peticion" "$URL/api/asistente/preguntar" || true)"
tipo="$(curl -s -o /dev/null -w '%{content_type}' --max-time 20 -H 'Content-Type: application/json' -d "$peticion" "$URL/api/asistente/preguntar" || true)"
recibido="$(cat "$archivo" 2>/dev/null || true)"
rm -f "$archivo"
if [ "$codigo" = "403" ] && [ "$recibido" = "$esperado" ]; then
  ok "AUTH-03 -> 403 con el cuerpo 403-acceso exacto"
else
  fallo "AUTH-03: codigo ${codigo:-ninguno}; cuerpo distinto del 403-acceso esperado"
  echo "  recibido: $recibido" >&2
fi
case "$tipo" in
  application/json*) ok "Content-Type $tipo" ;;
  *) fallo "Content-Type inesperado: $tipo" ;;
esac

if [ "$fallos" -ne 0 ]; then
  echo "SMOKE FALLIDO: $fallos comprobacion(es)" >&2
  exit 1
fi
echo "SMOKE OK"
