using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Pone al día el catálogo de conductores con la "lista de choferes actuales" que lleva
    /// transporte (CHOFER · NACIONALIDAD · LICENCIA · DNI). La lista se compara con lo
    /// registrado y se proponen cambios que el usuario revisa antes de aplicarlos:
    /// registrar a los que faltan, completar licencias y nombres, reactivar a los que vuelven
    /// y desactivar a los que ya no trabajan (siguen en el historial, solo dejan de salir en
    /// los formularios).
    ///
    /// El DNI es la llave. Un DNI que en el sistema es de otra persona, o que se repite en
    /// la lista, no se toca: se muestra como problema para corregirlo a mano.
    /// La lectura y la comparación son lógica pura (se prueban sin BD).
    /// </summary>
    public static class ListaChoferesService
    {
        public class FilaLista
        {
            public int Fila { get; set; }
            public string Nombre { get; set; }
            public string Nacionalidad { get; set; }
            public string Licencia { get; set; }
            /// <summary>DNI de 8 dígitos; null si la fila no trae uno o es de un extranjero.</summary>
            public string Dni { get; set; }
            /// <summary>Carnet de extranjería (9 dígitos o nacionalidad no peruana).</summary>
            public string Carnet { get; set; }
            public string DocumentoOriginal { get; set; }
            public List<string> Avisos { get; } = new List<string>();

            /// <summary>Llave de búsqueda: "D" + DNI o "C" + carnet.</summary>
            public string Clave => Dni != null ? "D" + Dni : Carnet != null ? "C" + Carnet : null;
            public string Documento => Dni ?? (Carnet != null ? "CE " + Carnet : null);
        }

        public class ConductorRegistrado
        {
            public int Id { get; set; }
            public string Dni { get; set; }
            public string Nombre { get; set; }
            public string ApPaterno { get; set; }
            public string ApMaterno { get; set; }
            public string Carnet { get; set; }
            public string Licencia { get; set; }
            public bool Activo { get; set; }
            public bool TieneViajeAbierto { get; set; }

            public IEnumerable<string> Claves
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(Dni)) yield return "D" + Dni.Trim();
                    if (!string.IsNullOrWhiteSpace(Carnet)) yield return "C" + Carnet.Trim().ToUpperInvariant();
                }
            }

            public string Documento => !string.IsNullOrWhiteSpace(Dni) ? Dni.Trim()
                : !string.IsNullOrWhiteSpace(Carnet) ? "CE " + Carnet.Trim() : null;

            public string NombreCompleto => string.Join(" ",
                new[] { Nombre, ApPaterno, ApMaterno }.Select(p => (p ?? "").Trim()).Where(p => p.Length > 0));
        }

        public enum TipoCambio { Registrar, Documento, Licencia, Nombre, Reactivar, Desactivar }

        [Serializable]
        public class CambioChofer
        {
            public string Clave { get; set; }
            public TipoCambio Tipo { get; set; }
            public int? IdConductor { get; set; }
            public string Dni { get; set; }
            public string Carnet { get; set; }
            /// <summary>Documento a mostrar: el DNI o "CE " + carnet.</summary>
            public string Documento { get; set; }
            /// <summary>Nombre como figura en el sistema (o en la lista, si es nuevo).</summary>
            public string NombreMostrado { get; set; }
            public string Nombre { get; set; }
            public string ApPaterno { get; set; }
            public string ApMaterno { get; set; }
            public string Licencia { get; set; }
            /// <summary>Qué cambia, en palabras ("B03620726 → B-03620726").</summary>
            public string Detalle { get; set; }
            public bool MarcadoPorDefecto { get; set; }
        }

        [Serializable]
        public class ProblemaLista
        {
            public int? Fila { get; set; }
            public string Nombre { get; set; }
            public string Mensaje { get; set; }
        }

        [Serializable]
        public class ComparacionLista
        {
            public int FilasLeidas { get; set; }
            public int SinCambios { get; set; }
            public List<CambioChofer> Cambios { get; } = new List<CambioChofer>();
            public List<ProblemaLista> Problemas { get; } = new List<ProblemaLista>();
            /// <summary>Correcciones hechas al leer (DNI con 9 dígitos, etc.).</summary>
            public List<ProblemaLista> Notas { get; } = new List<ProblemaLista>();
        }

        // ── Lectura ──────────────────────────────────────────────────────

        /// <summary>Lee la primera hoja del Excel (.xlsx).</summary>
        public static List<FilaLista> LeerExcel(Stream contenido)
        {
            XLWorkbook libro;
            try { libro = new XLWorkbook(contenido); }
            catch (Exception ex) { throw new ErrorNegocioException("No se pudo abrir el Excel. Use un archivo .xlsx.", ex); }

            using (libro)
            {
                var hoja = libro.Worksheets.First();
                var filas = new List<FilaLista>();
                var usado = hoja.RangeUsed();
                if (usado == null) return filas;
                foreach (var fila in usado.Rows())
                {
                    var celdas = fila.Cells().Select(c => TextoCelda(c)).ToList();
                    var f = InterpretarCeldas(celdas, fila.RowNumber());
                    if (f != null) filas.Add(f);
                }
                return filas;
            }
        }

        private static string TextoCelda(IXLCell celda)
        {
            // Un DNI guardado como número pierde el cero inicial (03620726 → 3620726): se
            // completa en InterpretarCeldas. El formato visible lo conserva si la celda lo tiene.
            string texto = celda.GetFormattedString();
            return string.IsNullOrWhiteSpace(texto) ? celda.GetString() : texto;
        }

        /// <summary>
        /// Lee filas pegadas desde Excel (columnas separadas por tabulador) o escritas con
        /// espacios ("1 PEDRO RAMOS QUISPE PERUANA B-01234567 01234567").
        /// </summary>
        public static List<FilaLista> LeerTexto(string texto)
        {
            var filas = new List<FilaLista>();
            int n = 0;
            foreach (var linea in (texto ?? "").Replace("\r", "").Split('\n'))
            {
                n++;
                if (string.IsNullOrWhiteSpace(linea)) continue;
                List<string> celdas = linea.Contains("\t")
                    ? linea.Split('\t').ToList()
                    : SepararLineaConEspacios(linea);
                var f = InterpretarCeldas(celdas, n);
                if (f != null) filas.Add(f);
            }
            return filas;
        }

        private static readonly Regex RxLineaEspacios = new Regex(
            @"^\s*(?:(?<n>\d{1,3})\s+)?(?<nombre>.+?)\s+(?:(?<nac>(?:PERUAN|VENEZOLAN|ECUATORIAN|COLOMBIAN|BOLIVIAN|CHILEN|EXTRANJER)[AO])\s+)?(?<lic>[A-Z]\s*-?\s*\d{6,10})\s+(?<dni>\d{7,9})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static List<string> SepararLineaConEspacios(string linea)
        {
            var m = RxLineaEspacios.Match(linea);
            if (!m.Success) return new List<string> { linea };
            return new List<string> { m.Groups["n"].Value, m.Groups["nombre"].Value, m.Groups["nac"].Value, m.Groups["lic"].Value, m.Groups["dni"].Value };
        }

        private static readonly Regex RxLicencia = new Regex(@"^[A-Z]\s*-?\s*\d{6,10}$", RegexOptions.Compiled);
        private static readonly Regex RxNombre = new Regex(@"^[A-ZÁÉÍÓÚÑÜ' .]+$", RegexOptions.Compiled);

        /// <summary>
        /// Reconoce cada columna por su forma (no por el encabezado, que cambia de una lista
        /// a otra): el DNI son 7 a 9 dígitos, la licencia una letra y dígitos, el nombre el
        /// texto de varias palabras y la nacionalidad el de una sola. Devuelve null para el
        /// encabezado y las filas vacías.
        /// </summary>
        public static FilaLista InterpretarCeldas(IList<string> celdas, int numeroFila)
        {
            var limpias = celdas.Select(c => Regex.Replace((c ?? "").Trim().ToUpperInvariant(), @"\s+", " "))
                .Where(c => c.Length > 0).ToList();
            if (limpias.Count == 0) return null;

            string dni = limpias.LastOrDefault(c => Regex.IsMatch(c, @"^\d{7,12}$"));
            string licencia = limpias.FirstOrDefault(c => RxLicencia.IsMatch(c));
            var textos = limpias.Where(c => RxNombre.IsMatch(c)).ToList();
            string nombre = textos.Where(t => t.Contains(" ")).OrderByDescending(t => t.Length).FirstOrDefault();
            string nacionalidad = textos.FirstOrDefault(t => !t.Contains(" ") && t != nombre);

            if (nombre == null && dni == null) return null;   // encabezado, título o fila vacía

            // El N° de la propia lista (1, 2, ...) identifica mejor la fila que la línea del archivo
            string numero = limpias.FirstOrDefault(c => Regex.IsMatch(c, @"^\d{1,3}$"));
            if (numero != null) numeroFila = int.Parse(numero);

            var fila = new FilaLista { Fila = numeroFila, Nombre = nombre, Nacionalidad = nacionalidad, DocumentoOriginal = dni };
            if (nombre == null)
            {
                fila.Avisos.Add("No trae el nombre del chofer.");
                return fila;
            }

            // El DNI peruano tiene 8 dígitos; el carnet de extranjería, 9. Con nacionalidad
            // extranjera o con 9 dígitos es carnet (la lista puede decir PERUANA por error).
            bool extranjero = nacionalidad != null && !nacionalidad.StartsWith("PERUAN");
            if (dni == null)
                fila.Avisos.Add("No trae un DNI ni un carnet de extranjería válido.");
            else if (extranjero || dni.Length == 9)
            {
                // Guardado como número en Excel, el carnet pierde los ceros iniciales
                fila.Carnet = dni.Length < 9 ? dni.PadLeft(9, '0') : dni;
                if (!extranjero)
                    fila.Avisos.Add($"El documento {dni} tiene 9 dígitos: se tomó como carnet de extranjería (el DNI tiene 8).");
            }
            else if (dni.Length == 7)
                fila.Dni = "0" + dni;   // Excel lo guardó como número y perdió el cero
            else if (dni.Length == 8)
                fila.Dni = dni;
            else
                fila.Avisos.Add($"El documento {dni} no es un DNI (8 dígitos) ni un carnet de extranjería (9).");

            if (licencia != null)
                fila.Licencia = licencia.Substring(0, 1) + Regex.Replace(licencia, @"\D", "");   // como la guarda la importación de guías ("B03620726")
            return fila;
        }

        // ── Comparación ──────────────────────────────────────────────────

        public static ComparacionLista Comparar(IList<FilaLista> filas, IList<ConductorRegistrado> registrados)
        {
            var r = new ComparacionLista { FilasLeidas = filas.Count };
            var porDocumento = registrados.SelectMany(c => c.Claves.Select(k => new { k, c }))
                .GroupBy(x => x.k).ToDictionary(g => g.Key, g => g.Select(x => x.c).ToList());
            var repetidos = new HashSet<string>(filas.Where(f => f.Clave != null)
                .GroupBy(f => f.Clave).Where(g => g.Count() > 1).Select(g => g.Key));
            var enLista = new HashSet<int>();
            int clave = 0;
            string NuevaClave() => "c" + (++clave);

            foreach (var f in filas)
            {
                bool tieneProblema = false;
                foreach (var aviso in f.Avisos)
                {
                    bool grave = f.Clave == null || f.Nombre == null;
                    (grave ? r.Problemas : r.Notas).Add(new ProblemaLista { Fila = f.Fila, Nombre = f.Nombre, Mensaje = aviso });
                    tieneProblema |= grave;
                }
                if (tieneProblema)
                {
                    // Sin documento no se registra; si el nombre ya está, no se le desactiva
                    foreach (var c in registrados.Where(c => NombresCompatibles(f.Nombre, c.NombreCompleto)))
                        enLista.Add(c.Id);
                    continue;
                }

                if (porDocumento.TryGetValue(f.Clave, out var mismos))
                {
                    var c = mismos.FirstOrDefault(x => NombresCompatibles(f.Nombre, x.NombreCompleto));
                    foreach (var x in mismos) enLista.Add(x.Id);
                    if (c == null)
                    {
                        r.Problemas.Add(new ProblemaLista
                        {
                            Fila = f.Fila,
                            Nombre = f.Nombre,
                            Mensaje = $"El documento {f.Documento} es de {mismos[0].NombreCompleto} en el sistema" +
                                      (repetidos.Contains(f.Clave) ? " y se repite en la lista" : "") +
                                      ": no se registró. Revise el documento correcto."
                        });
                        continue;
                    }
                    if (mismos.Count > 1)
                        r.Notas.Add(new ProblemaLista { Fila = f.Fila, Nombre = f.Nombre, Mensaje = $"Hay {mismos.Count} conductores con el documento {f.Documento} en el sistema." });

                    int cambios = r.Cambios.Count;
                    ProponerCambios(r, f, c, NuevaClave);
                    if (r.Cambios.Count == cambios) r.SinCambios++;
                    continue;
                }

                if (repetidos.Contains(f.Clave))
                {
                    r.Problemas.Add(new ProblemaLista { Fila = f.Fila, Nombre = f.Nombre, Mensaje = $"El documento {f.Documento} se repite en la lista: no se registró. Revise cuál es el correcto." });
                    continue;
                }

                // Mismo nombre con otro documento
                var homonimo = registrados.FirstOrDefault(c => c.Documento != null &&
                    ProgramacionDescarga.NombreCoincide(f.Nombre, c.NombreCompleto, tolerante: false) &&
                    ProgramacionDescarga.NombreCoincide(c.NombreCompleto, f.Nombre, tolerante: false));
                if (homonimo != null)
                {
                    enLista.Add(homonimo.Id);
                    // Un carnet de extranjería guardado como DNI sin los ceros iniciales
                    // (006792476 → 06792476): es la misma persona, se corrige el documento.
                    bool carnetComoDni = f.Carnet != null && !string.IsNullOrWhiteSpace(homonimo.Dni)
                        && f.Carnet.TrimStart('0') == homonimo.Dni.Trim().TrimStart('0');
                    if (carnetComoDni)
                    {
                        r.Cambios.Add(new CambioChofer
                        {
                            Clave = NuevaClave(), Tipo = TipoCambio.Documento, IdConductor = homonimo.Id,
                            Carnet = f.Carnet, Documento = f.Documento, NombreMostrado = homonimo.NombreCompleto,
                            Detalle = $"DNI {homonimo.Dni.Trim()} → carnet de extranjería {f.Carnet}",
                            MarcadoPorDefecto = true
                        });
                        ProponerCambios(r, f, homonimo, NuevaClave);
                        continue;
                    }
                    r.Problemas.Add(new ProblemaLista
                    {
                        Fila = f.Fila,
                        Nombre = f.Nombre,
                        Mensaje = $"En el sistema está con el documento {homonimo.Documento}, no con {f.Documento}: no se registró otra vez. Revise cuál es el correcto."
                    });
                    continue;
                }

                var partes = SepararNombre(f.Nombre);
                if (partes.Error != null)
                {
                    r.Problemas.Add(new ProblemaLista { Fila = f.Fila, Nombre = f.Nombre, Mensaje = partes.Error });
                    continue;
                }
                r.Cambios.Add(new CambioChofer
                {
                    Clave = NuevaClave(),
                    Tipo = TipoCambio.Registrar,
                    Dni = f.Dni,
                    Carnet = f.Carnet,
                    Documento = f.Documento,
                    NombreMostrado = f.Nombre,
                    Nombre = partes.Nombre,
                    ApPaterno = partes.ApPaterno,
                    ApMaterno = partes.ApMaterno,
                    Licencia = f.Licencia,
                    Detalle = $"Nombres: {partes.Nombre} · Ap. paterno: {partes.ApPaterno}" +
                              (partes.ApMaterno != null ? $" · Ap. materno: {partes.ApMaterno}" : "") +
                              (f.Licencia != null ? $" · Licencia {f.Licencia}" : " · Sin licencia"),
                    MarcadoPorDefecto = true
                });
            }

            foreach (var c in registrados.Where(c => c.Activo && !enLista.Contains(c.Id)).OrderBy(c => c.NombreCompleto))
            {
                r.Cambios.Add(new CambioChofer
                {
                    Clave = NuevaClave(),
                    Tipo = TipoCambio.Desactivar,
                    IdConductor = c.Id,
                    Documento = c.Documento,
                    NombreMostrado = c.NombreCompleto,
                    Detalle = c.TieneViajeAbierto
                        ? "No está en la lista, pero tiene un viaje abierto: desactívelo cuando lo cierre."
                        : "No está en la lista.",
                    MarcadoPorDefecto = !c.TieneViajeAbierto
                });
            }
            return r;
        }

        private static void ProponerCambios(ComparacionLista r, FilaLista f, ConductorRegistrado c, Func<string> nuevaClave)
        {
            if (!c.Activo)
                r.Cambios.Add(new CambioChofer
                {
                    Clave = nuevaClave(), Tipo = TipoCambio.Reactivar, IdConductor = c.Id, Documento = f.Documento,
                    NombreMostrado = c.NombreCompleto, Detalle = "Estaba inactivo y figura en la lista.", MarcadoPorDefecto = true
                });

            if (f.Licencia != null && NormalizarLicencia(c.Licencia) != NormalizarLicencia(f.Licencia))
                r.Cambios.Add(new CambioChofer
                {
                    Clave = nuevaClave(), Tipo = TipoCambio.Licencia, IdConductor = c.Id, Documento = f.Documento,
                    NombreMostrado = c.NombreCompleto, Licencia = f.Licencia,
                    Detalle = string.IsNullOrWhiteSpace(c.Licencia) ? $"Sin licencia → {f.Licencia}" : $"{c.Licencia.Trim()} → {f.Licencia}",
                    MarcadoPorDefecto = true
                });

            // El nombre de la lista solo reemplaza al registrado si aporta algo: si todas sus
            // palabras ya están en el sistema ("PEDRO RAMOS QUISPE" frente a "PEDRO
            // PABLO RAMOS QUISPE"), el registrado es más completo y se deja.
            if (!ProgramacionDescarga.NombreCoincide(f.Nombre, c.NombreCompleto, tolerante: false))
            {
                var partes = SepararNombre(f.Nombre);
                if (partes.Error == null)
                    r.Cambios.Add(new CambioChofer
                    {
                        Clave = nuevaClave(), Tipo = TipoCambio.Nombre, IdConductor = c.Id, Documento = f.Documento,
                        NombreMostrado = c.NombreCompleto,
                        Nombre = partes.Nombre, ApPaterno = partes.ApPaterno, ApMaterno = partes.ApMaterno,
                        Detalle = $"{c.NombreCompleto} → {f.Nombre}",
                        // Marcado si el registrado es una versión cortada o con una letra cambiada del de la lista
                        MarcadoPorDefecto = ProgramacionDescarga.NombreCoincide(c.NombreCompleto, f.Nombre)
                    });
            }
        }

        public static bool NombresCompatibles(string a, string b) =>
            ProgramacionDescarga.NombreCoincide(a, b) || ProgramacionDescarga.NombreCoincide(b, a);

        private static string NormalizarLicencia(string licencia) =>
            Regex.Replace((licencia ?? "").ToUpperInvariant(), @"[^A-Z0-9]", "");

        public class PartesNombre
        {
            public string Nombre { get; set; }
            public string ApPaterno { get; set; }
            public string ApMaterno { get; set; }
            public string Error { get; set; }
        }

        private static readonly HashSet<string> Particulas = new HashSet<string> { "DE", "DEL", "LA", "LAS", "LOS", "Y" };

        /// <summary>
        /// La lista trae "NOMBRES APELLIDO_PATERNO APELLIDO_MATERNO" en una sola columna: los
        /// dos últimos son los apellidos y lo demás los nombres. Las partículas van con su
        /// apellido ("MARIO VEGA DE LA CRUZ" → materno "DE LA CRUZ").
        /// </summary>
        public static PartesNombre SepararNombre(string completo)
        {
            var palabras = Regex.Split((completo ?? "").Trim().ToUpperInvariant(), @"\s+").Where(p => p.Length > 0).ToList();
            if (palabras.Count < 2)
                return new PartesNombre { Error = "El nombre necesita al menos un nombre y un apellido." };

            string TomarApellido()
            {
                var partes = new List<string> { palabras[palabras.Count - 1] };
                palabras.RemoveAt(palabras.Count - 1);
                while (palabras.Count > 1 && Particulas.Contains(palabras[palabras.Count - 1]))
                {
                    partes.Insert(0, palabras[palabras.Count - 1]);
                    palabras.RemoveAt(palabras.Count - 1);
                }
                return string.Join(" ", partes);
            }

            var r = new PartesNombre();
            if (palabras.Count == 2)
            {
                r.ApPaterno = palabras[1];
                r.Nombre = palabras[0];
            }
            else
            {
                r.ApMaterno = TomarApellido();
                r.ApPaterno = palabras.Count > 1 ? TomarApellido() : null;
                if (r.ApPaterno == null) { r.ApPaterno = r.ApMaterno; r.ApMaterno = null; }
                r.Nombre = string.Join(" ", palabras);
            }

            if (r.Nombre.Length > 50 || r.ApPaterno.Length > 20 || (r.ApMaterno ?? "").Length > 20)
                r.Error = "El nombre o algún apellido es más largo de lo que admite el sistema (50 y 20 letras).";
            return r;
        }

        // ── Base de datos ────────────────────────────────────────────────

        public static List<ConductorRegistrado> ObtenerRegistrados()
        {
            var dt = DbHelper.ConsultarTabla(@"
                SELECT c.idConductor, c.DNI, c.carnetExtranjeria, c.nombre, c.apPaterno, c.apMaterno, c.numeroLicencia, c.activo,
                       CAST(CASE WHEN EXISTS (SELECT 1 FROM ViajesEnProgreso v
                                              WHERE v.idConductor = c.idConductor AND v.activo = 1 AND v.estadoViaje = 'ABIERTO')
                                 THEN 1 ELSE 0 END AS bit) AS tieneViajeAbierto
                FROM Conductor c");
            return dt.Rows.Cast<DataRow>().Select(x => new ConductorRegistrado
            {
                Id = Convert.ToInt32(x["idConductor"]),
                Dni = x["DNI"] as string,
                Carnet = (x["carnetExtranjeria"] as string)?.Trim(),
                Nombre = x["nombre"] as string,
                ApPaterno = x["apPaterno"] as string,
                ApMaterno = x["apMaterno"] as string,
                Licencia = x["numeroLicencia"] as string,
                Activo = Convert.ToBoolean(x["activo"]),
                TieneViajeAbierto = Convert.ToBoolean(x["tieneViajeAbierto"])
            }).ToList();
        }

        /// <summary>Aplica los cambios elegidos en una sola transacción y los audita.</summary>
        public static void Aplicar(IList<CambioChofer> cambios)
        {
            DbHelper.EnTransaccion((conn, tx) =>
            {
                foreach (var c in cambios)
                {
                    switch (c.Tipo)
                    {
                        case TipoCambio.Registrar:
                            object id = DbHelper.EjecutarEscalar(conn, tx,
                                "EXEC sp_RegistrarConductorImportado @DNI, @nombre, @apPaterno, @apMaterno, @numeroLicencia, @carnetExtranjeria",
                                DbHelper.Param("@DNI", (object)c.Dni ?? DBNull.Value),
                                DbHelper.Param("@carnetExtranjeria", (object)c.Carnet ?? DBNull.Value),
                                DbHelper.Param("@nombre", c.Nombre),
                                DbHelper.Param("@apPaterno", c.ApPaterno),
                                DbHelper.Param("@apMaterno", (object)c.ApMaterno ?? DBNull.Value),
                                DbHelper.Param("@numeroLicencia", (object)c.Licencia ?? DBNull.Value));
                            c.IdConductor = Convert.ToInt32(id);
                            break;
                        case TipoCambio.Documento:
                            // Carnet de extranjería que estaba guardado como DNI
                            DbHelper.EjecutarNonQuery(conn, tx,
                                "UPDATE Conductor SET DNI = NULL, carnetExtranjeria = @ce WHERE idConductor = @id",
                                DbHelper.Param("@ce", c.Carnet), DbHelper.Param("@id", c.IdConductor));
                            break;
                        case TipoCambio.Licencia:
                            DbHelper.EjecutarNonQuery(conn, tx,
                                "UPDATE Conductor SET numeroLicencia = @lic WHERE idConductor = @id",
                                DbHelper.Param("@lic", c.Licencia), DbHelper.Param("@id", c.IdConductor));
                            break;
                        case TipoCambio.Nombre:
                            DbHelper.EjecutarNonQuery(conn, tx,
                                "UPDATE Conductor SET nombre = @n, apPaterno = @p, apMaterno = @m WHERE idConductor = @id",
                                DbHelper.Param("@n", c.Nombre), DbHelper.Param("@p", c.ApPaterno),
                                DbHelper.Param("@m", (object)c.ApMaterno ?? DBNull.Value), DbHelper.Param("@id", c.IdConductor));
                            break;
                        case TipoCambio.Reactivar:
                        case TipoCambio.Desactivar:
                            DbHelper.EjecutarNonQuery(conn, tx,
                                "UPDATE Conductor SET activo = @a WHERE idConductor = @id",
                                DbHelper.Param("@a", c.Tipo == TipoCambio.Reactivar), DbHelper.Param("@id", c.IdConductor));
                            break;
                    }
                }
            });

            foreach (var c in cambios)
                AuditoriaHelper.Registrar(c.Tipo == TipoCambio.Registrar ? "INSERT" : "UPDATE", "Conductor", c.IdConductor?.ToString(),
                    $"Lista de choferes actuales — {TextoTipo(c.Tipo)}: {c.NombreMostrado} ({c.Documento}). {c.Detalle}");
        }

        public static string TextoTipo(TipoCambio tipo)
        {
            switch (tipo)
            {
                case TipoCambio.Registrar: return "Registrar";
                case TipoCambio.Documento: return "Documento";
                case TipoCambio.Licencia: return "Licencia";
                case TipoCambio.Nombre: return "Nombre";
                case TipoCambio.Reactivar: return "Reactivar";
                default: return "Desactivar";
            }
        }
    }
}
