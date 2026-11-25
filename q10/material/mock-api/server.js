// API simulada de TutorFlow · Q10 (prueba técnica)
// Requiere Node 18+ y no tiene dependencias:  node server.js   (puerto 4000, o PORT=xxxx)
// IMPORTANTE: esta API NO valida permisos. Controlar quién puede ver qué es responsabilidad de tu aplicación.

const http = require("http");
const fs = require("fs");
const path = require("path");

const datos = JSON.parse(fs.readFileSync(path.join(__dirname, "datos.json"), "utf8"));
const PORT = process.env.PORT || 4000;

function send(res, status, body) {
  res.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
  res.end(JSON.stringify(body, null, 2));
}

const routes = [
  [/^\/estudiantes\/([^/]+)$/, (id) => datos.estudiantes.find((e) => e.id === id)],
  [/^\/estudiantes\/([^/]+)\/notas$/, (id) =>
    datos.estudiantes.some((e) => e.id === id) ? datos.notas.filter((n) => n.estudianteId === id) : undefined],
  [/^\/estudiantes\/([^/]+)\/asistencia$/, (id) =>
    datos.estudiantes.some((e) => e.id === id) ? datos.asistencia.filter((a) => a.estudianteId === id) : undefined],
  [/^\/programas\/([^/]+)\/estudiantes$/, (id) =>
    datos.programas.some((p) => p.id === id) ? datos.estudiantes.filter((e) => e.programaId === id) : undefined],
  [/^\/coordinadores\/([^/]+)$/, (id) => datos.coordinadores.find((c) => c.id === id)],
  [/^\/programas$/, () => datos.programas],
];

http.createServer((req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  if (req.method !== "GET") return send(res, 405, { error: "Solo GET" });
  if (url.pathname === "/salud") return send(res, 200, { estado: "ok", periodo: datos.periodo });
  for (const [re, fn] of routes) {
    const m = url.pathname.match(re);
    if (m) {
      const out = fn(decodeURIComponent(m[1] || ""));
      return out === undefined ? send(res, 404, { error: "No encontrado" }) : send(res, 200, out);
    }
  }
  send(res, 404, { error: "Ruta no existe" });
}).listen(PORT, () => console.log(`API simulada TutorFlow en http://localhost:${PORT}`));
