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
                        <label for="<%= ddlCliente.ClientID %>" class="sgv-etiqueta">Cliente: <span class="text-danger">*</span></label>
                        <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control" required="required"></asp:DropDownList>
                        <asp:RequiredFieldValidator ID="rfvCliente" runat="server" 
                            ControlToValidate="ddlCliente"
                            InitialValue="0"
                            ErrorMessage="Debe seleccionar un cliente."
                            CssClass="text-danger" 
                            Display="Dynamic">
                        </asp:RequiredFieldValidator>
                    </div>

                    <!-- Nombre del producto -->
                    <div class="form-group">
                        <label for="<%= txtNombre.ClientID %>" class="sgv-etiqueta">Nombre del producto: <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtNombre" runat="server" CssClass="form-control" required="required"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombre" runat="server" 
                            ControlToValidate="txtNombre" 
                            ErrorMessage="El nombre del producto es obligatorio." 
                            CssClass="text-danger" 
                            Display="Dynamic">
                        </asp:RequiredFieldValidator>
                    </div>

                    <!-- Descripción -->
                    <div class="form-group">
                        <label for="<%= txtDescripcion.ClientID %>" class="sgv-etiqueta">Descripción (opcional):</label>
                        <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                    </div>

                    <!-- Botón -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-lg" Text="Registrar producto" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>