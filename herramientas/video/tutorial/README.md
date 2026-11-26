# Tutoriales grabados mientras Claude opera Edge

Graba la pantalla mientras Claude usa el SGV en Edge (extensión Claude in Chrome) y luego arma un video
tutorial con voz de Perú, puntero dibujado (el puntero real no se mueve), onda en cada clic, zoom y subtítulos.

Requisitos: ffmpeg en el PATH (con `ddagrab`), Python 3.12 con `opencv-python`, `pillow` y `numpy`, y
`npm install` hecho en `herramientas/video` (para `voz.mjs`). Todo lo que se genera va en
`docs/DOCUMENTACION/capacitacion/` (gitignored).

**Antes de grabar:** la toma captura lo que se ve en pantalla. Usa `-SoloEdge`, cierra o minimiza
WhatsApp, el correo y cualquier otra ventana personal, y graba solo contra el sitio local o la BD de pruebas
(sin datos personales reales en pantalla).

## 1. Grabar

```powershell
cd herramientas\video\tutorial
.\grabar.ps1 -Iniciar -Nombre registrar-despacho -SoloEdge   # ffmpeg en segundo plano (h264_qsv/nvenc/amf o libx264)
.\grabar.ps1 -Calibracion '<json>'                            # ver "Calibrar" (una vez por toma)
# ... Claude opera Edge ...
.\grabar.ps1 -Detener                                         # 'q' a ffmpeg: el MKV queda bien cerrado
.\grabar.ps1 -Estado
```

Queda `docs/DOCUMENTACION/capacitacion/tomas/<aaaaMMdd_HHmmss>_<nombre>.mkv` (30 fps, sin audio, sin el puntero real) y al lado el `.json` con
la hora del primer cuadro (`inicioLocal`, `inicioUtc`, con milisegundos), `finLocal`/`finUtc`, la ventana de
Edge (`ventanaEdge`, píxeles de pantalla), el `recorte` si se usó `-SoloEdge`, y la `calibracion`.

## 2. Calibrar (Claude, al empezar cada toma)

Las coordenadas de la herramienta `computer` están en un "marco" (cada captura lo dice: `coordinate frame:
1568x750`) que no coincide con los píxeles de la pantalla. Con Edge ya en la página y la toma iniciada:

1. Ejecuta en la página con `javascript_tool`:
   ```js
   JSON.stringify({screenX, screenY, outerWidth, outerHeight, innerWidth, innerHeight, devicePixelRatio})
   ```
2. Toma una captura (`computer` → `screenshot`) y anota el `coordinate frame: WxH`.
3. Ejecuta `.\grabar.ps1 -Calibracion '<el json de 1 con "marcoAncho":W,"marcoAlto":H añadidos>'`.

El script calcula el origen del área de la página en la pantalla (bordes y barra de Edge a partir de
`outer - inner`, por `devicePixelRatio`) y la escala marco → pantalla (`innerWidth × dpr / marcoAncho`).
No cambies el tamaño de la ventana ni el zoom de Edge durante la toma. Sin calibración, `linea-tiempo.py`
la estima (borde inferior de la barra de Edge en el primer cuadro y el marco de la última captura).

Para que el puntero sepa dónde ir, en las tomas para tutorial haz los clics **por coordenada**
(`left_click` con `coordinate`); los clics por `ref` quedan en la línea de tiempo pero sin posición.

## 3. Línea de tiempo

```powershell
python linea-tiempo.py                                  # prueba: últimos 10 eventos del JSONL más reciente
$cap = '..\..\..\docs\DOCUMENTACION\capacitacion'
python linea-tiempo.py --toma $cap\tomas\<toma>.json [--salida $cap\<tema>\eventos.json] [--transcripcion x.jsonl]
```

Lee la transcripción de Claude Code (por defecto la más reciente del proyecto, con sus subagentes) y saca
clics, escritura, teclas, desplazamiento, navegación, `form_input`, `file_upload` y pausas (`find`,
`read_page`, `get_page_text`), con su segundo en la toma (`t`) y el primer cuadro con cambio visible en los
3 s siguientes (`tEfecto`). `x`, `y` van en píxeles del video. Nunca se guarda lo escrito (`type`,
`form_input`): solo su largo y si el campo parece de contraseña. El archivo es editable: se pueden corregir
posiciones o agregar eventos a mano.

## 4. Guion y video

`guion.json` (guárdalo junto al video, en `docs/DOCUMENTACION/capacitacion/<tema>/`):

```json
{ "titulo": "Registrar un despacho", "subtitulo": "Para el administrador de transporte",
  "cierre": "Gracias por su atención",
  "coordenadas": "video",
  "pasos": [
    { "desde": 0, "hasta": 25, "titulo": "Paso 1: Abrir Registro de despacho",
      "texto": "Desde el menú Despachos, abrimos Registro de despacho.", "zoom": null },
    { "desde": 25, "hasta": 70, "titulo": "Paso 2: Cargar los documentos",
      "texto": "Subimos el ZIP de Vitapro y la programación en Word.", "zoom": [300, 200, 1500, 900] } ] }
```

`desde`/`hasta` en segundos de la toma; `zoom` en píxeles del video (o del marco de la herramienta si
`"coordenadas": "marco"`); se agranda a 16:9.

```powershell
python armar.py --toma $cap\tomas\<toma>.mkv --eventos $cap\<tema>\eventos.json --guion $cap\<tema>\guion.json --salida $cap\<tema>\<video>.mp4 [--voz es-PE-AlexNeural] [--velocidad +5%] [--rapido]
```

Por paso: locución (`../voz.mjs`, en caché por texto), esperas de más de 1,5 s sin cambio visible acortadas a
0,4 s, y el tramo ajustado a la voz + 0,5 s (acelera hasta 2x y, si aún sobra, corta el final; si falta,
congela el último cuadro). La consola muestra en qué segundo del video cae cada evento. Portada de 3 s con el
nombre de la empresa (de `Empresa.RazonSocial` en `WebSGV/Web.config`) y cierre de 2 s. Salida 1920x1080
H.264 + AAC con `+faststart`. `--rapido` codifica con `h264_qsv` si existe. Los temporales quedan en
`<video>.tmp/`.

## Limitaciones

- Los clics por `ref` no traen coordenadas: no llevan puntero ni onda (sí cuentan para los tramos).
- En `browser_batch` la hora de cada acción se reparte entre el pedido y la respuesta; `tEfecto` lo corrige
  si la acción cambia algo en pantalla.
- La hora de inicio se calcula con el contador de cuadros de ffmpeg (error típico < 0,2 s).
- El resaltado de un campo al escribir busca sus bordes en el cuadro; si no los halla, dibuja un recuadro fijo.
- Un solo monitor (`output_idx=0`).
