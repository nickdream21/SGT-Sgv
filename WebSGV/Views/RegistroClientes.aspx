<%@ Page Title="Registro de Clientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroClientes.aspx.cs" Inherits="WebSGV.Views.RegistroClientes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdCliente" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-building mr-2" style="color:#2563eb;"></i>Registro de Clientes
                        </h2>
                        <p class="text-muted mb-0">Administra los clientes disponibles para las operaciones</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalClientes" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Cliente</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <asp:ValidationSummary ID="vsRegistroCliente" runat="server" ValidationGroup="vgRegistroCliente" CssClass="text-danger small mb-3" DisplayMode="BulletList" />
                            <div class="form-group">
                                <label class="font-weight-bold">RUC (Opcional):</label>
                                <div class="input-group">
                                    <asp:TextBox ID="txtRUC" runat="server" CssClass="form-control" MaxLength="11"></asp:TextBox>
                                    <div class="input-group-append">
                                        <asp:Button ID="btnBuscarRUC" runat="server" CssClass="btn btn-secondary" Text="Verificar" OnClientClick="verificarRUC(); return false;" />
                                    </div>
                                </div>
                                <asp:RegularExpressionValidator ID="revRuc" runat="server" ControlToValidate="txtRUC" ValidationGroup="vgRegistroCliente"
                                    ValidationExpression="^$|^\d{11}$" ErrorMessage="RUC: ingrese exactamente 11 dígitos o deje el campo vacío." CssClass="text-danger small" Display="Dynamic" />
                                <small class="form-text text-muted">Ingrese el RUC si lo conoce, o deje en blanco.</small>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Nombre / Raz&#xF3;n Social: <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgRegistroCliente"
                                    ControlToValidate="txtNombre"
                                    ErrorMessage="El nombre del cliente es obligatorio."
                                    CssClass="text-danger small"
                                    Display="Dynamic">
                                </asp:RequiredFieldValidator>
                                <asp:RegularExpressionValidator ID="revNombre" runat="server" ControlToValidate="txtNombre" ValidationGroup="vgRegistroCliente"
                                    ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú0-9\s\-\.&]{3,200}$" ErrorMessage="Nombre/Razón Social: use entre 3 y 200 caracteres válidos." CssClass="text-danger small" Display="Dynamic" />
                            </div>

                            <hr class="my-3" />
                            <p class="text-muted small mb-2 font-weight-bold text-uppercase" style="letter-spacing:.05em;">Datos comerciales</p>

                            <div class="form-group">
                                <label class="font-weight-bold">Direcci&#xF3;n fiscal:</label>
                                <asp:TextBox ID="txtDireccion" runat="server" CssClass="form-control" MaxLength="250"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Persona de contacto:</label>
                                <asp:TextBox ID="txtContacto" runat="server" CssClass="form-control" MaxLength="150"></asp:TextBox>
                            </div>
                            <div class="form-row">
                                <div class="form-group col-6">
                                    <label class="font-weight-bold">Tel&#xE9;fono:</label>
                                    <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control" MaxLength="50"></asp:TextBox>
                                </div>
                                <div class="form-group col-6">
                                    <label class="font-weight-bold">Moneda:</label>
                                    <asp:DropDownList ID="ddlMoneda" runat="server" CssClass="form-control">
                                        <asp:ListItem Value="PEN" Text="PEN - Soles" Selected="True"></asp:ListItem>
                                        <asp:ListItem Value="USD" Text="USD - D&#xF3;lares"></asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Correo:</label>
                                <asp:TextBox ID="txtCorreo" runat="server" CssClass="form-control" MaxLength="150" TextMode="Email"></asp:TextBox>
                                <asp:RegularExpressionValidator ID="revCorreo" runat="server" ControlToValidate="txtCorreo" ValidationGroup="vgRegistroCliente"
                                    ValidationExpression="^$|^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="Correo: ingrese una direcci&#xF3;n v&#xE1;lida o deje el campo vac&#xED;o." CssClass="text-danger small" Display="Dynamic" />
                            </div>
                            <div class="form-group">
                                <div class="custom-control custom-checkbox">
                                    <asp:CheckBox ID="chkEsExportador" runat="server" CssClass="mr-2" Text="Opera carga internacional" />
                                </div>
                                <small class="form-text text-muted">M&#xE1;rquelo si el cliente exporta: habilita su seguimiento de exportaci&#xF3;n.</small>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Observaciones:</label>
                                <asp:TextBox ID="txtObservaciones" runat="server" CssClass="form-control" MaxLength="500"
                                    TextMode="MultiLine" Rows="2"></asp:TextBox>
                            </div>

                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-block"
                                Text="Registrar Cliente" ValidationGroup="vgRegistroCliente" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los clientes <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Clientes Registrados</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_clientes"
                            oninput="filtrarTabla(this.value,'contenedorClientes')">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorClientes">
                            <asp:GridView ID="gvClientes" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay clientes registrados."
                                OnRowCommand="gvClientes_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idCliente" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="ruc" HeaderText="RUC" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE / RAZ&#xD3;N SOCIAL" />
                                    <asp:TemplateField HeaderText="CONTACTO">
                                        <ItemTemplate>
                                            <%# Eval("contacto") %>
                                            <asp:Literal ID="litTel" runat="server"
                                                Text='<%# string.IsNullOrWhiteSpace(Eval("telefono") as string) ? "" : "<br /><small class=\"text-muted\">" + System.Web.HttpUtility.HtmlEncode(Eval("telefono")) + "</small>" %>' />
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="OPERACI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="110">
                                        <ItemTemplate>
                                            <span class='badge <%# Convert.ToBoolean(Eval("esExportador")) ? "badge-info" : "badge-secondary" %>'>
                                                <%# Convert.ToBoolean(Eval("esExportador")) ? "Exportador" : "Nacional" %>
                                            </span>
                                            <br /><small class="text-muted"><%# Eval("monedaFacturacion") %></small>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="90">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseEstado(Eval("activo")) %>'>
                                                <%# ObtenerTextoEstado(Eval("activo")) %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="" ItemStyle-CssClass="text-center" ItemStyle-Width="40">
                                        <ItemTemplate>
                                            <button type="button" class="btn btn-outline-info btn-sm btn-editar" title="Editar"
                                                data-id='<%# Eval("idCliente") %>'
                                                data-ruc='<%# AttrEncode(Eval("ruc")) %>'
                                                data-nombre='<%# AttrEncode(Eval("nombre")) %>'
                                                data-direccion='<%# AttrEncode(Eval("direccion")) %>'
                                                data-contacto='<%# AttrEncode(Eval("contacto")) %>'
                                                data-telefono='<%# AttrEncode(Eval("telefono")) %>'
                                                data-correo='<%# AttrEncode(Eval("correo")) %>'
                                                data-moneda='<%# AttrEncode(Eval("monedaFacturacion")) %>'
                                                data-exportador='<%# Convert.ToBoolean(Eval("esExportador")) ? "1" : "0" %>'
                                                data-observaciones='<%# AttrEncode(Eval("observaciones")) %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCION" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idCliente") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este cliente?');">
                                                <%# ObtenerTextoBoton(Eval("activo")) %>
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

    <!-- Modal Editar Cliente -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title"><i class="fas fa-building mr-2"></i>Editar Cliente</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="form-row">
                        <div class="form-group col-md-4">
                            <label class="font-weight-bold">RUC (Opcional):</label>
                            <asp:TextBox ID="txtEditarRUC" runat="server" CssClass="form-control" MaxLength="11"></asp:TextBox>
                            <small class="form-text text-muted">11 d&#xED;gitos. Dejar en blanco si no aplica.</small>
                        </div>
                        <div class="form-group col-md-8">
                            <label class="font-weight-bold">Nombre / Raz&#xF3;n Social <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtEditarNombre" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="font-weight-bold">Direcci&#xF3;n fiscal:</label>
                        <asp:TextBox ID="txtEditarDireccion" runat="server" CssClass="form-control" MaxLength="250"></asp:TextBox>
                    </div>
                    <div class="form-row">
                        <div class="form-group col-md-5">
                            <label class="font-weight-bold">Persona de contacto:</label>
                            <asp:TextBox ID="txtEditarContacto" runat="server" CssClass="form-control" MaxLength="150"></asp:TextBox>
                        </div>
                        <div class="form-group col-md-3">
                            <label class="font-weight-bold">Tel&#xE9;fono:</label>
                            <asp:TextBox ID="txtEditarTelefono" runat="server" CssClass="form-control" MaxLength="50"></asp:TextBox>
                        </div>
                        <div class="form-group col-md-4">
                            <label class="font-weight-bold">Correo:</label>
                            <asp:TextBox ID="txtEditarCorreo" runat="server" CssClass="form-control" MaxLength="150" TextMode="Email"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-row align-items-center">
                        <div class="form-group col-md-4">
                            <label class="font-weight-bold">Moneda de facturaci&#xF3;n:</label>
                            <asp:DropDownList ID="ddlEditarMoneda" runat="server" CssClass="form-control">
                                <asp:ListItem Value="PEN" Text="PEN - Soles"></asp:ListItem>
                                <asp:ListItem Value="USD" Text="USD - D&#xF3;lares"></asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="form-group col-md-8 mb-0 pt-3">
                            <asp:CheckBox ID="chkEditarEsExportador" runat="server" CssClass="mr-2" Text="Opera carga internacional" />
                            <small class="form-text text-muted">Habilita el seguimiento de exportaci&#xF3;n para este cliente.</small>
                        </div>
                    </div>
                    <div class="form-group mb-0">
                        <label class="font-weight-bold">Observaciones:</label>
                        <asp:TextBox ID="txtEditarObservaciones" runat="server" CssClass="form-control" MaxLength="500"
                            TextMode="MultiLine" Rows="2"></asp:TextBox>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarCliente" runat="server" CssClass="btn btn-info"
                        Text="Guardar Cambios" OnClick="btnActualizarCliente_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdCliente: '<%= hfIdCliente.ClientID %>',
            txtEditarRUC: '<%= txtEditarRUC.ClientID %>',
            txtEditarNombre: '<%= txtEditarNombre.ClientID %>',
            txtEditarDireccion: '<%= txtEditarDireccion.ClientID %>',
            txtEditarContacto: '<%= txtEditarContacto.ClientID %>',
            txtEditarTelefono: '<%= txtEditarTelefono.ClientID %>',
            txtEditarCorreo: '<%= txtEditarCorreo.ClientID %>',
            txtEditarObservaciones: '<%= txtEditarObservaciones.ClientID %>',
            ddlEditarMoneda: '<%= ddlEditarMoneda.ClientID %>',
            chkEditarEsExportador: '<%= chkEditarEsExportador.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            txtRUC: '<%= txtRUC.ClientID %>',
            txtNombre: '<%= txtNombre.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroClientes.js") %>"></script>
</asp:Content>
