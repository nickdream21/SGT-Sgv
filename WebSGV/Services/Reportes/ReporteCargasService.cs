using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;

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
                       t.placaTracto AS Tracto, ca.placaCarreta AS Carreta, CAST(ISNULL(d.esExportacion, 0) AS BIT) AS EsExportacion,
                       c.Bolsas, c.KgBruto, c.KgNeto, c.Productos,
                       s.idSeguimiento, s.estado AS EstadoSeguimiento, s.bodegaDescarga AS Descarga,
                       vp.numeroViajeProgreso AS ViajeChofer,
                       -- Documentos que le faltan: mismas reglas que sp_LD_ObtenerLotesRegistrados / EstadoDocumental
                       CAST(CASE WHEN d.estadoDespacho = 'ANULADO' THEN 1 ELSE 0 END AS BIT) AS Anulado,
                       CASE WHEN d.estadoDespacho <> 'ANULADO' AND NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') IS NULL THEN 1 ELSE 0 END AS SinPedido,
                       CASE WHEN d.estadoDespacho <> 'ANULADO' AND NULLIF(LTRIM(RTRIM(d.guiaRemitente)), '') IS NULL THEN 1 ELSE 0 END AS SinGuia,
                       CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idFactura IS NULL THEN 1 ELSE 0 END AS SinFactura,
                       CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idFactura IS NOT NULL
                                 AND NOT EXISTS (SELECT 1 FROM DocumentosFactura df WHERE df.idFactura = d.idFactura AND df.activo = 1)
                            THEN 1 ELSE 0 END AS SinPdfFactura,
                       CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.esInternacional = 1 AND d.idCPIC IS NULL THEN 1 ELSE 0 END AS SinCPIC,
                       CASE WHEN d.estadoDespacho <> 'ANULADO' AND d.idCPIC IS NOT NULL
                                 AND NOT EXISTS (SELECT 1 FROM DocumentosCPIC dcp WHERE dcp.idCPIC = d.idCPIC AND dcp.activo = 1)
                            THEN 1 ELSE 0 END AS SinPdfCPIC
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
                SELECT d.idDespacho, d.fechaDespacho AS Fecha, NULLIF(LTRIM(RTRIM(d.numeroPedido)), '') AS Pedido, d.guiaRemitente AS Guia,
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
            public int Clientes { get; set; }
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
                Clientes = filas.Select(r => Convert.ToString(r["Cliente"]).Trim()).Distinct().Count(),
                Bolsas = filas.Sum(r => Dec(r["Bolsas"])),
                KgBruto = filas.Sum(r => Dec(r["KgBruto"])),
                FacturadoUsd = facturas.Where(r => Convert.ToString(r["Moneda"]) == "USD").Sum(r => Dec(r["ValorFactura"])),
                FacturadoPen = facturas.Where(r => Convert.ToString(r["Moneda"]) != "USD").Sum(r => Dec(r["ValorFactura"])),
                // Solo exportación espera la guía del correo; los terceros (Crisar) no la reciben.
                SinDocumentos = filas.Count(r => r["Guia"] == DBNull.Value && Convert.ToBoolean(r["EsExportacion"])),
                SinViajeChofer = filas.Count(r => r["ViajeChofer"] == DBNull.Value),
            };
        }

        /// <summary>Despachos de un mismo pedido (o, sin pedido, del mismo cliente y día), para la vista agrupada.</summary>
        public sealed class GrupoPedido
        {
            public string Titulo { get; set; }
            public string Cliente { get; set; }
            public DateTime Desde { get; set; }
            public DateTime Hasta { get; set; }
            public string Factura { get; set; }
            public string Moneda { get; set; }
            public decimal? ValorFactura { get; set; }
            public decimal Bolsas { get; set; }
            public decimal KgBruto { get; set; }
            /// <summary>"JAVE 3 · INBALNOR 2".</summary>
            public string Descargas { get; set; }
            public int SinViajeChofer { get; set; }
            public List<DataRow> Filas { get; set; }
        }

        public static List<GrupoPedido> AgruparPorPedido(DataTable dt) =>
            dt.Rows.Cast<DataRow>()
                .GroupBy(r => r["Pedido"] != DBNull.Value
                    ? Convert.ToString(r["Pedido"])
                    : $"{r["Cliente"]}|{Convert.ToDateTime(r["Fecha"]):yyyyMMdd}")
                .Select(g =>
                {
                    var filas = g.ToList();
                    var primera = filas[0];
                    return new GrupoPedido
                    {
                        Titulo = primera["Pedido"] != DBNull.Value ? Convert.ToString(primera["Pedido"]) : "Sin pedido",
                        Cliente = Convert.ToString(primera["Cliente"]),
                        Desde = filas.Min(r => Convert.ToDateTime(r["Fecha"])),
                        Hasta = filas.Max(r => Convert.ToDateTime(r["Fecha"])),
                        Factura = filas.Select(r => Convert.ToString(r["Factura"])).FirstOrDefault(s => s != ""),
                        Moneda = filas.Select(r => Convert.ToString(r["Moneda"])).FirstOrDefault(s => s != ""),
                        ValorFactura = filas.Where(r => r["ValorFactura"] != DBNull.Value).Select(r => (decimal?)Dec(r["ValorFactura"])).FirstOrDefault(),
                        Bolsas = filas.Sum(r => Dec(r["Bolsas"])),
                        KgBruto = filas.Sum(r => Dec(r["KgBruto"])),
                        Descargas = string.Join(" · ", filas.Where(r => r["Descarga"] != DBNull.Value)
                            .GroupBy(r => Convert.ToString(r["Descarga"])).OrderByDescending(x => x.Count())
                            .Select(x => $"{x.Key} {x.Count()}")),
                        SinViajeChofer = filas.Count(r => r["ViajeChofer"] == DBNull.Value),
                        Filas = filas
                    };
                })
                .OrderBy(x => x.Desde).ThenBy(x => x.Titulo)
                .ToList();

        // ================= Pestaña Resumen =================

        /// <summary>Carga de un día (o de un mes, en rangos largos) para el gráfico.</summary>
        public sealed class CargaPeriodo
        {
            public DateTime Inicio { get; set; }
            /// <summary>"03/10" o "oct. 2026".</summary>
            public string Etiqueta { get; set; }
            public int Viajes { get; set; }
            public decimal KgBruto { get; set; }
            public decimal Toneladas => Math.Round(KgBruto / 1000m, 1);
        }

        /// <summary>Hasta este número de días se grafica por día; más, por mes.</summary>
        public const int MaxDiasGraficoDiario = 62;

        /// <summary>Viajes y kg por día del rango (los días sin carga van en cero) o por mes si el rango es largo.</summary>
        public static List<CargaPeriodo> ResumenPorPeriodo(DataTable dt, DateTime desde, DateTime hasta)
        {
            bool porMes = (hasta.Date - desde.Date).TotalDays + 1 > MaxDiasGraficoDiario;
            Func<DateTime, DateTime> clave = f => porMes ? new DateTime(f.Year, f.Month, 1) : f.Date;
            var datos = dt.Rows.Cast<DataRow>()
                .GroupBy(r => clave(Convert.ToDateTime(r["Fecha"])))
                .ToDictionary(g => g.Key, g => g.ToList());

            var lista = new List<CargaPeriodo>();
            var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
            for (DateTime p = clave(desde); p <= hasta.Date; p = porMes ? p.AddMonths(1) : p.AddDays(1))
            {
                List<DataRow> filas;
                datos.TryGetValue(p, out filas);
                lista.Add(new CargaPeriodo
                {
                    Inicio = p,
                    Etiqueta = porMes ? p.ToString("MMM yyyy", cultura) : p.ToString("dd/MM"),
                    Viajes = filas?.Count ?? 0,
                    KgBruto = filas?.Sum(r => Dec(r["KgBruto"])) ?? 0m
                });
            }
            return lista;
        }

        /// <summary>Lo cargado para un cliente en el período.</summary>
        public sealed class CargaCliente
        {
            public string Cliente { get; set; }
            public int Viajes { get; set; }
            public int Pedidos { get; set; }
            public decimal Bolsas { get; set; }
            public decimal KgBruto { get; set; }
            /// <summary>% del peso total del período.</summary>
            public decimal Porcentaje { get; set; }
            public decimal FacturadoUsd { get; set; }
            public decimal FacturadoPen { get; set; }
            public decimal Toneladas => Math.Round(KgBruto / 1000m, 1);
        }

        /// <summary>Una fila por cliente, de mayor a menor peso; cada factura se suma una vez.</summary>
        public static List<CargaCliente> ResumenPorCliente(DataTable dt)
        {
            var filas = dt.Rows.Cast<DataRow>().ToList();
            decimal kgTotal = filas.Sum(r => Dec(r["KgBruto"]));
            return filas
                .GroupBy(r => Convert.ToString(r["Cliente"]).Trim())
                .Select(g =>
                {
                    var facturas = g.Where(r => r["idFactura"] != DBNull.Value)
                        .GroupBy(r => Convert.ToInt32(r["idFactura"])).Select(x => x.First()).ToList();
                    decimal kg = g.Sum(r => Dec(r["KgBruto"]));
                    return new CargaCliente
                    {
                        Cliente = g.Key,
                        Viajes = g.Count(),
                        Pedidos = g.Select(r => Convert.ToString(r["Pedido"])).Where(p => p != "").Distinct().Count(),
                        Bolsas = g.Sum(r => Dec(r["Bolsas"])),
                        KgBruto = kg,
                        Porcentaje = Porcentaje(kg, kgTotal),
                        FacturadoUsd = facturas.Where(r => Convert.ToString(r["Moneda"]) == "USD").Sum(r => Dec(r["ValorFactura"])),
                        FacturadoPen = facturas.Where(r => Convert.ToString(r["Moneda"]) != "USD").Sum(r => Dec(r["ValorFactura"])),
                    };
                })
                .OrderByDescending(c => c.KgBruto).ThenBy(c => c.Cliente)
                .ToList();
        }

        /// <summary>Lo cargado de un producto en el período.</summary>
        public sealed class CargaProducto
        {
            public string Codigo { get; set; }
            public string Producto { get; set; }
            public int Viajes { get; set; }
            public int Lotes { get; set; }
            public decimal Bolsas { get; set; }
            public decimal KgBruto { get; set; }
            public decimal Porcentaje { get; set; }
            public decimal Toneladas => Math.Round(KgBruto / 1000m, 1);
        }

        /// <summary>Una fila por producto (por código; sin código, por descripción) a partir de <see cref="LineasCarga"/>.</summary>
        public static List<CargaProducto> ResumenPorProducto(DataTable lineas)
        {
            var filas = lineas.Rows.Cast<DataRow>().ToList();
            decimal kgTotal = filas.Sum(r => Dec(r["KgBruto"]));
            Func<DataRow, string> codigo = r => Convert.ToString(r["Codigo"]).Trim();
            Func<DataRow, string> nombre = r => Convert.ToString(r["Producto"]).Trim();
            return filas
                .GroupBy(r => codigo(r) != "" ? codigo(r) : "~" + nombre(r).ToUpperInvariant())
                .Select(g =>
                {
                    decimal kg = g.Sum(r => Dec(r["KgBruto"]));
                    return new CargaProducto
                    {
                        Codigo = codigo(g.First()),
                        Producto = g.Select(nombre).FirstOrDefault(n => n != "") ?? "",
                        Viajes = g.Select(r => Convert.ToInt32(r["idDespacho"])).Distinct().Count(),
                        Lotes = g.Select(r => Convert.ToString(r["Lote"]).Trim()).Where(l => l != "").Distinct().Count(),
                        Bolsas = g.Sum(r => Dec(r["Bolsas"])),
                        KgBruto = kg,
                        Porcentaje = Porcentaje(kg, kgTotal),
                    };
                })
                .OrderByDescending(p => p.KgBruto).ThenBy(p => p.Producto)
                .ToList();
        }

        /// <summary>Documentación del período: lo que falta (con los textos de <see cref="EstadoDocumental"/>).</summary>
        public sealed class ResumenDocumentos
        {
            public EstadoDocumental Estado { get; set; }
            /// <summary>Despachos no anulados con al menos un documento faltante.</summary>
            public int Pendientes { get; set; }
            /// <summary>Pendientes con más de 3 días desde la carga.</summary>
            public int Atrasados { get; set; }
            public int Completos => Estado.Despachos - Pendientes;
        }

        public const int DiasToleranciaDocumentos = 3;

        public static ResumenDocumentos ResumenDocumental(DataTable dt, DateTime hoy)
        {
            var vigentes = dt.Rows.Cast<DataRow>().Where(r => !Convert.ToBoolean(r["Anulado"])).ToList();
            Func<DataRow, int> faltan = r => Int(r["SinPedido"]) + Int(r["SinGuia"]) + Int(r["SinFactura"])
                                           + Int(r["SinPdfFactura"]) + Int(r["SinCPIC"]) + Int(r["SinPdfCPIC"]);
            var pendientes = vigentes.Where(r => faltan(r) > 0).ToList();
            return new ResumenDocumentos
            {
                Estado = new EstadoDocumental
                {
                    Despachos = vigentes.Count,
                    SinPedido = vigentes.Sum(r => Int(r["SinPedido"])),
                    SinGuia = vigentes.Sum(r => Int(r["SinGuia"])),
                    SinFactura = vigentes.Sum(r => Int(r["SinFactura"])),
                    SinPdfFactura = vigentes.Sum(r => Int(r["SinPdfFactura"])),
                    SinCPIC = vigentes.Sum(r => Int(r["SinCPIC"])),
                    SinPdfCPIC = vigentes.Sum(r => Int(r["SinPdfCPIC"])),
                },
                Pendientes = pendientes.Count,
                Atrasados = pendientes.Count(r => (hoy.Date - Convert.ToDateTime(r["Fecha"]).Date).Days > DiasToleranciaDocumentos),
            };
        }

        /// <summary>
        /// El período con el que se compara. Si el rango llega a días que aún no pasan (el mes en curso),
        /// se compara solo lo transcurrido: del 1 al 2 de noviembre contra el 1 al 2 de octubre, no contra
        /// octubre entero. Un mes calendario se compara con el mismo tramo del mes anterior; otro rango,
        /// con los mismos días inmediatamente antes.
        /// </summary>
        public static void PeriodoAnterior(DateTime desde, DateTime hasta, DateTime hoy, out DateTime antDesde, out DateTime antHasta)
        {
            desde = desde.Date; hasta = hasta.Date; hoy = hoy.Date;
            DateTime hastaEf = hasta > hoy && hoy >= desde ? hoy : hasta;
            bool mesCalendario = desde.Day == 1 && hasta == desde.AddMonths(1).AddDays(-1);
            if (mesCalendario)
            {
                antDesde = desde.AddMonths(-1);
                DateTime finMesAnterior = desde.AddDays(-1);
                antHasta = antDesde.AddDays((hastaEf - desde).Days);
                if (antHasta > finMesAnterior) antHasta = finMesAnterior;
                return;
            }
            int dias = (hastaEf - desde).Days + 1;
            antDesde = desde.AddDays(-dias);
            antHasta = desde.AddDays(-1);
        }

        /// <summary>
        /// Con menos viajes que esto en el período anterior no se muestran variaciones: el sistema tiene
        /// datos reales desde octubre de 2026 y "▲ 5 725 %" contra 4 viajes no dice nada.
        /// </summary>
        public const int MinViajesParaComparar = 10;

        /// <summary>Variación porcentual frente al período anterior; null si antes no hubo nada.</summary>
        public static decimal? Variacion(decimal actual, decimal anterior) =>
            anterior == 0 ? (decimal?)null : Math.Round((actual - anterior) / anterior * 100m, 0);

        private static decimal Porcentaje(decimal parte, decimal total) =>
            total == 0 ? 0m : Math.Round(parte / total * 100m, 1);

        private static int Int(object v) => v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);

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

                r.HojasAdicionales.Add(HojaPorCliente(ResumenPorCliente(dt), r.Filtros, usuario));
                r.HojasAdicionales.Add(HojaPorProducto(ResumenPorProducto(lineas), r.Filtros, usuario));
                r.HojasAdicionales.Add(det);
            }
            return r;
        }

        private static ReporteTabular HojaPorCliente(List<CargaCliente> clientes, List<string> filtros, string usuario)
        {
            var t = new DataTable();
            t.Columns.Add("Cliente", typeof(string));
            t.Columns.Add("Viajes", typeof(int));
            t.Columns.Add("Pedidos", typeof(int));
            t.Columns.Add("Bolsas", typeof(decimal));
            t.Columns.Add("KgBruto", typeof(decimal));
            t.Columns.Add("Porcentaje", typeof(string));
            t.Columns.Add("FacturadoUsd", typeof(decimal));
            t.Columns.Add("FacturadoPen", typeof(decimal));
            foreach (var c in clientes)
                t.Rows.Add(c.Cliente, c.Viajes, c.Pedidos, c.Bolsas, c.KgBruto, TextoPorcentaje(c.Porcentaje),
                           c.FacturadoUsd, c.FacturadoPen);

            var h = new ReporteTabular
            {
                Titulo = "Carga por cliente",
                NombreHoja = "Por cliente",
                Filas = t.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario,
                Nota = "% del peso bruto total del período. El facturado suma cada factura una sola vez."
            };
            h.Filtros.AddRange(filtros);
            h.Columnas.Add(Col("Cliente", TipoColumnaReporte.Texto, 2.4f, d => d["Cliente"]));
            h.Columnas.Add(Col("Viajes", TipoColumnaReporte.Entero, 0.8f, d => d["Viajes"]));
            h.Columnas.Add(Col("Pedidos", TipoColumnaReporte.Entero, 0.8f, d => d["Pedidos"]));
            h.Columnas.Add(Col("Bolsas", TipoColumnaReporte.Entero, 0.9f, d => d["Bolsas"]));
            h.Columnas.Add(Col("Kg bruto", TipoColumnaReporte.Entero, 1f, d => d["KgBruto"]));
            h.Columnas.Add(Col("% del peso", TipoColumnaReporte.Codigo, 0.8f, d => d["Porcentaje"]));
            h.Columnas.Add(Col("Facturado US$", TipoColumnaReporte.Dolares, 1.1f, d => d["FacturadoUsd"]));
            h.Columnas.Add(Col("Facturado S/", TipoColumnaReporte.Soles, 1.1f, d => d["FacturadoPen"]));
            return h;
        }

        private static ReporteTabular HojaPorProducto(List<CargaProducto> productos, List<string> filtros, string usuario)
        {
            var t = new DataTable();
            t.Columns.Add("Codigo", typeof(string));
            t.Columns.Add("Producto", typeof(string));
            t.Columns.Add("Viajes", typeof(int));
            t.Columns.Add("Lotes", typeof(int));
            t.Columns.Add("Bolsas", typeof(decimal));
            t.Columns.Add("KgBruto", typeof(decimal));
            t.Columns.Add("Porcentaje", typeof(string));
            foreach (var p in productos)
                t.Rows.Add(p.Codigo, p.Producto, p.Viajes, p.Lotes, p.Bolsas, p.KgBruto, TextoPorcentaje(p.Porcentaje));

            var h = new ReporteTabular
            {
                Titulo = "Carga por producto",
                NombreHoja = "Por producto",
                Filas = t.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario,
                Nota = "Viajes: unidades que llevaron el producto. Lotes: lotes distintos cargados."
            };
            h.Filtros.AddRange(filtros);
            h.Columnas.Add(Col("Código", TipoColumnaReporte.Codigo, 0.9f, d => d["Codigo"]));
            h.Columnas.Add(Col("Producto", TipoColumnaReporte.Texto, 2.8f, d => d["Producto"]));
            h.Columnas.Add(Col("Viajes", TipoColumnaReporte.Entero, 0.8f, d => d["Viajes"]));
            h.Columnas.Add(Col("Lotes", TipoColumnaReporte.Entero, 0.8f, d => d["Lotes"]));
            h.Columnas.Add(Col("Bolsas", TipoColumnaReporte.Entero, 0.9f, d => d["Bolsas"]));
            h.Columnas.Add(Col("Kg bruto", TipoColumnaReporte.Entero, 1f, d => d["KgBruto"]));
            h.Columnas.Add(Col("% del peso", TipoColumnaReporte.Codigo, 0.8f, d => d["Porcentaje"]));
            return h;
        }

        /// <summary>"12,3 %" en es-PE.</summary>
        public static string TextoPorcentaje(decimal p) =>
            p.ToString("N1", System.Globalization.CultureInfo.GetCultureInfo("es-PE")) + " %";

        private static ColumnaReporte Col(string titulo, TipoColumnaReporte tipo, float ancho, Func<DataRow, object> valor) =>
            new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = valor };

        private static LineaResumen Linea(string etiqueta, decimal valor, TipoColumnaReporte tipo, bool destacada = false) =>
            new LineaResumen { Etiqueta = etiqueta, Valor = valor, Tipo = tipo, Destacada = destacada };
    }
}
