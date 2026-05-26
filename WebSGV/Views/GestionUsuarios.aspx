<%@ Page Title="Gestión de Usuarios" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="GestionUsuarios.aspx.cs"
    Inherits="WebSGV.Views.GestionUsuarios" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <!-- Encabezado -->
    <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
            <h2 class="mb-0"><i class="fas fa-users-cog text-primary mr-2"></i>Gestión de Usuarios</h2>
            <small class="text-muted">Administrar cuentas, roles y accesos al sistema</small>
        </div>
        <asp:Button ID="btnNuevoUsuario" runat="server" Text="+ Nuevo Usuario"
            CssClass="btn btn-primary" OnClick="btnNuevoUsuario_Click" />
    </div>

    <!-- Filtros -->
    <div class="card shadow-sm mb-3">
        <div class="card-body py-2">
            <div class="row align-items-end">
                <div class="col-md-4 mb-2 mb-md-0">
                    <asp:TextBox ID="txtBuscar" runat="server" CssClass="form-control form-control-sm"
                        placeholder="Buscar por usuario o nombre..." />
                </div>
                <div class="col-md-3 mb-2 mb-md-0">
                    <asp:DropDownList ID="ddlFiltroRol" runat="server" CssClass="form-control form-control-sm">
                        <asp:ListItem Value="">-- Todos los roles --</asp:ListItem>
                        <asp:ListItem Value="ADMIN">Administrador</asp:ListItem>
                        <asp:ListItem Value="SUPERVISOR">Supervisor</asp:ListItem>
                        <asp:ListItem Value="CONDUCTOR">Conductor</asp:ListItem>
                        <asp:ListItem Value="ADMINISTRADOR DE GRIFO">Admin de Grifo</asp:ListItem>
                        <asp:ListItem Value="ADMINISTRADOR DE MAQUINARIA">Admin de Maquinaria</asp:ListItem>
                        <asp:ListItem Value="OPERADOR">Operador</asp:ListItem>
                        <asp:ListItem Value="ADMINISTRADOR DE SISTEMA">Admin de Sistema</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="col-md-2 mb-2 mb-md-0">
                    <asp:DropDownList ID="ddlFiltroEstado" runat="server" CssClass="form-control form-control-sm">
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
            <asp:GridView ID="gvUsuarios" runat="server"
                AutoGenerateColumns="false"
                CssClass="table table-hover mb-0"
                GridLines="None"
                OnRowCommand="gvUsuarios_RowCommand">
                <Columns>
                    <asp:BoundField DataField="idUsuario" HeaderText="#" ItemStyle-Width="50px" />
                    <asp:BoundField DataField="nombreUsuario" HeaderText="Usuario" />
                    <asp:BoundField DataField="nombre" HeaderText="Nombre Completo" />
                    <asp:TemplateField HeaderText="Rol">
                        <ItemTemplate>
                            <span class="badge badge-info"><%# Eval("rol") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Estado" ItemStyle-Width="90px">
                        <ItemTemplate>
                            <span class='badge <%# (bool)Eval("activo") ? "badge-success" : "badge-danger" %>'>
                                <%# (bool)Eval("activo") ? "Activo" : "Inactivo" %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Acciones" ItemStyle-Width="250px">
                        <ItemTemplate>
                            <asp:Button runat="server" Text="Editar" CssClass="btn btn-sm btn-info mr-1"
                                CommandName="EditarUsuario" CommandArgument='<%# Eval("idUsuario") %>' />
                            <asp:Button runat="server"
                                Text='<%# (bool)Eval("activo") ? "Desactivar" : "Activar" %>'
                                CssClass='<%# (bool)Eval("activo") ? "btn btn-sm btn-warning mr-1" : "btn btn-sm btn-success mr-1" %>'
                                CommandName="ToggleActivo" CommandArgument='<%# Eval("idUsuario") %>'
                                OnClientClick="return confirm('¿Confirmar cambio de estado del usuario?');" />
                            <asp:Button runat="server" Text="Contraseña" CssClass="btn btn-sm btn-secondary"
                                CommandName="ResetPassword" CommandArgument='<%# Eval("idUsuario") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div class="text-center py-4 text-muted">
                        <i class="fas fa-users fa-2x mb-2 d-block"></i>
                        No se encontraron usuarios con los filtros seleccionados.
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>
    </div>

    <!-- ======================================================
         MODAL: Crear / Editar Usuario
         ====================================================== -->
    <div class="modal fade" id="modalUsuario" tabindex="-1" role="dialog" aria-labelledby="modalUsuarioLabel" aria-hidden="true">
        <div class="modal-dialog modal-lg modal-dialog-centered" role="document">
            <div class="modal-content">
                <div class="modal-header bg-primary text-white">
                    <h5 class="modal-title" id="modalUsuarioLabel">
                        <i class="fas fa-user-edit mr-2"></i>
                        <asp:Label ID="lblTituloModal" runat="server" Text="Nuevo Usuario" />
                    </h5>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Cerrar">
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
                                <label>Nombre de Usuario <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombreUsuario" runat="server" CssClass="form-control"
                                    placeholder="Sin espacios, ej: jperez" MaxLength="100" />
                                <small class="text-muted">Solo letras, números y guiones bajos.</small>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label>Nombre Completo <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                    placeholder="Ej: Juan Pérez García" MaxLength="200" />
                            </div>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-6">
                            <div class="form-group">
                                <label>Rol <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlRol" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="ADMIN">Administrador</asp:ListItem>
                                    <asp:ListItem Value="SUPERVISOR">Supervisor</asp:ListItem>
                                    <asp:ListItem Value="CONDUCTOR">Conductor</asp:ListItem>
                                    <asp:ListItem Value="ADMINISTRADOR DE GRIFO">Administrador de Grifo</asp:ListItem>
                                    <asp:ListItem Value="ADMINISTRADOR DE MAQUINARIA">Administrador de Maquinaria</asp:ListItem>
                                    <asp:ListItem Value="OPERADOR">Operador</asp:ListItem>
                                    <asp:ListItem Value="ADMINISTRADOR DE SISTEMA">Administrador de Sistema</asp:ListItem>
                                </asp:DropDownList>
                            </div>
                        </div>
                        <asp:Panel ID="pnlContrasena" runat="server" CssClass="col-md-6">
                            <div class="form-group">
                                <label>Contraseña <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtContrasena" runat="server" CssClass="form-control"
                                    TextMode="Password" placeholder="Mínimo 6 caracteres" />
                            </div>
                        </asp:Panel>
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
    <div class="modal fade" id="modalResetPass" tabindex="-1" role="dialog" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered" role="document">
            <div class="modal-content">
                <div class="modal-header bg-warning text-dark">
                    <h5 class="modal-title">
                        <i class="fas fa-key mr-2"></i>Restablecer Contraseña
                    </h5>
                    <button type="button" class="close" data-dismiss="modal">
                        <span>&times;</span>
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
                        <label>Nueva Contraseña <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtNuevaPass" runat="server" CssClass="form-control"
                            TextMode="Password" placeholder="Mínimo 6 caracteres" />
                    </div>
                    <div class="form-group mb-0">
                        <label>Confirmar Contraseña <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtConfirmarPass" runat="server" CssClass="form-control"
                            TextMode="Password" placeholder="Repite la nueva contraseña" />
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

</asp:Content>
