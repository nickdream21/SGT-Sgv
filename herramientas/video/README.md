# Videos del SGV

Graban la pantalla del sistema con Edge (`puppeteer-core`), ponen la locución con voz neural de Perú
(`msedge-tts`: envía el texto del guion al servicio de voz de Microsoft) y arman el MP4 con `ffmpeg-static`.

```powershell
cd herramientas\video
npm install                       # una vez por máquina
node grabar-liquidacion.mjs       # capacitación del conductor, vertical 720x1280, voz es-PE-CamilaNeural
node grabar-beneficios.mjs http://localhost:50291 <ruta del HTML del artículo>   # presentación, 1920x1080, es-PE-AlexNeural
```

Los MP4 quedan en `docs/DOCUMENTACION/capacitacion/` (gitignored).

- **Requisitos:** sitio local levantado (`herramientas/sitio-local/iniciar-sitio.ps1`) y
  `privado/CREDENCIALES_USUARIOS.txt` (las claves se leen del archivo; no se imprimen).
- **Liquidación:** el usuario conductor de pruebas debe estar enlazado a un conductor con un viaje abierto con GPS.
  El script envía y firma una liquidación real en la BD de pruebas: después hay que retirarla
  (`sp_DC_RetirarLiquidacion`) y restaurar el viaje. Los nombres y DNI reales se ocultan en pantalla
  pasándolos en `SGV_OCULTAR="NOMBRE APELLIDOS|DNI|..."` (no se guardan en el código).
- **Beneficios:** las cifras de las escenas son las medidas al 03/11/2026; si cambian, actualizar el guion.
- `voz.mjs "texto" salida.mp3 [voz]` genera solo un audio, para probar voces.
