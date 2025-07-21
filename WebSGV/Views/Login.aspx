<%@ Page Title="Iniciar Sesión" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="WebSGV.Views.WebForm1" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
        <!-- Estilos específicos para arreglar la página de login -->
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
    </style>
    <div class="main-container">
        <img src="/Content/favicon.png" alt="Logo" class="logo" />
        <h1 class="header">Sistema de Gestión de Transporte</h1>
        <p class="subheader">Bienvenido al sistema <strong>Servicios Generales Viviana (SGV)</strong>. Inicia sesión para continuar.</p>
        <div class="login-container">
            <h2 class="title">Iniciar Sesión</h2>
            <div class="form-group">
                <label for="username">Usuario:</label>
                <asp:TextBox ID="txtUsername" runat="server" CssClass="form-control" placeholder="Ingrese su usuario"></asp:TextBox>
            </div>
            <div class="form-group">
                <label for="password">Contraseña:</label>
                <asp:TextBox ID="txtPassword" runat="server" TextMode="Password" CssClass="form-control" placeholder="Ingrese su contraseña"></asp:TextBox>
            </div>
            <div class="form-group checkbox">
                <asp:CheckBox ID="chkRemember" runat="server" />
                <label for="chkRemember">Recordarme</label>
            </div>
            <asp:Button ID="btnLogin" runat="server" CssClass="login-btn" Text="Acceder"  OnClick="btnLogin_Click" />
        </div>
    </div>
</asp:Content>
