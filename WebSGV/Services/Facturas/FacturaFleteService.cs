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
using Newtonsoft.Json;

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
                DbHelper.Param("@usuario", usuario),
                DbHelper.Param("@items", ItemsJson(f.Items)));

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

        /// <summary>Líneas para sp_FF_RegistrarFacturaFlete (@items).</summary>
        public static string ItemsJson(IEnumerable<ItemFacturaFlete> items) =>
            JsonConvert.SerializeObject((items ?? Enumerable.Empty<ItemFacturaFlete>()).Select(i => new
            {
                orden = i.Orden, cantidad = i.Cantidad, unidadMedida = Recortar(i.UnidadMedida, 30), valorUnitario = i.ValorUnitario,
                descripcion = Recortar(i.Descripcion, 600), facturaComercial = Recortar(i.FacturaComercial, 30), cpic = Recortar(i.Cpic, 20),
                manifiestos = Recortar(i.Manifiestos, 200), guiaRemitente = Recortar(i.GuiaRemitente, 30),
                guiaTransportista = Recortar(i.GuiaTransportista, 30)
            }));

        private class ItemJson
        {
            public int orden { get; set; }
            public decimal? cantidad { get; set; }
            public string unidadMedida { get; set; }
            public decimal? valorUnitario { get; set; }
            public string descripcion { get; set; }
            public string facturaComercial { get; set; }
            public string cpic { get; set; }
            public string manifiestos { get; set; }
            public string guiaRemitente { get; set; }
            public string guiaTransportista { get; set; }
            public DateTime? fechaCargaSistema { get; set; }
        }

        private static List<ItemFacturaFlete> LeerItemsJson(object valor)
        {
            if (valor == null || valor == DBNull.Value) return new List<ItemFacturaFlete>();
            return (JsonConvert.DeserializeObject<List<ItemJson>>(Convert.ToString(valor)) ?? new List<ItemJson>())
                .Select(i => new ItemFacturaFlete
                {
                    Orden = i.orden, Cantidad = i.cantidad, UnidadMedida = i.unidadMedida, ValorUnitario = i.valorUnitario,
                    Descripcion = i.descripcion, FacturaComercial = i.facturaComercial, Cpic = i.cpic, Manifiestos = i.manifiestos,
                    GuiaRemitente = i.guiaRemitente, GuiaTransportista = i.guiaTransportista, FechaCargaSistema = i.fechaCargaSistema
                }).ToList();
        }

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
                    GuiaRemitente = r["guiaRemitente"] == DBNull.Value ? null : Convert.ToString(r["guiaRemitente"]).Trim(),
                    GuiaTransportista = r["guiaTransportista"] == DBNull.Value ? null : Convert.ToString(r["guiaTransportista"]).Trim(),
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
                    RucCliente = r["rucCliente"] == DBNull.Value ? null : Convert.ToString(r["rucCliente"]).Trim(),
                    Moneda = Convert.ToString(r["moneda"]),
                    OrdenServicio = r["ordenServicio"] == DBNull.Value ? null : Convert.ToString(r["ordenServicio"]),
                    Cantidad = r["cantidad"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["cantidad"]),
                    UnidadMedida = r["unidadMedida"] == DBNull.Value ? null : Convert.ToString(r["unidadMedida"]),
                    ValorUnitario = r["valorUnitario"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["valorUnitario"]),
                    ValorVenta = r["valorVenta"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["valorVenta"]),
                    ImporteTotal = Convert.ToDecimal(r["importeTotal"]),
                    MontoNetoPendiente = r["montoNetoPendiente"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["montoNetoPendiente"]),
                    FacturaComercial = r["numeroFacturaComercial"] == DBNull.Value ? null : Convert.ToString(r["numeroFacturaComercial"]),
                    NumeroCPIC = r["numeroCPIC"] == DBNull.Value ? null : Convert.ToString(r["numeroCPIC"]),
                    RutaArchivo = r["rutaArchivo"] == DBNull.Value ? null : Convert.ToString(r["rutaArchivo"]),
                    Descripcion = r["descripcion"] == DBNull.Value ? null : Convert.ToString(r["descripcion"]),
                    Items = LeerItemsJson(r["items"])
                };
                lista.Add(f);
                if (f.FacturaComercial != null)
                    cargaDeComercial[f.FacturaComercial] = r["fechaCargaSistema"] == DBNull.Value
                        ? (DateTime?)null : Convert.ToDateTime(r["fechaCargaSistema"]);
                foreach (var i in f.Items.Where(x => x.Referencia != null))
                    cargaDeComercial[i.Referencia] = i.FechaCargaSistema;
            }
            return lista;
        }

        /// <summary>
        /// Líneas de la factura para conciliar: las guardadas (FacturaFleteItem) o, si no tiene (factura registrada
        /// antes de la migración 39 o armada en una prueba), la cabecera como una sola línea.
        /// </summary>
        public static List<ItemFacturaFlete> Lineas(FacturaFleteRegistrada f)
        {
            if (f.Items != null && f.Items.Count > 0) return f.Items;
            return new List<ItemFacturaFlete>
            {
                new ItemFacturaFlete { Orden = 1, Cantidad = f.Cantidad, UnidadMedida = f.UnidadMedida, ValorUnitario = f.ValorUnitario,
                                       FacturaComercial = f.FacturaComercial, Cpic = f.NumeroCPIC, Descripcion = f.Descripcion }
            };
        }

        /// <summary>
        /// Parte del importe total de la factura que corresponde a una de sus líneas: todo si es la única (o todas son de
        /// la misma carga); si no, en proporción a su valor de venta (cantidad × valor unitario).
        /// </summary>
        public static decimal ImporteDeLinea(FacturaFleteRegistrada f, ItemFacturaFlete linea)
        {
            var lineas = Lineas(f);
            if (lineas.Count <= 1) return f.ImporteTotal;
            decimal total = lineas.Sum(x => x.ValorVenta ?? 0m);
            if (total <= 0m || !linea.ValorVenta.HasValue) return Math.Round(f.ImporteTotal / lineas.Count, 2);
            return Math.Round(f.ImporteTotal * linea.ValorVenta.Value / total, 2);
        }

        /// <summary>Las cargas que cobra la factura (facturas comerciales o guías), sin repetir.</summary>
        public static List<string> Referencias(FacturaFleteRegistrada f) =>
            Lineas(f).Select(x => x.Referencia).Where(x => x != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>Las C.P.I que menciona la factura, sin repetir.</summary>
        public static List<string> Cpics(FacturaFleteRegistrada f) =>
            Lineas(f).Select(x => x.Cpic).Where(x => x != null).Distinct().ToList();

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
        /// Une las cargas del período con las líneas de sus facturas de flete y clasifica cada fila
        /// (<see cref="EstadoConciliacion"/>). Cada línea se une a su carga por su referencia (factura comercial o,
        /// en las nacionales, guía del remitente): una factura puede cobrar varias cargas (Novopan del Ecuador) y una
        /// carga puede cobrarse en varias facturas a clientes distintos, una por tramo (Novopan del Ecuador y Novopan
        /// Perú); la cantidad se compara por cliente que paga. Las líneas cuya carga está en el sistema pero fuera del
        /// período se dejan fuera (son de otro período); las que no tienen carga en el sistema y se emitieron en el
        /// período salen como <see cref="EstadoConciliacion.SinCarga"/>. Las cargas de clientes sin ninguna factura de
        /// flete registrada no se listan como "por facturar" (no hay con qué compararlas): se cuentan en
        /// <paramref name="clientesSinFacturas"/>.
        /// </summary>
        public static List<FilaConciliacion> Conciliar(IList<PedidoCargado> pedidos, IList<FacturaFleteRegistrada> facturas,
            IDictionary<int, TarifaCliente> tarifas, IDictionary<string, DateTime?> cargaDeComercial,
            DateTime desde, DateTime hasta, out Dictionary<string, int> clientesSinFacturas)
        {
            clientesSinFacturas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var filas = new List<FilaConciliacion>();
            var porReferencia = new Dictionary<string, List<LineaFacturada>>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in facturas)
                foreach (var l in Lineas(f).Where(x => x.Referencia != null))
                {
                    if (!porReferencia.TryGetValue(l.Referencia, out var lista)) porReferencia[l.Referencia] = lista = new List<LineaFacturada>();
                    lista.Add(new LineaFacturada(f, l));
                }
            var usadas = new HashSet<LineaFacturada>();

            // Nuestra guía de transportista: por si la factura cita mal la guía del remitente (E001-18693 dice
            // "T700-0000651" por la T700-00001651 de su GRE EG03-00015200).
            var porGuiaNuestra = facturas.SelectMany(f => Lineas(f).Where(l => l.GuiaTransportista != null).Select(l => new LineaFacturada(f, l)))
                .GroupBy(x => x.Linea.GuiaTransportista, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var p in pedidos)
            {
                var fila = new FilaConciliacion { Pedido = p, Referencia = p.Referencia };
                if (p.Referencia != null && porReferencia.TryGetValue(p.Referencia, out var suyas))
                {
                    fila.Facturas.AddRange(suyas.Select(x => x.Factura).Distinct());
                    suyas.ForEach(x => usadas.Add(x));
                    CompararFacturado(fila, suyas);
                    filas.Add(fila);
                    continue;
                }
                if (p.FacturaComercial == null && p.GuiaTransportista != null && porGuiaNuestra.TryGetValue(p.GuiaTransportista, out var porGuia)
                    && (porGuia = porGuia.Where(x => !usadas.Contains(x)).ToList()).Count > 0)
                {
                    fila.Facturas.AddRange(porGuia.Select(x => x.Factura).Distinct());
                    porGuia.ForEach(x => usadas.Add(x));
                    CompararFacturado(fila, porGuia);
                    fila.Avisos.Insert(0, $"{string.Join(", ", porGuia.Select(x => x.Factura.NumeroFactura).Distinct())} cita la guía del remitente " +
                        $"{string.Join(", ", porGuia.Select(x => x.Linea.GuiaRemitente ?? "(ninguna)").Distinct())}, pero la carga tiene la {p.GuiaRemitente}: " +
                        $"se unió por nuestra guía de transportista {p.GuiaTransportista}. Corrija la factura o confírmelo.");
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

                bool? porTon = FacturaPorToneladas(tarifa.UnidadMedida);
                if (p.Referencia == null || (porTon != false && p.UnidadesSinCarga == p.Unidades))
                {
                    fila.Estado = EstadoConciliacion.SinDocumentos;
                    fila.Avisos.Add(p.Referencia == null
                        ? "Todavía no se registraron sus documentos (el ZIP del correo, o la CPIC y los MCI, o la guía del remitente): sin la factura comercial o la guía no se puede facturar el flete."
                        : "Sus despachos no tienen la carga leída de las guías.");
                }
                else
                {
                    fila.Estado = EstadoConciliacion.PorFacturar;
                    fila.Unidad = porTon == false ? "unid." : "t";
                    fila.CantidadCargada = porTon == false ? p.Unidades : p.ToneladasNetas;
                    if (tarifa.ValorUnitario.HasValue && porTon.HasValue)
                    {
                        fila.ImporteEstimado = Math.Round(fila.CantidadCargada.Value * tarifa.ValorUnitario.Value, 2);
                        fila.Moneda = tarifa.Moneda;
                    }
                    if (porTon != false && p.UnidadesSinCarga > 0)
                        fila.Avisos.Add($"{p.UnidadesSinCarga} unidad(es) sin la carga leída de su guía.");
                }
                filas.Add(fila);
            }

            foreach (var f in facturas)
            {
                bool enPeriodo = f.FechaEmision.Date >= desde.Date && f.FechaEmision.Date <= hasta.Date;
                var libres = Lineas(f).Where(l => !usadas.Contains(new LineaFacturada(f, l))).ToList();
                if (libres.Count == 0 || !enPeriodo) continue;

                foreach (var l in libres.Where(x => x.Referencia != null))
                {
                    if (cargaDeComercial != null && cargaDeComercial.TryGetValue(l.Referencia, out DateTime? carga) && carga.HasValue)
                        continue;                                                       // carga de otro período
                    var fila = FilaSinCarga(f, l, ImporteDeLinea(f, l));
                    fila.Avisos.Add(l.FacturaComercial != null
                        ? $"La factura comercial {l.FacturaComercial} no está en el sistema: falta registrar los documentos de esa carga (el ZIP del pedido, o la CPIC y sus MCI)."
                        : $"La guía del remitente {l.GuiaRemitente} no está en ningún despacho: falta registrar esa carga o su guía.");
                    AvisosDeFactura(fila, f);
                    filas.Add(fila);
                }

                var sinReferencia = libres.Where(x => x.Referencia == null).ToList();
                if (sinReferencia.Count > 0)
                {
                    bool todas = sinReferencia.Count == Lineas(f).Count;
                    var fila = FilaSinCarga(f, sinReferencia.Count == 1 ? sinReferencia[0] : null,
                        todas ? f.ImporteTotal : sinReferencia.Sum(x => ImporteDeLinea(f, x)));
                    if (sinReferencia.Count > 1) { fila.CantidadFacturada = null; fila.Unidad = null; }
                    fila.Avisos.Add(todas
                        ? "La factura no menciona la factura comercial ni la guía del remitente: no se sabe a qué carga corresponde."
                        : $"{sinReferencia.Count} línea(s) de la factura no mencionan la factura comercial ni la guía del remitente.");
                    var cpics = sinReferencia.Select(x => x.Cpic).Where(x => x != null).Distinct().ToList();
                    if (cpics.Count > 0) fila.Avisos.Add($"Menciona la C.P.I {string.Join(", ", cpics)}.");
                    AvisosDeFactura(fila, f);
                    filas.Add(fila);
                }
            }

            return filas.OrderBy(x => x.Fecha).ThenBy(x => x.Pedido?.NumeroPedido).ThenBy(x => x.Referencia).ToList();
        }

        /// <summary>Una línea de una factura de flete (para unirla con su carga).</summary>
        private sealed class LineaFacturada : IEquatable<LineaFacturada>
        {
            public LineaFacturada(FacturaFleteRegistrada f, ItemFacturaFlete l) { Factura = f; Linea = l; }
            public FacturaFleteRegistrada Factura { get; }
            public ItemFacturaFlete Linea { get; }
            public bool Equals(LineaFacturada o) => o != null && ReferenceEquals(o.Factura, Factura) && o.Linea.Orden == Linea.Orden;
            public override bool Equals(object o) => Equals(o as LineaFacturada);
            public override int GetHashCode() => (Factura.IdFacturaFlete * 397) ^ Linea.Orden;
        }

        private static FilaConciliacion FilaSinCarga(FacturaFleteRegistrada f, ItemFacturaFlete l, decimal importe)
        {
            var fila = new FilaConciliacion { Estado = EstadoConciliacion.SinCarga, Referencia = l?.Referencia };
            fila.Facturas.Add(f);
            fila.ImporteFacturado = importe;
            fila.Moneda = f.Moneda;
            fila.CantidadFacturada = l?.Cantidad ?? f.Cantidad;
            fila.Unidad = FacturaPorToneladas(l?.UnidadMedida ?? f.UnidadMedida) == false ? "unid." : "t";
            return fila;
        }

        private static void CompararFacturado(FilaConciliacion fila, List<LineaFacturada> lineas)
        {
            var p = fila.Pedido;
            fila.ImporteFacturado = lineas.Sum(x => ImporteDeLinea(x.Factura, x.Linea));
            fila.Moneda = lineas[0].Factura.Moneda;
            fila.Estado = EstadoConciliacion.Conforme;
            if (lineas.Select(x => x.Factura.Moneda).Distinct().Count() > 1)
                fila.Avisos.Add("Las facturas de la carga están en monedas distintas: el importe suma ambas.");

            // Un grupo por cliente que paga: varios clientes = varios tramos (Novopan del Ecuador y Novopan Perú).
            var tramos = lineas.GroupBy(x => x.Factura.Pagador).ToList();
            foreach (var t in tramos)
            {
                var suyas = t.Select(x => x.Factura).Distinct().ToList();
                if (suyas.Count > 1)
                    fila.Avisos.Add($"El pedido tiene {suyas.Count} facturas de flete ({string.Join(", ", suyas.Select(x => x.NumeroFactura))}): revise que no esté facturado dos veces.");
            }
            foreach (var f in fila.Facturas) AvisosDeFactura(fila, f);
            if (tramos.Count > 1)
                fila.Avisos.Add("Facturado por tramos: " + string.Join("; ", tramos.Select(t =>
                    $"{t.First().Factura.Cliente} ({string.Join(", ", t.Select(x => x.Factura.NumeroFactura).Distinct())})")) + ".");

            if (p.UnidadesSinCarga > 0 && lineas.Any(x => FacturaPorToneladas(x.Linea.UnidadMedida) == true))
                fila.Avisos.Add($"{p.UnidadesSinCarga} unidad(es) del pedido sin la carga leída de su guía.");

            bool primero = true;
            foreach (var t in tramos)
            {
                var unidades = t.Select(x => FacturaPorToneladas(x.Linea.UnidadMedida)).Distinct().ToList();
                if (unidades.Count != 1 || unidades[0] == null || t.Any(x => !x.Linea.Cantidad.HasValue))
                {
                    fila.Avisos.Add("No se pudo comparar la cantidad facturada con la cargada (unidad de medida no reconocida).");
                    return;
                }
                bool porTon = unidades[0].Value;
                decimal facturado = t.Sum(x => x.Linea.Cantidad.Value);
                decimal cargado = porTon ? p.ToneladasNetas : p.Unidades;
                decimal diferencia = facturado - cargado;
                bool cuadra = Math.Abs(diferencia) <= (porTon ? ToleranciaToneladas : 0m);

                if (primero || !cuadra)
                {
                    fila.Unidad = porTon ? "t" : "unid.";
                    fila.CantidadFacturada = facturado;
                    fila.CantidadCargada = cargado;
                    fila.Diferencia = diferencia;
                }
                if (!cuadra)
                {
                    bool yaHabiaDiferencia = fila.Estado == EstadoConciliacion.Diferencia;
                    fila.Estado = EstadoConciliacion.Diferencia;
                    string unidad = porTon ? "t" : "unid.";
                    string texto = (tramos.Count > 1 ? t.First().Factura.Cliente + ": s" : "S") +
                                   $"e facturaron {Num(facturado)} {unidad} y en el sistema hay {Num(cargado)} {unidad}";
                    if (porTon && p.Unidades > 0 && p.ToneladasNetas > 0)
                    {
                        decimal porUnidad = p.ToneladasNetas / p.Unidades;
                        decimal equivalentes = Math.Round(diferencia / porUnidad, 1);
                        texto += $" ({(equivalentes > 0 ? "≈ " + Num(equivalentes, 1) + " unidad(es) facturada(s) de más o que faltan en el sistema" : "≈ " + Num(-equivalentes, 1) + " unidad(es) cargada(s) sin facturar")})";
                    }
                    fila.Avisos.Insert(yaHabiaDiferencia ? 1 : 0, texto + ".");
                }
                primero = false;
            }
        }

        private static void AvisosDeFactura(FilaConciliacion fila, FacturaFleteRegistrada f)
        {
            if (NetoNoCuadra(f))
                fila.Avisos.Add($"{f.NumeroFactura}: el monto pendiente del crédito ({Num(f.MontoNetoPendiente.Value)}) no coincide con el importe total ({Num(f.ImporteTotal)}).");
        }

        /// <summary>Tasas de detracción del SPOT que se descuentan del crédito (transporte de carga 4 %, otros servicios 10 y 12 %).</summary>
        private static readonly decimal[] TasasDetraccion = { 0.04m, 0.10m, 0.12m };

        /// <summary>
        /// El monto neto pendiente del crédito no es el importe total ni el total menos la detracción (las de terceros
        /// llevan la del 4 % del transporte de carga y su neto es el 96 %): algo no cuadra (E001-18645: 0,60 de menos).
        /// </summary>
        public static bool NetoNoCuadra(FacturaFleteRegistrada f)
        {
            if (!f.MontoNetoPendiente.HasValue || f.MontoNetoPendiente.Value == f.ImporteTotal || f.ImporteTotal <= 0m) return false;
            decimal descontado = (f.ImporteTotal - f.MontoNetoPendiente.Value) / f.ImporteTotal;
            return !TasasDetraccion.Any(t => Math.Abs(descontado - t) <= 0.001m);
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
                    x.Pedido?.NumeroPedido, x.Referencia,
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
                       "tarifa de la última factura de flete del cliente. Las facturas se unen a su carga por la factura comercial (o, en las nacionales, la guía del remitente) que menciona cada línea."
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
            r.Columnas.Add(Col("Factura comercial / guía", C, 1.1f, "FacturaComercial"));
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
                    string.Join(", ", Referencias(f)), string.Join(", ", Cpics(f)), f.Cantidad, f.UnidadMedida, f.ValorUnitario,
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
            hoja.Columnas.Add(Col("Factura comercial / guía", C, 1.4f, "FacturaComercial"));
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
