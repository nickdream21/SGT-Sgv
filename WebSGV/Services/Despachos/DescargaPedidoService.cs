using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Descarga por pedido: lista los despachos de CARGA de un pedido que todavía no
    /// tienen su DESCARGA, con su carga, para registrar todas las descargas de una vez.
    /// Por defecto propone el mismo conductor, tracto y carreta de la carga; el usuario
    /// cambia solo los que hicieron relevo en la base.
    /// </summary>
    public static class DescargaPedidoService
    {
        /// <summary>Un despacho de carga pendiente de descarga.</summary>
        [Serializable]
        public class CargaPendiente
        {
            public int IdDespacho { get; set; }
            public DateTime FechaDespacho { get; set; }
            public string NumeroPedido { get; set; }
            public string NumeroTransporte { get; set; }
            public string NumeroEntrega { get; set; }
            public string GuiaRemitente { get; set; }
            public int IdCliente { get; set; }
            public string NombreCliente { get; set; }
            public int IdConductor { get; set; }
            public string NombreConductor { get; set; }
            public int IdTracto { get; set; }
            public string PlacaTracto { get; set; }
            public int IdCarreta { get; set; }
            public string PlacaCarreta { get; set; }
            public string PlantaCarga { get; set; }
            public string DestinoCarga { get; set; }
            public bool EsExportacion { get; set; }
            public List<LineaCarga> Carga { get; set; } = new List<LineaCarga>();
        }

        // Carga activa del pedido, con su detalle de carga registrado, sin una descarga
        // activa enlazada. Se exige el detalle (DespachoCarga) porque es lo que hereda la
        // descarga: las cargas antiguas registradas a mano no lo tienen y quedarían
        // "pendientes" para siempre.
        private const string FiltroPendientes = @"
            d.activo = 1 AND d.tipoOperacion = 'CARGA' AND d.numeroPedido IS NOT NULL
            AND EXISTS (SELECT 1 FROM DespachoCarga dc WHERE dc.idDespacho = d.idDespacho)
            AND NOT EXISTS (SELECT 1 FROM Despachos x WHERE x.idDespachoCarga = d.idDespacho AND x.activo = 1)";

        /// <summary>Pedidos con tractos cargados que todavía no se descargaron, los más recientes primero.</summary>
        public static DataTable ObtenerPedidosPendientes() =>
            DbHelper.ConsultarTabla($@"
                SELECT TOP 50 d.numeroPedido, cl.nombre AS cliente,
                       COUNT(*) AS tractos, MIN(d.fechaDespacho) AS fechaCarga,
                       MAX(d.destinoCarga) AS destinoCarga
                FROM Despachos d
                INNER JOIN Cliente cl ON cl.idCliente = d.idCliente
                WHERE {FiltroPendientes}
                GROUP BY d.numeroPedido, cl.nombre
                ORDER BY MIN(d.fechaDespacho) DESC, d.numeroPedido");

        /// <summary>Despachos de carga del pedido que faltan descargar, con su carga.</summary>
        public static List<CargaPendiente> ObtenerCargasPendientes(string numeroPedido)
        {
            var dt = DbHelper.ConsultarTabla($@"
                SELECT d.idDespacho, d.fechaDespacho, d.numeroPedido, d.numeroTransporte, d.numeroEntrega,
                       d.guiaRemitente, d.idCliente, cl.nombre AS cliente,
                       d.idConductor, LTRIM(RTRIM(CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno))) AS conductor,
                       d.idTracto, t.placaTracto, d.idCarreta, ca.placaCarreta,
                       d.lugarOperacion, d.destinoCarga, d.esExportacion
                FROM Despachos d
                INNER JOIN Cliente cl  ON cl.idCliente  = d.idCliente
                INNER JOIN Conductor c ON c.idConductor = d.idConductor
                INNER JOIN Tracto t    ON t.idTracto    = d.idTracto
                INNER JOIN Carreta ca  ON ca.idCarreta  = d.idCarreta
                WHERE {FiltroPendientes} AND d.numeroPedido = @pedido
                ORDER BY d.numeroTransporte, d.idDespacho",
                DbHelper.Param("@pedido", numeroPedido));

            var cargas = dt.Rows.Cast<DataRow>().Select(r => new CargaPendiente
            {
                IdDespacho = Convert.ToInt32(r["idDespacho"]),
                FechaDespacho = Convert.ToDateTime(r["fechaDespacho"]),
                NumeroPedido = r["numeroPedido"].ToString(),
                NumeroTransporte = r["numeroTransporte"] as string,
                NumeroEntrega = r["numeroEntrega"] as string,
                GuiaRemitente = r["guiaRemitente"] as string,
                IdCliente = Convert.ToInt32(r["idCliente"]),
                NombreCliente = r["cliente"].ToString(),
                IdConductor = Convert.ToInt32(r["idConductor"]),
                NombreConductor = r["conductor"].ToString(),
                IdTracto = Convert.ToInt32(r["idTracto"]),
                PlacaTracto = r["placaTracto"].ToString(),
                IdCarreta = Convert.ToInt32(r["idCarreta"]),
                PlacaCarreta = r["placaCarreta"].ToString(),
                PlantaCarga = r["lugarOperacion"] as string,
                DestinoCarga = r["destinoCarga"] as string,
                EsExportacion = r["esExportacion"] != DBNull.Value && Convert.ToBoolean(r["esExportacion"])
            }).ToList();

            if (cargas.Count == 0) return cargas;

            // Detalle de carga de todos esos despachos en una sola consulta
            var parametros = cargas.Select((c, i) => DbHelper.Param("@d" + i, c.IdDespacho)).ToArray();
            string lista = string.Join(", ", parametros.Select(p => p.ParameterName));
            var lineas = DbHelper.ConsultarTabla($@"
                SELECT idDespacho, idProducto, codigoProducto, descripcion, lote, cantidad,
                       pesoBrutoKg, pesoNetoKg, fechaProduccion, fechaVencimiento
                FROM DespachoCarga WHERE idDespacho IN ({lista})
                ORDER BY idDespachoCarga", parametros);

            var porDespacho = cargas.ToDictionary(c => c.IdDespacho);
            foreach (DataRow r in lineas.Rows)
            {
                porDespacho[Convert.ToInt32(r["idDespacho"])].Carga.Add(new LineaCarga
                {
                    IdProducto = r["idProducto"] as int?,
                    CodigoProducto = r["codigoProducto"] as string,
                    Descripcion = r["descripcion"].ToString(),
                    Lote = r["lote"] as string,
                    Cantidad = Convert.ToDecimal(r["cantidad"]),
                    PesoBrutoKg = r["pesoBrutoKg"] as decimal?,
                    PesoNetoKg = r["pesoNetoKg"] as decimal?,
                    FechaProduccion = r["fechaProduccion"] as DateTime?,
                    FechaVencimiento = r["fechaVencimiento"] as DateTime?
                });
            }

            return cargas;
        }
    }
}
