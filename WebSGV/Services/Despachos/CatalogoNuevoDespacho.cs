using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Choferes, tractos y carretas para los combos del despacho nuevo de la Bandeja de documentos (E6-20: antes la
    /// página leía la BD). Todos, los activos primero y los inactivos al final con "(inactivo)" o "(inactiva)"; el
    /// texto con los espacios repetidos juntados.
    /// </summary>
    public sealed class CatalogoNuevoDespacho
    {
        public List<KeyValuePair<int, string>> Conductores { get; private set; }
        public List<KeyValuePair<int, string>> Tractos { get; private set; }
        public List<KeyValuePair<int, string>> Carretas { get; private set; }

        /// <summary>Las tres listas (tres consultas). Los errores de BD se propagan.</summary>
        public static CatalogoNuevoDespacho Leer() => new CatalogoNuevoDespacho
        {
            Conductores = Lista(@"SELECT idConductor, LTRIM(RTRIM(CONCAT(nombre, ' ', apPaterno, ' ', apMaterno))) + CASE WHEN activo = 1 THEN '' ELSE ' (inactivo)' END
                                  FROM Conductor ORDER BY activo DESC, nombre, apPaterno"),
            Tractos = Lista("SELECT idTracto, LTRIM(RTRIM(placaTracto)) + CASE WHEN activo = 1 THEN '' ELSE ' (inactivo)' END FROM Tracto ORDER BY activo DESC, placaTracto"),
            Carretas = Lista("SELECT idCarreta, LTRIM(RTRIM(placaCarreta)) + CASE WHEN activo = 1 THEN '' ELSE ' (inactiva)' END FROM Carreta ORDER BY activo DESC, placaCarreta"),
        };

        private static List<KeyValuePair<int, string>> Lista(string sql) =>
            DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r[0]), Regex.Replace(Convert.ToString(r[1]), @"\s+", " "))).ToList();
    }
}
