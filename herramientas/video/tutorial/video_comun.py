"""Utilidades compartidas por linea-tiempo.py y armar.py (tutoriales grabados del SGV)."""
import json
import os
import shutil
import subprocess
from datetime import datetime

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, '..', '..', '..'))
DIR_CAPACITACION = os.path.join(RAIZ, 'docs', 'DOCUMENTACION', 'capacitacion')

# Detección de "cambio visible": cuadros reducidos a gris de ANCHO_DIF x ALTO_DIF (1/6 de 1080p); un píxel
# cambia si su diferencia supera UMBRAL_PIXEL. El cursor de texto que parpadea mueve unos 3 píxeles reducidos:
# para buscar el efecto de una acción basta MIN_EFECTO (una letra escrita ya cuenta); para decidir qué es una
# espera se pide MIN_ESPERA, así el parpadeo del cursor no impide acortarla.
ANCHO_DIF, ALTO_DIF = 320, 180
UMBRAL_PIXEL = 20
MIN_EFECTO = 4
MIN_ESPERA = 8


def _binario(nombre):
    ruta = shutil.which(nombre)
    if ruta:
        return ruta
    estatico = os.path.join(AQUI, '..', 'node_modules', 'ffmpeg-static', nombre + '.exe')
    if nombre == 'ffmpeg' and os.path.exists(estatico):
        return estatico
    raise SystemExit(f'No se encontró {nombre} en el PATH.')


FFMPEG = _binario('ffmpeg')
FFPROBE = _binario('ffprobe')


def leer_json(ruta):
    with open(ruta, encoding='utf-8-sig') as f:
        return json.load(f)


def escribir_json(ruta, datos):
    os.makedirs(os.path.dirname(os.path.abspath(ruta)), exist_ok=True)
    with open(ruta, 'w', encoding='utf-8') as f:
        json.dump(datos, f, ensure_ascii=False, indent=2)


def fecha(texto):
    """ISO 8601 (con Z u offset) -> datetime con zona."""
    return datetime.fromisoformat(texto.replace('Z', '+00:00'))


def info_video(ruta):
    r = subprocess.run([FFPROBE, '-v', 'error', '-select_streams', 'v:0',
                        '-show_entries', 'stream=width,height,r_frame_rate:format=duration',
                        '-of', 'json', ruta], capture_output=True, text=True, check=True)
    d = json.loads(r.stdout)
    s = d['streams'][0]
    num, den = s['r_frame_rate'].split('/')
    fps = float(num) / float(den)
    dur = float(d['format']['duration'])
    return {'ancho': int(s['width']), 'alto': int(s['height']), 'fps': fps, 'duracion': dur,
            'cuadros': int(round(dur * fps))}


def origen(toma):
    """Esquina en pantalla del área grabada (con grabar.ps1 -SoloEdge no es 0, 0)."""
    r = (toma or {}).get('recorte') or {}
    return float(r.get('x', 0)), float(r.get('y', 0))


def video_de_toma(toma, ruta_json):
    v = toma.get('video')
    if v and os.path.exists(v):
        return v
    alterno = os.path.splitext(ruta_json)[0] + '.mkv'
    return alterno if os.path.exists(alterno) else None


def diferencias(video):
    """Número de píxeles que cambian respecto del cuadro anterior, por cuadro (cache en <video>.dif.npy)."""
    cache = video + f'.dif{ANCHO_DIF}.npy'
    if os.path.exists(cache) and os.path.getmtime(cache) >= os.path.getmtime(video):
        return np.load(cache)
    tam = ANCHO_DIF * ALTO_DIF
    p = subprocess.Popen([FFMPEG, '-v', 'error', '-i', video, '-vf',
                          f'scale={ANCHO_DIF}:{ALTO_DIF}:flags=area,format=gray', '-f', 'rawvideo', '-'],
                         stdout=subprocess.PIPE)
    salida, anterior = [], None
    while True:
        b = p.stdout.read(tam)
        if len(b) < tam:
            break
        c = np.frombuffer(b, np.uint8).astype(np.int16)
        salida.append(0 if anterior is None else int(np.count_nonzero(np.abs(c - anterior) > UMBRAL_PIXEL)))
        anterior = c
    p.wait()
    arr = np.array(salida, dtype=np.int32)
    np.save(cache, arr)
    return arr


def hay_cambio(dif, minimo=MIN_ESPERA):
    return dif >= minimo


def cuadro(video, segundo):
    """Un cuadro BGR (numpy) del video en el segundo dado."""
    import cv2
    tmp = video + '.cuadro.png'
    subprocess.run([FFMPEG, '-v', 'error', '-y', '-ss', f'{segundo:.3f}', '-i', video, '-frames:v', '1', tmp],
                   check=True)
    img = cv2.imread(tmp)
    os.remove(tmp)
    return img


def a_16_9(rect, ancho_video, alto_video):
    """Agranda el rectángulo [x0, y0, x1, y1] hasta 16:9 (centrado) y lo encaja dentro del video."""
    x0, y0, x1, y1 = [float(v) for v in rect]
    w, h = max(x1 - x0, 16.0), max(y1 - y0, 9.0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    if w / h > 16 / 9:
        h = w * 9 / 16
    else:
        w = h * 16 / 9
    if w > ancho_video:
        w, h = ancho_video, ancho_video * 9 / 16
    if h > alto_video:
        h, w = alto_video, alto_video * 16 / 9
    x0 = min(max(cx - w / 2, 0), ancho_video - w)
    y0 = min(max(cy - h / 2, 0), alto_video - h)
    return [x0, y0, x0 + w, y0 + h]


def nombre_empresa():
    """Nombre comercial desde appSettings (Empresa.RazonSocial) sin la forma societaria, en tipo título."""
    import re
    import xml.etree.ElementTree as ET
    try:
        arbol = ET.parse(os.path.join(RAIZ, 'WebSGV', 'Web.config'))
        for add in arbol.getroot().iter('add'):
            if add.get('key') in ('Empresa.NombreComercial', 'Empresa.RazonSocial'):
                n = re.sub(r'\s+(E\.?I\.?R\.?L\.?|S\.?A\.?C\.?|S\.?A\.?|S\.?R\.?L\.?)\s*$', '', add.get('value', ''),
                           flags=re.I).strip()
                if n:
                    return ' '.join(p.capitalize() for p in n.lower().split())
    except (OSError, ET.ParseError):
        pass
    return 'Servicios Generales Viviana'
