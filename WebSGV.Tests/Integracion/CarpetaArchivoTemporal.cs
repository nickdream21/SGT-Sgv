using System;
using System.IO;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Redirige el archivo de PDF firmados (<c>~/App_Data/OrdenesViaje</c>) a una carpeta nueva bajo
    /// %TEMP% mientras dura la prueba (E6-7, M-087): las pruebas que firman con <c>FirmaService</c>
    /// generan y archivan el PDF de verdad, y no deben dejar archivos en el sitio. Al hacer Dispose
    /// restaura la carpeta anterior y borra la temporal con todo lo que tenga.
    /// Solo para clases de la colección de escritura (la carpeta del archivo es global).
    /// </summary>
    public sealed class CarpetaArchivoTemporal : IDisposable
    {
        private readonly string _anterior;

        public string Ruta { get; }

        public CarpetaArchivoTemporal()
        {
            Ruta = Path.Combine(Path.GetTempPath(), "SGV_PruebasFirma_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Ruta);
            _anterior = EmpresaConfigHelper.CarpetaArchivoOrdenesViajeDePrueba;
            EmpresaConfigHelper.CarpetaArchivoOrdenesViajeDePrueba = Ruta;
        }

        /// <summary>PDF que hay ahora en la carpeta (en sus subcarpetas AAAA/MM).</summary>
        public string[] Archivos() =>
            Directory.Exists(Ruta) ? Directory.GetFiles(Ruta, "*", SearchOption.AllDirectories).OrderBy(f => f).ToArray() : new string[0];

        public void Dispose()
        {
            EmpresaConfigHelper.CarpetaArchivoOrdenesViajeDePrueba = _anterior;
            if (Directory.Exists(Ruta))
                Directory.Delete(Ruta, recursive: true);
        }
    }
}
