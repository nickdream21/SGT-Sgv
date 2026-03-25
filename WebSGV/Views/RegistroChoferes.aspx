<%@ Page Title="Registro de Choferes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroChoferes.aspx.cs" Inherits="WebSGV.Views.RegistroChoferes" %>

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

            <!-- Panel Agregar -->
            <div class="col-12 col-md-5 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0"><i class="fas fa-plus-circle mr-2"></i>Agregar Conductor</h5>
                    </div>
                    <div class="card-body">
                        <asp:Panel ID="pnlFormulario" runat="server">
                            <!-- Primera fila -->
                            <div class="row">
                                <div class="col-md-4 form-group">
                                    <label class="font-weight-bold">Tipo Documento:</label>
                                    <asp:DropDownList ID="ddlTipoDocumento" runat="server" CssClass="form-control" onchange="mostrarCampoDocumento()">
                                        <asp:ListItem>DNI</asp:ListItem>
                                        <asp:ListItem>Carnet de Extranjería</asp:ListItem>
                                        <asp:ListItem>Pasaporte</asp:ListItem>
                                    </asp:DropDownList>
                                </div>
                                <div class="col-md-4 form-group" id="grupoDNI">
                                    <label class="font-weight-bold">DNI:</label>
                                    <asp:TextBox ID="txtDNI" runat="server" CssClass="form-control" MaxLength="8"></asp:TextBox>
                                </div>
                                <div class="col-md-4 form-group" id="grupoPasaporte" style="display:none;">
                                    <label class="font-weight-bold">Pasaporte:</label>
                                    <asp:TextBox ID="txtPasaporte" runat="server" CssClass="form-control"></asp:TextBox>
                                </div>
                                <div class="col-md-4 form-group d-flex align-items-end">
                                    <asp:Button ID="btnBuscarDNI" runat="server" CssClass="btn btn-secondary w-100" Text="Buscar DNI" OnClientClick="buscarPorDNI(); return false;" />
                                </div>
                            </div>

                            <!-- Segunda fila -->
                            <div class="row">
                                <div class="col-md-6 form-group" id="grupoCarnet">
                                    <label class="font-weight-bold">Carnet Extranjer&#xED;a:</label>
                                    <asp:TextBox ID="txtCarnetExtranjeria" runat="server" CssClass="form-control" MaxLength="11"></asp:TextBox>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Nombres: <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtNombres" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RequiredFieldValidator ID="rfvNombres" runat="server" ControlToValidate="txtNombres"
                                        ErrorMessage="El nombre es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                                </div>
                            </div>

                            <!-- Tercera fila -->
                            <div class="row">
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Apellido Paterno: <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtApellidoPaterno" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RequiredFieldValidator ID="rfvApellidoPaterno" runat="server" ControlToValidate="txtApellidoPaterno"
                                        ErrorMessage="El apellido paterno es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                                </div>
                                <div class="col-md-6 form-group">
                                    <label class="font-weight-bold">Apellido Materno: <span class="text-danger">*</span></label>
                                    <asp:TextBox ID="txtApellidoMaterno" runat="server" CssClass="form-control"></asp:TextBox>
                                    <asp:RequiredFieldValidator ID="rfvApellidoMaterno" runat="server" ControlToValidate="txtApellidoMaterno"
                                        ErrorMessage="El apellido materno es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                                </div>
                            </div>

                            <!-- Cuarta fila -->
                            <div class="row">
                                <div class="col-md-12 form-group">
                                    <label class="font-weight-bold">Tel&#xE9;fono:</label>
                                    <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control"></asp:TextBox>
                                </div>
                            </div>

                            <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-block" Text="Registrar Conductor" OnClick="btnRegistrar_Click" />
                        </asp:Panel>
                    </div>
                </div>
                <div class="alert alert-info mt-3">
                    <i class="fas fa-lightbulb mr-2"></i>
                    <strong>Informaci&#xF3;n:</strong> Los conductores <strong>activos</strong> aparecer&#xE1;n en los formularios de operaci&#xF3;n.
                    Los inactivos quedan ocultos pero sus datos hist&#xF3;ricos se conservan.
                </div>
            </div>

            <!-- Lista de Conductores -->
            <div class="col-12 col-md-7 mb-4">
                <div class="card shadow-sm">
                    <div class="card-header bg-light">
                        <h5 class="mb-0"><i class="fas fa-list mr-2"></i>Conductores Registrados</h5>
                    </div>
                    <div class="card-body p-0">
                        <div class="table-responsive">
                            <asp:GridView ID="gvConductores" runat="server"
                                CssClass="table table-hover mb-0"
                                AutoGenerateColumns="false"
                                EmptyDataText="No hay conductores registrados."
                                OnRowCommand="gvConductores_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="DNI" HeaderText="DOCUMENTO" />
                                    <asp:BoundField DataField="nombreCompleto" HeaderText="NOMBRE COMPLETO" />
                                    <asp:BoundField DataField="telefono" HeaderText="TEL&#xC9;FONO" ItemStyle-CssClass="text-center" />
                                    <asp:TemplateField HeaderText="ESTADO" ItemStyle-CssClass="text-center" ItemStyle-Width="90">
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

    <!-- Script para consultar por DNI -->
    <script>
        async function buscarPorDNI() {
            const dni = document.getElementById('<%= txtDNI.ClientID %>').value.trim();
            const token = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJlbWFpbCI6Im1vcmFucGFsYWNpb3NhbGVtYmVydEBnbWFpbC5jb20ifQ.-nOvFy3s-JXWGF6IoEeJU1NtSrGXhM6sL3msay8eKRI';

            if (dni.length !== 8) {
                alert("El DNI debe tener 8 dígitos.");
                return;
            }

            try {
                const response = await fetch(`https://dniruc.apisperu.com/api/v1/dni/${dni}?token=${token}`);

                if (!response.ok) {
                    throw new Error("Error en la solicitud");
                }

                const data = await response.json();

                if (data.nombres) {
                    document.getElementById('<%= txtNombres.ClientID %>').value = data.nombres;
                    document.getElementById('<%= txtApellidoPaterno.ClientID %>').value = data.apellidoPaterno;
                    document.getElementById('<%= txtApellidoMaterno.ClientID %>').value = data.apellidoMaterno;
                } else {
                    alert("No se encontró información para el DNI ingresado. Por favor, ingréselo manualmente.");
                }
            } catch (error) {
                console.error("Error al consultar el DNI:", error);
                alert("Ocurrió un error al consultar el DNI.");
            }
        }
    </script>

    <script>
        function mostrarCampoDocumento() {
            const tipo = document.getElementById('<%= ddlTipoDocumento.ClientID %>').value;

            document.getElementById('grupoDNI').style.display = 'none';
            document.getElementById('grupoCarnet').style.display = 'none';
            document.getElementById('grupoPasaporte').style.display = 'none';
            document.getElementById('<%= btnBuscarDNI.ClientID %>').style.display = 'none';

            if (tipo === "DNI") {
                document.getElementById('grupoDNI').style.display = 'block';
                document.getElementById('<%= btnBuscarDNI.ClientID %>').style.display = 'block';
            } else if (tipo === "Carnet de Extranjería") {
                document.getElementById('grupoCarnet').style.display = 'block';
            } else if (tipo === "Pasaporte") {
                document.getElementById('grupoPasaporte').style.display = 'block';
            }
        }

        document.addEventListener("DOMContentLoaded", function () {
            mostrarCampoDocumento();
        });
    </script>
</asp:Content>