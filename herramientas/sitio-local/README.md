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
