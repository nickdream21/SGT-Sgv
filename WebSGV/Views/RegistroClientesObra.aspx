<%@ Page Title="Registro de Clientes de Obra" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroClientesObra.aspx.cs" Inherits="WebSGV.Views.RegistroClientesObra" %>

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
                            <i class="fas fa-building mr-2" style="color:#2d6a4f;"></i>Registro de Clientes de Obra
                        </h2>
                        <p class="text-muted mb-0">Administra los clientes a quienes se presta servicio de maquinaria</p>
                    </div>
                    <span class="badge px-3 py-2" style="font-size:0.9rem;background-color:#2d6a4f;color:white;">
                        <asp:Label ID="lblTotalClientes" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Agregar -->
            <div class="col-12 col-md-5 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header text-white" style="background-color:#2d6a4f;">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Cliente</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <div class="form-group">
                                <label class="font-weight-bold">Nombre / Raz&#xF3;n Social <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control" MaxLength="200" placeholder="Nombre o raz&#xF3;n social"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ControlToValidate="txtNombre"
                                    ErrorMessage="El nombre es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">RUC</label>
                                <asp:TextBox ID="txtRuc" runat="server" CssClass="form-control" MaxLength="20" placeholder="Ej: 20123456789"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Contacto</label>
                                <asp:TextBox ID="txtContacto" runat="server" CssClass="form-control" MaxLength="200" placeholder="Persona de contacto"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Tel&#xE9;fono</label>
                                <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control" MaxLength="20" placeholder="Ej: 987654321"></asp:TextBox>
                            </div>
                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-block text-white"
                                style="background-color:#2d6a4f;" Text="Registrar Cliente" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
            </div>

            <!-- Lista de Clientes -->
            <div class="col-12 col-md-7 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Clientes Registrados</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvClientes" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay clientes registrados."
                                OnRowCommand="gvClientes_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE" />
                                    <asp:BoundField DataField="ruc" HeaderText="RUC" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="contacto" HeaderText="CONTACTO" />
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
                                                CommandArgument='<%# Eval("idClienteObra") %>'
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

</asp:Content>
