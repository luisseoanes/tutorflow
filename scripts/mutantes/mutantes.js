#!/usr/bin/env node
// Infraestructura de mutantes (FABRICA.md §5.3). Node >= 18, sin dependencias.
//
// Uso:
//   node scripts/mutantes/mutantes.js [--completo] [--solo M1,M3] [--integradas T-02,T-03]
//                                     [--catalogo <ruta>] [--listar] [--json <ruta>] [--raiz <dir>]
//
// Para cada mutante del catalogo: si su tarea esta integrada, aplica UN parche (buscar/reemplazar exacto) en sitio,
// ejecuta `dotnet test --filter` con los criterios que deben matarlo, y SIEMPRE restaura el archivo.
// Resultado por mutante:
//   MUERTO     alguna prueba de los criterios fallo con el mutante aplicado.
//   SOBREVIVE  las pruebas pasaron con el mutante aplicado (el arnes no lo detecta).
//   PENDIENTE  la tarea que introduce el codigo no esta integrada (merge `merge: T-xx`) y el parche no es aplicable.
//   FALLA      tarea integrada pero parche ausente/no aplicable (catalogo desactualizado), el mutante no compila
//              o ningun criterio listado tiene pruebas (nada se ejecuto).
// Codigo de salida 1 si algun mutante aplicable SOBREVIVE o FALLA. Con --completo (cierre de F2) PENDIENTE tambien falla.
//
// Comando dotnet: variable DOTNET_CMD (por defecto `dotnet`). Ejemplo local con el SDK en Docker:
//   DOTNET_CMD="docker compose -f scripts/sdk/compose.yml run --rm sdk dotnet" node scripts/mutantes/mutantes.js
//
// Solo en la corrida de mutantes se pasa -p:TreatWarningsAsErrors=false: varios mutantes dejan codigo inalcanzable
// (p. ej. M7 `return Permitir()` antes del `switch`), que Directory.Build.props convierte en error CS0162. Con eso el
// mutante compila y son las pruebas, no el compilador, quienes deben detectarlo. La compilacion normal (build-test)
// sigue con warnings como errores.

'use strict';
const fs = require('fs');
const path = require('path');
const { execFileSync, spawnSync } = require('child_process');

function parseArgs(argv) {
  const o = { completo: false, solo: null, integradas: null, catalogo: null, listar: false, json: null, raiz: process.cwd() };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--completo') o.completo = true;
    else if (a === '--solo') o.solo = argv[++i].split(',').map((s) => s.trim()).filter(Boolean);
    else if (a === '--integradas') o.integradas = argv[++i].split(',').map((s) => s.trim()).filter(Boolean);
    else if (a === '--catalogo') o.catalogo = argv[++i];
    else if (a === '--listar') o.listar = true;
    else if (a === '--json') o.json = argv[++i];
    else if (a === '--raiz') o.raiz = path.resolve(argv[++i]);
    else { console.error(`Argumento desconocido: ${a}`); process.exit(2); }
  }
  return o;
}

function git(raiz, args) {
  return execFileSync('git', args, { cwd: raiz, encoding: 'utf8' });
}

// Misma regla que scripts/trazabilidad: asuntos `merge: T-xx ...` en `git log --merges`.
function tareasIntegradas(raiz) {
  const set = new Set();
  for (const asunto of git(raiz, ['log', '--merges', '--format=%s']).split(/\r?\n/)) {
    const m = asunto.match(/^merge:\s*(.*)$/i);
    if (!m) continue;
    for (const t of m[1].match(/\bT-\d{2}[a-e]?\b/g) || []) set.add(t);
  }
  return [...set].sort();
}

function arbolLimpio(raiz) {
  return git(raiz, ['status', '--porcelain']).trim() === '';
}

function contar(texto, sub) {
  let n = 0;
  for (let i = texto.indexOf(sub); i !== -1; i = texto.indexOf(sub, i + sub.length)) n++;
  return n;
}

// Devuelve { ok, motivo, nuevos: Map(ruta -> contenido) } sin tocar el disco.
function prepararParches(raiz, mut) {
  if (!Array.isArray(mut.parches) || mut.parches.length === 0) {
    return { ok: false, motivo: 'el catalogo no define parche para este mutante' };
  }
  const nuevos = new Map();
  for (const p of mut.parches) {
    const ruta = path.join(raiz, p.archivo);
    if (!fs.existsSync(ruta)) return { ok: false, motivo: `archivo inexistente: ${p.archivo}` };
    const actual = nuevos.has(ruta) ? nuevos.get(ruta) : fs.readFileSync(ruta, 'utf8');
    const n = contar(actual, p.buscar);
    if (n === 0) return { ok: false, motivo: `patron no encontrado en ${p.archivo}` };
    if (n > 1 && !p.todas) return { ok: false, motivo: `patron ambiguo (${n} apariciones) en ${p.archivo}; use "todas": true o un patron unico` };
    if (p.buscar === p.reemplazar) return { ok: false, motivo: 'el parche no cambia nada' };
    nuevos.set(ruta, actual.split(p.buscar).join(p.reemplazar));
  }
  return { ok: true, nuevos };
}

function ejecutarPruebas(raiz, criterios, dotnetCmd) {
  const partes = dotnetCmd.trim().split(/\s+/);
  const filtro = criterios.map((c) => `Criterio=${c}`).join('|');
  const args = [...partes.slice(1), 'test', 'TutorFlow.sln', '--filter', filtro, '-p:TreatWarningsAsErrors=false', '--nologo'];
  const r = spawnSync(partes[0], args, { cwd: raiz, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  const salida = `${r.stdout || ''}\n${r.stderr || ''}`;
  let fallidas = 0;
  let pasadas = 0;
  let resumenes = 0;
  for (const m of salida.matchAll(/(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)/g)) {
    fallidas += Number(m[1]);
    pasadas += Number(m[2]);
    resumenes++;
  }
  return { codigo: r.status, salida, fallidas, pasadas, resumenes, filtro };
}

function ultimasLineas(texto, n) {
  return texto.split(/\r?\n/).filter((l) => l.trim()).slice(-n).join('\n');
}

function main() {
  const opt = parseArgs(process.argv.slice(2));
  const rutaCatalogo = opt.catalogo ? path.resolve(opt.raiz, opt.catalogo) : path.join(__dirname, 'catalogo.json');
  const catalogo = JSON.parse(fs.readFileSync(rutaCatalogo, 'utf8'));
  const dotnetCmd = process.env.DOTNET_CMD || 'dotnet';
  const integradas = opt.integradas || tareasIntegradas(opt.raiz);
  const mutantes = catalogo.mutantes.filter((m) => !opt.solo || opt.solo.includes(m.id));

  if (opt.listar) {
    for (const m of mutantes) console.log(`${m.id}\t${m.tarea}\t${integradas.includes(m.tarea) ? 'integrada' : 'no integrada'}\t${m.descripcion}`);
    return 0;
  }
  if (opt.solo) {
    const desconocidos = opt.solo.filter((id) => !catalogo.mutantes.some((m) => m.id === id));
    if (desconocidos.length) { console.error(`Mutantes desconocidos: ${desconocidos.join(', ')}`); return 2; }
  }
  if (!arbolLimpio(opt.raiz)) {
    console.error('El arbol de trabajo no esta limpio (git status --porcelain). Confirme o descarte los cambios antes de correr mutantes.');
    return 2;
  }

  console.log(`Tareas integradas: ${integradas.join(', ') || '(ninguna)'} · modo: ${opt.completo ? 'completo (PENDIENTE falla)' : 'tareas integradas'}`);
  console.log(`Comando dotnet: ${dotnetCmd}\n`);

  const resultados = [];
  let interrumpido = false;
  let restaurar = () => {};
  for (const sig of ['SIGINT', 'SIGTERM']) {
    process.on(sig, () => { interrumpido = true; restaurar(); process.exit(130); });
  }

  for (const mut of mutantes) {
    const res = { id: mut.id, tarea: mut.tarea, descripcion: mut.descripcion, criterios: mut.criterios, estado: '', detalle: '' };
    const integrada = integradas.includes(mut.tarea);
    const prep = prepararParches(opt.raiz, mut);
    if (!prep.ok) {
      if (integrada) {
        res.estado = 'FALLA';
        res.detalle = `tarea ${mut.tarea} integrada pero el parche no aplica (catalogo desactualizado): ${prep.motivo}`;
      } else {
        res.estado = 'PENDIENTE';
        res.detalle = `tarea ${mut.tarea} no integrada (${prep.motivo})`;
      }
    } else if (!integrada) {
      // El codigo existe pero la tarea no consta como integrada: no se cuenta como aprobado.
      res.estado = 'PENDIENTE';
      res.detalle = `tarea ${mut.tarea} no integrada (sin merge en la rama)`;
    } else {
      const originales = new Map([...prep.nuevos.keys()].map((r) => [r, fs.readFileSync(r, 'utf8')]));
      restaurar = () => { for (const [r, c] of originales) fs.writeFileSync(r, c); };
      try {
        for (const [r, c] of prep.nuevos) fs.writeFileSync(r, c);
        const t = ejecutarPruebas(opt.raiz, mut.criterios, dotnetCmd);
        if (t.resumenes === 0 || t.pasadas + t.fallidas === 0) {
          res.estado = 'FALLA';
          res.detalle = /error (CS|MSB)\d+/.test(t.salida)
            ? `el mutante no compila:\n${ultimasLineas(t.salida, 6)}`
            : `ninguna prueba ejecutada con el filtro ${t.filtro}:\n${ultimasLineas(t.salida, 6)}`;
        } else if (t.fallidas > 0) {
          res.estado = 'MUERTO';
          res.detalle = `${t.fallidas} prueba(s) fallaron, ${t.pasadas} pasaron (filtro ${t.filtro})`;
        } else {
          res.estado = 'SOBREVIVE';
          res.detalle = `${t.pasadas} prueba(s) pasaron con el mutante aplicado (filtro ${t.filtro})`;
        }
      } finally {
        restaurar();
        restaurar = () => {};
      }
      if (!arbolLimpio(opt.raiz)) {
        console.error(`El arbol quedo sucio tras ${mut.id}; se restaura con git checkout.`);
        for (const r of originales.keys()) execFileSync('git', ['checkout', '--', path.relative(opt.raiz, r)], { cwd: opt.raiz });
        if (!arbolLimpio(opt.raiz)) { console.error('No se pudo restaurar el arbol.'); return 2; }
        res.estado = 'FALLA';
        res.detalle += ' | el arbol quedo sucio tras la restauracion';
      }
    }
    resultados.push(res);
    console.log(`${res.id.padEnd(4)} ${res.estado.padEnd(10)} ${res.tarea}  ${res.descripcion}\n       ${res.detalle.replace(/\n/g, '\n       ')}`);
  }
  if (interrumpido) return 130;

  const cuenta = (e) => resultados.filter((r) => r.estado === e).length;
  console.log(`\nResumen: MUERTO ${cuenta('MUERTO')} · SOBREVIVE ${cuenta('SOBREVIVE')} · PENDIENTE ${cuenta('PENDIENTE')} · FALLA ${cuenta('FALLA')} (de ${resultados.length})`);
  const malos = resultados.filter((r) => r.estado === 'SOBREVIVE' || r.estado === 'FALLA' || (opt.completo && r.estado === 'PENDIENTE'));
  if (opt.json) {
    fs.writeFileSync(path.resolve(opt.raiz, opt.json), JSON.stringify({ integradas, completo: opt.completo, resultados }, null, 2) + '\n');
  }
  if (malos.length) {
    console.log(`RESULTADO: FALLA (${malos.map((r) => `${r.id} ${r.estado}`).join(', ')})`);
    return 1;
  }
  console.log('RESULTADO: OK' + (cuenta('PENDIENTE') ? ' (hay mutantes PENDIENTES: no cuentan como aprobados)' : ''));
  return 0;
}

try {
  process.exitCode = main();
} catch (e) {
  console.error('ERROR de mutantes: ' + e.message);
  process.exitCode = 2;
}
