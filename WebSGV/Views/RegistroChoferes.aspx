<%@ Page Title="Registro de Choferes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroChoferes.aspx.cs" Inherits="WebSGV.Views.RegistroChoferes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex justify-content-center align-items-center vh-100">
        <div class="card registro-choferes-card">
            <div class="card-header registro-choferes-header text-center">
                <h2 class="registro-header-title">Registro de Choferes</h2>
            </div>
            <div class="card-body">
                <asp:Panel ID="pnlFormulario" runat="server">
                    <!-- Primera fila -->
                    <div class="row">
                        <div class="col-md-4 form-group">
                            <label for="ddlTipoDocumento" class="form-label">
                                Tipo Documento:
                            </label>
                            <asp:DropDownList ID="ddlTipoDocumento" runat="server" CssClass="form-control" onchange="mostrarCampoDocumento()">
                                <asp:ListItem>DNI</asp:ListItem>
                                <asp:ListItem>Carnet de Extranjería</asp:ListItem>
                                <asp:ListItem>Pasaporte</asp:ListItem>
                            </asp:DropDownList>
                        </div>
                        <div class="col-md-4 form-group" id="grupoDNI">
                            <label for="txtDNI" class="form-label">
                                DNI:
                            </label>
                            <asp:TextBox ID="txtDNI" runat="server" CssClass="form-control" MaxLength="8"></asp:TextBox>
                        </div>
                        <div class="col-md-4 form-group" id="grupoPasaporte" style="display:none;">
                            <label for="txtPasaporte" class="form-label">
                                Pasaporte:
                            </label>
                            <asp:TextBox ID="txtPasaporte" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                        <div class="col-md-4 form-group d-flex align-items-end">
                            <asp:Button ID="btnBuscarDNI" runat="server" CssClass="btn btn-secondary w-100" Text="Buscar DNI" OnClientClick="buscarPorDNI(); return false;" />
                        </div>
                    </div>

                    <!-- Segunda fila -->
                    <div class="row">
                        <div class="col-md-6 form-group" id="grupoCarnet">
                            <label for="txtCarnetExtranjeria" class="form-label">Carnet Extranjería:</label>
                            <asp:TextBox ID="txtCarnetExtranjeria" runat="server" CssClass="form-control" MaxLength="11"></asp:TextBox>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="txtNombres" class="form-label">Nombres:</label>
                            <asp:TextBox ID="txtNombres" runat="server" CssClass="form-control"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvNombres" runat="server" ControlToValidate="txtNombres" 
                                ErrorMessage="El nombre es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                        </div>
                    </div>

                    <!-- Tercera fila -->
                    <div class="row">
                        <div class="col-md-6 form-group">
                            <label for="txtApellidoPaterno" class="form-label">Apellido Paterno:</label>
                            <asp:TextBox ID="txtApellidoPaterno" runat="server" CssClass="form-control"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvApellidoPaterno" runat="server" ControlToValidate="txtApellidoPaterno" 
                                ErrorMessage="El apellido paterno es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="txtApellidoMaterno" class="form-label">Apellido Materno:</label>
                            <asp:TextBox ID="txtApellidoMaterno" runat="server" CssClass="form-control"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvApellidoMaterno" runat="server" ControlToValidate="txtApellidoMaterno" 
                                ErrorMessage="El apellido materno es requerido" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                        </div>
                    </div>

                    <!-- Cuarta fila -->
                    <div class="row">
                        <div class="col-md-6 form-group">
                            <label for="txtTelefono" class="form-label">Teléfono:</label>
                            <asp:TextBox ID="txtTelefono" runat="server" CssClass="form-control"></asp:TextBox>
                        </div>
                        <div class="col-md-6 form-group">
                            <label for="txtFechaNacimiento" class="form-label">Fecha de Nacimiento:</label>
                            <asp:TextBox ID="txtFechaNacimiento" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            <asp:RequiredFieldValidator ID="rfvFechaNacimiento" runat="server" ControlToValidate="txtFechaNacimiento" 
                                ErrorMessage="La fecha de nacimiento es requerida" CssClass="text-danger" Display="Dynamic"></asp:RequiredFieldValidator>
                        </div>
                    </div>

                    <!-- Dirección y correo -->
                    <div class="form-group">
                        <label for="txtDireccion" class="form-label">Dirección:</label>
                        <asp:TextBox ID="txtDireccion" runat="server" CssClass="form-control"></asp:TextBox>
                    </div>

                    <div class="form-group">
                        <label for="txtCorreo" class="form-label">Correo:</label>
                        <asp:TextBox ID="txtCorreo" runat="server" CssClass="form-control"></asp:TextBox>
                        <asp:RegularExpressionValidator ID="revCorreo" runat="server" ControlToValidate="txtCorreo"
                            ValidationExpression="^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"
                            ErrorMessage="Formato de correo inválido" CssClass="text-danger" Display="Dynamic"></asp:RegularExpressionValidator>
                    </div>

                    <!-- Mensaje de resultado -->
                    <div class="alert alert-success" id="mensajeExito" runat="server" visible="false">
                        Conductor registrado exitosamente
                    </div>
                    <div class="alert alert-danger" id="mensajeError" runat="server" visible="false">
                        <asp:Literal ID="litError" runat="server"></asp:Literal>
                    </div>

                    <!-- Botón -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-lg" Text="Registrar" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>

    <!-- Script para consultar por DNI -->
    <script>
        async function buscarPorDNI() {
            const dni = document.getElementById('<%= txtDNI.ClientID %>').value.trim();
            const token = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJlbWFpbCI6Im1vcmFucGFsYWNpb3NhbGVtYmVydEBnbWFpbC5jb20ifQ.-nOvFy3s-JXWGF6IoEeJU1NtSrGXhM6sL3msay8eKRI'; // Pega aquí tu token

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
                    // Si encontramos los datos, los mostramos
                    document.getElementById('<%= txtNombres.ClientID %>').value = data.nombres;
                    document.getElementById('<%= txtApellidoPaterno.ClientID %>').value = data.apellidoPaterno;
                    document.getElementById('<%= txtApellidoMaterno.ClientID %>').value = data.apellidoMaterno;
                } else {
                    // Si no encontramos los datos, mostramos un mensaje y habilitamos los campos para ingresar manualmente
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

            // Ocultar todos los campos
            document.getElementById('grupoDNI').style.display = 'none';
            document.getElementById('grupoCarnet').style.display = 'none';
            document.getElementById('grupoPasaporte').style.display = 'none';

            // Ocultar el botón de "Buscar DNI"
            document.getElementById('<%= btnBuscarDNI.ClientID %>').style.display = 'none';

            // Mostrar solo el campo y botón seleccionado
            if (tipo === "DNI") {
                document.getElementById('grupoDNI').style.display = 'block';
                document.getElementById('<%= btnBuscarDNI.ClientID %>').style.display = 'block';  // Mostrar botón
            } else if (tipo === "Carnet de Extranjería") {
                document.getElementById('grupoCarnet').style.display = 'block';
            } else if (tipo === "Pasaporte") {
                document.getElementById('grupoPasaporte').style.display = 'block';
            }
        }

        // Ejecutar al cargar la página también
        document.addEventListener("DOMContentLoaded", function () {
            mostrarCampoDocumento();
        });
    </script>
</asp:Content>