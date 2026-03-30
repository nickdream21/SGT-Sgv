<%@ Page Title="Registro de Obras" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroObras.aspx.cs" Inherits="WebSGV.Views.RegistroObras" %>

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
                            <i class="fas fa-hard-hat mr-2" style="color:#2d6a4f;"></i>Registro de Obras
                        </h2>
                        <p class="text-muted mb-0">Administra las obras donde operan los equipos de maquinaria</p>
                    </div>
                    <span class="badge px-3 py-2" style="font-size:0.9rem;background-color:#2d6a4f;color:white;">
                        <asp:Label ID="lblTotalObras" runat="server" Text="0 registro(s)"></asp:Label>
                    </span>
                </div>
            </div>
        </div>

        <div class="row">

            <!-- Panel Agregar -->
            <div class="col-12 col-md-5 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header text-white" style="background-color:#2d6a4f;">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Obra</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <div class="form-group">
                                <label class="font-weight-bold">Nombre de la Obra <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control" MaxLength="300" placeholder="Nombre del proyecto u obra"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ControlToValidate="txtNombre"
                                    ErrorMessage="El nombre es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Cliente <span class="text-danger">*</span></label>
                                <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control">
                                </asp:DropDownList>
                                <asp:RequiredFieldValidator ID="rfvCliente" runat="server" ControlToValidate="ddlCliente"
                                    InitialValue="" ErrorMessage="Seleccione un cliente" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Ubicaci&#xF3;n</label>
                                <asp:TextBox ID="txtUbicacion" runat="server" CssClass="form-control" MaxLength="300" placeholder="Direcci&#xF3;n o referencia"></asp:TextBox>
                            </div>
                            <div class="row">
                                <div class="col-6">
                                    <div class="form-group">
                                        <label class="font-weight-bold">Fecha Inicio</label>
                                        <asp:TextBox ID="txtFechaInicio" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                    </div>
                                </div>
                                <div class="col-6">
                                    <div class="form-group">
                                        <label class="font-weight-bold">Fecha Fin</label>
                                        <asp:TextBox ID="txtFechaFin" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-block text-white"
                                style="background-color:#2d6a4f;" Text="Registrar Obra" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
            </div>

            <!-- Lista de Obras -->
            <div class="col-12 col-md-7 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Obras Registradas</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvObras" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay obras registradas."
                                OnRowCommand="gvObras_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="nombre" HeaderText="OBRA" />
                                    <asp:BoundField DataField="clienteNombre" HeaderText="CLIENTE" />
                                    <asp:BoundField DataField="ubicacion" HeaderText="UBICACI&#xD3;N" />
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="110">
                                        <ItemTemplate>
                                            <span class='badge <%# ObtenerClaseEstadoObra(Eval("estado")) %>'>
                                                <%# Eval("estado") %>
                                            </span>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                    <asp:TemplateField HeaderText="ACCI&#xD3;N" ItemStyle-CssClass="text-center" ItemStyle-Width="130">
                                        <ItemTemplate>
                                            <asp:LinkButton ID="lbFinalizar" runat="server"
                                                CommandName="CambiarEstado"
                                                CommandArgument='<%# Eval("idObra") %>'
                                                CssClass='<%# ObtenerClaseBotonObra(Eval("estado")) %>'
                                                OnClientClick="return confirm('&#xBF;Cambiar el estado de esta obra?');"
                                                Visible='<%# Eval("estado").ToString() != "FINALIZADA" %>'>
                                                <%# ObtenerTextoBotonObra(Eval("estado")) %>
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
