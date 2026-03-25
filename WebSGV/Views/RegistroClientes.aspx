<%@ Page Title="Registro de Clientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroClientes.aspx.cs" Inherits="WebSGV.Views.RegistroClientes" %>

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

            <!-- Panel Agregar -->
            <div class="col-12 col-md-4 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Cliente</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <div class="form-group">
                                <label class="font-weight-bold">RUC (Opcional):</label>
                                <div class="input-group">
                                    <asp:TextBox ID="txtRUC" runat="server" CssClass="form-control" MaxLength="11"></asp:TextBox>
                                    <div class="input-group-append">
                                        <asp:Button ID="btnBuscarRUC" runat="server" CssClass="btn btn-secondary" Text="Verificar" OnClientClick="verificarRUC(); return false;" />
                                    </div>
                                </div>
                                <small class="form-text text-muted">Ingrese el RUC si lo conoce, o deje en blanco.</small>
                            </div>
                            <div class="form-group">
                                <label class="font-weight-bold">Nombre / Raz&#xF3;n Social: <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control"></asp:TextBox>
                                <asp:RequiredFieldValidator ID="rfvNombre" runat="server"
                                    ControlToValidate="txtNombre"
                                    ErrorMessage="El nombre del cliente es obligatorio."
                                    CssClass="text-danger"
                                    Display="Dynamic">
                                </asp:RequiredFieldValidator>
                            </div>
                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-block"
                                Text="Registrar Cliente" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los clientes <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <!-- Lista de Clientes -->
            <div class="col-12 col-md-8 mb-4">
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
                                    <asp:BoundField DataField="idCliente" HeaderText="ID"
                                        ItemStyle-CssClass="text-center text-muted" ItemStyle-Width="60" />
                                    <asp:BoundField DataField="ruc" HeaderText="RUC" />
                                    <asp:BoundField DataField="nombre" HeaderText="NOMBRE / RAZ&#xD3;N SOCIAL" />
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

    <!-- Script para verificar RUC -->
    <script>
        async function verificarRUC() {
            const ruc = document.getElementById('<%= txtRUC.ClientID %>').value.trim();
            const token = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJlbWFpbCI6Im1vcmFucGFsYWNpb3NhbGVtYmVydEBnbWFpbC5jb20ifQ.-nOvFy3s-JXWGF6IoEeJU1NtSrGXhM6sL3msay8eKRI';

            if (ruc.length === 0) {
                alert("Debe ingresar un RUC para verificar.");
                return;
            }

            if (ruc.length !== 11) {
                alert("El RUC debe tener 11 dígitos.");
                return;
            }

            try {
                const response = await fetch(`https://dniruc.apisperu.com/api/v1/ruc/${ruc}?token=${token}`);

                if (!response.ok) {
                    throw new Error("Error en la solicitud");
                }

                const data = await response.json();

                if (data.razonSocial) {
                    document.getElementById('<%= txtNombre.ClientID %>').value = data.razonSocial;
                } else {
                    alert("No se encontró información para el RUC ingresado. Por favor, ingrese el nombre manualmente.");
                }
            } catch (error) {
                console.error("Error al consultar el RUC:", error);
                alert("Ocurrió un error al consultar el RUC.");
            }
        }
    </script>
</asp:Content>
