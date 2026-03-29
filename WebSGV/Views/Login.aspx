<%@ Page Title="Iniciar Sesión" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="WebSGV.Views.WebForm1" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .main-container {
            max-width: 800px;
            margin: 40px auto;
            text-align: center;
            padding: 20px;
        }

        .logo {
            max-width: 200px;
            margin-bottom: 20px;
        }

        .header {
            font-size: 28px;
            margin-bottom: 10px;
            font-weight: bold;
        }

        .subheader {
            font-size: 16px;
            margin-bottom: 30px;
            color: #666;
        }

        .login-container {
            background-color: #f8f9fa;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
            padding: 30px;
            max-width: 500px;
            margin: 0 auto;
            text-align: left;
        }

        .title {
            font-size: 24px;
            margin-bottom: 20px;
            text-align: center;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
        }

        .form-control {
            width: 100%;
            padding: 10px 15px;
            font-size: 16px;
            border: 1px solid #ced4da;
            border-radius: 4px;
        }

        .checkbox {
            display: flex;
            align-items: center;
        }

        .checkbox input {
            margin-right: 10px;
        }

        .login-btn {
            background-color: #007bff;
            color: white;
            border: none;
            padding: 12px 20px;
            font-size: 16px;
            border-radius: 4px;
            cursor: pointer;
            width: 100%;
            font-weight: 600;
        }

        .login-btn:hover {
            background-color: #0069d9;
        }

        .forgot-password {
            text-align: center;
            margin-top: 15px;
        }

        .forgot-password a {
            color: #007bff;
            text-decoration: none;
        }

        .forgot-password a:hover {
            text-decoration: underline;
        }

        .password-wrapper {
            position: relative;
        }

        .password-wrapper .form-control {
            padding-right: 42px;
        }

        /* Ocultar el botón nativo de revelar contraseña en Edge/Chromium */
        input[type="password"]::-ms-reveal,
        input[type="password"]::-ms-clear {
            display: none;
        }

        .btn-toggle-pass {
            position: absolute;
            right: 10px;
            top: 50%;
            transform: translateY(-50%);
            background: none;
            border: none;
            cursor: pointer;
            color: #6c757d;
            padding: 0;
            font-size: 15px;
            line-height: 1;
        }

        .btn-toggle-pass:hover {
            color: #343a40;
        }

        .login-error {
            background-color: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
            border-radius: 4px;
            padding: 10px 15px;
            margin-bottom: 20px;
            font-size: 14px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .login-error i {
            font-size: 16px;
            flex-shrink: 0;
        }
    </style>
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

    <script type="text/javascript">
        function togglePassword() {
            var input = document.getElementById('<%= txtPassword.ClientID %>');
            var icon = document.getElementById('iconTogglePass');
            if (input.type === 'password') {
                input.type = 'text';
                icon.classList.replace('fa-eye', 'fa-eye-slash');
            } else {
                input.type = 'password';
                icon.classList.replace('fa-eye-slash', 'fa-eye');
            }
        }

        // Autofocus en el primer campo vacío
        (function () {
            var user = document.getElementById('<%= txtUsername.ClientID %>');
            var pass = document.getElementById('<%= txtPassword.ClientID %>');
            if (user && !user.value) {
                user.focus();
            } else if (pass) {
                pass.focus();
            }
        })();
    </script>
</asp:Content>
