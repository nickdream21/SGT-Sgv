using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Datos corporativos de la empresa usados para encabezar documentos (PDFs
    /// de Orden de Viaje, reportes, etc.). Se cargan desde Web.config en la
    /// sección appSettings, bajo las claves "Empresa.*".
    /// </summary>
    public sealed class EmpresaInfo
    {
        public string RazonSocial { get; set; }
        public string Rubro { get; set; }
        public string Ruc { get; set; }
        public string DomicilioFiscal { get; set; }
        public string Web { get; set; }
    }

    /// <summary>
    /// Datos de un formato documental controlado (ISO/BASC): código, versión
    /// y fecha de vigencia. Se cargan desde la tabla FormatoControlado.
    /// </summary>
    public sealed class FormatoControladoInfo
    {
        public int IdFormato { get; set; }
        public string CodigoFormato { get; set; }
        public string Nombre { get; set; }
        public string Version { get; set; }
        public DateTime FechaVigencia { get; set; }
    }

    /// <summary>
    /// Acceso centralizado a los datos de la empresa y los formatos controlados.
    /// Todos los PDFs del sistema deben consumir esta clase en lugar de tener
    /// constantes hardcoded.
    /// </summary>
    public static class EmpresaConfigHelper
    {
        private static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        /// <summary>
        /// Obtiene los datos corporativos desde Web.config (appSettings).
        /// </summary>
        public static EmpresaInfo ObtenerEmpresa()
        {
            var app = ConfigurationManager.AppSettings;
            return new EmpresaInfo
            {
                RazonSocial     = app["Empresa.RazonSocial"]     ?? "",
                Rubro           = app["Empresa.Rubro"]           ?? "",
                Ruc             = app["Empresa.Ruc"]             ?? "",
                DomicilioFiscal = app["Empresa.DomicilioFiscal"] ?? "",
                Web             = app["Empresa.Web"]             ?? ""
            };
        }

        /// <summary>
        /// Obtiene el formato controlado activo correspondiente al código indicado.
        /// Devuelve null si no existe o no está activo.
        /// </summary>
        /// <param name="codigoFormato">Código del formato, p.ej. "SGV-CDF-F-05".</param>
        public static FormatoControladoInfo ObtenerFormato(string codigoFormato)
        {
            if (string.IsNullOrWhiteSpace(codigoFormato)) return null;

            const string query = @"
                SELECT TOP 1 idFormato, codigoFormato, nombre, version, fechaVigencia
                FROM FormatoControlado
                WHERE codigoFormato = @codigo AND activo = 1";

            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@codigo", codigoFormato);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new FormatoControladoInfo
                    {
                        IdFormato     = Convert.ToInt32(r["idFormato"]),
                        CodigoFormato = r["codigoFormato"].ToString(),
                        Nombre        = r["nombre"].ToString(),
                        Version       = r["version"].ToString(),
                        FechaVigencia = Convert.ToDateTime(r["fechaVigencia"])
                    };
                }
            }
        }

        /// <summary>
        /// Devuelve la ruta física absoluta donde se archivan los PDFs firmados
        /// de Órdenes de Viaje, creando la carpeta si no existe.
        /// </summary>
        public static string ObtenerRutaArchivoOrdenesViaje()
        {
            string rutaVirtual = ConfigurationManager.AppSettings["OrdenViaje.RutaArchivo"]
                                 ?? "~/App_Data/OrdenesViaje";
            string rutaFisica = System.Web.Hosting.HostingEnvironment.MapPath(rutaVirtual)
                                ?? rutaVirtual;

            if (!System.IO.Directory.Exists(rutaFisica))
                System.IO.Directory.CreateDirectory(rutaFisica);

            return rutaFisica;
        }
    }
}
