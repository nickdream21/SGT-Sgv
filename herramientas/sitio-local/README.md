# Sitio local: levantar WebSGV y probarlo

Para probar cambios en la app real (no solo con tests), en cualquiera de las dos máquinas.

1. Compilar: `msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal`
2. Levantar el sitio: `.\herramientas\sitio-local\iniciar-sitio.ps1`
   (desde Claude Code, en segundo plano). Queda en `https://localhost:44390` contra la BD de
   **pruebas** (`WebSGV/connectionStrings.config`).
3. Probar:
   - **En el navegador (Claude in Chrome).** Claude puede manejar Chrome si la extensión está
     conectada (ver abajo). Usuarios de prueba por rol: `privado/CREDENCIALES_USUARIOS.txt`.
   - **Sin navegador:** `probar-importacion.ps1 -Archivos <zip/pdf>, <docx>` hace lo mismo que
     "Leer documentos" en RegistroDespacho y resume la vista previa. Solo lee: no crea el lote.
   - **Caracterización de RegistroDespacho (E6-21):** `caracterizar-registro-despacho.ps1` corre 5 juegos
     fijos de `docs/DOCUMENTACION/` (dos de ZIP + Word, solo PDF, solo Word y descarga
     por pedido con su Word) y guarda lo que el usuario ve, normalizado, en `privado/caracterizacion/<juego>.txt`
     (fuera de git). Con `-Comparar` vuelve a correr y muestra las diferencias (exit 0 sin diferencias, 1 con
     ellas; la corrida nueva queda en `<juego>.nuevo.txt`; por consola los números de 8 dígitos salen como
     `########`); `-Juegos solo-word, descarga-word` corre algunos (con `powershell -File ...` escríbalos
     juntos y separados por comas: `-Juegos solo-word,descarga-word`). Solo envía "Leer documentos", "Ver
     tractos por descargar" y "Leer programación" (lista blanca: cualquier otro POST aborta); la contraseña
     va a curl por la entrada estándar, no en la línea de órdenes. Un juego que falla (falta un documento,
     el pedido ya no está pendiente, error) no se guarda y sale con 1; al comparar cuenta como diferencia.
     Caracteriza el HTML que devuelve el servidor, no lo que el JavaScript de la página cambia después en el
     navegador (select2, plegados, avisos). Uso al partir la página: guardar antes de cambiarla y
     `-Comparar` al terminar. Los pedidos de descarga salen de la BD de pruebas: si alguien los descarga,
     el juego falla y hay que elegir otro pedido.
   - **Recalcular viajes del seguimiento:** `actualizar-gps.ps1 -Ids 7250, 7273` pulsa "Actualizar GPS"
     en el detalle de cada viaje (escribe en la BD de pruebas, como el botón) y muestra el mensaje.

## Conectar Chrome con Claude Code (una vez por máquina)

Si Claude dice que no puede usar el navegador, es que la extensión no está conectada:

1. Instalar la extensión **Claude in Chrome** desde https://claude.ai/chrome, en el navegador que
   se usa a diario. Sirve en **Google Chrome o Microsoft Edge** (en la PC de la oficina está en Edge
   y funciona; la tienda de Chrome abierta desde Edge dice "Claude está instalado en Microsoft Edge").
2. Iniciar sesión en la extensión con **la misma cuenta de claude.ai** que usa Claude Code.
3. Cerrar Chrome por completo y volver a abrirlo (la primera vez lo necesita).
4. En Claude Code, escribir `/chrome` y elegir activar la conexión (o iniciar con
   `claude --chrome`). Al pedir algo del navegador, Claude abre su propia pestaña.
5. En la extensión, dar permiso al sitio `localhost` cuando lo pida.

Para saber si está conectado: pedirle a Claude "lista los navegadores conectados".

## Copia contra una BD de ensayo (capturas de la ayuda)

`iniciar-sitio-ensayo.ps1 -BaseDeDatos sgvEnsayo_*` copia la salida compilada a `%LOCALAPPDATA%\SGV-sitio-ensayo\`
con una conexión a esa BD local (autenticación de Windows), sin correo, ApisPeru ni GPS, y la levanta en
`http://localhost:50391`. Es la que usa `herramientas/manual/capturar.mjs` (ver su README): nunca se captura
contra la BD de pruebas ni el sitio de 44390/50291.

## Ver las pantallas sin ventana (capturas)

Si la captura de la extensión falla (ventana minimizada), `capturar.js` abre Edge sin ventana, inicia
sesión con `admin.transporte` (lee la clave de `privado/CREDENCIALES_USUARIOS.txt`, no la imprime) y
guarda la pantalla completa. También avisa de los errores de JavaScript de la página.

```bash
export MSYS_NO_PATHCONV=1   # solo en Git Bash: evita que convierta /Views/... en una ruta de Windows
node herramientas/sitio-local/capturar.js lista.png /Views/ListaDespachos.aspx 1366 2000
node herramientas/sitio-local/capturar.js a.png /Views/A.aspx -- b.png /Views/B.aspx 390   # varias; 390 = celular
```

El quinto argumento es JavaScript que se ejecuta antes de capturar (un clic, escribir en un filtro).
