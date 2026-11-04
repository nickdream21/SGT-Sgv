// Video de capacitación: cómo liquida el conductor su viaje desde el celular (formulario de nov-2026).
// Requisitos: sitio local levantado (herramientas/sitio-local/iniciar-sitio.ps1) y el usuario conductor
// de pruebas enlazado a un conductor con un viaje abierto y seguimiento GPS (lo prepara quien lo ejecuta).
// Uso: node grabar-liquidacion.mjs [sitio] [usuario]
import path from 'node:path';
import fs from 'node:fs';
import { Grabacion, abrirNavegador, claveDe, espera, RAIZ } from './comun.mjs';

const SITIO = process.argv[2] || 'http://localhost:50291';
const USUARIO = process.argv[3] || '03497777';
// Lo que se ve en pantalla en lugar de los datos reales del conductor.
const NOMBRE_MOSTRADO = 'CARLOS RAMÍREZ SOTO';
const DNI_MOSTRADO = '45871236';
// Nombres y DNI reales a ocultar (separados por |), se pasan al ejecutar para no dejarlos en el código.
const OCULTAR = (process.env.SGV_OCULTAR || '').split('|').map(x => x.trim()).filter(Boolean);

const salidaDir = path.join(RAIZ, 'docs', 'DOCUMENTACION', 'capacitacion');
fs.mkdirSync(salidaDir, { recursive: true });
const g = new Grabacion(path.join(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), 'salida', 'liquidacion'),
  { ancho: 720, alto: 1280, voice: 'es-PE-CamilaNeural', rate: '+2%' });

const browser = await abrirNavegador();
const page = await browser.newPage();
await page.emulate({
  viewport: { width: 390, height: 693, deviceScaleFactor: 2, isMobile: true, hasTouch: true },
  userAgent: 'Mozilla/5.0 (Linux; Android 13; SM-A145M) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36 EdgA/129.0.0.0'
});

// --- Ayudas que se inyectan en la página ---
async function preparar() {
  await page.evaluate((nombre, dni, ocultar) => {
    // Enmascarar nombre y DNI reales en cualquier texto visible.
    const reales = ocultar.slice();
    const n = document.querySelector('.dc-nombre'); if (n && n.innerText.trim()) reales.push(n.innerText.trim());
    const d = document.querySelector('.dc-dni'); if (d) { const m = d.innerText.match(/\d{6,}/); if (m) reales.push(m[0]); }
    window.__reales = (window.__reales || []).concat(reales);
    try { const guardados = JSON.parse(sessionStorage.getItem('__reales') || '[]'); window.__reales = window.__reales.concat(guardados); sessionStorage.setItem('__reales', JSON.stringify(window.__reales)); } catch (e) {}
    const usuarioNav = [...document.querySelectorAll('.navbar *, footer *')];
    const cambiar = (t) => {
      let s = t;
      (window.__reales || []).forEach(r => {
        const t = (r || '').trim(); if (t.length <= 3) return;
        const re = new RegExp(t.split(/\s+/).map(x => x.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('\\s+'), 'gi');
        s = s.replace(re, /^\d+$/.test(t) ? dni : nombre);
      });
      return s;
    };
    const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    while (w.nextNode()) { const v = w.currentNode.nodeValue; const c = cambiar(v); if (c !== v) w.currentNode.nodeValue = c; }
    // El usuario de la barra superior y del pie es el del usuario de pruebas: también se oculta.
    usuarioNav.forEach(el => { if (el.children.length === 0 && /[A-ZÁÉÍÓÚÑ]{3,} [A-ZÁÉÍÓÚÑ]{3,}/.test(el.textContent) && !/SISTEMA|CERRAR|SGV/.test(el.textContent.toUpperCase())) el.textContent = el.textContent.replace(/[A-ZÁÉÍÓÚÑ ]{8,}/, ' ' + nombre + ' '); });
    if (!document.getElementById('__estilo')) {
      const st = document.createElement('style'); st.id = '__estilo';
      st.textContent = `
        #__sub { position: fixed; left: 0; right: 0; top: 0; z-index: 99999; background: rgba(15, 23, 42, .9); color: #fff;
          font: 600 15px/1.35 "Segoe UI", system-ui, sans-serif; padding: 10px 14px; min-height: 56px; display: flex; align-items: center; }
        #__sub:empty { display: none; }
        .__toque { position: fixed; z-index: 99998; width: 44px; height: 44px; margin: -22px 0 0 -22px; border-radius: 50%;
          background: rgba(29, 78, 216, .28); border: 3px solid rgba(29, 78, 216, .9); pointer-events: none; }
        * { caret-color: transparent !important; }`;
      document.head.appendChild(st);
      const sub = document.createElement('div'); sub.id = '__sub'; document.body.appendChild(sub);
    }
  }, NOMBRE_MOSTRADO, DNI_MOSTRADO, OCULTAR);
}
async function subtitulo(t) { await page.evaluate(t => { const s = document.getElementById('__sub'); if (s) s.textContent = t; }, t); }
async function toque(sel) {
  await page.evaluate(sel => {
    const el = typeof sel === 'string' ? document.querySelector(sel) : null; if (!el) return;
    const r = el.getBoundingClientRect();
    const d = document.createElement('div'); d.className = '__toque';
    d.style.left = (r.left + r.width / 2) + 'px'; d.style.top = (r.top + r.height / 2) + 'px';
    document.body.appendChild(d);
  }, sel);
}
async function sinToque() { await page.evaluate(() => document.querySelectorAll('.__toque').forEach(x => x.remove())); }
async function centrar(sel, desfase = 0) {
  await page.evaluate((sel, desfase) => { const el = document.querySelector(sel); if (el) { el.scrollIntoView({ block: 'center' }); window.scrollBy(0, desfase); } }, sel, desfase);
  await espera(250);
}
async function escribir(sel, texto, cuadros = 3) {
  // Simula el tecleo en pocos pasos (cada paso es un cuadro del video).
  await page.focus(sel);
  const pasos = Math.max(1, Math.min(cuadros, texto.length));
  for (let i = 1; i <= pasos; i++) {
    const parcial = texto.slice(0, Math.ceil(texto.length * i / pasos));
    await page.evaluate((sel, v) => { const el = document.querySelector(sel); el.value = v; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }, sel, parcial);
    await g.cuadro(page, i === pasos ? 2 : 0.6);
  }
}
async function irA(url) { await page.goto(url, { waitUntil: 'networkidle2' }); await preparar(); }
async function tarjeta(titulo, sub) {
  await page.setContent(`<html><head><meta name="viewport" content="width=device-width, initial-scale=1"></head><body style="margin:0;height:100vh;display:flex;flex-direction:column;justify-content:center;gap:18px;padding:0 32px;
    background:#1d4ed8;color:#fff;font-family:'Segoe UI',system-ui,sans-serif">
    <div style="font-size:14px;letter-spacing:.12em;opacity:.8">SISTEMA SGV · CONDUCTORES</div>
    <div style="font-size:32px;font-weight:700;line-height:1.2">${titulo}</div>
    <div style="font-size:17px;opacity:.9;line-height:1.45">${sub}</div>
    <div style="margin-top:40px;font-size:13px;opacity:.7">Servicios Generales Viviana E.I.R.L.</div></body></html>`);
}

try {
  // 1. Portada
  g.escena('Hola. En este video te enseño a liquidar tu viaje desde el celular con el sistema SGV. Ya no necesitas llenar la orden de viaje en papel.');
  await tarjeta('Cómo liquidar tu viaje desde el celular', 'Fechas por GPS, tus gastos, resumen y firma. Unos cinco minutos.');
  await g.cuadro(page);

  // 2. Ingreso
  g.escena('Entra a la página del sistema. Escribe tu usuario, que es tu número de DNI, y tu contraseña. Luego toca Acceder.');
  await irA(SITIO + '/Views/Login.aspx');
  await subtitulo('1. Ingresa con tu DNI y tu contraseña');
  await g.cuadro(page, 1);
  const selUsuario = '[id$="txtUsername"]', selClave = '[id$="txtPassword"]', selAcceder = '[id$="btnLogin"]';
  await page.type(selUsuario, USUARIO);
  await page.type(selClave, claveDe(USUARIO));
  // En pantalla se muestra un DNI de ejemplo en lugar del real.
  await page.evaluate((sel, dni) => { const el = document.querySelector(sel); el.dataset.real = el.value; el.value = dni; }, selUsuario, DNI_MOSTRADO);
  await g.cuadro(page, 1.2);
  await toque(selAcceder); await g.cuadro(page, 0.8); await sinToque();
  await page.evaluate(sel => { const el = document.querySelector(sel); el.value = el.dataset.real; }, selUsuario);
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), page.click(selAcceder)]);
  await preparar();

  // 3. Mi viaje
  g.escena('En la pestaña Mi viaje ves tu viaje en curso, cuántos días llevas en ruta y tus despachos. Cuando regreses a la base, toca Liquidar mi viaje.');
  await subtitulo('2. Mi viaje: tu viaje en curso y tus despachos');
  await g.cuadro(page, 1.5);
  await page.evaluate(() => window.scrollTo(0, 260)); await espera(200);
  await g.cuadro(page, 1);
  await page.evaluate(() => window.scrollTo(0, 0)); await espera(200);
  await toque('#viajesActivos .dc-btn-primario'); await g.cuadro(page, 1); await sinToque();
  await page.click('#viajesActivos .dc-btn-primario'); await espera(500);

  // 4. Fechas por GPS
  g.escena('Arriba aparecen la salida y la llegada a la base. Las pone el GPS de tu unidad, con la marca verde GPS, así que no tienes que escribirlas. Si el GPS no las tiene, el sistema te pedirá la fecha y la hora.');
  await subtitulo('3. Salida y llegada: las pone el GPS');
  await g.cuadro(page, 1);
  await toque('.dc-hito .dc-gps'); await g.cuadro(page, 1.5); await sinToque();

  // 5. Gastos
  g.escena('Ahora escribe el total de cada gasto. En combustible pones lo que gastaste en soles y, si estuviste en Ecuador, también en dólares. Mira cómo el total de abajo se suma solo.');
  await subtitulo('4. Escribe el total de cada gasto');
  await centrar('#combustibleSoles', 60);
  await g.cuadro(page, 0.8);
  await toque('#combustibleSoles'); await g.cuadro(page, 0.5); await sinToque();
  await escribir('#combustibleSoles', '350', 3);
  await escribir('#combustibleDolares', '40', 2);

  // 6. Nota
  g.escena('Si quieres explicar un gasto, toca Nota y escribe el detalle. Por ejemplo, en alimentación: desayunos y almuerzos en ruta.');
  await subtitulo('5. Toca "Nota" para explicar un gasto');
  await escribir('#alimentacionSoles', '120', 2);
  const notaAli = '#alimentacionSoles';
  await page.evaluate(sel => document.querySelector(sel).closest('.dc-gasto').querySelector('.dc-nota-btn').id = '__notaAli', notaAli);
  await toque('#__notaAli'); await g.cuadro(page, 0.8); await sinToque();
  await page.click('#__notaAli'); await espera(200);
  await escribir('#descAlimentacion', 'Desayunos y almuerzos en ruta', 4);

  // 7. Peajes
  g.escena('En los viajes a Ecuador registras también los peajes: los de Perú en soles y los de Ecuador en dólares. Lo que no gastaste, déjalo en blanco.');
  await subtitulo('6. Viajes a Ecuador: peajes en soles y en dólares');
  await centrar('#peajesNacSoles', 40);
  await escribir('#peajesNacSoles', '85.50', 3);
  await escribir('#peajesExtDolares', '12', 2);
  await escribir('#hospedajeSoles', '60', 2);

  // 8. Otro gasto
  g.escena('¿Tuviste un gasto que no está en la lista? Toca Agregar otro gasto, escribe en qué fue y el monto.');
  await subtitulo('7. Un gasto que no está en la lista');
  await centrar('#gastosAdicionalesBody + button', -40);
  await page.evaluate(() => document.querySelector('#gastosAdicionalesBody + button').id = '__otro');
  await toque('#__otro'); await g.cuadro(page, 1); await sinToque();
  await page.click('#__otro'); await espera(300);
  await page.evaluate(() => { document.querySelector('.dc-gasto-extra .dc-concepto').id = '__concepto'; document.querySelector('.dc-gasto-extra .gasto-soles').id = '__otroMonto'; });
  await escribir('#__concepto', 'Lavado de unidad', 3);
  await escribir('#__otroMonto', '25', 2);

  // 9. Revisar
  g.escena('Cuando termines, toca Revisar y enviar. Aparece un resumen con tus fechas y cada gasto. Revísalo bien: si algo está mal, toca Corregir.');
  await subtitulo('8. Revisa el resumen antes de enviar');
  await toque('#barraTotal .dc-btn-primario'); await g.cuadro(page, 1); await sinToque();
  await page.click('#barraTotal .dc-btn-primario'); await espera(700);
  await g.cuadro(page, 3);

  // 10. Enviar
  g.escena('Si todo está bien, toca Enviar y firmar.');
  await subtitulo('9. Toca "Enviar y firmar"');
  await page.evaluate(() => document.querySelector('#modalRevision [id$="btnEnviarLiquidacion"]').id = '__enviar');
  await toque('#__enviar'); await g.cuadro(page, 1); await sinToque();
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), page.click('#__enviar')]);
  await preparar();
  await page.waitForFunction(() => /\d/.test((document.getElementById('vGastos') || {}).textContent || ''), { timeout: 15000 });
  await preparar();

  // 11. Firma
  g.escena('Ahora firmas. Revisa el total de tus gastos, lee la declaración jurada y marca la casilla. Luego firma con tu dedo dentro del recuadro.');
  await subtitulo('10. Marca la casilla y firma con tu dedo');
  await g.cuadro(page, 1.2);
  await centrar('#chkAcepto', 120);
  await toque('#chkAcepto'); await g.cuadro(page, 0.6); await sinToque();
  await page.click('#chkAcepto');
  const c = await page.$('#canvasFirma'); const b = await c.boundingBox();
  const trazo = [[0.12, 0.70], [0.22, 0.35], [0.30, 0.65], [0.40, 0.30], [0.48, 0.62], [0.58, 0.40], [0.66, 0.58], [0.78, 0.36], [0.88, 0.55]];
  await page.mouse.move(b.x + b.width * trazo[0][0], b.y + b.height * trazo[0][1]); await page.mouse.down();
  for (let i = 1; i < trazo.length; i++) {
    await page.mouse.move(b.x + b.width * trazo[i][0], b.y + b.height * trazo[i][1], { steps: 8 });
    if (i % 3 === 0) await g.cuadro(page, 0.5);
  }
  await page.mouse.up(); await espera(200);
  await g.cuadro(page, 1);

  // 12. Firmar y enviar
  g.escena('Toca Firmar y enviar. Listo: tu liquidación quedó firmada y enviada a la oficina.');
  await subtitulo('11. Toca "Firmar y enviar"');
  await centrar('#btnFirmar', 0);
  await toque('#btnFirmar'); await g.cuadro(page, 0.8); await sinToque();
  await page.click('#btnFirmar');
  await page.waitForFunction(() => /Firma registrada/.test(document.body.innerText), { timeout: 20000 });
  await preparar(); await page.evaluate(() => window.scrollTo(0, 0));
  await subtitulo('Listo: firmada y enviada a la oficina');
  await g.cuadro(page, 2);

  // 13. Dinero entregado y comprobantes
  g.escena('Recuerda: el dinero que te entregaron para el viaje lo registra la oficina; tú solo declaras tus gastos. Y entrega tus comprobantes en la oficina.');
  await tarjeta('Importante', '• El dinero que te entregaron lo registra la oficina: tú solo declaras tus gastos.<br><br>• Entrega tus comprobantes en la oficina.<br><br>• Liquida apenas llegues a la base.');
  await g.cuadro(page);

  // 14. Historial
  g.escena('En la pestaña Historial ves tus liquidaciones y su estado. Mientras diga En revisión, puedes retirarla si necesitas corregir algo. Si la oficina la rechaza, verás el motivo y tus datos para corregirla.');
  await irA(SITIO + '/Views/DashboardConductor.aspx');
  await page.click('#historial-tab'); await espera(500); await preparar();
  await subtitulo('12. Historial: el estado de tus liquidaciones');
  await g.cuadro(page, 2);
  await toque('#historial .dc-item .dc-pill'); await g.cuadro(page, 1); await sinToque();

  // 15. Cierre
  g.escena('Ante cualquier duda, comunícate con la oficina. ¡Buen viaje!');
  await tarjeta('¡Buen viaje!', 'Ante cualquier duda, comunícate con la oficina.');
  await g.cuadro(page);
} finally {
  await browser.close();
}

const salida = path.join(salidaDir, 'Como-liquidar-tu-viaje-celular.mp4');
await g.armar(salida);
