using System;
using System.Collections.Generic;
using System.Web;
using System.Web.SessionState;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// Lo que RegistroDespacho y sus controles guardan en la sesión entre postbacks (E6-23): el lote en curso, la
    /// importación del ZIP, la programación del Word y la descarga por pedido. Una sola puerta tipada en vez de
    /// <c>Session["..."]</c> repartido por la página.
    /// <para>
    /// Los nombres de las claves son los de antes y no deben cambiar: una vista previa abierta (o un lote a medio
    /// armar) tiene que sobrevivir a una publicación, y la sesión de quien está trabajando guarda estas claves.
    /// Los fija <c>EstadoRegistroDespachoTests</c>.
    /// </para>
    /// </summary>
    public sealed class EstadoRegistroDespacho
    {
        public const string ClaveLote = "LoteActual";
        public const string ClaveImportacion = "ImportacionPedido";
        public const string ClaveConductoresImportados = "ImportacionConductores";
        public const string ClaveProgramacion = "ProgramacionNueva";
        public const string ClaveCargasDescarga = "DescargaCargas";
        public const string ClaveConductoresDescarga = "DescargaConductores";
        public const string ClavePropuestasDescarga = "DescargaPropuestas";

        /// <summary>Las 7 claves, en el orden de arriba.</summary>
        public static readonly IReadOnlyList<string> Claves = new[]
        {
            ClaveLote, ClaveImportacion, ClaveConductoresImportados, ClaveProgramacion,
            ClaveCargasDescarga, ClaveConductoresDescarga, ClavePropuestasDescarga,
        };

        private readonly HttpSessionStateBase _sesion;

        public EstadoRegistroDespacho(HttpSessionStateBase sesion)
        {
            _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        }

        /// <summary>El estado sobre la sesión de la petición.</summary>
        public static EstadoRegistroDespacho De(HttpSessionState sesion) => new EstadoRegistroDespacho(new HttpSessionStateWrapper(sesion));

        /// <summary>Lote que se está armando ("Iniciar lote" → "Finalizar lote").</summary>
        public LoteDespachos Lote
        {
            get => _sesion[ClaveLote] as LoteDespachos;
            set => _sesion[ClaveLote] = value;
        }

        /// <summary>Vista previa de "Leer documentos" con el ZIP o los PDF del correo.</summary>
        public ImportacionPedido Importacion
        {
            get => _sesion[ClaveImportacion] as ImportacionPedido;
            set => _sesion[ClaveImportacion] = value;
        }

        /// <summary>Conductores de la importación aplicada ("Usar estos datos"), que entran al "Iniciar lote".</summary>
        public List<ConductorLote> ConductoresImportados
        {
            get => _sesion[ClaveConductoresImportados] as List<ConductorLote>;
            set => _sesion[ClaveConductoresImportados] = value;
        }

        /// <summary>Vista previa de "Programar con el Word" (la sección elegida y sus filas).</summary>
        public ProgramacionNueva Programacion
        {
            get => _sesion[ClaveProgramacion] as ProgramacionNueva;
            set => _sesion[ClaveProgramacion] = value;
        }

        /// <summary>Cargas del pedido sin descarga ("Ver tractos por descargar").</summary>
        public List<DescargaPedidoService.CargaPendiente> CargasDescarga
        {
            get => _sesion[ClaveCargasDescarga] as List<DescargaPedidoService.CargaPendiente>;
            set => _sesion[ClaveCargasDescarga] = value;
        }

        /// <summary>Conductores de la descarga aplicada ("Usar en el lote"), que entran al "Iniciar lote".</summary>
        public List<ConductorLote> ConductoresDescarga
        {
            get => _sesion[ClaveConductoresDescarga] as List<ConductorLote>;
            set => _sesion[ClaveConductoresDescarga] = value;
        }

        /// <summary>Lo que propuso la programación en Word para cada carga de la descarga (por IdDespacho de la carga).</summary>
        public Dictionary<int, PropuestaDescarga> PropuestasDescarga
        {
            get => _sesion[ClavePropuestasDescarga] as Dictionary<int, PropuestaDescarga>;
            set => _sesion[ClavePropuestasDescarga] = value;
        }
    }
}
