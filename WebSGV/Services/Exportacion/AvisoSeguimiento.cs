using System;
using System.Collections.Generic;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Avisos de RegistroSeguimiento y SeguimientoViaje tras redirigir por GET (B6-8): el querystring lleva
    /// solo un código fijo (<c>?msg=cerrado</c>) y el texto sale de aquí. Antes la página mostraba como aviso
    /// verde cualquier texto de <c>?msg=</c>, así que un enlace armado por otro podía mostrar mensajes falsos.
    /// Un código desconocido no muestra nada.
    /// </summary>
    public static class AvisoSeguimiento
    {
        public const string Creado = "creado";
        public const string DatosGuardados = "datos";
        public const string HoraCorregida = "hora";
        public const string ManualesGuardados = "manuales";
        public const string Cerrado = "cerrado";
        public const string Reabierto = "reabierto";

        private static readonly Dictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Creado, "Viaje creado. Usa \"Actualizar GPS\" para traer sus horas." },
            { DatosGuardados, "Datos del viaje guardados." },
            { ManualesGuardados, "Datos guardados." },
            { Cerrado, "Viaje cerrado." },
            { Reabierto, "Viaje reabierto." },
        };

        /// <summary>
        /// Texto del aviso para <paramref name="codigo"/>, o null si no es uno de los códigos fijos.
        /// <see cref="HoraCorregida"/> nombra el hito de <paramref name="columna"/> solo si es un hito conocido.
        /// </summary>
        public static string Texto(string codigo, string columna = null)
        {
            if (string.IsNullOrEmpty(codigo)) return null;
            if (codigo == HoraCorregida)
                return $"{HitosSeguimiento.Buscar(columna)?.Nombre ?? "Hito"}: hora corregida.";
            return Textos.TryGetValue(codigo, out string texto) ? texto : null;
        }
    }
}
