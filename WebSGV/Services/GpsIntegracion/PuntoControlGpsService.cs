using System;
using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.GpsIntegracion
{
    public class PuntoControlGps
    {
        public string Nombre { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public int RadioMetros { get; set; }
    }

    /// <summary>Lectura de los puntos de control GPS fijos (Base, Trujillo, CEBAF, etc.) definidos en <c>PuntoControlGps</c>.</summary>
    public static class PuntoControlGpsService
    {
        public static PuntoControlGps ObtenerPorNombre(string nombre)
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT nombre, latitud, longitud, radioMetros FROM PuntoControlGps WHERE nombre = @nombre AND activo = 1",
                DbHelper.Param("@nombre", nombre));

            if (dt.Rows.Count == 0) return null;

            DataRow row = dt.Rows[0];
            return new PuntoControlGps
            {
                Nombre = row["nombre"].ToString(),
                Latitud = Convert.ToDouble(row["latitud"]),
                Longitud = Convert.ToDouble(row["longitud"]),
                RadioMetros = Convert.ToInt32(row["radioMetros"])
            };
        }
    }
}
