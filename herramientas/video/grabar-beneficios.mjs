// Video corto de presentación: beneficios del sistema SGV (horizontal 1920×1080, voz es-PE-AlexNeural).
// Requisitos: sitio local levantado contra la BD de pruebas y el usuario admin.transporte en
// privado/CREDENCIALES_USUARIOS.txt. Las cifras son las medidas en la BD al 03/11/2026 (ver el
// artículo "Beneficios del SGV"); si cambian, actualizar las escenas.
// Uso: node grabar-beneficios.mjs [sitio] [articulo.html]
import path from 'node:path';
import fs from 'node:fs';
import { Grabacion, abrirNavegador, claveDe, espera, RAIZ } from './comun.mjs';

const SITIO = process.argv[2] || 'http://localhost:50291';
const ARTICULO = process.argv[3] || '';
const AQUI = path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1'));
const salidaDir = path.join(RAIZ, 'docs', 'DOCUMENTACION', 'capacitacion');
fs.mkdirSync(salidaDir, { recursive: true });
const g = new Grabacion(path.join(AQUI, 'salida', 'beneficios'), { ancho: 1920, alto: 1080, voice: 'es-PE-AlexNeural', rate: '+3%', pausa: 0.5 });

const browser = await abrirNavegador();
const page = await browser.newPage();
await page.setViewport({ width: 1440, height: 810, deviceScaleFactor: 4 / 3 });
await page.emulateMediaFeatures([{ name: 'prefers-color-scheme', value: 'light' }]);

const AZUL = '#1d4ed8';
async function lamina({ eyebrow = 'SISTEMA SGV', titulo, texto = '', cifras = [] }) {
  const cajas = cifras.map(c => `<div style="background:rgba(255,255,255,.1);border-radius:10px;padding:18px 22px;min-width:0">
      <div style="font:600 46px/1.1 'Segoe UI',system-ui">${c[0]}</div>
      <div style="font:400 17px/1.4 'Segoe UI',system-ui;opacity:.85;margin-top:6px">${c[1]}</div></div>`).join('');
  await page.setContent(`<html><head><meta charset="utf-8"></head><body style="margin:0;height:100vh;background:${AZUL};color:#fff;
    font-family:'Segoe UI',system-ui,sans-serif;display:flex;flex-direction:column;justify-content:center;padding:0 96px;box-sizing:border-box">
    <div style="font-size:15px;letter-spacing:.14em;opacity:.8">${eyebrow}</div>
    <div style="font-size:52px;font-weight:700;line-height:1.15;margin-top:14px;max-width:1100px">${titulo}</div>
    ${texto ? `<div style="font-size:22px;line-height:1.5;opacity:.92;margin-top:18px;max-width:1050px">${texto}</div>` : ''}
    ${cajas ? `<div style="display:grid;grid-template-columns:repeat(${cifras.length},1fr);gap:16px;margin-top:40px">${cajas}</div>` : ''}
    <div style="position:absolute;left:96px;bottom:40px;font-size:14px;opacity:.7">Servicios Generales Viviana E.I.R.L.</div>
  </body></html>`);
}
async function rotulo(titulo, sub) {
  await page.evaluate((titulo, sub) => {
    let r = document.getElementById('__rotulo');
    if (!r) {
      r = document.createElement('div'); r.id = '__rotulo';
      r.style.cssText = 'position:fixed;left:32px;bottom:28px;z-index:99999;background:rgba(15,23,42,.92);color:#fff;padding:14px 22px;border-radius:10px;border-left:6px solid #3b82f6;max-width:900px;font-family:Segoe UI,system-ui,sans-serif;box-shadow:0 8px 24px rgba(0,0,0,.25)';
      document.body.appendChild(r);
    }
    r.innerHTML = '<div style="font:700 22px/1.25 Segoe UI,system-ui">' + titulo + '</div>' + (sub ? '<div style="font:400 15px/1.4 Segoe UI,system-ui;opacity:.85;margin-top:4px">' + sub + '</div>' : '');
    document.documentElement.style.scrollBehavior = 'auto';
  }, titulo, sub || '');
}
async function ir(url, esperar = 1500) {
  try { await page.goto(url, { waitUntil: 'networkidle2', timeout: 60000 }); } catch (e) { console.log('aviso: ', url, e.message); }
  await espera(esperar);
  await page.evaluate(() => { const a = document.querySelector('.sgv-aviso'); if (a) a.remove(); });
}
async function bajar(px) { await page.evaluate(px => window.scrollTo(0, px), px); await espera(400); }

try {
  // Ingreso como oficina
  await ir(SITIO + '/Views/Login.aspx', 300);
  await page.type('[id$="txtUsername"]', 'admin.transporte');
  await page.type('[id$="txtPassword"]', claveDe('admin.transporte'));
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), page.click('[id$="btnLogin"]')]);

  g.escena('Este es el sistema SGV de Servicios Generales Viviana: toda la operación de transporte, del despacho a la liquidación, en un solo lugar y con el GPS como fuente de verdad.');
  await lamina({ titulo: 'La operación de transporte en un solo sistema', texto: 'Despacho, seguimiento GPS, documentación, reportes y liquidaciones.' });
  await g.cuadro(page);

  g.escena('Hasta ahora, el STATUS GENERAL se llenaba a mano: entre siete y diez minutos por fila, unas treinta y tres horas al mes. Y con errores: en el último año, el treinta y ocho por ciento de las filas no tenía las horas de Ecuador y una de cada cinco tenía horas imposibles.');
  await lamina({ eyebrow: 'EL PUNTO DE PARTIDA', titulo: 'El STATUS GENERAL a mano', cifras: [['7–10 min', 'por fila del Excel'], ['≈ 33 h', 'al mes solo en el STATUS'], ['38 %', 'de filas sin horas de Ecuador'], ['21 %', 'con horas imposibles']] });
  await g.cuadro(page);

  g.escena('Ahora el sistema consulta el GPS de toda la flota cada diez minutos y registra solo cada hito del viaje: salida de base, Trujillo, planta, frontera, Ecuador y retorno. En promedio, dieciséis horas por viaje que nadie tiene que escribir.');
  await ir(SITIO + '/Views/Exportacion/SeguimientoViaje.aspx', 2500);
  await rotulo('Seguimiento con GPS', 'Consulta automática cada 10 minutos · 16.6 horas registradas por viaje');
  await g.cuadro(page, 2);
  await bajar(420); await rotulo('Seguimiento con GPS', 'Cada hora con su origen: GPS, geocerca o corrección con motivo');
  await g.cuadro(page, 1.5);

  g.escena('El resultado son datos completos: en los viajes que arma el sistema, la frontera, la planta de Ecuador y el retorno quedan registrados en casi el cien por ciento, sin una sola hora imposible.');
  if (ARTICULO && fs.existsSync(ARTICULO)) {
    await page.goto('file:///' + ARTICULO.replace(/\\/g, '/'), { waitUntil: 'networkidle2' });
    await espera(1200);
    await page.evaluate(() => { const h = [...document.querySelectorAll('h2')].find(x => /Datos completos/.test(x.textContent)); if (h) window.scrollTo(0, h.getBoundingClientRect().top + window.scrollY - 20); });
    await espera(900);
  } else {
    await lamina({ titulo: 'Datos completos', cifras: [['62 % → 100 %', 'frontera y TCI'], ['0.2 % → 98 %', 'retorno a la base'], ['21 % → 0', 'horas imposibles']] });
  }
  await g.cuadro(page);

  g.escena('Los reportes que antes se armaban a mano salen en un clic: el STATUS general, la ubicación de las unidades para el grupo de WhatsApp, el cuadro del grifo Santa Rosa y las llegadas estimadas a planta.');
  await lamina({ eyebrow: 'REPORTES EN UN CLIC', titulo: 'Lo que se enviaba armado a mano, ahora sale solo', cifras: [['STATUS', 'general y extra, en Excel'], ['WhatsApp', 'ubicación de unidades en imagen'], ['Grifo', 'Santa Rosa: hora estimada'], ['Llegadas', 'a Trujillo, Jave e Inbalnor']] });
  await g.cuadro(page);

  g.escena('El dashboard muestra la operación en vivo: unidades por fase, esperas en curso y el tiempo típico de cada tramo, comparado con el período anterior.');
  await ir(SITIO + '/Views/Exportacion/DashboardExportacion.aspx', 4000);
  await rotulo('Dashboard de exportación', 'Unidades por fase · esperas en curso · tiempos por tramo');
  await g.cuadro(page, 1.5);
  await bajar(700); await g.cuadro(page, 1.5);

  g.escena('En el despacho, el sistema lee el Word de programación y los documentos del pedido, y muestra en cada lote qué documento falta: factura, guías, CPIC o manifiesto.');
  await ir(SITIO + '/Views/ListaDespachos.aspx', 2500);
  await rotulo('Despachos y documentación', 'Cada lote muestra qué documento falta');
  await g.cuadro(page);

  g.escena('El reporte de cargas resume viajes, toneladas, clientes, productos y lo facturado del mes, sin armar tablas.');
  await ir(SITIO + '/Views/ReporteCargas.aspx', 3000);
  await rotulo('Reporte de cargas', 'Viajes, toneladas, clientes, productos y facturado');
  await g.cuadro(page);

  g.escena('Y ahora el conductor liquida desde su celular: las fechas las pone el GPS, él declara sus gastos y firma con el dedo. La oficina registra lo entregado, aprueba y queda el PDF firmado. Son unas trescientas treinta órdenes de papel al mes que se dejan de imprimir.');
  await ir(SITIO + '/Views/LiquidacionesPendientes.aspx', 2500);
  await rotulo('Liquidación digital', 'Fechas por GPS · firma del conductor · aprobación en pantalla');
  await g.cuadro(page, 1);
  const hayFila = await page.$('button[title*="probar"], button[onclick*="abrirModalAprobar"]');
  if (hayFila) {
    await page.evaluate(() => { const b = document.querySelector('button[onclick*="abrirModalAprobar"]'); if (b) b.click(); });
    await espera(3000);
    await rotulo('Liquidación digital', 'La oficina registra el dinero entregado y aprueba');
    await g.cuadro(page, 1.5);
    await page.keyboard.press('Escape'); await espera(500);
  }

  g.escena('Todo descansa en un registro único de clientes, tractos, semirremolques y choferes, con usuarios por rol y auditoría de cada cambio.');
  await ir(SITIO + '/Views/RegistroChoferes.aspx', 2000);
  await rotulo('Maestros y control', '101 tractos · 134 semirremolques · 61 choferes · auditoría de cada cambio');
  await g.cuadro(page);

  g.escena('En resumen: más de cien horas de oficina al mes que dejan de hacerse a mano, datos completos con respaldo del GPS y una operación que se puede medir.');
  await lamina({ eyebrow: 'EN RESUMEN', titulo: 'Menos trabajo manual, más control', cifras: [['≈ 118 h', 'de oficina al mes que deja de hacerse a mano*'], ['≈ S/ 2 360', 'de imprenta al año'], ['16.6', 'horas por viaje registradas por el GPS']], texto: '<span style="font-size:15px;opacity:.8">* Estimación con los tiempos de referencia del informe "Beneficios del SGV".</span>' });
  await g.cuadro(page);

  g.escena('Sistema SGV. La operación de transporte, bajo control.');
  await lamina({ titulo: 'Sistema SGV', texto: 'La operación de transporte, bajo control.' });
  await g.cuadro(page);
} finally {
  await browser.close();
}

await g.armar(path.join(salidaDir, 'SGV-beneficios-del-sistema.mp4'));
