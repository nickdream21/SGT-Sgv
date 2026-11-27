using System;
using System.Collections.Generic;
using WebSGV.Services.Common;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Viaje vencido del tablero de seguimiento: sigue abierto (su fase no es "cerrado") y se programó (o se registró, si
    /// no tiene programación) hace más de <see cref="DiasVencido"/> días. Un viaje real dura hasta 9 días (el más largo
    /// cerrado, 8,5; p99 7,7, sgvActualizada 27/11/2026): casi siempre es una descarga que no se registró, de antes del
    /// sistema, con la carreta ya en un viaje posterior. Salen en su propia pestaña y un administrador los cierra con
    /// motivo (sp_SE_CerrarVencido, la misma regla en SQL con dbo.fn_AhoraPeru()).
    /// </summary>
    public static class ViajeVencido
    {
        /// <summary>Días abiertos que se admiten: con más, vencido (el día 10 todavía no, el 11 sí).</summary>
        public const int DiasVencido = 10;

        /// <summary>Lo que dura como máximo un viaje normal (para el aviso).</summary>
        public const int DiasViajeNormal = 9;

        public const string MotivoDescargaNoRegistrada = "Descarga no registrada (antes del sistema)";
        public const string MotivoCarretaEnOtroViaje = "La carreta ya está en otro viaje";
        public const string MotivoOtro = "Otro";

        /// <summary>Largo máximo del motivo (columna SeguimientoExportacion.motivoCierre).</summary>
        public const int LargoMaximoMotivo = 200;

        /// <summary>Motivos que se eligen en la pantalla, en orden; "Otro" pide escribirlo.</summary>
        public static readonly IReadOnlyList<string> Motivos = new[] { MotivoDescargaNoRegistrada, MotivoCarretaEnOtroViaje, MotivoOtro };

        /// <summary>Días desde la programación (o el registro) hasta hoy, por fecha; 0 sin fecha, negativo si es a futuro.</summary>
        public static int DiasAbierto(DateTime? fechaReferencia, DateTime hoy) =>
            fechaReferencia.HasValue ? (hoy.Date - fechaReferencia.Value.Date).Days : 0;

        /// <summary>Abierto (fase distinta de "cerrado") y con más de <see cref="DiasVencido"/> días desde su fecha.</summary>
        public static bool Es(string grupoFase, DateTime? fechaReferencia, DateTime hoy) =>
            !string.Equals(grupoFase, "cerrado", StringComparison.OrdinalIgnoreCase)
            && DiasAbierto(fechaReferencia, hoy) > DiasVencido;

        /// <summary>
        /// Solo se cierran desde el tablero los viajes de los despachos o creados a mano (origen DESPACHO / MANUAL), la misma
        /// condición de sp_SE_CerrarVencido. Los importados del Excel (origen NULL, el historial) son otro conjunto, sin decidir.
        /// </summary>
        public static bool SeCierraDesdeTablero(string origen)
        {
            string o = (origen ?? "").Trim().ToUpperInvariant();
            return o == "DESPACHO" || o == "MANUAL";
        }

        /// <summary>Aviso de la fila: cuántos días lleva abierto y, si la carreta ya tiene otro viaje, cuál.</summary>
        public static string Aviso(int diasAbierto, string carreta, string pedidoPosterior, int? idPosterior, DateTime? fechaPosterior, DateTime ahora)
        {
            string texto = $"Vencido: abierto hace {diasAbierto} días (un viaje normal dura hasta {DiasViajeNormal}).";
            if (idPosterior.HasValue && fechaPosterior.HasValue && !string.IsNullOrWhiteSpace(carreta))
            {
                string viaje = string.IsNullOrWhiteSpace(pedidoPosterior) ? "#" + idPosterior.Value : pedidoPosterior.Trim();
                texto += fechaPosterior.Value <= ahora
                    ? $" La carreta {carreta} ya salió en el viaje {viaje} del {fechaPosterior.Value:dd/MM}."
                    : $" La carreta {carreta} ya está programada en el viaje {viaje} del {fechaPosterior.Value:dd/MM}.";
            }
            return texto;
        }

        /// <summary>
        /// "Cerrado administrativamente el dd/MM/yyyy: motivo" para el detalle del viaje; null si no está FINALIZADO o no se
        /// cerró como vencido (un viaje reabierto conserva el motivo en la BD, pero ya no se muestra).
        /// </summary>
        public static string TextoCierre(string estado, string motivo, DateTime? fecha)
        {
            if (!string.Equals((estado ?? "").Trim(), "FINALIZADO", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(motivo))
                return null;
            return "Cerrado administrativamente" + (fecha.HasValue ? $" el {fecha.Value:dd/MM/yyyy}" : "") + ": " + motivo.Trim();
        }

        /// <summary>
        /// El motivo que se guarda: uno de <see cref="Motivos"/> o, con "Otro", el texto escrito (obligatorio, hasta
        /// <see cref="LargoMaximoMotivo"/>). Un motivo que no está en la lista se rechaza.
        /// </summary>
        public static string Motivo(string opcion, string otro)
        {
            string o = (opcion ?? "").Trim();
            if (o.Length == 0) throw new ErrorNegocioException("Elige el motivo del cierre.");
            if (string.Equals(o, MotivoOtro, StringComparison.Ordinal))
            {
                string texto = (otro ?? "").Trim();
                if (texto.Length == 0) throw new ErrorNegocioException("Escribe el motivo del cierre.");
                if (texto.Length > LargoMaximoMotivo)
                    throw new ErrorNegocioException($"El motivo admite hasta {LargoMaximoMotivo} caracteres.");
                return texto;
            }
            foreach (var m in Motivos)
                if (string.Equals(o, m, StringComparison.Ordinal)) return m;
            throw new ErrorNegocioException("Ese motivo no está en la lista.");
        }
    }
}
