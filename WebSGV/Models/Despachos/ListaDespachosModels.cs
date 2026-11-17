using System;
using System.Collections.Generic;

namespace WebSGV.Models.Despachos
{
    /// <summary>Viaje en progreso activo mostrado en la lista (grilla de viajes).</summary>
    [Serializable]
    public class ViajeActivo
    {
        public int IdViajeProgreso { get; set; }
        public string NumeroViajeProgreso { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductor { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaUltimaActividad { get; set; }
        public int CantidadDespachos { get; set; }
        public bool EsInternacional { get; set; }
        public string EstadoViaje { get; set; }
        public string DescripcionViaje { get; set; }
    }

    /// <summary>Despacho de un viaje/lote, con los ids necesarios para la Orden de Viaje.</summary>
    [Serializable]
    public class DespachoViaje
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public DateTime FechaDespacho { get; set; }

        // Información básica
        public string NombreCliente { get; set; }
        public string NombreConductor { get; set; }
        public string PlacaTracto { get; set; }
        public string PlacaCarreta { get; set; }
        public string TipoOperacion { get; set; }
        public string LugarOperacion { get; set; }
        public string EstadoDespacho { get; set; }
        public string GuiaRemitente { get; set; }
        public string GuiaTransportista { get; set; }
        public string NumeroViaje { get; set; }

        // ✅ NUEVOS: IDs necesarios para la Orden de Viaje
        public int IdConductor { get; set; }
        public int IdTracto { get; set; }
        public int IdCarreta { get; set; }
        public int IdCliente { get; set; }

        // ✅ NUEVOS: Datos para operaciones internacionales
        public bool EsInternacional { get; set; }
        public string NumeroCPIC { get; set; }
        public int? IdCPIC { get; set; }
    }

    /// <summary>Despacho con su conductor actual (grilla de edición de conductores del lote).</summary>
    [Serializable]
    public class DespachoConConductor
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public DateTime FechaDespacho { get; set; }
        public int IdConductor { get; set; }
        public string NombreConductorActual { get; set; }
    }

    /// <summary>Lote de despachos registrado (agrupación virtual mostrada en la lista de lotes).</summary>
    [Serializable]
    public class LoteRegistrado
    {
        public string IdLoteVirtual { get; set; }
        public DateTime FechaProgramacion { get; set; }
        public int IdCliente { get; set; }
        public string NombreCliente { get; set; }
        public string NumeroPedido { get; set; }
        public string TipoOperacion { get; set; }
        public bool EsInternacional { get; set; }

        /// <summary>FK a <c>Planta</c>: identifica el lote junto al resto de criterios.</summary>
        public int IdPlanta { get; set; }

        /// <summary>Nombre de la planta, para mostrar. Viene del catálogo, no del despacho.</summary>
        public string PlantaOperacion { get; set; }

        public int CantidadDespachos { get; set; }

        /// <summary>
        /// Una de las facturas del lote (el MAX de sp_LD_ObtenerLotesRegistrados). Si <see cref="CantidadFacturas"/> &gt; 1
        /// no representa al lote: los documentos se listan por id (Documentos del lote → "Factura y CPIC por despacho").
        /// </summary>
        public string NumeroFactura { get; set; }
        /// <summary>Una de las CPIC del lote (ver <see cref="NumeroFactura"/>).</summary>
        public string NumeroCPIC { get; set; }

        /// <summary>Facturas distintas de los despachos del lote (0, 1 o más: p. ej. Novopan con dos facturas el mismo día).</summary>
        public int CantidadFacturas { get; set; }
        /// <summary>CPIC distintas de los despachos del lote.</summary>
        public int CantidadCPIC { get; set; }

        /// <summary>
        /// N° de pedido para la grilla. Una carga sin pedido ni factura es la programada con
        /// el Word que espera el correo (RegistroDespacho la completa al leer los documentos).
        /// </summary>
        public string PedidoMostrado =>
            string.IsNullOrWhiteSpace(NumeroPedido) && string.IsNullOrWhiteSpace(NumeroFactura)
                && string.Equals(TipoOperacion, "CARGA", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrEmpty(PerfilDocumentos) || PerfilDocumentos == "PEDIDO")   // los terceros no traen pedido
                ? "Faltan documentos" : NumeroPedido;

        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; }

        public DateTime? FechaEmisionFactura { get; set; }
        public decimal? ValorTotalFactura { get; set; }
        public string MonedaFactura { get; set; }
        public DateTime? FechaEmisionCPIC { get; set; }
        public decimal? ValorFlete { get; set; }
        public List<int> IdsDespachos { get; set; }
        public string EstadoLote { get; set; }

        // Documentos pendientes: cuántos despachos no anulados del lote no tienen cada
        // documento (sp_LD_ObtenerLotesRegistrados; los arma EstadoDocumental).
        public int DespachosVigentes { get; set; }
        public int DespachosSinPedido { get; set; }
        public int DespachosSinGuia { get; set; }
        public int DespachosSinFactura { get; set; }
        public int DespachosSinPdfFactura { get; set; }
        public int DespachosSinCPIC { get; set; }
        public int DespachosSinPdfCPIC { get; set; }
        public int DespachosSinManifiesto { get; set; }

        /// <summary>
        /// Qué documentos se esperan del cliente (sp_LD_ObtenerLotesRegistrados): PEDIDO (ZIP del correo: pedido, factura
        /// con PDF y guías), CPIC (tercero que cruza: factura comercial de la CPIC y la CPIC) o GUIA (tercero nacional:
        /// guía del remitente).
        /// </summary>
        public string PerfilDocumentos { get; set; } = "PEDIDO";

        public LoteRegistrado()
        {
            IdsDespachos = new List<int>();
            NumeroPedido = "";
            NumeroFactura = "";
            NumeroCPIC = "";
            UsuarioCreacion = "";
            EstadoLote = "ACTIVO";
        }
    }

    /// <summary>
    /// Un despacho del lote con su factura y su CPIC (Documentos del lote → "Factura y CPIC por despacho";
    /// <c>ListaDespachosService.ObtenerDocumentosPorDespacho</c>).
    /// </summary>
    [Serializable]
    public class DocumentoDespachoLote
    {
        public int IdDespacho { get; set; }
        public string NumeroDespacho { get; set; }
        public string Chofer { get; set; }
        public string PlacaTracto { get; set; }
        public string PlacaCarreta { get; set; }
        /// <summary>Guía del remitente o, si no tiene, la GRE transportista (vacío si ninguna).</summary>
        public string Guia { get; set; }
        /// <summary>Cuántos manifiestos (MCI de cruce o retorno) tiene adjuntos.</summary>
        public int Manifiestos { get; set; }

        public int? IdFactura { get; set; }
        public string NumeroFactura { get; set; }
        public bool FacturaConPdf { get; set; }

        public int? IdCPIC { get; set; }
        public string NumeroCPIC { get; set; }
        public bool CpicConPdf { get; set; }
    }

    /// <summary>Una factura o CPIC distinta del lote, con su PDF y cuántos despachos la llevan.</summary>
    [Serializable]
    public class DocumentoDelLote
    {
        public const string FACTURA = "FACTURA";
        public const string CPIC = "CPIC";

        /// <summary><see cref="FACTURA"/> o <see cref="CPIC"/>.</summary>
        public string Tipo { get; set; }
        public int Id { get; set; }
        public string Numero { get; set; }
        public bool ConPdf { get; set; }
        public int Despachos { get; set; }

        public string TipoTexto => Tipo == FACTURA ? "Factura" : "CPIC";
        /// <summary>Argumento de los botones Ver/Subir PDF: "FACTURA:12" o "CPIC:34".</summary>
        public string Clave => Tipo + ":" + Id;
    }

    /// <summary>
    /// Entrada para la transacción de edición de lote
    /// (<c>ListaDespachosService.GuardarCambiosLote</c>). El code-behind lee y valida los
    /// controles del formulario de edición; el servicio compara con lo guardado y solo escribe lo que cambia.
    /// </summary>
    public class GuardarCambiosLoteInput
    {
        public List<int> IdsDespachos { get; set; } = new List<int>();
        public DateTime FechaDespacho { get; set; }
        public string NumeroPedido { get; set; }

        /// <summary>Planta de operación del lote (FK a <c>Planta</c>). Es lo que se graba.</summary>
        public int IdPlanta { get; set; }

        public string TipoOperacion { get; set; }
        public bool EsInternacional { get; set; }
        public string UsuarioModificacion { get; set; }
        public DateTime FechaActual { get; set; }

        /// <summary>
        /// idDespacho → conductor elegido en la grilla (todas las filas). Solo se escriben y reubican los que
        /// difieren del conductor guardado (2026-11-17, bloque 4, tarea 12).
        /// </summary>
        public Dictionary<int, int> CambiosConductores { get; set; } = new Dictionary<int, int>();

        // Factura: gestionar (panel visible y con número) o quitar (casilla "Quitar esta factura del lote").
        // Un panel oculto no hace nada. Quitar gana a gestionar (EdicionDocumentosLote.Accion).
        public bool GestionarFactura { get; set; }
        public bool QuitarFactura { get; set; }
        public string NumeroFactura { get; set; }
        public DateTime? FechaEmisionFactura { get; set; }
        public decimal? ValorTotalFactura { get; set; }
        /// <summary>PEN o USD; null deja la que tenía.</summary>
        public string MonedaFactura { get; set; }

        // CPIC: gestionar o quitar.
        public bool GestionarCpic { get; set; }
        public bool QuitarCpic { get; set; }
        public string NumeroCPIC { get; set; }
        public DateTime? FechaEmisionCPIC { get; set; }
        public decimal? ValorFlete { get; set; }
    }

    /// <summary>Documento que tocó una escritura de lote: su id y qué se hizo (CREADA, ENLAZADA, RENOMBRADA…).</summary>
    public class DocumentoGestionado
    {
        public int Id { get; set; }
        public string Accion { get; set; }
    }

    /// <summary>Lo que hizo <c>ListaDespachosService.GuardarCambiosLote</c> (null / vacío = no se tocó).</summary>
    public class ResultadoEdicionLote
    {
        public DocumentoGestionado Factura { get; set; }
        public DocumentoGestionado Cpic { get; set; }

        /// <summary>Ids de las facturas / CPIC quitadas de los despachos del lote (el documento no se borra).</summary>
        public List<int> FacturasQuitadas { get; set; } = new List<int>();
        public List<int> CpicsQuitadas { get; set; } = new List<int>();

        /// <summary>idDespacho → nuevo conductor, solo los que de verdad cambiaron.</summary>
        public Dictionary<int, int> ConductoresCambiados { get; set; } = new Dictionary<int, int>();

        /// <summary>Nada difería de lo guardado: no se escribió nada (ni usuario ni fecha de modificación).</summary>
        public bool SinCambios { get; set; }

        /// <summary>Instantáneas del lote (JSON) antes y después, para la auditoría. Null si <see cref="SinCambios"/>.</summary>
        public string AntesJson { get; set; }
        public string DespuesJson { get; set; }
    }

    /// <summary>Foto de un lote en la BD (despachos y sus documentos), para comparar y auditar la edición.</summary>
    public class InstantaneaLote
    {
        public List<InstantaneaDespacho> Despachos { get; set; } = new List<InstantaneaDespacho>();
        public List<InstantaneaDocumento> Facturas { get; set; } = new List<InstantaneaDocumento>();
        public List<InstantaneaDocumento> Cpics { get; set; } = new List<InstantaneaDocumento>();
    }

    public class InstantaneaDespacho
    {
        public int IdDespacho { get; set; }
        public DateTime? FechaDespacho { get; set; }
        public string NumeroPedido { get; set; }
        public int? IdPlanta { get; set; }
        public string TipoOperacion { get; set; }
        public bool EsInternacional { get; set; }
        public int? IdConductor { get; set; }
        public int? IdViajeProgreso { get; set; }
        public int? IdFactura { get; set; }
        public int? IdCPIC { get; set; }
    }

    /// <summary>Factura (Valor = valor total, con Moneda) o CPIC (Valor = flete, Moneda null).</summary>
    public class InstantaneaDocumento
    {
        public int Id { get; set; }
        public string Numero { get; set; }
        public DateTime? Fecha { get; set; }
        public decimal? Valor { get; set; }
        public string Moneda { get; set; }
    }

    /// <summary>Resultado de <c>sp_LD_AsignarDocumentosDespachos</c> (ids null si no se indicó ese documento).</summary>
    public class ResultadoAsignacionDocumentos
    {
        public int? IdFactura { get; set; }
        public string AccionFactura { get; set; }
        public int? IdCpic { get; set; }
        public string AccionCpic { get; set; }
        public int Despachos { get; set; }
    }
}
