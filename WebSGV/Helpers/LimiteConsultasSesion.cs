using System;
using System.Collections.Generic;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Límite de consultas a la API de DNI/RUC por sesión (B5-6): como mucho <see cref="Maximo"/> consultas
    /// reales en cualquier lapso de <see cref="Ventana"/>, con un contador compartido entre DNI y RUC. Las marcas
    /// de tiempo se guardan en la sesión del usuario; el almacén y el reloj son inyectables para las pruebas.
    ///
    /// Sin carreras: ASP.NET serializa las peticiones de una misma sesión con EnableSession = true (bloqueo
    /// exclusivo), así que dos consultas del mismo usuario no leen y escriben la lista a la vez.
    /// </summary>
    public sealed class LimiteConsultasSesion
    {
        public const int Maximo = 30;
        public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(10);
        public const string ClaveSesion = "ConsultasApiDocumento";
        public const string MensajeExcedido =
            "Demasiadas consultas seguidas; espere unos minutos o complete los datos a mano.";

        private readonly Func<object> _leer;
        private readonly Action<object> _guardar;
        private readonly Func<DateTime> _relojUtc;
        private readonly int _maximo;
        private readonly TimeSpan _ventana;

        public LimiteConsultasSesion(Func<object> leer, Action<object> guardar, Func<DateTime> relojUtc,
            int maximo = Maximo, TimeSpan? ventana = null)
        {
            _leer = leer ?? throw new ArgumentNullException(nameof(leer));
            _guardar = guardar ?? throw new ArgumentNullException(nameof(guardar));
            _relojUtc = relojUtc ?? throw new ArgumentNullException(nameof(relojUtc));
            if (maximo < 1) throw new ArgumentOutOfRangeException(nameof(maximo));
            _maximo = maximo;
            _ventana = ventana ?? Ventana;
            if (_ventana <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ventana));
        }

        /// <summary>Límite sobre la sesión de la petición actual (null si no hay sesión: entonces no se permite).</summary>
        public static LimiteConsultasSesion DeSesionActual()
        {
            var sesion = HttpContext.Current?.Session;
            if (sesion == null) return null;
            return new LimiteConsultasSesion(() => sesion[ClaveSesion], v => sesion[ClaveSesion] = v, () => DateTime.UtcNow);
        }

        /// <summary>
        /// True y registra la consulta si no se llegó al máximo dentro de la ventana; false sin registrar nada.
        /// Llamarlo solo justo antes de la consulta real (las entradas inválidas o duplicadas no cuentan).
        /// </summary>
        public bool Permitir()
        {
            DateTime ahora = _relojUtc();
            var marcas = new List<DateTime>();
            if (_leer() is DateTime[] guardadas)
                foreach (DateTime m in guardadas)
                    if (ahora - m < _ventana && m <= ahora) marcas.Add(m);   // descarta las vencidas (y las "del futuro")

            if (marcas.Count >= _maximo)
            {
                _guardar(marcas.ToArray());
                return false;
            }

            marcas.Add(ahora);
            _guardar(marcas.ToArray());
            return true;
        }
    }
}
