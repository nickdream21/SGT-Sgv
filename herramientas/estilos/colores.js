#!/usr/bin/env node
/*
 * Variables de color de SGV (E5-11). Herramienta de desarrollo: no se publica.
 *
 *   node herramientas/estilos/colores.js aplicar
 *       Cambia cada color literal (#hex, rgb(), rgba(), white) de las hojas propias por una
 *       variable con el MISMO valor exacto: la semántica si coinciden propiedad y valor, si no
 *       la de la paleta. La paleta vive en el :root de WebSGV/Content/sgv-base.css (se
 *       regenera). Los :root de las otras hojas pasan a ser alias. Se puede repetir: la
 *       segunda vez no cambia nada.
 *
 *   node herramientas/estilos/colores.js verificar [revision]
 *       Expande cada var() con los :root de todas las hojas y exige, hoja por hoja, el mismo
 *       texto que `git show <revision>:<hoja>` (HEAD si no se indica) expandido igual. Los
 *       colores se comparan en forma canónica (#fff = white = #ffffff, rgba con sus números),
 *       el resto del texto debe ser idéntico, también en hover, @media y modales. Además
 *       cuenta los literales fuera de los :root permitidos antes y ahora. Sale con 1 si hay
 *       una diferencia.
 *
 * Hojas: WebSGV/Content/*.css y WebSGV/Content/paginas/**\/*.css (no Content/lib).
 * Quedan como literales: los data-URI (dentro de url()) y --de-f-* (los lee
 * DashboardExportacion.js para pintar las fases).
 */
'use strict';

const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const RAIZ = path.resolve(__dirname, '..', '..');
const BASE = 'WebSGV/Content/sgv-base.css';

// Variables de otras hojas que se quedan con su literal (lista cerrada; la repite EstilosTests).
const LITERALES_PERMITIDOS = [/^--de-f-/];

// ---------------------------------------------------------------------------------------------
// Paletas de referencia (valores exactos). El nombre solo describe; el valor es el literal.
// ---------------------------------------------------------------------------------------------
const TAILWIND = {
    pizarra: { 50: 'f8fafc', 100: 'f1f5f9', 200: 'e2e8f0', 300: 'cbd5e1', 400: '94a3b8', 500: '64748b', 600: '475569', 700: '334155', 800: '1e293b', 900: '0f172a' },
    gris: { 50: 'f9fafb', 100: 'f3f4f6', 200: 'e5e7eb', 300: 'd1d5db', 400: '9ca3af', 500: '6b7280', 600: '4b5563', 700: '374151', 800: '1f2937', 900: '111827' },
    rojo: { 50: 'fef2f2', 100: 'fee2e2', 200: 'fecaca', 300: 'fca5a5', 400: 'f87171', 500: 'ef4444', 600: 'dc2626', 700: 'b91c1c', 800: '991b1b', 900: '7f1d1d' },
    naranja: { 50: 'fff7ed', 100: 'ffedd5', 200: 'fed7aa', 300: 'fdba74', 400: 'fb923c', 500: 'f97316', 600: 'ea580c', 700: 'c2410c', 800: '9a3412', 900: '7c2d12' },
    ambar: { 50: 'fffbeb', 100: 'fef3c7', 200: 'fde68a', 300: 'fcd34d', 400: 'fbbf24', 500: 'f59e0b', 600: 'd97706', 700: 'b45309', 800: '92400e', 900: '78350f' },
    amarillo: { 50: 'fefce8', 100: 'fef9c3', 200: 'fef08a', 300: 'fde047', 400: 'facc15', 500: 'eab308', 600: 'ca8a04', 700: 'a16207', 800: '854d0e', 900: '713f12' },
    verde: { 50: 'f0fdf4', 100: 'dcfce7', 200: 'bbf7d0', 300: '86efac', 400: '4ade80', 500: '22c55e', 600: '16a34a', 700: '15803d', 800: '166534', 900: '14532d' },
    esmeralda: { 50: 'ecfdf5', 100: 'd1fae5', 200: 'a7f3d0', 300: '6ee7b7', 400: '34d399', 500: '10b981', 600: '059669', 700: '047857', 800: '065f46', 900: '064e3b' },
    turquesa: { 50: 'f0fdfa', 100: 'ccfbf1', 200: '99f6e4', 300: '5eead4', 400: '2dd4bf', 500: '14b8a6', 600: '0d9488', 700: '0f766e', 800: '115e59', 900: '134e4a' },
    cian: { 50: 'ecfeff', 100: 'cffafe', 200: 'a5f3fc', 300: '67e8f9', 400: '22d3ee', 500: '06b6d4', 600: '0891b2', 700: '0e7490', 800: '155e75', 900: '164e63' },
    cielo: { 50: 'f0f9ff', 100: 'e0f2fe', 200: 'bae6fd', 300: '7dd3fc', 400: '38bdf8', 500: '0ea5e9', 600: '0284c7', 700: '0369a1', 800: '075985', 900: '0c4a6e' },
    azul: { 50: 'eff6ff', 100: 'dbeafe', 200: 'bfdbfe', 300: '93c5fd', 400: '60a5fa', 500: '3b82f6', 600: '2563eb', 700: '1d4ed8', 800: '1e40af', 900: '1e3a8a' },
    indigo: { 50: 'eef2ff', 100: 'e0e7ff', 200: 'c7d2fe', 300: 'a5b4fc', 400: '818cf8', 500: '6366f1', 600: '4f46e5', 700: '4338ca', 800: '3730a3', 900: '312e81' },
    violeta: { 50: 'f5f3ff', 100: 'ede9fe', 200: 'ddd6fe', 300: 'c4b5fd', 400: 'a78bfa', 500: '8b5cf6', 600: '7c3aed', 700: '6d28d9', 800: '5b21b6', 900: '4c1d95' },
    purpura: { 50: 'faf5ff', 100: 'f3e8ff', 200: 'e9d5ff', 300: 'd8b4fe', 400: 'c084fc', 500: 'a855f7', 600: '9333ea', 700: '7e22ce', 800: '6b21a8', 900: '581c87' },
    rosa: { 50: 'fdf2f8', 100: 'fce7f3', 200: 'fbcfe8', 300: 'f9a8d4', 400: 'f472b6', 500: 'ec4899', 600: 'db2777', 700: 'be185d', 800: '9d174d', 900: '831843' }
};

// Bootstrap 4.6 (colores del tema, grises, estados :hover/:active, foco y alertas).
const BOOTSTRAP = {
    'azul': '007bff', 'azul-hover': '0069d9', 'azul-activo': '0062cc', 'enlace-hover': '0056b3', 'foco': '80bdff',
    'verde': '28a745', 'verde-hover': '218838', 'verde-activo': '1e7e34',
    'rojo': 'dc3545', 'rojo-hover': 'c82333', 'rojo-activo': 'bd2130',
    'amarillo': 'ffc107', 'amarillo-hover': 'e0a800', 'amarillo-activo': 'd39e00',
    'cian': '17a2b8', 'cian-hover': '138496', 'cian-activo': '117a8b',
    'gris-hover': '5a6268', 'gris-activo': '545b62',
    'indigo': '6610f2', 'purpura': '6f42c1', 'rosa': 'e83e8c', 'naranja': 'fd7e14', 'turquesa': '20c997',
    'gris-100': 'f8f9fa', 'gris-200': 'e9ecef', 'gris-300': 'dee2e6', 'gris-400': 'ced4da', 'gris-500': 'adb5bd',
    'gris-600': '6c757d', 'gris-700': '495057', 'gris-800': '343a40', 'gris-900': '212529',
    'claro-hover': 'e2e6ea', 'oscuro-hover': '23272b',
    'alerta-primario-fondo': 'cce5ff', 'alerta-primario-borde': 'b8daff', 'alerta-primario-texto': '004085',
    'alerta-exito-fondo': 'd4edda', 'alerta-exito-borde': 'c3e6cb', 'alerta-exito-texto': '155724',
    'alerta-info-fondo': 'd1ecf1', 'alerta-info-borde': 'bee5eb', 'alerta-info-texto': '0c5460',
    'alerta-aviso-fondo': 'fff3cd', 'alerta-aviso-borde': 'ffeeba', 'alerta-aviso-texto': '856404',
    'alerta-peligro-fondo': 'f8d7da', 'alerta-peligro-borde': 'f5c6cb', 'alerta-peligro-texto': '721c24',
    'alerta-secundario-fondo': 'e2e3e5', 'alerta-secundario-borde': 'd6d8db', 'alerta-secundario-texto': '383d41'
};

const ESPECIALES = { ffffff: 'blanco', '000000': 'negro' };

// Semánticas: nombre, valor (#rrggbb) y propiedad donde se usan en vez de la paleta.
// 'todas' = color, fondo y borde (colores de acento). Las 9 primeras y --sgv-radio existían
// antes de E5-11 y conservan nombre y valor (--sgv-radio no es color y queda literal).
const SEMANTICAS = [
    ['texto', '1f2937', 'color'],
    ['texto-suave', '6b7280', 'color'],
    ['borde', 'e5e7eb', 'borde'],
    ['borde-fuerte', 'd1d5db', 'borde'],
    ['fondo', 'f9fafb', 'fondo'],
    ['primario', '1d4ed8', 'todas'],
    ['ok', '15803d', 'todas'],
    ['alerta', 'b45309', 'todas'],
    ['peligro', 'b91c1c', 'todas'],
    ['texto-medio', '374151', 'color'],
    ['superficie', 'ffffff', 'fondo'],
    ['fondo-pagina', 'f3f4f6', 'fondo'],
    ['primario-osc', '1e40af', 'todas'],
    ['primario-claro', 'eff6ff', 'fondo'],
    ['ok-texto', '166534', 'color'],
    ['ok-fondo', 'dcfce7', 'fondo'],
    ['ok-borde', '86efac', 'borde'],
    ['alerta-texto', '92400e', 'color'],
    ['alerta-fondo', 'fef3c7', 'fondo'],
    ['alerta-borde', 'fcd34d', 'borde'],
    ['peligro-texto', '991b1b', 'color'],
    ['peligro-fondo', 'fee2e2', 'fondo'],
    ['peligro-borde', 'fca5a5', 'borde'],
    ['info-texto', '1e40af', 'color'],
    ['info-fondo', 'dbeafe', 'fondo'],
    ['info-borde', '93c5fd', 'borde'],
    ['foco', '1d4ed8', 'foco']
];
const SEMANTICAS_NO_COLOR = [['radio', '6px']];

const NOMBRES_CSS = { white: 'ffffff', black: '000000' };
// Nombres que no se esperan; si aparecen, aplicar se detiene para que se agreguen arriba.
const NOMBRES_DESCONOCIDOS = ['red', 'green', 'blue', 'gray', 'grey', 'silver', 'orange', 'yellow', 'purple',
    'navy', 'maroon', 'teal', 'olive', 'lime', 'aqua', 'fuchsia', 'pink', 'brown', 'gold', 'darkgray', 'lightgray',
    'whitesmoke', 'gainsboro', 'darkred', 'darkgreen', 'crimson', 'tomato', 'coral', 'salmon', 'beige', 'ivory'];

// ---------------------------------------------------------------------------------------------
// Lectura de CSS
// ---------------------------------------------------------------------------------------------
function listarHojas() {
    const res = [];
    const content = path.join(RAIZ, 'WebSGV', 'Content');
    for (const f of fs.readdirSync(content)) if (f.toLowerCase().endsWith('.css')) res.push('WebSGV/Content/' + f);
    (function recorrer(dir, rel) {
        for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
            if (e.isDirectory()) recorrer(path.join(dir, e.name), rel + e.name + '/');
            else if (e.name.toLowerCase().endsWith('.css')) res.push(rel + e.name);
        }
    })(path.join(content, 'paginas'), 'WebSGV/Content/paginas/');
    return res.sort();
}

/** Copia del texto con comentarios, contenido de cadenas y de url() en blanco (misma longitud). */
function enmascarar(t) {
    const o = t.split('');
    const n = t.length;
    const blanquear = (a, b) => { for (let k = a; k < b && k < n; k++) if (o[k] !== '\n' && o[k] !== '\r') o[k] = ' '; };
    let i = 0;
    while (i < n) {
        const c = t[i];
        if (c === '/' && t[i + 1] === '*') {
            const f = t.indexOf('*/', i + 2);
            const e = f < 0 ? n : f + 2;
            blanquear(i, e);
            i = e;
            continue;
        }
        if (c === '"' || c === "'") {
            let k = i + 1;
            while (k < n && t[k] !== c) { if (t[k] === '\\') k++; k++; }
            blanquear(i + 1, k);
            i = k + 1;
            continue;
        }
        if ((c === 'u' || c === 'U') && /^url\(/i.test(t.substr(i, 4)) && !/[\w-]/.test(t[i - 1] || '')) {
            let k = i + 4;
            while (k < n && /\s/.test(t[k])) k++;
            if (t[k] === '"' || t[k] === "'") {
                const q = t[k];
                k++;
                while (k < n && t[k] !== q) { if (t[k] === '\\') k++; k++; }
                k++;
            }
            while (k < n && t[k] !== ')') k++;
            blanquear(i + 4, k);
            i = k + 1;
            continue;
        }
        i++;
    }
    return o.join('');
}

/** Declaraciones (propiedad: valor) con el rango del valor y el selector de su bloque. */
function declaraciones(t) {
    const m = enmascarar(t);
    const res = [];
    const pila = [];
    let ini = 0;
    for (let i = 0; i < m.length; i++) {
        const c = m[i];
        if (c === '{') {
            pila.push(m.slice(ini, i).replace(/﻿/g, '').trim());
            ini = i + 1;
        } else if (c === ';' || c === '}') {
            if (pila.length) {
                const seg = m.slice(ini, i);
                const r = /^\s*(-?-?[A-Za-z_][\w-]*)\s*:/.exec(seg);
                if (r) {
                    const sel = pila[pila.length - 1];
                    res.push({ prop: r[1], ini: ini + r[0].length, fin: i, selector: sel, raiz: sel === ':root' });
                }
            }
            if (c === '}') pila.pop();
            ini = i + 1;
        }
    }
    return { m, decls: res };
}

const RE_LITERAL = new RegExp(
    '#[0-9a-fA-F]+(?![\\w-])|(?<![\\w-])rgba?\\([^()]*\\)|(?<![\\w#-])(?:' +
    Object.keys(NOMBRES_CSS).concat(NOMBRES_DESCONOCIDOS).join('|') + ')(?![\\w-])', 'gi');

/** Literales de color dentro de los valores: { decl, ini, fin, texto }. */
function literales(t) {
    const { m, decls } = declaraciones(t);
    const res = [];
    for (const d of decls) {
        const v = m.slice(d.ini, d.fin);
        RE_LITERAL.lastIndex = 0;
        let r;
        while ((r = RE_LITERAL.exec(v))) {
            if (/^rgba?\(/i.test(r[0]) && /var\(/i.test(r[0])) continue;
            res.push({ decl: d, ini: d.ini + r.index, fin: d.ini + r.index + r[0].length, texto: t.slice(d.ini + r.index, d.ini + r.index + r[0].length) });
        }
    }
    return res;
}

function numeroCss(s) { return Number(String(s).trim()); }

/** {r,g,b,a} de un literal; lanza si no es un color reconocible. */
function leerColor(lit) {
    const s = lit.trim().toLowerCase();
    if (s[0] === '#') {
        let h = s.slice(1);
        if (h.length === 3 || h.length === 4) h = h.split('').map(x => x + x).join('');
        if (h.length !== 6 && h.length !== 8) throw new Error('Hex inválido: ' + lit);
        return { r: parseInt(h.slice(0, 2), 16), g: parseInt(h.slice(2, 4), 16), b: parseInt(h.slice(4, 6), 16), a: h.length === 8 ? Math.round(parseInt(h.slice(6, 8), 16) / 255 * 1000) / 1000 : 1 };
    }
    const f = /^rgba?\(([^()]*)\)$/.exec(s);
    if (f) {
        const p = f[1].split(/\s*[,/]\s*|\s+/).filter(x => x !== '');
        if (p.length < 3 || p.length > 4 || p.some(x => x.endsWith('%'))) throw new Error('rgb() no soportado: ' + lit);
        return { r: numeroCss(p[0]), g: numeroCss(p[1]), b: numeroCss(p[2]), a: p.length === 4 ? numeroCss(p[3]) : 1 };
    }
    if (NOMBRES_CSS[s]) return leerColor('#' + NOMBRES_CSS[s]);
    throw new Error('Color con nombre no previsto: ' + lit + ' (agréguelo a NOMBRES_CSS)');
}

const hex2 = n => n.toString(16).padStart(2, '0');
function hexDe(c) { return hex2(c.r) + hex2(c.g) + hex2(c.b); }
function canonico(c) { return c.a === 1 ? '#' + hexDe(c) : 'rgba(' + c.r + ',' + c.g + ',' + c.b + ',' + c.a + ')'; }

// ---------------------------------------------------------------------------------------------
// Nombres
// ---------------------------------------------------------------------------------------------
const NOMBRE_POR_HEX = (() => {
    const mapa = {};
    for (const [h, n] of Object.entries(ESPECIALES)) mapa[h] = n;
    for (const [fam, pasos] of Object.entries(TAILWIND))
        for (const [paso, h] of Object.entries(pasos)) if (!mapa[h]) mapa[h] = fam + '-' + paso;
    for (const [n, h] of Object.entries(BOOTSTRAP)) if (!mapa[h]) mapa[h] = 'bs-' + n;
    return mapa;
})();
function nombrePaleta(hex) { return '--sgv-' + (NOMBRE_POR_HEX[hex] || 'suelto-' + hex); }

function claseDePropiedad(prop) {
    const p = prop.toLowerCase();
    if (p === 'color') return 'color';
    if (p === 'background' || p.startsWith('background-')) return 'fondo';
    if (p.startsWith('outline')) return 'foco';
    if (p.startsWith('border')) return 'borde';
    return null;
}
function claseDeAlias(nombre) {
    const n = nombre.toLowerCase();
    if (/texto|ink|muted|suave|neutral/.test(n)) return 'color';
    if (/borde|border|grid/.test(n)) return 'borde';
    if (/fondo|bg|card|claro|soft|light-gray/.test(n)) return 'fondo';
    return null;
}
function semanticaPara(hex, clase) {
    // Primero la de la misma propiedad (p. ej. info-texto para color), después las de acento.
    for (const [n, h, c] of SEMANTICAS) if (h === hex && c === clase) return '--sgv-' + n;
    for (const [n, h, c] of SEMANTICAS) if (h === hex && c === 'todas') return '--sgv-' + n;
    return null;
}

// ---------------------------------------------------------------------------------------------
// aplicar
// ---------------------------------------------------------------------------------------------
function leer(rel) { return fs.readFileSync(path.join(RAIZ, rel), 'utf8'); }
function escribir(rel, t) { fs.writeFileSync(path.join(RAIZ, rel), t, 'utf8'); }

function bloqueRaiz(t) {
    const { m } = declaraciones(t);
    const r = /(^|[\s}﻿]):root\s*\{/.exec(m);
    if (!r) return null;
    const ini = r.index + r[0].length;
    const fin = m.indexOf('}', ini);
    return { ini, fin };
}

function esPermitido(nombre) { return LITERALES_PERMITIDOS.some(re => re.test(nombre)); }

function aplicar() {
    const hojas = listarHojas();
    const base = leer(BASE);
    const paleta = new Map();   // hex -> nombre
    const triples = new Map();  // hex -> nombre-rgb

    // Paleta que ya está en sgv-base (para que repetir sea idempotente).
    const raizBase = bloqueRaiz(base);
    {
        const { decls } = declaraciones(base);
        for (const d of decls) {
            if (!d.raiz) continue;
            const v = base.slice(d.ini, d.fin).trim();
            if (/-rgb$/.test(d.prop)) { const c = leerColor('rgb(' + v + ')'); triples.set(hexDe(c), d.prop); }
            else if (/^#|^rgba?\(/i.test(v) && !SEMANTICAS.some(s => '--sgv-' + s[0] === d.prop)) paleta.set(hexDe(leerColor(v)), d.prop);
        }
    }
    const agregarPaleta = hex => { if (!paleta.has(hex)) paleta.set(hex, nombrePaleta(hex)); return paleta.get(hex); };
    const agregarTriple = hex => { if (!triples.has(hex)) triples.set(hex, nombrePaleta(hex) + '-rgb'); return triples.get(hex); };
    for (const [, h] of SEMANTICAS) agregarPaleta(h);

    const nuevas = {};
    let cambios = 0;
    for (const rel of hojas) {
        let t = leer(rel);
        // var(--sgv-x, #literal): la variable siempre está definida (sgv-base va en todas las páginas).
        t = t.replace(/var\((--sgv-[\w-]+)\s*,[^()]*\)/g, 'var($1)');
        const lits = literales(t);
        const raiz = rel === BASE ? bloqueRaiz(t) : null;
        let salida = '';
        let pos = 0;
        for (const l of lits) {
            if (raiz && l.ini >= raiz.ini && l.fin <= raiz.fin) continue; // la paleta misma
            if (l.decl.raiz && esPermitido(l.decl.prop)) continue;
            const c = leerColor(l.texto);
            const hex = hexDe(c);
            let reemplazo;
            if (c.a !== 1) {
                const alfa = /^rgba?\(/i.test(l.texto) ? l.texto.replace(/^rgba?\(|\)$/gi, '').split(/\s*,\s*/)[3] : String(c.a);
                reemplazo = 'rgba(var(' + agregarTriple(hex) + '), ' + alfa.trim() + ')';
            } else {
                const clase = l.decl.prop.startsWith('--') ? claseDeAlias(l.decl.prop) : claseDePropiedad(l.decl.prop);
                const sem = semanticaPara(hex, clase);
                reemplazo = 'var(' + (sem || agregarPaleta(hex)) + ')';
            }
            salida += t.slice(pos, l.ini) + reemplazo;
            pos = l.fin;
            cambios++;
        }
        salida += t.slice(pos);
        nuevas[rel] = salida;
    }

    // :root de sgv-base regenerado.
    const familias = Object.keys(TAILWIND);
    const orden = nombre => {
        const n = nombre.replace(/^--sgv-/, '');
        if (n === 'blanco' || n === 'negro') return [0, n === 'blanco' ? 0 : 1, 0];
        const fam = familias.findIndex(f => n.startsWith(f + '-') && /^\d+$/.test(n.slice(f.length + 1)));
        if (fam >= 0) return [1, fam, Number(n.slice(familias[fam].length + 1))];
        if (n.startsWith('bs-')) return [2, Object.keys(BOOTSTRAP).indexOf(n.slice(3)), 0];
        return [3, 0, n];
    };
    const cmp = (a, b) => { const x = orden(a), y = orden(b); for (let i = 0; i < 3; i++) { if (x[i] < y[i]) return -1; if (x[i] > y[i]) return 1; } return 0; };
    const nombres = [...paleta.entries()].sort((a, b) => cmp(a[1], b[1]));
    const trip = [...triples.entries()].sort((a, b) => cmp(a[1].replace(/-rgb$/, ''), b[1].replace(/-rgb$/, '')));
    const lineas = [];
    lineas.push('');
    lineas.push('    /* Semánticas: lo que usan las hojas cuando coinciden propiedad y valor. Las 9 primeras');
    lineas.push('       y --sgv-radio existían antes de E5-11 (mismo nombre y valor). E5-12 cambia sus valores. */');
    for (const [n, h] of SEMANTICAS) lineas.push('    --sgv-' + n + ': var(' + paleta.get(h) + ');');
    for (const [n, v] of SEMANTICAS_NO_COLOR) lineas.push('    --sgv-' + n + ': ' + v + ';');
    lineas.push('');
    lineas.push('    /* Paleta exacta (sin redondeos): un valor por variable. Pasos de Tailwind por familia,');
    lineas.push('       --sgv-bs-* de Bootstrap 4 heredado y --sgv-suelto-* fuera de toda escala (E5-12 los quita).');
    lineas.push('       La genera herramientas/estilos/colores.js aplicar. */');
    for (const [h, n] of nombres) lineas.push('    ' + n + ': #' + h + ';');
    lineas.push('');
    lineas.push('    /* Triples para transparencias: rgba(var(--sgv-x-rgb), alfa). */');
    for (const [h, n] of trip) {
        const c = leerColor('#' + h);
        lineas.push('    ' + n + ': ' + c.r + ', ' + c.g + ', ' + c.b + ';');
    }
    const b = nuevas[BASE];
    const rb = bloqueRaiz(b);
    const eol = b.includes('\r\n') ? '\r\n' : '\n';
    nuevas[BASE] = b.slice(0, rb.ini) + lineas.join(eol) + eol + b.slice(rb.fin);

    let escritas = 0;
    for (const rel of hojas) if (nuevas[rel] !== leer(rel)) { escribir(rel, nuevas[rel]); escritas++; }
    console.log('Hojas: ' + hojas.length + ', reemplazos: ' + cambios + ', hojas escritas: ' + escritas);
    console.log('Paleta: ' + paleta.size + ' colores (' +
        [...paleta.values()].filter(n => /^--sgv-bs-/.test(n)).length + ' bs, ' +
        [...paleta.values()].filter(n => /^--sgv-suelto-/.test(n)).length + ' sueltos), ' +
        triples.size + ' triples, ' + SEMANTICAS.length + ' semánticas de color.');
}

// ---------------------------------------------------------------------------------------------
// verificar
// ---------------------------------------------------------------------------------------------
function definicionesRaiz(textos) {
    const tabla = new Map();
    for (const t of textos) {
        const { decls } = declaraciones(t);
        for (const d of decls) if (d.raiz && d.prop.startsWith('--')) tabla.set(d.prop, t.slice(d.ini, d.fin).trim());
    }
    return tabla;
}

/** Sustituye var(--x[, fb]) por su valor de la tabla (recursivo); deja las que no están. */
function expandir(texto, tabla) {
    let t = texto;
    for (let vuelta = 0; vuelta < 10; vuelta++) {
        let cambio = false;
        t = t.replace(/var\(\s*(--[\w-]+)\s*(?:,([^()]*))?\)/g, (todo, n, fb) => {
            if (tabla.has(n)) { cambio = true; return tabla.get(n); }
            return todo;
        });
        if (!cambio) break;
    }
    return t;
}

/** Colores en forma canónica dentro de los valores (comentarios, cadenas y url() intactos). */
function normalizar(t) {
    const lits = literales(t);
    let s = '';
    let pos = 0;
    for (const l of lits) { s += t.slice(pos, l.ini) + canonico(leerColor(l.texto)); pos = l.fin; }
    return s + t.slice(pos);
}

function sinRaiz(t) {
    const r = bloqueRaiz(t);
    return r ? t.slice(0, r.ini) + t.slice(r.fin) : t;
}

function contarFuera(t, rel) {
    // Literales fuera de los :root permitidos: cualquier :root de sgv-base y --de-f-*.
    return literales(t).filter(l => !(l.decl.raiz && (rel === BASE || esPermitido(l.decl.prop)))).length;
}

function verificar(rev) {
    const hojas = listarHojas();
    const antes = {};
    const lf = s => s.replace(/\r\n/g, '\n');
    for (const rel of hojas) antes[rel] = lf(cp.execFileSync('git', ['show', rev + ':' + rel], { cwd: RAIZ, encoding: 'utf8', maxBuffer: 1 << 26 }));
    const ahora = {};
    for (const rel of hojas) ahora[rel] = lf(leer(rel));
    const tablaAntes = definicionesRaiz(hojas.map(h => antes[h]));
    const tablaAhora = definicionesRaiz(hojas.map(h => ahora[h]));

    let difs = 0;
    for (const rel of hojas) {
        let a = antes[rel], b = ahora[rel];
        if (rel === BASE) { a = sinRaiz(a); b = sinRaiz(b); }
        // Fuera de :root, cada var() resuelta con la tabla de su versión.
        const na = normalizar(expandir(a, tablaAntes));
        const nb = normalizar(expandir(b, tablaAhora));
        if (na !== nb) {
            difs++;
            const la = na.split('\n'), lb = nb.split('\n');
            let i = 0;
            while (i < la.length && la[i] === lb[i]) i++;
            console.log('DISTINTA ' + rel + ' (línea ' + (i + 1) + ')\n  antes: ' + (la[i] || '').trim() + '\n  ahora: ' + (lb[i] || '').trim());
        }
    }
    // Variables :root de antes: mismo valor resuelto ahora (incluye las 10 --sgv-* de sgv-base).
    const resolver = (tabla, n) => normalizar('a{x:' + expandir('var(' + n + ')', tabla) + '}');
    for (const n of tablaAntes.keys()) {
        if (!tablaAhora.has(n)) { difs++; console.log('FALTA la variable ' + n); continue; }
        if (resolver(tablaAntes, n) !== resolver(tablaAhora, n)) { difs++; console.log('CAMBIÓ el valor de ' + n); }
    }
    const fueraAntes = hojas.reduce((s, h) => s + contarFuera(antes[h], h), 0);
    const fueraAhora = hojas.reduce((s, h) => s + contarFuera(ahora[h], h), 0);
    console.log('Hojas comparadas: ' + hojas.length + ' contra ' + rev + '. Diferencias: ' + difs + '.');
    console.log('Literales de color fuera de los :root permitidos: ' + rev + ' ' + fueraAntes + ', ahora ' + fueraAhora + '.');
    process.exitCode = difs === 0 ? 0 : 1;
}

const modo = process.argv[2];
if (modo === 'aplicar') aplicar();
else if (modo === 'verificar') verificar(process.argv[3] || 'HEAD');
else {
    console.log('Uso: node herramientas/estilos/colores.js aplicar | verificar [revision]');
    process.exitCode = 2;
}
