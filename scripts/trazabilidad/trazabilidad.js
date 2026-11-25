#!/usr/bin/env node
// Trazabilidad criterio -> pruebas/casos (AGENTS.md §6.4). Node >= 18, sin dependencias.
//
// Uso:
//   node scripts/trazabilidad/trazabilidad.js [--completo] [--integradas T-01,T-02] [--json <ruta>] [--raiz <dir>]
//
// Modo por defecto: solo exige los criterios de las tareas ya integradas (merge commits `merge: T-xx`
// en la rama actual). `--completo`: exige todos los criterios sin importar la tarea (cierre de F2).
// Salida: matriz en markdown por stdout; JSON opcional con --json. Codigo de salida 1 si falla.
//
// Falla si:
//  (a) una prueba, caso del dataset o marcador declara un id de criterio inexistente (o mal formado);
//  (b) un criterio de nivel bloqueante (dom/app/api/ret/cont) de un escenario Critico/Alto de una tarea
//      exigida no tiene prueba o caso;
//  (c) un criterio [eval] de una tarea exigida no tiene caso en evals/dataset.jsonl;
//  (d) un criterio no tiene tarea asignada o el dataset/asignacion es invalido.

'use strict';
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const NIVELES = ['dom', 'app', 'api', 'ret', 'cont', 'eval'];
const BLOQUEANTES = new Set(['dom', 'app', 'api', 'ret', 'cont']);
const SEVERIDADES_EXIGIDAS = new Set(['Crítica', 'Alta']);
const ID_CRITERIO = /^(AUTH|ACAD|RAG|ADV|RES)-\d{2}\.[a-z]$/;
const CARPETA_POR_NIVEL = {
  dom: ['TutorFlow.Domain.Tests', 'TutorFlow.Architecture.Tests'],
  app: ['TutorFlow.Application.Tests'],
  api: ['TutorFlow.Api.Tests'],
};
const CLAVES_CASO = new Set(['id', 'tipo', 'usuario', 'pregunta', 'cuerpo_crudo', 'criterios', 'esperado']);
const CLAVES_ESPERADO = new Set([
  'status', 'cifras', 'contiene', 'citas', 'citas_prohibidas', 'citas_permitidas', 'chunks_relevantes',
  'chunks_prohibidos', 'abstencion', 'prohibido', 'usoDatosEstudiante', 'estudiantesConsultados', 'nota',
]);

function parseArgs(argv) {
  const o = { completo: false, integradas: null, json: null, raiz: process.cwd() };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--completo') o.completo = true;
    else if (a === '--integradas') o.integradas = argv[++i].split(',').map((s) => s.trim()).filter(Boolean);
    else if (a === '--json') o.json = argv[++i];
    else if (a === '--raiz') o.raiz = path.resolve(argv[++i]);
    else { console.error(`Argumento desconocido: ${a}`); process.exit(2); }
  }
  return o;
}

function walk(dir, filtro, salida = []) {
  if (!fs.existsSync(dir)) return salida;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name === 'bin' || e.name === 'obj' || e.name === 'node_modules' || e.name === '.git') continue;
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, filtro, salida);
    else if (filtro(p)) salida.push(p);
  }
  return salida;
}

// ---------- escenarios ----------
function leerEscenarios(raiz) {
  const criterios = new Map(); // id -> {id, escenario, titulo, severidad, nivel}
  const lapidas = [];
  const dir = path.join(raiz, 'escenarios');
  for (const f of fs.readdirSync(dir).filter((n) => n.endsWith('.md')).sort()) {
    const lineas = fs.readFileSync(path.join(dir, f), 'utf8').split(/\r?\n/);
    let actual = null;
    for (const linea of lineas) {
      const h = linea.match(/^###\s+((?:AUTH|ACAD|RAG|ADV|RES)-\d{2})\s+·\s+(.*)$/);
      if (h) {
        actual = { id: h[1], titulo: h[2].trim(), severidad: null, lapida: /fusionado/i.test(h[2]), archivo: f };
        if (actual.lapida) lapidas.push(actual.id);
        continue;
      }
      if (/^#{1,3}\s/.test(linea)) { actual = null; continue; }
      if (!actual) continue;
      const s = linea.match(/\*\*Severidad:\*\*\s*([^\s·*]+)/);
      if (s && !actual.severidad) { actual.severidad = s[1]; continue; }
      const c = linea.match(/^\s+-\s+([a-z])\.\s+`\[(dom|app|api|ret|cont|eval)\]`\s*(.*)$/);
      if (c && !actual.lapida) {
        if (!actual.severidad) throw new Error(`Escenario ${actual.id} sin severidad antes de sus criterios (${f})`);
        const id = `${actual.id}.${c[1]}`;
        if (criterios.has(id)) throw new Error(`Criterio duplicado: ${id}`);
        criterios.set(id, {
          id, escenario: actual.id, titulo: actual.titulo, severidad: actual.severidad, nivel: c[2],
          texto: c[3].trim().slice(0, 120), archivo: f,
        });
      }
    }
  }
  return { criterios, lapidas };
}

// ---------- pruebas (.cs) ----------
function quitarComentariosCs(src) {
  return src.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ' '))
    .replace(/(^|[^:"'\\])\/\/.*$/gm, '$1');
}

function leerPruebas(raiz) {
  const items = []; // {id, archivo, nombre, proyecto, tipo:'test'}
  const base = path.join(raiz, 'tests');
  for (const f of walk(base, (p) => p.endsWith('.cs'))) {
    const rel = path.relative(raiz, f).split(path.sep).join('/');
    const proyecto = rel.split('/')[1] || '';
    const src = quitarComentariosCs(fs.readFileSync(f, 'utf8'));
    const re = /Trait\(\s*"Criterio"\s*,\s*"([^"]*)"\s*\)/g;
    let m;
    while ((m = re.exec(src)) !== null) {
      const resto = src.slice(m.index, m.index + 800);
      const metodo = resto.match(/\b(?:public|internal)\s+(?:async\s+)?(?:static\s+)?[\w<>\[\],.?]+\s+(\w+)\s*\(/);
      const clase = src.slice(0, m.index).match(/class\s+(\w+)(?![\s\S]*class\s+\w+)/);
      const nombre = `${clase ? clase[1] : path.basename(f, '.cs')}.${metodo ? metodo[1] : '(clase)'}`;
      for (const id of m[1].split(',').map((s) => s.trim())) {
        items.push({ id, fuente: 'test', archivo: rel, nombre, proyecto });
      }
    }
  }
  return items;
}

// ---------- marcadores de smoke (cont) ----------
function leerMarcadores(raiz) {
  const items = [];
  const base = path.join(raiz, 'scripts', 'smoke');
  for (const f of walk(base, () => true)) {
    const rel = path.relative(raiz, f).split(path.sep).join('/');
    const lineas = fs.readFileSync(f, 'utf8').split(/\r?\n/);
    lineas.forEach((l) => {
      const m = l.match(/^\s*(?:#|\/\/)\s*Criterio:\s*(.+)$/);
      if (m) for (const id of m[1].split(/[,\s]+/).filter(Boolean)) {
        items.push({ id, fuente: 'smoke', archivo: rel, nombre: path.basename(f), proyecto: 'smoke' });
      }
    });
  }
  return items;
}

// ---------- dataset ----------
function leerDataset(raiz, errores) {
  const ruta = path.join(raiz, 'evals', 'dataset.jsonl');
  const items = [];
  const casos = [];
  if (!fs.existsSync(ruta)) return { items, casos };
  const ids = new Set();
  fs.readFileSync(ruta, 'utf8').split(/\r?\n/).forEach((linea, i) => {
    if (!linea.trim()) return;
    const donde = `evals/dataset.jsonl:${i + 1}`;
    let caso;
    try { caso = JSON.parse(linea); } catch (e) { errores.push(`${donde}: JSON invalido`); return; }
    if (typeof caso.id !== 'string' || !caso.id) { errores.push(`${donde}: falta id`); return; }
    if (ids.has(caso.id)) errores.push(`${donde}: id de caso duplicado ${caso.id}`);
    ids.add(caso.id);
    for (const k of Object.keys(caso)) if (!CLAVES_CASO.has(k)) errores.push(`${donde} (${caso.id}): clave desconocida '${k}'`);
    if (!Array.isArray(caso.criterios) || caso.criterios.length === 0 || !caso.criterios.every((c) => typeof c === 'string')) {
      errores.push(`${donde} (${caso.id}): 'criterios' debe ser un arreglo no vacio de strings`);
      return;
    }
    if (caso.tipo !== 'meta') {
      const u = caso.usuario;
      if (!u || !['estudiante', 'coordinador'].includes(u.rol) || typeof u.id !== 'string') errores.push(`${donde} (${caso.id}): usuario{rol,id} invalido`);
      if (typeof caso.pregunta !== 'string' || !caso.pregunta) errores.push(`${donde} (${caso.id}): falta pregunta`);
    }
    if (!caso.esperado || typeof caso.esperado !== 'object') errores.push(`${donde} (${caso.id}): falta esperado`);
    else for (const k of Object.keys(caso.esperado)) if (!CLAVES_ESPERADO.has(k)) errores.push(`${donde} (${caso.id}): esperado.${k} desconocido`);
    casos.push(caso);
    for (const id of caso.criterios) items.push({ id, fuente: 'dataset', archivo: 'evals/dataset.jsonl', nombre: caso.id, proyecto: 'dataset' });
  });
  return { items, casos };
}

// ---------- asignacion ----------
function resolverAsignacion(raiz, criterios, errores) {
  const asig = JSON.parse(fs.readFileSync(path.join(raiz, 'scripts', 'trazabilidad', 'asignacion.json'), 'utf8'));
  const tareaDe = new Map();
  for (const [tarea, def] of Object.entries(asig.tareas)) {
    for (const id of def.criterios) {
      if (!criterios.has(id)) { errores.push(`asignacion.json: ${tarea} lista un criterio inexistente ${id}`); continue; }
      if (tareaDe.has(id)) errores.push(`asignacion.json: ${id} asignado a ${tareaDe.get(id)} y a ${tarea}`);
      else tareaDe.set(id, tarea);
    }
  }
  for (const c of criterios.values()) {
    if (tareaDe.has(c.id)) continue;
    const regla = asig.reglas.find((r) => [].concat(r.si_nivel).includes(c.nivel));
    if (regla) tareaDe.set(c.id, regla.tarea);
    else errores.push(`${c.id}: sin tarea asignada (nivel ${c.nivel})`);
  }
  return tareaDe;
}

function tareasIntegradas(raiz) {
  let salida = '';
  try {
    salida = execFileSync('git', ['log', '--merges', '--format=%s'], { cwd: raiz, encoding: 'utf8' });
  } catch (e) {
    throw new Error(`No se pudo leer 'git log --merges': ${e.message}`);
  }
  const set = new Set();
  for (const asunto of salida.split(/\r?\n/)) {
    const m = asunto.match(/^merge:\s*(.*)$/i);
    if (!m) continue;
    for (const t of m[1].match(/\bT-\d{2}[a-e]?\b/g) || []) set.add(t);
    // Un asunto 'merge: T-05a ...' integra solo lo que nombra; no se infieren rangos.
  }
  return [...set].sort();
}

// ---------- principal ----------
function main() {
  const opt = parseArgs(process.argv.slice(2));
  const errores = []; // fallos duros
  const avisos = [];

  const { criterios, lapidas } = leerEscenarios(opt.raiz);
  const tareaDe = resolverAsignacion(opt.raiz, criterios, errores);
  const pruebas = leerPruebas(opt.raiz);
  const marcadores = leerMarcadores(opt.raiz);
  const { items: delDataset, casos } = leerDataset(opt.raiz, errores);

  const integradas = opt.completo ? null : (opt.integradas || tareasIntegradas(opt.raiz));
  const exigida = (tarea) => opt.completo || integradas.includes(tarea);

  // (a) ids inexistentes
  const evidencia = new Map(); // id -> [items]
  for (const it of [...pruebas, ...marcadores, ...delDataset]) {
    if (!ID_CRITERIO.test(it.id)) { errores.push(`(a) ${it.fuente} ${it.archivo} ${it.nombre}: id de criterio mal formado '${it.id}'`); continue; }
    if (!criterios.has(it.id)) {
      const lap = lapidas.includes(it.id.split('.')[0]) ? ' (escenario fusionado: lapida)' : '';
      errores.push(`(a) ${it.fuente} ${it.archivo} ${it.nombre}: el criterio '${it.id}' no existe en escenarios/${lap}`);
      continue;
    }
    if (!evidencia.has(it.id)) evidencia.set(it.id, []);
    evidencia.get(it.id).push(it);
  }

  // (b)/(c) cobertura
  const filas = [];
  for (const c of criterios.values()) {
    const tarea = tareaDe.get(c.id);
    const ev = evidencia.get(c.id) || [];
    const fuentesValidas = c.nivel === 'eval' ? ['dataset']
      : c.nivel === 'ret' ? ['dataset', 'test']
        : c.nivel === 'cont' ? ['test', 'smoke']
          : ['test'];
    const validas = ev.filter((e) => fuentesValidas.includes(e.fuente));
    const exigidoAqui = exigida(tarea);
    let estado;
    if (c.nivel === 'eval') {
      if (validas.length) estado = 'OK';
      else if (exigidoAqui) { estado = 'FALTA'; errores.push(`(c) ${c.id} [eval] (${tarea}, integrada) sin caso en evals/dataset.jsonl`); }
      else estado = 'pendiente (tarea no integrada)';
    } else if (BLOQUEANTES.has(c.nivel) && SEVERIDADES_EXIGIDAS.has(c.severidad)) {
      if (validas.length) estado = 'OK';
      else if (exigidoAqui) { estado = 'FALTA'; errores.push(`(b) ${c.id} [${c.nivel}] ${c.severidad} (${tarea}, integrada) sin prueba ni caso`); }
      else estado = 'pendiente (tarea no integrada)';
    } else {
      estado = validas.length ? 'OK' : 'no exigido (severidad)';
    }
    // aviso de nivel/proyecto
    if (CARPETA_POR_NIVEL[c.nivel]) {
      for (const e of validas) {
        if (e.fuente === 'test' && !CARPETA_POR_NIVEL[c.nivel].includes(e.proyecto)) {
          avisos.push(`${c.id} [${c.nivel}] probado en ${e.proyecto} (se esperaba ${CARPETA_POR_NIVEL[c.nivel].join(' o ')})`);
        }
      }
    }
    filas.push({
      criterio: c.id, nivel: c.nivel, severidad: c.severidad, tarea, tareaExigida: exigidoAqui, estado,
      evidencias: validas.map((e) => `${e.fuente}:${e.archivo}${e.fuente === 'test' ? '::' + e.nombre : ' ' + e.nombre}`),
    });
  }

  const resumen = {
    modo: opt.completo ? 'completo' : 'tareas-integradas',
    tareasIntegradas: integradas,
    totalCriterios: filas.length,
    ok: filas.filter((f) => f.estado === 'OK').length,
    falta: filas.filter((f) => f.estado === 'FALTA').length,
    pendientes: filas.filter((f) => f.estado.startsWith('pendiente')).length,
    noExigidos: filas.filter((f) => f.estado.startsWith('no exigido')).length,
    casosDataset: casos.length,
    errores: errores.length,
  };

  // markdown
  const out = [];
  out.push('# Matriz de trazabilidad', '');
  out.push(`Modo: **${resumen.modo}**` + (integradas ? ` · tareas integradas: ${integradas.length ? integradas.join(', ') : '(ninguna)'}` : ''), '');
  out.push('| Criterio | Nivel | Severidad | Tarea | Estado | Pruebas / casos |', '|---|---|---|---|---|---|');
  for (const f of filas) {
    out.push(`| ${f.criterio} | ${f.nivel} | ${f.severidad} | ${f.tarea}${f.tareaExigida ? ' *' : ''} | ${f.estado} | ${f.evidencias.join('<br>') || '-'} |`);
  }
  out.push('', '`*` = tarea exigida en este modo.', '');
  out.push('## Resumen', '');
  out.push(`- Criterios: ${resumen.totalCriterios} · con prueba/caso: ${resumen.ok} · FALTA: ${resumen.falta} · pendientes (tarea no integrada): ${resumen.pendientes} · no exigidos: ${resumen.noExigidos}`);
  out.push(`- Casos en el dataset: ${resumen.casosDataset} · pruebas con Trait Criterio: ${pruebas.length}`);
  if (avisos.length) { out.push('', '## Avisos (no bloquean)', ...avisos.map((a) => `- ${a}`)); }
  if (errores.length) { out.push('', '## ERRORES (bloquean)', ...errores.map((e) => `- ${e}`)); }
  out.push('', errores.length ? `RESULTADO: FALLA (${errores.length} error(es))` : 'RESULTADO: OK');
  console.log(out.join('\n'));

  if (opt.json) {
    fs.mkdirSync(path.dirname(path.resolve(opt.json)), { recursive: true });
    fs.writeFileSync(opt.json, JSON.stringify({ resumen, filas, avisos, errores }, null, 2) + '\n');
  }
  process.exit(errores.length ? 1 : 0);
}

main();
