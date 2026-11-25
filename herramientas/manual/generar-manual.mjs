// Manual de la ayuda por rol en PDF (E5-21b, M-074): junta los temas de WebSGV/ContenidoAyuda que ve un grupo de
// rol (temas.json: roles, sin los de un interruptor apagado en Web.config), con la misma composición que
// Views/Ayuda.aspx?manual=1 (AyudaCatalogo.HtmlManual): portada, índice por sección y un tema por hoja, con
// Content/sgv-base.css y Content/paginas/AyudaManual.css. Las imágenes se leen del disco (file:///). Edge sin
// ventana imprime el PDF (--print-to-pdf). Sin dependencias de npm (Node 24). No usa la BD ni el sitio.
//
//   node herramientas/manual/generar-manual.mjs --rol A|C|K|Y|todos
//
// Salida: docs/DOCUMENTACION/capacitacion/Manual-SGV-<rol>.pdf (fuera de git). La portada dice que el texto es
// un borrador hasta la revisión del usuario.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const WEB = path.join(RAIZ, 'WebSGV');
const AYUDA = path.join(WEB, 'ContenidoAyuda');
const SALIDA = path.join(RAIZ, 'docs', 'DOCUMENTACION', 'capacitacion');

const GRUPOS = { A: 'Administración', C: 'Conductor', K: 'Contabilidad', Y: 'Administración del sistema' };
const NOTA_PORTADA = 'Borrador hasta la revisión del usuario.';

function argumento(nombre) {
  const i = process.argv.indexOf('--' + nombre);
  return i > 0 && i + 1 < process.argv.length ? process.argv[i + 1] : null;
}

const cod = s => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** appSettings de Web.config que están en "true" (los interruptores de los temas). */
function interruptoresEncendidos() {
  const xml = fs.readFileSync(path.join(WEB, 'Web.config'), 'utf8').replace(/<!--[\s\S]*?-->/g, '');
  const encendidos = new Set();
  for (const m of xml.matchAll(/<add\s+key="([^"]+)"\s+value="([^"]*)"/g))
    if (m[2].trim().toLowerCase() === 'true') encendidos.add(m[1]);
  return encendidos;
}

/** Temas del grupo en el orden del índice de la ayuda (sección por primera aparición, luego orden). */
function temasDelGrupo(temas, grupo, encendidos) {
  const secciones = [...new Set(temas.map(t => t.seccion))];
  return temas
    .filter(t => !t.borrador && (t.roles || []).includes(grupo) && (!t.interruptor || encendidos.has(t.interruptor)))
    .sort((a, b) => secciones.indexOf(a.seccion) - secciones.indexOf(b.seccion) || a.orden - b.orden);
}

function htmlManual(temas, grupo, fecha) {
  const enManual = new Set(temas.map(t => t.clave));
  let h = '<section class="manual-portada"><div class="manual-titulo">Manual del sistema SGV</div>'
    + '<p>Usuario: ' + cod(GRUPOS[grupo]) + '</p><p>Fecha: ' + cod(fecha) + '</p><p>' + cod(NOTA_PORTADA) + '</p></section>';
  h += '<nav class="manual-indice" aria-label="Contenido del manual"><h2>Contenido</h2><ol>';
  for (const seccion of [...new Set(temas.map(t => t.seccion))]) {
    h += '<li>' + cod(seccion) + '<ol>';
    for (const t of temas.filter(x => x.seccion === seccion)) h += '<li><a href="#tema-' + t.clave + '">' + cod(t.titulo) + '</a></li>';
    h += '</ol></li>';
  }
  h += '</ol></nav>';
  for (const t of temas) {
    const archivo = path.join(AYUDA, t.clave + '.html');
    if (!fs.existsSync(archivo)) continue;
    let frag = fs.readFileSync(archivo, 'utf8');
    frag = frag.replace(/\bsrc="([0-9]{2}\.webp)"/g, (_, img) => 'src="' + pathToFileURL(path.join(AYUDA, 'img', t.clave, img)).href + '"');
    // Otro tema del manual: su ancla. Uno que no está (otro rol o apagado): solo el texto.
    frag = frag.replace(/<a href="\?tema=([a-z0-9-]{1,40})">([\s\S]*?)<\/a>/g,
      (_, c, texto) => enManual.has(c) ? '<a href="#tema-' + c + '">' + texto + '</a>' : texto);
    h += '<section class="manual-tema" id="tema-' + t.clave + '"><h2>' + cod(t.titulo) + '</h2><div class="sgv-ayuda-cuerpo">' + frag + '</div></section>';
  }
  return h;
}

function edge() {
  const ruta = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe']
    .find(fs.existsSync);
  if (!ruta) throw new Error('No se encontró Edge.');
  return ruta;
}

function paginasDelPdf(buffer) {
  const texto = buffer.toString('latin1');
  return (texto.match(/\/Type\s*\/Page(?![s\w])/g) || []).length;
}

function generar(grupo, temasJson, encendidos) {
  const temas = temasDelGrupo(temasJson, grupo, encendidos);
  if (!temas.length) throw new Error('El grupo ' + grupo + ' no tiene temas abiertos.');
  const hoy = new Date();
  const fecha = String(hoy.getDate()).padStart(2, '0') + '/' + String(hoy.getMonth() + 1).padStart(2, '0') + '/' + hoy.getFullYear();
  const css = ['Content/sgv-base.css', 'Content/paginas/AyudaManual.css']
    .map(c => '<link rel="stylesheet" href="' + pathToFileURL(path.join(WEB, c)).href + '">').join('');
  const documento = '<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Manual del sistema SGV - '
    + cod(GRUPOS[grupo]) + '</title>' + css + '</head><body><main class="manual">' + htmlManual(temas, grupo, fecha) + '</main></body></html>';

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'sgv-manual-pdf-'));
  try {
    const html = path.join(tmp, 'manual.html');
    fs.writeFileSync(html, documento, 'utf8');
    fs.mkdirSync(SALIDA, { recursive: true });
    const pdf = path.join(SALIDA, 'Manual-SGV-' + grupo + '.pdf');
    if (fs.existsSync(pdf)) fs.rmSync(pdf);
    const r = spawnSync(edge(), ['--headless=new', '--disable-gpu', '--no-first-run', '--no-pdf-header-footer',
      '--allow-file-access-from-files', '--user-data-dir=' + path.join(tmp, 'perfil'), '--print-to-pdf=' + pdf,
      pathToFileURL(html).href], { stdio: 'ignore', timeout: 120000 });
    if (!fs.existsSync(pdf)) throw new Error('Edge no generó el PDF de ' + grupo + ' (código ' + r.status + ').');
    const datos = fs.readFileSync(pdf);
    console.log('Manual-SGV-' + grupo + '.pdf: ' + temas.length + ' temas, ' + paginasDelPdf(datos) + ' páginas, '
      + Math.round(datos.length / 1024) + ' KB');
  } finally {
    try { fs.rmSync(tmp, { recursive: true, force: true }); } catch { }
  }
}

try {
  const rol = (argumento('rol') || '').toUpperCase();
  const grupos = rol === 'TODOS' ? Object.keys(GRUPOS) : [rol];
  if (!grupos.every(g => GRUPOS[g])) throw new Error('Indique --rol A, C, K, Y o todos.');
  const temasJson = JSON.parse(fs.readFileSync(path.join(AYUDA, 'temas.json'), 'utf8')).temas;
  const encendidos = interruptoresEncendidos();
  for (const g of grupos) generar(g, temasJson, encendidos);
  console.log('Salida: ' + SALIDA);
} catch (e) {
  console.error(e.message);
  process.exit(1);
}
