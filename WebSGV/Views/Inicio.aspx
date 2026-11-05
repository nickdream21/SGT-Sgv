<%@ Page Title="Inicio" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Inicio.aspx.cs" Inherits="WebSGV.Views.Inicio" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%= WebSGV.Helpers.RecursoHelper.Url("~/Content/paginas/Inicio.css") %>" rel="stylesheet" />
    <div class="inicio-container">
        <img src="/Content/favicon.png" alt="Logo SGV" class="logo">
        <h1 class="inicio-titulo">Bienvenido al Sistema SGV</h1>
        <p class="inicio-descripcion">
            Gestiona de manera eficiente tus operaciones de transporte y logística.
        </p>
        <%-- Cargas registradas que esperan factura, guía o CPIC: la bandeja de documentos las muestra por
             cliente y pedido y es donde se suben (antes, ListaDespachos con el filtro "Documentos"). --%>
        <asp:HyperLink ID="lnkDocumentosPendientes" runat="server" Visible="false"
            NavigateUrl="~/Views/BandejaDocumentos.aspx" CssClass="inicio-aviso">
            <i class="fas fa-file-invoice"></i>
            <asp:Label ID="lblDocumentosPendientes" runat="server"></asp:Label>
        </asp:HyperLink>
    </div>
</asp:Content>
