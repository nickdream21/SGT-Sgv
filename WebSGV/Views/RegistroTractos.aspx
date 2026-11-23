<%@ Page Title="Tractos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroTractos.aspx.cs" Inherits="WebSGV.Views.RegistroTractos" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdTracto" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="sgv-encabezado">
            <div class="sgv-encabezado-titulo">
                <h1 class="h2">Tractos <asp:Label ID="lblTotalTractos" runat="server" CssClass="sgv-encabezado-contador" Text="0 registro(s)" /></h1>
                <p>Administra los tractos disponibles para las operaciones</p>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar tracto</h5>
                    </div>
                    <div class="card-body">
                        <asp:ValidationSummary ID="vsRegistroTracto" runat="server" ValidationGroup="vgRegistroTracto" CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                        <div class="form-group">
                            <label for="<%= txtPlaca.ClientID %>" class="font-weight-bold">N&#xBA; de Placa <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtPlaca" runat="server" CssClass="form-control"
                                placeholder="Ej: ABC-123" MaxLength="10"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvPlaca" runat="server" ControlToValidate="txtPlaca" ValidationGroup="vgRegistroTracto"
                                ErrorMessage="Placa: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revPlaca" runat="server" ControlToValidate="txtPlaca" ValidationGroup="vgRegistroTracto"
                                ValidationExpression="^[A-Za-z0-9\-]{6,10}$" ErrorMessage="Placa: use 6 a 10 caracteres (letras, números o guion)." CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="form-group">
                            <label for="<%= txtMarca.ClientID %>" class="font-weight-bold">Marca <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtMarca" runat="server" CssClass="form-control"
                                placeholder="Ej: VOLVO" MaxLength="30"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvMarca" runat="server" ControlToValidate="txtMarca" ValidationGroup="vgRegistroTracto"
                                ErrorMessage="Marca: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revMarca" runat="server" ControlToValidate="txtMarca" ValidationGroup="vgRegistroTracto"
                                ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.]{2,30}$" ErrorMessage="Marca: use entre 2 y 30 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <div class="form-group">
                            <label for="<%= txtModelo.ClientID %>" class="font-weight-bold">Modelo <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtModelo" runat="server" CssClass="form-control"
                                placeholder="Ej: FH 460" MaxLength="30"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvModelo" runat="server" ControlToValidate="txtModelo" ValidationGroup="vgRegistroTracto"
                                ErrorMessage="Modelo: este campo es obligatorio." CssClass="text-danger small" Display="Dynamic" />
                            <asp:RegularExpressionValidator ID="revModelo" runat="server" ControlToValidate="txtModelo" ValidationGroup="vgRegistroTracto"
                                ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.]{2,30}$" ErrorMessage="Modelo: use entre 2 y 30 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar tracto" ValidationGroup="vgRegistroTracto"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los tractos <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Tractos registrados</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_tractos" aria-label="Buscar tractos"
                            data-accion-input="filtrarTabla" data-contenedor="contenedorTractos">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorTractos">
                            <asp:GridView ID="gvTractos" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay tractos registrados."
                                OnRowCommand="gvTractos_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idTracto" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="placaTracto" HeaderText="PLACA" ItemStyle-CssClass="font-weight-bold" />
                                    <asp:BoundField DataField="marca" HeaderText="MARCA" />
                                    <asp:BoundField DataField="modelo" HeaderText="MODELO" />
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
                                                data-id='<%#: Eval("idTracto") %>'
                                                data-placa='<%# AttrEncode(Eval("placaTracto")) %>'
                                                data-marca='<%# AttrEncode(Eval("marca")) %>'
                                                data-modelo='<%# AttrEncode(Eval("modelo")) %>'
                                                data-con-historial='<%#: ConHistorial(Eval("conHistorial")) %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCION" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# ArgumentoEstado(Eval("idTracto"), Eval("activo")) %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                data-confirmar="¿Cambiar el estado de este tracto?">
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

    <!-- Modal Editar Tracto -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog" aria-modal="true" aria-labelledby="modalEditarTitulo">
        <div class="modal-dialog" role="document">
            <div class="modal-content" data-enter="<%= btnActualizarTracto.ClientID %>">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title" id="modalEditarTitulo"><i class="fas fa-truck mr-2" aria-hidden="true"></i>Editar tracto</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal" aria-label="Cerrar">&times;</button>
                </div>
                <div class="modal-body">
                    <asp:Panel ID="pnlErrorModal" runat="server" Visible="false" EnableViewState="false" CssClass="alert alert-danger" role="alert">
                        <asp:Label ID="lblErrorModal" runat="server" EnableViewState="false"></asp:Label>
                    </asp:Panel>
                    <div class="form-group">
                        <label for="<%= txtEditarPlaca.ClientID %>" class="font-weight-bold">N&#xBA; de Placa <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarPlaca" runat="server" CssClass="form-control text-uppercase" MaxLength="10"></asp:TextBox>
                        <small id="notaPlacaHistorial" class="form-text text-muted d-none">
                            <i class="fas fa-lock mr-1"></i>Este tracto tiene despachos u otro historial: la placa no se puede cambiar.
                            Si la placa est&#xE1; mal, registre el tracto correcto y desactive este.
                        </small>
                    </div>
                    <div class="form-group">
                        <label for="<%= txtEditarMarca.ClientID %>" class="font-weight-bold">Marca <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarMarca" runat="server" CssClass="form-control text-uppercase" MaxLength="30"></asp:TextBox>
                    </div>
                    <div class="form-group mb-0">
                        <label for="<%= txtEditarModelo.ClientID %>" class="font-weight-bold">Modelo <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtEditarModelo" runat="server" CssClass="form-control text-uppercase" MaxLength="30"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarTracto" runat="server" CssClass="btn btn-info"
                        Text="Guardar cambios" OnClick="btnActualizarTracto_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdTracto: '<%= hfIdTracto.ClientID %>',
            txtEditarPlaca: '<%= txtEditarPlaca.ClientID %>',
            txtEditarMarca: '<%= txtEditarMarca.ClientID %>',
            txtEditarModelo: '<%= txtEditarModelo.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            pnlErrorModal: '<%= pnlErrorModal.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroTractos.js") %>"></script>

</asp:Content>
