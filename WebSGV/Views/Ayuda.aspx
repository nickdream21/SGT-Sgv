<%@ Page Title="Ayuda" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Ayuda.aspx.cs" Inherits="WebSGV.Views.Ayuda" EnableViewState="false" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="sgv-encabezado">
        <div class="sgv-encabezado-titulo">
            <% if (TemaActual != null) { %>
            <a class="sgv-volver" href="<%: ResolveUrl("~/Views/Ayuda.aspx") %>">Volver a los temas de ayuda</a>
            <% } %>
            <h1 class="h2">Ayuda</h1>
            <p>C&#xF3;mo se usa cada pantalla del sistema</p>
        </div>
    </div>

    <%-- Tema pedido que el rol no puede abrir, borrador o inexistente: el mismo aviso (no dice si existe). --%>
    <asp:Panel ID="pnlNoDisponible" runat="server" Visible="false" CssClass="alert alert-warning" role="status">
        <span id="ayudaAviso">Ese tema no est&#xE1; disponible para su usuario.</span>
    </asp:Panel>

    <%-- Un tema: el fragmento es un archivo del repositorio validado por AyudaTests (lista blanca de HTML),
         nunca texto de un usuario. El modal de Site.Master toma #ayudaContenido y su data-titulo. --%>
    <asp:PlaceHolder ID="phTema" runat="server" Visible="false">
        <section class="sgv-panel">
            <div class="sgv-panel-cab">
                <h2 class="sgv-panel-titulo"><%: TemaActual?.Titulo %></h2>
                <% if (UrlVideo != null) { %>
                <a class="btn btn-outline-primary btn-sm" href="<%: UrlVideo %>" target="_blank" rel="noopener">Ver video</a>
                <% } %>
            </div>
            <div class="sgv-panel-cuerpo">
                <article id="ayudaContenido" class="sgv-ayuda-cuerpo" data-titulo="<%: TemaActual?.Titulo %>">
                    <asp:Literal ID="litFragmento" runat="server" Mode="PassThrough" />
                </article>
            </div>
        </section>
    </asp:PlaceHolder>

    <%-- Índice: los temas que ve el rol, por sección. --%>
    <asp:PlaceHolder ID="phIndice" runat="server" Visible="false">
        <section class="sgv-panel">
            <div class="sgv-panel-cab">
                <h2 class="sgv-panel-titulo">Temas de ayuda</h2>
            </div>
            <div class="sgv-panel-cuerpo sgv-ayuda-indice">
                <asp:Literal ID="litIndice" runat="server" Mode="PassThrough" />
            </div>
        </section>
    </asp:PlaceHolder>
</asp:Content>
