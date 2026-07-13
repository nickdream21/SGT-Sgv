<%@ Page Title="Recuperar Contraseña" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RecuperarContrasena.aspx.cs" Inherits="WebSGV.Views.RecuperarContrasena" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .recovery-container {
            max-width: 500px;
            margin: 50px auto;
            background-color: #f8f9fa;
            border-radius: 8px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.1);
            padding: 30px;
        }

        .recovery-title {
            font-size: 24px;
            margin-bottom: 10px;
            text-align: center;
            font-weight: bold;
        }

        .recovery-subtitle {
            text-align: center;
            color: #666;
            margin-bottom: 25px;
            font-size: 14px;
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

        .btn {
            padding: 12px 20px;
            font-size: 16px;
            border-radius: 4px;
            cursor: pointer;
            width: 100%;
            font-weight: 600;
            border: none;
        }

        .btn-primary {
            background-color: #007bff;
            color: white;
        }

        .btn-primary:hover {
            background-color: #0069d9;
        }

        .btn-secondary {
            background-color: #6c757d;
            color: white;
            margin-top: 10px;
        }

        .btn-secondary:hover {
            background-color: #545b62;
        }

        .alert {
            padding: 12px;
            margin-bottom: 20px;
            border-radius: 4px;
        }

        .alert-success {
            background-color: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }

        .alert-danger {
            background-color: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }
    </style>

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