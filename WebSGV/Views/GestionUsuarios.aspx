<%@ Page Title="Gestión de Usuarios" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="GestionUsuarios.aspx.cs"
    Inherits="WebSGV.Views.GestionUsuarios" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <!-- Encabezado -->
    <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
            <h2 class="mb-0"><i class="fas fa-users-cog text-primary mr-2"></i>Gestión de usuarios</h2>
            <small class="text-muted">Administrar cuentas, roles y accesos al sistema</small>
        </div>
        <asp:Button ID="btnNuevoUsuario" runat="server" Text="+ Nuevo Usuario"
            CssClass="btn btn-primary" OnClick="btnNuevoUsuario_Click" />
    </div>

    <!-- Filtros -->
    <div class="card shadow-sm mb-3">
        <div class="card-body py-2">
            <div class="row align-items-end" data-enter="<%= btnBuscar.ClientID %>">
                <div class="col-md-4 mb-2 mb-md-0">
                    <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm"
                        placeholder="Buscar por usuario o nombre..." aria-label="Buscar por usuario o nombre" />
                </div>
                 <div class="col-md-3 mb-2 mb-md-0">
                      <asp:DropDownList ID="ddlFiltroRol" runat="server" CssClass="form-control form-control-sm" aria-label="Filtrar por rol" />
                 </div>
                <div class="col-md-2 mb-2 mb-md-0">
                    <asp:DropDownList ID="ddlFiltroEstado" runat="server" CssClass="form-control form-control-sm" aria-label="Filtrar por estado">
                        <asp:ListItem Value="">-- Estado --</asp:ListItem>
                        <asp:ListItem Value="1">Activos</asp:ListItem>
                        <asp:ListItem Value="0">Inactivos</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="col-md-3">
                    <asp:Button ID="btnBuscar" runat="server" Text="Buscar"
                        CssClass="btn btn-secondary btn-sm mr-1" OnClick="btnBuscar_Click" />
                    <asp:Button ID="btnLimpiar" runat="server" Text="Limpiar"
                        CssClass="btn btn-outline-secondary btn-sm" OnClick="btnLimpiar_Click" />
                </div>
            </div>
        </div>
    </div>

    <!-- Mensaje de feedback -->
    <asp:Panel ID="pnlMensaje" runat="server" Visible="false" CssClass="mb-3">
        <asp:Label ID="lblMensaje" runat="server" />
    </asp:Panel>

    <!-- Tabla de usuarios -->
    <div class="card shadow-sm">
        <div class="card-body p-0">
            <div class="table-responsive">
            <asp:GridView ID="gvUsuarios" runat="server"
                AutoGenerateColumns="false"
                CssClass="table table-hover mb-0"
                GridLines="None"
                OnRowCommand="gvUsuarios_RowCommand">
                <Columns>
                    <asp:BoundField DataField="idUsuario" HeaderText="#" ItemStyle-Width="50px" />
                    <asp:BoundField DataField="nombreUsuario" HeaderText="Usuario" />
                    <asp:BoundField DataField="nombre" HeaderText="Nombre completo" />
                    <asp:TemplateField HeaderText="Rol">
                        <ItemTemplate>
                            <span class="badge badge-info"><%#: Eval("rol") %></span>
                            <%-- E4-14: verificación en dos pasos activa. --%>
                            <asp:PlaceHolder runat="server" Visible='<%# SegundoFactorActivo(Eval("segundoFactor")) %>'>
                                <span class="badge badge-success" title="Verificación en dos pasos activa">2 pasos</span>
                            </asp:PlaceHolder>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Estado" ItemStyle-Width="90px">
                        <ItemTemplate>
                            <span class='badge <%#: (bool)Eval("activo") ? "badge-success" : "badge-danger" %>'>
                                <%#: (bool)Eval("activo") ? "Activo" : "Inactivo" %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="270px" ItemStyle-Wrap="false">
                        <ItemTemplate>
                            <asp:Button runat="server" Text="Editar" CssClass="btn btn-sm btn-info mr-1"
                                CommandName="EditarUsuario" CommandArgument='<%# Eval("idUsuario") %>' />
                            <%-- Fila propia (E4-12): sin Desactivar ni Contraseña; el servidor también lo rechaza. --%>
                            <asp:Button runat="server" Visible='<%# AccionesFila(Eval("idUsuario")).PuedeDesactivar %>'
                                Text='<%# (bool)Eval("activo") ? "Desactivar" : "Activar" %>'
                                CssClass='<%# (bool)Eval("activo") ? "btn btn-sm btn-warning mr-1" : "btn btn-sm btn-success mr-1" %>'
                                CommandName="ToggleActivo" CommandArgument='<%# ArgumentoEstado(Eval("idUsuario"), Eval("activo")) %>'
                                data-confirmar='<%# ConfirmarCambioEstado(Eval("activo"), Eval("nombreUsuario")) %>'
                                data-confirmar-peligro='<%# (bool)Eval("activo") ? "true" : "false" %>' />
                            <asp:Button runat="server" Text="Contraseña" CssClass="btn btn-sm btn-secondary"
                                Visible='<%# AccionesFila(Eval("idUsuario")).PuedeRestablecerContrasena %>'
                                CommandName="ResetPassword" CommandArgument='<%# Eval("idUsuario") %>' />
                            <%-- E4-14: celular y códigos perdidos; no en la fila propia. Pide la contraseña del administrador. --%>
                            <asp:Button runat="server" Text="Quitar 2.º factor" CssClass="btn btn-sm btn-outline-danger ml-1"
                                Visible='<%# PuedeQuitarSegundoFactor(Eval("idUsuario"), Eval("segundoFactor")) %>'
                                CommandName="QuitarSegundoFactor" CommandArgument='<%# Eval("idUsuario") %>'
                                data-confirmar='<%# ConfirmarQuitarSegundoFactor(Eval("nombreUsuario")) %>'
                                data-confirmar-titulo="Quitar segundo factor" data-confirmar-peligro="true" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div class="text-center py-4 text-muted">
                        No se encontraron usuarios con los filtros seleccionados.
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
            </div>
        </div>
    </div>

    <!-- ======================================================
         MODAL: Crear / Editar Usuario
         ====================================================== -->
    <div class="modal fade" id="modalUsuario" tabindex="-1" role="dialog" aria-labelledby="modalUsuarioLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg modal-dialog-centered" role="document">
            <div class="modal-content" data-enter="<%= btnGuardarUsuario.ClientID %>">
                <div class="modal-header">
                    <h5 class="modal-title" id="modalUsuarioLabel">
                        <i class="fas fa-user-edit mr-2"></i>
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
                                <label for="<%= txtNombreUsuario.ClientID %>">Nombre de usuario <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombreUsuario" runat="server" CssClass="form-control"
                                    placeholder="Sin espacios, ej: jperez" MaxLength="100" autocomplete="off" />
                                <small class="text-muted">3 a 50 caracteres: letras, números, punto y guion bajo.</small>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="<%= txtNombre.ClientID %>">Nombre completo <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                    placeholder="Ej: Juan Pérez García" MaxLength="200" />
                            </div>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="<%= ddlRol.ClientID %>">Rol <span class="text-danger">*</span></label>
                                 <asp:DropDownList ID="ddlRol" runat="server" CssClass="form-control" />
                             </div>
                         </div>
                        <asp:Panel ID="pnlContrasena" runat="server" CssClass="col-md-6">
                            <div class="form-group">
                                <label for="<%= txtContrasena.ClientID %>">Contraseña <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtContrasena" runat="server" CssClass="form-control"
                                    TextMode="Password" autocomplete="new-password" /><%-- placeholder: Page_Load (PasswordHelper.TextoMinimoNueva) --%>
                                <small class="form-text text-muted"><%: WebSGV.Helpers.PoliticaContrasena.TextoAyuda %></small>
                            </div>
                        </asp:Panel>
                    </div>
                    <div class="row">
                        <div class="col-md-12">
                            <div class="form-group">
                                <label for="<%= ddlConductor.ClientID %>">Conductor asociado</label>
                                <asp:DropDownList ID="ddlConductor" runat="server" CssClass="form-control" />
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
                        CssClass="btn btn-primary" OnClick="btnGuardarUsuario_Click" />
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
                        <i class="fas fa-key mr-2" aria-hidden="true"></i>Restablecer Contraseña
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
                        <label for="<%= txtNuevaPass.ClientID %>">Nueva contraseña <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtNuevaPass" runat="server" CssClass="form-control"
                            TextMode="Password" autocomplete="new-password" /><%-- placeholder: Page_Load (PasswordHelper.TextoMinimoNueva) --%>
                        <small class="form-text text-muted"><%: WebSGV.Helpers.PoliticaContrasena.TextoAyuda %></small>
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtConfirmarPass.ClientID %>">Confirmar contraseña <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtConfirmarPass" runat="server" CssClass="form-control"
                            TextMode="Password" placeholder="Repite la nueva contraseña" autocomplete="new-password" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnResetPass" runat="server" Text="Restablecer"
                        CssClass="btn btn-warning" OnClick="btnResetPass_Click" />
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
                        <i class="fas fa-user-shield mr-2"></i>Confirma que eres tú
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
                        <label for="<%= txtContrasenaAdmin.ClientID %>">Tu contraseña <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtContrasenaAdmin" runat="server" CssClass="form-control"
                            TextMode="Password" autocomplete="current-password" MaxLength="200" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnConfirmarReaut" runat="server" Text="Confirmar"
                        CssClass="btn btn-primary" OnClick="btnConfirmarReaut_Click" />
                </div>
            </div>
        </div>
    </div>


    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfAccionReaut: '<%= hfAccionReaut.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/GestionUsuarios.js") %>"></script>
</asp:Content>
