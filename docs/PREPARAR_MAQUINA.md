# Preparar una máquina para trabajar en SGV

Para dejar lista una PC nueva o la laptop de casa. Cómo compilar, la arquitectura y las convenciones están en [`CLAUDE.md`](../CLAUDE.md); levantar y probar el sitio local, en [`herramientas/sitio-local/README.md`](../herramientas/sitio-local/README.md). Aquí solo lo que no está en git y lo que se instala una vez.

## 1. Clonar

```powershell
git clone https://github.com/nickdream21/SGT-Sgv.git
```

El historial se reescribió el 2026-07-10 (purga de PDF). Un clon anterior a esa fecha **no** se actualiza con `git pull`: volver a clonar (o `git fetch` + `git reset --hard origin/master`, que descarta lo local sin subir). **Nunca hacer push desde un clon viejo**: volvería a subir el historial purgado.

Al empezar cada día en una máquina: `git fetch` y `git pull` en `master` (las dos PC trabajan sobre la misma rama; un SP que sale DISTINTO puede ser un commit que aún no se bajó).

## 2. Archivos que no están en git

Copiarlos a mano desde la otra PC (p. ej. con la transferencia de archivos de AnyDesk) a la misma ruta dentro del repo. Nunca se suben a git ni se pegan sus valores en el chat.

| Archivo | Para qué | Cómo |
|---|---|---|
| `WebSGV/connectionStrings.config` | BD de pruebas `sgvActualizada` (sitio local, `sql.ps1`, respaldos y, solo con `SGV_TEST_BD=actualizada`, pruebas de integración) | Copiarlo, o crearlo desde `WebSGV/connectionStrings.config.example`; con `Encrypt=True;TrustServerCertificate=True` (U-084: sin el segundo, somee no conecta) |
| `WebSGV/connectionStrings.Production.config` | BD `sgvTransporte` (`-Entorno produccion`). Solo en la PC que vaya a hacer el [paso a producción](PASO_A_PRODUCCION.md) | Copiarlo (mismo formato que el anterior) |
| `WebSGV/appSettings.Secrets.config` | GPS (Onway), clave del cron del GPS, apisperu, SMTP y la clave maestra del segundo factor (`Seguridad.ClaveSegundoFactor`) | Copiarlo, o crearlo desde `WebSGV/appSettings.Secrets.config.example` (un comentario por clave). Para publicar desde esta PC debe tener `GpsAutomaticoClave` (ver `herramientas/publicar/README.md`). `Seguridad.ClaveSegundoFactor` debe ser la misma en todas las PCs y en el sitio que usan la misma BD (copiarla, no generar otra): si cambia, los secretos guardados no se descifran |
| `privado/` | Credenciales por rol de pruebas (`CREDENCIALES_USUARIOS.txt`), scripts con datos de personas, tablas de trabajo | Copiar la carpeta **menos** `ftp-respaldo.xml` |
| `docs/DOCUMENTACION/` | Documentos reales de prueba (ZIP de Vitapro, Word de programaciones, listas de choferes) | Copiar la carpeta |

**`privado/ftp-respaldo.xml` no se copia: se regenera en cada máquina.** Está cifrado con DPAPI para el usuario de Windows de la PC donde se creó, así que en otra PC no sirve. Lo usan el respaldo de archivos y `publicar.ps1`:

```powershell
.\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial   # pide la contraseña FTP de somee; repetir si se rota
```

## 3. Herramientas

- **Visual Studio 2022** con desarrollo web ASP.NET (MSBuild, IIS Express, `vstest.console.exe`) y `nuget restore WebSGV.sln` la primera vez.
- **Microsoft Command Line Utilities for SQL Server** (`sqlcmd`, ODBC 18): los usa `WebSGV/Database/sql.ps1` y los `aplicar-*.ps1`.
- **`sqlpackage`** (`dotnet tool install -g microsoft.sqlpackage`), solo si esta PC hace respaldos, el ensayo de restauración (`docs/PROCEDIMIENTO_RESPALDO.md`) o el ensayo de migraciones (sección 5.1).
- **Node.js**, solo para las capturas sin ventana (`herramientas/sitio-local/capturar.js`).
- **Extensión Claude in Chrome** (Chrome o Edge) para que Claude pruebe en el navegador: pasos en `herramientas/sitio-local/README.md`.

## 4. Variables de entorno (por usuario)

| Variable | La usan | Valor |
|---|---|---|
| `SGV_CARPETA_PUBLICACIONES` | `herramientas/publicar/publicar.ps1` (si no se pasa `-CarpetaPublicaciones`) | Carpeta **fuera del repo** donde se guarda un paquete por versión (en la PC de la oficina, `C:\PublicacionesSGV`) |
| `SGV_CARPETA_RESPALDO` | `herramientas/respaldo/*.ps1` (si no se pasa `-Carpeta`) | Carpeta **fuera del repo** de los respaldos |
| `SGV_SCRIPT_AVISO` | `sellar-registros.ps1` y la tarea `SGV - Respaldo diario` (`avisar-tarea.ps1`) | Ruta completa de `herramientas\comun\enviar-aviso.ps1` en esta PC: ante una FALLA manda **un** correo con el SMTP de `WebSGV/appSettings.Secrets.config` (sección 1) a `AvisoErroresPara` de `Web.config`. Sin ella, una FALLA no avisa por correo |
| `SGV_AVISO_PARA` | `enviar-aviso.ps1` (opcional) | Otro destinatario para esos avisos (varios separados por comas) |

```powershell
[Environment]::SetEnvironmentVariable('SGV_CARPETA_PUBLICACIONES', 'C:\PublicacionesSGV', 'User')
[Environment]::SetEnvironmentVariable('SGV_CARPETA_RESPALDO', '<carpeta de respaldos>', 'User')
[Environment]::SetEnvironmentVariable('SGV_SCRIPT_AVISO', (Resolve-Path .\herramientas\comun\enviar-aviso.ps1).Path, 'User')   # desde la raíz del repo
```

Solo hacen falta en la PC que publica o respalda. Abrir una consola nueva después de fijarlas. Para probar el aviso sin mandar correos: `.\herramientas\comun\enviar-aviso.ps1 -Asunto 'SGV: prueba de aviso' -Cuerpo prueba -CarpetaSalida <carpeta temporal>` deja un `.eml` en esa carpeta; sin `-CarpetaSalida` lo envía. La tarea lee `SGV_SCRIPT_AVISO` al correr: fijarla o cambiarla no obliga a registrarla de nuevo (actualizar el repo a E6-14, sí).

## 5. Si esta PC hace el respaldo diario

Hoy lo hace la PC de la oficina, a las 23:00, a la unidad compartida de Google Workspace `Respaldos_Sistemas_SGV`. En otra PC: instalar **Google Drive para escritorio** con una cuenta que vea esa unidad (aparece en `G:\Unidades compartidas\Respaldos_Sistemas_SGV`), usar esa ruta como `SGV_CARPETA_RESPALDO`, guardar la credencial FTP (sección 2) y registrar la tarea con `programar-respaldo.ps1` (`docs/PROCEDIMIENTO_RESPALDO.md`, sección 3). No registrarla en las dos PC a la vez.

### 5.1. Si esta PC ensaya migraciones y SP (`ensayar-migracion.ps1`)

Obligatorio antes de aplicar migraciones o SP a la BD del sitio (`herramientas/respaldo/README.md`, E3-1). Hoy solo está listo en la PC de la oficina. Hace falta:

- **SQL Server local**: SQL Server Express (instancia por defecto, `.`) o **LocalDB** (`(localdb)\MSSQLLocalDB`, viene con Visual Studio), con el usuario de Windows con permiso para crear bases. Comprobar: `sqlcmd -S . -E -C -Q "SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE')"` (o `-S "(localdb)\MSSQLLocalDB"`) → `1`.
- **`sqlpackage`** (sección 3), en el PATH o en `%USERPROFILE%\.dotnet\tools`.
- **La carpeta de respaldos** con al menos un `.bacpac` en `<carpeta>\bd\`: la unidad compartida de Google Drive (`G:\Unidades compartidas\Respaldos_Sistemas_SGV`, sección 5) como `SGV_CARPETA_RESPALDO` o `-Carpeta`. Sin Drive, `-RespaldoNuevo` exporta uno (necesita `connectionStrings.config`) o se pasa `-Bacpac` a un archivo fuera del repo.
- La solución compilada en Debug (para las `[FactBD]`) o `-SinPruebas`.

### 5.2. Copia local para las pruebas de integración (toda PC que corra la suite)

Las `[FactBD]` corren por defecto contra la copia local `sgvEnsayo_pruebas`, no contra `sgvActualizada`. Con los requisitos de 5.1 (SQL Server local o LocalDB, `sqlpackage`, un `.bacpac`):

```powershell
.\herramientas\respaldo\preparar-bd-pruebas.ps1 -Carpeta '<carpeta de respaldos>'     # sin .bacpac: agregar -RespaldoNuevo
```

Repetirlo (sin parámetros) tras aplicar un SP o una migración nuevos, y con `-Rehacer` cuando se quieran datos más nuevos (`herramientas/respaldo/README.md`, "Copia local para las pruebas de integración"). Sin la copia, las `[FactBD]` salen omitidas; `SGV_TEST_BD=actualizada` las corre contra `sgvActualizada` solo a pedido.

## 6. Comprobar

```powershell
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
$vstest = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -find '**\vstest.console.exe' | Select-Object -First 1
& $vstest WebSGV.Tests\bin\Debug\net48\WebSGV.Tests.dll
.\WebSGV\Database\sql.ps1 "SELECT DB_NAME()"          # debe decir sgvActualizada
```

Todas las pruebas en verde. Sin la copia local `sgvEnsayo_pruebas` (5.2), las de integración salen como omitidas (no fallan). Después, levantar el sitio local (`herramientas/sitio-local/README.md`).
