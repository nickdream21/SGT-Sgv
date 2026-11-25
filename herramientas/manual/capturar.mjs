// Capturas de la ayuda (E5-21, M-074): sigue las recetas "capturas" de WebSGV/ContenidoAyuda/temas.json y
// guarda cada una recortada al bloque que explica (720 px de alto como mucho) en
// ContenidoAyuda/img/<tema>/NN.webp (calidad 80; baja a 60 y 45 si pasa de 150 KB). Edge sin ventana por CDP, sin dependencias de npm (Node 24: fetch y WebSocket).
//
// SOLO contra el sitio de ensayo (herramientas/sitio-local/iniciar-sitio-ensayo.ps1) sobre una BD sgvEnsayo_*
// anonimizada (herramientas/respaldo/anonimizar-ensayo.sql): rehúsa si el sitio no sirve un ENSAYO.txt con la
// BD indicada, si no es localhost o si es el puerto del sitio de pruebas (44390 / 50291).
//
//   node herramientas/manual/capturar.mjs --bd sgvEnsayo_X [--sitio http://localhost:50391] [--tema clave] [--usuario admin.transporte]
//
// Inicia sesión con el usuario de pruebas del grupo del tema (la contraseña se lee de
// privado/CREDENCIALES_USUARIOS.txt y nunca se imprime). La pantalla de ingreso no se captura.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const AYUDA = path.join(RAIZ, 'WebSGV', 'ContenidoAyuda');
const esperar = ms => new Promise(r => setTimeout(r, ms));

// Usuario de pruebas por grupo de la ayuda (A administración, K contabilidad, Y sistema). El conductor (C)
// entra con su documento: sus capturas (21b) necesitan un usuario de ensayo propio, no el de privado/.
const USUARIO_DEL_GRUPO = { A: 'admin.transporte', K: 'contabilidad', Y: 'admin.sistema' };

function argumento(nombre, porDefecto) {
  const i = process.argv.indexOf('--' + nombre);
  return i > 0 && i + 1 < process.argv.length ? process.argv[i + 1] : porDefecto;
}

function claveDe(usuario) {
  const texto = fs.readFileSync(path.join(RAIZ, 'privado', 'CREDENCIALES_USUARIOS.txt'), 'utf8');
  const m = texto.match(new RegExp('(^|\\s)' + usuario.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s+(\\S+)', 'm'));
  if (!m) throw new Error('No está ' + usuario + ' en privado/CREDENCIALES_USUARIOS.txt');
  return m[2];
}

async function exigirEnsayo(sitio, bd) {
  if (!/^sgvEnsayo_[A-Za-z0-9_]+$/.test(bd || '')) throw new Error('Indique --bd sgvEnsayo_* (la BD de ensayo anonimizada).');
  const u = new URL(sitio);
  if (!['localhost', '127.0.0.1'].includes(u.hostname)) throw new Error('Solo un sitio local: ' + sitio);
  if (['44390', '50291'].includes(u.port)) throw new Error('Ese es el sitio de la BD de pruebas: use el de ensayo (iniciar-sitio-ensayo.ps1).');
  let texto = '';
  try { const r = await fetch(new URL('/ENSAYO.txt', sitio)); texto = r.ok ? (await r.text()).trim() : ''; } catch { }
  if (texto !== bd) throw new Error('El sitio no es la copia de ensayo de ' + bd + ' (ENSAYO.txt dice "' + texto + '").');
}

async function abrirEdge() {
  const edge = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe']
    .find(fs.existsSync);
  if (!edge) throw new Error('No se encontró Edge.');
  const puerto = 9300 + Math.floor(Math.random() * 500);
  const perfil = fs.mkdtempSync(path.join(os.tmpdir(), 'sgv-manual-'));
  const proc = spawn(edge, ['--headless=new', '--remote-debugging-port=' + puerto, '--user-data-dir=' + perfil, '--no-first-run',
    '--hide-scrollbars', '--lang=es-PE', 'about:blank'], { stdio: 'ignore' });
  let destino;
  for (let i = 0; i < 40 && !destino; i++) {
    await esperar(250);
    try { destino = (await (await fetch('http://127.0.0.1:' + puerto + '/json')).json()).find(t => t.type === 'page'); } catch { }
  }
  if (!destino) { proc.kill(); throw new Error('Edge no respondió.'); }
  const ws = new WebSocket(destino.webSocketDebuggerUrl);
  await new Promise((ok, mal) => { ws.onopen = ok; ws.onerror = mal; });
  let id = 0; const pendientes = new Map();
  ws.onmessage = ev => {
    const m = JSON.parse(ev.data);
    if (m.id && pendientes.has(m.id)) { pendientes.get(m.id)(m); pendientes.delete(m.id); }
  };
  const cdp = (method, params = {}) => new Promise((ok, mal) => {
    const n = ++id;
    pendientes.set(n, m => m.error ? mal(new Error(method + ': ' + m.error.message)) : ok(m.result));
    ws.send(JSON.stringify({ id: n, method, params }));
  });
  const cerrar = () => { try { ws.close(); } catch { } proc.kill(); try { fs.rmSync(perfil, { recursive: true, force: true }); } catch { } };
  return { cdp, cerrar };
}

(async () => {
  const sitio = argumento('sitio', 'http://localhost:50391');
  const bd = argumento('bd');
  const soloTema = argumento('tema');
  await exigirEnsayo(sitio, bd);

  const temas = JSON.parse(fs.readFileSync(path.join(AYUDA, 'temas.json'), 'utf8')).temas
    .filter(t => !t.borrador && (t.capturas || []).length && (!soloTema || t.clave === soloTema));
  if (!temas.length) { console.log('No hay recetas de captura' + (soloTema ? ' para ' + soloTema : '') + '.'); return; }

  const { cdp, cerrar } = await abrirEdge();
  const evaluar = async expr => (await cdp('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true })).result.value;
  const ir = async url => {
    await cdp('Page.navigate', { url });
    for (let i = 0; i < 80; i++) { await esperar(250); if (await evaluar('document.readyState') === 'complete') return; }
    throw new Error('La página no terminó de cargar: ' + url);
  };
  let sesion = null;
  const entrar = async usuario => {
    if (sesion === usuario) return;
    await cdp('Network.clearBrowserCookies');
    await ir(new URL('/Views/Login.aspx', sitio).href);
    await evaluar(`(function(){var u=document.querySelector('[id$=txtUsername]'),p=document.querySelector('[id$=txtPassword]');
      u.value=${JSON.stringify(usuario)};p.value=${JSON.stringify(claveDe(usuario))};document.querySelector('[id$=btnLogin]').click();})()`);
    await esperar(2500);
    if (await evaluar('!!document.querySelector("[id$=txtPassword]")')) throw new Error('No se pudo iniciar sesión con ' + usuario + '.');
    sesion = usuario;
  };

  let hechas = 0;
  try {
    await cdp('Page.enable'); await cdp('Runtime.enable'); await cdp('Network.enable');
    for (const t of temas) {
      const usuario = argumento('usuario', USUARIO_DEL_GRUPO[t.roles.find(r => USUARIO_DEL_GRUPO[r])]);
      if (!usuario) { console.log(t.clave + ': sin usuario de ensayo para sus roles; se omite.'); continue; }
      await entrar(usuario);
      const dir = path.join(AYUDA, 'img', t.clave);
      fs.mkdirSync(dir, { recursive: true });
      for (const r of t.capturas) {
        if (!/^\d{2}\.webp$/.test(r.archivo)) throw new Error(t.clave + ': nombre de captura no válido ' + r.archivo);
        await cdp('Emulation.setDeviceMetricsOverride', { width: r.ancho, height: r.ancho < 600 ? 844 : 900, deviceScaleFactor: 1, mobile: r.ancho < 600 });
        await ir(new URL('/' + r.url, sitio).href);
        await esperar(800);
        for (const clic of r.clics || []) {
          await evaluar(`(function(){var e=document.querySelector(${JSON.stringify(clic)}); if(e) e.click();})()`);
          await esperar(1200);
        }
        const caja = await evaluar(`(function(){var e=document.querySelector(${JSON.stringify(r.selector)}); if(!e) return null;
          e.scrollIntoView({block:'start'}); var b=e.getBoundingClientRect();
          return {x:Math.max(0,b.left+window.scrollX-8), y:Math.max(0,b.top+window.scrollY-8), width:Math.min(b.width+16, document.documentElement.clientWidth), height:Math.min(b.height+16, 720)};})()`);
        if (!caja) { console.log(t.clave + '/' + r.archivo + ': no está ' + r.selector + ' en ' + r.url + '; se omite.'); continue; }
        let datos;
        for (const calidad of [80, 60, 45]) {
          const s = await cdp('Page.captureScreenshot', { format: 'webp', quality: calidad, captureBeyondViewport: true, clip: { ...caja, scale: 1 } });
          datos = Buffer.from(s.data, 'base64');
          if (datos.length <= 150 * 1024) break;
        }
        const salida = path.join(dir, r.archivo);
        fs.writeFileSync(salida, datos);
        hechas++;
        console.log(t.clave + '/' + r.archivo + ': ' + Math.round(datos.length / 1024) + ' KB (' + r.ancho + ' px, ' + r.selector + ')');
      }
      if (!fs.readdirSync(dir).length) fs.rmdirSync(dir);
    }
  } finally {
    cerrar();
  }
  console.log(hechas + ' captura(s). Agregue cada una al fragmento con <figure><img src="NN.webp" alt="..."><figcaption>...</figcaption></figure>.');
})().catch(e => { console.error(e.message); process.exit(1); });
