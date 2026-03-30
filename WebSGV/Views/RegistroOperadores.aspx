<%@ Page Title="Registro de Operadores" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroOperadores.aspx.cs" Inherits="WebSGV.Views.RegistroOperadores" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Panel ID="pnlMensaje" runat="server" Visible="false">
        <asp:Label ID="lblMensaje" runat="server"></asp:Label>
    </asp:Panel>

    <div class="container-fluid px-3">

        <!-- Encabezado -->
        <div class="row mb-4">
            <div class="col-12">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-3">
                    <div>
                        <h2 class="mb-1" style="color:#1e293b;font-weight:600;">
                            <i class="fas fa-hard-hat mr-2" style="color:#2d6a4f;"></i>Registro de Operadores
                        </h2>
                        <p class="text-muted mb-0">Administra los operadores de maquinaria pesada</p>
                    </div>
                    <span class="badge px-3 py-2" style="font-size:0.9rem;background-color:#2d6a4f;color:white;">
                        <asp:Label ID="lblTotalOperadores" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Agregar -->
            <div class="col-12 col-md-5 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header text-white" style="background-color:#2d6a4f;">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Operador</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <div class="form-group">
                                <label class="font-weight-bold">DNI <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtDNI" runat="server" CssClass="form-control" MaxLength="15" placeholder="Ej: 12345678"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvDNI" runat="server" ControlToValidate="txtDNI"
                                    ErrorMessage="El DNI es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Nombre Completo <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control" MaxLength="200" placeholder="Nombre completo del operador"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ControlToValidate="txtNombre"
                                    ErrorMessage="El nombre es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Tel&#xE9;fono</label>
                                <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control" MaxLength="20" placeholder="Ej: 987654321"></asp:TextBox>
                            </div>
                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-block text-white" 
                                style="background-color:#2d6a4f;" Text="Registrar Operador" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los operadores <strong>activos</strong> estar&#xE1;n disponibles para asignaciones de maquinaria.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <!-- Lista de Operadores -->
            <div class="col-12 col-md-7 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Operadores Registrados</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvOperadores" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay operadores registrados."
                                OnRowCommand="gvOperadores_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="dni" HeaderText="DNI" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE" />
                                    <asp:BoundField DataField="telefono" HeaderText="TEL&#xC9;FONO" ItemStyle-CssClass="text-center" />
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="90">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseEstado(Eval("activo")) %>'>
                                                <%# ObtenerTextoEstado(Eval("activo")) %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="110">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idOperador") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este operador?');">
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

</asp:Content>
