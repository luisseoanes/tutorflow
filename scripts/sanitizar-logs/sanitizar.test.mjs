import test from 'node:test';
import assert from 'node:assert/strict';
import { sanitizarTexto, sanitizarLinea, sanitizarJsonl, nuevoConteo } from './sanitizar.mjs';

const falsa = (pre, n) => pre + 'abcDEF0123456789_-'.repeat(3).slice(0, n); // sintética, no es una llave real

test('cada patrón se enmascara', () => {
  const casos = [
    ['correo', 'escribe a persona.x@gmail.com ya', '[EMAIL]'],
    ['google-AIza', 'k=' + falsa('AIza', 30), '[SECRETO]'],
    ['google-AQ', 'k ' + falsa('AQ.', 30), '[SECRETO]'],
    ['sk', 'x ' + falsa('sk-', 30), '[SECRETO]'],
    ['github', falsa('ghp_', 30).replace(/[_-]/g, 'A').replace('ghpA', 'ghp_'), '[SECRETO]'],
    ['github', falsa('gho_', 30).replace(/[_-]/g, 'A').replace('ghoA', 'gho_'), '[SECRETO]'],
    ['cabecera-autorizacion', 'Authorization: Bearer ' + falsa('', 30), '[SECRETO]'],
    ['cabecera-autorizacion', 'curl -H "Authorization: ' + falsa('', 30) + '"', '[SECRETO]'],
    ['x-goog-api-key', 'x-goog-api-key: ' + falsa('', 30), '[SECRETO]'],
    ['asignacion-llave', 'LLM_API_KEY=valorsecreto123', '[SECRETO]'],
    ['asignacion-llave', 'EMBEDDING_API_KEY: valorsecreto123', '[SECRETO]'],
    ['cuenta', 'accountUuid: 11111111-2222-3333-4444-555555555555', '[CUENTA]'],
  ];
  for (const [tipo, entrada, marca] of casos) {
    const c = nuevoConteo();
    const s = sanitizarTexto(entrada, c);
    assert.ok(s.includes(marca), tipo + ': ' + s);
    assert.ok(c[tipo] >= 1, `${tipo} contado`);
    assert.ok(!/abcDEF0123|valorsecreto|persona\.x|11111111-2222/.test(s), tipo + ' sin residuo');
  }
});

test('el texto técnico no cambia', () => {
  const t = [
    'AuthorizationPolicy.Authorize() y IAuthorization', 'using Microsoft.AspNetCore.Authorization;',
    '<task-notification><task-id>abc</task-id>', 'sk-id y sk-notification', 'Co-Authored-By: Claude <noreply@anthropic.com>',
    'alumno@horizonte.edu.co', 'LLM_API_KEY=${LLM_API_KEY}', 'LLM_API_KEY=', 'LLM_API_KEY: ""', 'LLM_API_KEY=<su llave>',
    'LLM_API_KEY=…', 'EMBEDDING_API_KEY=\nEMBEDDING_PRICE_PER_MTOK=', 'Bearer', 'error: 403 en L4 \\n línea',
    'Authorization: Bearer', 'x-goog-api-key', 'AIza corto', 'AQ.corto',
  ];
  for (const x of t) assert.equal(sanitizarTexto(x), x, x);
});

test('JSONL: válido línea a línea, claves de cuenta y líneas sin reemplazos idénticas', () => {
  const limpia = '{"type":"user","message":{"content":"hola \\u00e9 mundo"},"n":1.0}';
  const sucia = JSON.stringify({ type: 'x', accountUuid: '11111111-2222-3333-4444-555555555555',
    message: { content: [{ type: 'text', text: 'mi correo a@b.com\ny LLM_API_KEY=zzzzzzzzzzzz "comillas" \\ barra' }] } });
  const { salida, conteo } = sanitizarJsonl(limpia + '\n' + sucia + '\n');
  const ls = salida.split('\n');
  assert.equal(ls[0], limpia);
  assert.equal(ls[2], '');
  const o = JSON.parse(ls[1]);
  assert.equal(o.accountUuid, '[CUENTA]');
  assert.equal(o.message.content[0].text, 'mi correo [EMAIL]\ny LLM_API_KEY=[SECRETO] "comillas" \\ barra');
  assert.deepEqual([conteo.cuenta, conteo.correo, conteo['asignacion-llave']], [1, 1, 1]);
  for (const l of ls.filter(Boolean)) JSON.parse(l);
});

test('determinista e idempotente', () => {
  const l = JSON.stringify({ t: 'a@b.com ' + falsa('AIza', 30) });
  const a = sanitizarLinea(l), b = sanitizarLinea(l);
  assert.equal(a, b);
  assert.equal(sanitizarLinea(a), a);
});

test('JSON anidado como cadena (comillas escapadas)', () => {
  const t = '{\\"accountUuid\\":\\"11111111-2222-3333-4444-555555555555\\"}';
  const s = sanitizarTexto(t);
  assert.ok(s.includes('[CUENTA]') && !s.includes('11111111-2222'));
});
