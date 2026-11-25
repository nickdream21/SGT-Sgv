# Capturas de la ayuda

Las imágenes de la ayuda (`WebSGV/ContenidoAyuda/img/<tema>/NN.webp`) salen de una copia del sitio contra una
BD de **ensayo anonimizada**, nunca de la BD de pruebas (`sgvActualizada`) ni del sitio de 44390/50291.

## Pasos

```powershell
# 1. BD de ensayo desde el último .bacpac (local, autenticación de Windows), sin borrarla al terminar.
.\herramientas\respaldo\ensayar-migracion.ps1 -Carpeta C:\RespaldosSGV -Conservar -SinPruebas -NombreBd sgvEnsayo_ayuda

# 2. Anonimizarla (aborta si la BD no es sgvEnsayo_*). El SELECT final debe dar 0 en las columnas "SinAnonimizar".
.\WebSGV\Database\sql.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_ayuda -Servidor '(localdb)\MSSQLLocalDB' -Archivo herramientas\respaldo\anonimizar-ensayo.sql

# 3. Compilar y levantar la copia de ensayo en http://localhost:50391 (en segundo plano).
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
.\herramientas\sitio-local\iniciar-sitio-ensayo.ps1 -BaseDeDatos sgvEnsayo_ayuda

# 4. Solo para los temas del conductor (celular, 390 px): clave de ensayo para un conductor anonimizado. Rehúsa
#    fuera de una BD sgvEnsayo_*; la clave no se imprime, va solo al JSON indicado (fuera del repositorio).
.\herramientas\respaldo\clave-conductor-ensayo.ps1 -BaseDeDatos sgvEnsayo_ayuda -Salida $env:TEMP\sgv-conductor-ensayo.json

# 5. Capturar (todas las recetas, o una con --tema). Sin --tema vuelve a sacar TODAS las imágenes: revise con
#    git diff las que no quería cambiar y restáurelas con git checkout.
node herramientas\manual\capturar.mjs --bd sgvEnsayo_ayuda --credencial-conductor $env:TEMP\sgv-conductor-ensayo.json
node herramientas\manual\capturar.mjs --bd sgvEnsayo_ayuda --tema peajes

# 6. Cerrar el IIS Express del puerto 50391, borrar la BD de ensayo, el login que creó la importación (lo
#    imprime el paso 1), la copia del sitio y la credencial del conductor.
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "ALTER DATABASE [sgvEnsayo_ayuda] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [sgvEnsayo_ayuda]"
Remove-Item -Recurse -Force $env:LOCALAPPDATA\SGV-sitio-ensayo
Remove-Item $env:TEMP\sgv-conductor-ensayo.json
```

## Recetas

Cada tema de `temas.json` puede tener `"capturas"`: `archivo` (`01.webp`..), `url` (relativa, p. ej.
`Views/RegistroPeajes.aspx`), `ancho` (1366 oficina, 390 celular), `selector` (el bloque que se recorta) y
`clics` (selectores que se pulsan antes, p. ej. para abrir una pestaña). Solo se capturan los temas sin
`"borrador"`. Máximo 3 imágenes por tema y 150 KB cada una (lo comprueba `AyudaTests`).

Después de capturar:

1. Revise cada imagen: ningún nombre, documento, teléfono ni dato que no sea de prueba.
2. Agréguela al fragmento: `<figure><img src="01.webp" alt="Qué se ve"><figcaption>Texto</figcaption></figure>`.
3. Regístrela en `WebSGV.csproj` como `<Content Include="ContenidoAyuda\img\<tema>\01.webp" />`.

El script inicia sesión con el usuario de pruebas del grupo del tema (contraseña leída de
`privado/CREDENCIALES_USUARIOS.txt`, nunca impresa) y rehúsa trabajar si el sitio no sirve un `ENSAYO.txt`
con la BD indicada (lo escribe `iniciar-sitio-ensayo.ps1`).

Los temas sin datos que mostrar en la BD de ensayo (firma, conformidad e historial del conductor sin liquidaciones;
Mi seguridad con el aviso de que el segundo factor no está configurado en la copia de ensayo) quedan sin imagen.

# Manual completo (PDF por rol)

```powershell
node herramientas\manual\generar-manual.mjs --rol A      # A administración, C conductor, K contabilidad, Y sistema
node herramientas\manual\generar-manual.mjs --rol todos
```

Junta los temas que ve cada grupo de rol (`temas.json`, sin los temas cuyo interruptor está apagado en
`Web.config`) con la misma composición que `Views/Ayuda.aspx?manual=1` (`AyudaCatalogo.HtmlManual`): portada con
el rol, la fecha y "Borrador hasta la revisión del usuario.", índice por sección y un tema por hoja, con
`Content/sgv-base.css` y `Content/paginas/AyudaManual.css`. Las imágenes se leen del disco; no usa la BD ni el
sitio. Edge sin ventana imprime el PDF (`--print-to-pdf`), sin dependencias de npm.

Salida: `docs/DOCUMENTACION/capacitacion/Manual-SGV-<rol>.pdf` (fuera de git). Quite la nota de borrador de la
portada (`NOTA_PORTADA`) cuando el usuario apruebe los textos.

Dentro del sitio, cada usuario imprime su manual desde Ayuda > "Manual completo" (`Ayuda.aspx?manual=1`, botón
Imprimir).
