<%@ Page Title="Registro de Choferes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroChoferes.aspx.cs" Inherits="WebSGV.Views.RegistroChoferes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:HiddenField ID="hfIdConductor" runat="server" />

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-user-tie mr-2" style="color:#2563eb;"></i>Registro de Conductores
                        </h2>
                        <p class="text-muted mb-0">Administra los conductores disponibles para las operaciones</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalConductores" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <div class="col-12 col-md-5 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Conductor</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <asp:ValidationSummary ID="vsRegistroConductor" runat="server" ValidationGroup="vgRegistroConductor" CssClass="text-danger small mb-3" DisplayMode="BulletList" />

                            <%-- Fila 1: Tipo documento + campo de documento (en la misma posición) --%>
                            <div class="row align-items-end">
                                <div class="col-md-4 form-group">
                                    <label class="font-weight-bold">Tipo Documento:</label>
                                    <asp:DropDownList ID="ddlTipoDocumento" runat="server" CssClass="form-control" onchange="mostrarCampoDocumento()">
                                        <asp:ListItem>DNI</asp:ListItem>
                                        <asp:ListItem>Carnet de Extranjería</asp:ListItem>
                                        <asp:ListItem>Pasaporte</asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                                <div class="col-md-8 form-group" id="grupoDNI">
                                    <label class="font-weight-bold">DNI:</label>
                                    <div class="input-group">
                                        <asp:TextBox ID="txtDNI" runat="server" CssClass="form-control" MaxLength="8" placeholder="8 dígitos"></asp:TextBox>
                                        <div class="input-group-append">
                                            <asp:Button ID="btnBuscarDNI" runat="server" CssClass="btn btn-secondary" Text="Buscar" OnClientClick="buscarPorDNI(); return false;" />
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-8 form-group" id="grupoCarnet" style="display:none;">
                                    <label class="font-weight-bold">Carnet Extranjer&#xED;a:</label>
                                    <asp:TextBox ID="txtCarnetExtranjeria" runat="server" CssClass="form-control" MaxLength="11" placeholder="Núm. carnet"></asp:TextBox>
                                </div>
                                <div class="col-md-8 form-group" id="grupoPasaporte" style="display:none;">
                                    <label class="font-weight-bold">Pasaporte:</label>
                                    <asp:TextBox ID="txtPasaporte" runat="server" CssClass="form-control" placeholder="Núm. pasaporte"></asp:TextBox>
                                </div>
                                <asp:RegularExpressionValidator ID="revDni" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtDNI" ValidationExpression="^$|^\d{8}$" ErrorMessage="DNI: debe tener exactamente 8 dígitos." CssClass="text-danger small" Display="Dynamic" />
                                <asp:RegularExpressionValidator ID="revCarnet" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtCarnetExtranjeria" ValidationExpression="^$|^[A-Za-z0-9\-]{6,12}$" ErrorMessage="Carnet de extranjería: use de 6 a 12 caracteres alfanuméricos." CssClass="text-danger small" Display="Dynamic" />
                                <asp:RegularExpressionValidator ID="revPasaporte" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtPasaporte" ValidationExpression="^$|^[A-Za-z0-9\-]{6,12}$" ErrorMessage="Pasaporte: use de 6 a 12 caracteres alfanuméricos." CssClass="text-danger small" Display="Dynamic" />
                            </div>

                            <%-- Fila 2: Nombres + Apellido Paterno --%>
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Nombres: <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtNombres" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RequiredFieldValidator ID="rfvNombres" runat="server" ControlToValidate="txtNombres" ValidationGroup="vgRegistroConductor"
                                        ErrorMessage="El nombre es requerido" CssClass="text-danger small" Display="Dynamic"></asp:RequiredFieldValidator>
                                    <asp:RegularExpressionValidator ID="revNombres" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtNombres" ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú\s]{2,100}$" ErrorMessage="Nombres: solo letras y espacios (2 a 100 caracteres)." CssClass="text-danger small" Display="Dynamic" />
                                </div>
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Apellido Paterno: <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtApellidoPaterno" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RequiredFieldValidator ID="rfvApellidoPaterno" runat="server" ControlToValidate="txtApellidoPaterno" ValidationGroup="vgRegistroConductor"
                                        ErrorMessage="El apellido paterno es requerido" CssClass="text-danger small" Display="Dynamic"></asp:RequiredFieldValidator>
                                    <asp:RegularExpressionValidator ID="revApellidoPaterno" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtApellidoPaterno" ValidationExpression="^[A-Za-zÁÉÍÓÚÑáéíóú\s]{2,100}$" ErrorMessage="Apellido paterno: solo letras y espacios (2 a 100 caracteres)." CssClass="text-danger small" Display="Dynamic" />
                                </div>
                            </div>

                            <%-- Fila 3: Apellido Materno + Teléfono --%>
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Apellido Materno:</label>
                                    <asp:TextBox ID="txtApellidoMaterno" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RegularExpressionValidator ID="revApellidoMaterno" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtApellidoMaterno" ValidationExpression="^$|^[A-Za-zÁÉÍÓÚÑáéíóú\s]{2,100}$" ErrorMessage="Apellido materno: solo letras y espacios (2 a 100 caracteres)." CssClass="text-danger small" Display="Dynamic" />
                                </div>
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Tel&#xE9;fono:</label>
                                    <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RegularExpressionValidator ID="revTelefono" runat="server" ValidationGroup="vgRegistroConductor" ControlToValidate="txtTelefono" ValidationExpression="^$|^\d{7,9}$" ErrorMessage="Teléfono: ingrese entre 7 y 9 dígitos numéricos." CssClass="text-danger small" Display="Dynamic" />
                                </div>
                            </div>

                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-block" Text="Registrar Conductor" ValidationGroup="vgRegistroConductor" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los conductores <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <div class="col-12 col-md-7 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light d-flex justify-content-between align-items-center">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Conductores Registrados</h5>
                        <input type="text" id="txtBuscar" class="form-control form-control-sm ml-3"
                            style="max-width:200px;" placeholder="Buscar..." autocomplete="off" spellcheck="false" autocapitalize="off" autocorrect="off" name="filtro_conductores"
                            oninput="filtrarTabla(this.value,'contenedorConductores')">
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive" id="contenedorConductores">
                            <asp:GridView ID="gvConductores" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay conductores registrados."
                                OnRowCommand="gvConductores_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="documentoDisplay" HeaderText="DOCUMENTO" />
                                    <asp:BoundField DataField="nombreCompleto" HeaderText="NOMBRE COMPLETO" />
                                    <asp:BoundField DataField="telefono" HeaderText="TEL&#xC9;FONO" ItemStyle-CssClass="text-center" />
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
                                                data-id='<%# Eval("idConductor") %>'
                                                data-nombres='<%# AttrEncode(Eval("nombre")) %>'
                                                data-ap-paterno='<%# AttrEncode(Eval("apPaterno")) %>'
                                                data-ap-materno='<%# AttrEncode(Eval("apMaterno")) %>'
                                                data-telefono='<%# AttrEncode(Eval("telefono")) %>'>
                                                <i class="fas fa-edit"></i>
                                            </button>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCION" ItemStyle-CssClass="text-center" ItemStyle-Width="110">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idConductor") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este conductor?');">
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

    <!-- Modal Editar Conductor -->
    <div class="modal fade" id="modalEditar" tabindex="-1" role="dialog">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header" style="background:#0ea5e9;color:#fff;">
                    <h5 class="modal-title"><i class="fas fa-user-edit mr-2"></i>Editar Conductor</h5>
                    <button type="button" class="close" style="color:#fff;" data-dismiss="modal">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="row">
                        <div class="col-md-6 form-group">
                            <label class="font-weight-bold">Nombres <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtEditarNombres" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                        <div class="col-md-6 form-group">
                            <label class="font-weight-bold">Apellido Paterno <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtEditarApellidoPaterno" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                    </div>
                    <div class="row">
                        <div class="col-md-6 form-group">
                            <label class="font-weight-bold">Apellido Materno</label>
                            <asp:TextBox ID="txtEditarApellidoMaterno" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                        <div class="col-md-6 form-group mb-0">
                            <label class="font-weight-bold">Tel&#xE9;fono</label>
                            <asp:TextBox ID="txtEditarTelefono" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" data-dismiss="modal">Cancelar</button>
                    <asp:Button ID="btnActualizarConductor" runat="server" CssClass="btn btn-info"
                        Text="Guardar Cambios" OnClick="btnActualizarConductor_Click" />
                </div>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            hfIdConductor: '<%= hfIdConductor.ClientID %>',
            txtEditarNombres: '<%= txtEditarNombres.ClientID %>',
            txtEditarApellidoPaterno: '<%= txtEditarApellidoPaterno.ClientID %>',
            txtEditarApellidoMaterno: '<%= txtEditarApellidoMaterno.ClientID %>',
            txtEditarTelefono: '<%= txtEditarTelefono.ClientID %>',
            pnlMensaje: '<%= pnlMensaje.ClientID %>',
            txtDNI: '<%= txtDNI.ClientID %>',
            txtNombres: '<%= txtNombres.ClientID %>',
            txtApellidoPaterno: '<%= txtApellidoPaterno.ClientID %>',
            txtApellidoMaterno: '<%= txtApellidoMaterno.ClientID %>',
            ddlTipoDocumento: '<%= ddlTipoDocumento.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/RegistroChoferes.js") %>"></script>
</asp:Content>
