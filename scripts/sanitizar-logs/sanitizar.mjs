// Sanitizado de transcripciones JSONL (FABRICA.md §11). Node sin dependencias.
// Enmascara SOLO correos, credenciales e identificadores de cuenta; no toca nada más.
// Opera sobre los valores de cadena ya decodificados de cada línea JSON. Una línea sin
// reemplazos se emite byte a byte igual al original.

const TOKEN = '[A-Za-z0-9_\\-.]';

// El orden importa: las reglas más específicas primero. Cada regla: tipo, marca, regex y reemplazo opcional.
export const REGLAS = [
  // Identificadores de cuenta en texto (p. ej. JSON anidado dentro de una cadena).
  { tipo: 'cuenta', marca: '[CUENTA]',
    re: /((?:accountUuid|organizationUuid)(?:\\*["'])?\s*[:=]\s*\\*["']?)[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/g,
    rep: (m, p) => p + '[CUENTA]' },
  // Líneas LLM_API_KEY / EMBEDDING_API_KEY con valor (formato .env con "=", YAML con ": ").
  // No se enmascaran marcadores sin valor (vacío, ${...}, <...>, …).
  { tipo: 'asignacion-llave', marca: '[SECRETO]',
    re: new RegExp('((?:LLM|EMBEDDING)_API_KEY(?:=|:[ \\t]?)(?:\\\\?["\'])?)(?![$<{])(' + TOKEN + '+)', 'g'),
    rep: (m, p, v) => (v === '[SECRETO]' ? m : p + '[SECRETO]') },
  // Cabeceras.
  { tipo: 'cabecera-autorizacion', marca: '[SECRETO]',
    re: /(\b(?:Bearer|Basic)[ \t]+)(?!\[SECRETO\])[A-Za-z0-9._~+\/=\-]{16,}/g,
    rep: (m, p) => p + '[SECRETO]' },
  { tipo: 'cabecera-autorizacion', marca: '[SECRETO]',
    re: /(\bAuthorization(?:\\*["'])?\s*[:=]\s*\\*["']?)(?!(?:Bearer|Basic)\b)(?!\[SECRETO\])[A-Za-z0-9._~+\/=\-]{20,}/gi,
    rep: (m, p) => p + '[SECRETO]' },
  { tipo: 'x-goog-api-key', marca: '[SECRETO]',
    re: /(x-goog-api-key(?:\\*["'])?\s*[:=]\s*\\*["']?)(?!\[SECRETO\])[A-Za-z0-9_\-.]{16,}/gi,
    rep: (m, p) => p + '[SECRETO]' },
  // Llaves por formato.
  { tipo: 'google-AIza', marca: '[SECRETO]', re: /AIza[0-9A-Za-z_-]{20,}/g },
  { tipo: 'google-AQ', marca: '[SECRETO]', re: /\bAQ\.[0-9A-Za-z_-]{20,}/g },
  { tipo: 'sk', marca: '[SECRETO]', re: /\bsk-[A-Za-z0-9_-]{20,}/g },
  { tipo: 'github', marca: '[SECRETO]', re: /\bgh[pousr]_[A-Za-z0-9]{20,}/g },
  { tipo: 'github', marca: '[SECRETO]', re: /\bgithub_pat_[A-Za-z0-9_]{20,}/g },
  { tipo: 'aws', marca: '[SECRETO]', re: /\b(?:AKIA|ASIA)[0-9A-Z]{16}\b/g },
  { tipo: 'clave-privada', marca: '[SECRETO]',
    re: /-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(?:-----END [A-Z ]*PRIVATE KEY-----|$)/g },
  // Correos, salvo los que la regla tutorflow-email-en-logs de .gitleaks.toml declara sin dato personal.
  { tipo: 'correo', marca: '[EMAIL]',
    re: /[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}/g,
    rep: (m) => (/@horizonte\.edu\.co|noreply@anthropic\.com|@example\.(com|org)/.test(m) ? m : '[EMAIL]') },
];

export const TIPOS = [...new Set(REGLAS.map((r) => r.tipo))];

export function nuevoConteo() {
  return Object.fromEntries(TIPOS.map((t) => [t, 0]));
}

/** Sanitiza una cadena; acumula los reemplazos reales (texto distinto) en `conteo`. */
export function sanitizarTexto(texto, conteo = nuevoConteo()) {
  let s = texto;
  for (const r of REGLAS) {
    s = s.replace(r.re, (...args) => {
      const m = args[0];
      const nuevo = r.rep ? r.rep(...args) : r.marca;
      if (nuevo !== m) conteo[r.tipo]++;
      return nuevo;
    });
  }
  return s;
}

const CLAVES_CUENTA = new Set(['accountUuid', 'organizationUuid']);

function recorrer(v, conteo, clave) {
  if (typeof v === 'string') {
    if (CLAVES_CUENTA.has(clave) && v !== '[CUENTA]') { conteo.cuenta++; return '[CUENTA]'; }
    return sanitizarTexto(v, conteo);
  }
  if (Array.isArray(v)) return v.map((x) => recorrer(x, conteo));
  if (v && typeof v === 'object') {
    const o = {};
    for (const k of Object.keys(v)) o[k] = recorrer(v[k], conteo, k);
    return o;
  }
  return v;
}

/** Sanitiza una línea JSONL. Devuelve la línea original, sin tocar, si no hubo reemplazos. */
export function sanitizarLinea(linea, conteo = nuevoConteo()) {
  if (linea.trim() === '') return linea;
  let obj;
  try { obj = JSON.parse(linea); } catch {
    return sanitizarTexto(linea, conteo); // no es JSON: texto plano
  }
  const local = nuevoConteo();
  const nuevo = recorrer(obj, local);
  const total = Object.values(local).reduce((a, b) => a + b, 0);
  for (const t of TIPOS) conteo[t] += local[t];
  return total === 0 ? linea : JSON.stringify(nuevo);
}

export function sanitizarJsonl(contenido) {
  const conteo = nuevoConteo();
  const salida = contenido.split('\n').map((l) => sanitizarLinea(l, conteo)).join('\n');
  return { salida, conteo };
}
