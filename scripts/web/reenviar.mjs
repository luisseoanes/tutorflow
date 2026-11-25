// Reenviador TCP minimo: expone en 127.0.0.1:<puerto local> el puerto publicado por el compose en el anfitrion.
// Motivo: Chromium solo trata como "contexto seguro" a localhost; con http://host.docker.internal las APIs caches y
// serviceWorker no existen y la prueba de almacenamiento vacio (web/e2e/simulado.spec.ts) no puede ejecutarse.
// Uso: node scripts/web/reenviar.mjs 3000 host.docker.internal:3000
import net from "node:net";

const [puertoLocal, destino] = process.argv.slice(2);
const [host, puerto] = destino.split(":");
net
  .createServer((cliente) => {
    const remoto = net.connect(Number(puerto), host);
    cliente.pipe(remoto).pipe(cliente);
    const cerrar = () => { cliente.destroy(); remoto.destroy(); };
    cliente.on("error", cerrar);
    remoto.on("error", cerrar);
  })
  .listen(Number(puertoLocal), "127.0.0.1");
