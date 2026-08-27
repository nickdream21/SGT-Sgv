using System;
using System.Globalization;
using System.Linq;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Validaciones puras (sin System.Web ni BD) del módulo de Orden de Viaje.
    /// Extraídas de <c>AgregarOrdenViaje.aspx.cs</c> y <c>DashboardConductor.aspx.cs</c>
    /// (que las compartían duplicadas). El code-behind conserva adaptadores delgados.
    /// </summary>
    public static class OrdenViajeValidaciones
    {
        /// <summary>
        /// Valida el formato del número de orden: exactamente 6 dígitos, no "000000".
        /// Devuelve el mensaje de error, o <see cref="string.Empty"/> si es válido.
        /// </summary>
        public static string ValidarFormatoNumeroOrden(string numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
                return "El número de orden es obligatorio.";

            if (numero.Length != 6)
                return $"El número de orden debe tener exactamente 6 dígitos. Ingresado: {numero.Length} caracteres.";

            if (!numero.All(char.IsDigit))
                return "El número de orden debe contener solo números (0-9).";

            if (numero == "000000")
                return "El número de orden no puede ser '000000'.";

            return string.Empty;
        }

        /// <summary>
        /// Valida que una cadena represente una hora válida en formato H:mm o HH:mm
        /// (00:00–23:59). Acepta con y sin cero inicial para interoperar con distintos
        /// dispositivos. Usa cultura invariante para no depender del locale del servidor.
        /// </summary>
        public static bool ValidarFormatoHora(string hora)
        {
            if (string.IsNullOrWhiteSpace(hora)) return false;
            string[] formatos = { @"h\:mm", @"hh\:mm" };
            if (!TimeSpan.TryParseExact(hora.Trim(), formatos, CultureInfo.InvariantCulture,
                    TimeSpanStyles.None, out TimeSpan ts))
                return false;
            return ts.Hours >= 0 && ts.Hours <= 23 && ts.Minutes >= 0 && ts.Minutes <= 59;
        }

        /// <summary>
        /// Indica si una fecha cae dentro del rango admitido por SQL Server
        /// (1753-01-01 … 9999-12-31) y no es <see cref="DateTime.MinValue"/>.
        /// </summary>
        public static bool EsFechaValidaSQL(DateTime fecha)
        {
            return fecha >= new DateTime(1753, 1, 1)
                && fecha <= new DateTime(9999, 12, 31)
                && fecha != DateTime.MinValue;
        }

        /// <summary>
        /// Valida los datos generales (fechas/horas) al guardar/editar una orden de viaje
        /// desde <c>AgregarOrdenViaje</c>: exige las cuatro (fecha y hora de salida/llegada)
        /// y que la salida no sea posterior a la llegada. Devuelve el mensaje de error
        /// acumulado (líneas separadas por <c>\n</c>), o cadena vacía si todo es válido.
        /// </summary>
        public static string ValidarDatosGeneralesOrdenViaje(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada)
        {
            string mensajeError = "";

            if (fechaSalida == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Salida'.\n";

            if (string.IsNullOrEmpty(horaSalida))
                mensajeError += "Por favor, seleccione una 'Hora de Salida'.\n";

            if (fechaLlegada == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Llegada'.\n";

            if (string.IsNullOrEmpty(horaLlegada))
                mensajeError += "Por favor, seleccione una 'Hora de Llegada'.\n";

            if (fechaSalida != DateTime.MinValue && fechaLlegada != DateTime.MinValue)
            {
                if (fechaSalida > fechaLlegada)
                    mensajeError += "La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.\n";
            }

            return mensajeError;
        }

        /// <summary>
        /// Valida los datos generales al enviar una liquidación desde
        /// <c>DashboardConductor</c>: exige ambas fechas, que la salida no sea posterior a
        /// la llegada, que ninguna supere un año desde hoy y que las horas (si vienen)
        /// tengan formato HH:mm válido. Devuelve el mensaje acumulado o cadena vacía.
        /// </summary>
        public static string ValidarDatosGeneralesLiquidacion(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada, string horaLlegadaDeclarada = null)
        {
            string mensajeError = "";

            // Validar fechas obligatorias
            if (fechaSalida == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Salida'.\n";

            if (fechaLlegada == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Llegada'.\n";

            // La hora de salida la registra el conductor (no existe hora programada): es obligatoria.
            if (string.IsNullOrWhiteSpace(horaSalida))
                mensajeError += "Por favor, ingrese la 'Hora de Salida'.\n";

            // Validar orden lógico de fechas
            if (fechaSalida != DateTime.MinValue && fechaLlegada != DateTime.MinValue)
            {
                if (fechaSalida > fechaLlegada)
                    mensajeError += "La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.\n";

                // M-3: Detectar fechas absurdamente futuras (más de 1 año desde hoy)
                DateTime limiteMaximo = DateTime.Today.AddYears(1);
                if (fechaSalida > limiteMaximo || fechaLlegada > limiteMaximo)
                    mensajeError += "Las fechas no pueden ser superiores a un año desde hoy.\n";
            }

            // M-2: Validar formato de hora (HH:mm, rango 00:00-23:59)
            if (!string.IsNullOrEmpty(horaSalida) && !ValidarFormatoHora(horaSalida))
                mensajeError += "El formato de 'Hora de Salida' es incorrecto. Use HH:MM (ej. 08:30).\n";

            if (!string.IsNullOrEmpty(horaLlegada) && !ValidarFormatoHora(horaLlegada))
                mensajeError += "El formato de 'Hora de Llegada' es incorrecto. Use HH:MM (ej. 18:00).\n";

            // La hora de llegada declarada es autoreportada y opcional: solo se valida el formato si viene.
            if (!string.IsNullOrEmpty(horaLlegadaDeclarada) && !ValidarFormatoHora(horaLlegadaDeclarada))
                mensajeError += "El formato de 'Hora Real de Llegada' es incorrecto. Use HH:MM (ej. 18:00).\n";

            return mensajeError;
        }
    }
}
