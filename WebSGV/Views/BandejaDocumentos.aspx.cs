using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;

namespace WebSGV.Views
{
    /// <summary>
    /// Bandeja de documentos (menú Despacho): se sueltan los ZIP y PDF del correo y las facturas de flete;
    /// <see cref="BandejaDocumentosService"/> los reconoce y une con sus cargas, el usuario revisa la vista
    /// previa (puede cambiar el despacho de una guía o no registrarla) y confirma. Debajo, el estado de los
    /// documentos de cada lote por cliente y pedido, con enlace a su panel en ListaDespachos.
    /// </summary>
    public partial class BandejaDocumentos : PaginaBase
    {
        private const string SesionLectura = "BandejaDocumentosLectura";
        private const string PrefijoSeleccion = "bd_sel_";
        private const string PrefijoNuevo = "bd_nuevo_";
        private const string PrefijoMci = "bd_mci_";
        private const string PrefijoGre = "bd_gre_";
        private const string ValorNuevo = "nuevo";

        private static int? Entero(string s) => int.TryParse(s, out int v) ? v : (int?)null;
        private static readonly CultureInfo Pe = CultureInfo.GetCultureInfo("es-PE");

        private LecturaBandeja Lectura
        {
            get { return Session[SesionLectura] as LecturaBandeja; }
            set { if (value == null) Session.Remove(SesionLectura); else Session[SesionLectura] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarClientes();
                txtDesde.Text = FechaHelper.Hoy().AddDays(-60).ToString("yyyy-MM-dd");
                txtHasta.Text = FechaHelper.Hoy().AddDays(7).ToString("yyyy-MM-dd");
                if (Lectura != null) MostrarVistaPrevia(Lectura, null);
                CargarTablero();
            }
        }

        private void CargarClientes()
        {
            ddlCliente.Items.Clear();
            ddlCliente.Items.Add(new ListItem("Todos", ""));
            foreach (DataRow r in RegistroDespachoService.ObtenerClientesActivos().Rows)
                ddlCliente.Items.Add(new ListItem(Convert.ToString(r["nombre"]).Trim(), Convert.ToString(r["idCliente"])));
        }

        // ── Leer ─────────────────────────────────────────────────────────────

        protected void btnLeer_Click(object sender, EventArgs e)
        {
            var archivos = (fuArchivos.PostedFiles ?? new List<HttpPostedFile>()).Where(f => f != null && f.ContentLength > 0).ToList();
            if (archivos.Count == 0)
            {
                MostrarMensaje("Elija uno o varios archivos (ZIP o PDF).", "warning");
                CargarTablero();
                return;
            }
            try
            {
                var subidos = new List<ImportacionPedidoService.ArchivoSubido>();
                var omitidos = new List<string>();
                foreach (var a in archivos)
                {
                    string nombre = Path.GetFileName(a.FileName);
                    if (a.ContentLength > ImportacionPedidoService.TamanoMaximoArchivo)
                    {
                        omitidos.Add($"{nombre}: pesa más de {ImportacionPedidoService.TamanoMaximoArchivo / 1024 / 1024} MB.");
                        continue;
                    }
                    using (var ms = new MemoryStream())
                    {
                        a.InputStream.CopyTo(ms);
                        subidos.Add(new ImportacionPedidoService.ArchivoSubido { Nombre = nombre, Contenido = ms.ToArray() });
                    }
                }
                var lectura = BandejaDocumentosService.Leer(subidos, EmpresaConfigHelper.ObtenerEmpresa().Ruc);
                lectura.NoReconocidos.InsertRange(0, omitidos);
                Lectura = lectura;
                MostrarVistaPrevia(lectura, null);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Bandeja de documentos: error al leer los archivos");
                MostrarMensaje("No se pudieron leer los archivos. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
            CargarTablero();
        }

        protected void btnDescartar_Click(object sender, EventArgs e)
        {
            Lectura = null;
            pnlVistaPrevia.Visible = false;
            MostrarMensaje("Lectura descartada: no se registró nada.", "info");
            CargarTablero();
        }

        // ── Registrar ────────────────────────────────────────────────────────

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            var lectura = Lectura;
            if (lectura == null)
            {
                MostrarMensaje("La lectura expiró: vuelva a subir los archivos.", "warning");
                CargarTablero();
                return;
            }
            try
            {
                var eleccion = new Dictionary<string, EleccionGuia>();
                var guias = GuiasEditables(lectura);
                for (int i = 0; i < guias.Count; i++)
                {
                    string valor = Request.Form[PrefijoSeleccion + i];
                    if (valor == null) continue;
                    eleccion[guias[i].Clave] = valor == ValorNuevo
                        ? new EleccionGuia
                        {
                            Nuevo = true,
                            IdConductor = Entero(Request.Form[PrefijoNuevo + "con_" + i]),
                            IdTracto = Entero(Request.Form[PrefijoNuevo + "tra_" + i]),
                            IdCarreta = Entero(Request.Form[PrefijoNuevo + "car_" + i]),
                            Fecha = DateTime.TryParse(Request.Form[PrefijoNuevo + "fec_" + i], out DateTime f) ? f : (DateTime?)null
                        }
                        : EleccionGuia.Programado(Entero(valor));
                }
                var mcis = new Dictionary<int, int?>();
                for (int i = 0; i < lectura.Mcis.Count; i++)
                {
                    string valor = Request.Form[PrefijoMci + i];
                    if (valor != null) mcis[i] = Entero(valor);
                }
                var gres = new Dictionary<int, string>();
                for (int i = 0; i < lectura.Gres.Count; i++)
                {
                    string valor = Request.Form[PrefijoGre + i];
                    if (valor != null) gres[i] = valor;
                }
                var errores = BandejaDocumentosService.AplicarEleccion(lectura, eleccion, mcis, gres);
                if (errores.Count > 0)
                {
                    MostrarVistaPrevia(lectura, errores);
                    CargarTablero();
                    return;
                }

                var r = BandejaDocumentosService.Registrar(lectura, Convert.ToString(Session["Nombre"]),
                    DocumentoHelper.RaizFisicaUploads(Server), EmpresaConfigHelper.ObtenerEmpresa().Ruc);
                bool algo = r.DespachosCompletados + r.DespachosCreados + r.FacturasFleteRegistradas + r.PdfAdjuntados
                            + r.CpicsRegistradas + r.ManifiestosAdjuntados + r.GresRegistradas > 0;
                if (!algo && r.Errores.Count > 0)
                {
                    MostrarVistaPrevia(lectura, r.Errores);
                    CargarTablero();
                    return;
                }

                Lectura = null;
                pnlVistaPrevia.Visible = false;
                var partes = new List<string>();
                if (r.DespachosCompletados > 0) partes.Add($"{r.DespachosCompletados} despacho(s) completado(s)");
                if (r.DespachosCreados > 0) partes.Add($"{r.DespachosCreados} despacho(s) nuevo(s)");
                if (r.CpicsRegistradas > 0) partes.Add($"{r.CpicsRegistradas} CPIC");
                if (r.ManifiestosAdjuntados > 0) partes.Add($"{r.ManifiestosAdjuntados} manifiesto(s)");
                if (r.GresRegistradas > 0) partes.Add($"{r.GresRegistradas} GRE transportista");
                if (r.FacturasCreadas > 0) partes.Add($"{r.FacturasCreadas} factura(s) del cliente registrada(s)");
                if (r.PdfAdjuntados > 0) partes.Add($"{r.PdfAdjuntados} PDF de factura adjuntado(s)");
                if (r.FacturasFleteRegistradas > 0) partes.Add($"{r.FacturasFleteRegistradas} factura(s) de flete");
                string texto = partes.Count == 0 ? "No había nada nuevo que registrar." : "Listo: " + string.Join(", ", partes) + ".";
                bool todoEntro = r.Errores.Count == 0 && r.SinRegistrar.Count == 0;
                var html = new StringBuilder($"<div class='alert alert-{(todoEntro ? "success" : "warning")} bd-alerta'><strong>{H(texto)}</strong>");
                if (r.Detalle.Count > 0) html.Append("<ul class='mb-0 mt-1'>").Append(string.Concat(r.Detalle.Select(d => $"<li>{H(d)}</li>"))).Append("</ul>");
                if (r.Errores.Count > 0) html.Append("<div class='mt-2'><strong>No se pudo registrar:</strong></div><ul class='mb-0'>").Append(string.Concat(r.Errores.Select(d => $"<li>{H(d)}</li>"))).Append("</ul>");
                if (r.SinRegistrar.Count > 0) html.Append("<div class='mt-2'><strong>Quedó sin registrar:</strong></div><ul class='mb-0'>").Append(string.Concat(r.SinRegistrar.Select(d => $"<li>{H(d)}</li>"))).Append("</ul>");
                html.Append("</div>");
                pnlMensaje.Visible = true;
                litMensaje.Text = html.ToString();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Bandeja de documentos: error al registrar");
                MostrarMensaje("No se pudo registrar. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
                MostrarVistaPrevia(lectura, null);
            }
            CargarTablero();
        }

        /// <summary>Guías cuyo despacho se puede elegir, en el orden en que se dibujan los selectores.</summary>
        private static List<GuiaBandeja> GuiasEditables(LecturaBandeja l) =>
            l.Pedidos.SelectMany(GuiasEnOrden).Where(g => g.Estado != EstadoGuiaBandeja.YaRegistrada).ToList();

        /// <summary>Orden de las guías de un pedido en la tabla (el mismo con que se numeran los selectores).</summary>
        private static IEnumerable<GuiaBandeja> GuiasEnOrden(PedidoBandeja p) => p.Guias.OrderBy(x => x.Tracto.NumeroGuia ?? x.Tracto.NumeroTransporte);

        // ── Vista previa ─────────────────────────────────────────────────────

        private void MostrarVistaPrevia(LecturaBandeja l, List<string> errores)
        {
            pnlVistaPrevia.Visible = true;
            var guias = l.Guias.ToList();
            int porDescarte = guias.Count(g => g.PorDescarte);
            int unidas = guias.Count(g => g.Estado == EstadoGuiaBandeja.Unida) - porDescarte;
            int nuevas = guias.Count(g => g.Estado == EstadoGuiaBandeja.Nueva);
            int sin = guias.Count(g => g.Estado == EstadoGuiaBandeja.SinProgramado);
            int ya = guias.Count(g => g.Estado == EstadoGuiaBandeja.YaRegistrada);
            int fletes = l.FacturasFlete.Count;

            var resumen = new List<string>();
            if (l.Pedidos.Count > 0) resumen.Add($"{l.Pedidos.Count} pedido(s), {guias.Count} guía(s)");
            if (unidas > 0) resumen.Add($"<span class='bd-txt-ok'>{unidas} se completan</span>");
            if (porDescarte > 0) resumen.Add($"<span class='bd-txt-aviso'>{porDescarte} por confirmar (unidas por descarte)</span>");
            if (nuevas > 0) resumen.Add($"<span class='bd-txt-aviso'>{nuevas} despacho(s) nuevo(s) por revisar</span>");
            if (sin > 0) resumen.Add($"<span class='bd-txt-aviso'>{sin} sin despacho</span>");
            int cpics = l.Cpics.Count(c => c.Estado == EstadoAduanaBandeja.Registrar);
            int mcis = l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.Registrar || m.Estado == EstadoAduanaBandeja.TrasSuCpic);
            int mcisPorElegir = l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.Elegir);
            if (cpics > 0) resumen.Add($"<span class='bd-txt-ok'>{cpics} CPIC</span>");
            if (mcis > 0) resumen.Add($"<span class='bd-txt-ok'>{mcis} manifiesto(s)</span>");
            if (mcisPorElegir > 0) resumen.Add($"<span class='bd-txt-aviso'>{mcisPorElegir} manifiesto(s) por elegir</span>");
            int gresUnidas = l.Gres.Count(g => g.Estado == EstadoGreBandeja.Unida);
            int gresNuevas = l.Gres.Count(g => g.Estado == EstadoGreBandeja.Nueva);
            int gresRevisar = l.Gres.Count(g => g.Estado == EstadoGreBandeja.Elegir || g.Estado == EstadoGreBandeja.NoSeRegistra);
            if (gresUnidas > 0) resumen.Add($"<span class='bd-txt-ok'>{gresUnidas} GRE en su carga</span>");
            if (gresNuevas > 0) resumen.Add($"<span class='bd-txt-aviso'>{gresNuevas} GRE con despacho nuevo</span>");
            if (gresRevisar > 0) resumen.Add($"<span class='bd-txt-aviso'>{gresRevisar} GRE por revisar</span>");
            if (ya > 0) resumen.Add($"{ya} ya registrada(s)");
            int pdfs = l.Pedidos.Count(p => p.FaltaPdfQueTrae);
            if (pdfs > 0) resumen.Add($"<span class='bd-txt-ok'>{pdfs} PDF de factura por adjuntar</span>");
            if (fletes > 0) resumen.Add($"{fletes} factura(s) de flete");
            if (l.NoReconocidos.Count > 0) resumen.Add($"<span class='bd-txt-aviso'>{l.NoReconocidos.Count} archivo(s) con observaciones</span>");
            litResumenPrevia.Text = "<div class='bd-previa-resumen'>" + string.Join(" &#xB7; ", resumen) + "</div>";

            btnRegistrar.Enabled = l.HayAlgoQueRegistrar;
            btnRegistrar.Text = l.HayAlgoQueRegistrar ? "Registrar" : "Nada nuevo que registrar";

            var sb = new StringBuilder();
            if (errores != null && errores.Count > 0)
                sb.Append("<div class='alert alert-danger bd-alerta'><strong>Corrija antes de registrar:</strong><ul class='mb-0'>")
                  .Append(string.Concat(errores.Select(x => $"<li>{H(x)}</li>"))).Append("</ul></div>");

            if (l.NoReconocidos.Count > 0)
                sb.Append("<div class='bd-bloque bd-bloque-aviso'><div class='bd-bloque-titulo'><i class='fas fa-exclamation-triangle mr-1'></i>Observaciones de los archivos</div><ul class='mb-0'>")
                  .Append(string.Concat(l.NoReconocidos.Select(x => $"<li>{H(x)}</li>"))).Append("</ul></div>");

            // Los selectores se numeran por GuiasEditables (como los lee btnRegistrar_Click), no por el orden en pantalla.
            var indices = GuiasEditables(l).Select((g, i) => new { g, i }).ToDictionary(x => x.g, x => x.i);
            var catalogos = l.Guias.Any(g => g.Estado == EstadoGuiaBandeja.Nueva || g.Estado == EstadoGuiaBandeja.SinProgramado)
                ? new Catalogos() : null;

            // Primero lo que necesita al usuario; lo conforme queda plegado (sus selectores se envían igual).
            // Si hay errores, todo abierto: el que hay que corregir puede estar en un bloque sin observaciones.
            var bloques = new List<KeyValuePair<bool, Func<bool, string>>>();
            void Bloque(bool revisar, Func<bool, string> html) => bloques.Add(new KeyValuePair<bool, Func<bool, string>>(revisar, html));
            foreach (var p in l.Pedidos)
                Bloque(PedidoPorRevisar(p), abierto => HtmlPedido(l, p, indices, catalogos, abierto));
            if (l.Cpics.Count > 0) Bloque(l.Cpics.Any(c => c.PorRevisar), abierto => HtmlCpics(l, abierto));
            if (l.Mcis.Count > 0) Bloque(l.Mcis.Any(m => m.PorRevisar), abierto => HtmlMcis(l, abierto));
            if (l.Gres.Count > 0) Bloque(l.Gres.Any(g => g.PorRevisar), abierto => HtmlGres(l, abierto));
            if (l.FacturasFlete.Count > 0) Bloque(l.FacturasFlete.Any(FletePorRevisar), abierto => HtmlFacturasFlete(l, abierto));

            bool abrirTodo = errores != null && errores.Count > 0;
            var porRevisar = bloques.Where(b => b.Key).ToList();
            var sinObservaciones = bloques.Where(b => !b.Key).ToList();
            if (porRevisar.Count > 0)
            {
                sb.Append($"<div class='bd-grupo bd-grupo-revisar'><i class='fas fa-exclamation-circle mr-1'></i>Para revisar ({porRevisar.Count})</div>");
                foreach (var b in porRevisar) sb.Append(b.Value(true));
            }
            if (sinObservaciones.Count > 0)
            {
                sb.Append($"<div class='bd-grupo'><i class='fas fa-check-circle mr-1'></i>Sin observaciones ({sinObservaciones.Count}): ábralos si quiere ver el detalle</div>");
                foreach (var b in sinObservaciones) sb.Append(b.Value(abrirTodo));
            }
            if (bloques.Count == 0)
                sb.Append("<div class='bd-vacio'>No se reconoció ningún documento.</div>");
            litVistaPrevia.Text = sb.ToString();
        }

        private static bool PedidoPorRevisar(PedidoBandeja p) =>
            p.Avisos.Count > 0 || p.Guias.Any(g => g.PorRevisar || (g.Estado != EstadoGuiaBandeja.YaRegistrada && g.Avisos.Count > 0));

        private static bool FletePorRevisar(FacturaFleteBandeja f) =>
            f.Estado == EstadoFacturaFleteBandeja.NoValida || (f.Estado == EstadoFacturaFleteBandeja.Nueva && !string.IsNullOrWhiteSpace(f.Detalle));

        /// <summary>Orden de las filas de un pedido: lo que hay que revisar, lo que trae avisos, lo conforme y lo ya registrado.</summary>
        private static int Prioridad(GuiaBandeja g) =>
            g.PorRevisar ? 0 : g.Estado == EstadoGuiaBandeja.YaRegistrada ? 3 : g.Avisos.Count > 0 ? 1 : 2;

        private static string AperturaBloque(bool abierto) => $"<details class='bd-pedido'{(abierto ? " open" : "")}>";

        private const string Flecha = "<i class='fas fa-chevron-right bd-plegar'></i>";

        /// <summary>Pastillas con los conteos de un bloque, para saber qué tiene sin abrirlo.</summary>
        private static string Conteos(params (int cuantos, string texto, string tipo)[] items) =>
            "<div class='bd-conteos'>" + string.Concat(items.Where(x => x.cuantos > 0).Select(x => Chip($"{x.cuantos} {x.texto}", x.tipo))) + "</div>";

        /// <summary>Choferes, tractos y carretas para los despachos nuevos (los inactivos al final, marcados).</summary>
        private sealed class Catalogos
        {
            public readonly List<KeyValuePair<int, string>> Conductores, Tractos, Carretas;
            public Catalogos()
            {
                Conductores = Lista(@"SELECT idConductor, LTRIM(RTRIM(CONCAT(nombre, ' ', apPaterno, ' ', apMaterno))) + CASE WHEN activo = 1 THEN '' ELSE ' (inactivo)' END
                                      FROM Conductor ORDER BY activo DESC, nombre, apPaterno");
                Tractos = Lista("SELECT idTracto, LTRIM(RTRIM(placaTracto)) + CASE WHEN activo = 1 THEN '' ELSE ' (inactivo)' END FROM Tracto ORDER BY activo DESC, placaTracto");
                Carretas = Lista("SELECT idCarreta, LTRIM(RTRIM(placaCarreta)) + CASE WHEN activo = 1 THEN '' ELSE ' (inactiva)' END FROM Carreta ORDER BY activo DESC, placaCarreta");
            }
            private static List<KeyValuePair<int, string>> Lista(string sql) =>
                DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>()
                    .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r[0]), System.Text.RegularExpressions.Regex.Replace(Convert.ToString(r[1]), @"\s+", " "))).ToList();
        }

        private static string Opciones(List<KeyValuePair<int, string>> lista, int? elegido, string vacio)
        {
            var sb = new StringBuilder($"<option value=''>{H(vacio)}</option>");
            foreach (var kv in lista)
                sb.Append($"<option value='{kv.Key}'{(elegido == kv.Key ? " selected" : "")}>{H(kv.Value)}</option>");
            return sb.ToString();
        }

        /// <summary>Fila plegable con chofer, tracto, carreta y fecha del despacho nuevo (se muestra al elegir "Crear despacho nuevo").</summary>
        private static string FilaNuevo(GuiaBandeja g, int indice, Catalogos c)
        {
            bool visible = g.Estado == EstadoGuiaBandeja.Nueva;
            return $"<tr class='bd-fila-nuevo' data-indice='{indice}'{(visible ? "" : " hidden")}><td></td><td colspan='5'>" +
                   "<div class='bd-nuevo'><span class='bd-nuevo-titulo'><i class='fas fa-plus-circle mr-1'></i>Despacho nuevo</span>" +
                   $"<label>Chofer<select name='{PrefijoNuevo}con_{indice}' class='form-control form-control-sm'>{Opciones(c.Conductores, g.NuevoIdConductor, "— Elija —")}</select></label>" +
                   $"<label>Tracto<select name='{PrefijoNuevo}tra_{indice}' class='form-control form-control-sm'>{Opciones(c.Tractos, g.NuevoIdTracto, "—")}</select></label>" +
                   $"<label>Carreta<select name='{PrefijoNuevo}car_{indice}' class='form-control form-control-sm'>{Opciones(c.Carretas, g.NuevoIdCarreta, "—")}</select></label>" +
                   $"<label>Fecha de carga<input type='date' name='{PrefijoNuevo}fec_{indice}' class='form-control form-control-sm' value='{g.NuevaFecha:yyyy-MM-dd}' /></label>" +
                   "</div></td></tr>";
        }

        private string HtmlPedido(LecturaBandeja l, PedidoBandeja p, Dictionary<GuiaBandeja, int> indices, Catalogos catalogos, bool abierto)
        {
            var imp = p.Pedido;
            var f = imp.Factura;
            var sb = new StringBuilder(AperturaBloque(abierto));
            sb.Append("<summary class='bd-pedido-cabecera'><div>")
              .Append($"<div class='bd-pedido-titulo'>{Flecha}{H(imp.NombreCliente ?? "Cliente no reconocido")} &#xB7; Pedido {H(imp.NumeroPedido ?? "sin número")}</div>")
              .Append($"<div class='bd-pedido-sub'><i class='far fa-file-archive mr-1'></i>{H(p.Archivo)}");
            if (!string.IsNullOrEmpty(imp.Destino)) sb.Append($" &#xB7; destino {H(imp.Destino)}");
            sb.Append("</div>")
              .Append(Conteos(
                  (p.Guias.Count(g => g.PorDescarte), "por confirmar", "aviso"),
                  (p.Guias.Count(g => g.Estado == EstadoGuiaBandeja.SinProgramado), "sin despacho", "aviso"),
                  (p.Guias.Count(g => g.Estado == EstadoGuiaBandeja.Nueva), "despacho(s) nuevo(s)", "aviso"),
                  (p.Guias.Count(g => g.Estado == EstadoGuiaBandeja.Unida && !g.PorDescarte), "se completan", "ok"),
                  (p.Guias.Count(g => g.Estado == EstadoGuiaBandeja.YaRegistrada), "ya registrada(s)", "gris")))
              .Append("</div><div class='bd-pedido-factura'>");
            if (f?.NumeroFactura != null)
            {
                string monto = f.ValorTotal.HasValue ? $" &#xB7; {(f.Moneda == "USD" ? "US$" : "S/")} {f.ValorTotal.Value.ToString("N2", Pe)}" : "";
                sb.Append($"<span class='bd-factura-num'><i class='fas fa-receipt mr-1'></i>{H(f.NumeroFactura)}</span>")
                  .Append($"<span class='bd-factura-det'>{(f.FechaEmision.HasValue ? f.FechaEmision.Value.ToString("dd/MM/yyyy") : "")}{monto}</span>")
                  .Append(p.IdFacturaExistente.HasValue ? Chip("Ya registrada", "gris") : Chip("Factura nueva", "ok"));
                bool seCrea = !p.IdFacturaExistente.HasValue && p.Guias.Any(g => g.Estado == EstadoGuiaBandeja.Unida || g.Estado == EstadoGuiaBandeja.Nueva);
                if (p.FacturaPdf != null && (p.FaltaPdfQueTrae || seCrea)) sb.Append(Chip("Se adjunta su PDF", "ok"));
                else if (p.IdFacturaExistente.HasValue && p.FacturaConPdf) sb.Append(Chip("Ya tiene PDF", "gris"));
            }
            else sb.Append(Chip("Sin factura del cliente", "aviso"));
            sb.Append("</div></summary>");

            if (p.Avisos.Count > 0)
                sb.Append("<ul class='bd-avisos bd-avisos-pedido'>").Append(string.Concat(p.Avisos.Select(a => $"<li>{H(a)}</li>"))).Append("</ul>");

            sb.Append("<div class='table-responsive'><table class='table table-sm bd-tabla'><thead><tr>")
              .Append("<th>Gu&#xED;a</th><th>Traslado</th><th>Seg&#xFA;n la gu&#xED;a</th><th class='bd-num'>Carga</th><th>Despacho que completa</th><th>Estado</th></tr></thead><tbody>");
            foreach (var g in GuiasEnOrden(p).OrderBy(Prioridad))
            {
                var t = g.Tracto;
                string clase = g.PorRevisar ? "bd-fila-aviso" : g.Estado == EstadoGuiaBandeja.YaRegistrada ? "bd-fila-gris" : "";
                sb.Append($"<tr class='{clase}'>")
                  .Append($"<td class='bd-cod'>{H(t.NumeroGuia ?? "N° transp. " + t.NumeroTransporte)}</td>")
                  .Append($"<td class='bd-cod'>{(t.FechaTraslado.HasValue ? t.FechaTraslado.Value.ToString("dd/MM") : "")}</td>")
                  .Append($"<td><span class='bd-cod'>{H(t.PlacaSistema ?? t.Placa)}</span><br><small class='text-muted'>{H(t.NombreConductorSistema ?? t.NombreChofer)}</small></td>")
                  .Append($"<td class='bd-num'>{t.CantidadTotal.ToString("N0", Pe)} bolsas<br><small class='text-muted'>{(t.PesoBrutoTotalKg / 1000m).ToString("N2", Pe)} t</small></td>");

                int? suIndice = null;
                if (g.Estado == EstadoGuiaBandeja.YaRegistrada)
                    sb.Append($"<td><small>{H(g.DespachoExistente)}</small></td><td>{Chip("Ya registrada", "gris")}</td>");
                else
                {
                    suIndice = indices[g];
                    sb.Append("<td>").Append(Selector(l, g, suIndice.Value)).Append("</td><td>");
                    sb.Append(g.PorDescarte ? Chip("Confirme el despacho", "aviso") + $"<div class='bd-regla'>Ni el tracto ni la carreta coinciden: {H(g.Regla)}</div>"
                            : g.Estado == EstadoGuiaBandeja.Unida ? Chip("Se completa", "ok") + $"<div class='bd-regla'>{H(g.Regla)}</div>"
                            : g.Estado == EstadoGuiaBandeja.Nueva ? Chip("Despacho nuevo", "aviso")
                            : Chip("Sin despacho", "aviso"));
                    sb.Append("</td>");
                }
                sb.Append("</tr>");
                if (suIndice.HasValue && catalogos != null) sb.Append(FilaNuevo(g, suIndice.Value, catalogos));
                if (g.Avisos.Count > 0)
                    sb.Append($"<tr class='{clase} bd-fila-avisos'><td></td><td colspan='5'><ul class='bd-avisos'>")
                      .Append(string.Concat(g.Avisos.Select(a => $"<li>{H(a)}</li>"))).Append("</ul></td></tr>");
            }
            sb.Append("</tbody></table></div></details>");
            return sb.ToString();
        }

        private static string Selector(LecturaBandeja l, GuiaBandeja g, int indice)
        {
            bool nuevo = g.Estado == EstadoGuiaBandeja.Nueva;
            var sb = new StringBuilder($"<select name='{PrefijoSeleccion}{indice}' class='form-control form-control-sm bd-selector' data-indice='{indice}'>");
            sb.Append($"<option value=''{(g.IdProgramado.HasValue || nuevo ? "" : " selected")}>— No registrar ahora —</option>");
            if (g.Candidatos.Count > 0) sb.Append("<optgroup label='Completar el despacho programado'>");
            foreach (int id in g.Candidatos)
            {
                if (!l.Programados.TryGetValue(id, out var p)) continue;
                sb.Append($"<option value='{id}'{(g.IdProgramado == id ? " selected" : "")}>{H(p.Texto)}</option>");
            }
            if (g.Candidatos.Count > 0) sb.Append("</optgroup>");
            sb.Append($"<option value='{ValorNuevo}'{(nuevo ? " selected" : "")}>+ Crear despacho nuevo</option>");
            return sb.Append("</select>").ToString();
        }

        private static string HtmlCpics(LecturaBandeja l, bool abierto)
        {
            var sb = new StringBuilder(AperturaBloque(abierto)).Append("<summary class='bd-pedido-cabecera'><div class='bd-pedido-titulo'>")
                .Append($"{Flecha}<i class='fas fa-shipping-fast mr-1'></i>Cartas de porte (CPIC)</div>")
                .Append(Conteos(
                    (l.Cpics.Count(c => c.Estado == EstadoAduanaBandeja.NoSeRegistra), "no se registra(n)", "mal"),
                    (l.Cpics.Count(c => c.Estado == EstadoAduanaBandeja.Registrar), "se registra(n)", "ok"),
                    (l.Cpics.Count(c => c.Estado == EstadoAduanaBandeja.YaRegistrado), "ya registrada(s)", "gris"),
                    (l.Cpics.Count(c => c.Estado == EstadoAduanaBandeja.OtraVersion), "otra versión", "gris")))
                .Append("</summary><div class='table-responsive'><table class='table table-sm bd-tabla'><thead><tr><th>CPIC</th><th>Archivo</th>")
                .Append("<th>Factura comercial</th><th>Pedido</th><th class='bd-num'>Flete</th><th>Estado</th><th>Detalle</th></tr></thead><tbody>");
            foreach (var c in l.Cpics.OrderByDescending(x => x.PorRevisar))
            {
                sb.Append($"<tr class='{(c.PorRevisar ? "bd-fila-aviso" : c.Estado == EstadoAduanaBandeja.OtraVersion || c.Estado == EstadoAduanaBandeja.YaRegistrado ? "bd-fila-gris" : "")}'>")
                  .Append($"<td class='bd-cod'>{H(c.Cpic.Numero ?? "?")}</td><td><small>{H(c.Archivo)}</small></td>")
                  .Append($"<td class='bd-cod'>{H(c.Cpic.FacturaComercial ?? "—")}</td><td class='bd-cod'>{H(c.Pedido ?? "—")}</td>")
                  .Append($"<td class='bd-num'>{(c.Cpic.ValorFlete.HasValue ? "US$ " + c.Cpic.ValorFlete.Value.ToString("N2", Pe) : "")}</td>")
                  .Append($"<td>{ChipAduana(c.Estado)}</td><td><small>{H(c.Detalle)}</small></td></tr>");
            }
            return sb.Append("</tbody></table></div></details>").ToString();
        }

        private static string HtmlMcis(LecturaBandeja l, bool abierto)
        {
            var sb = new StringBuilder(AperturaBloque(abierto)).Append("<summary class='bd-pedido-cabecera'><div class='bd-pedido-titulo'>")
                .Append($"{Flecha}<i class='fas fa-passport mr-1'></i>Manifiestos de carga (MCI)</div>")
                .Append(Conteos(
                    (l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.Elegir), "por elegir", "aviso"),
                    (l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.NoSeRegistra), "no se registra(n)", "mal"),
                    (l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.Registrar || m.Estado == EstadoAduanaBandeja.TrasSuCpic), "se registra(n)", "ok"),
                    (l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.YaRegistrado), "ya registrado(s)", "gris"),
                    (l.Mcis.Count(m => m.Estado == EstadoAduanaBandeja.OtraVersion), "otra versión", "gris")))
                .Append("</summary><div class='table-responsive'><table class='table table-sm bd-tabla'><thead><tr><th>MCI</th><th>CPIC</th><th>Seg&#xFA;n el MCI</th>")
                .Append("<th>Tipo</th><th>Despacho</th><th>Estado</th><th>Detalle</th></tr></thead><tbody>");
            // El nombre del selector lleva la posición en l.Mcis, no la fila en pantalla.
            foreach (int i in Enumerable.Range(0, l.Mcis.Count).OrderByDescending(x => l.Mcis[x].PorRevisar))
            {
                var m = l.Mcis[i];
                bool editable = m.Estado == EstadoAduanaBandeja.Registrar || m.Estado == EstadoAduanaBandeja.Elegir;
                string despacho;
                if (editable && m.Candidatos.Count > 0)
                {
                    var sel = new StringBuilder($"<select name='{PrefijoMci}{i}' class='form-control form-control-sm bd-selector'>")
                        .Append($"<option value=''{(m.IdDespacho.HasValue ? "" : " selected")}>— No adjuntar —</option>");
                    foreach (var kv in m.Candidatos)
                        sel.Append($"<option value='{kv.Key}'{(m.IdDespacho == kv.Key ? " selected" : "")}>{H(kv.Value)}</option>");
                    despacho = sel.Append("</select>").ToString();
                }
                else despacho = m.IdDespacho.HasValue && m.Candidatos.TryGetValue(m.IdDespacho.Value, out string t) ? $"<small>{H(t)}</small>" : "—";
                sb.Append($"<tr class='{(m.PorRevisar ? "bd-fila-aviso" : editable || m.Estado == EstadoAduanaBandeja.TrasSuCpic ? "" : "bd-fila-gris")}'>")
                  .Append($"<td class='bd-cod'>{H(m.Mci.Numero ?? "?")}</td><td class='bd-cod'>{H(m.Mci.CartaPorte ?? "—")}</td>")
                  .Append($"<td><span class='bd-cod'>{H(m.Mci.PlacaTracto)} / {H(m.Mci.PlacaCarreta)}</span><br><small class='text-muted'>{H(m.Mci.Conductor)}</small></td>")
                  .Append($"<td>{(m.Mci.EsRetorno ? "Retorno" : "Cruce")}</td><td>{despacho}</td>")
                  .Append($"<td>{ChipAduana(m.Estado)}</td><td><small>{H(m.Detalle)}</small></td></tr>");
            }
            return sb.Append("</tbody></table></div></details>").ToString();
        }

        private static string HtmlGres(LecturaBandeja l, bool abierto)
        {
            var sb = new StringBuilder(AperturaBloque(abierto)).Append("<summary class='bd-pedido-cabecera'><div class='bd-pedido-titulo'>")
                .Append($"{Flecha}<i class='fas fa-truck mr-1'></i>Gu&#xED;as transportista (GRE de SUNAT)</div>")
                .Append(Conteos(
                    (l.Gres.Count(g => g.Estado == EstadoGreBandeja.Elegir), "por elegir", "aviso"),
                    (l.Gres.Count(g => g.Estado == EstadoGreBandeja.NoSeRegistra), "no se registra(n)", "mal"),
                    (l.Gres.Count(g => g.Estado == EstadoGreBandeja.Nueva), "despacho(s) nuevo(s)", "aviso"),
                    (l.Gres.Count(g => g.Estado == EstadoGreBandeja.Unida), "en su carga", "ok"),
                    (l.Gres.Count(g => g.Estado == EstadoGreBandeja.YaRegistrada), "ya registrada(s)", "gris")))
                .Append("</summary><div class='table-responsive'><table class='table table-sm bd-tabla'><thead><tr><th>GRE</th><th>Traslado</th>")
                .Append("<th>Cliente</th><th>Gu&#xED;a remitente</th><th>Seg&#xFA;n la GRE</th><th>Carga</th><th>Estado</th><th>Detalle</th></tr></thead><tbody>");
            // El nombre del selector lleva la posición en l.Gres, no la fila en pantalla.
            foreach (int i in Enumerable.Range(0, l.Gres.Count).OrderByDescending(x => l.Gres[x].PorRevisar).ThenBy(x => l.Gres[x].Gre.FechaTraslado))
            {
                var g = l.Gres[i];
                var gre = g.Gre;
                bool editable = g.Estado == EstadoGreBandeja.Unida || g.Estado == EstadoGreBandeja.Nueva || g.Estado == EstadoGreBandeja.Elegir;
                string carga;
                if (editable)
                {
                    var sel = new StringBuilder($"<select name='{PrefijoGre}{i}' class='form-control form-control-sm bd-selector'>")
                        .Append($"<option value=''{(g.Estado == EstadoGreBandeja.Elegir ? " selected" : "")}>— No registrar ahora —</option>");
                    foreach (var kv in g.Candidatos)
                        sel.Append($"<option value='{kv.Key}'{(g.Estado == EstadoGreBandeja.Unida && g.IdDespacho == kv.Key ? " selected" : "")}>{H(kv.Value)}</option>");
                    if (g.PuedeCrear)
                        sel.Append($"<option value='{BandejaDocumentosService.GreNuevo}'{(g.Estado == EstadoGreBandeja.Nueva ? " selected" : "")}>+ Crear despacho nuevo</option>");
                    carga = sel.Append("</select>").ToString() + (g.Regla != null && g.Estado == EstadoGreBandeja.Unida ? $"<div class='bd-regla'>{H(g.Regla)}</div>" : "");
                }
                else carga = "—";
                string estado = g.Estado == EstadoGreBandeja.Unida ? Chip("Se pone en su carga", "ok")
                              : g.Estado == EstadoGreBandeja.Nueva ? Chip("Despacho nuevo", "aviso")
                              : g.Estado == EstadoGreBandeja.Elegir ? Chip("Elija la carga", "aviso")
                              : g.Estado == EstadoGreBandeja.YaRegistrada ? Chip("Ya registrada", "gris") : Chip("No se registra", "mal");
                sb.Append($"<tr class='{(g.PorRevisar ? "bd-fila-aviso" : g.Estado == EstadoGreBandeja.YaRegistrada ? "bd-fila-gris" : "")}'>")
                  .Append($"<td class='bd-cod'>{H(gre.Numero ?? "?")}<br><small class='text-muted'>{H(g.Archivo)}</small></td>")
                  .Append($"<td class='bd-cod'>{(gre.FechaTraslado.HasValue ? gre.FechaTraslado.Value.ToString("dd/MM/yyyy") : "")}</td>")
                  .Append($"<td>{H(g.Cliente ?? gre.NombreRemitente)}<br><small class='text-muted'>a {H(gre.NombreDestinatario)}</small></td>")
                  .Append($"<td class='bd-cod'>{H(gre.GuiasRemitenteTexto ?? "—")}</td>")
                  .Append($"<td><span class='bd-cod'>{H(gre.PlacaTracto)} / {H(gre.PlacaCarreta)}</span><br><small class='text-muted'>{H(g.Conductor ?? gre.NombreConductor)}</small></td>")
                  .Append($"<td>{carga}</td><td>{estado}</td><td><small>{H(g.Detalle)}</small></td></tr>");
            }
            return sb.Append("</tbody></table></div></details>").ToString();
        }

        private static string ChipAduana(EstadoAduanaBandeja e)
        {
            switch (e)
            {
                case EstadoAduanaBandeja.Registrar: return Chip("Se registra", "ok");
                case EstadoAduanaBandeja.Elegir: return Chip("Elija el despacho", "aviso");
                case EstadoAduanaBandeja.TrasSuCpic: return Chip("Se registra con su CPIC", "ok");
                case EstadoAduanaBandeja.YaRegistrado: return Chip("Ya registrado", "gris");
                case EstadoAduanaBandeja.OtraVersion: return Chip("Otra versión", "gris");
                default: return Chip("No se registra", "mal");
            }
        }

        private static string HtmlFacturasFlete(LecturaBandeja l, bool abierto)
        {
            var sb = new StringBuilder(AperturaBloque(abierto)).Append("<summary class='bd-pedido-cabecera'><div class='bd-pedido-titulo'>")
                .Append($"{Flecha}<i class='fas fa-file-invoice-dollar mr-1'></i>Facturas de flete (SUNAT)</div>")
                .Append(Conteos(
                    (l.FacturasFlete.Count(f => f.Estado == EstadoFacturaFleteBandeja.NoValida), "no se registra(n)", "mal"),
                    (l.FacturasFlete.Count(f => f.Estado == EstadoFacturaFleteBandeja.Nueva), "nueva(s)", "ok"),
                    (l.FacturasFlete.Count(f => f.Estado == EstadoFacturaFleteBandeja.YaRegistrada), "ya registrada(s)", "gris")))
                .Append("</summary><div class='table-responsive'><table class='table table-sm bd-tabla'><thead><tr><th>Factura</th><th>Emisi&#xF3;n</th>")
                .Append("<th>Cliente</th><th>Factura comercial</th><th class='bd-num'>Importe</th><th>Estado</th><th>Observaciones</th></tr></thead><tbody>");
            foreach (var x in l.FacturasFlete.OrderByDescending(FletePorRevisar))
            {
                var f = x.Factura;
                string estado = x.Estado == EstadoFacturaFleteBandeja.Nueva ? Chip("Nueva", "ok")
                              : x.Estado == EstadoFacturaFleteBandeja.YaRegistrada ? Chip("Ya registrada", "gris") : Chip("No se registra", "mal");
                sb.Append("<tr>")
                  .Append($"<td class='bd-cod'>{H(f.NumeroFactura)}</td>")
                  .Append($"<td class='bd-cod'>{(f.FechaEmision.HasValue ? f.FechaEmision.Value.ToString("dd/MM/yyyy") : "")}</td>")
                  .Append($"<td>{H(f.NombreCliente)}</td>")
                  .Append($"<td class='bd-cod'>{H(f.NumeroFacturaComercial ?? "—")}</td>")
                  .Append($"<td class='bd-num'>{(f.ImporteTotal.HasValue ? H((f.Moneda == "PEN" ? "S/ " : "US$ ") + f.ImporteTotal.Value.ToString("N2", Pe)) : "")}</td>")
                  .Append($"<td>{estado}</td><td><small>{H(x.Detalle)}</small></td></tr>");
            }
            return sb.Append("</tbody></table></div></details>").ToString();
        }

        // ── Tablero ──────────────────────────────────────────────────────────

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            if (Lectura != null) MostrarVistaPrevia(Lectura, null);
            CargarTablero();
        }

        private void CargarTablero()
        {
            try
            {
                DateTime? desde = DateTime.TryParse(txtDesde.Text, out DateTime d) ? d : (DateTime?)null;
                DateTime? hasta = DateTime.TryParse(txtHasta.Text, out DateTime h) ? h : (DateTime?)null;
                int? idCliente = int.TryParse(ddlCliente.SelectedValue, out int c) ? c : (int?)null;
                string texto = (txtBuscar.Text ?? "").Trim();
                string pedido = null, factura = null, chofer = null;
                if (texto.Length > 0)
                {
                    if (texto.All(char.IsDigit)) pedido = texto;
                    else if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"^[A-Za-z]\d{3}-")) factura = texto;
                    else chofer = texto;
                }
                string documentos = string.IsNullOrEmpty(ddlVer.SelectedValue) ? null : ddlVer.SelectedValue;

                var lotes = ListaDespachosService.ObtenerLotesRegistrados(idCliente, null, null, pedido, desde, hasta, "ACTIVO",
                        numeroFactura: factura, nombreConductor: chofer, documentos: documentos)
                    .OrderBy(x => x.NombreCliente).ThenBy(x => x.FechaProgramacion).ToList();
                litContadorTablero.Text = $"<span class='bd-contador'>{lotes.Count} lote(s)</span>";
                litTablero.Text = HtmlTablero(lotes);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Bandeja de documentos: error al cargar el tablero");
                litTablero.Text = $"<div class='alert alert-danger bd-alerta'>No se pudo cargar el tablero. {H(MensajeErrorHelper.ParaUsuario(ex))}</div>";
            }
        }

        private string HtmlTablero(List<LoteRegistrado> lotes)
        {
            if (lotes.Count == 0)
                return "<div class='bd-vacio'><i class='fas fa-check-circle mr-1 text-success'></i>No hay lotes con esos filtros.</div>";
            DateTime hoy = FechaHelper.Hoy();
            var sb = new StringBuilder();
            foreach (var cliente in lotes.GroupBy(x => x.NombreCliente))
            {
                int pendientes = cliente.Count(x => !EstadoDocumental.DeLote(x).Completo);
                sb.Append($"<div class='bd-cliente'><div class='bd-cliente-titulo'>{H(cliente.Key)}")
                  .Append($"<span class='bd-cliente-cuenta'>{cliente.Count()} lote(s){(pendientes > 0 ? $" &#xB7; {pendientes} con pendientes" : "")}</span></div>")
                  .Append("<div class='table-responsive'><table class='table table-sm bd-tabla bd-tabla-tablero'><thead><tr>")
                  .Append("<th>Carga</th><th>Pedido</th><th>Planta</th><th class='bd-num'>Unid.</th><th>Documentos</th><th>Antig&#xFC;edad</th><th></th></tr></thead><tbody>");
                foreach (var lote in cliente)
                {
                    var e = EstadoDocumental.DeLote(lote);
                    bool atrasado = e.Atrasado(lote.FechaProgramacion, hoy);
                    string url = ResolveUrl("~/Views/ListaDespachos.aspx") + "?lote=" + HttpUtility.UrlEncode(lote.IdLoteVirtual);
                    sb.Append(atrasado ? "<tr class='bd-fila-atrasada'>" : "<tr>")
                      .Append($"<td class='bd-cod'>{lote.FechaProgramacion:dd/MM/yyyy}</td>")
                      .Append($"<td class='bd-cod'>{H(string.IsNullOrEmpty(lote.NumeroPedido) ? "—" : lote.NumeroPedido)}</td>")
                      .Append($"<td>{H(lote.PlantaOperacion)}</td>")
                      .Append($"<td class='bd-num'>{e.Despachos}</td>")
                      .Append($"<td class='bd-docs'>{ChipsDocumentos(lote, e)}</td>")
                      .Append($"<td class='{(atrasado ? "bd-txt-mal" : "")}'>{H(e.Antiguedad(lote.FechaProgramacion, hoy))}</td>")
                      .Append($"<td class='text-right'><a class='btn btn-outline-primary btn-sm' href='{H(url)}'>Abrir</a></td></tr>");
                }
                sb.Append("</tbody></table></div></div>");
            }
            return sb.ToString();
        }

        /// <summary>Una pastilla por documento: verde si está, ámbar si falta (con cuántos faltan).</summary>
        private static string ChipsDocumentos(LoteRegistrado lote, EstadoDocumental e)
        {
            var sb = new StringBuilder();
            void Doc(string nombre, int faltan, bool aplica = true)
            {
                if (!aplica) return;
                bool ok = faltan == 0;
                string detalle = ok || faltan >= e.Despachos ? "" : $" {e.Despachos - faltan}/{e.Despachos}";
                sb.Append($"<span class='bd-doc {(ok ? "bd-doc-ok" : "bd-doc-falta")}'><i class='fas {(ok ? "fa-check" : "fa-times")}'></i>{H(nombre)}{detalle}</span>");
            }
            bool carga = lote.TipoOperacion == "CARGA";
            // Cada cliente manda lo suyo: Vitapro el ZIP (pedido, factura con PDF, guías); Crisar y Novopan la CPIC
            // (con su factura comercial); Polinplast nuestra GRE transportista (cita su guía del remitente).
            bool pedido = e.Perfil == "PEDIDO", cpic = e.Perfil == "CPIC", guia = e.Perfil == "GUIA";
            Doc("Pedido", e.SinPedido, carga && pedido);
            Doc(cpic ? "Factura comercial" : "Factura", e.SinFactura, carga && (pedido || cpic));
            Doc("PDF factura", e.SinPdfFactura, carga && pedido && e.SinFactura < e.Despachos);   // sin factura todavía, su PDF no aplica
            Doc(guia ? "GRE transportista" : "Guías", e.SinGuia, carga && (pedido || guia));
            Doc("CPIC", e.SinCPIC, lote.EsInternacional || (carga && cpic));
            // Un lote nacional no exige CPIC, pero si ya tiene una (la de su pedido) se espera su PDF (EstadoDocumental).
            Doc("PDF CPIC", e.SinPdfCPIC, lote.EsInternacional || !string.IsNullOrEmpty(lote.NumeroCPIC));
            Doc("MCI", e.SinManifiesto, lote.EsInternacional || (carga && cpic));
            return sb.ToString();
        }

        // ── Utilidades ───────────────────────────────────────────────────────

        private static string Chip(string texto, string tipo) => $"<span class='bd-chip bd-chip-{tipo}'>{H(texto)}</span>";

        private static string H(string s) => HttpUtility.HtmlEncode(s ?? "");

        private void MostrarMensaje(string mensaje, string tipo)
        {
            pnlMensaje.Visible = true;
            litMensaje.Text = $"<div class='alert alert-{tipo} bd-alerta'>{H(mensaje)}</div>";
        }
    }
}
