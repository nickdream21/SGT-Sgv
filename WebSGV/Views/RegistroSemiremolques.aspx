<%@ Page Title="Registro de Semiremolques" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroSemiremolques.aspx.cs" Inherits="WebSGV.Views.RegistroSemiremolques" %>
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
                            <i class="fas fa-trailer mr-2" style="color:#2563eb;"></i>Registro de Semiremolques
                        </h2>
                        <p class="text-muted mb-0">Administra los semiremolques disponibles para las operaciones</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalSemiremolques" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Agregar -->
            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Semiremolque</h5>
                    </div>
                    <div class="card-body">
                        <div class="form-group">
                            <label class="font-weight-bold">N&#xBA; de Placa <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtPlaca" runat="server" CssClass="form-control"
                                placeholder="Ej: ABC-123" MaxLength="20"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvPlaca" runat="server" ControlToValidate="txtPlaca"
                                ErrorMessage="La placa es requerida" CssClass="text-danger" Display="Dynamic" />
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold">Marca <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtMarca" runat="server" CssClass="form-control"
                                placeholder="Ej: RANDON" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvMarca" runat="server" ControlToValidate="txtMarca"
                                ErrorMessage="La marca es requerida" CssClass="text-danger" Display="Dynamic" />
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold">Modelo <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtModelo" runat="server" CssClass="form-control"
                                placeholder="Ej: SR GR 3E" MaxLength="100"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvModelo" runat="server" ControlToValidate="txtModelo"
                                ErrorMessage="El modelo es requerido" CssClass="text-danger" Display="Dynamic" />
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar Semiremolque"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los semiremolques <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <!-- Lista de Semiremolques -->
            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Semiremolques Registrados</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvSemiremolques" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay semiremolques registrados."
                                OnRowCommand="gvSemiremolques_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idCarreta" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="60" />
                                    <asp:BoundField DataField="placaCarreta" HeaderText="PLACA" ItemStyle-CssClass="font-weight-bold" />
                                    <asp:BoundField DataField="marca" HeaderText="MARCA" />
                                    <asp:BoundField DataField="modelo" HeaderText="MODELO" />
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="100">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseEstado(Eval("activo")) %>'>
                                                <%# ObtenerTextoEstado(Eval("activo")) %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCION" ItemStyle-CssClass="text-center" ItemStyle-Width="110">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbToggle" runat="server"
                                                CommandName="ToggleActivo"
                                                CommandArgument='<%# Eval("idCarreta") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este semiremolque?');">
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
