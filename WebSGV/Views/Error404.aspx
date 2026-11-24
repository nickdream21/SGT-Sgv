<%@ Page Title="Página no encontrada" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Error404.aspx.cs" Inherits="WebSGV.Views.Error404" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .error-container {
            max-width: 600px;
            margin: 80px auto;
            text-align: center;
            padding: 40px 20px;
        }
        .error-title {
            font-size: 1.75rem;
            font-weight: 700;
            color: #1a2230;
            margin-bottom: 10px;
        }
        .error-message {
            font-size: 1rem;
            color: #566173;
            margin-bottom: 30px;
            line-height: 1.6;
        }
        .error-actions .btn {
            margin: 5px;
        }
        .error-actions .btn-primary {
            background-color: #1f3a6e;
            border-color: #1f3a6e;
            color: white;
            font-weight: 500;
            padding: 10px 24px;
        }
        .error-actions .btn-outline-secondary {
            background-color: white;
            border: 1px solid #a8b0bc;
            color: #566173;
            font-weight: 500;
            padding: 10px 24px;
        }
        .error-code {
            font-size: 0.85rem;
            color: #566173;
            margin-top: 30px;
        }
    </style>

    <div class="error-container">
        <h1 class="error-title">Página no encontrada</h1>
        <p class="error-message">
            La página que buscas no existe o fue movida.
            <br />
            Verifica la dirección o regresa al inicio.
        </p>
        <div class="error-actions">
            <a href="javascript:history.back()" class="btn btn-outline-secondary">
                Volver
            </a>
            <a href="/Views/Login.aspx" class="btn btn-primary">
                Ir al inicio
            </a>
        </div>
        <p class="error-code">
            Error 404 — La ruta solicitada no existe
        </p>
    </div>
</asp:Content>