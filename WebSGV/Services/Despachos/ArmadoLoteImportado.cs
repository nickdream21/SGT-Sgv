using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Services.Despachos
{
    /// <summary>Lo elegido en un selector de la página: su valor y el texto que se ve (null si no hay nada elegido).</summary>
    internal class OpcionElegida
    {
        public string Valor { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>
    /// Armado del lote a partir de la vista previa de "Leer documentos" de RegistroDespacho (E6-22): leer las filas
    /// elegidas (desde DTO, no desde controles), validar las altas, registrar los productos y conductores aprobados,
    /// armar los conductores del lote y el lote mismo. La página solo lee los controles y muestra lo que devuelve;
    /// los mensajes, su orden, los SP y la auditoría son los de antes.
    /// </summary>
    internal static class ArmadoLoteImportado
    {
        /// <summary>Valor de la opción "+ Registrar nuevo" del selector de conductor de la vista previa.</summary>
        public const string ValorConductorNuevo = "NUEVO";

        // ── Lo que la página lee de la vista previa ─────────────────────────────────────────

        /// <summary>Una fila de la tabla de tractos importados tal como está en la página.</summary>
        public class FilaVistaPrevia
        {
            public bool Incluida { get; set; }
            /// <summary>El selector de despacho programado se ve en la fila.</summary>
            public bool ConProgramados { get; set; }
            public string ValorProgramado { get; set; }
            public OpcionElegida Conductor { get; set; }
            public OpcionElegida Tracto { get; set; }
            public OpcionElegida Carreta { get; set; }
        }

        /// <summary>Un producto nuevo de la vista previa con el nombre que dejó el usuario.</summary>
        public class ProductoAprobado
        {
            public ImportacionPedidoService.ProductoNuevo Producto { get; set; }
            public string Nombre { get; set; }
        }

        /// <summary>Los datos de un conductor nuevo tal como los dejó el usuario (por número de transporte).</summary>
        public class ConductorNuevoLeido
        {
            public string Transporte { get; set; }
            public string ApPaterno { get; set; }
            public string ApMaterno { get; set; }
            public string Nombres { get; set; }
        }

        /// <summary>Todo lo que el usuario eligió en la vista previa.</summary>
        public class EleccionImportacion
        {
            /// <summary>Una por fila de la tabla, en orden (la fila i es el tracto i de la importación).</summary>
            public List<FilaVistaPrevia> Filas { get; set; } = new List<FilaVistaPrevia>();
            /// <summary>Productos nuevos; null si su recuadro no se ve.</summary>
            public List<ProductoAprobado> Productos { get; set; }
            /// <summary>Conductores nuevos; null si su recuadro no se ve (entonces tampoco se exigen sus datos).</summary>
            public List<ConductorNuevoLeido> ConductoresNuevos { get; set; }
            public bool AltasAprobadas { get; set; }
        }

        // ── Lo que se arma ──────────────────────────────────────────────────────────────────

        /// <summary>Una fila incluida de la vista previa con lo que el usuario eligió.</summary>
        public class FilaImportacion
        {
            public TractoImportado Tracto { get; set; }
            /// <summary>Id del conductor, o <see cref="ValorConductorNuevo"/>.</summary>
            public string ValorConductor { get; set; }
            public string NombreConductor { get; set; }
            public int IdTracto { get; set; }
            public string PlacaTracto { get; set; }
            public int IdCarreta { get; set; }
            public string PlacaCarreta { get; set; }
            public int? IdDespachoProgramado { get; set; }
        }

        /// <summary>Un conductor nuevo aprobado: el tracto de su guía y su nombre.</summary>
        public class ConductorAprobado
        {
            public TractoImportado Tracto { get; set; }
            public string ApPaterno { get; set; }
            public string ApMaterno { get; set; }
            public string Nombres { get; set; }
        }

        /// <summary>La importación ya validada, lista para registrar las altas y armar los conductores.</summary>
        public class ImportacionPreparada
        {
            public List<FilaImportacion> Filas { get; set; }
            public List<ProductoAprobado> Productos { get; set; }
            public List<ConductorAprobado> Conductores { get; set; }
        }

        // ── Validación ──────────────────────────────────────────────────────────────────────

        /// <summary>Lo que el lote necesita y no se elige en la tabla: fecha, planta y factura (en ese orden).</summary>
        public static List<string> ValidarDatosLote(ImportacionPedido imp, string fechaTexto, string plantaValor,
            out DateTime fecha, out int idPlanta) =>
            ValidarDatosLote(imp, fechaTexto, plantaValor, FechaHelper.Hoy(),
                (factura, idCliente) => RegistroDespachoService.ValidarDocumentosDuplicados(factura, null, idCliente).FacturaExiste,
                out fecha, out idPlanta);

        internal static List<string> ValidarDatosLote(ImportacionPedido imp, string fechaTexto, string plantaValor, DateTime hoy,
            Func<string, int?, bool> facturaRegistrada, out DateTime fecha, out int idPlanta)
        {
            var errores = new List<string>();

            if (!DespachoValidaciones.FechaDespachoEsValida(fechaTexto, hoy))
                errores.Add("La fecha de programación no es válida.");
            DespachoValidaciones.TryParseFechaIso(fechaTexto, out fecha);

            if (!int.TryParse(plantaValor, out idPlanta) || idPlanta <= 0)
                errores.Add("Elija la planta de carga.");

            var f = imp.Factura;
            if (f == null || string.IsNullOrEmpty(f.NumeroFactura))
                errores.Add("No se leyó la factura (la carga nacional la requiere): súbala junto con los demás documentos o use \"Corregir datos a mano\".");
            else
            {
                if (!f.FechaEmision.HasValue || f.FechaEmision.Value.Date > hoy)
                    errores.Add($"La fecha de emisión de la factura {f.NumeroFactura} no es válida.");
                if (!f.ValorTotal.HasValue || f.ValorTotal.Value <= 0)
                    errores.Add($"No se leyó el importe total de la factura {f.NumeroFactura}.");
                if (facturaRegistrada(f.NumeroFactura, imp.IdCliente))
                    errores.Add($"La factura {f.NumeroFactura} ya está registrada. Si es otra parte del mismo pedido, use \"Corregir datos a mano\".");
            }
            return errores;
        }

        /// <summary>
        /// Común a "Crear lote con estos datos" y "Corregir datos a mano": valida las filas y las altas y agrega los
        /// errores a <paramref name="errores"/> (después de los que ya traía). Sin errores devuelve la importación
        /// preparada; con alguno, null. No escribe nada.
        /// </summary>
        public static ImportacionPreparada Validar(ImportacionPedido imp, EleccionImportacion eleccion, List<string> errores)
        {
            var filas = LeerFilas(imp, eleccion.Filas, out List<string> erroresFilas);
            errores.AddRange(erroresFilas);
            if (filas.Count == 0 && erroresFilas.Count == 0)
                errores.Add("No hay tractos marcados para incluir.");

            var productos = eleccion.Productos ?? new List<ProductoAprobado>();
            var conductores = LeerConductoresNuevos(eleccion.ConductoresNuevos, filas, errores);

            bool hayAltas = conductores.Count > 0 ||
                productos.Any(p => filas.Any(f => f.Tracto.Lineas.Any(l => !l.IdProducto.HasValue && EsMismoProducto(l, p.Producto))));
            if (hayAltas && !eleccion.AltasAprobadas)
                errores.Add("Marque la aprobación de los productos y conductores nuevos.");

            return errores.Count > 0 ? null
                : new ImportacionPreparada { Filas = filas, Productos = productos, Conductores = conductores };
        }

        /// <summary>Las filas incluidas, con los errores de cada una y los repetidos dentro del lote.</summary>
        internal static List<FilaImportacion> LeerFilas(ImportacionPedido imp, IList<FilaVistaPrevia> vista, out List<string> errores)
        {
            errores = new List<string>();
            var filas = new List<FilaImportacion>();

            for (int i = 0; i < vista.Count && i < imp.Tractos.Count; i++)
            {
                var v = vista[i];
                var t = imp.Tractos[i];
                if (t.YaRegistrado || !v.Incluida) continue;

                string etiqueta = $"Transporte {t.NumeroTransporte}";

                // Completa un despacho programado: chofer, tracto y carreta son los del despacho
                string eleccion = ProgramacionCarga.ErrorEleccionProgramado(v.ConProgramados, v.ValorProgramado, t);
                if (eleccion != null)
                {
                    errores.Add(eleccion);
                    continue;
                }
                var programado = v.ConProgramados && int.TryParse(v.ValorProgramado, out int idProg) && idProg > 0
                    ? imp.Programados?.FirstOrDefault(p => p.IdDespacho == idProg) : null;
                if (programado != null)
                {
                    if (t.Lineas.Count == 0) errores.Add($"{etiqueta}: no tiene carga leída.");
                    filas.Add(new FilaImportacion
                    {
                        Tracto = t,
                        ValorConductor = programado.IdConductor.ToString(),
                        NombreConductor = programado.NombreConductor,
                        IdTracto = programado.IdTracto,
                        PlacaTracto = programado.PlacaTracto,
                        IdCarreta = programado.IdCarreta,
                        PlacaCarreta = programado.PlacaCarreta,
                        IdDespachoProgramado = programado.IdDespacho
                    });
                    continue;
                }

                if (v.Conductor.Valor == "0") errores.Add($"{etiqueta}: elija el conductor.");
                if (v.Tracto.Valor == "0") errores.Add($"{etiqueta}: elija el tracto.");
                if (v.Carreta.Valor == "0") errores.Add($"{etiqueta}: elija la carreta.");
                if (t.Lineas.Count == 0) errores.Add($"{etiqueta}: no tiene carga leída.");

                filas.Add(new FilaImportacion
                {
                    Tracto = t,
                    ValorConductor = v.Conductor.Valor,
                    NombreConductor = v.Conductor.Texto?.Split('-')[0].Trim(),
                    IdTracto = int.TryParse(v.Tracto.Valor, out int idT) ? idT : 0,
                    PlacaTracto = v.Tracto.Texto,
                    IdCarreta = int.TryParse(v.Carreta.Valor, out int idC) ? idC : 0,
                    PlacaCarreta = v.Carreta.Texto
                });
            }

            // Lo mismo que valida "Agregar conductor": nada repetido dentro del lote
            if (HayRepetidos(filas.Select(f => f.ValorConductor))) errores.Add("Hay un conductor repetido en más de un tracto.");
            if (HayRepetidos(filas.Select(f => f.IdTracto.ToString()))) errores.Add("Hay un tracto repetido en más de una fila.");
            if (HayRepetidos(filas.Select(f => f.IdCarreta.ToString()))) errores.Add("Hay una carreta repetida en más de un tracto.");
            if (HayRepetidos(filas.Where(f => f.IdDespachoProgramado.HasValue).Select(f => f.IdDespachoProgramado.ToString())))
                errores.Add("Hay un despacho programado elegido en más de una guía.");

            return filas;
        }

        /// <summary>Hay un valor elegido más de una vez (sin contar "0" ni "Registrar nuevo").</summary>
        public static bool HayRepetidos(IEnumerable<string> valores) =>
            valores.Where(v => v != "0" && v != ValorConductorNuevo).GroupBy(v => v).Any(g => g.Count() > 1);

        /// <summary>Conductores nuevos de las filas incluidas que quedaron en "Registrar nuevo".</summary>
        internal static List<ConductorAprobado> LeerConductoresNuevos(IList<ConductorNuevoLeido> leidos, List<FilaImportacion> filas, List<string> errores)
        {
            var lista = new List<ConductorAprobado>();
            if (leidos == null) return lista;

            foreach (var leido in leidos)
            {
                var fila = filas.FirstOrDefault(f => f.Tracto.NumeroTransporte == leido.Transporte && f.ValorConductor == ValorConductorNuevo);
                if (fila == null) continue;

                var nuevo = new ConductorAprobado
                {
                    Tracto = fila.Tracto,
                    ApPaterno = (leido.ApPaterno ?? "").Trim(),
                    ApMaterno = (leido.ApMaterno ?? "").Trim(),
                    Nombres = (leido.Nombres ?? "").Trim()
                };
                if (nuevo.ApPaterno.Length == 0 || nuevo.Nombres.Length == 0)
                    errores.Add($"Conductor DNI {fila.Tracto.DniSegunLicencia}: complete apellido paterno y nombres.");
                lista.Add(nuevo);
            }

            foreach (var f in filas.Where(f => f.ValorConductor == ValorConductorNuevo && lista.All(c => c.Tracto != f.Tracto)))
                errores.Add($"Transporte {f.Tracto.NumeroTransporte}: no hay datos para registrar al conductor; elíjalo de la lista.");

            return lista;
        }

        internal static bool EsMismoProducto(LineaCarga linea, ImportacionPedidoService.ProductoNuevo producto) =>
            producto.Codigo != null
                ? linea.CodigoProducto == producto.Codigo
                : LectorDocumentosPedido.Normalizar(linea.Descripcion) == LectorDocumentosPedido.Normalizar(producto.Descripcion);

        // ── Escritura ───────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Registra las altas aprobadas, arma los conductores del lote (cada uno con su viaje abierto) y deja
        /// constancia de la importación en la auditoría. Lo llama la página solo con una importación sin errores.
        /// </summary>
        public static List<ConductorLote> Aplicar(ImportacionPedido imp, ImportacionPreparada preparada, string usuario)
        {
            RegistrarAltas(imp, preparada, usuario);
            var conductores = CrearConductores(preparada.Filas, RegistroDespachoService.ViajeAbiertoParaCarga);
            AuditarImportacion(imp, preparada.Filas, conductores.Count);
            return conductores;
        }

        /// <summary>
        /// Registra los productos y conductores aprobados (solo los que usan las filas incluidas) y completa la
        /// licencia de los conductores existentes.
        /// </summary>
        internal static void RegistrarAltas(ImportacionPedido imp, ImportacionPreparada preparada, string usuario)
        {
            var filas = preparada.Filas;
            foreach (var p in preparada.Productos)
            {
                if (!filas.Any(f => f.Tracto.Lineas.Any(l => !l.IdProducto.HasValue && EsMismoProducto(l, p.Producto))))
                    continue;

                string nombre = string.IsNullOrWhiteSpace(p.Nombre) ? p.Producto.Descripcion : p.Nombre;
                int id = ImportacionPedidoService.RegistrarProducto(imp, p.Producto, nombre);
                AuditoriaHelper.Registrar("INSERT", "Producto", id,
                    $"Producto {nombre} (código {p.Producto.Codigo}) registrado desde la importación del pedido {imp.NumeroPedido}, aprobado por {usuario}");
            }

            foreach (var c in preparada.Conductores)
            {
                int id = ImportacionPedidoService.RegistrarConductor(c.Tracto.DniSegunLicencia, c.Nombres, c.ApPaterno, c.ApMaterno, c.Tracto.Licencia);
                c.Tracto.IdConductor = id;
                AuditoriaHelper.Registrar("INSERT", "Conductor", id,
                    $"Conductor {c.Nombres} {c.ApPaterno} {c.ApMaterno} (DNI {c.Tracto.DniSegunLicencia}) registrado desde la importación del pedido {imp.NumeroPedido}, aprobado por {usuario}");

                var fila = filas.First(f => f.Tracto == c.Tracto);
                fila.ValorConductor = id.ToString();
                fila.NombreConductor = $"{c.Nombres} {c.ApPaterno} {c.ApMaterno}".Trim().ToUpperInvariant();
            }

            // Licencia de la guía en conductores existentes que no la tenían: solo si es el mismo
            // chofer de la guía (con un cambio de la programación, la licencia es de otro)
            foreach (var f in filas.Where(f => !string.IsNullOrEmpty(f.Tracto.Licencia)
                                               && f.Tracto.IdConductorGuia.HasValue && f.ValorConductor == f.Tracto.IdConductorGuia.Value.ToString()))
            {
                try { ImportacionPedidoService.CompletarLicencia(int.Parse(f.ValorConductor), f.Tracto.Licencia); }
                catch (Exception ex) { LogSGV.Error(ex, "Error en {Contexto}", "CompletarLicencia"); }   // dato accesorio: no frena la importación
            }
        }

        /// <summary>Los conductores del lote, uno por fila, con el viaje abierto que da <paramref name="viajeParaCarga"/>.</summary>
        internal static List<ConductorLote> CrearConductores(IEnumerable<FilaImportacion> filas, Func<int, ViajeEnProgreso> viajeParaCarga) =>
            filas.Select(f => CrearConductor(f, viajeParaCarga)).ToList();

        internal static ConductorLote CrearConductor(FilaImportacion f, Func<int, ViajeEnProgreso> viajeParaCarga)
        {
            int idConductor = int.Parse(f.ValorConductor);

            // Misma regla que DeterminarViajeProgreso sin elección manual: se filtra por
            // ámbito solo si el filtro deja algún viaje, y se usa el más reciente. La carga
            // del packing list es siempre nacional (planta de origen). Un despacho programado
            // ya tiene su viaje: no se toca.
            var viaje = f.IdDespachoProgramado.HasValue ? null : viajeParaCarga(idConductor);

            return new ConductorLote
            {
                IdConductor = idConductor,
                NombreConductor = f.NombreConductor,
                IdTracto = f.IdTracto,
                PlacaTracto = f.PlacaTracto,
                IdCarreta = f.IdCarreta,
                PlacaCarreta = f.PlacaCarreta,
                GuiaRemitente = f.Tracto.GuiasParaDespacho,
                NumeroEntrega = f.Tracto.NumeroEntrega,
                NumeroTransporte = f.Tracto.NumeroTransporte,
                Carga = f.Tracto.Lineas,
                IdDespachoProgramado = f.IdDespachoProgramado,
                IdViajeProgreso = viaje?.IdViajeProgreso,
                NumeroViajeProgreso = viaje?.NumeroViajeProgreso,
                EstadoViaje = f.IdDespachoProgramado.HasValue ? "Despacho programado" : viaje?.EstadoViaje ?? "Por asignar"
            };
        }

        /// <summary>Constancia de la importación y de por qué una guía no coincide con lo registrado (la guía no se reemite).</summary>
        internal static void AuditarImportacion(ImportacionPedido imp, IEnumerable<FilaImportacion> filas, int tractos) =>
            AuditoriaHelper.Registrar("IMPORT", "Despachos", null, DescripcionAuditoria(imp, filas, tractos));

        internal static string DescripcionAuditoria(ImportacionPedido imp, IEnumerable<FilaImportacion> filas, int tractos)
        {
            var cambios = filas.Where(f => f.Tracto.Programacion?.Cambio != null)
                .Select(f => $"{f.Tracto.NumeroGuia ?? f.Tracto.NumeroTransporte}: {f.Tracto.NombreChofer}/{f.Tracto.Placa} → {f.NombreConductor}/{f.PlacaTracto}")
                .ToList();
            return $"Documentos del pedido {imp.NumeroPedido} importados: {tractos} tracto(s), factura {imp.Factura?.NumeroFactura}" +
                (imp.Programacion == null ? ", sin programación" : $", programación {imp.Programacion.NombreArchivo}") +
                (cambios.Count == 0 ? string.Empty : $". Cambios según la programación: {string.Join("; ", cambios)}");
        }

        // ── Lote ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// "Crear lote con estos datos": el lote de CARGA nacional con lo leído (cliente, pedido, destino, factura y
        /// su PDF). El PDF de la factura pasa a ser del lote: se quita de la importación para no borrarlo al limpiarla.
        /// </summary>
        public static LoteDespachos ArmarLote(ImportacionPedido imp, DateTime fecha, int idPlanta, string nombrePlanta,
            List<ConductorLote> conductores, string usuario)
        {
            var lote = new LoteDespachos
            {
                FechaProgramacion = fecha.ToString("yyyy-MM-dd"),
                IdCliente = imp.IdCliente.Value,
                NombreCliente = imp.NombreCliente,
                NumeroPedido = DespachoValidaciones.ValidarNumeroPedido(imp.NumeroPedido) ? imp.NumeroPedido : string.Empty,
                TipoOperacion = "CARGA",
                EsInternacional = false,
                IdPlanta = idPlanta,
                PlantaOperacion = nombrePlanta,
                DestinoCarga = imp.Destino,
                EsExportacion = imp.EsExportacion,
                UsuarioCreacion = usuario
            };
            lote.Documentacion.NumeroFactura = imp.Factura.NumeroFactura;
            lote.Documentacion.FechaEmisionFactura = imp.Factura.FechaEmision;
            lote.Documentacion.ValorTotalFactura = imp.Factura.ValorTotal;
            lote.Documentacion.MonedaFactura = imp.Factura.Moneda ?? "PEN";
            lote.Documentacion.FacturaArchivoRutaTemp = imp.FacturaArchivoRutaTemp;
            lote.Documentacion.FacturaArchivoNombreOriginal = imp.FacturaArchivoNombreOriginal;
            imp.FacturaArchivoRutaTemp = null;   // ahora es del lote: no borrarlo al limpiar
            lote.Conductores.AddRange(conductores);
            return lote;
        }

        /// <summary>
        /// Al iniciar el lote tras "Corregir datos a mano": agrega los conductores importados, el N° de pedido si el
        /// formulario no lo trae, el destino de la carga y el PDF de la factura si el usuario no adjuntó otro (y la
        /// misma factura): ese PDF pasa a ser del lote.
        /// </summary>
        public static void IncorporarAlLote(LoteDespachos lote, ImportacionPedido imp, List<ConductorLote> conductores)
        {
            lote.Conductores.AddRange(conductores);

            if (string.IsNullOrEmpty(lote.NumeroPedido) && DespachoValidaciones.ValidarNumeroPedido(imp.NumeroPedido))
                lote.NumeroPedido = imp.NumeroPedido;

            lote.DestinoCarga = imp.Destino;
            lote.EsExportacion = imp.EsExportacion;

            bool mismaFactura = imp.Factura != null &&
                string.Equals(lote.Documentacion.NumeroFactura, imp.Factura.NumeroFactura, StringComparison.OrdinalIgnoreCase);
            if (mismaFactura && string.IsNullOrEmpty(lote.Documentacion.FacturaArchivoRutaTemp)
                && !string.IsNullOrEmpty(imp.FacturaArchivoRutaTemp))
            {
                lote.Documentacion.FacturaArchivoRutaTemp = imp.FacturaArchivoRutaTemp;
                lote.Documentacion.FacturaArchivoNombreOriginal = imp.FacturaArchivoNombreOriginal;
                imp.FacturaArchivoRutaTemp = null;   // ahora es del lote: no borrarlo al limpiar
            }
        }

        // ── Lo que muestra la vista previa ──────────────────────────────────────────────────

        /// <summary>Avisos de la vista previa: los de la lectura más cliente sin reconocer (primero), ya registrados y planta sin reconocer.</summary>
        public static List<string> Advertencias(ImportacionPedido imp)
        {
            var advertencias = imp.Advertencias.ToList();
            if (!imp.IdCliente.HasValue)
                advertencias.Insert(0, "El RUC del remitente no corresponde a ningún cliente activo. Elija el cliente en la configuración base antes de usar estos datos.");
            if (imp.Tractos.Any(t => t.YaRegistrado))
                advertencias.Add("Los tractos marcados como \"ya registrado\" tienen un despacho con ese N° de transporte y no se volverán a cargar.");
            if (!imp.IdPlantaSugerida.HasValue)
                advertencias.Add($"No se reconoció la planta de carga ({imp.Origen ?? "sin origen en los documentos"}): elíjala en la configuración base.");
            return advertencias;
        }

        /// <summary>
        /// Qué se tomó de la programación en Word (o el recordatorio de subirla), en HTML ya codificado: el Word es lo
        /// que realmente sale, las guías quedan con la lista que se mandó al cliente. <paramref name="tono"/>: vacío
        /// (neutro), "rd-notice-warn" (hay algo que revisar) o "rd-notice-ok".
        /// </summary>
        public static string ResumenProgramacionHtml(ImportacionPedido imp, out string tono)
        {
            tono = string.Empty;
            var prog = imp.Programacion;
            if (prog == null)
                return Html("Sin la programación del día ni despachos programados: el chofer, el tracto y la carreta salen de las guías (la carreta, de la última con que salió el tracto). " +
                            "Si hubo ajustes de chofer, vuelva a leer los documentos junto con el Word de la programación.");

            var pendientes = imp.Tractos.Where(t => !t.YaRegistrado).ToList();
            if (pendientes.Count == 0)
                return Html($"Programación · {prog.Titulo}: los {imp.Tractos.Count} tracto(s) de este pedido ya están registrados; no hay nada que tomar de ella.");

            int cambios = pendientes.Count(t => t.Programacion?.Cambio != null);
            int conAvisos = pendientes.Count(t => t.Programacion != null && t.Programacion.Avisos.Count > 0);
            int completan = pendientes.Count(t => t.Programacion?.IdDespachoProgramado != null);

            var sb = new StringBuilder();
            if (prog.DesdeDespachos)
            {
                sb.Append(Html($"Despachos programados con el Word: {completan} de {pendientes.Count} guía(s) completan su despacho programado " +
                               "(no se crea otro). Chofer, tracto y carreta quedan los del despacho; la guía aporta pedido, factura y carga."));
            }
            else
            {
                sb.Append(Html($"Programación{(prog.Fecha.HasValue ? " del " + prog.Fecha.Value.ToString("dd/MM/yyyy") : "")} · {prog.Titulo}: " +
                               $"{prog.Encontrados} de {pendientes.Count} tracto(s) unidos con su guía. Chofer, tracto y carreta salen de la programación."));
                if (completan > 0)
                    sb.Append(Html($" {completan} completan un despacho ya programado (no se crea otro)."));
            }
            if (cambios > 0)
                sb.Append(Html($" {cambios} con cambio respecto de la guía (normal: la guía no se reemite)."));
            sb.Append(conAvisos > 0
                ? Html($" {conAvisos} con avisos en ámbar: revíselos antes de continuar.")
                : Html(" Sin nada que revisar."));
            if (prog.AvisosGenerales.Count > 0)
                sb.Append("<ul class=\"mb-0 mt-1\">" + string.Concat(prog.AvisosGenerales.Select(a => $"<li>{Html(a)}</li>")) + "</ul>");

            tono = conAvisos > 0 || prog.AvisosGenerales.Count > 0 ? "rd-notice-warn" : "rd-notice-ok";
            return sb.ToString();
        }

        private static string Html(string texto) => HttpUtility.HtmlEncode(texto ?? string.Empty);
    }
}
