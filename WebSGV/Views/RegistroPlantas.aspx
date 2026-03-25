<%@ Page Title="Registro de Plantas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroPlantas.aspx.cs" Inherits="WebSGV.Views.RegistroPlantas" %>
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
                            <i class="fas fa-industry mr-2" style="color:#2563eb;"></i>Registro de Plantas
                        </h2>
                        <p class="text-muted mb-0">Administra las plantas de operaci&#xF3;n disponibles para los despachos</p>
                    </div>
                    <span class="badge badge-primary px-3 py-2" style="font-size:0.9rem;">
                        <asp:Label ID="lblTotalPlantas" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Agregar -->
            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Planta</h5>
                    </div>
                    <div class="card-body">
                        <div class="form-group">
                            <label class="font-weight-bold">Nombre de la Planta <span class="text-danger">*</span></label>
                            <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"
                                placeholder="Ej: PLANTA TRUJILLO" MaxLength="200"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvNombre" runat="server"
                                ControlToValidate="txtNombre"
                                ErrorMessage="El nombre de la planta es requerido."
                                CssClass="text-danger small" Display="Dynamic" />
                            <small class="form-text text-muted">
                                <i class="fas fa-info-circle mr-1"></i>El nombre se guardar&#xE1; en may&#xFA;sculas.
                            </small>
                        </div>
                        <div class="form-group">
                            <label class="font-weight-bold">&#xC1;mbito <span class="text-danger">*</span></label>
                            <asp:DropDownList ID="ddlAmbito" runat="server" CssClass="form-control">
                                <asp:ListItem Value="0" Text="Nacional (Per&#xFA;)"></asp:ListItem>
                                <asp:ListItem Value="1" Text="Internacional"></asp:ListItem>
                            </asp:DropDownList>
                            <small class="form-text text-muted">
                                Define si la planta se muestra en operaciones nacionales o internacionales.
                            </small>
                        </div>
                        <asp:Button ID="btnRegistrar" runat="server" Text="Registrar Planta"
                            CssClass="btn btn-primary btn-block" OnClick="btnRegistrar_Click" />
                    </div>
                </div>

                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Las plantas <strong>activas</strong> aparecer&#xE1;n en el selector de Planta de Operaci&#xF3;n al registrar despachos.
                    Las inactivas quedan ocultas pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <!-- Lista de Plantas -->
            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Plantas Registradas</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvPlantas" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay plantas registradas."
                                OnRowCommand="gvPlantas_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="idPlanta" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="55" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE DE LA PLANTA" />
                                    <asp:TemplateField HeaderText="&#xC1;MBITO" ItemStyle-CssClass="text-center" ItemStyle-Width="120">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseAmbito(Eval("esInternacional")) %>'>
                                                <%# ObtenerTextoAmbito(Eval("esInternacional")) %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
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
                                                CommandArgument='<%# Eval("idPlanta") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de esta planta?');">
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
