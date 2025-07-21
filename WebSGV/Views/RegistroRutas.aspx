<%@ Page Title="Registro de Rutas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroRutas.aspx.cs" Inherits="WebSGV.Views.RegistroRutas" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="d-flex justify-content-center align-items-center vh-100">
        <div class="card registro-rutas-card">
            <div class="card-header registro-rutas-header text-center">
                <h2 class="registro-header-title">Registro de Rutas</h2>
            </div>
            <div class="card-body">
                <asp:Panel ID="pnlFormulario" runat="server">
                    <!-- Nombre de la Ruta -->
                    <div class="form-group">
                        <label for="txtNombreRuta" class="form-label">Nombre de la Ruta: <span class="text-danger">*</span></label>
                        <asp:TextBox ID="txtNombreRuta" runat="server" CssClass="form-control" required="required"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombreRuta" runat="server" 
                            ControlToValidate="txtNombreRuta" 
                            ErrorMessage="El nombre de la ruta es obligatorio." 
                            CssClass="text-danger" 
                            Display="Dynamic">
                        </asp:RequiredFieldValidator>
                    </div>

                    <!-- Cliente asociado -->
                    <div class="form-group">
                        <label for="ddlCliente" class="form-label">Cliente: <span class="text-danger">*</span></label>
                        <asp:DropDownList ID="ddlCliente" runat="server" CssClass="form-control" 
                            DataTextField="nombre" DataValueField="idCliente" required="required">
                        </asp:DropDownList>
                        <asp:RequiredFieldValidator ID="rfvCliente" runat="server" 
                            ControlToValidate="ddlCliente"
                            InitialValue="0"
                            ErrorMessage="Debe seleccionar un cliente."
                            CssClass="text-danger" 
                            Display="Dynamic">
                        </asp:RequiredFieldValidator>
                        <small class="form-text text-muted">Esta ruta será asignada específicamente al cliente seleccionado.</small>
                    </div>

                    <!-- Descripción -->
                    <div class="form-group">
                        <label for="txtDescripcion" class="form-label">Descripción (opcional):</label>
                        <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                    </div>

                    <!-- Botón -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" CssClass="btn btn-primary btn-lg" Text="Registrar Ruta" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>