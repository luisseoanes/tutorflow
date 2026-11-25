#!/usr/bin/env node
// Uso: node scripts/sanitizar-logs/index.mjs --origen <dir> [--destino logs] [--paralelas bb78739b,...]
// Exporta TODAS las sesiones de <origen> (JSONL de la raíz y subagentes en <id>/agent-*.jsonl)
// a <destino>/<AAAA-MM-DD>_<fase>_<rol>_<id-corto>.jsonl + .md, y escribe <destino>/MANIFIESTO.md.
import { readFileSync, writeFileSync, readdirSync, existsSync, statSync, mkdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, basename } from 'node:path';
import { sanitizarJsonl, TIPOS } from './sanitizar.mjs';

const args = process.argv.slice(2);
const opt = (n, d) => { const i = args.indexOf('--' + n); return i >= 0 ? args[i + 1] : d; };
const origen = opt('origen', 'logs/raw');
const destino = opt('destino', 'logs');
const paralelas = (opt('paralelas', 'bb78739b') || '').split(',').filter(Boolean);

const sesiones = [];
for (const f of readdirSync(origen).sort()) {
  const p = join(origen, f);
  if (f.endsWith('.jsonl') && statSync(p).isFile()) {
    const id = basename(f, '.jsonl');
    const paralela = paralelas.some((x) => id.startsWith(x));
    sesiones.push({ ruta: p, id, rol: paralela ? 'candidato-paralela' : 'orquestador', fase: paralela ? 'F2' : 'F0-F2',
      nota: paralela ? 'sesión paralela del candidato (no es un agente de la fábrica)'
        : 'sesión del orquestador (F0–F2; creció durante F2)' });
  } else if (statSync(p).isDirectory()) {
    for (const a of readdirSync(p).sort()) {
      if (!/^agent-.*\.jsonl$/.test(a)) continue;
      const id = a.replace(/^agent-/, '').replace(/\.jsonl$/, '');
      const metaP = join(p, a.replace(/\.jsonl$/, '.meta.json'));
      const meta = existsSync(metaP) ? JSON.parse(readFileSync(metaP, 'utf8')) : {};
      sesiones.push({ ruta: join(p, a), id, rol: meta.agentType || 'desconocido', fase: 'F2', nota: meta.description || '' });
    }
  }
}

mkdirSync(destino, { recursive: true });
const filas = [];
for (const s of sesiones) {
  const bruto = readFileSync(s.ruta);
  const sha = createHash('sha256').update(bruto).digest('hex');
  const texto = bruto.toString('utf8');
  const { salida, conteo } = sanitizarJsonl(texto);
  const lineas = texto.split('\n').filter(Boolean).map((l) => JSON.parse(l));
  const ts = lineas.map((l) => l.timestamp).filter(Boolean).sort();
  const fecha = (ts[0] || '0000-00-00').slice(0, 10);
  const nombre = `${fecha}_${s.fase}_${s.rol}_${s.id.slice(0, 8)}`;
  writeFileSync(join(destino, nombre + '.jsonl'), salida);

  const tokens = { entrada: 0, salida: 0, cacheLectura: 0, cacheCreacion: 0 };
  let mensajes = 0, herramientas = 0;
  for (const l of lineas) {
    const u = l.message?.usage;
    if (u) {
      tokens.entrada += u.input_tokens || 0; tokens.salida += u.output_tokens || 0;
      tokens.cacheLectura += u.cache_read_input_tokens || 0; tokens.cacheCreacion += u.cache_creation_input_tokens || 0;
    }
    if (l.type === 'user' || l.type === 'assistant') mensajes++;
    if (Array.isArray(l.message?.content)) for (const c of l.message.content) if (c.type === 'tool_use') herramientas++;
  }
  const primero = lineas.find((l) => l.type === 'user' && l.message);
  let tarea = '';
  if (primero) {
    const c = primero.message.content;
    tarea = (typeof c === 'string' ? c : (c.find?.((x) => x.type === 'text')?.text || '')).split('\n').find((x) => x.trim()) || '';
  }
  const tareaSegura = JSON.parse(sanitizarJsonl(JSON.stringify({ t: tarea.slice(0, 200) })).salida).t.replace(/\|/g, '/');
  const md = [
    `# ${nombre}`, '',
    `- Sesión: \`${s.id}\``, `- Rol: ${s.rol}`, `- Fase: ${s.fase}`, `- Descripción: ${s.nota}`,
    `- Inicio: ${ts[0] || 'n/d'}  ·  Fin: ${ts.at(-1) || 'n/d'}`,
    `- Líneas: ${lineas.length}  ·  Mensajes (user/assistant): ${mensajes}  ·  Llamadas a herramientas: ${herramientas}`,
    `- Tokens (suma de \`usage\`): entrada ${tokens.entrada}, salida ${tokens.salida}, cache lectura ${tokens.cacheLectura}, cache creación ${tokens.cacheCreacion}`,
    `- Primera línea del encargo: ${tareaSegura}`,
    `- Original: ${bruto.length} bytes, SHA-256 \`${sha}\``,
    `- Reemplazos: ${TIPOS.map((t) => `${t}=${conteo[t]}`).join(', ')}`,
    '', 'Resultado y commits: ver el handoff de la tarea dentro de la transcripción (este archivo es el registro íntegro, sanitizado).', '',
  ].join('\n');
  writeFileSync(join(destino, nombre + '.md'), md);
  filas.push({ nombre, id: s.id, fase: s.fase, rol: s.rol, nota: s.nota, bytes: bruto.length, sha, conteo });
}

filas.sort((a, b) => a.nombre.localeCompare(b.nombre));
const tot = Object.fromEntries(TIPOS.map((t) => [t, filas.reduce((a, f) => a + f.conteo[t], 0)]));
const manifiesto = [
  '# Manifiesto de transcripciones', '',
  'Generado por `scripts/sanitizar-logs` (determinista). El SHA-256 es el del archivo ORIGINAL sin sanitizar.',
  'Tipos de reemplazo: correo → `[EMAIL]`; google-AIza, google-AQ, sk, github, aws, clave-privada, asignacion-llave, cabecera-autorizacion, x-goog-api-key → `[SECRETO]`; cuenta → `[CUENTA]`.', '',
  '| Archivo | Id de sesión | Fase | Rol / agente | Descripción | Bytes (original) | SHA-256 (original) | Reemplazos por tipo |',
  '|---|---|---|---|---|---|---|---|',
  ...filas.map((f) => `| \`${f.nombre}.jsonl\` | ${f.id} | ${f.fase} | ${f.rol} | ${f.nota} | ${f.bytes} | \`${f.sha}\` | ${TIPOS.filter((t) => f.conteo[t]).map((t) => `${t}=${f.conteo[t]}`).join(', ') || 'ninguno'} |`),
  '', `Totales por tipo: ${TIPOS.map((t) => `${t}=${tot[t]}`).join(', ')}.`, '',
].join('\n');
writeFileSync(join(destino, 'MANIFIESTO.md'), manifiesto);
console.log(`Sesiones exportadas: ${filas.length}`);
console.log('Totales por tipo: ' + JSON.stringify(tot));
