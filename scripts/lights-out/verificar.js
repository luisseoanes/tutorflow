#!/usr/bin/env node
// Regla lights-out (AGENTS.md §9.4): un commit que modifica archivos fuera de
// `*.md`, `.claude/**`, `.gitignore`, `.env.example` y `.gitleaks.toml` debe tener el trailer
// `Generado-por: Claude Code` en su bloque de trailers (git interpret-trailers --parse).
//
// Uso: node scripts/lights-out/verificar.js [--rango <rev-range>]   (por defecto: todo HEAD)
//      Ejemplos de rango: main..HEAD · <base>..<head>
// Salida: lista cada commit evaluado; exit 1 si alguno incumple.

'use strict';
const { execFileSync } = require('child_process');

function git(args, input) {
  return execFileSync('git', args, { encoding: 'utf8', input, maxBuffer: 64 * 1024 * 1024 });
}

const EXENTOS_EXACTOS = new Set(['.gitignore', '.env.example', '.gitleaks.toml']);
function exento(ruta) {
  return ruta.endsWith('.md') || ruta.startsWith('.claude/') || EXENTOS_EXACTOS.has(ruta);
}

function tieneTrailer(sha) {
  const cuerpo = git(['log', '-1', '--format=%B', sha]);
  const trailers = git(['interpret-trailers', '--parse'], cuerpo);
  return trailers.split(/\r?\n/).some((l) => /^Generado-por:\s*Claude Code\s*$/.test(l));
}

function archivosTocados(sha) {
  const padres = git(['rev-list', '--parents', '-n', '1', sha]).trim().split(' ').length - 1;
  // En un merge solo cuentan los archivos que difieren de todos los padres (resolucion propia del merge).
  const args = padres > 1
    ? ['diff-tree', '--no-commit-id', '--name-only', '-r', '-z', '--cc', sha]
    : ['diff-tree', '--no-commit-id', '--name-only', '-r', '-z', '--root', sha];
  return git(args).split('\0').filter(Boolean);
}

function main() {
  const i = process.argv.indexOf('--rango');
  const rango = i > -1 ? process.argv[i + 1] : 'HEAD';
  const shas = git(['rev-list', '--reverse', rango]).split('\n').filter(Boolean);
  let fallos = 0;
  for (const sha of shas) {
    const asunto = git(['log', '-1', '--format=%s', sha]).trim();
    const fuera = archivosTocados(sha).filter((f) => !exento(f));
    if (fuera.length === 0) {
      console.log(`ok     ${sha.slice(0, 7)} sin archivos de codigo: ${asunto}`);
    } else if (tieneTrailer(sha)) {
      console.log(`ok     ${sha.slice(0, 7)} con trailer: ${asunto}`);
    } else {
      fallos++;
      console.log(`FALLA  ${sha.slice(0, 7)} sin trailer 'Generado-por: Claude Code' y toca: ${fuera.slice(0, 5).join(', ')}${fuera.length > 5 ? ', ...' : ''}  (${asunto})`);
    }
  }
  console.log(`\nCommits evaluados: ${shas.length} · incumplimientos: ${fallos}`);
  console.log(fallos ? 'RESULTADO: FALLA' : 'RESULTADO: OK');
  process.exit(fallos ? 1 : 0);
}

main();
