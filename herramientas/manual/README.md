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

# 4. Capturar (todas las recetas, o una con --tema).
node herramientas\manual\capturar.mjs --bd sgvEnsayo_ayuda
node herramientas\manual\capturar.mjs --bd sgvEnsayo_ayuda --tema peajes

# 5. Cerrar IIS Express y borrar la BD de ensayo y el login que creó la importación (lo imprime el paso 1).
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -Q "ALTER DATABASE [sgvEnsayo_ayuda] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [sgvEnsayo_ayuda]"
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
