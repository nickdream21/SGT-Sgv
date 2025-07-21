<%@ Page Title="Registro de Clientes" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroClientes.aspx.cs" Inherits="WebSGV.Views.RegistroClientes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex justify-content-center align-items-center vh-100">
        <div class="card registro-clientes-card">
            <div class="card-header registro-clientes-header text-center">
                <h2 class="registro-header-title">Registro de Clientes</h2>
            </div>
            <div class="card-body">
                <asp:Panel ID="pnlFormulario" runat="server">
                    <!-- RUC (Opcional) -->
                    <div class="form-group">
                        <label for="txtRUC" class="form-label">RUC (Opcional):</label>
                        <div class="input-group">
                            <asp:TextBox ID="txtRUC" runat="server" CssClass="form-control" MaxLength="11"></asp:TextBox>
                            <div class="input-group-append">
                                <asp:Button ID="btnBuscarRUC" runat="server" CssClass="btn btn-secondary" Text="Verificar" OnClientClick="verificarRUC(); return false;" />
                            </div>
                        </div>
                        <small class="form-text text-muted">Ingrese el RUC si lo conoce, o deje en blanco si no lo sabe.</small>
                    </div>

                    <!-- Nombre/Razón Social (Obligatorio) -->
                    <div class="form-group">
                        <label for="txtNombre" class="form-label">Nombre / Razón Social: <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control" required="required"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombre" runat="server" 
                            ControlToValidate="txtNombre" 
                            ErrorMessage="El nombre del cliente es obligatorio." 
                            CssClass="text-danger" 
                            Display="Dynamic">
                        </asp:RequiredFieldValidator>
                    </div>

                    <!-- Botón de Registro -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-lg" Text="Registrar Cliente" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
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
                    // Si encontramos los datos, los mostramos
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