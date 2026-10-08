<%@ Page Title="Iniciar Sesión" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="WebSGV.Views.WebForm1" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Login.css") %>" rel="stylesheet" />
    <div class="main-container">
        <img src="<%= ResolveUrl("~/Content/favicon.png") %>" alt="Logo SGV" class="logo" />
        <h1 class="header">Sistema de Gestión de Transporte</h1>
        <p class="subheader">Bienvenido al sistema <strong>Servicios Generales Viviana (SGV)</strong>. Inicia sesión para continuar.</p>
        <div class="login-container">
            <h2 class="title">Iniciar Sesión</h2>

            <asp:Panel ID="pnlError" runat="server" Visible="false" CssClass="login-error">
                <i class="fas fa-exclamation-circle"></i>
                <asp:Label ID="lblError" runat="server"></asp:Label>
            </asp:Panel>

            <div class="form-group">
                <label for="<%= txtUsername.ClientID %>">Usuario:</label>
                <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" placeholder="Ingrese su usuario" autocomplete="username"></asp:TextBox>
            </div>
            <div class="form-group">
                <label for="<%= txtPassword.ClientID %>">Contraseña:</label>
                <div class="password-wrapper">
                    <asp:TextBox ID="txtPassword" runat="server" TextMode="Password" CssClass="form-control" placeholder="Ingrese su contraseña" autocomplete="current-password"></asp:TextBox>
                    <button type="button" class="btn-toggle-pass" onclick="togglePassword()" title="Mostrar/ocultar contraseña" tabindex="-1" aria-label="Mostrar u ocultar contraseña">
                        <i id="iconTogglePass" class="fas fa-eye"></i>
                    </button>
                </div>
            </div>
            <div class="form-group checkbox">
                <asp:CheckBox ID="chkRemember" runat="server" />
                <label for="<%= chkRemember.ClientID %>">Recordarme</label>
            </div>
            <asp:Button ID="btnLogin" runat="server" CssClass="login-btn" Text="Acceder" OnClick="btnLogin_Click" />
            <div class="forgot-password">
                <a href="RecuperarContrasena.aspx">¿Olvidó su contraseña?</a>
            </div>
        </div>
    </div>

    <script>
        // Valores del servidor que usa el script de la página (ids de controles ASP.NET).
        var SGV = Object.assign(window.SGV || {}, {
            txtPassword: '<%= txtPassword.ClientID %>',
            txtUsername: '<%= txtUsername.ClientID %>'
        });
    </script>
    <script src="<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/Login.js") %>"></script>
</asp:Content>
