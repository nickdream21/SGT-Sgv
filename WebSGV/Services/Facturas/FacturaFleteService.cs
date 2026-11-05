using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Facturas;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Facturas de flete (las nuestras, E001-xxxxx): registro desde el PDF de SUNAT y conciliación
    /// con lo cargado en el sistema (FacturasFlete.aspx).
    ///
    /// Cada factura de flete se une a su pedido por la factura comercial que menciona
    /// ("SEGUN FACTURA COMERCIAL: F222-00008426"), la misma que guarda el lote del pedido en
    /// <c>Factura</c>. Con eso se ve, por pedido: lo facturado frente a lo cargado (toneladas o
    /// unidades), lo cargado que falta facturar y las facturas cuya carga no está en el sistema.
    /// </summary>
    public static class FacturaFleteService
    {
        /// <summary>Diferencia de toneladas que se tolera entre lo facturado y lo cargado (redondeos).</summary>
        public const decimal ToleranciaToneladas = 0.05m;

        public const string CarpetaUploads = "FacturasFlete";

        private static readonly CultureInfo Pe = CultureInfo.GetCultureInfo("es-PE");

        // ── Registro ─────────────────────────────────────────────────────────

        /// <summary>
        /// Lee el PDF, comprueba que la factura sea de la empresa (RUC emisor) y la registra con su
        /// PDF en ~/Uploads/FacturasFlete/{año}/. Volver a subir la misma factura no la duplica.
        /// </summary>
        public static ResultadoFacturaSubida Registrar(string nombreArchivo, byte[] pdf, string usuario,
                                                       string raizFisicaUploads, string rucEmpresa)
        {
            var r = new ResultadoFacturaSubida { Archivo = nombreArchivo, Estado = "No se registró" };
            FacturaFleteLeida f;
            try
            {
                f = LectorFacturaFlete.Leer(ExtractorTextoPdf.ExtraerLineas(pdf));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo leer el PDF de la factura de flete {Archivo}", nombreArchivo);
                r.Detalle = "No se pudo leer el PDF (¿es la representación impresa de SUNAT?).";
                return r;
            }
            r.Factura = f;

            if (!f.EsValida)
            {
                r.Detalle = string.Join(" ", f.Errores);
                return r;
            }
            if (!string.IsNullOrWhiteSpace(rucEmpresa) && f.RucEmisor != null && f.RucEmisor != rucEmpresa.Trim())
            {
                r.Detalle = $"La emitió el RUC {f.RucEmisor}, no la empresa ({rucEmpresa.Trim()}): no es una factura de flete nuestra.";
                return r;
            }

            string rutaVirtual = null;
            if (!string.IsNullOrEmpty(raizFisicaUploads))
            {
                string anio = f.FechaEmision.Value.Year.ToString(CultureInfo.InvariantCulture);
                string nombre = DocumentoHelper.NombreSeguro(f.NumeroFactura) + ".pdf";
                string carpeta = Path.Combine(raizFisicaUploads, CarpetaUploads, anio);
                string fisica = Path.Combine(carpeta, nombre);
                if (!DocumentoHelper.EstaDentroDe(fisica, raizFisicaUploads))
                    throw new ErrorNegocioException("Nombre de archivo no permitido.");
                Directory.CreateDirectory(carpeta);
                File.WriteAllBytes(fisica, pdf);
                rutaVirtual = $"~/Uploads/{CarpetaUploads}/{anio}/{nombre}";
            }

            DataTable dt = DbHelper.ConsultarTablaSp("sp_FF_RegistrarFacturaFlete",
                DbHelper.Param("@numeroFactura", f.NumeroFactura),
                DbHelper.Param("@fechaEmision", f.FechaEmision.Value),
                DbHelper.Param("@fechaVencimiento", f.FechaVencimiento),
                DbHelper.Param("@formaPago", Recortar(f.FormaPago, 20)),
                DbHelper.Param("@rucCliente", f.RucCliente),
                DbHelper.Param("@nombreCliente", Recortar(f.NombreCliente, 200)),
                DbHelper.Param("@moneda", f.Moneda),
                DbHelper.Param("@ordenServicio", Recortar(f.OrdenServicio, 30)),
                DbHelper.Param("@observacion", Recortar(f.Observacion, 300)),
                DbHelper.Param("@cantidad", f.Cantidad),
                DbHelper.Param("@unidadMedida", Recortar(f.UnidadMedida, 30)),
                DbHelper.Param("@valorUnitario", f.ValorUnitario),
                DbHelper.Param("@descripcion", Recortar(f.Descripcion, 600)),
                DbHelper.Param("@valorVenta", f.ValorVenta),
                DbHelper.Param("@igv", f.Igv),
                DbHelper.Param("@importeTotal", f.ImporteTotal.Value),
                DbHelper.Param("@montoNetoPendiente", f.MontoNetoPendiente),
                DbHelper.Param("@numeroFacturaComercial", f.NumeroFacturaComercial),
                DbHelper.Param("@numeroCPIC", f.NumeroCPIC),
                DbHelper.Param("@rutaArchivo", rutaVirtual),
                DbHelper.Param("@usuario", usuario));

            string accion = dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0]["accion"]) : "";
            int id = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["idFacturaFlete"]) : 0;
            r.Ok = true;
            r.Estado = accion == "CREADA" ? "Registrada" : accion == "ACTUALIZADA" ? "Actualizada" : "Sin cambios";
            r.Detalle = string.Join(" ", f.Observaciones);
            if (accion != "SIN CAMBIOS")
                AuditoriaHelper.Registrar(accion == "CREADA" ? "INSERT" : "UPDATE", "FacturaFlete", id,
                    $"Factura de flete {f.NumeroFactura} ({f.Moneda} {f.ImporteTotal.Value.ToString("N2", CultureInfo.InvariantCulture)}, " +
                    $"factura comercial {f.NumeroFacturaComercial ?? "-"}) leída de {nombreArchivo}");
            return r;
        }

        private static string Recortar(string s, int max) =>
            s == null ? null : (s.Length <= max ? s : s.Substring(0, max));

        // ── Consultas ────────────────────────────────────────────────────────

        public static List<PedidoCargado> ObtenerPedidos(DateTime desde, DateTime hasta, int? idCliente,
                                                         out Dictionary<int, TarifaCliente> tarifas)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_FF_ObtenerPedidosCargados",
                DbHelper.Param("@desde", desde.Date), DbHelper.Param("@hasta", hasta.Date), DbHelper.Param("@idCliente", idCliente));
            tarifas = new Dictionary<int, TarifaCliente>();
            var lista = new List<PedidoCargado>();
            foreach (DataRow r in dt.Rows)
            {
                var p = new PedidoCargado
                {
                    IdCliente = r["idCliente"] == DBNull.Value ? 0 : Convert.ToInt32(r["idCliente"]),
                    Cliente = Convert.ToString(r["cliente"]).Trim(),
                    NumeroPedido = r["numeroPedido"] == DBNull.Value ? null : Convert.ToString(r["numeroPedido"]).Trim(),
                    FacturaComercial = r["facturaComercial"] == DBNull.Value ? null : Convert.ToString(r["facturaComercial"]).Trim(),
                    FechaCarga = Convert.ToDateTime(r["fechaCarga"]),
                    Unidades = Convert.ToInt32(r["unidades"]),
                    ToneladasNetas = Convert.ToDecimal(r["toneladasNetas"]),
                    UnidadesSinCarga = Convert.ToInt32(r["unidadesSinCarga"]),
                    ViajesSeguimiento = r["viajesSeguimiento"] == DBNull.Value ? 0 : Convert.ToInt32(r["viajesSeguimiento"])
                };
                lista.Add(p);
                if (!tarifas.ContainsKey(p.IdCliente))
                    tarifas[p.IdCliente] = new TarifaCliente
                    {
                        FacturasRegistradas = Convert.ToInt32(r["facturasDelCliente"]),
                        ValorUnitario = r["tarifaValorUnitario"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["tarifaValorUnitario"]),
                        UnidadMedida = r["tarifaUnidad"] == DBNull.Value ? null : Convert.ToString(r["tarifaUnidad"]),
                        Moneda = r["tarifaMoneda"] == DBNull.Value ? null : Convert.ToString(r["tarifaMoneda"])
                    };
            }
            return lista;
        }

        public static List<FacturaFleteRegistrada> ObtenerFacturas(DateTime desde, DateTime hasta, int? idCliente,
                                                                   out Dictionary<string, DateTime?> cargaDeComercial)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_FF_ObtenerFacturasFlete",
                DbHelper.Param("@desde", desde.Date), DbHelper.Param("@hasta", hasta.Date), DbHelper.Param("@idCliente", idCliente));
            cargaDeComercial = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            var lista = new List<FacturaFleteRegistrada>();
            foreach (DataRow r in dt.Rows)
            {
                var f = new FacturaFleteRegistrada
                {
                    IdFacturaFlete = Convert.ToInt32(r["idFacturaFlete"]),
                    NumeroFactura = Convert.ToString(r["numeroFactura"]),
                    FechaEmision = Convert.ToDateTime(r["fechaEmision"]),
                    FechaVencimiento = r["fechaVencimiento"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fechaVencimiento"]),
                    IdCliente = r["idCliente"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idCliente"]),
                    Cliente = Convert.ToString(r["cliente"]).Trim(),
                    Moneda = Convert.ToString(r["moneda"]),
                    OrdenServicio = r["ordenServicio"] == DBNull.Value ? null : Convert.ToString(r["ordenServicio"]),
                    Cantidad = r["cantidad"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["cantidad"]),
                    UnidadMedida = r["unidadMedida"] == DBNull.Value ? null : Convert.ToString(r["unidadMedida"]),
                    ValorUnitario = r["valorUnitario"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["valorUnitario"]),
                    ImporteTotal = Convert.ToDecimal(r["importeTotal"]),
                    MontoNetoPendiente = r["montoNetoPendiente"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["montoNetoPendiente"]),
                    FacturaComercial = r["numeroFacturaComercial"] == DBNull.Value ? null : Convert.ToString(r["numeroFacturaComercial"]),
                    NumeroCPIC = r["numeroCPIC"] == DBNull.Value ? null : Convert.ToString(r["numeroCPIC"]),
                    RutaArchivo = r["rutaArchivo"] == DBNull.Value ? null : Convert.ToString(r["rutaArchivo"])
                };
                lista.Add(f);
                if (f.FacturaComercial != null)
                    cargaDeComercial[f.FacturaComercial] = r["fechaCargaSistema"] == DBNull.Value
                        ? (DateTime?)null : Convert.ToDateTime(r["fechaCargaSistema"]);
            }
            return lista;
        }

        /// <summary>Última tarifa facturada a un cliente (para estimar lo que falta facturar).</summary>
        public class TarifaCliente
        {
            public int FacturasRegistradas { get; set; }
            public decimal? ValorUnitario { get; set; }
            public string UnidadMedida { get; set; }
            public string Moneda { get; set; }
        }

        // ── Conciliación (sin BD) ────────────────────────────────────────────

        /// <summary>"TONELADAS", "TM", "TNE" → toneladas; "VIAJE", "UNIDAD", "SERVICIO" → unidades; si no, null.</summary>
        public static bool? FacturaPorToneladas(string unidadMedida)
        {
            string u = (unidadMedida ?? string.Empty).Trim().ToUpperInvariant();
            if (u.StartsWith("TONELADA") || u == "TM" || u == "TNE" || u == "TN" || u == "T") return true;
            if (u.StartsWith("VIAJE") || u.StartsWith("UNIDAD") || u.StartsWith("SERVICIO") || u == "UND" || u == "NIU") return false;
            return null;
        }

        /// <summary>
        /// Une las cargas del período con sus facturas de flete y clasifica cada fila
        /// (<see cref="EstadoConciliacion"/>). Las facturas cuya carga está en el sistema pero fuera
        /// del período se dejan fuera (son de otro período); las que no tienen carga en el sistema
        /// y se emitieron en el período salen como <see cref="EstadoConciliacion.SinCarga"/>. Las
        /// cargas de clientes sin ninguna factura de flete registrada no se listan como "por
        /// facturar" (no hay con qué compararlas): se cuentan en <paramref name="clientesSinFacturas"/>.
        /// </summary>
        public static List<FilaConciliacion> Conciliar(IList<PedidoCargado> pedidos, IList<FacturaFleteRegistrada> facturas,
            IDictionary<int, TarifaCliente> tarifas, IDictionary<string, DateTime?> cargaDeComercial,
            DateTime desde, DateTime hasta, out Dictionary<string, int> clientesSinFacturas)
        {
            clientesSinFacturas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var filas = new List<FilaConciliacion>();
            var porComercial = facturas.Where(f => f.FacturaComercial != null)
                .GroupBy(f => f.FacturaComercial, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var usadas = new HashSet<int>();

            foreach (var p in pedidos)
            {
                var fila = new FilaConciliacion { Pedido = p };
                if (p.FacturaComercial != null && porComercial.TryGetValue(p.FacturaComercial, out var suyas))
                {
                    fila.Facturas.AddRange(suyas);
                    suyas.ForEach(x => usadas.Add(x.IdFacturaFlete));
                    CompararFacturado(fila);
                    filas.Add(fila);
                    continue;
                }

                tarifas.TryGetValue(p.IdCliente, out var tarifa);
                if (tarifa == null || tarifa.FacturasRegistradas == 0)
                {
                    clientesSinFacturas.TryGetValue(p.Cliente ?? "", out int n);
                    clientesSinFacturas[p.Cliente ?? ""] = n + p.Unidades;
                    continue;
                }

                if (p.FacturaComercial == null || p.UnidadesSinCarga == p.Unidades)
                {
                    fila.Estado = EstadoConciliacion.SinDocumentos;
                    fila.Avisos.Add(p.FacturaComercial == null
                        ? "Todavía no se registraron los documentos del correo (ZIP): sin factura comercial no se puede facturar el flete."
                        : "Sus despachos no tienen la carga leída de las guías.");
                }
                else
                {
                    fila.Estado = EstadoConciliacion.PorFacturar;
                    bool? porTon = FacturaPorToneladas(tarifa.UnidadMedida);
                    fila.Unidad = porTon == false ? "unid." : "t";
                    fila.CantidadCargada = porTon == false ? p.Unidades : p.ToneladasNetas;
                    if (tarifa.ValorUnitario.HasValue && porTon.HasValue)
                    {
                        fila.ImporteEstimado = Math.Round(fila.CantidadCargada.Value * tarifa.ValorUnitario.Value, 2);
                        fila.Moneda = tarifa.Moneda;
                    }
                    if (p.UnidadesSinCarga > 0)
                        fila.Avisos.Add($"{p.UnidadesSinCarga} unidad(es) sin la carga leída de su guía.");
                }
                filas.Add(fila);
            }

            foreach (var f in facturas.Where(x => !usadas.Contains(x.IdFacturaFlete)))
            {
                DateTime? carga = null;
                bool enSistema = f.FacturaComercial != null && cargaDeComercial != null
                                 && cargaDeComercial.TryGetValue(f.FacturaComercial, out carga) && carga.HasValue;
                if (enSistema) continue;                                        // carga de otro período
                if (f.FechaEmision.Date < desde.Date || f.FechaEmision.Date > hasta.Date) continue;

                var fila = new FilaConciliacion { Estado = EstadoConciliacion.SinCarga };
                fila.Facturas.Add(f);
                fila.ImporteFacturado = f.ImporteTotal;
                fila.Moneda = f.Moneda;
                fila.CantidadFacturada = f.Cantidad;
                fila.Unidad = FacturaPorToneladas(f.UnidadMedida) == false ? "unid." : "t";
                fila.Avisos.Add(f.FacturaComercial == null
                    ? "La factura no menciona la factura comercial: no se sabe a qué pedido corresponde."
                    : $"La factura comercial {f.FacturaComercial} no está en el sistema: falta registrar los documentos (ZIP) de ese pedido.");
                AvisosDeFactura(fila, f);
                filas.Add(fila);
            }

            return filas.OrderBy(x => x.Fecha).ThenBy(x => x.Pedido?.NumeroPedido).ToList();
        }

        private static void CompararFacturado(FilaConciliacion fila)
        {
            var p = fila.Pedido;
            fila.ImporteFacturado = fila.Facturas.Sum(x => x.ImporteTotal);
            fila.Moneda = fila.Facturas[0].Moneda;
            fila.Estado = EstadoConciliacion.Conforme;

            if (fila.Facturas.Count > 1)
                fila.Avisos.Add($"El pedido tiene {fila.Facturas.Count} facturas de flete ({string.Join(", ", fila.Facturas.Select(x => x.NumeroFactura))}): revise que no esté facturado dos veces.");
            foreach (var f in fila.Facturas) AvisosDeFactura(fila, f);

            var unidades = fila.Facturas.Select(x => FacturaPorToneladas(x.UnidadMedida)).Distinct().ToList();
            if (unidades.Count != 1 || unidades[0] == null || fila.Facturas.Any(x => !x.Cantidad.HasValue))
            {
                fila.Avisos.Add("No se pudo comparar la cantidad facturada con la cargada (unidad de medida no reconocida).");
                return;
            }

            bool porTon = unidades[0].Value;
            fila.Unidad = porTon ? "t" : "unid.";
            fila.CantidadFacturada = fila.Facturas.Sum(x => x.Cantidad.Value);
            fila.CantidadCargada = porTon ? p.ToneladasNetas : p.Unidades;
            fila.Diferencia = fila.CantidadFacturada - fila.CantidadCargada;

            decimal tolerancia = porTon ? ToleranciaToneladas : 0m;
            if (p.UnidadesSinCarga > 0)
                fila.Avisos.Add($"{p.UnidadesSinCarga} unidad(es) del pedido sin la carga leída de su guía.");
            if (Math.Abs(fila.Diferencia.Value) <= tolerancia) return;

            fila.Estado = EstadoConciliacion.Diferencia;
            string unidad = fila.Unidad;
            string texto = $"Se facturaron {Num(fila.CantidadFacturada.Value)} {unidad} y en el sistema hay {Num(fila.CantidadCargada.Value)} {unidad}";
            if (porTon && p.Unidades > 0 && p.ToneladasNetas > 0)
            {
                decimal porUnidad = p.ToneladasNetas / p.Unidades;
                decimal equivalentes = Math.Round(fila.Diferencia.Value / porUnidad, 1);
                texto += $" ({(equivalentes > 0 ? "≈ " + Num(equivalentes, 1) + " unidad(es) facturada(s) de más o que faltan en el sistema" : "≈ " + Num(-equivalentes, 1) + " unidad(es) cargada(s) sin facturar")})";
            }
            fila.Avisos.Insert(0, texto + ".");
        }

        private static void AvisosDeFactura(FilaConciliacion fila, FacturaFleteRegistrada f)
        {
            if (f.MontoNetoPendiente.HasValue && f.MontoNetoPendiente.Value != f.ImporteTotal)
                fila.Avisos.Add($"{f.NumeroFactura}: el monto pendiente del crédito ({Num(f.MontoNetoPendiente.Value)}) no coincide con el importe total ({Num(f.ImporteTotal)}).");
        }

        private static string Num(decimal v, int decimales = 2) => v.ToString("N" + decimales, Pe);

        /// <summary>Texto corto del estado para la grilla y el Excel.</summary>
        public static string TextoEstado(EstadoConciliacion e)
        {
            switch (e)
            {
                case EstadoConciliacion.Conforme: return "Conforme";
                case EstadoConciliacion.Diferencia: return "Diferencia";
                case EstadoConciliacion.PorFacturar: return "Por facturar";
                case EstadoConciliacion.SinDocumentos: return "Sin documentos";
                case EstadoConciliacion.SinCarga: return "Factura sin carga en el sistema";
                default: return e.ToString();
            }
        }

        /// <summary>Días hasta el vencimiento (negativo = vencida); null si no tiene.</summary>
        public static int? DiasParaVencer(FacturaFleteRegistrada f, DateTime hoy) =>
            f.FechaVencimiento.HasValue ? (int)(f.FechaVencimiento.Value.Date - hoy.Date).TotalDays : (int?)null;

        // ── Excel ────────────────────────────────────────────────────────────

        /// <summary>Hoja "Conciliación" (una fila por pedido o factura) y hoja "Facturas" (las emitidas en el período).</summary>
        public static Reportes.ReporteTabular Reporte(IList<FilaConciliacion> filas, IList<FacturaFleteRegistrada> facturas,
            DateTime desde, DateTime hasta, string nombreCliente, string usuario, DateTime hoy)
        {
            var dt = new DataTable();
            foreach (var c in new[] { "Estado", "FechaCarga", "Cliente", "Pedido", "FacturaComercial", "Unidades", "Viajes",
                                      "FacturaFlete", "Emision", "Vence", "Unidad", "Facturado", "Cargado", "Diferencia",
                                      "ImporteUsd", "ImportePen", "EstimadoUsd", "EstimadoPen", "Avisos" })
                dt.Columns.Add(c, typeof(object));
            foreach (var x in filas)
            {
                bool usd = x.Moneda != "PEN";
                dt.Rows.Add(TextoEstado(x.Estado), x.Pedido?.FechaCarga, x.Pedido?.Cliente ?? x.Facturas.FirstOrDefault()?.Cliente,
                    x.Pedido?.NumeroPedido, x.Pedido?.FacturaComercial ?? x.Facturas.FirstOrDefault()?.FacturaComercial,
                    x.Pedido?.Unidades, x.Pedido?.ViajesSeguimiento,
                    string.Join(", ", x.Facturas.Select(f => f.NumeroFactura)),
                    x.Facturas.Count > 0 ? (object)x.Facturas[0].FechaEmision : null,
                    x.Facturas.Count > 0 ? (object)x.Facturas[0].FechaVencimiento : null,
                    x.Unidad, x.CantidadFacturada, x.CantidadCargada, x.Diferencia,
                    usd ? x.ImporteFacturado : null, usd ? null : x.ImporteFacturado,
                    usd ? x.ImporteEstimado : null, usd ? null : x.ImporteEstimado,
                    string.Join(" ", x.Avisos));
            }

            var r = new Reportes.ReporteTabular
            {
                Titulo = "Conciliación de facturas de flete",
                NombreHoja = "Conciliación",
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario,
                IncluirTotales = true,
                Nota = "Cargado = peso neto de las guías del pedido (o unidades, si la factura es por viaje). Estimado = lo cargado por la " +
                       "tarifa de la última factura de flete del cliente. Las facturas se unen a su pedido por la factura comercial que mencionan."
            };
            r.Filtros.Add($"Cargas del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}");
            if (!string.IsNullOrWhiteSpace(nombreCliente)) r.Filtros.Add("Cliente: " + nombreCliente);

            var T = Reportes.TipoColumnaReporte.Texto; var C = Reportes.TipoColumnaReporte.Codigo;
            var F = Reportes.TipoColumnaReporte.Fecha; var E = Reportes.TipoColumnaReporte.Entero;
            var D = Reportes.TipoColumnaReporte.Decimal;
            r.Columnas.Add(Col("Estado", C, 1.1f, "Estado", d => Convert.ToString(d["Estado"]) != "Conforme"));
            r.Columnas.Add(Col("Carga", F, 0.8f, "FechaCarga"));
            r.Columnas.Add(Col("Cliente", T, 1.1f, "Cliente"));
            r.Columnas.Add(Col("Pedido", C, 1f, "Pedido"));
            r.Columnas.Add(Col("Factura comercial", C, 1.1f, "FacturaComercial"));
            r.Columnas.Add(Col("Unid.", E, 0.5f, "Unidades"));
            r.Columnas.Add(Col("Viajes", E, 0.5f, "Viajes"));
            r.Columnas.Add(Col("Factura de flete", C, 1f, "FacturaFlete"));
            r.Columnas.Add(Col("Emisión", F, 0.8f, "Emision"));
            r.Columnas.Add(Col("Vence", F, 0.8f, "Vence"));
            r.Columnas.Add(Col("En", C, 0.4f, "Unidad"));
            r.Columnas.Add(Col("Facturado", D, 0.8f, "Facturado"));
            r.Columnas.Add(Col("Cargado", D, 0.8f, "Cargado"));
            r.Columnas.Add(Col("Diferencia", D, 0.8f, "Diferencia", d => d["Diferencia"] != null && d["Diferencia"] != DBNull.Value && Convert.ToDecimal(d["Diferencia"]) != 0));
            if (filas.Any(x => x.Moneda != "PEN"))
            {
                r.Columnas.Add(Col("Facturado US$", Reportes.TipoColumnaReporte.Dolares, 1f, "ImporteUsd"));
                r.Columnas.Add(Col("Por facturar US$ (est.)", Reportes.TipoColumnaReporte.Dolares, 1f, "EstimadoUsd"));
            }
            if (filas.Any(x => x.Moneda == "PEN"))
            {
                r.Columnas.Add(Col("Facturado S/", Reportes.TipoColumnaReporte.Soles, 1f, "ImportePen"));
                r.Columnas.Add(Col("Por facturar S/ (est.)", Reportes.TipoColumnaReporte.Soles, 1f, "EstimadoPen"));
            }
            r.Columnas.Add(Col("Avisos", T, 3f, "Avisos"));

            foreach (EstadoConciliacion e in Enum.GetValues(typeof(EstadoConciliacion)))
            {
                int n = filas.Count(x => x.Estado == e);
                if (n > 0) r.Resumen.Add(new Reportes.LineaResumen { Etiqueta = TextoEstado(e), Valor = n, Tipo = Reportes.TipoColumnaReporte.Entero });
            }

            var hf = new DataTable();
            foreach (var c in new[] { "Numero", "Emision", "Vence", "Dias", "Cliente", "OS", "FacturaComercial", "CPIC", "Cantidad", "Unidad", "Tarifa", "Usd", "Pen", "Neto" })
                hf.Columns.Add(c, typeof(object));
            foreach (var f in facturas.Where(x => x.FechaEmision.Date >= desde.Date && x.FechaEmision.Date <= hasta.Date))
                hf.Rows.Add(f.NumeroFactura, f.FechaEmision, f.FechaVencimiento, DiasParaVencer(f, hoy), f.Cliente, f.OrdenServicio,
                    f.FacturaComercial, f.NumeroCPIC, f.Cantidad, f.UnidadMedida, f.ValorUnitario,
                    f.Moneda == "PEN" ? null : (object)f.ImporteTotal, f.Moneda == "PEN" ? (object)f.ImporteTotal : null, f.MontoNetoPendiente);
            var hoja = new Reportes.ReporteTabular
            {
                Titulo = "Facturas de flete emitidas", NombreHoja = "Facturas", Filas = hf.Rows.Cast<DataRow>().ToList(), GeneradoPor = usuario,
                Nota = "Días = días que faltan para el vencimiento (negativo: vencida). El sistema no registra todavía los pagos."
            };
            hoja.Filtros.Add($"Emitidas del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}");
            hoja.Columnas.Add(Col("Factura", C, 1f, "Numero"));
            hoja.Columnas.Add(Col("Emisión", F, 0.8f, "Emision"));
            hoja.Columnas.Add(Col("Vence", F, 0.8f, "Vence"));
            hoja.Columnas.Add(Col("Días", E, 0.5f, "Dias", d => d["Dias"] != null && d["Dias"] != DBNull.Value && Convert.ToInt32(d["Dias"]) < 0));
            hoja.Columnas.Add(Col("Cliente", T, 1.2f, "Cliente"));
            hoja.Columnas.Add(Col("OS", C, 1f, "OS"));
            hoja.Columnas.Add(Col("Factura comercial", C, 1.1f, "FacturaComercial"));
            hoja.Columnas.Add(Col("CPIC", C, 0.7f, "CPIC"));
            hoja.Columnas.Add(Col("Cantidad", D, 0.8f, "Cantidad"));
            hoja.Columnas.Add(Col("Unidad", C, 0.9f, "Unidad"));
            hoja.Columnas.Add(Col("Tarifa", D, 0.6f, "Tarifa"));
            hoja.Columnas.Add(Col("Importe US$", Reportes.TipoColumnaReporte.Dolares, 1f, "Usd"));
            hoja.Columnas.Add(Col("Importe S/", Reportes.TipoColumnaReporte.Soles, 1f, "Pen"));
            hoja.Columnas.Add(Col("Neto pendiente", D, 0.9f, "Neto"));
            r.HojasAdicionales.Add(hoja);
            return r;
        }

        private static Reportes.ColumnaReporte Col(string titulo, Reportes.TipoColumnaReporte tipo, float ancho, string campo,
                                                   Func<DataRow, bool> alerta = null) =>
            new Reportes.ColumnaReporte
            {
                Titulo = titulo, Tipo = tipo, Ancho = ancho, Alerta = alerta,
                Valor = d => d[campo] == DBNull.Value ? null : d[campo]
            };
    }
}
