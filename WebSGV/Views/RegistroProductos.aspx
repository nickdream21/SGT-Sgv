<%@ Page Title="Registro de Productos" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroProductos.aspx.cs" Inherits="WebSGV.Views.RegistroProductos" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex justify-content-center align-items-center vh-100">
        <div class="card registro-productos-card">
            <div class="card-header registro-productos-header text-center">
                <h2 class="registro-header-title">Registro de Productos</h2>
            </div>
            <div class="card-body">
                <asp:Panel ID="pnlFormulario" runat="server">
                    <!-- Cliente asociado -->
                    <div class="form-group">
                        <label for="ddlCliente" class="form-label">Cliente: <span class="text-danger">*</span></label>
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
                        <label for="txtNombre" class="form-label">Nombre del Producto: <span class="text-danger">*</span></label>
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
                        <label for="txtDescripcion" class="form-label">Descripción (opcional):</label>
                        <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                    </div>

                    <!-- Botón -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-lg" Text="Registrar Producto" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>