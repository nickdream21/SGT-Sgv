<%@ Page Title="Peajes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroPeajes.aspx.cs" Inherits="WebSGV.Views.RegistroPeajes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdEstacion" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Peajes <asp:Label ID="lblTotalPeajes" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
                <p>Administra las estaciones de peaje disponibles para las liquidaciones</p>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Estaci&#xF3;n</h5>
                    </div>
                    <div class="card-body">
                        <asp:ValidationSummary ID="vsRegistroPeaje" runat="server" ValidationGroup="vgRegistroPeaje" CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                        <div class="form-group">
                            <label for="<%= txtNombre.ClientID %>" class="font-weight-bold">Nombre de la Estaci&#xF3;n <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                placeholder="Ej: ESTACION DE PEAJE CHICAMA" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvNombrePeaje" runat="server" ValidationGroup="vgRegistroPeaje"
                                ControlToValidate="txtNombre" ErrorMessage="Nombre de estación: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revNombrePeaje" runat="server" ValidationGroup="vgRegistroPeaje"
                                ControlToValidate="txtNombre" ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.,]{3,100}$" ErrorMessage="Nombre de estación: use entre 3 y 100 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                            <small class="form-text text-muted">
                                <i class="fas fa-info-circle mr-1"></i>El nombre se guardar&#xE1; en may&#xFA;sculas.
                            </small>
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar peaje" ValidationGroup="vgRegistroPeaje"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Las estaciones <strong>activas</strong> aparecer&#xE1;n en el selector de peajes al registrar liquidaciones.
                    Las inactivas quedan ocultas pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Estaciones registradas</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_peajes" aria-label="Buscar peajes"
                            data-accion-input="filtrarTabla" data-contenedor="contenedorPeajes">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorPeajes">
                            <asp:GridView ID="gvPeajes" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay estaciones de peaje registradas."
                                OnRowCommand="gvPeajes_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idEstacion" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE DE LA ESTACI&#xD3;N" />
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="90">
                                        <ItemTemplate>
                                            <span class='badge <%#: ObtenerClaseEstado(Eval("activo")) %>'>
                                                <%#: ObtenerTextoEstado(Eval("activo")) %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="" ItemStyle-CssClass="text-center" ItemStyle-Width="40">
                                        <ItemTemplate>
                                            <button type="button" class="btn btn-outline-info btn-sm btn-editar" title="Editar"
                                                data-id='<%#: Eval("idEstacion") %>'
                                                data-nombre='<%# AttrEncode(Eval("nombre")) %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# ArgumentoEstado(Eval("idEstacion"), Eval("activo")) %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                data-confirmar="¿Cambiar el estado de este peaje?">
                                                <%#: ObtenerTextoBoton(Eval("activo")) %>
                                            </asp:LinkButton>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                        </div>
                    </div>
                </div>
            </div>

        </div>
    </div>

    <!-- Modal Editar Peaje -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarPeaje.ClientID %>">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title" id="modalEditarTitulo"><i class="fas fa-road mr-2" aria-hidden="true"></i>Editar Estaci&#xF3;n de Peaje</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-group mb-0">
                        <label for="<%= txtEditarNombre.ClientID %>" class="font-weight-bold">Nombre de la Estaci&#xF3;n <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarNombre" runat="server" CssClass="form-control text-uppercase" MaxLength="100"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarPeaje" runat="server" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarPeaje_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdEstacion: '<%= hfIdEstacion.ClientID %>',
            txtEditarNombre: '<%= txtEditarNombre.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroPeajes.js") %>"></script>

</asp:Content>
