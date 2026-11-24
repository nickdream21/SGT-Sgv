<%@ Page Title="Gestión de usuarios" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="GestionUsuarios.aspx.cs"
    Inherits="WebSGV.Views.GestionUsuarios" %>

<%@ Import Namespace="WebSGV.Services.Usuarios" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <%-- Botones de la fila (E5-17): la grilla no guarda ViewState; cada botón copia su data-argumento
         ("EDITAR|id", "ESTADO|id|0/1", "CONTRASENA|id", "QUITAR2F|id") aquí y pulsa btnAccionFila
         (SGV.acciones.enviarFila). El servidor lo revalida (formato, usuario, fila propia y contraseña). --%>
    <asp:HiddenField ID="hfArgumentoFila" runat="server" />
    <%-- Filtros de la última búsqueda (solo los escriben Buscar y Limpiar): con ellos se lista en todo postback. --%>
    <asp:HiddenField ID="hfFiltroTexto" runat="server" />
    <asp:HiddenField ID="hfFiltroRol" runat="server" />
    <asp:HiddenField ID="hfFiltroEstado" runat="server" />
    <asp:Button ID="btnAccionFila" runat="server" Text="Acción de la fila" CssClass="d-none" CausesValidation="false"
        OnClick="btnAccionFila_Click" tabindex="-1" aria-hidden="true" />

    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Gestión de usuarios <asp:Label ID="lblContador" runat="server" CssClass="sgv-encabezado-contador" EnableViewState="false" /></h1>
            <p>Administrar cuentas, roles y accesos al sistema</p>
        </div>
        <div class="sgv-encabezado-acciones">
            <asp:Button ID="btnNuevoUsuario" runat="server" Text="Nuevo usuario"
                CssClass="btn btn-primary" OnClick="btnNuevoUsuario_Click" />
        </div>
    </div>

    <details class="sgv-filtros" open data-plegar-movil>
        <summary>Filtros <span class="sgv-filtros-cuenta"></span></summary>
        <div class="sgv-filtros-cuerpo">
            <div class="form-row align-items-end" data-enter="<%= btnBuscar.ClientID %>">
                <div class="col-md-4 mb-2 mb-md-0">
                    <label for="<%= txtBuscar.ClientID %>" class="sgv-etiqueta">Usuario o nombre</label>
                    <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm" MaxLength="50"
                        placeholder="Buscar por usuario o nombre" autocomplete="off" spellcheck="false" />
                </div>
                <div class="col-md-3 mb-2 mb-md-0">
                    <label for="<%= ddlFiltroRol.ClientID %>" class="sgv-etiqueta">Rol</label>
                    <asp:DropDownList ID="ddlFiltroRol" runat="server" CssClass="form-control form-control-sm" />
                </div>
                <div class="col-md-2 mb-2 mb-md-0">
                    <label for="<%= ddlFiltroEstado.ClientID %>" class="sgv-etiqueta">Estado</label>
                    <asp:DropDownList ID="ddlFiltroEstado" runat="server" CssClass="form-control form-control-sm">
                        <asp:ListItem Value="">Todos</asp:ListItem>
                        <asp:ListItem Value="1">Activos</asp:ListItem>
                        <asp:ListItem Value="0">Inactivos</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="col-md-3">
                    <asp:Button ID="btnBuscar" runat="server" Text="Buscar"
                        CssClass="btn btn-secondary btn-sm mr-1" CausesValidation="false" OnClick="btnBuscar_Click" />
                    <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar"
                        CssClass="btn btn-outline-secondary btn-sm" CausesValidation="false" OnClick="btnLimpiar_Click" />
                </div>
            </div>
        </div>
    </details>

    <!-- Mensaje de feedback -->
    <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="mb-3">
        <asp:Label ID="lblMensaje" runat="server" />
    </asp:Panel>

    <section class="sgv-panel">
        <div class="sgv-panel-cab">
            <div>
                <h2 class="sgv-panel-titulo">Usuarios</h2>
                <p class="sgv-panel-nota mb-0" aria-live="polite"><asp:Literal ID="litResumen" runat="server" Mode="Encode" EnableViewState="false" /></p>
            </div>
        </div>
        <div class="table-responsive">
            <asp:GridView ID="gvUsuarios" runat="server"
                CssClass="table sgv-tabla mb-0"
                AutoGenerateColumns="false"
                EnableViewState="false"
                AllowPaging="true" PageSize="25"
                OnPageIndexChanging="gvUsuarios_PageIndexChanging"
                GridLines="None" CellSpacing="-1"
                EmptyDataText="No hay usuarios registrados.">
                <PagerSettings Mode="NumericFirstLast" PageButtonCount="5" FirstPageText="Primera" LastPageText="Última" />
                <PagerStyle CssClass="sgv-paginador" />
                <EmptyDataRowStyle CssClass="sgv-vacio" />
                <%-- Plantillas en una línea: 25 filas con su sangría pesaban unos 23 KB de espacios. --%>
                <Columns>
                    <asp:BoundField DataField="idUsuario" HeaderText="ID"
                        HeaderStyle-CssClass="sgv-col-id d-none d-md-table-cell" ItemStyle-CssClass="sgv-col-id d-none d-md-table-cell sgv-suave" />
                    <asp:TemplateField HeaderText="Usuario">
                        <%-- En el celular el nombre, el rol y el estado van bajo el usuario (sus columnas se ocultan). --%>
                        <ItemTemplate><span class="font-weight-bold text-nowrap"><%#: Eval("nombreUsuario") %></span><div class="small text-muted d-md-none"><%#: Eval("nombre") %></div><div class="d-sm-none mt-1"><%# SelloRol(Eval("rol"), Eval("segundoFactor")) %> <%# SelloEstado(Eval("activo")) %></div></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="nombre" HeaderText="Nombre completo"
                        HeaderStyle-CssClass="d-none d-md-table-cell" ItemStyle-CssClass="d-none d-md-table-cell" />
                    <asp:TemplateField HeaderText="Rol" HeaderStyle-CssClass="d-none d-sm-table-cell" ItemStyle-CssClass="d-none d-sm-table-cell">
                        <%-- E4-14: "2 pasos" si tiene la verificación en dos pasos activa. --%>
                        <ItemTemplate><%# SelloRol(Eval("rol"), Eval("segundoFactor")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Estado" HeaderStyle-CssClass="sgv-col-estado d-none d-sm-table-cell" ItemStyle-CssClass="sgv-col-estado d-none d-sm-table-cell">
                        <ItemTemplate><%# SelloEstado(Eval("activo")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Acciones" HeaderStyle-CssClass="sgv-col-acciones" ItemStyle-CssClass="sgv-col-acciones">
                        <%-- Fila propia (E4-12): sin Desactivar ni Contraseña; el servidor también lo rechaza. "Quitar 2.º factor"
                             (E4-14): celular y códigos perdidos, no en la fila propia; pide la contraseña del administrador. --%>
                        <ItemTemplate><div class="sgv-acciones flex-wrap flex-md-nowrap"><button type="button" class="btn btn-sm btn-outline-secondary" data-accion="enviarFila" data-argumento='<%#: ArgumentoFila(AccionFilaUsuarioTipo.Editar, Eval("idUsuario")) %>' aria-label='<%#: "Editar al usuario " + Eval("nombreUsuario") %>'>Editar</button><asp:PlaceHolder runat="server" Visible='<%# AccionesFila(Eval("idUsuario")).PuedeDesactivar %>'><button type="button" class='<%#: (bool)Eval("activo") ? "btn btn-sm btn-warning" : "btn btn-sm btn-success" %>' data-accion="enviarFila" data-argumento='<%#: ArgumentoFila(AccionFilaUsuarioTipo.Estado, Eval("idUsuario"), Eval("activo")) %>' data-confirmar='<%#: ConfirmarCambioEstado(Eval("activo"), Eval("nombreUsuario")) %>' data-confirmar-peligro='<%#: (bool)Eval("activo") ? "true" : "false" %>'><%#: (bool)Eval("activo") ? "Desactivar" : "Activar" %></button></asp:PlaceHolder><asp:PlaceHolder runat="server" Visible='<%# AccionesFila(Eval("idUsuario")).PuedeRestablecerContrasena %>'><button type="button" class="btn btn-sm btn-secondary" data-accion="enviarFila" data-argumento='<%#: ArgumentoFila(AccionFilaUsuarioTipo.Contrasena, Eval("idUsuario")) %>' aria-label='<%#: "Restablecer la contraseña de " + Eval("nombreUsuario") %>'>Contraseña</button></asp:PlaceHolder><asp:PlaceHolder runat="server" Visible='<%# PuedeQuitarSegundoFactor(Eval("idUsuario"), Eval("segundoFactor")) %>'><button type="button" class="btn btn-sm btn-outline-danger" data-accion="enviarFila" data-argumento='<%#: ArgumentoFila(AccionFilaUsuarioTipo.QuitarSegundoFactor, Eval("idUsuario")) %>' data-confirmar='<%#: ConfirmarQuitarSegundoFactor(Eval("nombreUsuario")) %>' data-confirmar-titulo="Quitar segundo factor" data-confirmar-peligro="true">Quitar 2.º factor</button></asp:PlaceHolder></div></ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>

    <!-- ======================================================
         MODAL: Crear / Editar Usuario
         ====================================================== -->
    <div class="modal fade" id="modalUsuario" tabindex="-1" role="dialog" aria-labelledby="modalUsuarioLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg modal-dialog-centered" role="document">
            <div class="modal-content" data-enter="<%= btnGuardarUsuario.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalUsuarioLabel">
                        <asp:Label ID="lblTituloModal" runat="server" Text="Nuevo usuario" />
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <asp:HiddenField ID="hfIdUsuario" runat="server" Value="0" />

                    <asp:Panel ID="pnlMensajeModal" runat="server" Visible="false" CssClass="mb-3">
                        <asp:Label ID="lblMensajeModal" runat="server" />
                    </asp:Panel>

                    <div class="row">
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="<%= txtNombreUsuario.ClientID %>">Nombre de usuario <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtNombreUsuario" runat="server" CssClass="form-control"
                                    placeholder="Sin espacios, ej: jperez" MaxLength="100" autocomplete="off" aria-required="true" />
                                <asp:RequiredFieldValidator ID="rfvNombreUsuario" runat="server" ControlToValidate="txtNombreUsuario"
                                    ErrorMessage="Escriba el nombre de usuario." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgUsuario" />
                                <small class="text-muted">3 a 50 caracteres: letras, números, punto y guion bajo.</small>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="<%= txtNombre.ClientID %>">Nombre completo <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                    placeholder="Ej: Juan Pérez García" MaxLength="200" aria-required="true" />
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ControlToValidate="txtNombre"
                                    ErrorMessage="Escriba el nombre completo." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgUsuario" />
                            </div>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="<%= ddlRol.ClientID %>">Rol <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                 <asp:DropDownList ID="ddlRol" runat="server" CssClass="form-control" aria-required="true" />
                             </div>
                         </div>
                        <asp:Panel ID="pnlContrasena" runat="server" CssClass="col-md-6">
                            <div class="form-group">
                                <label for="<%= txtContrasena.ClientID %>">Contraseña <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:TextBox ID="txtContrasena" runat="server" CssClass="form-control"
                                    TextMode="Password" autocomplete="new-password" aria-required="true" /><%-- placeholder: Page_Load (PasswordHelper.TextoMinimoNueva) --%>
                                <small class="form-text text-muted"><%: WebSGV.Helpers.PoliticaContrasena.TextoAyuda %></small>
                                <%-- Dentro de pnlContrasena: solo en el alta (en la edición el panel no se pinta ni se valida). --%>
                                <asp:RequiredFieldValidator ID="rfvContrasena" runat="server" ControlToValidate="txtContrasena"
                                    ErrorMessage="Escriba la contraseña del nuevo usuario." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgUsuario" />
                            </div>
                        </asp:Panel>
                    </div>
                    <div class="row">
                        <div class="col-md-12">
                            <div class="form-group">
                                <%-- Obligatorio solo con el rol Conductor: GestionUsuarios.js muestra u oculta el asterisco y aria-required. --%>
                                <label for="<%= ddlConductor.ClientID %>">Conductor asociado <span id="guObligConductor" class="sgv-obligatorio" aria-hidden="true">*</span></label>
                                <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control" EnableViewState="false" aria-required="true" />
                                <%-- Sin OnServerValidate: btnGuardarUsuario_Click ya lo rechaza con su mensaje (y lo llama también
                                     btnConfirmarReaut_Click, que valida otro grupo). --%>
                                <asp:CustomValidator ID="cvConductor" runat="server" ControlToValidate="ddlConductor" ValidateEmptyText="true"
                                    ClientValidationFunction="guValidarConductor"
                                    ErrorMessage="Asocie un conductor al usuario con rol Conductor." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgUsuario" />
                                <small class="text-muted">
                                    Es obligatorio cuando el rol es Conductor. Esta asociación determina qué viajes puede consultar y firmar el usuario.
                                </small>
                            </div>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnGuardarUsuario" runat="server" Text="Guardar"
                        CssClass="btn btn-primary" OnClick="btnGuardarUsuario_Click" ValidationGroup="vgUsuario" />
                </div>
            </div>
        </div>
    </div>

    <!-- ======================================================
         MODAL: Restablecer Contraseña
         ====================================================== -->
    <div class="modal fade" id="modalResetPass" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalResetPassTitulo" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered" role="document">
            <div class="modal-content" data-enter="<%= btnResetPass.ClientID %>">
                <div class="modal-header sgv-modal-alerta">
                    <h5 class="modal-title" id="modalResetPassTitulo">
                        Restablecer Contraseña
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <asp:HiddenField ID="hfIdUsuarioReset" runat="server" Value="0" />
                    <p class="text-muted mb-3">
                        Restableciendo contraseña para:
                        <strong><asp:Label ID="lblUsuarioReset" runat="server" /></strong>
                    </p>

                    <asp:Panel ID="pnlMensajeReset" runat="server" Visible="false" CssClass="mb-3">
                        <asp:Label ID="lblMensajeReset" runat="server" />
                    </asp:Panel>

                    <div class="form-group">
                        <label for="<%= txtNuevaPass.ClientID %>">Nueva contraseña <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtNuevaPass" runat="server" CssClass="form-control"
                            TextMode="Password" autocomplete="new-password" aria-required="true" /><%-- placeholder: Page_Load (PasswordHelper.TextoMinimoNueva) --%>
                        <small class="form-text text-muted"><%: WebSGV.Helpers.PoliticaContrasena.TextoAyuda %></small>
                        <asp:RequiredFieldValidator ID="rfvNuevaPass" runat="server" ControlToValidate="txtNuevaPass"
                            ErrorMessage="Escriba la nueva contraseña." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgReset" />
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtConfirmarPass.ClientID %>">Confirmar contraseña <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtConfirmarPass" runat="server" CssClass="form-control"
                            TextMode="Password" placeholder="Repite la nueva contraseña" autocomplete="new-password" aria-required="true" />
                        <asp:RequiredFieldValidator ID="rfvConfirmarPass" runat="server" ControlToValidate="txtConfirmarPass"
                            ErrorMessage="Repita la nueva contraseña." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgReset" />
                        <asp:CompareValidator ID="cmpConfirmarPass" runat="server" ControlToValidate="txtConfirmarPass" ControlToCompare="txtNuevaPass"
                            ErrorMessage="Las contraseñas no coinciden." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgReset" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnResetPass" runat="server" Text="Restablecer"
                        CssClass="btn btn-warning" OnClick="btnResetPass_Click" ValidationGroup="vgReset" />
                </div>
            </div>
        </div>
    </div>

    <!-- ======================================================
         MODAL: Confirmar con la contraseña del administrador (E4-12)
         ====================================================== -->
    <div class="modal fade" id="modalReautenticar" tabindex="-1" role="dialog" aria-labelledby="modalReautenticarLabel" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered" role="document">
            <div class="modal-content" data-enter="<%= btnConfirmarReaut.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalReautenticarLabel">
                        Confirma que eres tú
                    </h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Cerrar">
                        <span aria-hidden="true">&times;</span>
                    </button>
                </div>
                <div class="modal-body">
                    <asp:HiddenField ID="hfAccionReaut" runat="server" Value="" />
                    <p class="mb-3">
                        Para <asp:Label ID="lblAccionReaut" runat="server" />, escribe tu contraseña.
                        <span class="text-muted d-block small mt-1">No se volverá a pedir durante <%: (int)WebSGV.Services.Usuarios.AdminUsuariosReglas.VentanaReautenticacion.TotalMinutes %> minutos.</span>
                    </p>

                    <asp:Panel ID="pnlMensajeReaut" runat="server" Visible="false" CssClass="mb-3">
                        <asp:Label ID="lblMensajeReaut" runat="server" />
                    </asp:Panel>

                    <div class="form-group mb-0">
                        <label for="<%= txtContrasenaAdmin.ClientID %>">Tu contraseña <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtContrasenaAdmin" runat="server" CssClass="form-control"
                            TextMode="Password" autocomplete="current-password" MaxLength="200" aria-required="true" />
                        <asp:RequiredFieldValidator ID="rfvContrasenaAdmin" runat="server" ControlToValidate="txtContrasenaAdmin"
                            ErrorMessage="Escriba su contraseña." Display="Dynamic" CssClass="sgv-campo-error" SetFocusOnError="true" ValidationGroup="vgReaut" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnConfirmarReaut" runat="server" Text="Confirmar"
                        CssClass="btn btn-primary" OnClick="btnConfirmarReaut_Click" ValidationGroup="vgReaut" />
                </div>
            </div>
        </div>
    </div>


    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfAccionReaut: '<%= hfAccionReaut.ClientID %>',
            ddlRol: '<%= ddlRol.ClientID %>',
            ddlConductorUsuario: '<%= ddlConductor.ClientID %>',
            cvConductor: '<%= cvConductor.ClientID %>',
            hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>',
            btnAccionFila: '<%= btnAccionFila.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/GestionUsuarios.js") %>"></script>
</asp:Content>
