using System;
using System.Collections.Generic;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Usuarios;

namespace WebSGV.Views
{
    public partial class GestionUsuarios : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            txtContrasena.Attributes["placeholder"] = PasswordHelper.TextoMinimoNueva;
            txtNuevaPass.Attributes["placeholder"] = PasswordHelper.TextoMinimoNueva;

            // La grilla no guarda ViewState (E5-17): se enlaza en cada petición, en el manejador que cambia algo o,
            // si ninguno lo hizo, en OnPreRender. Los filtros aplicados viven en hfFiltroTexto/Rol/Estado (solo los
            // escriben Buscar y Limpiar) y la página en el ControlState del GridView: se conservan tras editar,
            // activar, restablecer o quitar el segundo factor, también en la página 2 o con un filtro.
            if (!IsPostBack)
                CargarRoles();
        }

        /// <summary>
        /// ddlConductor va sin ViewState (E5-17: los conductores activos con su documento pesaban casi todo el
        /// ViewState de la página). Se llena en cada petición antes de leer lo enviado, así el conductor elegido se
        /// recupera en el postback del alta o la edición. Solo con la sesión del rol de la página: a los demás
        /// Page_Load los saca sin consultar nada.
        /// </summary>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (SecurityHelper.TieneSesionActiva() && SecurityHelper.EsAdminSistema())
                CargarConductores();
        }

        /// <summary>true cuando CargarUsuarios ya enlazó la grilla en esta petición.</summary>
        private bool _grillaEnlazada;

        protected override void OnPreRender(EventArgs e)
        {
            if (!_grillaEnlazada && !Response.IsRequestBeingRedirected)
                CargarUsuarios();
            base.OnPreRender(e);
        }

        private void CargarRoles()
        {
            ddlFiltroRol.Items.Clear();
            ddlFiltroRol.Items.Add(new ListItem("-- Todos los roles --", ""));

            foreach (var rol in CargarRolesAsignables())
                ddlFiltroRol.Items.Add(new ListItem(rol.Texto, rol.Valor));
        }

        /// <summary>
        /// Rehace solo la lista de roles del modal (no toca el filtro ni su selección): así un rol
        /// retirado que se agregó al editar a un usuario que lo tiene no queda en el ViewState
        /// para el siguiente alta o edición.
        /// </summary>
        private List<RolesHelper.RolDisponible> CargarRolesAsignables()
        {
            var rolesDisponibles = RolesHelper.ObtenerRolesDisponibles();

            ddlRol.Items.Clear();
            foreach (var rol in rolesDisponibles)
                ddlRol.Items.Add(new ListItem(rol.Texto, rol.Valor));

            return rolesDisponibles;
        }

        private void CargarConductores()
        {
            DataTable dt = GestionUsuariosService.ConductoresParaVincular();

            ddlConductor.Items.Clear();
            ddlConductor.Items.Add(new ListItem("-- No aplica --", ""));

            foreach (DataRow row in dt.Rows)
            {
                string descripcion = row["descripcion"].ToString();
                if (row["nombreUsuario"] != DBNull.Value)
                    descripcion += " (usuario: " + row["nombreUsuario"] + ")";

                ddlConductor.Items.Add(new ListItem(descripcion, row["idConductor"].ToString()));
            }
        }

        private void CargarUsuarios()
        {
            _grillaEnlazada = true;
            try
            {
                EnlazarUsuarios();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo cargar la lista de usuarios: " + MensajeErrorHelper.ParaUsuario(ex), "warning");
                gvUsuarios.DataSource = null;
                gvUsuarios.DataBind();
            }
        }

        private void EnlazarUsuarios()
        {
            // Los filtros son los que se aplicaron con Buscar (campos ocultos), no lo que haya en los controles:
            // paginar o usar una fila no filtra con algo elegido sin pulsar Buscar. Los campos ocultos los puede
            // cambiar el usuario: texto recortado, rol de la lista y estado "", "0" o "1".
            string buscar = ListaCatalogo.RecortarBusqueda(hfFiltroTexto.Value);
            string rol    = ddlFiltroRol.Items.FindByValue(hfFiltroRol.Value ?? "") != null ? hfFiltroRol.Value ?? "" : "";
            string estado = AdminUsuariosReglas.FiltroEstadoValido(hfFiltroEstado.Value);
            if (txtBuscar.Text != buscar) txtBuscar.Text = buscar;
            ddlFiltroRol.SelectedValue = rol;
            ddlFiltroEstado.SelectedValue = estado;
            bool conFiltro = buscar.Length > 0 || rol.Length > 0 || estado.Length > 0;

            // segundoFactor: 1 si lo tiene activo, 0 si tiene un alta a medias, NULL si no tiene nada (E4-14). Sin la
            // migración 62 y con el interruptor apagado, NULL para todos (sin insignias).
            DataTable dt = GestionUsuariosService.Listar(buscar, rol, estado);

            gvUsuarios.PageIndex = ListaCatalogo.IndiceValido(gvUsuarios.PageIndex, dt.Rows.Count);
            gvUsuarios.EmptyDataText = conFiltro
                ? "No se encontraron usuarios con los filtros seleccionados."
                : "No hay usuarios registrados.";
            gvUsuarios.DataSource = dt;
            gvUsuarios.DataBind();

            string resumen = ListaCatalogo.Resumen(gvUsuarios.PageIndex, dt.Rows.Count, dt.Rows.Count, "");
            litResumen.Text = conFiltro && resumen.Length > 0 ? resumen + " con los filtros aplicados" : resumen;

            var (total, activos) = GestionUsuariosService.Totales();
            lblContador.Text = AdminUsuariosReglas.TextoContador(total, activos);
        }

        protected void gvUsuarios_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvUsuarios.PageIndex = e.NewPageIndex;
            CargarUsuarios();
        }

        protected void btnNuevoUsuario_Click(object sender, EventArgs e)
        {
            // E4-12: un alta pide la contraseña del administrador. Se pide antes de abrir el formulario: la
            // contraseña del usuario nuevo (campo password) no sobrevive a otro envío de la página.
            if (!ReautenticacionVigente())
            {
                PedirReautenticacion(AccionReautNuevo, "crear un usuario nuevo");
                return;
            }
            AbrirNuevoUsuario();
        }

        private void AbrirNuevoUsuario()
        {
            hfIdUsuario.Value       = "0";
            lblTituloModal.Text     = "Nuevo usuario";
            txtNombreUsuario.Text   = "";
            txtNombreUsuario.Enabled = true;
            txtNombre.Text          = "";
            CargarRolesAsignables();   // descarta un rol retirado que dejó la última edición
            ddlRol.Enabled          = true;
            if (ddlRol.Items.Count > 0)
                ddlRol.SelectedIndex = 0;
            ddlConductor.SelectedIndex = 0;
            txtContrasena.Text      = "";
            pnlContrasena.Visible   = true;
            pnlMensajeModal.Visible = false;

            AbrirModal("modalUsuario");
        }

        /// <summary>
        /// Botón de una fila (E5-17): deja "ACCION|id[|estado]" en hfArgumentoFila y pulsa este botón oculto. El
        /// campo lo puede cambiar el usuario: se lee con formato estricto (AdminUsuariosReglas.TryLeerAccionFila), se
        /// exige que el usuario exista y cada acción repite sus comprobaciones de siempre (fila propia, contraseña del
        /// administrador, último administrador de sistema en UsuarioEstadoService). Leer el argumento no autoriza nada.
        /// </summary>
        protected void btnAccionFila_Click(object sender, EventArgs e)
        {
            string argumento = hfArgumentoFila.Value;
            hfArgumentoFila.Value = "";
            if (!AdminUsuariosReglas.TryLeerAccionFila(argumento, out AccionFilaUsuarioTipo accion, out int idUsuario, out bool nuevoActivo))
            {
                MostrarMensaje("No se pudo leer la fila; recargue la página.", "warning");
                CargarUsuarios();
                return;
            }

            UsuarioGestion usuario = GestionUsuariosService.Leer(idUsuario);
            if (usuario == null)
            {
                MostrarMensaje("El usuario ya no existe; se actualizó la lista.", "warning");
                CargarUsuarios();
                return;
            }

            switch (accion)
            {
                case AccionFilaUsuarioTipo.Editar:
                    CargarUsuarioEnModal(usuario);
                    break;
                case AccionFilaUsuarioTipo.Estado:
                    // Mismo "id|estadoDestino" de B5-8 que leía el antiguo CommandArgument.
                    CambiarEstadoUsuario(idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture) + (nuevoActivo ? "|1" : "|0"));
                    break;
                case AccionFilaUsuarioTipo.QuitarSegundoFactor:
                    QuitarSegundoFactor(idUsuario);
                    break;
                case AccionFilaUsuarioTipo.Contrasena:
                    RestablecerContrasenaDeFila(idUsuario);
                    break;
            }
        }

        /// <summary>Botón Contraseña de una fila: no la propia y con la contraseña del administrador (E4-12).</summary>
        private void RestablecerContrasenaDeFila(int idUsuario)
        {
            if (AdminUsuariosReglas.EsFilaPropia(idUsuario, SesionHelper.IdUsuarioActual))
            {
                MostrarMensaje(AdminUsuariosReglas.MensajeRestablecerPropia, "warning");
                CargarUsuarios();
                return;
            }
            if (!ReautenticacionVigente())
            {
                PedirReautenticacion(AccionReautReset + idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "restablecer la contraseña de otro usuario");
                return;
            }
            PrepararResetPassword(idUsuario);
        }

        // ── Reautenticación del administrador (E4-12) ─────────────────────

        /// <summary>Acción pendiente "RESET|id": tras la contraseña, abre el modal de restablecer de ese usuario.</summary>
        private const string AccionReautReset = "RESET|";

        /// <summary>Acción pendiente: tras la contraseña, vuelve a guardar el modal de usuario (alta o cambio de rol).</summary>
        private const string AccionReautGuardar = "GUARDAR";

        /// <summary>Acción pendiente: tras la contraseña, abre el formulario de usuario nuevo.</summary>
        private const string AccionReautNuevo = "NUEVO";

        /// <summary>Acción pendiente "ACTIVAR|id": tras la contraseña, activa esa cuenta de administración.</summary>
        private const string AccionReautActivar = "ACTIVAR|";

        /// <summary>Acción pendiente "QUITAR2F|id": tras la contraseña, quita el segundo factor de ese usuario (E4-14).</summary>
        private const string AccionReautQuitarSegundoFactor = "QUITAR2F|";

        /// <summary>
        /// True mientras btnConfirmarReaut_Click retoma un guardado: si era un alta, la contraseña del usuario nuevo
        /// llega vacía (un campo password no se vuelve a enviar) y se pide escribirla otra vez con ese motivo.
        /// </summary>
        private bool _retomandoTrasReautenticacion;

        /// <summary>Acciones de la fila: sin Desactivar ni Contraseña en la del usuario conectado.</summary>
        protected AccionesFilaUsuario AccionesFila(object idUsuario) =>
            AdminUsuariosReglas.Decidir(Convert.ToInt32(idUsuario, System.Globalization.CultureInfo.InvariantCulture),
                SesionHelper.IdUsuarioActual);

        private bool ReautenticacionVigente() =>
            AdminUsuariosReglas.ReautenticacionVigente(SesionHelper.ReautenticacionAdminUtc(Session), DateTime.UtcNow);

        /// <summary>
        /// Abre el modal de contraseña del administrador. <paramref name="accion"/> solo elige qué se continúa
        /// después: esa acción vuelve a validar todo (fila propia, rol, ventana) en el servidor.
        /// </summary>
        private void PedirReautenticacion(string accion, string texto)
        {
            hfAccionReaut.Value     = accion;
            lblAccionReaut.Text     = System.Web.HttpUtility.HtmlEncode(texto);
            txtContrasenaAdmin.Text = "";
            pnlMensajeReaut.Visible = false;
            AbrirModal("modalReautenticar");
        }

        protected void btnConfirmarReaut_Click(object sender, EventArgs e)
        {
            // Solo el grupo vgReaut (la contraseña); el guardado que retoma no vuelve a mirar Page.IsValid.
            if (!Page.IsValid) { AbrirModal("modalReautenticar"); return; }
            int? idActual = SesionHelper.IdUsuarioActual;
            string error = ReautenticacionHelper.Verificar(idActual ?? 0, txtContrasenaAdmin.Text,
                PropositoReautenticacion.AdminUsuarios);
            txtContrasenaAdmin.Text = "";
            if (error != null)
            {
                AvisoHelper.Pintar(lblMensajeReaut, error, AvisoHelper.Peligro, "mb-0");
                pnlMensajeReaut.Visible = true;
                AbrirModal("modalReautenticar");
                return;
            }

            SesionHelper.MarcarReautenticacionAdmin(Session, DateTime.UtcNow);
            string accion = hfAccionReaut.Value ?? "";
            hfAccionReaut.Value = "";

            if (accion == AccionReautGuardar)
            {
                _retomandoTrasReautenticacion = true;
                btnGuardarUsuario_Click(sender, e);
                return;
            }
            if (accion == AccionReautNuevo)
            {
                AbrirNuevoUsuario();
                return;
            }
            if (accion.StartsWith(AccionReautActivar, StringComparison.Ordinal)
                && int.TryParse(accion.Substring(AccionReautActivar.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int idActivar) && idActivar > 0)
            {
                CambiarEstadoUsuario(idActivar.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|1");
                return;
            }
            if (accion.StartsWith(AccionReautQuitarSegundoFactor, StringComparison.Ordinal)
                && int.TryParse(accion.Substring(AccionReautQuitarSegundoFactor.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int idQuitar) && idQuitar > 0)
            {
                QuitarSegundoFactor(idQuitar);
                return;
            }
            if (accion.StartsWith(AccionReautReset, StringComparison.Ordinal)
                && int.TryParse(accion.Substring(AccionReautReset.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int idReset) && idReset > 0
                && !AdminUsuariosReglas.EsFilaPropia(idReset, idActual))
            {
                PrepararResetPassword(idReset);
                return;
            }
            MostrarMensaje("Contraseña confirmada. Vuelva a elegir la acción.", "info");
        }

        /// <summary>Abre el modal de edición con el usuario que ya leyó btnAccionFila_Click.</summary>
        private void CargarUsuarioEnModal(UsuarioGestion u)
        {
            int idUsuario = u.IdUsuario;
            hfIdUsuario.Value        = idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lblTituloModal.Text      = "Editar usuario";
            txtNombreUsuario.Text    = u.NombreUsuario;
            txtNombreUsuario.Enabled = false;
            txtNombre.Text           = u.Nombre;
            // Los roles son un valor cerrado en MAYUSCULAS; se normaliza para que un valor antiguo
            // en minuscula no deje de coincidir con la lista ni se vuelva a guardar asi.
            string rolUsuario = u.Rol.Trim().ToUpperInvariant();
            // Lista limpia: el rol retirado solo aparece mientras se edita al usuario que ya lo tiene.
            CargarRolesAsignables();
            ListItem itemRol = ddlRol.Items.FindByValue(rolUsuario);
            if (itemRol == null)
                ddlRol.Items.Add(new ListItem(rolUsuario, rolUsuario));
            ddlRol.SelectedValue     = rolUsuario;
            // Su propio rol no se cambia (E4-12): el servidor lo rechaza igual en btnGuardarUsuario_Click.
            ddlRol.Enabled           = AdminUsuariosReglas.Decidir(idUsuario, SesionHelper.IdUsuarioActual).PuedeCambiarRol;
            ddlConductor.SelectedIndex = 0;
            if (u.IdConductor.HasValue)
            {
                ListItem itemConductor = ddlConductor.Items.FindByValue(u.IdConductor.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (itemConductor != null)
                    ddlConductor.SelectedValue = itemConductor.Value;
            }
            pnlContrasena.Visible    = false;
            pnlMensajeModal.Visible  = false;

            AbrirModal("modalUsuario");
        }

        /// <summary>
        /// Activar/desactivar con estado destino explícito "id|0" / "id|1" (B5-8, como B5-3 en los maestros): un F5
        /// o una segunda pestaña ya no reactiva al usuario recién desactivado. El sello de sesión se renueva solo si
        /// el estado cambia de verdad.
        /// </summary>
        private void CambiarEstadoUsuario(object argumento)
        {
            if (!EstadoActivoService.TryLeerArgumento(argumento, out int idUsuario, out bool nuevoActivo))
            {
                MostrarMensaje("No se pudo leer la fila; recargue la página.", "warning");
                CargarUsuarios();
                return;
            }

            // E4-12: activar una cuenta de administración pide la contraseña del administrador.
            if (nuevoActivo && !ReautenticacionVigente())
            {
                UsuarioGestion u = GestionUsuariosService.Leer(idUsuario);
                if (u != null && AdminUsuariosReglas.RequiereReautenticacionAlActivar(u.Rol))
                {
                    PedirReautenticacion(AccionReautActivar + idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "activar la cuenta de administración " + u.NombreUsuario.Trim());
                    CargarUsuarios();
                    return;
                }
            }

            try
            {
                ResultadoCambioEstado r = UsuarioEstadoService.CambiarEstado(idUsuario, nuevoActivo, SesionHelper.IdUsuarioActual);
                MostrarMensaje(r.Mensaje, r.Cambiado ? "success" : "warning");
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "warning");
            }
            CargarUsuarios();
        }

        /// <summary>
        /// "Quitar segundo factor" (E4-14): el usuario perdió el celular y los códigos. Solo sobre otra fila, con la
        /// contraseña del administrador (ventana de E4-12). Borra el factor y sus códigos y renueva el sello: las
        /// sesiones abiertas de ese usuario se cierran; al volver a entrar solo pide la contraseña (o el alta, si el
        /// interruptor la exige).
        /// </summary>
        private void QuitarSegundoFactor(int idUsuario)
        {
            if (AdminUsuariosReglas.EsFilaPropia(idUsuario, SesionHelper.IdUsuarioActual))
            {
                MostrarMensaje(AdminUsuariosReglas.MensajeQuitarSegundoFactorPropio, "warning");
                CargarUsuarios();
                return;
            }
            if (!ReautenticacionVigente())
            {
                PedirReautenticacion(AccionReautQuitarSegundoFactor + idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "quitar el segundo factor de otro usuario");
                CargarUsuarios();
                return;
            }

            try
            {
                string nombre = (GestionUsuariosService.Leer(idUsuario)?.NombreUsuario ?? "").Trim();
                if (SegundoFactorService.Quitar(idUsuario))
                {
                    AuditoriaHelper.Registrar(SegundoFactorService.AccionQuitado, "Usuarios", idUsuario,
                        "Segundo factor quitado por Admin Sistema (usuario " + nombre + "); sus sesiones se cierran");
                    MostrarMensaje("Se quitó el segundo factor de " + nombre + ". Al entrar solo se le pedirá la contraseña"
                        + (SegundoFactorReglas.ExigirSegundoFactor ? " y deberá configurarlo de nuevo." : "."), "success");
                }
                else
                    MostrarMensaje("El usuario " + nombre + " ya no tenía segundo factor.", "info");
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "warning");
            }
            CargarUsuarios();
        }

        /// <summary>Fila con segundo factor (activo o alta a medias): muestra "Quitar 2.º factor" si no es la propia.</summary>
        protected bool PuedeQuitarSegundoFactor(object idUsuario, object segundoFactor) =>
            AdminUsuariosReglas.PuedeQuitarSegundoFactor(Convert.ToInt32(idUsuario, System.Globalization.CultureInfo.InvariantCulture),
                SesionHelper.IdUsuarioActual, segundoFactor != null && segundoFactor != DBNull.Value);

        /// <summary>1 = activo (insignia "2 pasos").</summary>
        protected static bool SegundoFactorActivo(object segundoFactor) =>
            segundoFactor != null && segundoFactor != DBNull.Value && Convert.ToInt32(segundoFactor) == 1;

        /// <summary>data-confirmar de "Quitar 2.º factor" (texto plano, como ConfirmarCambioEstado).</summary>
        protected string ConfirmarQuitarSegundoFactor(object nombreUsuario) =>
            $"¿Quitar el segundo factor de {Convert.ToString(nombreUsuario)}?\\n\\nHágalo solo si perdió el celular y sus códigos de recuperación. " +
            "Sus sesiones abiertas se cerrarán.";

        /// <summary>data-argumento de un botón de la fila (E5-17): "EDITAR|id", "ESTADO|id|estadoDestino" (B5-8), ...</summary>
        protected static string ArgumentoFila(AccionFilaUsuarioTipo accion, object idUsuario, object activo = null) =>
            AdminUsuariosReglas.ArgumentoFila(accion, idUsuario, activo);

        /// <summary>Sello del rol y, si tiene la verificación en dos pasos activa, el sello "2 pasos" (E4-14).</summary>
        protected static string SelloRol(object rol, object segundoFactor) =>
            EstadoUiHelper.Sello(Convert.ToString(rol), TonoEstado.Neutro) +
            (SegundoFactorActivo(segundoFactor) ? " " + EstadoUiHelper.Sello("2 pasos", TonoEstado.Info) : "");

        /// <summary>Sello Activo (verde) o Inactivo (gris).</summary>
        protected static string SelloEstado(object activo) =>
            EstadoUiHelper.Sello(EstadoUiHelper.ObtenerTextoEstado(activo),
                EstadoUiHelper.ObtenerTextoEstado(activo) == "Activo" ? TonoEstado.Ok : TonoEstado.Neutro);

        private static string MensajeRolNoVigente(string rol) =>
            $"El rol {rol} ya no está vigente en el sistema y no se puede asignar. Elija otro rol.";

        private void PrepararResetPassword(int idUsuario)
        {
            UsuarioGestion u = GestionUsuariosService.Leer(idUsuario);
            if (u == null)
            {
                MostrarMensaje("El usuario ya no existe; se actualizó la lista.", "warning");
                CargarUsuarios();
                return;
            }
            lblUsuarioReset.Text = System.Web.HttpUtility.HtmlEncode(u.NombreUsuario);

            hfIdUsuarioReset.Value   = idUsuario.ToString();
            txtNuevaPass.Text        = "";
            txtConfirmarPass.Text    = "";
            pnlMensajeReset.Visible  = false;

            AbrirModal("modalResetPass");
        }

        protected void btnGuardarUsuario_Click(object sender, EventArgs e)
        {
            int    idUsuario     = Convert.ToInt32(hfIdUsuario.Value);
            string nombreUsuario = txtNombreUsuario.Text.Trim();
            string nombre        = txtNombre.Text.Trim();
            string rol           = (ddlRol.SelectedValue ?? "").Trim().ToUpperInvariant();
            // Recortada, como en Restablecer contraseña (el celular agrega un espacio al final).
            string contrasena    = (txtContrasena.Text ?? "").Trim();
            int idConductor;
            bool tieneConductor = int.TryParse(ddlConductor.SelectedValue, out idConductor) && idConductor > 0;
            bool esRolConductor = RolesHelper.EsRolConductor(rol);

            if (string.IsNullOrEmpty(nombreUsuario) || string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(rol))
            {
                MostrarMensajeModal("Todos los campos obligatorios deben ser completados.", "danger");
                AbrirModal("modalUsuario");
                return;
            }

            if (esRolConductor && !tieneConductor)
            {
                MostrarMensajeModal("Debe asociar un conductor al usuario con rol Conductor.", "danger");
                AbrirModal("modalUsuario");
                return;
            }

            if (!esRolConductor)
                tieneConductor = false;

            if (idUsuario == 0)
            {
                if (!RolesHelper.PuedeAsignarseRol(rol, null))
                {
                    MostrarMensajeModal(MensajeRolNoVigente(rol), "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(nombreUsuario, @"^[A-Za-z0-9_.]{3,50}$"))
                {
                    MostrarMensajeModal("El nombre de usuario debe tener entre 3 y 50 caracteres: solo letras, números, punto y guion bajo, sin espacios.", "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
                if (string.IsNullOrEmpty(contrasena))
                {
                    MostrarMensajeModal(_retomandoTrasReautenticacion
                        ? "Contraseña confirmada. Vuelva a escribir la contraseña del nuevo usuario y pulse Guardar."
                        : "Debe ingresar una contraseña para el nuevo usuario.",
                        _retomandoTrasReautenticacion ? "info" : "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
            }

            try
            {
                if (idUsuario == 0)
                {
                    // Dentro del try: consulta el DNI y los apellidos del conductor vinculado.
                    string errorContrasena = ContrasenaNuevaHelper.ValidarParaNuevoUsuario(contrasena, nombreUsuario, nombre,
                        tieneConductor ? (int?)idConductor : null);
                    if (errorContrasena != null)
                    {
                        MostrarMensajeModal(errorContrasena, "danger");
                        AbrirModal("modalUsuario");
                        return;
                    }
                }

                // Conductor libre y nombre libre antes de pedir la contraseña del administrador, con los mismos
                // mensajes sin el prefijo "Error al guardar" (GestionUsuariosService.Crear los repite antes del INSERT).
                try
                {
                    if (tieneConductor)
                        GestionUsuariosService.ValidarConductorLibre(idConductor, idUsuario);
                    if (idUsuario == 0)
                        GestionUsuariosService.ValidarNombreLibre(nombreUsuario);
                }
                catch (ErrorNegocioException exNegocio)
                {
                    MostrarMensajeModal(MensajeErrorHelper.ParaUsuario(exNegocio), "danger");
                    AbrirModal("modalUsuario");
                    return;
                }

                if (idUsuario == 0)
                {
                    // E4-12: toda alta exige la contraseña del administrador (vigente: se pidió al abrir el formulario).
                    // Si venció mientras se llenaba, se pide y btnConfirmarReaut_Click retoma este guardado.
                    if (AdminUsuariosReglas.RequiereReautenticacionAlGuardar(esAlta: true, cambiaRol: false) && !ReautenticacionVigente())
                    {
                        PedirReautenticacion(AccionReautGuardar, "crear el usuario " + nombreUsuario);
                        return;
                    }

                    // Política, conductor y nombre libres (otra vez), hash, INSERT con "debe cambiar" y auditoría.
                    GestionUsuariosService.Crear(nombreUsuario, nombre, rol, contrasena, tieneConductor ? (int?)idConductor : null);
                    MostrarMensaje($"Usuario '{nombreUsuario}' creado.", "success");
                }
                else
                {
                    UsuarioGestion anterior = GestionUsuariosService.Leer(idUsuario);
                    if (anterior == null)
                        throw new Services.Common.ErrorNegocioException("El usuario ya no existe. Actualice la lista.");
                    string rolAnterior = anterior.Rol.Trim();

                    // Un rol retirado solo se conserva si ya era el suyo; no se asigna a nadie más.
                    if (!RolesHelper.PuedeAsignarseRol(rol, rolAnterior))
                        throw new Services.Common.ErrorNegocioException(MensajeRolNoVigente(rol));

                    bool cambiaRol = !string.Equals(rolAnterior, rol.Trim(), StringComparison.OrdinalIgnoreCase);

                    // Nadie se cambia su propio rol (ni se quita ni se da permisos): lo hace otro administrador de sistema.
                    if (cambiaRol && AdminUsuariosReglas.EsFilaPropia(idUsuario, SesionHelper.IdUsuarioActual))
                        throw new Services.Common.ErrorNegocioException(AdminUsuariosReglas.MensajeCambiarRolPropio);

                    // Cambiar un rol pide la contraseña del administrador (E4-12); los datos del modal siguen en el
                    // formulario y btnConfirmarReaut_Click vuelve a llamar a este método.
                    if (AdminUsuariosReglas.RequiereReautenticacionAlGuardar(esAlta: false, cambiaRol: cambiaRol) && !ReautenticacionVigente())
                    {
                        PedirReautenticacion(AccionReautGuardar, "cambiar el rol de " + txtNombreUsuario.Text.Trim());
                        return;
                    }

                    // Con la guarda del último administrador de sistema (M-066). Cambiar el rol o el conductor
                    // vinculado renueva el sello: cierra sus sesiones abiertas.
                    UsuarioEstadoService.ActualizarDatos(idUsuario, nombre, rol,
                        tieneConductor ? (int?)idConductor : null);

                    string Json(string n, string r, int? idC) => Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        nombre = n,
                        rol = r,
                        idConductor = idC
                    });
                    AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario,
                        string.Equals(rolAnterior, rol.Trim(), StringComparison.OrdinalIgnoreCase)
                            ? $"Usuario actualizado: nombre={nombre}, rol={rol}"
                            : $"Usuario actualizado: nombre={nombre}, rol {rolAnterior} -> {rol}",
                        Json(anterior.Nombre, rolAnterior, anterior.IdConductor),
                        Json(nombre, rol, tieneConductor ? (int?)idConductor : null));
                    MostrarMensaje("Usuario actualizado.", "success");
                }

                CargarUsuarios();
            }
            catch (Exception ex)
            {
                MostrarMensajeModal("Error al guardar: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
                AbrirModal("modalUsuario");
            }
        }

        protected void btnResetPass_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) { AbrirModal("modalResetPass"); return; }
            int    idUsuario     = Convert.ToInt32(hfIdUsuarioReset.Value);
            // Recortadas las dos (se guarda recortada): una de solo espacios queda vacía y se rechaza.
            string nuevaPass     = (txtNuevaPass.Text ?? "").Trim();
            string confirmarPass = (txtConfirmarPass.Text ?? "").Trim();

            // E4-12: el botón no aparece en la fila propia y la ventana se pidió al abrir el modal; se vuelve a
            // comprobar aquí por si el envío llega igual (o la ventana venció mientras se escribía).
            if (AdminUsuariosReglas.EsFilaPropia(idUsuario, SesionHelper.IdUsuarioActual))
            {
                MostrarMensaje(AdminUsuariosReglas.MensajeRestablecerPropia, "warning");
                CargarUsuarios();
                return;
            }
            if (!ReautenticacionVigente())
            {
                PedirReautenticacion(AccionReautReset + idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "restablecer la contraseña de otro usuario");
                return;
            }

            try
            {
                // Política, confirmación, UPDATE con "debe cambiar" y sello nuevo (cierra sus sesiones) y
                // auditoría: ContrasenaService (E6-16).
                string error = Services.Usuarios.ContrasenaService.RestablecerPorAdministrador(idUsuario, nuevaPass, confirmarPass);
                if (error != null)
                {
                    MostrarMensajeReset(error, "danger");
                    AbrirModal("modalResetPass");
                    return;
                }

                MostrarMensaje("Contraseña restablecida.", "success");
                CargarUsuarios();
            }
            catch (Exception ex)
            {
                MostrarMensajeReset(MensajeErrorHelper.ParaUsuario(ex), "danger");
                AbrirModal("modalResetPass");
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            hfFiltroTexto.Value  = ListaCatalogo.RecortarBusqueda(txtBuscar.Text);
            hfFiltroRol.Value    = ddlFiltroRol.SelectedValue ?? "";
            hfFiltroEstado.Value = ddlFiltroEstado.SelectedValue ?? "";
            gvUsuarios.PageIndex = 0;
            CargarUsuarios();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            hfFiltroTexto.Value  = "";
            hfFiltroRol.Value    = "";
            hfFiltroEstado.Value = "";
            gvUsuarios.PageIndex = 0;
            pnlMensaje.Visible   = false;
            CargarUsuarios();
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, mensaje, tipo, "mb-0");
        }

        /// <summary>
        /// data-confirmar del botón Activar/Desactivar: el nombre del usuario y el efecto del cambio, en texto
        /// plano ("\n" escrito como barra + n = salto de línea). El Button codifica el atributo y SGV.confirmar
        /// (SiteMaster.js) lo muestra con .text(): el nombre que viene de la BD nunca se lee como HTML ni JS.
        /// </summary>
        protected string ConfirmarCambioEstado(object activo, object nombreUsuario)
        {
            string usuario = Convert.ToString(nombreUsuario);
            return Convert.ToBoolean(activo)
                ? $"¿Desactivar al usuario {usuario}?\\n\\nNo podrá iniciar sesión hasta que lo vuelva a activar."
                : $"¿Activar al usuario {usuario}?\\n\\nPodrá volver a iniciar sesión.";
        }

        private void MostrarMensajeModal(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeModal, mensaje, tipo, "mb-0");
            pnlMensajeModal.Visible = true;
        }

        private void MostrarMensajeReset(string mensaje, string tipo)
        {
            AvisoHelper.Pintar(lblMensajeReset, mensaje, tipo, "mb-0");
            pnlMensajeReset.Visible = true;
        }

        private void AbrirModal(string idModal)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "AbrirModal" + idModal,
                $"$(document).ready(function(){{$('#{idModal}').modal('show');}});", true);
        }
    }
}
