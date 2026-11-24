<%@ Page Title="Registro de rutas" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegistroRutas.aspx.cs" Inherits="WebSGV.Views.RegistroRutas" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <h1 class="h2">Registro de rutas</h1>
        </div>
    </div>
    <div class="d-flex justify-content-center">
        <div class="card registro-rutas-card">
            <div class="card-body">
                <asp:Label ID="lblMensaje" runat="server" Visible="false" EnableViewState="false" />
                <asp:Panel ID="pnlFormulario" runat="server">
                    <!-- Nombre de la Ruta -->
                    <div class="form-group">
                        <label for="<%= txtNombreRuta.ClientID %>" class="sgv-etiqueta">Nombre de la ruta: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtNombreRuta" runat="server" aria-required="true" CssClass="form-control"></asp:TextBox>
                        <asp:RequiredFieldValidator ID="rfvNombreRuta" runat="server" ValidationGroup="vgRegistroRuta"
                            ControlToValidate="txtNombreRuta"
                            ErrorMessage="El nombre de la ruta es obligatorio."
                            CssClass="sgv-campo-error"
                            Display="Dynamic" SetFocusOnError="true" />
                    </div>

                    <!-- Cliente asociado -->
                    <div class="form-group">
                        <label for="<%= ddlCliente.ClientID %>" class="sgv-etiqueta">Cliente: <span class="sgv-obligatorio" aria-hidden="true">*</span></label>
                        <asp:DropDownList ID="ddlCliente" runat="server" aria-required="true" CssClass="form-control" 
                            DataTextField="nombre" DataValueField="idCliente">
                        </asp:DropDownList>
                        <asp:RequiredFieldValidator ID="rfvCliente" runat="server" ValidationGroup="vgRegistroRuta"
                            ControlToValidate="ddlCliente"
                            InitialValue="0"
                            ErrorMessage="Debe seleccionar un cliente."
                            CssClass="sgv-campo-error"
                            Display="Dynamic" SetFocusOnError="true" />
                        <small class="form-text text-muted">Esta ruta será asignada específicamente al cliente seleccionado.</small>
                    </div>

                    <!-- Descripción -->
                    <div class="form-group">
                        <label for="<%= txtDescripcion.ClientID %>" class="sgv-etiqueta">Descripción (opcional):</label>
                        <asp:TextBox ID="txtDescripcion" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                    </div>

                    <!-- Botón -->
                    <div class="text-center mt-4">
                        <asp:Button ID="btnRegistrar" runat="server" ValidationGroup="vgRegistroRuta" CssClass="btn btn-primary btn-lg" Text="Registrar ruta" OnClick="btnRegistrar_Click" />
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>