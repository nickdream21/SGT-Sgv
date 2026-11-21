---
name: nuevo-sp
description: Pasos para crear o cambiar un procedimiento almacenado (sp_*) de SGV: el archivo en Database/StoredProcedures, cómo aplicarlo a la BD de pruebas y cómo llamarlo desde C#.
---

# Agregar un procedimiento almacenado

1. Crea `WebSGV/Database/StoredProcedures/sp_MODULO_Nombre.sql`, un archivo por procedimiento, con `CREATE OR ALTER PROCEDURE` y `SET NOCOUNT ON`. El prefijo es el del módulo (`sp_DC_`, `sp_LD_`, `sp_SE_`…).
2. Aplícalo a la BD de pruebas: `WebSGV/Database/aplicar-procedimientos.ps1 -Entorno pruebas` (primero `-SoloListar` para ver FALTA / DISTINTO). Hasta que se aplique, el código que lo llama falla.
3. Llámalo desde C# con `CommandType.StoredProcedure`; los parámetros que pueden ser nulos van como `(object)valor ?? DBNull.Value`.
4. Si necesita algo propio al pasar a producción (un control previo, un orden), anótalo en `docs/PASO_A_PRODUCCION.md`, y en la memoria `pendientes_paso_a_produccion.md`: producción se aplica aparte (`aplicar-procedimientos.ps1 -Entorno produccion`). Lo de cada publicación está en "Antes y después de publicar" de `herramientas/publicar/README.md`.
