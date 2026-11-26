"""Arma un video tutorial (1920x1080, H.264 + AAC) a partir de una toma, sus eventos y un guion.

  python armar.py --toma x.mkv --eventos eventos.json --guion guion.json --salida video.mp4
                  [--voz es-PE-CamilaNeural] [--velocidad +0%] [--rapido]

Por cada paso del guion: locución con voz de Perú (../voz.mjs), tramo de la toma sin las esperas, ajustado a la
duración de la voz, con puntero sintético, onda en cada clic, resaltado del campo al escribir, zoom suave,
título arriba y subtítulos abajo. Portada (3 s) y cierre (2 s). Ver README.md.
"""
import argparse
import hashlib
import os
import re
import subprocess
import sys
import wave

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import video_comun as vc  # noqa: E402

ANCHO, ALTO, FPS = 1920, 1080, 30
TASA_AUDIO = 24000
VOZ_MJS = os.path.abspath(os.path.join(vc.AQUI, '..', 'voz.mjs'))
FUENTE = r'C:\Windows\Fonts\segoeui.ttf'
FUENTE_NEGRITA = r'C:\Windows\Fonts\segoeuib.ttf'

AZUL = (19, 49, 92)          # RGB, franja del título y portada
ACENTO = (255, 176, 32)      # RGB, onda del clic y resaltado
ESPERA_LARGA, ESPERA_CORTA = 1.5, 0.4
VELOCIDAD_MAX = 2.0
ANTICIPO_PUNTERO = 0.6
DURACION_ONDA = 0.4
DURACION_ZOOM = 0.5
ENCADENAR = 4.0              # si el siguiente clic llega antes, el puntero se queda visible y se desliza


def suave(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


def fuente(tam, negrita=False):
    try:
        return ImageFont.truetype(FUENTE_NEGRITA if negrita else FUENTE, tam)
    except OSError:
        return ImageFont.load_default()


# ------------------------------------------------------------------------------------------- capas RGBA

class Capa:
    """Imagen RGBA lista para mezclar rápido sobre un cuadro BGR."""

    def __init__(self, rgba, x, y):
        a = np.asarray(rgba, dtype=np.uint16)
        self.bgr = a[..., [2, 1, 0]] * a[..., 3:4]
        self.inv = 255 - a[..., 3:4]
        self.x, self.y = int(x), int(y)
        self.h, self.w = a.shape[:2]

    def mezclar(self, cuadro, x=None, y=None, opacidad=1.0):
        x = self.x if x is None else int(round(x))
        y = self.y if y is None else int(round(y))
        x0, y0 = max(x, 0), max(y, 0)
        x1, y1 = min(x + self.w, cuadro.shape[1]), min(y + self.h, cuadro.shape[0])
        if x0 >= x1 or y0 >= y1 or opacidad <= 0:
            return
        sx, sy = x0 - x, y0 - y
        bgr = self.bgr[sy:sy + y1 - y0, sx:sx + x1 - x0]
        inv = self.inv[sy:sy + y1 - y0, sx:sx + x1 - x0]
        roi = cuadro[y0:y1, x0:x1].astype(np.uint16)
        if opacidad >= 1.0:
            cuadro[y0:y1, x0:x1] = ((roi * inv + bgr) // 255).astype(np.uint8)
        else:
            mezcla = (roi * inv + bgr) // 255
            cuadro[y0:y1, x0:x1] = (roi + (mezcla.astype(np.int32) - roi) * opacidad).astype(np.uint8)


def imagen_puntero(alto=28):
    """Flecha blanca con borde negro; la punta queda en (2, 2) de la imagen."""
    k = 4
    puntos = [(0, 0), (0, 21), (5, 16.5), (8.6, 24.5), (11.6, 23.2), (8.1, 15.4), (14.6, 15.2)]
    esc = alto / 25.0 * k
    img = Image.new('RGBA', (int(17 * esc) + 8 * k, int(27 * esc) + 8 * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = [(2 * k + x * esc, 2 * k + y * esc) for x, y in puntos]
    sombra = [(x + 1.5 * k, y + 2 * k) for x, y in pts]
    d.polygon(sombra, fill=(0, 0, 0, 70))
    d.polygon(pts, fill=(255, 255, 255, 255), outline=(0, 0, 0, 255))
    d.line(pts + [pts[0]], fill=(0, 0, 0, 255), width=int(1.6 * k), joint='curve')
    return img.resize((img.width // k, img.height // k), Image.LANCZOS)


def capa_texto_caja(lineas, tam, ancho_max, negrita=False, fondo=(0, 0, 0, 175), relleno=(28, 14)):
    f = fuente(tam, negrita)
    medidas = [f.getbbox(t) for t in lineas]
    alto_linea = int(tam * 1.32)
    w = min(max(b[2] - b[0] for b in medidas) + 2 * relleno[0], ancho_max)
    h = alto_linea * len(lineas) + 2 * relleno[1]
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=14, fill=fondo)
    for i, t in enumerate(lineas):
        tw = f.getbbox(t)[2]
        d.text(((w - tw) / 2, relleno[1] + i * alto_linea - f.getbbox('Ág')[1] + (alto_linea - tam) / 2), t,
               font=f, fill=(255, 255, 255, 255))
    return img


def partir_lineas(texto, f, ancho):
    palabras, lineas, actual = texto.split(), [], ''
    for p in palabras:
        prueba = (actual + ' ' + p).strip()
        if f.getbbox(prueba)[2] <= ancho or not actual:
            actual = prueba
        else:
            lineas.append(actual)
            actual = p
    if actual:
        lineas.append(actual)
    return lineas


def frases(texto):
    """Frases para subtítulos: corta en . ! ? ; : y, si una frase es larga, también en comas."""
    trozos = [t.strip() for t in re.split(r'(?<=[.!?;:])\s+', texto.strip()) if t.strip()]
    salida = []
    for t in trozos:
        if len(t) <= 95:
            salida.append(t)
            continue
        actual = ''
        for parte in re.split(r'(?<=,)\s+', t):
            if actual and len(actual) + len(parte) > 95:
                salida.append(actual)
                actual = parte
            else:
                actual = (actual + ' ' + parte).strip()
        if actual:
            salida.append(actual)
    return salida


def capa_titulo(titulo, empresa):
    alto = 74
    img = Image.new('RGBA', (ANCHO, alto), AZUL + (248,))
    d = ImageDraw.Draw(img)
    d.rectangle([0, alto - 4, ANCHO, alto], fill=ACENTO + (255,))
    f = fuente(36, True)
    d.text((40, (alto - 4 - 36) / 2 - f.getbbox('Ág')[1] + 4), titulo, font=f, fill=(255, 255, 255, 255))
    fe = fuente(22)
    tw = fe.getbbox(empresa)[2]
    d.text((ANCHO - tw - 40, (alto - 4 - 22) / 2 - fe.getbbox('Ág')[1] + 2), empresa, font=fe,
           fill=(255, 255, 255, 190))
    return Capa(img, 0, 0)


def capas_subtitulos(texto, duracion_voz):
    """[(desde, hasta, Capa)] repartidas en proporción a la longitud de cada frase."""
    lista = frases(texto)
    if not lista:
        return []
    total = sum(len(t) for t in lista)
    f = fuente(38)
    t, salida = 0.0, []
    for fr in lista:
        dur = duracion_voz * len(fr) / total
        lineas = partir_lineas(fr, f, 1500)
        img = capa_texto_caja(lineas, 38, 1600)
        salida.append((t, t + dur, Capa(img, (ANCHO - img.width) // 2, ALTO - img.height - 46)))
        t += dur
    return salida


def imagen_portada(titulo, subtitulo, empresa, cierre=False):
    img = Image.new('RGB', (ANCHO, ALTO), AZUL)
    d = ImageDraw.Draw(img)
    for y in range(ALTO):  # degradado vertical
        k = y / ALTO
        d.line([(0, y), (ANCHO, y)], fill=(int(11 + 14 * k), int(30 + 30 * k), int(60 + 50 * k)))
    fe = fuente(34, True)
    ft = fuente(76, True)
    fs = fuente(40)
    d.text((140, 150), empresa.upper(), font=fe, fill=(255, 255, 255))
    d.rectangle([140, 210, 340, 216], fill=ACENTO)
    y = 380
    for linea in partir_lineas(titulo, ft, ANCHO - 280):
        d.text((140, y), linea, font=ft, fill=(255, 255, 255))
        y += 96
    y += 20
    for linea in partir_lineas(subtitulo or '', fs, ANCHO - 280):
        d.text((140, y), linea, font=fs, fill=(214, 226, 240))
        y += 56
    pie = 'Sistema de Gestión de Viajes (SGV)'
    d.text((140, ALTO - 140), pie, font=fuente(28), fill=(190, 205, 225))
    return cv2.cvtColor(np.asarray(img), cv2.COLOR_RGB2BGR)


# ------------------------------------------------------------------------------------------- audio

def generar_voz(texto, carpeta, voz, velocidad):
    clave = hashlib.sha1(f'{voz}|{velocidad}|{texto}'.encode('utf-8')).hexdigest()[:16]
    wav = os.path.join(carpeta, f'voz_{clave}.wav')
    if not os.path.exists(wav):
        mp3 = os.path.join(carpeta, f'voz_{clave}.mp3')
        r = subprocess.run(['node', VOZ_MJS, texto, mp3, voz, velocidad], capture_output=True, text=True,
                           encoding='utf-8', errors='replace')
        if r.returncode != 0 or not os.path.exists(mp3):
            raise SystemExit('No se pudo generar la locución con voz.mjs (¿npm install en herramientas/video? '
                             '¿hay internet?):\n' + (r.stderr or r.stdout or '').strip()[-1500:])
        subprocess.run([vc.FFMPEG, '-v', 'error', '-y', '-i', mp3, '-ac', '1', '-ar', str(TASA_AUDIO), wav],
                       check=True)
        os.remove(mp3)
    with wave.open(wav, 'rb') as w:
        datos = np.frombuffer(w.readframes(w.getnframes()), np.int16)
    return datos


def escribir_wav(ruta, muestras):
    with wave.open(ruta, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(TASA_AUDIO)
        w.writeframes(muestras.astype(np.int16).tobytes())


# ------------------------------------------------------------------------------------------- video de entrada

class Lector:
    """Lee cuadros BGR de la toma por índice; reabre ffmpeg con -ss si hay que retroceder o saltar lejos."""

    def __init__(self, video, ancho, alto, fps):
        self.video, self.w, self.h, self.fps = video, ancho, alto, fps
        self.p, self.actual, self.ultimo = None, -1, None
        self.tam = ancho * alto * 3

    def _abrir(self, indice):
        if self.p:
            self.p.kill()
        self.p = subprocess.Popen([vc.FFMPEG, '-v', 'error', '-ss', f'{indice / self.fps:.4f}', '-i', self.video,
                                   '-f', 'rawvideo', '-pix_fmt', 'bgr24', '-'], stdout=subprocess.PIPE,
                                  bufsize=self.tam * 2)
        self.actual = indice - 1
        self.ultimo = None

    def cuadro(self, indice):
        if self.p is None or indice < self.actual or indice > self.actual + 150:
            self._abrir(indice)
        while self.actual < indice or self.ultimo is None:
            b = self.p.stdout.read(self.tam)
            if len(b) < self.tam:
                break  # fin del video: se repite el último cuadro
            self.ultimo = np.frombuffer(b, np.uint8).reshape(self.h, self.w, 3)
            self.actual += 1
        if self.ultimo is None:
            raise SystemExit(f'No se pudo leer el cuadro {indice} de {self.video}')
        return self.ultimo

    def cerrar(self):
        if self.p:
            self.p.kill()


def detectar_campo(gris, x, y):
    """Rectángulo del campo de texto que contiene (x, y): bordes horizontales arriba/abajo y verticales a los lados."""
    h, w = gris.shape
    x, y = int(x), int(y)
    if not (2 <= x < w - 2 and 2 <= y < h - 2):
        return None

    def borde_horizontal(y0, paso):
        for yy in range(y0, y0 + paso * 70, paso):
            if not (1 <= yy < h - 1):
                return None
            ok = all(abs(int(gris[yy, xx]) - int(gris[yy - paso, xx])) > 22 for xx in (x - 14, x, x + 14)
                     if 0 <= xx < w)
            if ok:
                return yy
        return None

    arriba, abajo = borde_horizontal(y, -1), borde_horizontal(y, 1)
    if arriba is None or abajo is None or not (16 <= abajo - arriba <= 120):
        return None
    fila = min(arriba + 3, abajo - 1)
    izq = der = None
    for xx in range(x, max(x - 900, 1), -1):
        if abs(int(gris[fila, xx]) - int(gris[fila, xx - 1])) > 22:
            izq = xx
            break
    for xx in range(x, min(x + 900, w - 1)):
        if abs(int(gris[fila, xx]) - int(gris[fila, xx + 1])) > 22:
            der = xx
            break
    if izq is None or der is None or not (30 <= der - izq <= 1400):
        return None
    return [izq - 3, arriba - 3, der + 3, abajo + 3]


# ------------------------------------------------------------------------------------------- pasos

def tramo_sin_esperas(dif, f0, f1, protegidos, fps):
    """Índices de cuadros del tramo, con las esperas largas (sin cambio visible) acortadas."""
    cambia = vc.hay_cambio(dif)
    kept, quietos = [], []
    larga, corta = int(ESPERA_LARGA * fps), int(ESPERA_CORTA * fps)

    def volcar():
        if len(quietos) > larga:
            kept.extend(quietos[:corta])
        else:
            kept.extend(quietos)
        quietos.clear()

    for f in range(f0, f1):
        quieto = f < len(cambia) and not cambia[f] and not protegidos[f - f0]
        if quieto:
            quietos.append(f)
        else:
            volcar()
            kept.append(f)
    volcar()
    return kept or [f0]


def preparar_paso(n, paso, eventos, dif, fps, total_cuadros, voz_muestras, conv, guion, rect_base, info):
    desde = float(paso['desde'])
    hasta = float(paso['hasta'])
    f0 = max(int(desde * fps), 0)
    f1 = max(min(int(hasta * fps), total_cuadros), f0 + 1)
    propios = [e for e in eventos if desde <= e.get('tEfecto', e['t']) < hasta]
    protegidos = np.zeros(f1 - f0, bool)
    for e in propios:
        c = int(e.get('tEfecto', e['t']) * fps)
        a, b = max(c - int(0.7 * fps), f0), min(c + int(0.5 * fps), f1)
        protegidos[a - f0:b - f0] = True
    kept = tramo_sin_esperas(dif, f0, f1, protegidos, fps)

    dur_voz = len(voz_muestras) / TASA_AUDIO
    objetivo = dur_voz + 0.5
    disponible = len(kept) / fps
    velocidad = min(disponible / objetivo, VELOCIDAD_MAX) if disponible > objetivo else 1.0
    n_cuadros = int(round(objetivo * FPS))
    indices = [kept[min(int(k * velocidad * fps / FPS), len(kept) - 1)] for k in range(n_cuadros)]

    kept_arr = np.array(kept)
    marcas = []
    for e in propios:
        c = int(round(e.get('tEfecto', e['t']) * fps))
        pos = int(np.searchsorted(kept_arr, c))
        t_out = pos / fps / velocidad
        if t_out <= objetivo - 0.05:
            m = dict(e)
            m['tOut'] = t_out
            marcas.append(m)

    zoom = paso.get('zoom')
    if zoom and guion.get('coordenadas') == 'marco':
        ox, oy = conv.get('origen', (0, 0))
        zoom = [conv['viewportX'] + zoom[0] * conv['escala'] - ox, conv['viewportY'] + zoom[1] * conv['escala'] - oy,
                conv['viewportX'] + zoom[2] * conv['escala'] - ox, conv['viewportY'] + zoom[3] * conv['escala'] - oy]
    rect_zoom = vc.a_16_9(zoom, info['ancho'], info['alto']) if zoom else None
    print(f'  paso {n}: toma {desde:.1f}-{hasta:.1f} s ({(f1 - f0) / fps:.1f} s), sin esperas {disponible:.1f} s, '
          f'voz {dur_voz:.1f} s, velocidad x{velocidad:.2f}, {len(marcas)} eventos')
    return {'indices': indices, 'marcas': marcas, 'duracion': objetivo, 'dur_voz': dur_voz,
            'rect_zoom': rect_zoom, 'rect_base': rect_base}


def rect_en(t, p):
    base, z = p['rect_base'], p['rect_zoom']
    if not z:
        return base
    T = p['duracion']
    if t < DURACION_ZOOM:
        u = suave(t / DURACION_ZOOM)
    elif t > T - DURACION_ZOOM:
        u = suave((T - t) / DURACION_ZOOM)
    else:
        u = 1.0
    return [b + (zz - b) * u for b, zz in zip(base, z)]


def puntero_en(t, anclas):
    """(x, y, opacidad) del puntero en coordenadas de la toma, o None."""
    if not anclas:
        return None
    sig = next((i for i, a in enumerate(anclas) if a[0] >= t), None)
    ant = (sig - 1) if sig is not None else len(anclas) - 1
    if sig is not None and t >= anclas[sig][0] - ANTICIPO_PUNTERO:
        ts, xs, ys = anclas[sig]
        u = suave((t - (ts - ANTICIPO_PUNTERO)) / ANTICIPO_PUNTERO)
        if ant >= 0 and ts - anclas[ant][0] < ENCADENAR:
            _, xa, ya = anclas[ant]
            return xa + (xs - xa) * u, ya + (ys - ya) * u, 1.0
        return xs + 70 * (1 - u), ys + 55 * (1 - u), min(1.0, u * 2)
    if ant >= 0 and t >= anclas[ant][0]:
        ta, xa, ya = anclas[ant]
        if sig is not None and anclas[sig][0] - ta < ENCADENAR:
            return xa, ya, 1.0
        if t - ta < 1.0:
            return xa, ya, 1.0
        if t - ta < 1.3:
            return xa, ya, 1.0 - (t - ta - 1.0) / 0.3
    return None


def renderizar_paso(p, lector, salida, titulo, subtitulos, flecha):
    marcas = p['marcas']
    anclas = [(m['tOut'], m['x'], m['y']) for m in marcas
              if m.get('x') is not None and m['tipo'] in ('clic', 'doble_clic', 'triple_clic', 'desplazar')]
    ondas = []
    for m in marcas:
        if m.get('x') is not None and m['tipo'] in ('clic', 'doble_clic', 'triple_clic'):
            ondas.append((m['tOut'], m['x'], m['y']))
            if m['tipo'] != 'clic':
                ondas.append((m['tOut'] + 0.15, m['x'], m['y']))
    escrituras = []
    for m in marcas:
        if m['tipo'] in ('escribir', 'formulario'):
            prev = [a for a in anclas if a[0] <= m['tOut']]
            if m.get('x') is not None:
                escrituras.append({'t': m['tOut'], 'x': m['x'], 'y': m['y'], 'rect': None})
            elif prev:
                escrituras.append({'t': m['tOut'], 'x': prev[-1][1], 'y': prev[-1][2], 'rect': None})
    hx, hy = 2, 2  # punta de la flecha dentro de la imagen
    for k, idx in enumerate(p['indices']):
        t = k / FPS
        fuente_cuadro = lector.cuadro(idx)
        x0, y0, x1, y1 = rect_en(t, p)
        esc = ANCHO / (x1 - x0)
        ix0, iy0, ix1, iy1 = int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))
        if ix0 == 0 and iy0 == 0 and (ix1, iy1) == (fuente_cuadro.shape[1], fuente_cuadro.shape[0]) and \
                fuente_cuadro.shape[:2] == (ALTO, ANCHO):
            cuadro = fuente_cuadro.copy()
        else:
            recorte = fuente_cuadro[max(iy0, 0):iy1, max(ix0, 0):ix1]
            interp = cv2.INTER_AREA if esc < 1 else cv2.INTER_LINEAR
            cuadro = cv2.resize(recorte, (ANCHO, ALTO), interpolation=interp)

        def a_salida(x, y):
            return (x - x0) * esc, (y - y0) * esc

        # Resaltado del campo al escribir (se detecta el borde del campo en el cuadro de ese instante).
        for e in escrituras:
            if e['t'] <= t < e['t'] + 1.6:
                if e['rect'] is None:
                    gris = cv2.cvtColor(fuente_cuadro, cv2.COLOR_BGR2GRAY)
                    e['rect'] = detectar_campo(gris, e['x'], e['y']) or \
                        [e['x'] - 170, e['y'] - 24, e['x'] + 170, e['y'] + 24]
                ax, ay = a_salida(e['rect'][0], e['rect'][1])
                bx, by = a_salida(e['rect'][2], e['rect'][3])
                u = (t - e['t']) / 1.6
                alfa = min(1.0, u * 6) * (1 - suave((u - 0.6) / 0.4) if u > 0.6 else 1)
                capa = cuadro.copy()
                cv2.rectangle(capa, (int(ax), int(ay)), (int(bx), int(by)), ACENTO[::-1], 4, cv2.LINE_AA)
                cv2.addWeighted(capa, alfa, cuadro, 1 - alfa, 0, cuadro)

        for to, ox, oy in ondas:
            if to <= t < to + DURACION_ONDA:
                u = (t - to) / DURACION_ONDA
                cx, cy = a_salida(ox, oy)
                r = 12 + 44 * suave(u)
                x_a, y_a = int(max(cx - 64, 0)), int(max(cy - 64, 0))
                x_b, y_b = int(min(cx + 64, ANCHO)), int(min(cy + 64, ALTO))
                if x_a < x_b and y_a < y_b:
                    roi = cuadro[y_a:y_b, x_a:x_b]
                    capa = roi.copy()
                    cv2.circle(capa, (int(cx - x_a), int(cy - y_a)), int(r), ACENTO[::-1], 6, cv2.LINE_AA)
                    cv2.circle(capa, (int(cx - x_a), int(cy - y_a)), int(r * 0.4), ACENTO[::-1], -1, cv2.LINE_AA)
                    alfa = 0.95 * (1 - u * u)
                    cv2.addWeighted(capa, alfa, roi, 1 - alfa, 0, roi)

        pos = puntero_en(t, anclas)
        if pos:
            px, py = a_salida(pos[0], pos[1])
            flecha.mezclar(cuadro, px - hx, py - hy, pos[2])

        titulo.mezclar(cuadro)
        for d0, d1, capa in subtitulos:
            if d0 <= t < d1:
                capa.mezclar(cuadro)
        salida.write(cuadro.tobytes())


def escribir_fijo(salida, img, segundos, desde_negro=0.0, a_negro=0.0):
    n = int(round(segundos * FPS))
    for k in range(n):
        t = k / FPS
        f = 1.0
        if desde_negro and t < desde_negro:
            f = t / desde_negro
        if a_negro and t > segundos - a_negro:
            f = min(f, (segundos - t) / a_negro)
        salida.write((img if f >= 1 else (img * f).astype(np.uint8)).tobytes())
    return n


def codificador_salida(rapido):
    if rapido:
        r = subprocess.run([vc.FFMPEG, '-v', 'quiet', '-f', 'lavfi', '-i', 'color=s=640x360', '-frames:v', '1',
                            '-c:v', 'h264_qsv', '-f', 'null', '-'])
        if r.returncode == 0:
            return ['-c:v', 'h264_qsv', '-preset', 'medium', '-global_quality', '22']
    return ['-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20']


def validar_guion(guion):
    pasos = guion.get('pasos') if isinstance(guion, dict) else None
    if not isinstance(pasos, list) or not pasos:
        raise SystemExit('El guion no tiene "pasos" (lista con al menos un paso).')
    for n, p in enumerate(pasos, 1):
        if not isinstance(p, dict):
            raise SystemExit(f'Guion, paso {n}: debe ser un objeto.')
        try:
            desde, hasta = float(p['desde']), float(p['hasta'])
        except (KeyError, TypeError, ValueError):
            raise SystemExit(f'Guion, paso {n}: faltan "desde" y "hasta" (segundos de la toma).')
        if hasta <= desde or desde < 0:
            raise SystemExit(f'Guion, paso {n}: "hasta" ({hasta}) debe ser mayor que "desde" ({desde}).')
        if not str(p.get('texto') or '').strip():
            raise SystemExit(f'Guion, paso {n}: falta el "texto" de la locución.')
        z = p.get('zoom')
        if z is not None and not (isinstance(z, list) and len(z) == 4 and z[2] > z[0] and z[3] > z[1]):
            raise SystemExit(f'Guion, paso {n}: "zoom" debe ser [x0, y0, x1, y1] con x1 > x0 e y1 > y0, o null.')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--toma', required=True, help='video de la toma (.mkv)')
    ap.add_argument('--eventos', help='eventos.json de linea-tiempo.py (opcional)')
    ap.add_argument('--guion', required=True)
    ap.add_argument('--salida', required=True)
    ap.add_argument('--voz', default='es-PE-CamilaNeural', help='es-PE-CamilaNeural o es-PE-AlexNeural')
    ap.add_argument('--velocidad', default='+0%', help='velocidad de la voz, p. ej. +5%%')
    ap.add_argument('--empresa', help='nombre en portada (por defecto, el de Web.config)')
    ap.add_argument('--rapido', action='store_true', help='codificar con h264_qsv si está disponible')
    a = ap.parse_args()

    toma_json = os.path.splitext(a.toma)[0] + '.json'
    toma = vc.leer_json(toma_json) if os.path.exists(toma_json) else {}
    guion = vc.leer_json(a.guion)
    validar_guion(guion)
    datos_ev = vc.leer_json(a.eventos) if a.eventos else {'eventos': []}
    eventos = datos_ev.get('eventos', [])
    conv = dict(datos_ev.get('conversion') or {'viewportX': 0, 'viewportY': 0, 'escala': 1.0})
    ox, oy = vc.origen(toma)
    conv['origen'] = (ox, oy)
    empresa = a.empresa or vc.nombre_empresa()
    info = vc.info_video(a.toma)
    fps = info['fps']
    print(f'Toma: {a.toma} ({info["ancho"]}x{info["alto"]}, {info["duracion"]:.1f} s, {fps:g} fps)')

    v = toma.get('ventanaEdge')
    if v and not v.get('minimizada'):
        rect_base = vc.a_16_9([v['x'] - ox, v['y'] - oy, v['x'] - ox + v['ancho'], v['y'] - oy + v['alto']],
                              info['ancho'], info['alto'])
    else:
        rect_base = vc.a_16_9([0, 0, info['ancho'], info['alto']], info['ancho'], info['alto'])

    carpeta = os.path.splitext(os.path.abspath(a.salida))[0] + '.tmp'
    os.makedirs(carpeta, exist_ok=True)
    dif = vc.diferencias(a.toma)

    print('Locuciones y tramos:')
    pasos, audio = [], [np.zeros(int(3.0 * TASA_AUDIO), np.int16)]
    inicio_paso = 3.0  # tras la portada
    for n, paso in enumerate(guion['pasos'], 1):
        voz = generar_voz(paso['texto'], carpeta, a.voz, a.velocidad)
        p = preparar_paso(n, paso, eventos, dif, fps, len(dif), voz, conv, guion, rect_base, info)
        n_muestras = int(round(len(p['indices']) / FPS * TASA_AUDIO))
        pista = np.zeros(n_muestras, np.int16)
        pista[:min(len(voz), n_muestras)] = voz[:n_muestras]
        audio.append(pista)
        p['titulo'] = paso.get('titulo') or f'Paso {n}'
        p['texto'] = paso['texto']
        for m in p['marcas']:
            print(f"      {m['tipo']} en {inicio_paso + m['tOut']:.2f} s del video")
        inicio_paso += len(p['indices']) / FPS
        pasos.append(p)
    audio.append(np.zeros(int(2.0 * TASA_AUDIO), np.int16))
    wav = os.path.join(carpeta, 'audio.wav')
    escribir_wav(wav, np.concatenate(audio))

    flecha_img = imagen_puntero(28)
    flecha_img.save(os.path.join(carpeta, 'puntero.png'))
    flecha = Capa(flecha_img, 0, 0)

    os.makedirs(os.path.dirname(os.path.abspath(a.salida)), exist_ok=True)
    cmd = [vc.FFMPEG, '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'bgr24', '-s', f'{ANCHO}x{ALTO}',
           '-r', str(FPS), '-i', '-', '-i', wav] + codificador_salida(a.rapido) + \
          ['-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '128k', '-ar', '48000', '-ac', '2',
           '-shortest', '-movflags', '+faststart', a.salida]
    enc = subprocess.Popen(cmd, stdin=subprocess.PIPE, bufsize=ANCHO * ALTO * 3 * 4)
    lector = Lector(a.toma, info['ancho'], info['alto'], fps)
    try:
        print('Renderizando...')
        portada = imagen_portada(guion.get('titulo', 'Tutorial SGV'), guion.get('subtitulo', ''), empresa)
        escribir_fijo(enc.stdin, portada, 3.0, desde_negro=0.4)
        for n, p in enumerate(pasos, 1):
            titulo = capa_titulo(p['titulo'], empresa)
            subs = capas_subtitulos(p['texto'], p['dur_voz'])
            renderizar_paso(p, lector, enc.stdin, titulo, subs, flecha)
            print(f'  paso {n} listo ({p["duracion"]:.1f} s)')
        cierre = imagen_portada(guion.get('cierre', 'Gracias por su atención'), guion.get('titulo', ''), empresa)
        escribir_fijo(enc.stdin, cierre, 2.0, a_negro=0.5)
    finally:
        lector.cerrar()
        enc.stdin.close()
        enc.wait()
    if enc.returncode != 0:
        raise SystemExit('ffmpeg falló al codificar el video.')
    dur = vc.info_video(a.salida)['duracion']
    print(f'Video: {a.salida} ({dur:.1f} s)')


if __name__ == '__main__':
    main()
