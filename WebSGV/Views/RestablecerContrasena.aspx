<%@ Page Title="Restablecer Contraseña" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RestablecerContrasena.aspx.cs" Inherits="WebSGV.Views.RestablecerContrasena" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RestablecerContrasena.css") %>" rel="stylesheet" />

    <div class="reset-container">
        <h2 class="reset-title">Restablecer contraseña</h2>
        <p class="reset-subtitle">Ingrese su nueva contraseña.</p>

        <asp:Panel ID="pnlMensaje" runat="server" CssClass="alert" Visible="false">
            <asp:Label ID="lblMensaje" runat="server"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlFormulario" runat="server">
            <div class="form-group">
                <label>Nueva contraseña:</label>
                <asp:TextBox ID="txtNuevaContrasena" runat="server" TextMode="Password" CssClass="form-control" placeholder="Ingrese su nueva contraseña"></asp:TextBox>
                <p class="password-requirements">Mínimo 6 caracteres.</p>
            </div>

            <div class="form-group">
                <label>Confirmar contraseña:</label>
                <asp:TextBox ID="txtConfirmarContrasena" runat="server" TextMode="Password" CssClass="form-control" placeholder="Confirme su nueva contraseña"></asp:TextBox>
            </div>

            <asp:Button ID="btnRestablecer" runat="server" CssClass="btn btn-primary" Text="Restablecer contraseña" OnClick="btnRestablecer_Click" />
        </asp:Panel>

        <asp:Button ID="btnVolverLogin" runat="server" CssClass="btn btn-secondary" Text="Volver al login" OnClick="btnVolverLogin_Click" CausesValidation="false" />
    </div>
</asp:Content>