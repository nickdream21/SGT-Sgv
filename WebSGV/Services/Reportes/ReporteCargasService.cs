using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Services.Reportes
{
    /// <summary>Filtros del reporte de cargas.</summary>
    public sealed class FiltroCargas
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public int? IdCliente { get; set; }
        public string NombreCliente { get; set; }
        /// <summary>N° de pedido (o su comienzo).</summary>
        public string Pedido { get; set; }
        /// <summary>Placa de tracto o carreta, nombre del chofer, guía o factura.</summary>
        public string Texto { get; set; }
    }

    /// <summary>
    /// Reporte de cargas: lo que se cargó (despachos de CARGA) con sus documentos, unidad y carga,
    /// incluidos los despachos históricos sin viaje del chofer. Solo lee.
    /// </summary>
    public static class ReporteCargasService
    {
        public const int MaxDias = 366;

        private const string Filtro = @"
            d.activo = 1 AND d.tipoOperacion = 'CARGA'
            AND d.fechaDespacho >= @desde AND d.fechaDespacho <= @hasta
            AND (@idCliente IS NULL OR d.idCliente = @idCliente)
            AND (@pedido IS NULL OR d.numeroPedido LIKE @pedido + '%')
            AND (@texto IS NULL
                 OR REPLACE(t.placaTracto, '-', '') LIKE '%' + REPLACE(@texto, '-', '') + '%'
                 OR REPLACE(ca.placaCarreta, '-', '') LIKE '%' + REPLACE(@texto, '-', '') + '%'
                 OR LTRIM(RTRIM(co.nombre)) + ' ' + LTRIM(RTRIM(co.apPaterno)) + ' ' + ISNULL(LTRIM(RTRIM(co.apMaterno)), '') LIKE '%' + @texto + '%'
                 OR d.guiaRemitente LIKE '%' + @texto + '%'
                 OR f.numeroFactura LIKE '%' + @texto + '%')";

        private const string Joins = @"
            FROM Despachos d
            JOIN Cliente cl            ON cl.idCliente = d.idCliente
            LEFT JOIN Planta p         ON p.idPlanta = d.idPlanta
            LEFT JOIN Factura f        ON f.idFactura = d.idFactura
            LEFT JOIN Conductor co     ON co.idConductor = d.idConductor
            LEFT JOIN Tracto t         ON t.idTracto = d.idTracto
            LEFT JOIN Carreta ca       ON ca.idCarreta = d.idCarreta
            LEFT JOIN ViajesEnProgreso vp ON vp.idViajeProgreso = d.idViajeProgreso";

        /// <summary>Una fila por despacho de carga, con los totales de su carga.</summary>
        public static DataTable Despachos(FiltroCargas f) =>
            DbHelper.ConsultarTabla(@"
                SELECT d.idDespacho, d.fechaDespacho AS Fecha, d.horaDespacho AS Hora, cl.nombre AS Cliente, p.nombre AS Planta,
                       NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') AS Pedido, f.numeroFactura AS Factura, f.moneda AS Moneda,
                       f.valorTotal AS ValorFactura, d.idFactura, d.guiaRemitente AS Guia, d.numeroTransporte AS Transporte,
                       LTRIM(RTRIM(LTRIM(RTRIM(co.nombre)) + ' ' + LTRIM(RTRIM(co.apPaterno)) + ' ' + ISNULL(LTRIM(RTRIM(co.apMaterno)), ''))) AS Conductor,
                       t.placaTracto AS Tracto, ca.placaCarreta AS Carreta,
                       c.Bolsas, c.KgBruto, c.KgNeto, c.Productos,
                       s.idSeguimiento, s.estado AS EstadoSeguimiento, s.bodegaDescarga AS Descarga,
                       vp.numeroViajeProgreso AS ViajeChofer
                " + Joins + @"
                OUTER APPLY (SELECT SUM(dc.cantidad) AS Bolsas, SUM(dc.pesoBrutoKg) AS KgBruto, SUM(dc.pesoNetoKg) AS KgNeto,
                                    COUNT(DISTINCT dc.codigoProducto) AS Productos
                             FROM DespachoCarga dc WHERE dc.idDespacho = d.idDespacho) c
                OUTER APPLY (SELECT TOP 1 se.idSeguimiento, se.estado, se.bodegaDescarga FROM SeguimientoExportacion se
                             WHERE se.idDespachoOrigen = d.idDespacho AND se.activo = 1 ORDER BY se.idSeguimiento) s
                WHERE " + Filtro + @"
                ORDER BY d.fechaDespacho, d.horaDespacho, d.idDespacho", Parametros(f));

        /// <summary>Una fila por línea de carga (producto y lote) de cada despacho.</summary>
        public static DataTable LineasCarga(FiltroCargas f) =>
            DbHelper.ConsultarTabla(@"
                SELECT d.fechaDespacho AS Fecha, NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') AS Pedido, d.guiaRemitente AS Guia,
                       t.placaTracto AS Tracto, ca.placaCarreta AS Carreta, dc.codigoProducto AS Codigo, dc.descripcion AS Producto,
                       dc.lote AS Lote, dc.cantidad AS Bolsas, dc.pesoBrutoKg AS KgBruto, dc.pesoNetoKg AS KgNeto,
                       dc.fechaProduccion AS Produccion, dc.fechaVencimiento AS Vencimiento
                " + Joins + @"
                JOIN DespachoCarga dc ON dc.idDespacho = d.idDespacho
                WHERE " + Filtro + @"
                ORDER BY d.fechaDespacho, d.horaDespacho, d.idDespacho, dc.idDespachoCarga", Parametros(f));

        private static System.Data.SqlClient.SqlParameter[] Parametros(FiltroCargas f) => new[]
        {
            DbHelper.Param("@desde", f.Desde.Date),
            DbHelper.Param("@hasta", f.Hasta.Date),
            DbHelper.Param("@idCliente", f.IdCliente),
            DbHelper.Param("@pedido", string.IsNullOrWhiteSpace(f.Pedido) ? null : f.Pedido.Trim()),
            DbHelper.Param("@texto", string.IsNullOrWhiteSpace(f.Texto) ? null : f.Texto.Trim()),
        };

        /// <summary>Totales para las tarjetas de la página y el resumen del Excel/PDF.</summary>
        public sealed class Totales
        {
            public int Despachos { get; set; }
            public int Pedidos { get; set; }
            public decimal Bolsas { get; set; }
            public decimal KgBruto { get; set; }
            public decimal FacturadoUsd { get; set; }
            public decimal FacturadoPen { get; set; }
            public int SinDocumentos { get; set; }
            public int SinViajeChofer { get; set; }
        }

        public static Totales Calcular(DataTable dt)
        {
            var filas = dt.Rows.Cast<DataRow>().ToList();
            // La factura es del pedido: se suma una vez aunque la compartan varios despachos.
            var facturas = filas.Where(r => r["idFactura"] != DBNull.Value)
                .GroupBy(r => Convert.ToInt32(r["idFactura"])).Select(g => g.First()).ToList();
            return new Totales
            {
                Despachos = filas.Count,
                Pedidos = filas.Select(r => Convert.ToString(r["Pedido"])).Where(p => p != "").Distinct().Count(),
                Bolsas = filas.Sum(r => Dec(r["Bolsas"])),
                KgBruto = filas.Sum(r => Dec(r["KgBruto"])),
                FacturadoUsd = facturas.Where(r => Convert.ToString(r["Moneda"]) == "USD").Sum(r => Dec(r["ValorFactura"])),
                FacturadoPen = facturas.Where(r => Convert.ToString(r["Moneda"]) != "USD").Sum(r => Dec(r["ValorFactura"])),
                SinDocumentos = filas.Count(r => r["Guia"] == DBNull.Value),
                SinViajeChofer = filas.Count(r => r["ViajeChofer"] == DBNull.Value),
            };
        }

        private static decimal Dec(object v) => v == null || v == DBNull.Value ? 0m : Convert.ToDecimal(v);

        /// <summary>"Sin viaje del chofer" para los despachos históricos; si no, el N° del viaje.</summary>
        public static string ViajeChofer(DataRow r) =>
            r["ViajeChofer"] == DBNull.Value ? "Sin viaje del chofer" : Convert.ToString(r["ViajeChofer"]);

        /// <summary>Excel / PDF: hoja de despachos y, en el Excel, la hoja con cada producto y lote.</summary>
        public static ReporteTabular Reporte(FiltroCargas f, string usuario, bool conDetalle)
        {
            DataTable dt = Despachos(f);
            var tot = Calcular(dt);
            var r = new ReporteTabular
            {
                Titulo = "Reporte de cargas",
                NombreHoja = "Cargas",
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario,
                Nota = "Un despacho de carga por unidad. \"Sin viaje del chofer\": carga registrada con los documentos del correo " +
                       "que no entra a la liquidación del chofer. El valor facturado suma cada factura una sola vez."
            };
            r.Filtros.Add($"Período: {f.Desde:dd/MM/yyyy} al {f.Hasta:dd/MM/yyyy}");
            if (!string.IsNullOrWhiteSpace(f.NombreCliente)) r.Filtros.Add("Cliente: " + f.NombreCliente);
            if (!string.IsNullOrWhiteSpace(f.Pedido)) r.Filtros.Add("Pedido: " + f.Pedido.Trim());
            if (!string.IsNullOrWhiteSpace(f.Texto)) r.Filtros.Add("Búsqueda: " + f.Texto.Trim());

            r.Columnas.Add(Col("Fecha", TipoColumnaReporte.Fecha, 0.9f, d => d["Fecha"]));
            r.Columnas.Add(Col("Cliente", TipoColumnaReporte.Texto, 1.2f, d => d["Cliente"]));
            r.Columnas.Add(Col("Pedido", TipoColumnaReporte.Codigo, 1f, d => d["Pedido"]));
            r.Columnas.Add(Col("Factura", TipoColumnaReporte.Codigo, 1.1f, d => d["Factura"]));
            r.Columnas.Add(Col("Guía", TipoColumnaReporte.Codigo, 1.1f, d => d["Guia"]));
            r.Columnas.Add(Col("N° transporte", TipoColumnaReporte.Codigo, 0.9f, d => d["Transporte"]));
            r.Columnas.Add(Col("Conductor", TipoColumnaReporte.Texto, 2.2f, d => d["Conductor"]));
            r.Columnas.Add(Col("Tracto", TipoColumnaReporte.Codigo, 0.8f, d => d["Tracto"]));
            r.Columnas.Add(Col("Carreta", TipoColumnaReporte.Codigo, 0.8f, d => d["Carreta"]));
            r.Columnas.Add(Col("Bolsas", TipoColumnaReporte.Entero, 0.8f, d => d["Bolsas"]));
            r.Columnas.Add(Col("Kg bruto", TipoColumnaReporte.Entero, 0.9f, d => d["KgBruto"]));
            r.Columnas.Add(Col("Descarga", TipoColumnaReporte.Codigo, 0.9f, d => d["Descarga"]));
            r.Columnas.Add(Col("Viaje del chofer", TipoColumnaReporte.Codigo, 1.1f, ViajeChofer));

            r.Resumen.Add(Linea("Despachos de carga", tot.Despachos, TipoColumnaReporte.Entero));
            r.Resumen.Add(Linea("Pedidos", tot.Pedidos, TipoColumnaReporte.Entero));
            r.Resumen.Add(Linea("Bolsas", tot.Bolsas, TipoColumnaReporte.Entero));
            r.Resumen.Add(Linea("Kg bruto", tot.KgBruto, TipoColumnaReporte.Entero));
            if (tot.FacturadoUsd > 0) r.Resumen.Add(Linea("Facturado en dólares", tot.FacturadoUsd, TipoColumnaReporte.Dolares, destacada: true));
            if (tot.FacturadoPen > 0) r.Resumen.Add(Linea("Facturado en soles", tot.FacturadoPen, TipoColumnaReporte.Soles, destacada: tot.FacturadoUsd == 0));
            if (tot.SinDocumentos > 0) r.Resumen.Add(Linea("Despachos todavía sin guía", tot.SinDocumentos, TipoColumnaReporte.Entero));

            if (conDetalle)
            {
                var lineas = LineasCarga(f);
                var det = new ReporteTabular
                {
                    Titulo = "Detalle de la carga",
                    NombreHoja = "Detalle de carga",
                    Filas = lineas.Rows.Cast<DataRow>().ToList(),
                    GeneradoPor = usuario,
                };
                det.Filtros.AddRange(r.Filtros);
                det.Columnas.Add(Col("Fecha", TipoColumnaReporte.Fecha, 0.9f, d => d["Fecha"]));
                det.Columnas.Add(Col("Pedido", TipoColumnaReporte.Codigo, 1f, d => d["Pedido"]));
                det.Columnas.Add(Col("Guía", TipoColumnaReporte.Codigo, 1.1f, d => d["Guia"]));
                det.Columnas.Add(Col("Tracto", TipoColumnaReporte.Codigo, 0.8f, d => d["Tracto"]));
                det.Columnas.Add(Col("Carreta", TipoColumnaReporte.Codigo, 0.8f, d => d["Carreta"]));
                det.Columnas.Add(Col("Código", TipoColumnaReporte.Codigo, 0.8f, d => d["Codigo"]));
                det.Columnas.Add(Col("Producto", TipoColumnaReporte.Texto, 2.6f, d => d["Producto"]));
                det.Columnas.Add(Col("Lote", TipoColumnaReporte.Codigo, 1f, d => d["Lote"]));
                det.Columnas.Add(Col("Bolsas", TipoColumnaReporte.Entero, 0.8f, d => d["Bolsas"]));
                det.Columnas.Add(Col("Kg bruto", TipoColumnaReporte.Entero, 0.9f, d => d["KgBruto"]));
                det.Columnas.Add(Col("Kg neto", TipoColumnaReporte.Entero, 0.9f, d => d["KgNeto"]));
                det.Columnas.Add(Col("Producción", TipoColumnaReporte.Fecha, 0.9f, d => d["Produccion"]));
                det.Columnas.Add(Col("Vencimiento", TipoColumnaReporte.Fecha, 0.9f, d => d["Vencimiento"]));
                r.HojasAdicionales.Add(det);
            }
            return r;
        }

        private static ColumnaReporte Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<DataRow, object> valor) =>
            new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = valor };

        private static LineaResumen Linea(string etiqueta, decimal valor, TipoColumnaReporte tipo, bool destacada = false) =>
            new LineaResumen { Etiqueta = etiqueta, Valor = valor, Tipo = tipo, Destacada = destacada };
    }
}
