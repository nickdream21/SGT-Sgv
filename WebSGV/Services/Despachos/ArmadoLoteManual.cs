using System;
using System.Collections.Generic;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// "Registrar un lote a mano" de RegistroDespacho (E6-26), sin BD ni controles: qué documentos pide cada operación,
    /// la validación de la configuración base y el armado del lote. El control (ConfiguracionBaseLote.ascx) lee sus
    /// campos en un <see cref="DatosConfiguracionBase"/>, consulta los duplicados y guarda los adjuntos; los mensajes y su
    /// orden son los de antes.
    /// </summary>
    internal static class ArmadoLoteManual
    {
        /// <summary>Lo escrito en la configuración base, tal como está en la página.</summary>
        public class DatosConfiguracionBase
        {
            public string FechaDespacho { get; set; }
            public string IdCliente { get; set; }
            public string NombreCliente { get; set; }
            /// <summary>El panel del N° de pedido se ve (carga nacional).</summary>
            public bool PedidoVisible { get; set; }
            public string NumeroPedido { get; set; }
            public string TipoOperacion { get; set; }
            public bool EsInternacional { get; set; }
            public string IdPlanta { get; set; }
            public string NombrePlanta { get; set; }

            /// <summary>El panel de la factura se ve.</summary>
            public bool FacturaVisible { get; set; }
            public string NumeroFactura { get; set; }
            public string FechaEmisionFactura { get; set; }
            public string ValorTotalFactura { get; set; }
            public string MonedaFactura { get; set; }
            /// <summary>Por qué no sirve el documento de factura adjunto (ArchivoSubidoHelper); null o vacío si no hay o está bien.</summary>
            public string ErrorArchivoFactura { get; set; }

            /// <summary>El panel de la CPIC se ve.</summary>
            public bool CpicVisible { get; set; }
            public string NumeroCpic { get; set; }
            public string FechaEmisionCpic { get; set; }
            public string ValorFlete { get; set; }
            /// <summary>Por qué no sirve el documento de CPIC adjunto; null o vacío si no hay o está bien.</summary>
            public string ErrorArchivoCpic { get; set; }
        }

        /// <summary>Los documentos de la configuración base según la operación y el ámbito.</summary>
        public class DocumentosRequeridos
        {
            public bool Factura { get; set; }
            public bool Cpic { get; set; }
            /// <summary>Descarga internacional: la CPIC puede llegar después (sus validadores se apagan).</summary>
            public bool CpicOpcional { get; set; }
            public string Ayuda { get; set; }
        }

        /// <summary>Números que se consultan contra la BD (null: no se consulta ese documento).</summary>
        public class ConsultaDuplicados
        {
            public string NumeroFactura { get; set; }
            public string NumeroCpic { get; set; }
            public int? IdCliente { get; set; }
        }

        // ── Documentos por operación ────────────────────────────────────────────────────────

        /// <summary>Paneles de factura y CPIC, CPIC opcional y el texto de ayuda para <paramref name="tipoOperacion"/>.</summary>
        public static DocumentosRequeridos Documentos(string tipoOperacion, bool esInternacional)
        {
            var d = new DocumentosRequeridos { Ayuda = "" };
            if (string.IsNullOrEmpty(tipoOperacion))
                d.Ayuda = "Seleccione el tipo de operación y ámbito para ver los documentos requeridos.";
            else if (esInternacional && tipoOperacion == "CARGA")
            {
                d.Factura = d.Cpic = true;
                d.Ayuda = "Internacional CARGA: Requiere N° Factura + CPIC (se reutilizarán para todos los conductores)";
            }
            else if (esInternacional && tipoOperacion == "DESCARGA")
            {
                d.Cpic = true;
                d.Ayuda = "Internacional DESCARGA: CPIC opcional (se reutilizará para todos los conductores). Si aún no lo tiene, agréguelo después al lote en Lista de despachos.";
            }
            else if (!esInternacional && tipoOperacion == "CARGA")
            {
                d.Factura = true;
                d.Ayuda = "Nacional CARGA: Requiere N° Factura (se reutilizará para todos los conductores)";
            }
            else if (!esInternacional && tipoOperacion == "DESCARGA")
                d.Ayuda = "Nacional DESCARGA: Solo se requieren guías específicas por conductor";

            // En la descarga el CPIC puede llegar después que la programación: no se exige
            // (se agrega al lote desde ListaDespachos). Si se escribe el número, se piden fecha y flete.
            d.CpicOpcional = esInternacional && tipoOperacion == "DESCARGA";
            return d;
        }

        // ── Validación ──────────────────────────────────────────────────────────────────────

        /// <summary>La factura y la CPIC que se consultan (las de los paneles a la vista y con número).</summary>
        public static ConsultaDuplicados NumerosAConsultar(DatosConfiguracionBase d) => new ConsultaDuplicados
        {
            NumeroFactura = LlevaFactura(d) ? d.NumeroFactura.Trim() : null,
            NumeroCpic = LlevaCpic(d) ? d.NumeroCpic.Trim() : null,
            // La factura es única por número y cliente (otro cliente puede usar el mismo número).
            IdCliente = int.TryParse(d.IdCliente, out int cli) && cli > 0 ? cli : (int?)null,
        };

        /// <summary>El error de los duplicados (factura y CPIC ya registradas); null si no hay.</summary>
        public static string TextoDuplicados(ConsultaDuplicados c, bool facturaExiste, bool cpicExiste)
        {
            var errores = new List<string>();
            if (c.NumeroFactura != null && facturaExiste)
                errores.Add($"El número de factura '{c.NumeroFactura}' ya está registrado en el sistema para este cliente");
            if (c.NumeroCpic != null && cpicExiste)
                errores.Add($"El número de CPIC '{c.NumeroCpic}' ya está registrado en el sistema");
            return errores.Count > 0 ? string.Join(". ", errores) : null;
        }

        /// <summary>
        /// El aviso de la configuración base ("Errores de validación: …."), o null si está bien. <paramref name="errorDuplicados"/>
        /// es el de <see cref="TextoDuplicados"/> (la consulta se hace siempre, antes).
        /// </summary>
        public static string Validar(DatosConfiguracionBase d, DateTime hoy, string errorDuplicados)
        {
            var errores = new List<string>();

            if (!DespachoValidaciones.TryParseFechaIso(d.FechaDespacho, out _))
                errores.Add("La fecha de programación no tiene un formato válido");

            // Validaciones adicionales - solo si el campo es visible
            if (d.PedidoVisible && !string.IsNullOrEmpty(d.NumeroPedido) && !DespachoValidaciones.ValidarNumeroPedido(d.NumeroPedido))
                errores.Add("El número de pedido debe tener exactamente 10 dígitos");

            if (d.FacturaVisible)
                ValidarFechaEmision(d.FechaEmisionFactura, hoy, "factura", errores);
            if (d.CpicVisible)
                ValidarFechaEmision(d.FechaEmisionCpic, hoy, "CPIC", errores);

            // Documento adjunto (opcional) de factura/CPIC
            if (d.FacturaVisible && !string.IsNullOrEmpty(d.ErrorArchivoFactura))
                errores.Add("Documento de factura: " + d.ErrorArchivoFactura);
            if (d.CpicVisible && !string.IsNullOrEmpty(d.ErrorArchivoCpic))
                errores.Add("Documento de CPIC: " + d.ErrorArchivoCpic);

            if (!string.IsNullOrEmpty(errorDuplicados))
                errores.Add(errorDuplicados);

            ValidarRequeridos(d, errores);

            return errores.Count > 0 ? "Errores de validación: " + string.Join(". ", errores) + "." : null;
        }

        private static void ValidarFechaEmision(string valor, DateTime hoy, string documento, List<string> errores)
        {
            if (string.IsNullOrWhiteSpace(valor)) return;
            if (!DespachoValidaciones.TryParseFechaIso(valor, out DateTime fecha))
                errores.Add($"La fecha de emisión de {documento} no tiene un formato válido");
            else if (fecha.Date > hoy)
                errores.Add($"La fecha de emisión de {documento} no puede ser futura");
        }

        /// <summary>La documentación que exige cada operación.</summary>
        private static void ValidarRequeridos(DatosConfiguracionBase d, List<string> errores)
        {
            string tipoOperacion = d.TipoOperacion;
            if (d.EsInternacional && tipoOperacion == "CARGA")
            {
                if (string.IsNullOrEmpty(d.NumeroFactura))
                    errores.Add("Número de factura requerido para carga internacional");
                if (string.IsNullOrEmpty(d.NumeroCpic))
                    errores.Add("Número de CPIC requerido para carga internacional");
            }
            else if (d.EsInternacional && tipoOperacion == "DESCARGA")
            {
                // CPIC opcional (puede llegar después): si se escribe, con su fecha y su flete
                if (!string.IsNullOrWhiteSpace(d.NumeroCpic))
                {
                    if (string.IsNullOrWhiteSpace(d.FechaEmisionCpic))
                        errores.Add("Con N° de CPIC, indique su fecha de emisión");
                    if (string.IsNullOrWhiteSpace(d.ValorFlete))
                        errores.Add("Con N° de CPIC, indique el valor del flete");
                }
            }
            else if (!d.EsInternacional && tipoOperacion == "CARGA")
            {
                if (string.IsNullOrEmpty(d.NumeroFactura))
                    errores.Add("Número de factura requerido para carga nacional");
            }
        }

        // ── Armado ──────────────────────────────────────────────────────────────────────────

        /// <summary>El lote lleva la factura: su panel se ve y tiene número.</summary>
        public static bool LlevaFactura(DatosConfiguracionBase d) => d.FacturaVisible && !string.IsNullOrEmpty(d.NumeroFactura);

        /// <summary>El lote lleva la CPIC: su panel se ve y tiene número.</summary>
        public static bool LlevaCpic(DatosConfiguracionBase d) => d.CpicVisible && !string.IsNullOrEmpty(d.NumeroCpic);

        /// <summary>
        /// El lote con su documentación base (sin los adjuntos: los guarda el control después), o null con el aviso en
        /// <paramref name="error"/>. Un cliente que no es número lanza (como antes: lo atrapa "Iniciar lote").
        /// </summary>
        public static LoteDespachos Armar(DatosConfiguracionBase d, string usuario, out string error)
        {
            error = null;
            if (!DespachoValidaciones.TryParseFechaIso(d.FechaDespacho, out DateTime fechaProgramacion))
            {
                error = "La fecha de programación no tiene un formato válido.";
                return null;
            }

            // El RequiredFieldValidator ya cubre el caso normal; esta guarda evita una
            // excepción de formato si el catálogo de plantas no llegó a cargar.
            if (!int.TryParse(d.IdPlanta, out int idPlanta) || idPlanta <= 0)
            {
                error = "Debe seleccionar una planta de operación válida.";
                return null;
            }

            var lote = new LoteDespachos
            {
                FechaProgramacion = fechaProgramacion.ToString("yyyy-MM-dd"),
                IdCliente = Convert.ToInt32(d.IdCliente),
                NombreCliente = d.NombreCliente,
                NumeroPedido = d.PedidoVisible ? (d.NumeroPedido ?? string.Empty).Trim() : string.Empty,
                TipoOperacion = d.TipoOperacion,
                EsInternacional = d.EsInternacional,
                IdPlanta = idPlanta,
                PlantaOperacion = d.NombrePlanta,
                UsuarioCreacion = usuario
            };

            if (LlevaFactura(d) && !ArmarFactura(d, lote.Documentacion, out error))
                return null;
            if (LlevaCpic(d) && !ArmarCpic(d, lote.Documentacion, out error))
                return null;
            return lote;
        }

        private static bool ArmarFactura(DatosConfiguracionBase d, DocumentacionBase doc, out string error)
        {
            // E5-1: coma o punto decimal, sin separador de miles ni exponente (MontoHelper.LeerMonto).
            if (!LeerMonto(d.ValorTotalFactura, "Valor total de la factura", "El valor total de la factura debe ser mayor a 0.",
                    out decimal valorFactura, out error))
                return false;
            if (!DespachoValidaciones.TryParseFechaIso(d.FechaEmisionFactura, out DateTime fechaFactura))
            {
                error = "La fecha de emisión de factura no tiene un formato válido.";
                return false;
            }
            doc.NumeroFactura = d.NumeroFactura.Trim();
            doc.FechaEmisionFactura = fechaFactura;
            doc.ValorTotalFactura = valorFactura;
            doc.MonedaFactura = d.MonedaFactura == "USD" ? "USD" : "PEN";
            return true;
        }

        private static bool ArmarCpic(DatosConfiguracionBase d, DocumentacionBase doc, out string error)
        {
            if (!LeerMonto(d.ValorFlete, "Valor del flete", "El valor del flete debe ser mayor a 0.", out decimal valorFlete, out error))
                return false;
            if (!DespachoValidaciones.TryParseFechaIso(d.FechaEmisionCpic, out DateTime fechaCpic))
            {
                error = "La fecha de emisión de CPIC no tiene un formato válido.";
                return false;
            }
            doc.NumeroCPIC = d.NumeroCpic.Trim();
            doc.FechaEmisionCPIC = fechaCpic;
            doc.ValorFlete = valorFlete;
            return true;
        }

        /// <summary>Un monto mayor a 0: el aviso de MontoHelper si no se lee, <paramref name="noPositivo"/> si es 0 o menos.</summary>
        private static bool LeerMonto(string texto, string campo, string noPositivo, out decimal valor, out string error)
        {
            error = null;
            try { valor = MontoHelper.LeerMonto(texto, campo); }
            catch (ErrorNegocioException ex)
            {
                valor = 0;
                error = MensajeErrorHelper.ParaUsuario(ex);
                return false;
            }
            if (valor <= 0)
            {
                error = noPositivo;
                return false;
            }
            return true;
        }
    }
}
