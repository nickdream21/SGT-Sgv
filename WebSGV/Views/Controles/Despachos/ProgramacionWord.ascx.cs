using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using static WebSGV.Views.Controles.Despachos.ControlesFormulario;

namespace WebSGV.Views.Controles.Despachos
{
    /// <summary>
    /// "Programar con el Word" de RegistroDespacho (E6-23). Flujo: "Leer documentos" con solo el Word → vista previa
    /// (en la sesión, <see cref="EstadoRegistroDespacho.Programacion"/>) → "Programar despachos" crea las cargas sin
    /// documentos y el viaje del seguimiento. Cuando llega el correo, "Leer documentos" une cada guía con su despacho
    /// programado. La página pinta los avisos (<see cref="Aviso"/>) y muestra u oculta lo suyo con los demás eventos.
    /// <para>
    /// Los repetidores van sin ViewState (M-169, parte 1): en un postback con la vista previa a la vista se vuelven a
    /// enlazar desde la sesión en <see cref="Page_Load"/>, antes de que ASP.NET lea el formulario por segunda vez; lo
    /// que el usuario eligió en cada fila llega por los datos enviados.
    /// </para>
    /// </summary>
    public partial class ProgramacionWord : UserControl
    {
        /// <summary>Un aviso para el usuario (texto plano) y su tipo ("success", "warning", "danger", "info").</summary>
        public sealed class AvisoEventArgs : EventArgs
        {
            public AvisoEventArgs(string mensaje, string tipo) { Mensaje = mensaje; Tipo = tipo; }
            public string Mensaje { get; }
            public string Tipo { get; }
        }

        /// <summary>Aviso del recuadro de documentos (la página lo pinta en su lblMensajeImportacion).</summary>
        public event EventHandler<AvisoEventArgs> Aviso;

        /// <summary>Se mostró la vista previa: la página oculta su aviso, la vista previa del ZIP y el formulario manual.</summary>
        public event EventHandler Abierta;

        /// <summary>La vista previa se cerró sola (programada o vencida): la página vuelve a mostrar el formulario manual.</summary>
        public event EventHandler Cerrada;

        /// <summary>Se programaron despachos: la página recarga sus listas.</summary>
        public event EventHandler Programada;

        /// <summary>"Descartar": la página descarta todo lo del recuadro (también la importación) y lo avisa.</summary>
        public event EventHandler Descartada;

        /// <summary>Botones que la página registra como AsyncPostBackTrigger de UpdatePanelMain.</summary>
        public IEnumerable<Control> DisparadoresAsincronos => new Control[] { btnProgramarDespachos, btnDescartarProgramacion, btnRegistrarEntregas };

        /// <summary>La vista previa del Word está a la vista (paso 1 de la tira del día).</summary>
        public bool VistaPreviaVisible => pnlProgramacionNueva.Visible;

        private EstadoRegistroDespacho _estado;
        private EstadoRegistroDespacho Estado => _estado ?? (_estado = EstadoRegistroDespacho.De(Session));

        private ProgramacionNueva Actual
        {
            get => Estado.Programacion;
            set => Estado.Programacion = value;
        }

        // El formulario enviado es de la vista previa de la sesión (misma marca); si no, otra pestaña la cambió.
        private bool _vigente, _mostrada, _mostrarAlPintar;

        protected void Page_Load(object sender, EventArgs e)
        {
            var p = Actual;
            if (!IsPostBack || !pnlProgramacionNueva.Visible || p == null) return;
            _vigente = RegistroDespachoReglas.ProgramacionVigente(Request.Form[hfMarcaProg.UniqueID], p);
            // Con otra marca no se enlaza aquí: los datos enviados (de las filas de otra vista previa) se aplicarían a estas
            // filas. Se muestra la de la sesión, con el aviso, al pintar si ningún evento lo hizo antes (Disponible).
            _mostrarAlPintar = !_vigente;
            if (!_vigente) return;
            EnlazarEntregas(p);
            if (!p.SinCarga) EnlazarFilas(p);
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (_mostrarAlPintar && !_mostrada && pnlProgramacionNueva.Visible) Disponible(Actual);
        }

        #region Lo que llama la página

        /// <summary>Word de la programación: .docx de hasta 5 MB que sea de verdad un .docx. Null si está bien.</summary>
        public static string ValidarWord(ImportacionPedidoService.ArchivoSubido word)
        {
            if (word == null) throw new ArgumentNullException(nameof(word));
            return ArchivoSubidoHelper.Validar(word.Nombre, word.Contenido?.LongLength ?? 0, word.Contenido, ArchivoSubidoHelper.MaxWord, ".docx");
        }

        /// <summary>Lee el Word (solo, sin documentos del correo) y muestra su vista previa.</summary>
        public void Leer(ImportacionPedidoService.ArchivoSubido word)
        {
            if (word == null) throw new ArgumentNullException(nameof(word));
            string errorWord = ValidarWord(word);
            if (errorWord != null)
            {
                Avisar($"El Word {word.Nombre} no se puede usar: {errorWord}.", "warning");
                return;
            }

            ProgramacionNueva p;
            try { p = ProgramacionDespachoService.LeerWord(word.Contenido, word.Nombre); }
            catch (Exception ex)
            {
                Avisar("No se pudo leer la programación. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "LeerProgramacionNueva"), "warning");
                return;
            }

            Actual = p;
            Mostrar(p);
        }

        /// <summary>Al volver a la página, muestra la programación que quedó en la sesión. False si no hay.</summary>
        public bool Restaurar()
        {
            var p = Actual;
            if (p == null) return false;
            Mostrar(p);
            return true;
        }

        /// <summary>Quita la programación de la sesión y oculta la vista previa.</summary>
        public void Cerrar()
        {
            Actual = null;
            pnlProgramacionNueva.Visible = false;
        }

        #endregion

        #region Vista previa

        private void Mostrar(ProgramacionNueva p)
        {
            Abierta?.Invoke(this, EventArgs.Empty);
            pnlProgramacionNueva.Visible = true;
            hfMarcaProg.Value = p.Marca;
            _mostrada = true;

            pnlSeccionProg.Visible = p.SeccionesCarga.Count > 1;
            ddlSeccionProg.Items.Clear();
            for (int i = 0; i < p.SeccionesCarga.Count; i++)
                ddlSeccionProg.Items.Add(new ListItem(p.SeccionesCarga[i], i.ToString()));
            RestaurarSeleccion(ddlSeccionProg, p.IndiceSeccion.ToString());

            pnlEntregasTerceros.Visible = p.Entregas.Count > 0;
            btnRegistrarEntregas.Visible = p.Entregas.Any(x => x.IdSeguimiento.HasValue && !x.YaRegistrada);
            EnlazarEntregas(p);
            pnlCargaProg.Visible = btnProgramarDespachos.Visible = !p.SinCarga;
            if (p.SinCarga) return;

            litProgTitulo.Text = Html(p.Titulo);
            litProgArchivo.Text = Html(p.NombreArchivo);
            litProgUnidades.Text = $"{p.Filas.Count(f => !f.IdDespachoExistente.HasValue)} por programar" +
                (p.Filas.Any(f => f.IdDespachoExistente.HasValue) ? $" · {p.Filas.Count(f => f.IdDespachoExistente.HasValue)} ya programada(s)" : "");

            ddlClienteProg.Items.Clear();
            ddlClienteProg.Items.Add(new ListItem("-- Seleccione un cliente --", "0"));
            foreach (DataRow r in RegistroDespachoService.ObtenerClientesActivos().Rows)
                ddlClienteProg.Items.Add(new ListItem(r["nombre"].ToString(), r["idCliente"].ToString()));
            RestaurarSeleccion(ddlClienteProg, p.IdCliente?.ToString());

            ddlPlantaProg.Items.Clear();
            ddlPlantaProg.Items.Add(new ListItem("-- Seleccione planta --", "0"));
            foreach (DataRow r in RegistroDespachoService.ObtenerPlantasPorAmbito(p.EsInternacional).Rows)
                ddlPlantaProg.Items.Add(new ListItem(r["nombre"].ToString(), r["idPlanta"].ToString()));
            RestaurarSeleccion(ddlPlantaProg, p.IdPlanta?.ToString());

            txtFechaProg.Text = p.FechaCarga?.ToString("yyyy-MM-dd") ?? string.Empty;
            chkExportacionProg.Checked = p.EsExportacion;

            pnlAvisosProg.Visible = p.AvisosGenerales.Count > 0;
            litAvisosProg.Text = "<ul class=\"mb-0 mt-1\">" + string.Concat(p.AvisosGenerales.Select(a => $"<li>{Html(a)}</li>")) + "</ul>";

            EnlazarFilas(p);
        }

        /// <summary>Filas de las unidades (sin ViewState: también en cada postback, desde la sesión).</summary>
        private void EnlazarFilas(ProgramacionNueva p)
        {
            rptProgramacionNueva.DataSource = p.Filas;
            rptProgramacionNueva.DataBind();
        }

        /// <summary>Descargas, entregas y retornos con carga del Word (sin ViewState: también en cada postback).</summary>
        private void EnlazarEntregas(ProgramacionNueva p)
        {
            rptEntregasTerceros.DataSource = p.Entregas.Select(RegistroDespachoReglas.VistaEntrega).ToList();
            rptEntregasTerceros.DataBind();
        }

        protected void rptProgramacionNueva_ItemDataBound(object sender, RepeaterItemEventArgs e)
        {
            if (e.Item.ItemType != ListItemType.Item && e.Item.ItemType != ListItemType.AlternatingItem) return;
            var f = (FilaProgramacionNueva)e.Item.DataItem;

            var ddlConductor = Llenar(e.Item, "ddlConductorProg", Conductores, "NombreCompleto", "idConductor", f.IdConductor);
            var ddlTracto = Llenar(e.Item, "ddlTractoProg", Tractos, "placaTracto", "idTracto", f.IdTracto);
            var ddlCarreta = Llenar(e.Item, "ddlCarretaProg", Carretas, "placaCarreta", "idCarreta", f.IdCarreta);

            ((TextBox)e.Item.FindControl("txtHoraProg")).Text = f.Hora?.ToString(@"hh\:mm") ?? string.Empty;

            bool existe = f.IdDespachoExistente.HasValue;
            var chk = (CheckBox)e.Item.FindControl("chkIncluirProg");
            chk.Checked = chk.Enabled = !existe;
            ddlConductor.Enabled = ddlTracto.Enabled = ddlCarreta.Enabled = !existe;

            var sb = new StringBuilder();
            if (existe)
                sb.Append(EstadoUiHelper.Sello("Ya programado", TonoEstado.Alerta) +
                          $"<small class=\"sgv-suave d-block mt-1\">El tracto o la carreta ya tiene despacho ese día (N° {f.IdDespachoExistente}).</small>");
            sb.Append(RegistroDespachoReglas.AvisosHtml(f.Avisos));
            if (sb.Length == 0) sb.Append(EstadoUiHelper.Sello("Listo", TonoEstado.Ok));
            ((Literal)e.Item.FindControl("litEstadoProg")).Text = sb.ToString();
        }

        /// <summary>Lista de una fila: "-- Seleccione --" y el catálogo, con lo propuesto por el Word elegido (o en rojo si falta).</summary>
        private static DropDownList Llenar(RepeaterItem item, string id, DataTable fuente, string texto, string valor, int? propuesto)
        {
            var ddl = (DropDownList)item.FindControl(id);
            ddl.Items.Add(new ListItem("-- Seleccione --", "0"));
            foreach (DataRow r in fuente.Rows)
                ddl.Items.Add(new ListItem(r[texto].ToString(), r[valor].ToString()));
            RestaurarSeleccion(ddl, propuesto?.ToString());
            if (!propuesto.HasValue) ddl.CssClass += " is-invalid";
            return ddl;
        }

        // Catálogos de las listas de cada fila: una consulta por petición aunque se enlace dos veces (Page_Load y un evento).
        private DataTable _conductores, _tractos, _carretas;
        private DataTable Conductores => _conductores ?? (_conductores = RegistroDespachoService.ObtenerConductoresActivos());
        private DataTable Tractos => _tractos ?? (_tractos = RegistroDespachoService.ObtenerTractosActivos());
        private DataTable Carretas => _carretas ?? (_carretas = RegistroDespachoService.ObtenerCarretasActivas());

        /// <summary>Libera los catálogos de las filas (CA2213).</summary>
        public override void Dispose()
        {
            _conductores?.Dispose();
            _tractos?.Dispose();
            _carretas?.Dispose();
            base.Dispose();
        }

        #endregion

        #region Eventos

        protected void ddlSeccionProg_SelectedIndexChanged(object sender, EventArgs e)
        {
            var actual = Actual;
            if (!Disponible(actual?.Programacion == null ? null : actual)) return;
            try
            {
                int.TryParse(ddlSeccionProg.SelectedValue, out int indice);
                var p = ProgramacionDespachoService.Proponer(actual.Programacion, actual.NombreArchivo, indice);
                Actual = p;
                Mostrar(p);
            }
            catch (Exception ex)
            {
                Avisar("No se pudo mostrar esa sección. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "ddlSeccionProg_SelectedIndexChanged"), "warning");
            }
        }

        protected void btnProgramarDespachos_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            try
            {
                var p = Actual;
                if (!Disponible(p)) return;

                var errores = new List<string>();
                LeerDatosCarga(errores, out int idCliente, out int idPlanta, out DateTime fecha);
                var filas = LeerFilasElegidas(p, errores);
                if (filas.Count == 0 && errores.Count == 0) errores.Add("No hay unidades marcadas para programar.");
                if (ArmadoLoteImportado.HayRepetidos(filas.Select(f => f.IdConductor.ToString()))) errores.Add("Hay un conductor repetido en más de una unidad.");
                if (ArmadoLoteImportado.HayRepetidos(filas.Select(f => f.IdTracto.ToString()))) errores.Add("Hay un tracto repetido en más de una fila.");
                if (ArmadoLoteImportado.HayRepetidos(filas.Select(f => f.IdCarreta.ToString()))) errores.Add("Hay una carreta repetida en más de una fila.");

                if (errores.Count > 0)
                {
                    Avisar("Revise estos datos: " + string.Join(" ", errores), "warning");
                    return;
                }

                // Leído antes de mostrar la sección siguiente, que cambia la casilla (el mensaje decía
                // "seguimiento de exportación" al programar el afrecho del 24/10/2026 sin exportación).
                bool esExportacion = chkExportacionProg.Checked;
                var rutaProgramada = RutaProgramacion.Reconocer(p.Titulo);
                var r = ProgramacionDespachoService.Programar(filas, idCliente, idPlanta, fecha, esExportacion, UsuarioActual(Page), p.EsInternacional);

                if (r.Creados.Count == 0)
                {
                    Avisar("No se programó ningún despacho. " + string.Join(" ", r.Errores), "danger");
                    return;
                }
                // M-086: el paso siguiente del día es revisar lo que les falta a los lotes.
                hlVerLotesPendientes.Visible = true;

                Cerrar();
                Cerrada?.Invoke(this, EventArgs.Empty);
                Programada?.Invoke(this, EventArgs.Empty);
                MostrarSeccionSiguiente(p);
                Avisar($"Se programaron {r.Creados.Count} despacho(s) de carga del {fecha:dd/MM/yyyy}" +
                    (rutaProgramada != null ? " y su seguimiento ya está en el tablero (" + rutaProgramada.Cliente + ")"
                     : esExportacion ? " y su seguimiento de exportación ya está en el tablero (pedido PENDIENTE)" : "") +
                    ". Cuando llegue el correo, suba el ZIP aquí: cada guía completará su despacho." +
                    (r.Errores.Count > 0 ? " No se pudieron programar: " + string.Join(" ", r.Errores) : ""),
                    r.Errores.Count > 0 ? "warning" : "success");
            }
            catch (Exception ex)
            {
                Avisar("No se pudieron programar los despachos. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnProgramarDespachos_Click"), "danger");
            }
        }

        /// <summary>
        /// Cliente, planta y fecha de carga elegidos (la fecha, hasta 7 días después de hoy y hasta los días antes que
        /// diga <c>Despachos.DiasAtrasProgramacionWord</c> en Web.config; 7 por defecto).
        /// </summary>
        private void LeerDatosCarga(List<string> errores, out int idCliente, out int idPlanta, out DateTime fecha)
        {
            if (!int.TryParse(ddlClienteProg.SelectedValue, out idCliente) || idCliente <= 0) errores.Add("Elija el cliente.");
            if (!int.TryParse(ddlPlantaProg.SelectedValue, out idPlanta) || idPlanta <= 0) errores.Add("Elija la planta de carga.");
            int diasAtras = DespachoValidaciones.DiasAtrasProgramacionWord(
                ConfigurationManager.AppSettings[DespachoValidaciones.ClaveDiasAtrasProgramacionWord]);
            if (!DespachoValidaciones.TryParseFechaIso(txtFechaProg.Text, out fecha)
                || !DespachoValidaciones.FechaCargaWordEsValida(fecha, FechaHelper.Hoy(), diasAtras))
                errores.Add($"La fecha de carga no es válida (hasta {diasAtras} días antes o 7 días después de hoy).");
        }

        /// <summary>Las filas marcadas que no estaban ya programadas, con lo elegido en cada una.</summary>
        private List<ProgramacionDespachoService.FilaElegida> LeerFilasElegidas(ProgramacionNueva p, List<string> errores)
        {
            var filas = new List<ProgramacionDespachoService.FilaElegida>();
            foreach (RepeaterItem item in rptProgramacionNueva.Items)
            {
                if (item.ItemIndex >= p.Filas.Count) break;
                // Defensa extra: una fila que el formulario no trajo (la hora siempre viaja) no se programa.
                if (Request.Form[item.FindControl("txtHoraProg").UniqueID] == null) continue;
                var f = p.Filas[item.ItemIndex];
                if (f.IdDespachoExistente.HasValue || !((CheckBox)item.FindControl("chkIncluirProg")).Checked) continue;

                string etiqueta = $"{f.Chofer ?? "(sin chofer)"} / {f.Tracto ?? "(sin tracto)"}";
                int.TryParse(((DropDownList)item.FindControl("ddlConductorProg")).SelectedValue, out int idConductor);
                int.TryParse(((DropDownList)item.FindControl("ddlTractoProg")).SelectedValue, out int idTracto);
                int.TryParse(((DropDownList)item.FindControl("ddlCarretaProg")).SelectedValue, out int idCarreta);
                string hora = ((TextBox)item.FindControl("txtHoraProg")).Text.Trim();

                if (idConductor <= 0) errores.Add($"{etiqueta}: elija el conductor.");
                if (idTracto <= 0) errores.Add($"{etiqueta}: elija el tracto.");
                if (idCarreta <= 0) errores.Add($"{etiqueta}: elija la carreta.");
                TimeSpan? horaProgramada = ProgramacionCarga.HoraDelHorario(hora);
                if (hora.Length > 0 && !horaProgramada.HasValue) errores.Add($"{etiqueta}: la hora {hora} no es válida.");

                filas.Add(new ProgramacionDespachoService.FilaElegida
                {
                    IdConductor = idConductor, IdTracto = idTracto, IdCarreta = idCarreta,
                    Hora = horaProgramada, Etiqueta = etiqueta
                });
            }
            return filas;
        }

        /// <summary>El Word trae otras secciones de carga (otro cliente): se pasa a la siguiente sin volver a subirlo.</summary>
        private void MostrarSeccionSiguiente(ProgramacionNueva p)
        {
            if (p.Programacion == null || p.SeccionesCarga.Count <= 1) return;
            try
            {
                var siguiente = ProgramacionDespachoService.Proponer(p.Programacion, p.NombreArchivo, (p.IndiceSeccion + 1) % p.SeccionesCarga.Count);
                Actual = siguiente;
                Mostrar(siguiente);
            }
            catch (Exception ex) { RegistrarError("btnProgramarDespachos_Click (siguiente sección)", ex); }
        }

        protected void btnRegistrarEntregas_Click(object sender, EventArgs e)
        {
            var p = Actual;
            if (!Disponible(p)) return;
            try
            {
                int n = ProgramacionDespachoService.RegistrarEntregas(p.Entregas.Where(x => !x.YaRegistrada), SesionHelper.IdUsuario(Session));
                if (p.Programacion != null) p.Entregas = ProgramacionDespachoService.ProponerEntregas(p.Programacion);
                Actual = p;
                Mostrar(p);
                Avisar($"Se completaron {n} viaje(s) con su descarga, lugar de entrega o retorno con carga. Véalos en el seguimiento.", "success");
            }
            catch (Exception ex)
            {
                Avisar("No se pudieron registrar las entregas. " + MensajeErrorHelper.ParaUsuario(ex, "Error en {Contexto}", "btnRegistrarEntregas_Click"), "danger");
            }
        }

        protected void btnDescartarProgramacion_Click(object sender, EventArgs e) =>
            Descartada?.Invoke(this, EventArgs.Empty);

        #endregion

        #region Utilidades

        /// <summary>La programación ya no está en la sesión: se avisa y se vuelve al formulario.</summary>
        private void Vencida()
        {
            Avisar("La programación expiró. Vuelva a subir el Word.", "warning");
            pnlProgramacionNueva.Visible = false;
            Cerrada?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Se puede seguir con <paramref name="p"/>: no venció (null) y el formulario es de esta vista previa. Si es de otra
        /// (otra pestaña leyó otro Word o cambió de sección), se muestra la de la sesión y se avisa, sin grabar nada.
        /// </summary>
        private bool Disponible(ProgramacionNueva p)
        {
            if (p == null) { Vencida(); return false; }
            if (_vigente) return true;
            Mostrar(p);
            Avisar("La programación cambió en otra pestaña; revísela.", "warning");
            return false;
        }

        private void Avisar(string mensaje, string tipo) => Aviso?.Invoke(this, new AvisoEventArgs(mensaje, tipo));

        #endregion
    }
}
