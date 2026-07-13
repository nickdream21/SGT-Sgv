<%@ Page Title="Página No Encontrada" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Error404.aspx.cs" Inherits="WebSGV.Views.Error404" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .error-container {
            max-width: 600px;
            margin: 80px auto;
            text-align: center;
            padding: 40px 20px;
        }
        .error-icon {
            font-size: 5rem;
            color: #f59e0b;
            margin-bottom: 20px;
        }
        .error-title {
            font-size: 1.75rem;
            font-weight: 700;
            color: #1e293b;
            margin-bottom: 10px;
        }
        .error-message {
            font-size: 1rem;
            color: #64748b;
            margin-bottom: 30px;
            line-height: 1.6;
        }
        .error-actions .btn {
            margin: 5px;
        }
        .btn-primary-custom {
            background-color: #2563eb;
            border-color: #2563eb;
            color: white;
            font-weight: 500;
            padding: 10px 24px;
        }
        .btn-secondary-custom {
            background-color: white;
            border: 1px solid #cbd5e1;
            color: #64748b;
            font-weight: 500;
            padding: 10px 24px;
        }
        .error-code {
            font-size: 0.85rem;
            color: #94a3b8;
            margin-top: 30px;
        }
    </style>

    <div class="error-container">
        <div class="error-icon">
            <i class="fas fa-search"></i>
        </div>
        <h1 class="error-title">Página no encontrada</h1>
        <p class="error-message">
            La página que buscas no existe o fue movida.
            <br />
            Verifica la dirección o regresa al inicio.
        </p>
        <div class="error-actions">
            <a href="javascript:history.back()" class="btn btn-secondary-custom">
                <i class="fas fa-arrow-left mr-2"></i>Volver
            </a>
            <a href="/Views/Login.aspx" class="btn btn-primary-custom">
                <i class="fas fa-home mr-2"></i>Ir al Inicio
            </a>
        </div>
        <p class="error-code">
            <i class="fas fa-info-circle mr-1"></i>
            Error 404 — La ruta solicitada no existe
        </p>
    </div>
</asp:Content>