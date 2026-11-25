#!/usr/bin/env bash
# Peticiones reales, a través del servicio web (:3000, proxy /api), a la API SIN llave del LLM.
# Uso: bash api-real.sh > logs/api-real.txt
B=${WEB_BASE_URL:-http://localhost:3000}
post() { curl -s -i -X POST "$B/api/asistente/preguntar" -H 'Content-Type: application/json' -d "$1"; echo; echo; }
echo '$ GET /api/salud'; curl -s -i "$B/api/salud"; echo; echo
echo '$ POST AUTH-03 (EST-001) -> 403-acceso'
post '{"pregunta":"¿Cuál es la nota de Bases de Datos de Andrés Martínez?","usuario":{"rol":"estudiante","id":"EST-001"}}'
echo '$ POST id inexistente -> 403-identidad'
post '{"pregunta":"hola","usuario":{"rol":"estudiante","id":"EST-999"}}'
echo '$ POST 1001 caracteres -> 400'
LARGA=$(head -c 1001 /dev/zero | tr '\0' 'a')
post "{\"pregunta\":\"$LARGA\",\"usuario\":{\"rol\":\"estudiante\",\"id\":\"EST-001\"}}"
echo '$ POST pregunta normativa sin llave -> 503'
post '{"pregunta":"¿Cuál es el máximo de faltas permitido según el reglamento?","usuario":{"rol":"estudiante","id":"EST-001"}}'
