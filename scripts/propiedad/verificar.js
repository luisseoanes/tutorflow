#!/usr/bin/env node
// Job `propiedad` (FABRICA.md §5.2, T-12): cada commit de agente solo toca las rutas de su trailer `Agente:`
// segun la tabla de scripts/propiedad/rutas.json (transcrita de FABRICA §4.1). Node >= 18, sin dependencias.
//
// Uso:
//   node scripts/propiedad/verificar.js --rango <a..b|sha>   commits del rango (como scripts/lights-out)
//   node scripts/propiedad/verificar.js --todo                toda la historia alcanzable desde HEAD
//   opciones: --base <sha>   commits que son ancestros de <sha> (incluido) no se evaluan (base documentada)
//             --raiz <dir>   repositorio a evaluar
//   node scripts/propiedad/verificar.js --autoprueba          crea repos temporales y comprueba que los casos rojos fallan
//
// Reglas por commit:
//  - sin 'Agente:' ni 'Generado-por:' -> commit humano: no se evalua (se informa).
//  - 'Generado-por:' sin 'Agente:' -> FALLA (commit de agente sin identificar).
//  - 'Agente:' desconocido o repetido con valores distintos -> FALLA.
//  - Archivos tocados fuera de las rutas del agente (o dentro de 'excluidas') -> FALLA.
//  - Merge: solo se evaluan los cambios propios del merge (git diff-tree -c: archivos que difieren de TODOS los padres,
//    es decir, resolucion de conflictos o cambios añadidos); un merge limpio no tiene ninguno.
// Codigo de salida 1 si hay violaciones.

'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

function git(raiz, args, opciones = {}) {
  return execFileSync('git', args, { cwd: raiz, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'], ...opciones });
}

function globARegex(g) {
  let r = '';
  for (let i = 0; i < g.length; i++) {
    const c = g[i];
    if (c === '*' && g[i + 1] === '*') {
      i++;
      if (g[i + 1] === '/') { i++; r += '(?:.*/)?'; } else r += '.*';
    } else if (c === '*') r += '[^/]*';
    else r += c.replace(/[.+?^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp(`^${r}$`);
}

function cargarReglas(raiz) {
  const ruta = path.join(raiz, 'scripts', 'propiedad', 'rutas.json');
  const def = JSON.parse(fs.readFileSync(ruta, 'utf8'));
  const reglas = {};
  for (const [agente, d] of Object.entries(def.agentes)) {
    reglas[agente] = { permitidas: d.permitidas.map(globARegex), excluidas: d.excluidas.map(globARegex) };
  }
  return reglas;
}

function permitido(regla, archivo) {
  return regla.permitidas.some((r) => r.test(archivo)) && !regla.excluidas.some((r) => r.test(archivo));
}

function listarCommits(raiz, opt) {
  const args = ['rev-list', '--topo-order'];
  if (opt.rango) {
    if (opt.rango.includes('..')) args.push(opt.rango); else args.push('-n', '1', opt.rango);
  } else args.push('HEAD');
  if (opt.base) args.push(`^${opt.base}`);
  const out = git(raiz, args).split('\n').filter(Boolean);
  return out;
}

function leerCommit(raiz, sha) {
  const sep = '\u001f';
  const f = `%P${sep}%s${sep}%(trailers:key=Agente,valueonly,unfold,separator=%x1e)${sep}%(trailers:key=Generado-por,valueonly,unfold,separator=%x1e)`;
  const [padres, asunto, agente, generado] = git(raiz, ['log', '-1', `--format=${f}`, sha]).replace(/\n$/, '').split(sep);
  const esMerge = padres.trim().split(' ').filter(Boolean).length > 1;
  // -c: en un merge solo archivos distintos de todos los padres (cambios propios). --no-renames: un borrado cuenta.
  const archivos = git(raiz, ['diff-tree', '--root', '--no-commit-id', '--name-only', '-r', '--no-renames', '-z', ...(esMerge ? ['-c'] : []), sha])
    .split('\0').filter(Boolean);
  const valores = (agente || '').split('\u001e').map((s) => s.trim().toLowerCase()).filter(Boolean);
  return { sha, asunto, esMerge, agentes: [...new Set(valores)], generado: (generado || '').trim() !== '', archivos };
}

function evaluar(raiz, shas, reglas) {
  const violaciones = [];
  const resumen = { agente: 0, humanos: 0, merges: 0, mergesConCambios: 0 };
  for (const sha of shas) {
    const c = leerCommit(raiz, sha);
    const corto = `${sha.slice(0, 10)} ${c.asunto.slice(0, 70)}`;
    if (c.esMerge) resumen.merges++;
    if (c.agentes.length === 0) {
      if (c.generado) violaciones.push(`${corto}: trailer 'Generado-por' sin 'Agente:' (commit de agente sin identificar)`);
      else resumen.humanos++;
      continue;
    }
    if (c.agentes.length > 1) { violaciones.push(`${corto}: varios valores de 'Agente:' (${c.agentes.join(', ')})`); continue; }
    const agente = c.agentes[0];
    const regla = reglas[agente];
    if (!regla) { violaciones.push(`${corto}: 'Agente: ${agente}' desconocido (validos: ${Object.keys(reglas).join(', ')})`); continue; }
    resumen.agente++;
    if (c.esMerge && c.archivos.length) resumen.mergesConCambios++;
    const fuera = c.archivos.filter((a) => !permitido(regla, a));
    if (fuera.length) {
      violaciones.push(`${corto}: 'Agente: ${agente}' toca rutas que no son suyas${c.esMerge ? ' (cambios propios del merge)' : ''}: ${fuera.slice(0, 6).join(', ')}${fuera.length > 6 ? ` (+${fuera.length - 6})` : ''}`);
    }
  }
  return { violaciones, resumen };
}

function parseArgs(argv) {
  const o = { rango: null, todo: false, base: null, raiz: process.cwd(), autoprueba: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--rango') o.rango = argv[++i];
    else if (a === '--todo') o.todo = true;
    else if (a === '--base') o.base = argv[++i];
    else if (a === '--raiz') o.raiz = path.resolve(argv[++i]);
    else if (a === '--autoprueba') o.autoprueba = true;
    else { console.error(`Argumento desconocido: ${a}`); process.exit(2); }
  }
  return o;
}

// ---------- autoprueba: casos rojos y verdes sobre un repo temporal ----------
function autoprueba(raizScripts) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'propiedad-'));
  const g = (...a) => git(tmp, a);
  const escribir = (rel, txt) => { fs.mkdirSync(path.dirname(path.join(tmp, rel)), { recursive: true }); fs.writeFileSync(path.join(tmp, rel), txt); };
  fs.mkdirSync(path.join(tmp, 'scripts', 'propiedad'), { recursive: true });
  fs.copyFileSync(path.join(raizScripts, 'scripts', 'propiedad', 'rutas.json'), path.join(tmp, 'scripts', 'propiedad', 'rutas.json'));
  g('init', '-q', '-b', 'main');
  g('config', 'user.email', 'prueba@example.invalid'); g('config', 'user.name', 'prueba'); g('config', 'commit.gpgsign', 'false');
  const reglas = cargarReglas(tmp);
  let n = 0;
  const caso = (nombre, archivos, trailers, esperaFalla) => {
    for (const a of archivos) escribir(a, `${nombre}\n`);
    g('add', '-A');
    g('commit', '-q', '-m', `prueba: ${nombre}\n\n${trailers.join('\n')}`);
    const sha = g('rev-parse', 'HEAD').trim();
    const { violaciones } = evaluar(tmp, [sha], reglas);
    const fallo = violaciones.length > 0;
    const ok = fallo === esperaFalla;
    console.log(`${ok ? 'OK   ' : 'FALLO'} ${nombre}: ${fallo ? 'viola' : 'pasa'} (esperado: ${esperaFalla ? 'viola' : 'pasa'})`);
    if (!ok) n++;
  };
  const GP = ['Generado-por: Claude Code'];
  caso('core toca qa/', ['qa/x.md'], ['Agente: core', ...GP], true);
  caso('qa toca web/', ['web/x'], ['Agente: qa', ...GP], true);
  caso('qa toca solo qa/', ['qa/revisiones/T-13-r1.md'], ['Agente: qa', ...GP], false);
  caso('core toca web/ y src de Core', ['web/src/a.ts', 'src/TutorFlow.Domain/A.cs'], ['Agente: core', ...GP], false);
  caso('core toca Llm de IA', ['src/TutorFlow.Application/Llm/A.cs'], ['Agente: core', ...GP], true);
  caso('ia toca Llm', ['src/TutorFlow.Application/Llm/A.cs'], ['Agente: ia', ...GP], false);
  caso('arnes toca web/', ['web/Dockerfile'], ['Agente: arnes', ...GP], true);
  caso('arnes toca CI y scripts', ['.github/workflows/ci.yml', 'scripts/x.sh'], ['Agente: arnes', ...GP], false);
  caso('arnes toca logs/README.md', ['logs/README.md'], ['Agente: arnes', ...GP], true);
  caso('orquestador toca codigo', ['src/TutorFlow.Api/P.cs'], ['Agente: orquestador', ...GP], true);
  caso('orquestador toca documento', ['INTERVENCIONES.md'], ['Agente: orquestador', ...GP], false);
  caso('Generado-por sin Agente', ['README.md'], [...GP], true);
  caso('Agente desconocido', ['README.md'], ['Agente: mago', ...GP], true);
  caso('commit humano', ['README.md'], [], false);
  // merge limpio vs merge con cambios propios
  g('checkout', '-q', '-b', 'rama');
  escribir('evals/e.json', '1'); g('add', '-A'); g('commit', '-q', '-m', 'x\n\nAgente: arnes\nGenerado-por: Claude Code');
  g('checkout', '-q', 'main');
  escribir('qa/otro.md', '1'); g('add', '-A'); g('commit', '-q', '-m', 'y\n\nAgente: qa\nGenerado-por: Claude Code');
  g('merge', '--no-ff', '-q', '-m', 'merge: prueba\n\nAgente: orquestador\nGenerado-por: Claude Code', 'rama');
  let { violaciones } = evaluar(tmp, [g('rev-parse', 'HEAD').trim()], reglas);
  console.log(`${violaciones.length === 0 ? 'OK   ' : 'FALLO'} merge limpio del orquestador: ${violaciones.length ? 'viola' : 'pasa'} (esperado: pasa)`);
  if (violaciones.length) n++;
  g('checkout', '-q', '-b', 'rama2');
  escribir('evals/f.json', '1'); g('add', '-A'); g('commit', '-q', '-m', 'z\n\nAgente: arnes\nGenerado-por: Claude Code');
  g('checkout', '-q', 'main');
  g('merge', '--no-ff', '--no-commit', '-q', 'rama2');
  escribir('src/TutorFlow.Api/Colado.cs', 'x'); g('add', '-A');
  g('commit', '-q', '-m', 'merge: con cambio propio\n\nAgente: orquestador\nGenerado-por: Claude Code');
  ({ violaciones } = evaluar(tmp, [g('rev-parse', 'HEAD').trim()], reglas));
  console.log(`${violaciones.length ? 'OK   ' : 'FALLO'} merge del orquestador con cambio propio en src/: ${violaciones.length ? 'viola' : 'pasa'} (esperado: viola)`);
  if (!violaciones.length) n++;
  fs.rmSync(tmp, { recursive: true, force: true });
  console.log(n ? `AUTOPRUEBA: FALLA (${n})` : 'AUTOPRUEBA: OK');
  process.exit(n ? 1 : 0);
}

function main() {
  const opt = parseArgs(process.argv.slice(2));
  if (opt.autoprueba) return autoprueba(path.resolve(__dirname, '..', '..'));
  if (!opt.rango && !opt.todo) { console.error('Indica --rango <rango> o --todo'); process.exit(2); }
  const reglas = cargarReglas(opt.raiz);
  const shas = listarCommits(opt.raiz, opt);
  const { violaciones, resumen } = evaluar(opt.raiz, shas, reglas);
  console.log(`Commits evaluados: ${shas.length} (de agente: ${resumen.agente}, humanos sin trailers: ${resumen.humanos}, merges: ${resumen.merges}, merges con cambios propios: ${resumen.mergesConCambios})`);
  if (opt.base) console.log(`Base documentada: ${opt.base} (sus ancestros no se evaluan)`);
  if (violaciones.length) {
    console.log('VIOLACIONES de propiedad de rutas (FABRICA §4.1):');
    for (const v of violaciones) console.log(` - ${v}`);
    console.log(`RESULTADO: FALLA (${violaciones.length})`);
    process.exit(1);
  }
  console.log('RESULTADO: OK');
}

main();
