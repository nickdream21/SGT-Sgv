using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Cruza la programación en Word de la descarga ("GUAYAQUIL-VITAPRO" + N° de pedido)
    /// con las cargas pendientes del pedido. La carreta es la que lleva el producto, así
    /// que es la llave: cada fila se une a la carga que salió con esa carreta. CHOFER y
    /// TRACTO son los de la descarga; CAMBIO es el chofer al que sacan (debe ser el que
    /// cargó) y vacío quiere decir que descarga el mismo que cargó.
    ///
    /// Lógica pura (sin BD) para poder probarla; los catálogos llegan como listas.
    /// </summary>
    public static class ProgramacionDescarga
    {
        /// <summary>
        /// Sección del pedido: la que lleva su número; si ninguna lo lleva, la que comparte
        /// más carretas con las cargas pendientes. Null si no hay ninguna relacionada.
        /// </summary>
        public static SeccionProgramacion SeccionDelPedido(ProgramacionTransporte programacion,
            string numeroPedido, IEnumerable<string> carretasPendientes)
        {
            var porNumero = programacion.Secciones.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.NumeroPedido) && s.NumeroPedido == (numeroPedido ?? "").Trim());
            if (porNumero != null) return porNumero;

            var carretas = new HashSet<string>(carretasPendientes
                .Select(LectorDocumentosPedido.NormalizarPlaca).Where(p => p != null));
            return programacion.Secciones
                .Select(s => new { s, comunes = s.Filas.Count(f => carretas.Contains(LectorDocumentosPedido.NormalizarPlaca(f.Carreta) ?? "")) })
                .Where(x => x.comunes > 0)
                .OrderByDescending(x => x.comunes)
                .Select(x => x.s)
                .FirstOrDefault();
        }

        /// <summary>
        /// Propuesta para cada carga (por IdDespacho de la carga). <paramref name="avisosGenerales"/>
        /// recibe las filas de la programación que no corresponden a ninguna carga pendiente.
        /// </summary>
        public static Dictionary<int, PropuestaDescarga> Proponer(
            IList<DescargaPedidoService.CargaPendiente> cargas, SeccionProgramacion seccion,
            IList<KeyValuePair<int, string>> conductores, IList<KeyValuePair<int, string>> tractos,
            out List<string> avisosGenerales)
        {
            avisosGenerales = new List<string>();
            var propuestas = new Dictionary<int, PropuestaDescarga>();

            var filasPorCarreta = seccion.Filas
                .Where(f => LectorDocumentosPedido.NormalizarPlaca(f.Carreta) != null)
                .GroupBy(f => LectorDocumentosPedido.NormalizarPlaca(f.Carreta))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var carga in cargas)
            {
                var p = new PropuestaDescarga();
                propuestas[carga.IdDespacho] = p;

                string carreta = LectorDocumentosPedido.NormalizarPlaca(carga.PlacaCarreta) ?? "";
                if (!filasPorCarreta.TryGetValue(carreta, out var filas))
                {
                    p.Avisos.Add($"Su carreta {carga.PlacaCarreta} no figura en la programación: se dejó sin marcar.");
                    continue;
                }

                var fila = filas[0];
                if (filas.Count > 1)
                    p.Avisos.Add($"La carreta {carga.PlacaCarreta} aparece {filas.Count} veces en la programación: se tomó la primera fila.");

                p.EnProgramacion = true;
                p.ChoferProgramado = fila.Chofer;
                p.TractoProgramado = fila.Tracto;
                p.Cambio = fila.Cambio;

                // Tracto de la descarga, por placa
                if (!string.IsNullOrEmpty(fila.Tracto))
                {
                    string placa = LectorDocumentosPedido.NormalizarPlaca(fila.Tracto);
                    var tracto = tractos.Where(t => LectorDocumentosPedido.NormalizarPlaca(t.Value) == placa).ToList();
                    if (tracto.Count == 1) p.IdTracto = tracto[0].Key;
                    else p.Avisos.Add($"El tracto {fila.Tracto} no está registrado (o no está activo).");
                }
                else p.Avisos.Add("La programación no trae el tracto de esta carreta.");

                // Conductor de la descarga, por nombre (el Word trae nombre y apellido cortos)
                if (!string.IsNullOrEmpty(fila.Chofer))
                {
                    // Primero palabra por palabra exacta; la tolerancia solo si nadie coincide
                    // exacto ("JULIO MENA" no debe traer a "TULIO MERA").
                    var candidatos = conductores.Where(c => NombreCoincide(fila.Chofer, c.Value, tolerante: false)).ToList();
                    if (candidatos.Count == 0)
                        candidatos = conductores.Where(c => NombreCoincide(fila.Chofer, c.Value)).ToList();
                    if (candidatos.Count > 1 && candidatos.Any(c => c.Key == carga.IdConductor))
                        candidatos = candidatos.Where(c => c.Key == carga.IdConductor).ToList();

                    if (candidatos.Count == 1) p.IdConductor = candidatos[0].Key;
                    else if (candidatos.Count == 0) p.Avisos.Add($"{fila.Chofer} no está registrado como conductor: elíjalo o regístrelo.");
                    else p.Avisos.Add($"Hay {candidatos.Count} conductores que coinciden con {fila.Chofer}: elija el correcto.");
                }
                else p.Avisos.Add("La programación no trae el chofer de esta carreta.");

                // Comprobación con quien cargó
                if (string.IsNullOrEmpty(fila.Cambio))
                {
                    if (!string.IsNullOrEmpty(fila.Chofer) && !NombreCoincide(fila.Chofer, carga.NombreConductor))
                        p.Avisos.Add($"La programación no indica cambio (descarga el mismo que cargó), pero esta carga la registró {carga.NombreConductor}, no {fila.Chofer}.");
                }
                else if (!NombreCoincide(fila.Cambio, carga.NombreConductor))
                {
                    p.Avisos.Add($"La programación saca a {fila.Cambio}, pero esta carreta la cargó {carga.NombreConductor}.");
                }
            }

            var carretasPendientes = new HashSet<string>(cargas.Select(c => LectorDocumentosPedido.NormalizarPlaca(c.PlacaCarreta) ?? ""));
            // Un solo aviso: al descargar el último tracto de un pedido, las demás filas son
            // las ya descargadas y salían nueve avisos iguales.
            var ajenas = seccion.Filas
                .Where(f => !carretasPendientes.Contains(LectorDocumentosPedido.NormalizarPlaca(f.Carreta) ?? ""))
                .Select(f => $"{f.Carreta ?? "(sin placa)"} ({f.Chofer})").ToList();
            if (ajenas.Count == 1)
                avisosGenerales.Add($"La programación incluye la carreta {ajenas[0]}, que no está entre las cargas pendientes de este pedido.");
            else if (ajenas.Count > 1)
                avisosGenerales.Add($"La programación incluye {ajenas.Count} carretas que no están entre las cargas pendientes de este pedido " +
                                    $"(ya descargadas o de otro pedido): {string.Join(", ", ajenas)}.");

            return propuestas;
        }

        /// <summary>
        /// El nombre corto del Word ("ROGER PAZ", "MARK TORRES") corresponde al nombre
        /// completo: cada palabra del corto coincide con una palabra distinta del completo,
        /// sin importar el orden. Con <paramref name="tolerante"/>, admite una letra de
        /// diferencia o un nombre cortado.
        /// </summary>
        public static bool NombreCoincide(string corto, string completo, bool tolerante = true)
        {
            var buscadas = Palabras(corto);
            var disponibles = Palabras(completo);
            if (buscadas.Count == 0 || disponibles.Count == 0) return false;

            foreach (var palabra in buscadas)
            {
                int i = disponibles.FindIndex(d => d == palabra);
                if (i < 0 && tolerante) i = disponibles.FindIndex(d => Parecidas(palabra, d));
                if (i < 0) return false;
                disponibles.RemoveAt(i);
            }
            return true;
        }

        private static List<string> Palabras(string nombre) =>
            Regex.Split(LectorDocumentosPedido.Normalizar((nombre ?? "").Split(new[] { " - " }, StringSplitOptions.None)[0]), @"[^A-Z]+")
                .Where(p => p.Length >= 2)
                .ToList();

        private static bool Parecidas(string a, string b)
        {
            if (a.Length < 4 || b.Length < 4) return false;
            if (a.StartsWith(b) || b.StartsWith(a)) return true;   // nombre cortado ("ROBE")
            return Math.Abs(a.Length - b.Length) <= 1 && Distancia(a, b) <= 1;
        }

        private static int Distancia(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }
    }
}
