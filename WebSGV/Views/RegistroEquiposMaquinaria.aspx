<%@ Page Title="Registro de Equipos de Maquinaria" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroEquiposMaquinaria.aspx.cs" Inherits="WebSGV.Views.RegistroEquiposMaquinaria" %>

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
                            <i class="fas fa-cogs mr-2" style="color:#2d6a4f;"></i>Registro de Equipos / Maquinaria
                        </h2>
                        <p class="text-muted mb-0">Administra los equipos de maquinaria pesada (placas)</p>
                    </div>
                    <span class="badge px-3 py-2" style="font-size:0.9rem;background-color:#2d6a4f;color:white;">
                        <asp:Label ID="lblTotalEquipos" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Agregar -->
            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header text-white" style="background-color:#2d6a4f;">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Equipo</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <div class="form-group">
                                <label class="font-weight-bold">Placa <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtPlaca" runat="server" CssClass="form-control" MaxLength="20" placeholder="Ej: EQP-001"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvPlaca" runat="server" ControlToValidate="txtPlaca"
                                    ErrorMessage="La placa es requerida" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Tipo <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlTipo" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="" Text="-- Seleccione --"></asp:ListItem>
                                    <asp:ListItem Value="Excavadora" Text="Excavadora"></asp:ListItem>
                                    <asp:ListItem Value="Retroexcavadora" Text="Retroexcavadora"></asp:ListItem>
                                    <asp:ListItem Value="Cargador Frontal" Text="Cargador Frontal"></asp:ListItem>
                                    <asp:ListItem Value="Volquete" Text="Volquete"></asp:ListItem>
                                    <asp:ListItem Value="Rodillo" Text="Rodillo"></asp:ListItem>
                                    <asp:ListItem Value="Motoniveladora" Text="Motoniveladora"></asp:ListItem>
                                    <asp:ListItem Value="Tractor" Text="Tractor"></asp:ListItem>
                                    <asp:ListItem Value="Otro" Text="Otro"></asp:ListItem>
                                </asp:DropDownList>
                                <asp:RequiredFieldValidator ID="rfvTipo" runat="server" ControlToValidate="ddlTipo"
                                    InitialValue="" ErrorMessage="Seleccione un tipo" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Descripci&#xF3;n</label>
                                <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" MaxLength="200" placeholder="Descripci&#xF3;n breve"></asp:TextBox>
                            </div>
                            <div class="row">
                                <div class="col-6">
                                    <div class="form-group">
                                        <label class="font-weight-bold">Marca</label>
                                        <asp:TextBox ID="txtMarca" runat="server" CssClass="form-control" MaxLength="100" placeholder="Ej: CAT"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-6">
                                    <div class="form-group">
                                        <label class="font-weight-bold">Modelo</label>
                                        <asp:TextBox ID="txtModelo" runat="server" CssClass="form-control" MaxLength="100" placeholder="Ej: 320D"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">A&#xF1;o</label>
                                <asp:TextBox ID="txtAnio" runat="server" CssClass="form-control" MaxLength="4" TextMode="Number" placeholder="Ej: 2020"></asp:TextBox>
                            </div>
                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-block text-white"
                                style="background-color:#2d6a4f;" Text="Registrar Equipo" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
            </div>

            <!-- Lista de Equipos -->
            <div class="col-12 col-md-8 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Equipos Registrados</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvEquipos" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay equipos registrados."
                                OnRowCommand="gvEquipos_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="placa" HeaderText="PLACA" />
                                    <asp:BoundField DataField="tipo" HeaderText="TIPO" />
                                    <asp:BoundField DataField="descripcion" HeaderText="DESCRIPCI&#xD3;N" />
                                    <asp:BoundField DataField="marca" HeaderText="MARCA" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="modelo" HeaderText="MODELO" ItemStyle-CssClass="text-center" />
                                    <asp:BoundField DataField="anio" HeaderText="A&#xD1;O" ItemStyle-CssClass="text-center" />
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
                                                CommandArgument='<%# Eval("idEquipo") %>'
                                                CssClass='<%# ObtenerClaseBoton(Eval("activo")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de este equipo?');">
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
