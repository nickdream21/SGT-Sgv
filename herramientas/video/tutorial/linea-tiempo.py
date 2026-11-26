"""Línea de tiempo de una toma: qué hizo Claude en el navegador y en qué segundo del video.

Lee la transcripción de Claude Code (JSONL) y saca los eventos de Claude in Chrome (clics, escritura,
teclas, desplazamiento, navegación, formularios, subida de archivos y pausas de lectura).

  python linea-tiempo.py                                   # lista los últimos 10 eventos del JSONL más reciente
  python linea-tiempo.py --toma <toma.json> --salida eventos.json [--transcripcion x.jsonl] [--sin-afinar]
"""
import argparse
import glob
import json
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import video_comun as vc  # noqa: E402

# Claude Code guarda las transcripciones en ~/.claude/projects/<ruta del repo con : \ / cambiados por ->
DIR_TRANSCRIPCIONES = os.path.join(os.path.expanduser('~'), '.claude', 'projects', re.sub(r'[:\\/]', '-', vc.RAIZ))
PREFIJO = 'mcp__claude-in-chrome__'
CONTRASENA = re.compile(r'contrase|password|passw|clave|pwd|secret|token', re.I)
CLICS = {'left_click': 'clic', 'right_click': 'clic', 'double_click': 'doble_clic', 'triple_click': 'triple_clic'}
PAUSAS = {'find', 'read_page', 'get_page_text'}


def transcripcion_por_defecto():
    archivos = sorted(glob.glob(os.path.join(DIR_TRANSCRIPCIONES, '*.jsonl')), key=os.path.getmtime, reverse=True)
    if not archivos:
        raise SystemExit(f'No hay transcripciones en {DIR_TRANSCRIPCIONES}')
    return archivos[0]


def archivos_de(transcripcion):
    """La sesión y los subagentes que lanzó (sus JSONL van en <sesion>/subagents/)."""
    base = os.path.splitext(transcripcion)[0]
    return [transcripcion] + sorted(glob.glob(os.path.join(base, 'subagents', '*.jsonl')))


def texto_de_resultado(contenido):
    if isinstance(contenido, str):
        return contenido
    partes = []
    for b in contenido or []:
        if isinstance(b, dict) and b.get('type') == 'text':
            partes.append(b.get('text', ''))
    return '\n'.join(partes)


def leer_llamadas(archivos):
    """Lista de llamadas a Claude in Chrome: {ts, fin, nombre, input, resultado, archivo}."""
    usos, resultados = {}, {}
    for archivo in archivos:
        with open(archivo, encoding='utf-8') as f:
            for linea in f:
                if 'claude-in-chrome' not in linea and '"tool_result"' not in linea:
                    continue
                try:
                    d = json.loads(linea)
                except json.JSONDecodeError:
                    continue
                contenido = (d.get('message') or {}).get('content')
                if not isinstance(contenido, list) or 'timestamp' not in d:
                    continue
                for b in contenido:
                    if not isinstance(b, dict):
                        continue
                    if b.get('type') == 'tool_use' and str(b.get('name', '')).startswith(PREFIJO):
                        usos[b['id']] = {'ts': vc.fecha(d['timestamp']), 'nombre': b['name'][len(PREFIJO):],
                                         'input': b.get('input') or {}, 'archivo': os.path.basename(archivo)}
                    elif b.get('type') == 'tool_result' and b.get('tool_use_id'):
                        resultados[b['tool_use_id']] = (vc.fecha(d['timestamp']), texto_de_resultado(b.get('content')))
    llamadas = []
    for i, u in usos.items():
        fin, res = resultados.get(i, (u['ts'], ''))
        u.update(fin=fin, resultado=res)
        llamadas.append(u)
    llamadas.sort(key=lambda u: u['ts'])
    return llamadas


def acciones(llamada):
    """Despliega browser_batch en sus acciones, repartidas en el tiempo entre el pedido y la respuesta."""
    if llamada['nombre'] != 'browser_batch':
        return [(llamada['ts'], llamada['nombre'], llamada['input'] if isinstance(llamada['input'], dict) else {})]
    lista = llamada['input'].get('actions') if isinstance(llamada['input'], dict) else None
    if isinstance(lista, str):
        try:
            lista = json.loads(lista)
        except json.JSONDecodeError:
            lista = []
    lista = [a for a in (lista or []) if isinstance(a, dict)] if isinstance(lista, list) else []
    dur = (llamada['fin'] - llamada['ts']).total_seconds()
    salida = []
    for k, a in enumerate(lista):
        nombre = str(a.get('name') or a.get('tool') or '').replace(PREFIJO, '')
        inp = a.get('input') or a.get('args')
        if not isinstance(inp, dict):
            # Formato plano: {"tool": "navigate", "url": ...}
            inp = {c: v for c, v in a.items() if c not in ('name', 'tool', 'input', 'args')}
        t = llamada['ts'].timestamp() + (dur * k / max(len(lista), 1))
        salida.append((t, nombre, inp))
    return salida


def es_sensible(*textos):
    return any(CONTRASENA.search(t or '') for t in textos)


def eventos_de(llamadas):
    """Eventos crudos (hora UTC en segundos epoch) y el marco de coordenadas que informó cada captura.

    Lo escrito (type, form_input) nunca se guarda ni se imprime: solo su largo y si el campo parece de contraseña.
    """
    eventos, refs, marco = [], {}, None
    ultimo_clic = ''  # descripción del ref y resumen del último clic: indican en qué campo se escribe
    sensibles = 0
    for ll in llamadas:
        res = ll['resultado'] or ''
        # find: "- ref_14: textbox ..."; read_page: "ref_18 textbox ..."
        for m in re.finditer(r'(ref_\d+):?[ \t]+([^\n]+)', res):
            refs[m.group(1)] = m.group(2).strip()
        for t, nombre, inp in acciones(ll):
            t = t if isinstance(t, float) else t.timestamp()
            accion = str(inp.get('action', '') or '')
            ref = inp.get('ref')
            desc = refs.get(ref, '') if ref else ''
            resumen = str(inp.get('action_summary', '') or '')
            ev = None
            if nombre == 'computer':
                coord = inp.get('coordinate')
                if accion in CLICS:
                    ev = {'tipo': CLICS[accion]}
                    ultimo_clic = desc + ' ' + resumen
                elif accion == 'type':
                    sensible = es_sensible(resumen, desc, ultimo_clic)
                    ev = {'tipo': 'escribir', 'largo': len(str(inp.get('text', '') or ''))}
                elif accion == 'key':
                    ev = {'tipo': 'tecla', 'tecla': str(inp.get('text', '') or '')[:30]}
                elif accion in ('scroll', 'scroll_to'):
                    ev = {'tipo': 'desplazar', 'direccion': inp.get('scroll_direction')}
                if ev is not None and isinstance(coord, (list, tuple)) and len(coord) == 2:
                    ev['mx'], ev['my'] = coord[0], coord[1]
            elif nombre == 'navigate':
                ev = {'tipo': 'navegar', 'url': str(inp.get('url', '') or '')}
            elif nombre == 'form_input':
                sensible = es_sensible(resumen, desc)
                ev = {'tipo': 'formulario', 'largo': len(str(inp.get('value', '') or ''))}
            elif nombre == 'file_upload':
                ev = {'tipo': 'subir', 'archivos': [os.path.basename(str(p)) for p in inp.get('paths') or []]}
            elif nombre in PAUSAS:
                ev = {'tipo': 'pausa', 'herramienta': nombre}
            if nombre == 'computer' and accion in ('screenshot', 'zoom'):
                m = re.search(r'coordinate frame:\s*(\d+)x(\d+)', res)
                if m:
                    marco = (int(m.group(1)), int(m.group(2)))
            if ev is None:
                continue
            ev['epoch'] = t
            if ref:
                ev['ref'] = ref
            if ev['tipo'] in ('escribir', 'formulario'):
                # Ni resumen ni descripción: podrían repetir lo escrito.
                if sensible:
                    ev['contrasena'] = True
                    sensibles += 1
            else:
                if desc:
                    ev['descripcion'] = desc
                if resumen:
                    ev['resumen'] = resumen
            ev['marco'] = marco
            eventos.append(ev)
    return eventos, sensibles

def estimar_conversion(toma, video, marco):
    """Sin calibración: origen = esquina del contenido bajo la barra de Edge; escala = ancho viewport / ancho marco."""
    import cv2
    v = toma.get('ventanaEdge') or {'x': 0, 'y': 0, 'ancho': toma['pantalla']['ancho'],
                                     'alto': toma['pantalla']['alto']}
    x0, y0, w = int(v['x']), int(v['y']), int(v['ancho'])
    ox, oy = (int(c) for c in vc.origen(toma))
    vy = y0 + 85
    if video:
        img = vc.cuadro(video, 0.5)
        gris = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY).astype(np.int16)
        franja = gris[:, max(x0 - ox + 10, 0): min(x0 - ox + w - 10, gris.shape[1])]
        desde, hasta = max(y0 - oy + 50, 0), min(y0 - oy + 220, gris.shape[0] - 2)
        # Borde inferior de la barra del navegador: la primera fila (de arriba abajo) en la que cambia el color
        # en casi todo el ancho de la ventana; el texto, los iconos y la caja de la dirección no llegan a los bordes.
        for y in range(desde, hasta):
            if np.mean(np.abs(franja[y + 1] - franja[y]) > 6) > 0.9:
                vy = oy + y + 1
                break
    ancho_marco = marco[0] if marco else w
    return {'viewportX': x0, 'viewportY': vy, 'escala': round(w / ancho_marco, 5), 'marcoAncho': ancho_marco,
            'marcoAlto': marco[1] if marco else None, 'fuente': 'estimada'}


def afinar(eventos, video, fps):
    """Lleva cada evento al primer cuadro con cambio visible dentro de los 3 s siguientes ("efecto")."""
    dif = vc.diferencias(video)
    cambia = vc.hay_cambio(dif, vc.MIN_EFECTO)
    for ev in eventos:
        f0 = max(int(ev['t'] * fps), 1)
        f1 = min(f0 + int(3 * fps), len(cambia))
        idx = np.flatnonzero(cambia[f0:f1])
        ev['tEfecto'] = round((f0 + idx[0]) / fps, 3) if len(idx) else ev['t']


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--transcripcion', help='JSONL de Claude Code (por defecto, el más reciente del proyecto)')
    ap.add_argument('--toma', help='JSON de la toma (grabar.ps1)')
    ap.add_argument('--salida', help='eventos.json a escribir')
    ap.add_argument('--listar', type=int, default=10, help='sin --toma: cuántos eventos mostrar')
    ap.add_argument('--sin-afinar', action='store_true', help='no buscar el cuadro del efecto en el video')
    a = ap.parse_args()

    transcripcion = a.transcripcion or transcripcion_por_defecto()
    llamadas = leer_llamadas(archivos_de(transcripcion))
    eventos, sensibles = eventos_de(llamadas)

    if not a.toma:
        print(f'Transcripción: {transcripcion} ({len(llamadas)} llamadas a Claude in Chrome, '
              f'{len(eventos)} eventos, {sensibles} escrituras en campos de contraseña; lo escrito nunca se guarda)')
        from datetime import datetime
        for ev in eventos[-a.listar:]:
            hora = datetime.fromtimestamp(ev['epoch']).strftime('%H:%M:%S.%f')[:-3]
            detalle = ev.get('url') or ev.get('resumen') or ev.get('tecla') or ev.get('herramienta') or ''
            if 'largo' in ev:
                detalle = f"({ev['largo']} caracteres{', contraseña' if ev.get('contrasena') else ''})"
            pos = f" ({ev['mx']},{ev['my']})" if 'mx' in ev else (f" {ev['ref']}" if 'ref' in ev else '')
            print(f'  {hora}  {ev["tipo"]:<12}{pos}  {str(detalle)[:70]}')
        return

    toma = vc.leer_json(a.toma)
    if not toma.get('inicioUtc'):
        raise SystemExit('La toma no tiene inicioUtc (¿se detuvo bien?).')
    inicio = vc.fecha(toma['inicioUtc']).timestamp()
    duracion = float(toma.get('duracion') or 1e9)
    video = vc.video_de_toma(toma, a.toma)
    fps = vc.info_video(video)['fps'] if video else float(toma.get('fps', 30))

    dentro = []
    for ev in eventos:
        t = ev.pop('epoch') - inicio
        if 0 <= t <= duracion:
            ev['t'] = round(t, 3)
            dentro.append(ev)

    marcos = [ev['marco'] for ev in dentro if ev.get('marco')]
    marco = marcos[-1] if marcos else None
    cal = toma.get('calibracion')
    if cal:
        conv = {'viewportX': cal['viewportX'], 'viewportY': cal['viewportY'], 'escala': cal['escala'],
                'marcoAncho': cal.get('marcoAncho'), 'marcoAlto': cal.get('marcoAlto'), 'fuente': 'calibracion'}
    else:
        conv = estimar_conversion(toma, video, marco)
    ox, oy = vc.origen(toma)
    for ev in dentro:
        ev.pop('marco', None)
        if 'mx' in ev:  # x, y: píxeles del video (pantalla menos el recorte de -SoloEdge)
            ev['x'] = round(conv['viewportX'] + ev['mx'] * conv['escala'] - ox, 1)
            ev['y'] = round(conv['viewportY'] + ev['my'] * conv['escala'] - oy, 1)

    if video and not a.sin_afinar:
        afinar(dentro, video, fps)

    salida = a.salida or os.path.splitext(a.toma)[0] + '.eventos.json'
    vc.escribir_json(salida, {'toma': os.path.abspath(a.toma), 'video': video, 'transcripcion': transcripcion,
                              'fps': fps, 'origen': [ox, oy], 'conversion': conv, 'escriturasContrasena': sensibles, 'eventos': dentro})
    print(f'{len(dentro)} eventos dentro de la toma ({sensibles} escrituras en campos de contraseña; '
          f'lo escrito nunca se guarda); '
          f'conversión {conv["fuente"]}: origen ({conv["viewportX"]}, {conv["viewportY"]}), escala {conv["escala"]}')
    print(f'Escrito: {salida}')


if __name__ == '__main__':
    main()
