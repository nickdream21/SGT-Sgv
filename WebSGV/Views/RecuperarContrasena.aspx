<%@ Page Title="Recuperar Contraseña" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RecuperarContrasena.aspx.cs" Inherits="WebSGV.Views.RecuperarContrasena" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/RecuperarContrasena.css") %>" rel="stylesheet" />

    <div class="recovery-container">
        <h2 class="recovery-title">Recuperar Contraseña</h2>
        <p class="recovery-subtitle">Ingrese su usuario o correo electrónico y le enviaremos un enlace para restablecer su contraseña.</p>

        <asp:Panel ID="pnlMensaje" runat="server" CssClass="alert" Visible="false">
            <asp:Label ID="lblMensaje" runat="server"></asp:Label>
        </asp:Panel>

        <div class="form-group">
            <label for="txtUsuarioEmail">Usuario o Correo Electrónico:</label>
            <asp:TextBox ID="txtUsuarioEmail" runat="server" CssClass="form-control" placeholder="usuario o email@ejemplo.com"></asp:TextBox>
        </div>

        <asp:Button ID="btnEnviar" runat="server" CssClass="btn btn-primary" Text="Enviar Enlace de Recuperación" OnClick="btnEnviar_Click" />
        <asp:Button ID="btnVolver" runat="server" CssClass="btn btn-secondary" Text="Volver al Login" OnClick="btnVolver_Click" CausesValidation="false" />
    </div>
</asp:Content>