<%@ Page Title="Registro de productos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroProductos.aspx.cs" Inherits="WebSGV.Views.RegistroProductos" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Registro de productos</h1>
        </div>
    </div>
    <div class="d-flex justify-content-center">
        <div class="card registro-productos-card">
            <div class="card-body">
                <asp:Label ID="lblMensaje" runat="server" Visible="false" EnableViewState="false" />
                <asp:Panel ID="pnlFormulario" runat="server">
                    <!-- Cliente asociado -->
                    <div class="form-group">
                        <label for="<%= ddlCliente.ClientID %>" class="sgv-etiqueta">Cliente: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:DropDownList ID="ddlCliente" runat="server" aria-required="true" CssClass="form-control"></asp:DropDownList>
                        <asp:RequiredFieldValidator ID="rfvCliente" runat="server" ValidationGroup="vgRegistroProducto"
                            ControlToValidate="ddlCliente"
                            InitialValue="0"
                            ErrorMessage="Debe seleccionar un cliente."
                            CssClass="sgv-campo-error"
                            Display="Dynamic" SetFocusOnError="true" />
                    </div>

                    <!-- Nombre del producto -->
                    <div class="form-group">
                        <label for="<%= txtNombre.ClientID %>" class="sgv-etiqueta">Nombre del producto: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtNombre" runat="server" aria-required="true" CssClass="form-control"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombre" runat="server" ValidationGroup="vgRegistroProducto"
                            ControlToValidate="txtNombre"
                            ErrorMessage="El nombre del producto es obligatorio."
                            CssClass="sgv-campo-error"
                            Display="Dynamic" SetFocusOnError="true" />
                    </div>

                    <!-- Descripción -->
                    <div class="form-group">
                        <label for="<%= txtDescripcion.ClientID %>" class="sgv-etiqueta">Descripción (opcional):</label>
                        <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                    </div>

                    <!-- Botón -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" ValidationGroup="vgRegistroProducto" CssClass="btn btn-primary btn-lg" Text="Registrar producto" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>